using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ClearBannerView : CancellableBannerView
{
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private Text label;

    [SerializeField] private float popInDuration = 0.28f;
    [SerializeField] private float holdDuration = 0.9f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    [SerializeField] private float punchScale = 1.18f;
    [SerializeField] private float startScale = 0.45f;
    // 페이드되는 동안 위로 얼마나 떠오르는지 - 그냥 훅 사라지는 게 아니라 퇴장하듯 보이게 한다.
    [SerializeField] private float riseDistance = 45f;

    private Vector2 restPosition;

    public float TotalPlayDuration => popInDuration + holdDuration + fadeOutDuration;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        restPosition = scaleTarget.anchoredPosition;

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void Show(string text = "CLEAR !")
    {
        if (routine != null) StopCoroutine(routine);

        label.text = text;

        gameObject.SetActive(true);
        routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        scaleTarget.anchoredPosition = restPosition;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popInDuration);

            canvasGroup.alpha = Mathf.Clamp01(k * 3f);
            scaleTarget.localScale = Vector3.one * Easing.PopCurve(k, startScale, punchScale);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        scaleTarget.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(holdDuration);

        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeOutDuration);

            canvasGroup.alpha = 1f - k;
            scaleTarget.anchoredPosition = restPosition + Vector2.up * (riseDistance * k);
            scaleTarget.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, k);
            yield return null;
        }

        routine = null;
        canvasGroup.alpha = 0f;
        scaleTarget.localScale = Vector3.one;
        scaleTarget.anchoredPosition = restPosition;
        gameObject.SetActive(false);
    }
}
