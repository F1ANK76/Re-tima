using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴: 실제 진행 중인 씬 위에 게임 이름과 Play 버튼을 띄운다. 그래서 플레이어가
// 아무것도 누르기 전부터 주인공은 이미 숲을 달리고 있다 - 메뉴는 게임의 그림이 아니라
// 실제 게임 그 자체를 보여준다. 손으로 배선할 것도, 팔레트와 어긋날 여지도 없이 자기
// 자신의 껍데기를 온전히 갖도록 코드로 빌드했다.
public class TitleScreenView : MonoBehaviour
{
    [SerializeField] private string titleText = "Re:tima";
    [SerializeField] private float fadeOutDuration = 0.45f;

    // 숨기는 UI 요소
    [SerializeField] private GameObject[] hideWhileShowing;

    // 플레이어 전투 시 위치값
    [SerializeField] private Transform centerWhileShowing;

    private CanvasGroup canvasGroup;
    private Button playButton;
    private Sprite backdropSprite;
    private Sprite buttonSprite;
    private Sprite skySplitSprite;
    private Action onPlay;
    private bool[] hiddenPriorState;
    private bool playPressed;
    // Show()는 게임당 한 번만 호출되므로, 매번 다시 캡처할 필요 없이 여기서 한 번만
    // 저장해두면 된다 - Camera.main이 아직 없어 CenterSubject가 조기 종료되는 경우에도
    // RestoreSubject가 되돌릴 값은 항상 확보되어 있다.
    private Vector3 originalSubjectPosition;

    private void Awake()
    {
        // 맨 위에 그리기
        transform.SetAsLastSibling();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (centerWhileShowing != null) originalSubjectPosition = centerWhileShowing.position;

        Build();
    }

    public void Show(Action playCallback)
    {
        onPlay = playCallback;
        playPressed = false;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        if (playButton != null) playButton.interactable = true;
        HideHud();
        CenterSubject();
        gameObject.SetActive(true);
    }

    private void CenterSubject()
    {
        Camera cam = Camera.main;
        if (centerWhileShowing == null || cam == null) return;

        // 카메라의 x 좌표에 맞추면, 씬의 전투 간격이 어떻게 되어 있든 그를 중앙선에
        // 위치시킬 수 있다.
        Vector3 p = originalSubjectPosition;
        p.x = cam.transform.position.x;
        centerWhileShowing.position = p;
    }

    private void RestoreSubject()
    {
        if (centerWhileShowing == null) return;

        centerWhileShowing.position = originalSubjectPosition;
    }

    private void HandlePlayPressed()
    {
        // 아래에서 interactable을 꺼두면 두 번째 클릭은 막을 수 있지만 두 번째 호출 자체는
        // 막지 못한다 - 그리고 런이 두 번 시작되어서도 안 되고, 이미 비활성화한 오브젝트에
        // 페이드 코루틴이 또 생성되어서도 안 된다.
        if (playPressed) return;
        playPressed = true;

        HideAndFadeOut();

        // 페이드 후가 아니라 페이드 전에 발동한다: 스테이지 카드가 이 화면 아래에서 불투명한
        // 상태로 올라오므로, 플레이어에게는 그 사이에 게임플레이 화면이 잠깐 번쩍이는 게
        // 아니라 메뉴가 곧바로 "Stage 1-1"로 넘어가는 것처럼 보인다.
        onPlay?.Invoke();
        onPlay = null;
    }

    // Play를 눌렀을 때와 동일하게 숨기지만 핸드오프 콜백은 빼둔다 - 이미 자체적인 스폰
    // 플로우를 진행하는 진입점(디버그 스테이지 점프 패널) 등, Play가 시작시켰을 무언가를
    // 다시 반복할 필요 없이 그저 타이틀 껍데기만 치워버리면 되는 경우를 위한 것이다.
    public void Dismiss()
    {
        if (playPressed) return;
        playPressed = true;

        onPlay = null;
        HideAndFadeOut();
    }

    private void HideAndFadeOut()
    {
        if (playButton != null) playButton.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 런이 시작되기 전에 복원하므로, StageManager 자체의 반복 토글 표시/숨김이 한
        // 프레임 뒤에 취소되는 게 아니라 최종적으로 반영된다. 주인공도 같은 타이밍에
        // 전투 자리로 되돌아간다 - 바로 이 프레임에 아래의 스테이지 카드가 불투명해지므로,
        // 이 이동은 화면에 전혀 보이지 않는다.
        RestoreHud();
        RestoreSubject();

        StartCoroutine(FadeOutAndHide());
    }

    private void HideHud()
    {
        if (hideWhileShowing == null) return;

        hiddenPriorState = new bool[hideWhileShowing.Length];
        for (int i = 0; i < hideWhileShowing.Length; i++)
        {
            if (hideWhileShowing[i] == null) continue;
            hiddenPriorState[i] = hideWhileShowing[i].activeSelf;
            hideWhileShowing[i].SetActive(false);
        }
    }

    private void RestoreHud()
    {
        if (hideWhileShowing == null || hiddenPriorState == null) return;

        for (int i = 0; i < hideWhileShowing.Length && i < hiddenPriorState.Length; i++)
        {
            if (hideWhileShowing[i] != null) hideWhileShowing[i].SetActive(hiddenPriorState[i]);
        }
        hiddenPriorState = null;
    }

    private IEnumerator FadeOutAndHide()
    {
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void Build()
    {
        // 타이틀 화면 오브젝트의 RectTransform이 해상도가 뭐든 상관없이 화면 전체를 정확히 꽉 채우게 됨.
        var rect = GetComponent<RectTransform>();
        Stretch(rect);

        // 실제 씬 위에서는 이게 그저 스크림(scrim)일 뿐이다 - 맨 위와 맨 아래는 어둡고
        // 가운데는 투명하다 - 그래서 타이틀과 버튼이 밑에 깔린 게임플레이를 가리지
        // 않으면서도 잘 읽히게 한다.
        backdropSprite = CreateEdgeScrim();
        Image backdrop = gameObject.AddComponent<Image>();
        backdrop.sprite = backdropSprite;

        // 아래의 타이틀/버튼보다 먼저 추가해서 형제 순서상 그것들 뒤에 오도록 한다.
        BuildDayNightSkySplit();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildTitle(font);
        BuildPlayButton(font);
    }

    // 왼쪽은 낮 하늘, 오른쪽은 밤 하늘로 페이드되는, 실제 씬의 나무 라인 위쪽에 고정된
    // 배경 - 플레이어가 아직 어느 것도 고르기 전에 두 스테이지(1 = 낮, 2 = 밤)를 미리
    // 보여준다. 실제 스카이박스는 "절반은 낮, 절반은 밤" 같은 개념이 없으므로(그 블렌드는
    // 씬 전체에 단일 슬라이더로 적용된다 - StageMoodController 참고), 이건 나무에 닿을
    // 즈음엔 완전히 투명해지는 그려진 띠로 그걸 흉내내며, 그 아래의 실제 지면/캐릭터는
    // 전혀 건드리지 않는다.
    private void BuildDayNightSkySplit()
    {
        var go = new GameObject("DayNightSkySplit", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        skySplitSprite = CreateDayNightSkySplitSprite();
        var image = go.AddComponent<Image>();
        image.sprite = skySplitSprite;
        image.color = Color.white;
        image.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        // 이 띠의 불투명한 부분이(아래 vanishFrac 참고) 페이드되기 전까지 나무 라인 위의
        // 하늘 전체 영역(이 캔버스의 1920x1080 기준 해상도에서 ~330px)을 충분히 덮을
        // 만큼 크게 잡았다 - 분할선이 화면 맨 위를 따라 그어진 띠가 아니라 하늘 자체가
        // 나뉜 것처럼 읽혀야 한다.
        rt.sizeDelta = new Vector2(0f, 460f);
    }

    // StageMoodController가 런타임에 실제로 크로스페이드하는 것과 동일한 팔레트(그
    // 태양/안개 색)이므로, 타이틀의 그려진 분할선이 이 화면만을 위해 새로 지어낸 세
    // 번째의 무관한 색조가 아니라 게임 자체가 보여주는 것과 동일한 두 분위기로 읽힌다.
    private static readonly Color DaySkyTop = new Color(0.35f, 0.55f, 0.92f);
    private static readonly Color DaySkyHorizon = new Color(0.85f, 0.88f, 0.93f);
    private static readonly Color NightSkyTop = new Color(0.08f, 0.09f, 0.24f);
    private static readonly Color NightSkyHorizon = new Color(0.36f, 0.25f, 0.42f);

    private static Sprite CreateDayNightSkySplitSprite()
    {
        const int width = 480, height = 230;
        // 가운데의 이음매. 일부러 좁게 잡았다 - 어느 쪽도 뚜렷한 하늘처럼 보이지 않게
        // 만드는 넓은 그라데이션이 아니라, 하늘이 깔끔하게 반으로 갈린 것처럼 읽혀야
        // 한다. 그래도 단일 열이 아니라 몇 픽셀에 걸쳐 부드럽게 처리하여, 화면이 늘어나서
        // 표시될 때 삐죽삐죽한 선으로 앨리어싱되지 않게 한다.
        const float seamHalfWidth = 0.025f;
        // 이 높이 비율 아래로는 띠가 완전히 투명해진다. (띠의 대부분에 걸쳐 페이드되는
        // 대신) 작게 잡아서, 두 하늘이 하늘이 트인 영역 거의 전체에 걸쳐 선명하게 유지되고
        // 뚜렷하게 갈라져 보이며, 띠의 맨 아래쪽 - BuildDayNightSkySplit이 나무 라인에
        // 닿도록 크기를 잡는 지점 - 에서만 짧게 인계되어, 딱딱한 경계선으로 끝나는
        // 대신 실제 씬으로 자연스럽게 섞여든다.
        const float vanishFrac = 0.12f;

        var rng = new System.Random(20260824);
        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float ty = (float)y / (height - 1);
            // TitleCardBackdrop 자체의 하늘 그라데이션이 쓰는 것과 동일한 이징 - 그래서 이
            // 게임에서 그려지는 두 하늘이 하나의 그라데이션 형태를 공유하게 된다.
            Color day = Color.Lerp(DaySkyHorizon, DaySkyTop, Mathf.Pow(ty, 0.6f));
            Color night = Color.Lerp(NightSkyHorizon, NightSkyTop, Mathf.Pow(ty, 0.6f));

            float rowAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(vanishFrac, 1f, ty));

            for (int x = 0; x < width; x++)
            {
                float tx = (float)x / (width - 1);
                float nightAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f - seamHalfWidth, 0.5f + seamHalfWidth, tx));

                Color c = Color.Lerp(day, night, nightAmount);
                float n = ((float)rng.NextDouble() - 0.5f) / 255f;
                pixels[y * width + x] = new Color(c.r + n, c.g + n, c.b + n, rowAlpha);
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 1f));
    }

    private void BuildTitle(Font font)
    {
        var go = new GameObject("TitleText", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = 92;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.color = Color.white;
        label.text = titleText;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.16f, 0.11f, 0.25f, 0.55f);
        shadow.effectDistance = new Vector2(5f, -5f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -110f);
        rt.sizeDelta = new Vector2(1600f, 180f);
    }

    private void BuildPlayButton(Font font)
    {
        var go = new GameObject("PlayButton", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        buttonSprite = CreatePlayButtonSprite();
        var image = go.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.color = Color.white;

        playButton = go.AddComponent<Button>();
        playButton.targetGraphic = image;
        var colors = playButton.colors;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        playButton.colors = colors;
        playButton.onClick.AddListener(HandlePlayPressed);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 130f);
        // 스프라이트와 맞춰서, 아트워크가 비율에 맞지 않게 늘어나지 않도록 한다.
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 44;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "PLAY";

        var labelShadow = labelGo.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0.07f, 0.05f, 0.13f, 0.6f);
        labelShadow.effectDistance = new Vector2(0f, -2f);

        // 스프라이트 전체가 아니라 도드라진 표면(face)을 기준으로 가운데 정렬해서, 옆면과
        // 드롭 섀도우 때문에 캡션이 아래로 처지지 않게 한다.
        var labelRt = labelGo.GetComponent<RectTransform>();
        Stretch(labelRt);
        labelRt.offsetMin = new Vector2(0f, ButtonFaceBottom);
        labelRt.offsetMax = new Vector2(0f, ButtonFaceTop - ButtonHeight);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 폭이 4px밖에 안 된다: 가로 방향으로는 아무것도 변하지 않고, 어차피 Image가 화면
    // 전체로 늘려서 표시한다.
    private static Sprite CreateEdgeScrim()
    {
        const int width = 4, height = 256;
        var tint = new Color(0.098f, 0.071f, 0.157f);
        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);   // 아래는 0, 위는 1
            float top = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, t)) * 0.45f;
            float bottom = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0f, t)) * 0.40f;
            float alpha = Mathf.Max(top, bottom);

            for (int x = 0; x < width; x++)
                pixels[y * width + x] = new Color(tint.r, tint.g, tint.b, alpha);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
    }

    // 생성된 버튼 스프라이트의 레이아웃, 아래쪽 가장자리로부터의 텍스처 픽셀 단위.
    private const int ButtonWidth = 340;
    private const int ButtonHeight = 118;
    private const float ButtonFaceTop = 114f;
    private const float ButtonFaceBottom = 24f;
    private const float ButtonLipBottom = 10f;
    private const float ButtonRadius = 26f;

    // 실제로 두께가 있는 키캡: 위에는 밝은 면, 그 아래로 드러나는 어두운 옆면, 그리고
    // 그 밑의 컨택트 섀도우. Unity의 기본 UI 스프라이트는 딱딱한 평면 사각형이라, 뒤의
    // 씬 옆에 놓이면 스타일이 안 입혀진 것처럼 보인다.
    private static Sprite CreatePlayButtonSprite()
    {
        var faceTop = new Color(0.404f, 0.337f, 0.596f);
        var faceBottom = new Color(0.235f, 0.184f, 0.361f);
        var lip = new Color(0.137f, 0.106f, 0.216f);
        var highlight = new Color(0.640f, 0.580f, 0.808f);
        var shadow = new Color(0.055f, 0.039f, 0.098f);

        var pixels = new Color[ButtonWidth * ButtonHeight];

        for (int y = 0; y < ButtonHeight; y++)
        {
            for (int x = 0; x < ButtonWidth; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                Color result = new Color(0f, 0f, 0f, 0f);

                // 버튼이 놓인 바닥에 그림자를 드리워서, 배경으로부터 떠 있는 것처럼 보이게 한다.
                float shadowDist = RoundedBoxDistance(px, py, 14f, 2f, ButtonWidth - 14f, 20f, 14f);
                float shadowAlpha = Mathf.Clamp01(-shadowDist / 9f) * 0.30f;
                if (shadowAlpha > 0f) result = new Color(shadow.r, shadow.g, shadow.b, shadowAlpha);

                // 전체 높이로 그려지는 옆면 - 실제로는 표면 아래쪽 띠 부분만 보인다.
                float lipCoverage = Coverage(RoundedBoxDistance(px, py, 0f, ButtonLipBottom,
                    ButtonWidth, ButtonFaceTop, ButtonRadius));
                if (lipCoverage > 0f) result = Over(new Color(lip.r, lip.g, lip.b, lipCoverage), result);

                float faceCoverage = Coverage(RoundedBoxDistance(px, py, 0f, ButtonFaceBottom,
                    ButtonWidth, ButtonFaceTop, ButtonRadius));
                if (faceCoverage > 0f)
                {
                    float t = Mathf.InverseLerp(ButtonFaceBottom, ButtonFaceTop, py);
                    Color face = Color.Lerp(faceBottom, faceTop, t);

                    // 위쪽 가장자리를 따라 밝은 테두리를 넣으면 위에 광원이 있는 것처럼 보인다.
                    float rim = Mathf.Clamp01((py - (ButtonFaceTop - 7f)) / 7f);
                    face = Color.Lerp(face, highlight, rim * rim * 0.55f);

                    result = Over(new Color(face.r, face.g, face.b, faceCoverage), result);
                }

                pixels[y * ButtonWidth + x] = result;
            }
        }

        var texture = new Texture2D(ButtonWidth, ButtonHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, ButtonWidth, ButtonHeight), new Vector2(0.5f, 0.5f));
    }

    // 둥근 사각형까지의 부호 있는 거리: 안쪽이면 음수, 바깥쪽이면 양수.
    private static float RoundedBoxDistance(float px, float py, float x0, float y0, float x1, float y1, float radius)
    {
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
        float halfW = (x1 - x0) * 0.5f, halfH = (y1 - y0) * 0.5f;

        float qx = Mathf.Abs(px - cx) - (halfW - radius);
        float qy = Mathf.Abs(py - cy) - (halfH - radius);

        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - radius;
    }

    private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

    private static Color Over(Color src, Color dst)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

        float r = (src.r * src.a + dst.r * dst.a * (1f - src.a)) / a;
        float g = (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a;
        float b = (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a;
        return new Color(r, g, b, a);
    }

    private void OnDestroy()
    {
        DestroyGenerated(backdropSprite);
        DestroyGenerated(buttonSprite);
        DestroyGenerated(skySplitSprite);
    }

    // 런타임에 생성되므로, 이걸 뒤에서 대신 수거해줄 다른 무언가가 없다.
    private static void DestroyGenerated(Sprite sprite)
    {
        if (sprite == null) return;
        Destroy(sprite.texture);
        Destroy(sprite);
    }
}
