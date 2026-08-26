using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Plays over the player's death: a full-screen dim behind a punched-in "FAIL !" label,
// held briefly, then faded - both driven by one CanvasGroup so the darken and the text
// appear/disappear together. Unlike ClearBannerView this isn't fire-and-forget: StageManager
// yields on Play() so the checkpoint rewind/respawn only happens once the flourish is done.
[RequireComponent(typeof(CanvasGroup))]
public class FailBannerView : MonoBehaviour
{
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private Text label;

    [SerializeField] private float popInDuration = 0.25f;
    [SerializeField] private float holdDuration = 1f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    // Overshoot the settled size on the way in, then ease back down to it - a straight
    // 0->1 scale reads as the text simply appearing, with no sense of impact.
    [SerializeField] private float punchScale = 1.15f;
    [SerializeField] private float startScale = 0.5f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (scaleTarget == null) scaleTarget = (RectTransform)transform;

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    // Runs on unscaled time so it plays out the same regardless of what Time.timeScale is
    // doing around the death (nothing else is expected to be running at this point anyway).
    public IEnumerator Play(string text = "FAIL !")
    {
        if (label != null) label.text = text;

        scaleTarget.localScale = Vector3.one * startScale;
        gameObject.SetActive(true);

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popInDuration);

            canvasGroup.alpha = k;
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
            yield return null;
        }

        canvasGroup.alpha = 0f;
        scaleTarget.localScale = Vector3.one;
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
