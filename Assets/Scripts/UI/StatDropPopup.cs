using UnityEngine;

// Spawns a "+N STAT" popup above the player's own health bar every time
// GameEvents.OnStatDropGained fires - grade reads through color and size instead of being
// spelled out in the text. A world-space TextMesh with Billboard rather than a Canvas Text -
// that's how every other piece of floating combat text in this game already works
// (ParrySuccessText, the health bars themselves), so this rides the same convention.
public class StatDropPopup : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    // Matches ParrySuccessText's own placement above the same health bar, so the two read as
    // the same family of floating text.
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.65f, 0f);
    // These two multiply: rendered height is characterSize * fontSize / ~9, so fontSize is
    // NOT just a quality knob - it scales the text on screen exactly like characterSize does.
    // The pair is deliberately "high fontSize, low characterSize": that renders the same
    // on-screen size as 36/0.13 but rasterizes the glyphs at twice the resolution, which is
    // what keeps them from going soft (it reads worst in the WebGL build).
    //
    // characterSize is the Epic-grade size specifically - GradeVisuals.GetSizeScale ramps
    // every other grade up/down from it (Epic sits in the middle of the five), so the smallest
    // grade (Normal, 0.55x) is what actually sets the floor on legibility here.
    [SerializeField] private int fontSize = 72;
    [SerializeField] private float characterSize = 0.065f;

    // Any one of the Hovl "Sparks explode <color>" prefabs works as the base - its own
    // baked-in color is discarded and replaced at runtime (see SpawnSparkleBurst), so the
    // choice of which variant sits in this slot doesn't matter.
    [SerializeField] private ParticleSystem sparkleBurstPrefab;

    private Font font;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnEnable()
    {
        GameEvents.OnStatDropGained += HandleStatDropGained;
    }

    private void OnDisable()
    {
        GameEvents.OnStatDropGained -= HandleStatDropGained;
    }

    private void HandleStatDropGained(StatGrade grade, StatType statType, float amount)
    {
        if (anchor == null) return;

        var go = new GameObject("StatDropPopup");
        go.transform.SetParent(anchor, false);
        // A couple of drops landing back to back spread horizontally so their popups don't
        // perfectly overlap.
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        // ATK grows in fractional steps (0.1/0.3/0.5/...) - rounding to a whole number would
        // display every Normal/Rare/Epic ATK drop as "+0 ATK". HP stays whole either way.
        string amountText = statType == StatType.Attack ? amount.ToString("0.#") : amount.ToString("0");
        string text = $"+{amountText} {GetStatAbbreviation(statType)}";

        PopupText.Build(go, font, text, fontSize,
            characterSize * GradeVisuals.GetSizeScale(grade),
            GradeVisuals.GetPopupTextColor(grade));

        go.AddComponent<Billboard>();
        go.AddComponent<StatDropPopupMotion>();

        if (sparkleBurstPrefab != null) SpawnSparkleBurst(go.transform, grade);
    }

    private static string GetStatAbbreviation(StatType statType) => statType == StatType.Attack ? "ATK" : "HP";

    // Parented under the popup itself so it pops in with the same scale punch and gets
    // cleaned up automatically when StatDropPopupMotion destroys the popup - no independent
    // lifetime to manage here.
    //
    // Retinted to the exact grade color rather than picking whichever preset-colored variant
    // is closest - the pack only ships blue/green/pink/red/white/yellow, none of which is
    // GradeVisuals' Epic purple, and hand-picking a "close enough" substitute per grade would
    // drift out of sync with GradeVisuals the moment that ramp ever changes.
    private void SpawnSparkleBurst(Transform parent, StatGrade grade)
    {
        ParticleSystem burst = Instantiate(sparkleBurstPrefab, parent);
        burst.transform.localPosition = Vector3.zero;

        // Same ramp the popup text's own size already scales by - a Legendary burst reads
        // bigger and denser than a Normal one, not just a different hue.
        float strength = GradeVisuals.GetAuraStrength(grade);
        burst.transform.localScale = Vector3.one * strength;

        Color color = GradeVisuals.GetColor(grade);

        var main = burst.main;
        // The source prefab loops a burst every lengthInSec forever - one clean burst per
        // stat gain reads as a flourish, a repeating one would read as a malfunction.
        main.loop = false;
        main.startColor = color;

        var colorOverLifetime = burst.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var emission = burst.emission;
        ParticleSystem.Burst particleBurst = emission.GetBurst(0);
        particleBurst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(4f, 50f * strength));
        emission.SetBurst(0, particleBurst);
    }
}

// The popup's own rise/fade/pop, separated out so StatDropPopup only has to build the object
// and hand off - each popup then animates and cleans itself up independently, which matters
// once two drops land close enough together to have overlapping popups in flight at once.
public class StatDropPopupMotion : MonoBehaviour
{
    [SerializeField] private float duration = 1.5f;
    // Short on purpose: this floats in local space above a health bar that's only ~0.5 units
    // wide, so it needs to read as a small pop in place rather than a long flight.
    [SerializeField] private float riseDistance = 0.3f;
    [SerializeField] private float popStartScale = 0.4f;
    // Fraction of the duration held at full opacity before the fade starts. Generous: the
    // popup has to actually be readable, and fading almost immediately (this was 0.30) left
    // well under half a second at full opacity to read a number in.
    [SerializeField] private float opaqueFraction = 0.55f;

    // Every TextMesh under here, not just the root one - PopupText builds the outline from
    // eight child copies, and fading only the fill would leave a solid black silhouette
    // hanging in the air after the number itself had gone.
    private TextMesh[] labels;
    private Color[] baseColors;
    private Vector3 startPos;
    private float elapsed;

    private void Awake()
    {
        labels = GetComponentsInChildren<TextMesh>();
        // Captured once: alpha is written every frame, so re-reading each label's current
        // color would be reading back what this already wrote.
        baseColors = new Color[labels.Length];
        for (int i = 0; i < labels.Length; i++) baseColors[i] = labels[i].color;

        startPos = transform.localPosition;
        transform.localScale = Vector3.one * popStartScale;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // Quick punch in over the first fifth, then a steady rise for the rest - popping the
        // instant it appears is what makes it read as a burst rather than just drifting text.
        float popT = Mathf.Clamp01(t / 0.2f);
        transform.localScale = Vector3.one * Mathf.Lerp(popStartScale, 1f, EaseOutBack(popT));

        transform.localPosition = startPos + Vector3.up * (riseDistance * EaseOutQuad(t));

        float alpha = 1f - Mathf.Clamp01((t - opaqueFraction) / Mathf.Max(0.0001f, 1f - opaqueFraction));
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            Color c = baseColors[i];
            c.a = alpha;
            labels[i].color = c;
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
