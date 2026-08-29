using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 현재 제작된 마지막 메인 스테이지(StageManager.MaxMainStage)의 보스를 클리어하면 뜨는 종료
// 화면. TitleScreenView와 같은 방식으로 코드로만 자기 UI를 짓는다 - 씬에는 빈 GameObject
// 하나에 이 컴포넌트만 붙이면 된다. "Coming Soon"은 지금 여기가 실제 콘텐츠의 끝이라는 뜻이라
// 다음 스테이지로 이어지는 배너 대신 이 화면에서 완전히 멈춘다.
[RequireComponent(typeof(RectTransform))]
public class GameCompleteView : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.4f;

    private CanvasGroup canvasGroup;
    private Text timeLabel;
    private Button restartButton;
    private Action onRestart;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        // TitleScreenView와 동일한 이유: 게임플레이 위에 얹는 오버레이라 항상 맨 위에 그려져야 한다.
        transform.SetAsLastSibling();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Build();

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void Show(float elapsedSeconds, Action onRestartCallback)
    {
        onRestart = onRestartCallback;

        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        if (timeLabel != null) timeLabel.text = $"CLEAR TIME  {minutes:00}:{seconds:00}";

        if (restartButton != null) restartButton.interactable = true;
        canvasGroup.blocksRaycasts = true;
        gameObject.SetActive(true);

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        fadeRoutine = null;
    }

    private void HandleRestartPressed()
    {
        // 재진입 방지 - 씬 리로드가 실제로 일어나기 전까지 두 번째 클릭이 콜백을 또 태우지 않게 한다.
        if (restartButton != null) restartButton.interactable = false;

        Action callback = onRestart;
        onRestart = null;
        callback?.Invoke();
    }

    private void Build()
    {
        var rect = (RectTransform)transform;
        Stretch(rect);

        Image backdrop = gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.05f, 0.04f, 0.09f, 0.92f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildText("TitleText", "ALL STAGES CLEAR!", font, 88, FontStyle.Bold,
            new Vector2(0f, -160f), new Vector2(1600f, 140f), Color.white);
        BuildText("SubtitleText", "More stages coming soon", font, 40, FontStyle.Italic,
            new Vector2(0f, -260f), new Vector2(1200f, 80f), new Color(0.85f, 0.85f, 0.9f));
        timeLabel = BuildText("TimeText", "CLEAR TIME  00:00", font, 44, FontStyle.Bold,
            new Vector2(0f, -330f), new Vector2(900f, 70f), new Color(1f, 0.85f, 0.4f));

        BuildRestartButton(font);
    }

    private Text BuildText(string name, string content, Font font, int fontSize, FontStyle style,
        Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.text = content;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(3f, -3f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        return label;
    }

    private void BuildRestartButton(Font font)
    {
        var go = new GameObject("RestartButton", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.404f, 0.337f, 0.596f);

        restartButton = go.AddComponent<Button>();
        restartButton.targetGraphic = image;
        var colors = restartButton.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        restartButton.colors = colors;
        restartButton.onClick.AddListener(HandleRestartPressed);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 140f);
        rt.sizeDelta = new Vector2(300f, 100f);

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 40;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "RESTART";

        var labelShadow = labelGo.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0.07f, 0.05f, 0.13f, 0.6f);
        labelShadow.effectDistance = new Vector2(0f, -2f);

        Stretch(labelGo.GetComponent<RectTransform>());
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
