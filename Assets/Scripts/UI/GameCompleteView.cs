using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class GameCompleteView : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.4f;

    [SerializeField] private Text timeLabel;
    [SerializeField] private Button restartButton;

    private CanvasGroup canvasGroup;
    private Action onRestart;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        // TitleScreenView와 동일한 이유: 게임플레이 위에 얹는 오버레이라 항상 맨 위에 그려져야 한다.
        transform.SetAsLastSibling();
        canvasGroup = GetComponent<CanvasGroup>();

        restartButton.onClick.AddListener(HandleRestartPressed);

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void Show(float elapsedSeconds, Action onRestartCallback)
    {
        onRestart = onRestartCallback;

        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timeLabel.text = $"CLEAR TIME  {minutes:00}:{seconds:00}";

        restartButton.interactable = true;
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
        restartButton.interactable = false;

        Action callback = onRestart;
        onRestart = null;
        callback?.Invoke();
    }
}
