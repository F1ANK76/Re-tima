using System.Collections;
using UnityEngine;

// Equipment counterpart to StatPotionPickup, same three beats (toss, settle, run-over) and the
// same grade aura/sparkle language - only the visual differs: a single Sword or Shield mesh
// (tinted by grade) instead of a per-stat vial. Equip/mastery effects are applied by
// EquipmentDropManager.CompleteDrop on contact, not at drop time, so a pickup dropped but never
// reached (player dies first, etc.) never pays out.
public class EquipmentDropPickup : MonoBehaviour
{
    [Header("Toss + bounce (beat 1)")]
    [SerializeField] private float landDuration = 0.75f;
    [SerializeField] private float tossDistance = 1.4f;
    [SerializeField] private float tossHeight = 1.1f;

    [Header("Run-over (beats 2-3)")]
    [SerializeField] private float settleHoldDuration = 0.2f;
    [SerializeField] private float pickupRadius = 0.45f;
    [SerializeField] private float approachTimeout = 6f;

    private float approachSpeed = 5f;

    [Header("Visual (per equipment type)")]
    // One mesh per type, same as StatPotionPickup's atkVisualPrefab/hpVisualPrefab - rarity
    // reads entirely through the aura/sparkle/size ramp below, not a different model per grade.
    [SerializeField] private GameObject swordVisualPrefab;
    [SerializeField] private GameObject shieldVisualPrefab;
    [SerializeField] private float visualBaseScale = 0.4f;
    // Applied on top of whatever material the mesh imports with - the generated FBXs
    // reference their PBR textures by external side-car file path (a model.fbm folder),
    // which a single downloaded .fbx never carries, so the imported material has no
    // texture and renders invisible. Overriding here keeps material setup independent
    // of whatever the FBX import happens to resolve.
    [SerializeField] private Material swordMaterial;
    [SerializeField] private Material shieldMaterial;
    // The shield mesh is authored as a disc lying in the XZ plane (face pointing up), so
    // dropped as-authored it reads as a bowl on the ground rather than a shield. Rotating it
    // upright is per-asset framing, not something the drop logic should hardcode.
    [SerializeField] private Vector3 swordVisualEuler = Vector3.zero;
    [SerializeField] private Vector3 shieldVisualEuler = new Vector3(90f, 0f, 0f);

    [Header("Grade aura")]
    [SerializeField] private float auraSize = 1.5f;
    [SerializeField] private float auraBrightnessMax = 1.7f;
    [SerializeField] private float auraLightIntensityMax = 3f;
    [SerializeField] private float auraLightRange = 2.6f;

    [Header("Twinkle sparkles")]
    [SerializeField] private int sparkleCount = 4;
    [SerializeField] private float sparkleOrbitRadius = 0.5f;
    [SerializeField] private float sparkleSize = 0.6f;
    [SerializeField] private float sparkleBrightnessMax = 2.4f;
    [SerializeField] private float sparkleBlinkSpeed = 1.1f;
    [SerializeField] private float sparkleOrbitSpeed = 35f;

    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private Renderer[] renderers;
    // Just the mesh's own renderer(s), not the aura/sparkle quads added afterward in
    // SpawnAura - exposed so EquipmentPreviewRig can frame the UI icon on the item's actual
    // silhouette instead of guessing a fixed camera distance that fits every grade/mesh.
    public Renderer[] VisualRenderers => renderers;
    private EquipmentType equipType;
    private StatGrade grade;
    private Transform player;
    private CombatLoop combatLoop;
    private EquipmentDropManager dropManager;
    // See StatPotionPickup's identical field for why this is needed - HandlePlayerDied can
    // StopAllCoroutines mid-toss, before this ever reaches its own PopIdleHold call.
    private bool idleHoldActive;
    private Vector3 restScale;
    private float restBottomOffset;
    private Material auraMaterial;
    private Material sparkleMaterial;
    // A per-instance copy of the shared sword/shield material, tinted by grade - the shared
    // asset itself must stay untouched since every pickup in flight references the same one.
    private Material visualMaterialInstance;

    public void Initialize(EquipmentType equipType, StatGrade grade, Transform player, CombatLoop combatLoop, float approachSpeed, EquipmentDropManager dropManager)
    {
        this.equipType = equipType;
        this.grade = grade;
        this.player = player;
        this.combatLoop = combatLoop;
        this.dropManager = dropManager;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;

        SpawnVisual();
        restScale = transform.localScale * GradeVisuals.GetPotionScale(grade);

        transform.localScale = restScale;
        restBottomOffset = ComputeBottomOffset();

        SpawnAura();

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

    // See StatPotionPickup.HandlePlayerDied - same bug (this pickup's coroutine runs
    // independently of StageManager's death sequence and kept sliding into/paying out to an
    // already-dead player), same fix.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }

    private float ComputeBottomOffset()
    {
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        return transform.position.y - combined.min.y;
    }

    private void SpawnVisual()
    {
        GameObject prefab = equipType == EquipmentType.Sword ? swordVisualPrefab : shieldVisualPrefab;

        if (prefab == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            return;
        }

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(equipType == EquipmentType.Sword ? swordVisualEuler : shieldVisualEuler);
        visual.transform.localScale = Vector3.one * visualBaseScale;

        renderers = visual.GetComponentsInChildren<Renderer>();

        Material overrideMaterial = equipType == EquipmentType.Sword ? swordMaterial : shieldMaterial;
        if (overrideMaterial != null)
        {
            // Instanced rather than shared: tinting _BaseColor below multiplies straight onto
            // whatever this material already is, so mutating the shared asset would recolor
            // every sword/shield currently on screen, not just this one.
            visualMaterialInstance = new Material(overrideMaterial)
            {
                color = GradeVisuals.GetColor(grade)
            };

            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = visualMaterialInstance;
        }
    }

    private void SpawnAura()
    {
        float strength = GradeVisuals.GetAuraStrength(grade);
        Color color = GradeVisuals.GetColor(grade);

        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
        aura.name = "GradeAura";
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
        light.shadows = LightShadows.None;

        aura.AddComponent<Billboard>();
        aura.AddComponent<StatPotionAuraMotion>();

        SpawnSparkles(color, strength);
    }

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
            sparkle.AddComponent<StatPotionSparkle>()
                   .Initialize(sparkleSize, sparkleBlinkSpeed, (float)i / sparkleCount);
        }
    }

    private static Material CreateAdditiveGlowMaterial(Color tint, Texture2D texture = null)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetTexture("_BaseMap", texture != null ? texture : GlowTexture);
        mat.SetColor("_BaseColor", tint);

        return mat;
    }

    // Shares the exact star-glint and radial-glow textures StatPotionPickup builds, generated
    // independently here (a private static cache per class) since the two never share a class
    // hierarchy - cheap 64x64 textures, not worth wiring a shared asset for.
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
                    float dx = Mathf.Abs(x - center) / center;
                    float dy = Mathf.Abs(y - center) / center;

                    float horizontal = Falloff(dx) * Needle(dy);
                    float vertical = Falloff(dy) * Needle(dx);
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

    private static float Falloff(float d)
    {
        float f = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
        return f * f;
    }

    private static float Needle(float d) => Mathf.Clamp01(1f - Mathf.Clamp01(d) * 14f);

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

        if (combatLoop != null) combatLoop.PopIdleHold();
        idleHoldActive = false;

        yield return new WaitForSeconds(settleHoldDuration);
        yield return PlayRunOver();

        Collect();
    }

    private IEnumerator PlayToss()
    {
        Vector3 startPos = transform.position;
        float groundY = ResolveGroundY() + restBottomOffset;

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

            transform.localScale = restScale * Mathf.Clamp01(p / 0.3f);

            Vector3 flat = Vector3.Lerp(startPos, landingPos, EaseOutQuad(p));
            flat.y = Mathf.Lerp(startPos.y, groundY, EaseOutQuad(p)) + tossHeight * HopHeight(p);
            transform.position = flat;

            yield return null;
        }

        transform.localScale = restScale;
        transform.position = landingPos;
    }

    private IEnumerator PlayRunOver()
    {
        float restY = transform.position.y;
        float elapsed = 0f;

        while (elapsed < approachTimeout)
        {
            elapsed += Time.deltaTime;
            if (player == null) yield break;

            Vector3 pos = transform.position;

            Vector3 toPlayer = player.position - pos;
            toPlayer.y = 0f;

            if (toPlayer.magnitude <= pickupRadius) yield break;

            pos += toPlayer.normalized * approachSpeed * Time.deltaTime;
            pos.y = restY;
            transform.position = pos;

            yield return null;
        }
    }

    private float ResolveGroundY()
    {
        if (player == null) return transform.position.y;

        Collider playerCollider = player.GetComponent<Collider>();
        return playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
    }

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
        if (dropManager != null) dropManager.CompleteDrop(equipType, grade);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
        if (sparkleMaterial != null) Destroy(sparkleMaterial);
        if (visualMaterialInstance != null) Destroy(visualMaterialInstance);
    }
}
