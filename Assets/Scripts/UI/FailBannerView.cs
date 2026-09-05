using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class FailBannerView : MonoBehaviour
{
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private Text label;

    [SerializeField] private float popInDuration = 0.25f;
    [SerializeField] private float holdDuration = 1f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    [SerializeField] private float punchScale = 1.15f;
    [SerializeField] private float startScale = 0.5f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public IEnumerator Play(string text = "FAIL !")
    {
        label.text = text;

        scaleTarget.localScale = Vector3.one * startScale;
        gameObject.SetActive(true);

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popInDuration);

            canvasGroup.alpha = k;
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
            yield return null;
        }

        canvasGroup.alpha = 0f;
        scaleTarget.localScale = Vector3.one;
        gameObject.SetActive(false);
    }
}
