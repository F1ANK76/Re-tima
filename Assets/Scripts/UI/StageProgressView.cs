using UnityEngine;
using UnityEngine.UI;

// The 5-dot track directly under the "Stage X-Y" HUD label (see HudController) - the four
// normal substages plus the boss, so a glance at the row shows where the run sits within the
// current main stage and makes the boss read as a fixed last stop rather than a number the
// player has to remember. Rebuilt only once (Awake); a stage change only ever moves the
// current marker and restyles the five dots, never changes their count or layout.
public class StageProgressView : MonoBehaviour
{
    // Four normal substages plus the boss - same constant StageManager itself gauges the
    // boss substage against, so the two can never drift out of sync.
    private const int DotCount = StageManager.BossSubStage;

    // Hidden for as long as this is up - the banner already owns top-center for that beat
    // (see StageBannerView), and the track popping in underneath a card that's mid pop-in/
    // fade-out itself read as visual clutter rather than a second readout of the same info.
    [SerializeField] private StageBannerView banner;

    [SerializeField] private float trackWidth = 220f;
    [SerializeField] private float dotSize = 16f;
    [SerializeField] private float bossDotScale = 1.35f;
    [SerializeField] private float currentDotScale = 1.2f;
    [SerializeField] private float lineThickness = 3f;
    [SerializeField] private float arrowOffsetY = 16f;

    private static readonly Color TrackColor = new Color(0.32f, 0.32f, 0.38f);
    private static readonly Color FutureDotColor = new Color(0.32f, 0.32f, 0.38f);
    private static readonly Color CompletedDotColor = new Color(0.85f, 0.85f, 0.9f);
    private static readonly Color CurrentDotColor = new Color(0.68f, 0.58f, 1f);
    // The boss dot is always tinted toward red/orange regardless of completed/current/future
    // state - the point is "this slot is the boss" being readable at a glance, not just
    // whichever generic state color the other four dots use.
    private static readonly Color BossFutureColor = new Color(0.55f, 0.26f, 0.18f);
    private static readonly Color BossReadyColor = new Color(1f, 0.45f, 0.15f);

    private Font font;
    private readonly Image[] dots = new Image[DotCount];
    private RectTransform arrowRect;
    private readonly float[] dotX = new float[DotCount];

    private int currentSubStage = 1;

    private CanvasGroup canvasGroup;
    // Tracked so Update only writes the CanvasGroup on an actual transition, not every frame.
    private bool bannerWasVisible;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        // A CanvasGroup rather than SetActive(false) for the hide below - toggling
        // activeSelf would run OnDisable and drop the OnStageChanged subscription, so a
        // substage change landing while the banner is up would never reach this until the
        // next one happened to fire after re-enabling.
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
        if (bannerVisible == bannerWasVisible) return;

        bannerWasVisible = bannerVisible;
        canvasGroup.alpha = bannerVisible ? 0f : 1f;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        // Clamped rather than trusted outright - StageManager's own SubStage can briefly sit
        // outside 1..DotCount during a debug jump's intermediate state, and a stray dot index
        // would throw building the marker position below.
        currentSubStage = Mathf.Clamp(subStage, 1, DotCount);
        Refresh();
    }

    private void Build()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(trackWidth + dotSize, 40f);

        // Background track, added first so every dot draws on top of it.
        var line = new GameObject("Track", typeof(RectTransform));
        line.transform.SetParent(transform, false);
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax = new Vector2(0.5f, 0.5f);
        lineRt.pivot = new Vector2(0.5f, 0.5f);
        lineRt.anchoredPosition = Vector2.zero;
        lineRt.sizeDelta = new Vector2(trackWidth, lineThickness);
        line.AddComponent<Image>().color = TrackColor;

        float step = trackWidth / (DotCount - 1);
        for (int i = 0; i < DotCount; i++)
        {
            dotX[i] = -trackWidth * 0.5f + i * step;

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

        // A simple "▼" sitting above whichever dot is current - points straight down at it,
        // and slides to a new dot rather than being rebuilt whenever the substage advances.
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

        if (arrowRect != null)
        {
            arrowRect.anchoredPosition = new Vector2(dotX[currentSubStage - 1], arrowOffsetY);
            Image currentImage = dots[currentSubStage - 1];
            var arrowText = arrowRect.GetComponent<Text>();
            if (arrowText != null) arrowText.color = currentImage.color;
        }
    }

    // One filled circle, shared by every dot - a hard-edged disc reads as a fixed waypoint
    // rather than a glow, so unlike the soft falloff textures elsewhere in this game's pickups
    // this one anti-aliases only its outer rim, not its whole body.
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
