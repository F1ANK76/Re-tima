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
    // 별도 텍스트 없이 "채우면 저게 소환된다"가 읽힌다. 흰색 실루엣이어야 한다: 두 상태 모두
    // Image.color로만 제어되므로 이미 색이 입혀진 스프라이트는 틴트와 충돌한다.
    [SerializeField] private Sprite bossIconSprite;
    [SerializeField] private float bossIconSize = 44f;
    // 아이콘 가장자리와 바 오른쪽 가장자리 사이의 간격. 아이콘을 자기 절반 너비 + 이 값만큼
    // 안쪽에 두어 바 밖으로 튀어나오지 않고, 100% 연출에서 게이지가 펀치될 여유도 남는다.
    [SerializeField] private float bossIconInset = 6f;
    // 두 상태 모두 흰색이며, 오직 불투명도만 다르므로 아이콘은 색조가 바뀌지도 움직이지도
    // 않는다. 게이지가 아직 차오르는 중일 땐 흐릿하게, 가득 차면 완전히 선명하게 보인다.
    [SerializeField] private Color bossIconDimColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color bossIconReadyColor = Color.white;

    private Image fillImage;
    private Color fillBaseColor;
    private Color textBaseColor;

    private RectTransform barRect;
    private Image bossIconImage;

    private int displayedPercent;
    private Coroutine gaugeRoutine;

    // 채움이 100에 도달한 뒤 펀치/플래시 연출이 끝날 때까지의 시간 - StageManager가 이 값을
    // 읽어서, 무관한 자체 타이머(매직 넘버)로 따로 놀지 않고 엘리트 스폰 + "Elite Boss !"
    // 알림을 이 연출이 마무리되는 바로 그 순간에 맞춘다.
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
            float scale = Mathf.Lerp(1f, readyPunchScale, EaseOutBack(scaleT));
            if (scaleT >= 1f) scale = Mathf.Lerp(readyPunchScale, 1f, EaseOutQuad(Mathf.InverseLerp(0.35f, 1f, p)));
            transform.localScale = Vector3.one * scale;

            // 밝게 번쩍인 뒤, 흰색으로 계속 남아있지 않고 채움 자체의 색으로 다시 페이드된다.
            float flash = 1f - EaseOutQuad(p);
            if (fillImage != null) fillImage.color = Color.Lerp(fillBaseColor, Color.white, flash);
            if (percentText != null) percentText.color = Color.Lerp(textBaseColor, Color.white, flash);

            yield return null;
        }

        transform.localScale = Vector3.one;
        if (fillImage != null) fillImage.color = fillBaseColor;
        if (percentText != null) percentText.color = textBaseColor;
    }

    // 바에 자식으로 붙이고 마지막에 추가하여 채움 위에 그려진다 - 채움이 아이콘을 가리며
    // 지나가는 게 아니라 아이콘 아래로 훑고 지나간다.
    private void BuildBossIcon()
    {
        if (barRect == null || bossIconSprite == null) return;

        var go = new GameObject("BossIcon", typeof(RectTransform));
        go.transform.SetParent(barRect, false);

        var iconRect = go.GetComponent<RectTransform>();
        // 바의 오른쪽 가장자리에 고정되고 세로로는 가운데 정렬되어, 특정 해상도에서 바의
        // 너비가 실제로 어떻게 결정되든 항상 바 끝에 위치한다.
        iconRect.anchorMin = new Vector2(1f, 0.5f);
        iconRect.anchorMax = new Vector2(1f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(bossIconSize, bossIconSize);
        // 자신의 절반 너비에 inset을 더한 만큼 왼쪽으로 당겨지는데, 이게 바 끝에 걸쳐
        // 늘어지지 않고 바 안쪽에 머물게 해준다.
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
