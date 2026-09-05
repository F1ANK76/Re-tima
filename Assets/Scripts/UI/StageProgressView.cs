using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class StageProgressView : MonoBehaviour
{
    private const int DotCount = StageManager.BossSubStage;

    [SerializeField] private StageBannerView banner;

    [SerializeField] private Image[] dots;
    [SerializeField] private Image[] segments;
    [SerializeField] private RectTransform arrowRect;

    [SerializeField] private float bossDotScale = 1.35f;
    [SerializeField] private float currentDotScale = 1.2f;
    [SerializeField] private float arrowOffsetY = 16f;

    // 현재 위치 점이 "여기 있다"고 반짝이는 정도 - 크기를 사인파로 흔든다.
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseScaleAmount = 0.18f;

    private static readonly Color TrackColor = new Color(0.32f, 0.32f, 0.38f);
    private static readonly Color FutureDotColor = new Color(0.32f, 0.32f, 0.38f);
    // 지나온 위치 = 연두색, 현재 위치 = 하늘색.
    private static readonly Color CompletedDotColor = new Color(0.6f, 0.93f, 0.32f);
    private static readonly Color CurrentDotColor = new Color(0.3f, 0.75f, 1f);
    private static readonly Color BossFutureColor = new Color(0.5f, 0.12f, 0.12f);
    private static readonly Color BossReadyColor = new Color(0.95f, 0.15f, 0.15f);

    private int currentSubStage = 1;

    private CanvasGroup canvasGroup;
    // Update가 매 프레임이 아니라 실제로 상태가 전환될 때만 CanvasGroup에 쓰도록 이 값을 추적한다.
    private bool bannerWasVisible;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        Refresh();
    }

    private void OnEnable() => GameEvents.OnStageChanged += HandleStageChanged;
    private void OnDisable() => GameEvents.OnStageChanged -= HandleStageChanged;

    private void Update()
    {
        bool bannerVisible = banner.gameObject.activeSelf;
        if (bannerVisible != bannerWasVisible)
        {
            bannerWasVisible = bannerVisible;
            canvasGroup.alpha = bannerVisible ? 0f : 1f;
        }

        // 배너에 가려져 있는 동안은 안 보이니 애니메이션을 돌릴 이유가 없다.
        if (!bannerVisible) PulseCurrentDot();
    }

    private void PulseCurrentDot()
    {
        int index = currentSubStage - 1;
        if (index < 0 || index >= DotCount) return;

        Image dot = dots[index];
        bool isBoss = currentSubStage == DotCount;
        float baseScale = isBoss ? bossDotScale : currentDotScale;

        float wave = Mathf.Sin(Time.unscaledTime * pulseSpeed);
        dot.rectTransform.localScale = Vector3.one * (baseScale + wave * pulseScaleAmount);
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentSubStage = Mathf.Clamp(subStage, 1, DotCount);
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < DotCount; i++)
        {
            int stage = i + 1;
            bool isBoss = stage == DotCount;
            bool completed = stage < currentSubStage;
            bool current = stage == currentSubStage;

            Color color;
            if (isBoss) color = (completed || current) ? BossReadyColor : BossFutureColor;
            else color = completed ? CompletedDotColor : (current ? CurrentDotColor : FutureDotColor);
            dots[i].color = color;

            float scale = isBoss ? bossDotScale : (current ? currentDotScale : 1f);
            dots[i].rectTransform.localScale = Vector3.one * scale;
        }

        for (int i = 0; i < segments.Length; i++)
        {
            int rightStage = i + 2;
            segments[i].color = currentSubStage >= rightStage ? CompletedDotColor : TrackColor;
        }

        {
            float x = dots[currentSubStage - 1].rectTransform.anchoredPosition.x;
            arrowRect.anchoredPosition = new Vector2(x, arrowOffsetY);
            Image currentImage = dots[currentSubStage - 1];
            var arrowText = arrowRect.GetComponent<Text>();
            if (arrowText != null) arrowText.color = currentImage.color;
        }
    }
}
