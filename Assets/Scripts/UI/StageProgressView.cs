using UnityEngine;
using UnityEngine.UI;

// "Stage X-Y" HUD 라벨(HudController) 바로 아래의 5점 트랙 - 일반 서브스테이지 4개 + 보스.
// 한 번 보면 메인 스테이지 안에서 진행 위치가 어디쯤인지 알 수 있고, 보스가 기억해야 할 숫자가
// 아니라 고정된 마지막 정거장처럼 읽히게 한다. Awake에서 딱 한 번만 빌드되며, 스테이지가 바뀌어도
// 현재 마커를 옮기고 점 스타일만 바꿀 뿐 개수나 레이아웃은 절대 변하지 않는다.
public class StageProgressView : MonoBehaviour
{
    // 일반 서브스테이지 4개에 보스를 더한 값 - StageManager 자신이 보스 서브스테이지를
    // 판별할 때 쓰는 것과 동일한 상수이므로, 이 둘이 서로 어긋날 일이 없다.
    private const int DotCount = StageManager.BossSubStage;

    // 이게 떠 있는 동안엔 숨는다(StageBannerView) - 화면 상단 중앙은 이미 배너가 차지하고 있고,
    // 팝인/페이드아웃 중인 카드 밑에서 트랙까지 같이 팝인되면 정보 재확인이 아니라 잡음으로 읽혔다.
    [SerializeField] private StageBannerView banner;

    [SerializeField] private float trackWidth = 220f;
    [SerializeField] private float dotSize = 16f;
    [SerializeField] private float bossDotScale = 1.35f;
    [SerializeField] private float currentDotScale = 1.2f;
    [SerializeField] private float lineThickness = 3f;
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

    private Font font;
    private readonly Image[] dots = new Image[DotCount];
    // 점 사이 구간 하나하나 - segments[i]는 dots[i]와 dots[i+1] 사이를 잇는다. 이걸 하나의
    // 통짜 트랙 대신 구간별로 나눠서, 클리어한 구간만 따로 연두색으로 물들일 수 있게 한다.
    private readonly Image[] segments = new Image[DotCount - 1];
    private RectTransform arrowRect;
    private readonly float[] dotX = new float[DotCount];

    private int currentSubStage = 1;

    private CanvasGroup canvasGroup;
    // Update가 매 프레임이 아니라 실제로 상태가 전환될 때만 CanvasGroup에 쓰도록 이 값을 추적한다.
    private bool bannerWasVisible;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        // 숨김은 SetActive(false)가 아니라 CanvasGroup으로 - activeSelf를 토글하면 OnDisable이
        // OnStageChanged 구독을 끊어서, 배너가 떠 있는 동안 바뀐 서브스테이지는 재활성화 후
        // 우연히 다음 이벤트가 오기 전까지 절대 받을 수 없다.
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        Build();
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

    private void Build()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(trackWidth + dotSize, 40f);

        float step = trackWidth / (DotCount - 1);
        for (int i = 0; i < DotCount; i++)
        {
            dotX[i] = -trackWidth * 0.5f + i * step;
        }

        // 점 사이 구간을 하나의 통짜 트랙이 아니라 개별 세그먼트로 깐다 - 점보다 먼저 추가해서
        // 항상 점 밑에 깔리고, 나중에 Refresh에서 구간별로 색을 따로 칠할 수 있다.
        for (int i = 0; i < segments.Length; i++)
        {
            var segGo = new GameObject("Segment" + (i + 1), typeof(RectTransform));
            segGo.transform.SetParent(transform, false);
            var segRt = segGo.GetComponent<RectTransform>();
            segRt.anchorMin = new Vector2(0.5f, 0.5f);
            segRt.anchorMax = new Vector2(0.5f, 0.5f);
            segRt.pivot = new Vector2(0.5f, 0.5f);
            segRt.anchoredPosition = new Vector2((dotX[i] + dotX[i + 1]) * 0.5f, 0f);
            segRt.sizeDelta = new Vector2(dotX[i + 1] - dotX[i], lineThickness);

            var segImage = segGo.AddComponent<Image>();
            segImage.color = TrackColor;
            segments[i] = segImage;
        }

        for (int i = 0; i < DotCount; i++)
        {
            var dotGo = new GameObject("Dot" + (i + 1), typeof(RectTransform));
            dotGo.transform.SetParent(transform, false);
            var dotRt = dotGo.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0.5f, 0.5f);
            dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = new Vector2(dotX[i], 0f);
            dotRt.sizeDelta = new Vector2(dotSize, dotSize);

            var image = dotGo.AddComponent<Image>();
            image.sprite = CircleSprite;
            dots[i] = image;
        }

        // 현재 점 위에 그냥 "▼"를 하나 띄워서 그 점을 바로 아래로 가리킨다 - 서브스테이지가
        // 진행될 때마다 다시 만드는 게 아니라 새 점 위치로 슬라이드된다.
        var arrowGo = new GameObject("Arrow", typeof(RectTransform));
        arrowGo.transform.SetParent(transform, false);
        arrowRect = arrowGo.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(24f, 20f);

        var arrowText = arrowGo.AddComponent<Text>();
        arrowText.font = font;
        arrowText.fontSize = 18;
        arrowText.fontStyle = FontStyle.Bold;
        arrowText.alignment = TextAnchor.MiddleCenter;
        arrowText.color = CurrentDotColor;
        arrowText.text = "▼";
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
            arrowRect.anchoredPosition = new Vector2(dotX[currentSubStage - 1], arrowOffsetY);
            Image currentImage = dots[currentSubStage - 1];
            var arrowText = arrowRect.GetComponent<Text>();
            if (arrowText != null) arrowText.color = currentImage.color;
        }
    }

    // 모든 점이 공유하는 채워진 원 하나 - 또렷한 원반이 은은한 빛보다 고정된 경유지처럼 보이므로,
    // 다른 픽업들의 부드러운 감쇠 텍스처와 달리 원 전체가 아니라 바깥 테두리만 안티앨리어싱한다.
    private static Sprite circleSprite;
    private static Sprite CircleSprite
    {
        get
        {
            if (circleSprite != null) return circleSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center * 0.92f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 1f - Mathf.Clamp01((d - radius) / 2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return circleSprite;
        }
    }
}
