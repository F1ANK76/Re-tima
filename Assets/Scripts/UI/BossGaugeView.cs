using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 서브스테이지의 엘리트를 향해 차오르는 화면 하단 중앙 게이지.
public class BossGaugeView : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Text percentText;

    [SerializeField] private float fillTweenDuration = 0.35f;
    [SerializeField] private float readyFlashDuration = 0.5f;
    [SerializeField] private float readyPunchScale = 1.18f;

    [Header("Boss icon (right end of the bar)")]
    [SerializeField] private Image bossIconImage;
    [SerializeField] private Color bossIconDimColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color bossIconReadyColor = Color.white;

    private Image fillImage;
    private Color fillBaseColor;
    private Color textBaseColor;

    private int displayedPercent;
    private Coroutine gaugeRoutine;

    public float ReadyFlourishDuration => fillTweenDuration + readyFlashDuration;

    private void Awake()
    {
        fillImage = fillRect.GetComponent<Image>();
        fillBaseColor = fillImage.color;
        textBaseColor = percentText.color;

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
            float p = Easing.OutQuad(Mathf.Clamp01(t / fillTweenDuration));
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

        Vector2 anchorMax = fillRect.anchorMax;
        anchorMax.x = Mathf.Clamp01(percent / 100f);
        fillRect.anchorMax = anchorMax;

        percentText.text = $"{percent}%";

        SetBossReady(percent >= 100);
    }

    private void SetBossReady(bool ready)
    {
        bossIconImage.color = ready ? bossIconReadyColor : bossIconDimColor;
    }

    private IEnumerator PlayReadyFlourish()
    {
        float t = 0f;
        while (t < readyFlashDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / readyFlashDuration);

            float scaleT = Mathf.Clamp01(p / 0.35f);
            float scale = Mathf.LerpUnclamped(1f, readyPunchScale, Easing.OutBack(scaleT));
            if (scaleT >= 1f) scale = Mathf.Lerp(readyPunchScale, 1f, Easing.OutQuad(Mathf.InverseLerp(0.35f, 1f, p)));
            transform.localScale = Vector3.one * scale;

            // 밝게 번쩍인 뒤, 흰색으로 계속 남아있지 않고 채움 자체의 색으로 다시 페이드된다.
            float flash = 1f - Easing.OutQuad(p);
            fillImage.color = Color.Lerp(fillBaseColor, Color.white, flash);
            percentText.color = Color.Lerp(textBaseColor, Color.white, flash);

            yield return null;
        }

        transform.localScale = Vector3.one;
        fillImage.color = fillBaseColor;
        percentText.color = textBaseColor;
    }
}
