using System.Collections;
using Benjathemaker;
using UnityEngine;

// Spawned by StatDropManager in place of the old instant-apply. Three beats, in order:
//   1. The player stops dead (idle) while the potion (a RedVial for ATK, GreenVial for HP -
//      see SpawnVisual) is tossed past the monster and bounces to a rest on the ground.
//   2. The moment it settles, the player is released back into motion.
//   3. The potion closes the remaining gap at ground level and pays out on contact.
// The player never actually walks anywhere in this game (CombatLoop's "running" is a fixed
// in-place animation - see GroundScroller/BackdropScroller for the illusion of movement), so
// beat 3 has to move the potion instead: it slides in at exactly the pace monsters walk at,
// flat along the floor and at a constant speed, which is the world coming toward a running
// player. Deliberately NOT a homing/magnet pull - nothing accelerates toward the player and
// nothing lifts off the ground, because the read is the player running over it, not the
// potion flying to them.
public class StatPotionPickup : MonoBehaviour
{
    [Header("Toss + bounce (beat 1)")]
    // The whole toss-and-settle, start to rest.
    [SerializeField] private float landDuration = 0.75f;
    // How far past the death point it's thrown, directly away from the player - the monster
    // dies between the two, so this is what puts the potion down "behind" it.
    [SerializeField] private float tossDistance = 1.4f;
    // Peak of the opening arc. Every rebound after it is a fraction of this (see HopHeights).
    [SerializeField] private float tossHeight = 1.1f;

    [Header("Run-over (beats 2-3)")]
    // Beat 2: the settled potion sits untouched this long. GroundScroller/BackdropScroller
    // ease their scroll in over ~0.4s rather than snapping to full speed, so the player is
    // still getting up to pace for a moment after being released - the potion holding still
    // through that is what keeps the two reading as the same motion.
    [SerializeField] private float settleHoldDuration = 0.2f;
    // Close enough to count as underfoot. Compared flat (XZ only): the potion rests on the
    // floor while the player's transform is their capsule's centre, so a true 3D distance
    // would never get this small.
    [SerializeField] private float pickupRadius = 0.45f;
    // Backstop only. At approach speed nothing should ever take this long; without it a null
    // or unreachable player would leave the potion sliding forever.
    [SerializeField] private float approachTimeout = 6f;

    // Set from StageConfigSO.monsterMoveSpeed by StatDropManager - the potion has to close
    // distance at the same rate a monster walking in does, or the two read as the player
    // running at two different speeds.
    private float approachSpeed = 5f;

    [Header("Visual (per stat type)")]
    // ATK drops as a RedVial, HP as a GreenVial - the vial's own color is left completely
    // untouched (no grade tint/emission on it) so ATK vs HP stays readable at a glance no
    // matter the grade. Rarity lives entirely in the aura below instead.
    [SerializeField] private GameObject atkVisualPrefab;
    [SerializeField] private GameObject hpVisualPrefab;
    // Both vials measure ~2.23 tall at scale 1 (much taller/thinner than the old icon mesh) -
    // this is the "Normal" size the grade ramp (GetPotionScale) then scales up from.
    [SerializeField] private float visualBaseScale = 0.4f;

    [Header("Grade aura")]
    // All three aura layers are driven by GradeVisuals.GetAuraStrength (0.2 at Normal, 1 at
    // Legendary) multiplied into these ceilings, so grade reads as depth of glow rather than
    // size alone. Size is deliberately NOT part of the ramp - the potion mesh already grows
    // per grade (GetPotionScale), and growing the halo on top of that doubled up.
    [SerializeField] private float auraSize = 1.5f;
    // Additive halo brightness. Multiplied into the grade color, so a Normal drop is a faint
    // white wash and a Legendary one a solid green bloom.
    [SerializeField] private float auraBrightnessMax = 1.7f;
    // The potion also throws colored light onto the ground around it - this is what makes it
    // read as *emitting* an aura rather than just having a decal pasted behind it.
    [SerializeField] private float auraLightIntensityMax = 3f;
    [SerializeField] private float auraLightRange = 2.6f;

    [Header("Twinkle sparkles")]
    // Star glints orbiting the vial, each blinking on its own offset phase so something is
    // always catching the light - a single pulsing halo reads as "glowing", this reads as
    // "반짝반짝". Staggered rather than synchronised: four stars flashing in unison would
    // look like one flashing light, not a shimmer.
    [SerializeField] private int sparkleCount = 4;
    // In root-local units, so the ring widens with the potion's own grade scale.
    [SerializeField] private float sparkleOrbitRadius = 0.5f;
    [SerializeField] private float sparkleSize = 0.6f;
    // Additive stars need more punch than the soft halo to register as a glint at this size.
    [SerializeField] private float sparkleBrightnessMax = 2.4f;
    // Full blink cycles per second, per star.
    [SerializeField] private float sparkleBlinkSpeed = 1.1f;
    // Slow drift of the whole ring, so the glints aren't pinned to fixed screen positions.
    [SerializeField] private float sparkleOrbitSpeed = 35f;

    // Successive hops: the opening toss, then decaying rebounds. Each is a sine arc leaving
    // and returning to exactly ground level, so the run reads as unbroken contact instead of
    // a shape that snaps off the floor between bounces. Durations sum to 1.
    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private Renderer[] renderers;
    private StatType statType;
    private StatGrade grade;
    private float amount;
    private Transform player;
    private CombatLoop combatLoop;
    // Tracks whether PushIdleHold has been called and not yet matched by PopIdleHold - needed
    // because HandlePlayerDied below can cut TossThenRunOver off with StopAllCoroutines at any
    // point in its three beats, including mid-toss before it ever reaches its own PopIdleHold
    // call. Without this, dying mid-toss would strand CombatLoop's idle hold permanently on,
    // and the player would never be allowed to run again even after respawning.
    private bool idleHoldActive;
    private Vector3 restScale;
    // Pivot-to-mesh-bottom distance at restScale, measured rather than guessed - a bigger
    // grade grows the mesh around its pivot in both directions, so a fixed lift sinks a
    // large potion's bottom below the floor by exactly however much it grew. Measuring this
    // per-instance keeps the bottom flush with the ground at every size, with no per-grade
    // constant to hand-tune.
    private float restBottomOffset;
    // Built in code per instance (the tint differs per grade), so nothing else will collect it
    // when this object goes away - see OnDestroy.
    private Material auraMaterial;
    // One material shared by all of this potion's sparkles - same reason as auraMaterial, it's
    // built in code so nothing else will collect it (see OnDestroy).
    private Material sparkleMaterial;

    public void Initialize(StatType statType, StatGrade grade, float amount, Transform player, CombatLoop combatLoop, float approachSpeed)
    {
        this.statType = statType;
        this.grade = grade;
        this.amount = amount;
        this.player = player;
        this.combatLoop = combatLoop;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;

        SpawnVisual();
        // The prefab's authored scale is the Normal size - rarer grades read as visibly
        // bigger potions, not just a different color, on top of that.
        restScale = transform.localScale * GradeVisuals.GetPotionScale(grade);

        transform.localScale = restScale;
        restBottomOffset = ComputeBottomOffset();

        // Strictly after ComputeBottomOffset: the halo quad extends well below the vial, so a
        // renderer measured into those bounds would push the whole potion up off the ground by
        // the height of its own glow.
        SpawnAura();

        // Beat 1 begins here: CombatLoop.Update() reads this and holds the player in idle
        // rather than letting them run on while the potion is still in the air.
        if (combatLoop != null)
        {
            combatLoop.PushIdleHold();
            idleHoldActive = true;
        }

        StartCoroutine(TossThenRunOver());
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    // A kill and the player's own death can land in the same beat - the monster that just
    // dropped this dies to the same hit that kills the player. Left alone, this pickup's
    // coroutine runs on completely independent of StageManager's death sequence (its
    // StopAllCoroutines only reaches its own coroutines, not this object's), so the potion
    // kept sliding into a player who was already dead and even paid out its stat afterward.
    // Freezing it in place here is what the death sequence actually shows instead.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }

    // Distance from this transform's pivot down to the bottom of its rendered mesh, at
    // whatever scale is currently applied. Combines every renderer in case the visual has
    // more than one part.
    private float ComputeBottomOffset()
    {
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        return transform.position.y - combined.min.y;
    }

    // Instantiates the RedVial/GreenVial matching this drop's stat type as a child - the
    // asset itself now carries that identity instead of a shared icon mesh tinted by hand.
    private void SpawnVisual()
    {
        GameObject prefab = statType == StatType.Attack ? atkVisualPrefab : hpVisualPrefab;
        if (prefab == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            return;
        }

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * visualBaseScale;

        // This asset ships with its own idle rotate/float/scale loop - PlayToss/PlayRunOver
        // below already drive this object's position and scale every frame, so the two would
        // fight over the same transform if this were left running.
        SimpleGemsAnim anim = visual.GetComponent<SimpleGemsAnim>();
        if (anim != null) Destroy(anim);

        renderers = visual.GetComponentsInChildren<Renderer>();
    }

    // Two layers on one billboarded object: an additive halo quad (the visible glow) and a
    // point light (colored light thrown onto the ground). Both take their color from the grade
    // and their strength from GetAuraStrength.
    private void SpawnAura()
    {
        float strength = GradeVisuals.GetAuraStrength(grade);
        Color color = GradeVisuals.GetColor(grade);

        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
        aura.name = "GradeAura";
        // CreatePrimitive ships a collider; this is a purely visual object and nothing here
        // uses physics (pickup is a distance check, see PlayRunOver).
        Destroy(aura.GetComponent<Collider>());

        aura.transform.SetParent(transform, false);
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localScale = Vector3.one * auraSize;

        auraMaterial = CreateAdditiveGlowMaterial(color * (auraBrightnessMax * strength));
        aura.GetComponent<MeshRenderer>().material = auraMaterial;

        Light light = aura.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = auraLightIntensityMax * strength;
        light.range = auraLightRange;
        // Shadows off: this is a glow, and a small prop casting real shadows over the fight
        // would draw far more attention than "은은하게" allows (and costs a shadow map).
        light.shadows = LightShadows.None;

        aura.AddComponent<Billboard>();
        aura.AddComponent<StatPotionAuraMotion>();

        SpawnSparkles(color, strength);
    }

    // A ring of blinking star glints. All of them share one material (same tint), so the
    // per-star twinkle is driven purely by transform scale - independent phases without
    // needing one material instance per star to animate a color on.
    private void SpawnSparkles(Color color, float strength)
    {
        if (sparkleCount <= 0) return;

        sparkleMaterial = CreateAdditiveGlowMaterial(color * (sparkleBrightnessMax * strength), SparkleTexture);

        var ring = new GameObject("SparkleRing");
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.AddComponent<StatPotionSparkleRing>().Initialize(sparkleOrbitSpeed);

        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject sparkle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sparkle.name = "Sparkle" + i;
            Destroy(sparkle.GetComponent<Collider>());

            float angle = (360f / sparkleCount) * i;
            sparkle.transform.SetParent(ring.transform, false);
            sparkle.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * (Vector3.up * sparkleOrbitRadius);
            sparkle.transform.localScale = Vector3.one * sparkleSize;

            sparkle.GetComponent<MeshRenderer>().material = sparkleMaterial;

            sparkle.AddComponent<Billboard>();
            // Phase spread across the ring: star i starts 1/count of a cycle behind star i-1,
            // so the blinks chase each other around instead of firing together.
            sparkle.AddComponent<StatPotionSparkle>()
                   .Initialize(sparkleSize, sparkleBlinkSpeed, (float)i / sparkleCount);
        }
    }

    private static Material CreateAdditiveGlowMaterial(Color tint, Texture2D texture = null)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        // Additive transparent: URP needs the surface/blend properties AND the matching
        // keyword/queue set by hand, since this material is built in code rather than through
        // the shader GUI that normally keeps those in sync.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        // Both faces: Billboard orients local +Z away from the camera, so which side of the
        // quad ends up facing the viewer depends on that convention - drawing both sides makes
        // the halo visible regardless.
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetTexture("_BaseMap", texture != null ? texture : GlowTexture);
        mat.SetColor("_BaseColor", tint);

        return mat;
    }

    // Four-point star glint, shared by every sparkle on every potion. A soft radial blob
    // (GlowTexture) reads as a lamp; the cross-shaped falloff below is what actually reads as
    // a twinkle at this size.
    private static Texture2D sparkleTexture;
    private static Texture2D SparkleTexture
    {
        get
        {
            if (sparkleTexture != null) return sparkleTexture;

            const int size = 64;
            sparkleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Normalized to the quad's half-extent, so the spikes reach its edges.
                    float dx = Mathf.Abs(x - center) / center;
                    float dy = Mathf.Abs(y - center) / center;

                    // Two crossed needles: each arm stays bright along its own axis while
                    // falling off sharply across it, which is what gives the star its points
                    // instead of a diamond.
                    float horizontal = Falloff(dx) * Needle(dy);
                    float vertical = Falloff(dy) * Needle(dx);
                    // Round core so the arms meet in a bright centre rather than a seam.
                    float core = Falloff(Mathf.Sqrt(dx * dx + dy * dy) * 2.6f);

                    float v = Mathf.Clamp01(horizontal + vertical + core);
                    pixels[y * size + x] = new Color(v, v, v, v);
                }
            }

            sparkleTexture.SetPixels(pixels);
            sparkleTexture.Apply();
            return sparkleTexture;
        }
    }

    // Smooth 1->0 over [0,1], squared for a tighter falloff.
    private static float Falloff(float d)
    {
        float f = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
        return f * f;
    }

    // Very tight falloff across an arm's width - anything off the axis dies almost at once.
    private static float Needle(float d) => Mathf.Clamp01(1f - Mathf.Clamp01(d) * 14f);

    // One soft radial falloff shared by every potion ever dropped - the texture is identical
    // for all of them, only the tint differs (that lives on the per-instance material).
    private static Texture2D glowTexture;
    private static Texture2D GlowTexture
    {
        get
        {
            if (glowTexture != null) return glowTexture;

            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Squared smoothstep: bright core falling off to nothing well inside the
                    // quad's edge, so the halo never shows a hard square boundary.
                    float falloff = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(r));
                    falloff *= falloff;

                    pixels[y * size + x] = new Color(falloff, falloff, falloff, falloff);
                }
            }

            glowTexture.SetPixels(pixels);
            glowTexture.Apply();
            return glowTexture;
        }
    }

    private IEnumerator TossThenRunOver()
    {
        yield return PlayToss();

        // Beat 2: the toss owned "stay put"; releasing it drops the player straight back into
        // CombatLoop's default "nothing in range, keep moving" state.
        if (combatLoop != null) combatLoop.PopIdleHold();
        idleHoldActive = false;

        yield return new WaitForSeconds(settleHoldDuration);
        yield return PlayRunOver();

        Collect();
    }

    // Arcs away from the player and bounces down to a stop, all inside landDuration.
    private IEnumerator PlayToss()
    {
        Vector3 startPos = transform.position;
        float groundY = ResolveGroundY() + restBottomOffset;

        // Straight back along the player-to-potion line, so wherever the monster died the
        // potion always lands on the far side of it rather than off to one side.
        Vector3 away = Vector3.right;
        if (player != null)
        {
            Vector3 delta = startPos - player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f) away = delta.normalized;
        }

        Vector3 landingPos = startPos + away * tossDistance;
        landingPos.y = groundY;

        transform.localScale = Vector3.zero;

        float t = 0f;
        while (t < landDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / landDuration);

            // Pops to full size over the opening arc, not the whole sequence - it should be
            // fully formed by the time it first hits the ground.
            transform.localScale = restScale * Mathf.Clamp01(p / 0.3f);

            // Decelerating: most of the ground is covered by the first hop, the rebounds
            // barely creep forward - which is what a thrown object shedding speed looks like.
            Vector3 flat = Vector3.Lerp(startPos, landingPos, EaseOutQuad(p));
            flat.y = Mathf.Lerp(startPos.y, groundY, EaseOutQuad(p)) + tossHeight * HopHeight(p);
            transform.position = flat;

            yield return null;
        }

        transform.localScale = restScale;
        transform.position = landingPos;
    }

    // Ground-locked approach at a flat, constant speed - the world sliding past a running
    // player. Runs until the potion is underfoot rather than for a fixed duration, so the
    // distance it actually has to cover sets how long the run takes.
    private IEnumerator PlayRunOver()
    {
        float restY = transform.position.y;
        float elapsed = 0f;

        while (elapsed < approachTimeout)
        {
            elapsed += Time.deltaTime;
            if (player == null) yield break;

            Vector3 pos = transform.position;

            // Flattened: the potion tracks the player's position on the floor plane and stays
            // at its own resting height the whole way, so it slides rather than rises.
            Vector3 toPlayer = player.position - pos;
            toPlayer.y = 0f;

            if (toPlayer.magnitude <= pickupRadius) yield break;

            pos += toPlayer.normalized * approachSpeed * Time.deltaTime;
            pos.y = restY;
            transform.position = pos;

            yield return null;
        }
    }

    // The player's own collider bottom is the ground line everything else in this game snaps
    // to (Monster.SpawnUltimateImpactVfx does exactly this) - there's no ground collider to
    // raycast against. Falling back to the spawn height keeps the toss sane if it's missing.
    private float ResolveGroundY()
    {
        if (player == null) return transform.position.y;

        Collider playerCollider = player.GetComponent<Collider>();
        return playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
    }

    // Normalized height across the hop sequence: 1 at the opening arc's peak, 0 at every
    // ground contact and at rest.
    private static float HopHeight(float p)
    {
        float cursor = 0f;

        for (int i = 0; i < HopDurations.Length; i++)
        {
            float span = HopDurations[i];
            if (p < cursor + span || i == HopDurations.Length - 1)
            {
                float local = Mathf.Clamp01((p - cursor) / span);
                return HopHeights[i] * Mathf.Sin(local * Mathf.PI);
            }
            cursor += span;
        }

        return 0f;
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    private void Collect()
    {
        PlayerCharacter pc = player != null ? player.GetComponent<PlayerCharacter>() : null;
        if (pc != null)
        {
            if (statType == StatType.Attack) pc.IncreaseAttack(amount);
            else pc.IncreaseMaxHp(amount);
        }

        // Drives both the existing "+N STAT" popup and the player's buff aura VFX - the
        // pickup moment is now what those react to instead of the kill itself. Deliberately
        // no pose here: the player is mid-run (see PlayRunOver) and should stay that way -
        // eating this is a beat in the run, not a stop for it.
        GameEvents.RaiseStatDropGained(grade, statType, amount);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
        if (sparkleMaterial != null) Destroy(sparkleMaterial);
    }
}

// The aura's own fade-in and idle pulse, kept separate so StatPotionPickup stays focused on
// the toss/run-over sequence - and so the glow keeps breathing on its own through every beat
// of that sequence without it having to drive the effect frame by frame.
public class StatPotionAuraMotion : MonoBehaviour
{
    // Matches the potion's own pop-in, so the glow arrives with the vial rather than snapping
    // on at full strength while the mesh is still scaling up.
    [SerializeField] private float fadeInDuration = 0.25f;
    // Continuous breathing. Deeper than the original 0.14 - at that amplitude the pulse was
    // technically running but invisible across the potion's ~1.5s life. Still a swell rather
    // than a strobe; the sharp "반짝" is the sparkles' job (see StatPotionSparkle).
    [SerializeField] private float pulseAmplitude = 0.3f;
    [SerializeField] private float pulseSpeed = 3f;

    private Material material;
    private Light auraLight;
    private Color baseTint;
    private float baseIntensity;
    private float elapsed;

    private void Awake()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        // sharedMaterial, not material: SpawnAura already assigned a material built for this
        // one potion, and the instancing getter would hand back a *copy* that StatPotionPickup's
        // OnDestroy doesn't know about and therefore never cleans up.
        if (meshRenderer != null) material = meshRenderer.sharedMaterial;
        auraLight = GetComponent<Light>();

        if (material != null) baseTint = material.GetColor("_BaseColor");
        if (auraLight != null) baseIntensity = auraLight.intensity;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float fade = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
        float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed * Mathf.PI) * pulseAmplitude;
        float factor = fade * pulse;

        if (material != null) material.SetColor("_BaseColor", baseTint * factor);
        if (auraLight != null) auraLight.intensity = baseIntensity * factor;
    }
}

// Slowly rotates the whole sparkle ring. Separate from the individual blinks so the glints
// drift around the potion instead of sitting at fixed screen positions the entire time.
public class StatPotionSparkleRing : MonoBehaviour
{
    private float degreesPerSecond;

    public void Initialize(float degreesPerSecond) => this.degreesPerSecond = degreesPerSecond;

    private void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}

// One star glint's blink. Drives localScale only - every sparkle on a potion shares a single
// material, so animating scale is what lets each one blink on its own phase without needing a
// material instance per star.
public class StatPotionSparkle : MonoBehaviour
{
    // Raising the sine to a high power turns a smooth wave into a narrow spike: a quick flash
    // with a long dark gap, which is what separates a twinkle from a throb.
    private const float BlinkSharpness = 5f;
    // Never fully vanishes; a star popping in from literally nothing reads as a glitch.
    private const float MinScaleFactor = 0.12f;

    private float restScale;
    private float blinkSpeed;
    private float phase;
    private float elapsed;

    public void Initialize(float restScale, float blinkSpeed, float phase)
    {
        this.restScale = restScale;
        this.blinkSpeed = blinkSpeed;
        this.phase = phase;

        transform.localScale = Vector3.one * (restScale * MinScaleFactor);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float wave = Mathf.Sin((elapsed * blinkSpeed + phase) * Mathf.PI * 2f) * 0.5f + 0.5f;
        float spike = Mathf.Pow(wave, BlinkSharpness);

        transform.localScale = Vector3.one * (restScale * Mathf.Lerp(MinScaleFactor, 1f, spike));
    }
}
