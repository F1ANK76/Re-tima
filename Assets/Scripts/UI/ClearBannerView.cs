using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// One-shot pop-in/hold/fade-out flourish, top-center. Originally just "CLEAR !" for an
// elite/boss kill; Show(text) now reuses the same animation for the elite/boss *arrival*
// callouts too ("Elite Boss !", "Final Boss !") - same treatment, different moment, different
// words. Purely cosmetic - nothing waits on it, so it runs on unscaled time and simply plays
// itself out over whatever else is happening (the victory pose, the next stage banner).
[RequireComponent(typeof(CanvasGroup))]
public class ClearBannerView : MonoBehaviour
{
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private Text label;

    [SerializeField] private float popInDuration = 0.28f;
    [SerializeField] private float holdDuration = 0.9f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    // Overshoot the settled size on the way in, then ease back down to it - a straight
    // 0->1 scale reads as the text simply appearing, with no sense of impact.
    [SerializeField] private float punchScale = 1.18f;
    [SerializeField] private float startScale = 0.45f;
    // How far it drifts upward while fading, so it exits rather than blinking out.
    [SerializeField] private float riseDistance = 45f;

    private CanvasGroup canvasGroup;
    private Vector2 restPosition;
    private Coroutine routine;

    // Pop-in through fully faded out - callers that need to wait for the banner to be
    // completely gone (rather than just shown) read this instead of hardcoding a duration
    // that would drift the moment these three are retuned.
    public float TotalPlayDuration => popInDuration + holdDuration + fadeOutDuration;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (scaleTarget == null) scaleTarget = (RectTransform)transform;

        // Captured before the first play, since the animation itself overwrites the
        // position and a later read would return wherever the last run left it.
        restPosition = scaleTarget.anchoredPosition;

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void Show(string text = "CLEAR !")
    {
        // A second call landing while the last flourish is still up restarts it rather
        // than layering a second animation onto the same transform.
        if (routine != null) StopCoroutine(routine);

        if (label != null) label.text = text;

        gameObject.SetActive(true);
        routine = StartCoroutine(PlayRoutine());
    }

    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
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
            scaleTarget.localScale = Vector3.one * PopCurve(k);
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

    // Rises past the settled size in the first ~60% of the pop, then eases back onto it.
    private float PopCurve(float k)
    {
        if (k < 0.6f)
        {
            float rise = k / 0.6f;
            return Mathf.Lerp(startScale, punchScale, 1f - (1f - rise) * (1f - rise));
        }

        float settle = (k - 0.6f) / 0.4f;
        return Mathf.Lerp(punchScale, 1f, settle * settle * (3f - 2f * settle));
    }
}
