using UnityEngine;
using UnityEngine.UI;

// "Stage X-Y" HUD 라벨(HudController) 바로 아래의 5점 트랙 - 일반 서브스테이지 4개 + 보스.
// 한 번 보면 메인 스테이지 안에서 진행 위치가 어디쯤인지 알 수 있고, 보스가 기억해야 할 숫자가
// 아니라 고정된 마지막 정거장처럼 읽히게 한다. 점/구간/화살표는 씬에 미리 지어져 있다(개수나
// 레이아웃이 절대 안 바뀌므로) - 이 스크립트는 상태가 바뀔 때 색/크기/화살표 위치만 갱신한다.
[RequireComponent(typeof(CanvasGroup))]
public class StageProgressView : MonoBehaviour
{
    // 일반 서브스테이지 4개에 보스를 더한 값 - StageManager 자신이 보스 서브스테이지를
    // 판별할 때 쓰는 것과 동일한 상수이므로, 이 둘이 서로 어긋날 일이 없다.
    private const int DotCount = StageManager.BossSubStage;

    // 이게 떠 있는 동안엔 숨는다(StageBannerView) - 화면 상단 중앙은 이미 배너가 차지하고 있고,
    // 팝인/페이드아웃 중인 카드 밑에서 트랙까지 같이 팝인되면 정보 재확인이 아니라 잡음으로 읽혔다.
    [SerializeField] private StageBannerView banner;

    // 점 5개(Dot1..DotCount)와 그 사이 구간 4개(Segment1..DotCount-1) - 씬에 미리 배치된 순서
    // 그대로다.
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
    // 보스 점은 완료/현재/미래와 무관하게 항상 빨강 쪽 - 다른 네 점이 쓰는 상태 색과
    // 상관없이 "이 자리가 보스"라는 걸 한눈에 알아보게 하는 게 요점이다.
    private static readonly Color BossFutureColor = new Color(0.5f, 0.12f, 0.12f);
    private static readonly Color BossReadyColor = new Color(0.95f, 0.15f, 0.15f);

    private int currentSubStage = 1;

    private CanvasGroup canvasGroup;
    // Update가 매 프레임이 아니라 실제로 상태가 전환될 때만 CanvasGroup에 쓰도록 이 값을 추적한다.
    private bool bannerWasVisible;

    private void Awake()
    {
        // 숨김은 SetActive(false)가 아니라 CanvasGroup으로 - activeSelf를 토글하면 OnDisable이
        // OnStageChanged 구독을 끊어서, 배너가 떠 있는 동안 바뀐 서브스테이지는 재활성화 후
        // 우연히 다음 이벤트가 오기 전까지 절대 받을 수 없다.
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        Refresh();
    }

    private void OnEnable() => GameEvents.OnStageChanged += HandleStageChanged;
    private void OnDisable() => GameEvents.OnStageChanged -= HandleStageChanged;

    private void Update()
    {
        bool bannerVisible = banner != null && banner.gameObject.activeSelf;
        if (bannerVisible != bannerWasVisible)
        {
            bannerWasVisible = bannerVisible;
            canvasGroup.alpha = bannerVisible ? 0f : 1f;
        }

        // 배너에 가려져 있는 동안은 안 보이니 애니메이션을 돌릴 이유가 없다.
        if (!bannerVisible) PulseCurrentDot();
    }

    // 현재 위치 점만 크기를 사인파로 흔들어서 "지금 여기 있다"는 걸 계속 눈에 띄게 한다 - 알파는
    // 항상 1로 고정한다. 알파까지 같이 낮추면 점이 작아지는 순간(가장자리가 반투명해지는 원 스프라이트
    // 특성과 겹쳐) 밑에 깔린 트랙 세그먼트 선이 원 안에 비쳐 보였다.
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
        // 그대로 믿지 않고 클램프 - 디버그 점프의 중간 상태에선 StageManager의 SubStage가 잠깐
        // 1..DotCount 밖으로 나갈 수 있고, 벗어난 점 인덱스는 아래 마커 위치 계산에서 예외를 던진다.
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

        // 세그먼트 i는 (i+1)번 점과 (i+2)번 점을 잇는다 - 오른쪽 점에 도착한 순간(그 점이 완료든
        // 현재 위치든) 그 구간은 이미 지나온 길이므로 바로 연두색으로 물든다.
        for (int i = 0; i < segments.Length; i++)
        {
            int rightStage = i + 2;
            segments[i].color = currentSubStage >= rightStage ? CompletedDotColor : TrackColor;
        }

        if (arrowRect != null)
        {
            float x = dots[currentSubStage - 1].rectTransform.anchoredPosition.x;
            arrowRect.anchoredPosition = new Vector2(x, arrowOffsetY);
            Image currentImage = dots[currentSubStage - 1];
            var arrowText = arrowRect.GetComponent<Text>();
            if (arrowText != null) arrowText.color = currentImage.color;
        }
    }
}
