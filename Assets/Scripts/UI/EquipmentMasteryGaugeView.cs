using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A slim gauge for one equipment slot's mastery meter - the same "차오르는" filling-bar
// language as BossGaugeView, but for the progress that a duplicate or lower-grade pickup now
// feeds instead of being wasted (see EquipmentDropManager.CompleteDrop). Unlike the boss gauge,
// this one is meant to cross 100% repeatedly: each crossing is a level gained, and the bar
// wraps back toward 0 rather than sitting capped.
//
// Builds its own background/fill/label as children in code (matching TitleScreenView elsewhere
// in this UI), but - unlike a fully standalone HUD element - leaves its OWN RectTransform
// (position/size within whatever parent it's given) entirely to the caller when
// anchorToBottomCenter is off, which is how EquipmentPanelView embeds one under each row.
public class EquipmentMasteryGaugeView : MonoBehaviour
{
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    [SerializeField] private EquipmentType equipType;
    [SerializeField] private Color fillColor = new Color(0.4f, 0.75f, 1f);
    // Standalone-mode only (see anchorToBottomCenter): this bar's own height, and its distance
    // from the screen's bottom edge - callers of a standalone bar place each instance at a
    // different offset so multiple gauges stack rather than overlap.
    [SerializeField] private float barHeight = 30f;
    [SerializeField] private float bottomOffset = 160f;
    // On: this component owns its RectTransform outright (a bottom-center HUD strip). Off: the
    // caller has already sized and positioned this GameObject (e.g. a row inside a panel) and
    // this must not fight that - it only ever touches its children.
    [SerializeField] private bool anchorToBottomCenter = true;
    // Off: show only the percent ("45%"), since an embedded gauge usually sits right under a
    // label that already spells out the type/level/grade and would otherwise repeat it.
    [SerializeField] private bool includeTypeAndLevelInLabel = true;
    [SerializeField] private float fillTweenDuration = 0.35f;

    private RectTransform fillRect;
    private Text label;
    private int displayedPercent;
    private int lastKnownLevel = -1;
    private Coroutine tweenRoutine;

    private void Awake()
    {
        if (anchorToBottomCenter) ConfigureStandaloneAnchors();
        Build();
    }

    // For callers building one of these at runtime (EquipmentPanelView): add the component
    // while the GameObject is still inactive, call this, then activate it - Unity defers
    // Awake on an inactive object, so this is guaranteed to run before Build() ever does and
    // before Awake could apply the standalone-mode defaults.
    public void Configure(EquipmentDropManager manager, EquipmentType type, Color color,
        bool anchorToBottomCenter, bool includeTypeAndLevelInLabel)
    {
        equipmentDropManager = manager;
        equipType = type;
        fillColor = color;
        this.anchorToBottomCenter = anchorToBottomCenter;
        this.includeTypeAndLevelInLabel = includeTypeAndLevelInLabel;
    }

    private void OnEnable()
    {
        GameEvents.OnEquipmentPickedUp += HandlePickedUp;
        Refresh(instant: true);
    }

    private void OnDisable()
    {
        GameEvents.OnEquipmentPickedUp -= HandlePickedUp;
    }

    private void HandlePickedUp(EquipmentType pickedType, StatGrade grade)
    {
        if (pickedType != equipType) return;
        Refresh(instant: false);
    }

    private void Refresh(bool instant)
    {
        if (equipmentDropManager == null) return;

        int level = equipType == EquipmentType.Sword ? equipmentDropManager.SwordLevel : equipmentDropManager.ShieldLevel;
        float progress = equipType == EquipmentType.Sword
            ? equipmentDropManager.SwordMasteryProgressPercent
            : equipmentDropManager.ShieldMasteryProgressPercent;
        int percent = Mathf.RoundToInt(progress);

        // A level-up wraps the meter back down near 0 - that's a genuine reset (the level
        // gained is the payoff), not progress draining away, so it snaps instantly rather than
        // animating backward.
        bool levelJustChanged = level != lastKnownLevel;
        lastKnownLevel = level;

        if (instant || levelJustChanged || percent <= displayedPercent)
        {
            if (tweenRoutine != null) StopCoroutine(tweenRoutine);
            tweenRoutine = null;
            ApplyPercent(percent, level);
            return;
        }

        if (tweenRoutine != null) StopCoroutine(tweenRoutine);
        tweenRoutine = StartCoroutine(TweenTo(percent, level));
    }

    private IEnumerator TweenTo(int target, int level)
    {
        int start = displayedPercent;
        float t = 0f;

        while (t < fillTweenDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = EaseOutQuad(Mathf.Clamp01(t / fillTweenDuration));
            ApplyPercent(Mathf.RoundToInt(Mathf.Lerp(start, target, p)), level);
            yield return null;
        }

        ApplyPercent(target, level);
        tweenRoutine = null;
    }

    private void ApplyPercent(int percent, int level)
    {
        displayedPercent = percent;

        if (fillRect != null)
        {
            Vector2 anchorMax = fillRect.anchorMax;
            anchorMax.x = Mathf.Clamp01(percent / 100f);
            fillRect.anchorMax = anchorMax;
        }

        if (label != null)
        {
            if (includeTypeAndLevelInLabel)
            {
                string typeLabel = equipType == EquipmentType.Sword ? "Sword" : "Shield";
                label.text = $"{typeLabel} Lv.{level}  {percent}%";
            }
            else
            {
                label.text = $"{percent}%";
            }
        }
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    // Same bottom-center band the boss gauge sits in, so a standalone bar shares its width
    // regardless of screen resolution. Skipped entirely when embedded (see anchorToBottomCenter).
    private void ConfigureStandaloneAnchors()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.333f, 0f);
        rt.anchorMax = new Vector2(0.667f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottomOffset);
        rt.sizeDelta = new Vector2(0f, barHeight);
    }

    private void Build()
    {
        Image background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.6f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(transform, false);
        fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGo.AddComponent<Image>();
        fillImage.color = fillColor;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        label = labelGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 16;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        var shadow = labelGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }
}
