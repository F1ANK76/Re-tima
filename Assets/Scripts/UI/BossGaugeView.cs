using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 서브스테이지의 엘리트를 향해 차오르는 화면 하단 중앙 게이지.
public class BossGaugeView : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Text percentText;

    // 처치 한 번어치의 채움이 애니메이션되는 데 걸리는 시간 - 바가 상승하는 게 바로
    // "차오르는" 느낌을 주는 부분이며, 즉시 채워버리면 그 느낌이 완전히 사라진다.
    [SerializeField] private float fillTweenDuration = 0.35f;
    [SerializeField] private float readyFlashDuration = 0.5f;
    [SerializeField] private float readyPunchScale = 1.18f;

    [Header("Boss icon (right end of the bar)")]
    // 바가 무엇을 향해 채워지는지 표시한다 - 채움이 대기 중인 아이콘에 도달하는 것만으로
    // 별도 텍스트 없이 "채우면 저게 소환된다"가 읽힌다. 씬에 미리 배치된 자식이라(바 오른쪽
    // 가장자리 안쪽으로 앵커) 위치/스프라이트는 인스펙터에서 조정한다 - 색만 여기서 토글한다.
    [SerializeField] private Image bossIconImage;
    // 두 상태 모두 흰색이며, 오직 불투명도만 다르므로 아이콘은 색조가 바뀌지도 움직이지도
    // 않는다. 게이지가 아직 차오르는 중일 땐 흐릿하게, 가득 차면 완전히 선명하게 보인다.
    [SerializeField] private Color bossIconDimColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color bossIconReadyColor = Color.white;

    private Image fillImage;
    private Color fillBaseColor;
    private Color textBaseColor;

    private int displayedPercent;
    private Coroutine gaugeRoutine;

    // 채움이 100에 도달한 뒤 펀치/플래시 연출이 끝날 때까지의 시간 - StageManager가 이 값을
    // 읽어서, 무관한 자체 타이머(매직 넘버)로 따로 놀지 않고 엘리트 스폰 + "Elite Boss !"
    // 알림을 이 연출이 마무리되는 바로 그 순간에 맞춘다.
    public float ReadyFlourishDuration => fillTweenDuration + readyFlashDuration;

    private void Awake()
    {
        fillImage = fillRect != null ? fillRect.GetComponent<Image>() : null;
        if (fillImage != null) fillBaseColor = fillImage.color;
        if (percentText != null) textBaseColor = percentText.color;

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

        // 표시값보다 낮은 새 값은 진행도가 깎인 게 아니라 서브스테이지가 다음 판을 위해
        // 게이지를 리셋한 것 - 뒤로 애니메이션되면 없던 페널티처럼 보이므로 즉시 스냅한다.
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

    // 바를 다 채운 보상 연출: 게이지 전체에 빠른 펀치 스케일, 채움에는 흰색 플래시. StageManager
    // 자체 딜레이가 엘리트를 스폰하는 순간과 맞물려 "가득 찬 게 이걸 소환했다"는 인상을 준다.
    private IEnumerator PlayReadyFlourish()
    {
        float t = 0f;
        while (t < readyFlashDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / readyFlashDuration);

            // 빠르게 펀치된 뒤 남은 시간 동안 서서히 안착 - 스탯 드롭 팝업의 팝인과 같은
            // 곡선을 재사용해 여기서도 "묵직하게 착지했다"는 느낌을 준다.
            float scaleT = Mathf.Clamp01(p / 0.35f);
            float scale = Mathf.LerpUnclamped(1f, readyPunchScale, Easing.OutBack(scaleT));
            if (scaleT >= 1f) scale = Mathf.Lerp(readyPunchScale, 1f, Easing.OutQuad(Mathf.InverseLerp(0.35f, 1f, p)));
            transform.localScale = Vector3.one * scale;

            // 밝게 번쩍인 뒤, 흰색으로 계속 남아있지 않고 채움 자체의 색으로 다시 페이드된다.
            float flash = 1f - Easing.OutQuad(p);
            if (fillImage != null) fillImage.color = Color.Lerp(fillBaseColor, Color.white, flash);
            if (percentText != null) percentText.color = Color.Lerp(textBaseColor, Color.white, flash);

            yield return null;
        }

        transform.localScale = Vector3.one;
        if (fillImage != null) fillImage.color = fillBaseColor;
        if (percentText != null) percentText.color = textBaseColor;
    }
}
