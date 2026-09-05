using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageBannerView : CancellableBannerView
{
    [SerializeField] private Text bannerText;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private float displayDuration = 1f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float popStartScale = 1.6f;
    [SerializeField] private float openingHoldDuration = 0.4f;

    private RectTransform textRect;

    private bool hasShownOnce;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        textRect = bannerText.rectTransform;
        // 씬이 어떤 텍스트로 저장되어 있었든 상관없이 빈 상태로 시작한다.
        textGroup.alpha = 0f;

        // 배너는 기본적으로 비활성화 상태로 시작한다 - 안내가 필요할 때 Show()를 호출한다.
        gameObject.SetActive(false);
    }

    public void Show(string text, Action onComplete)
    {
        Cancel();

        bannerText.text = text;

        bool openOnBlack = !hasShownOnce;
        hasShownOnce = true;

        canvasGroup.alpha = openOnBlack ? 1f : 0f;
        textGroup.alpha = 0f;
        textRect.localScale = Vector3.one * popStartScale;
        gameObject.SetActive(true);
        routine = StartCoroutine(PlayThenHide(openOnBlack, onComplete));
    }

    private IEnumerator PlayThenHide(bool openOnBlack, Action onComplete)
    {
        yield return null;

        if (openOnBlack)
        {
            yield return new WaitForSecondsRealtime(openingHoldDuration);
        }
        else
        {
            yield return FadeGroup(canvasGroup, 0f, 1f, fadeDuration);
        }

        yield return PopTextIn();
        yield return new WaitForSecondsRealtime(displayDuration);

        yield return FadeGroup(canvasGroup, 1f, 0f, fadeDuration);

        routine = null;
        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    private IEnumerator PopTextIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            textGroup.alpha = p;
            textRect.localScale = Vector3.one * Mathf.LerpUnclamped(popStartScale, 1f, Easing.OutBack(p, 1.2f));
            yield return null;
        }

        textGroup.alpha = 1f;
        textRect.localScale = Vector3.one;
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float fromAlpha, float toAlpha, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(t / duration));
            yield return null;
        }

        group.alpha = toAlpha;
    }
}
