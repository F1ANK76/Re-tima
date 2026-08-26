using UnityEngine;

// Floating damage number above whichever monster's HealthBar just took a hit - every point
// of player-dealt damage flows through Monster.TakeDamage (normal swings and the ultimate
// both land there), so this single listener covers both without caring which system dealt
// the blow.
public class DamagePopup : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private int fontSize = 32;
    [SerializeField] private float characterSize = 0.1f;

    private static readonly Color TextColor = new Color(1f, 0.95f, 0.85f);
    private Font font;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnEnable()
    {
        GameEvents.OnMonsterDamaged += HandleMonsterDamaged;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterDamaged -= HandleMonsterDamaged;
    }

    private void HandleMonsterDamaged(Monster monster, float amount)
    {
        if (monster == null || amount <= 0f) return;

        // Parented under the monster's own HealthBar rather than its root transform - that's
        // already tuned to sit above each monster type's head (and scales automatically with
        // Elite/Boss's bumped-up transform.localScale), so this rides the same anchor instead
        // of duplicating a per-monster-type height offset.
        HealthBarView healthBar = monster.GetComponentInChildren<HealthBarView>();
        Transform anchor = healthBar != null ? healthBar.transform : monster.transform;

        var go = new GameObject("DamagePopup");
        go.transform.SetParent(anchor, false);
        // A couple of hits landing back to back spread horizontally so their numbers don't
        // perfectly overlap.
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.GetComponent<MeshRenderer>().material = font.material;
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = TextColor;
        // ATK now grows in fractional steps (0.1/0.3/0.5/...), so a hit can land as e.g. 1.4 -
        // rounding to a whole number would misreport small hits as 1 or even 0.
        tm.text = amount.ToString("0.#");

        go.AddComponent<Billboard>();
        go.AddComponent<DamagePopupMotion>();
    }
}

// Rise/fade/pop over a much shorter window than the player's own stat popups - a damage
// number needs to read fast and get out of the way, not linger over an enemy that's likely
// about to take several more hits in quick succession.
public class DamagePopupMotion : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float riseDistance = 0.25f;
    [SerializeField] private float popStartScale = 0.4f;

    private TextMesh label;
    private Vector3 startPos;
    private float elapsed;

    private void Awake()
    {
        label = GetComponent<TextMesh>();
        startPos = transform.localPosition;
        transform.localScale = Vector3.one * popStartScale;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // Quick punch in over the first fifth, then a steady rise for the rest - popping the
        // instant it appears is what makes it read as a hit landing rather than just drifting
        // text.
        float popT = Mathf.Clamp01(t / 0.2f);
        transform.localScale = Vector3.one * Mathf.Lerp(popStartScale, 1f, EaseOutBack(popT));

        transform.localPosition = startPos + Vector3.up * (riseDistance * EaseOutQuad(t));

        // Holds fully opaque through the pop, then fades over the remaining ~70%.
        float alpha = 1f - Mathf.Clamp01((t - 0.30f) / 0.70f);
        if (label != null)
        {
            Color c = label.color;
            c.a = alpha;
            label.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    private static float EaseOutBack(float x)
    {
        const float overshoot = 1.6f;
        const float c3 = overshoot + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + overshoot * m * m;
    }
}
