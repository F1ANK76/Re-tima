using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The bottom-center gauge that builds toward the substage's elite.
public class BossGaugeView : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Text percentText;

    // How long a kill's worth of fill takes to animate in - the bar rising is the "차오르는"
    // read; snapping the fill instantly would lose that entirely.
    [SerializeField] private float fillTweenDuration = 0.35f;
    [SerializeField] private float readyFlashDuration = 0.5f;
    [SerializeField] private float readyPunchScale = 1.18f;

    [Header("Boss icon (right end of the bar)")]
    // Marks what the bar is filling toward - the fill arriving at a waiting icon is what tells
    // the player "filling this summons that", without any text to read.
    // A white silhouette works best here: both states are driven purely by Image.color, so an
    // already-colored sprite would fight the tint.
    [SerializeField] private Sprite bossIconSprite;
    [SerializeField] private float bossIconSize = 44f;
    // Gap between the icon's edge and the bar's right edge. The icon is positioned from its
    // own half-width plus this, so it always sits fully inside the bar rather than overhanging
    // it - with enough margin left over for the 100% flourish's punch on the whole gauge.
    [SerializeField] private float bossIconInset = 6f;
    // Both states are white; only the opacity separates them, so the icon never changes hue
    // and never moves. Faded while the gauge is still building, solid once it's full.
    [SerializeField] private Color bossIconDimColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color bossIconReadyColor = Color.white;

    private Image fillImage;
    private Color fillBaseColor;
    private Color textBaseColor;

    private RectTransform barRect;
    private Image bossIconImage;

    private int displayedPercent;
    private Coroutine gaugeRoutine;

    // From a fill reaching 100 to the punch/flash flourish finishing - StageManager reads
    // this so it can time the elite's spawn + "Elite Boss !" announcement to land right as
    // this flourish settles, instead of drifting apart on its own unrelated timer.
    public float ReadyFlourishDuration => fillTweenDuration + readyFlashDuration;

    private void Awake()
    {
        barRect = fillRect != null ? fillRect.parent as RectTransform : null;
        fillImage = fillRect != null ? fillRect.GetComponent<Image>() : null;
        if (fillImage != null) fillBaseColor = fillImage.color;
        if (percentText != null) textBaseColor = percentText.color;

        BuildBossIcon();
        SetBossReady(false);
    }

    private void OnEnable()
    {
        GameEvents.OnBossGaugeChanged += HandleGaugeChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnBossGaugeChanged -= HandleGaugeChanged;
    }

    private void HandleGaugeChanged(int percent)
    {
        if (gaugeRoutine != null) StopCoroutine(gaugeRoutine);

        // A drop (any new value lower than what's on screen) is the substage resetting the
        // gauge for the next grind, not progress draining away - that snaps instantly rather
        // than animating backward, which would read as a penalty that never happened.
        if (percent <= displayedPercent)
        {
            ApplyPercent(percent);
            return;
        }

        gaugeRoutine = StartCoroutine(TweenTo(percent));
    }

    private IEnumerator TweenTo(int target)
    {
        int start = displayedPercent;
        float t = 0f;

        while (t < fillTweenDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = EaseOutQuad(Mathf.Clamp01(t / fillTweenDuration));
            ApplyPercent(Mathf.RoundToInt(Mathf.Lerp(start, target, p)));
            yield return null;
        }

        ApplyPercent(target);

        if (target >= 100) yield return PlayReadyFlourish();
        gaugeRoutine = null;
    }

    private void ApplyPercent(int percent)
    {
        displayedPercent = percent;

        if (fillRect != null)
        {
            Vector2 anchorMax = fillRect.anchorMax;
            anchorMax.x = Mathf.Clamp01(percent / 100f);
            fillRect.anchorMax = anchorMax;
        }

        if (percentText != null) percentText.text = $"{percent}%";

        SetBossReady(percent >= 100);
    }

    private void SetBossReady(bool ready)
    {
        if (bossIconImage == null) return;

        bossIconImage.color = ready ? bossIconReadyColor : bossIconDimColor;
    }

    // The payoff for filling the bar: a quick punch-scale on the whole gauge plus a white
    // flash on the fill, timed to land right as StageManager's own delay is about to spawn
    // the elite - "the gauge maxing out is what summoned this".
    private IEnumerator PlayReadyFlourish()
    {
        float t = 0f;
        while (t < readyFlashDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / readyFlashDuration);

            // Punch out quickly, settle back over the remainder - same shape as the stat-drop
            // popup's own pop-in, reused here for the same "landed with a bit of weight" read.
            float scaleT = Mathf.Clamp01(p / 0.35f);
            float scale = Mathf.Lerp(1f, readyPunchScale, EaseOutBack(scaleT));
            if (scaleT >= 1f) scale = Mathf.Lerp(readyPunchScale, 1f, EaseOutQuad(Mathf.InverseLerp(0.35f, 1f, p)));
            transform.localScale = Vector3.one * scale;

            // Flash bright, then fade back to the fill's own color rather than lingering white.
            float flash = 1f - EaseOutQuad(p);
            if (fillImage != null) fillImage.color = Color.Lerp(fillBaseColor, Color.white, flash);
            if (percentText != null) percentText.color = Color.Lerp(textBaseColor, Color.white, flash);

            yield return null;
        }

        transform.localScale = Vector3.one;
        if (fillImage != null) fillImage.color = fillBaseColor;
        if (percentText != null) percentText.color = textBaseColor;
    }

    // Parented to the bar and added last, so it draws over the fill - the fill sweeps in
    // underneath the icon rather than clipping it.
    private void BuildBossIcon()
    {
        if (barRect == null || bossIconSprite == null) return;

        var go = new GameObject("BossIcon", typeof(RectTransform));
        go.transform.SetParent(barRect, false);

        var iconRect = go.GetComponent<RectTransform>();
        // Pinned to the bar's right edge, vertically centred on it, so it stays at the end of
        // the bar whatever the bar's own width resolves to on a given resolution.
        iconRect.anchorMin = new Vector2(1f, 0.5f);
        iconRect.anchorMax = new Vector2(1f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(bossIconSize, bossIconSize);
        // Pulled left by its own half-width plus the inset, which is what keeps it inside the
        // bar instead of hanging off the end.
        iconRect.anchoredPosition = new Vector2(-(bossIconSize * 0.5f + bossIconInset), 0f);

        bossIconImage = go.AddComponent<Image>();
        bossIconImage.sprite = bossIconSprite;
        bossIconImage.raycastTarget = false;
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
