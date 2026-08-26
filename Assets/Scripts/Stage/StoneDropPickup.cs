using System.Collections;
using UnityEngine;

// Stage 3's drop, and the third member of the family that already includes StatPotionPickup
// (stage 1) and EquipmentDropPickup (stage 2). Same three beats in the same order - toss and
// bounce to rest, a short settle, then a flat ground-level run-over that pays out on contact -
// so all three stages' pickups read as the same "the player runs over it" motion.
//
// Unlike its two siblings, a stone has NO grade: every crystal that drops is the same stone
// at the same size, and the only thing that varies is which of the two it is. Red is the ATK
// stone and green the HP one, following the RedVial/GreenVial color language stage 1 already
// established - so the aura is colored to match the crystal rather than to encode a rarity.
//
// One other difference, forced by the visual being a Hovl Studio Crystal effect prefab rather
// than a static mesh: the crystal is drawn as a PARTICLE (the root system renders the Crystal1
// mesh), so at spawn there are no live particles and the renderer bounds are empty - the
// bounds-measuring trick its siblings use to sit flush on the floor returns zero here. A fixed
// authored offset is used instead.
public class StoneDropPickup : MonoBehaviour
{
    [Header("Toss + bounce (beat 1)")]
    // Same arc shape as StatPotionPickup's, but thrown noticeably farther and higher than
    // the potion's 1.4/1.1 - a stone is a bigger, heavier-reading object and the longer
    // flight is what sells it. landDuration is stretched to match, since covering more ground
    // in the same 0.75s would just look sped up rather than thrown harder.
    [SerializeField] private float landDuration = 0.9f;
    [SerializeField] private float tossDistance = 2.3f;
    [SerializeField] private float tossHeight = 2f;

    [Header("Run-over (beats 2-3)")]
    [SerializeField] private float settleHoldDuration = 0.2f;
    [SerializeField] private float pickupRadius = 0.45f;
    [SerializeField] private float approachTimeout = 6f;

    private float approachSpeed = 5f;
    // Extra distance behind tossDistance, set per-instance when a kill drops more than one
    // stone at once - each successive stone in the burst lands further back than the last, so
    // the group reads as a trail rather than a stack landing on the same spot.
    private float extraTossDistance;

    [Header("Visual (per option type)")]
    // Hovl Studio's "Crystal effect red" / "Crystal effect green". Both are self-contained
    // looping systems with playOnAwake set, so instantiating one is all that is needed.
    [SerializeField] private GameObject attackVisualPrefab;
    [SerializeField] private GameObject hpVisualPrefab;
    // One size for every stone - there are no grades, so nothing varies it.
    [SerializeField] private float stoneScale = 0.825f;
    // How far above the ground the crystal's pivot sits, as a MULTIPLE of stoneScale rather
    // than an absolute distance. The crystal's pivot-to-bottom distance is exactly
    // proportional to its scale (measured: 0.607 at scale 0.55, 0.910 at 0.825 - the same
    // 1.103 ratio), so a fixed lift only ever looks right at one size and buries the stone at
    // any larger one. Tuned against how the stone sat at its original 0.55/0.35 pairing;
    // the raw particle bounds run wider than the visible crystal, so they overshoot.
    [SerializeField] private float groundLiftPerScale = 0.636f;

    [Header("Aura")]
    // Colored to match the crystal itself, so the halo reinforces which stone it is instead
    // of encoding a rarity the stone doesn't have.
    [SerializeField] private Color attackAuraColor = new Color(1f, 0.28f, 0.2f);
    [SerializeField] private Color hpAuraColor = new Color(0.25f, 0.9f, 0.3f);
    [SerializeField] private float auraSize = 1.5f;
    [SerializeField] private float auraBrightness = 1.7f;
    [SerializeField] private float auraLightIntensity = 3f;
    [SerializeField] private float auraLightRange = 2.6f;

    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private StatType statType;
    private Transform player;
    private CombatLoop combatLoop;
    private StoneDropManager dropManager;
    private Vector3 restScale;
    // Built in code per instance, so nothing else will collect it - see OnDestroy.
    private Material auraMaterial;
    // Same reason as StatPotionPickup's: HandlePlayerDied can cut the coroutine off mid-toss,
    // before it ever reaches its own PopIdleHold call.
    private bool idleHoldActive;

    public void Initialize(StatType statType, Transform player,
        CombatLoop combatLoop, float approachSpeed, StoneDropManager dropManager, float extraTossDistance = 0f)
    {
        this.statType = statType;
        this.player = player;
        this.combatLoop = combatLoop;
        this.dropManager = dropManager;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;
        this.extraTossDistance = extraTossDistance;

        SpawnVisual();
        restScale = Vector3.one * stoneScale;
        transform.localScale = restScale;

        auraMaterial = PickupAura.Attach(transform,
            statType == StatType.Attack ? attackAuraColor : hpAuraColor,
            auraSize, auraBrightness, auraLightIntensity, auraLightRange);

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

    // See StatPotionPickup.HandlePlayerDied - a drop from the kill that also killed the
    // player must freeze where it is instead of sliding into (and paying out to) a corpse.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }

    private void SpawnVisual()
    {
        GameObject prefab = statType == StatType.Attack ? attackVisualPrefab : hpVisualPrefab;
        if (prefab == null) return;

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
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
        float groundY = ResolveGroundY() + groundLiftPerScale * stoneScale;

        Vector3 away = Vector3.right;
        if (player != null)
        {
            Vector3 delta = startPos - player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f) away = delta.normalized;
        }

        Vector3 landingPos = startPos + away * (tossDistance + extraTossDistance);
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
        if (dropManager != null) dropManager.AddStone(statType);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
    }
}
