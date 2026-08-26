using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageBannerView : MonoBehaviour
{
    [SerializeField] private Text bannerText;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float popStartScale = 1.3f;
    // How long the opening card sits empty before the title animates in, so the curtain
    // reads as a deliberate beat rather than a dropped frame.
    [SerializeField] private float openingHoldDuration = 0.4f;
    // OFF falls back to whatever flat color the Image was authored with.
    [SerializeField] private bool useGeneratedBackdrop = true;

    private CanvasGroup canvasGroup;
    private CanvasGroup textGroup;
    private RectTransform textRect;
    private Coroutine routine;
    private Sprite generatedBackdrop;

    // The very first announcement of the session opens behind a fully opaque curtain so the
    // game is never visible before the title lands. Every later one (a new substage, a
    // post-death restart) fades up over gameplay the player is already watching.
    private bool hasShownOnce;

    private void Awake()
    {
        // Added at runtime rather than required on the prefab/scene object - fading the
        // backdrop and the text separately needs a group on each, but nothing about this
        // component depends on them having been wired up by hand beforehand.
        canvasGroup = GetOrAddGroup(gameObject);
        if (bannerText != null)
        {
            textRect = bannerText.rectTransform;
            textGroup = GetOrAddGroup(bannerText.gameObject);
            // Starts empty no matter what text the scene was saved with.
            textGroup.alpha = 0f;
        }

        if (useGeneratedBackdrop) ApplyGeneratedBackdrop();

        // The banner is left active in the scene for editing, but an opaque card that nobody
        // asked for would sit over the menu until Play is pressed. Show() turns it back on,
        // and callers that announce immediately (no title screen) do so from Start - still
        // before the first frame renders, so nothing flashes either way.
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void ApplyGeneratedBackdrop()
    {
        Image image = GetComponent<Image>();
        if (image == null) return;

        generatedBackdrop = TitleCardBackdrop.Create();
        image.sprite = generatedBackdrop;
        // The authored fill was flat black and would otherwise multiply the artwork away.
        image.color = Color.white;

        // White-on-pastel is the tradeoff for a bright card; a soft plum shadow buys the
        // title back its contrast without darkening the sky behind it.
        if (bannerText != null && bannerText.GetComponent<Shadow>() == null)
        {
            Shadow shadow = bannerText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.16f, 0.11f, 0.25f, 0.55f);
            shadow.effectDistance = new Vector2(4f, -4f);
        }
    }

    private void OnDestroy()
    {
        if (generatedBackdrop == null) return;

        // Built at runtime, so nothing else will collect the texture behind it.
        Destroy(generatedBackdrop.texture);
        Destroy(generatedBackdrop);
    }

    public void Show(string text, Action onComplete)
    {
        // A new announcement supersedes a pending one. Without this the old routine's
        // SetActive(false) lands mid-flight and kills the new routine before its callback
        // runs, so the spawn it was holding never happens.
        Cancel();

        if (bannerText != null) bannerText.text = text;

        bool openOnBlack = !hasShownOnce;
        hasShownOnce = true;

        // Set opaque in the same call that activates the object, so no frame ever renders
        // the scene uncovered on the way into the opening.
        canvasGroup.alpha = openOnBlack ? 1f : 0f;
        if (textGroup != null) textGroup.alpha = 0f;
        if (textRect != null) textRect.localScale = Vector3.one * popStartScale;
        gameObject.SetActive(true);
        routine = StartCoroutine(PlayThenHide(openOnBlack, onComplete));
    }

    // Drops a pending announcement without firing its callback - used when a death
    // invalidates whatever spawn was queued behind the banner. Instant, not animated:
    // a death shouldn't wait out a fade before the rest of the restart sequence can run.
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

    private IEnumerator PlayThenHide(bool openOnBlack, Action onComplete)
    {
        // Skip one frame first: the frame that enters play mode (or finishes a scene load)
        // carries a huge deltaTime, and counting it against the banner would cut the
        // on-screen time (and the fades below) short. Realtime waits keep the durations
        // wall-clock accurate.
        yield return null;

        if (openOnBlack)
        {
            // Already fully black from Show(); just let it sit a beat before the title
            // arrives, so the curtain reads as intentional.
            yield return new WaitForSecondsRealtime(openingHoldDuration);
        }
        else
        {
            yield return FadeGroup(canvasGroup, 0f, 1f, fadeDuration);
        }

        yield return PopTextIn();
        yield return new WaitForSecondsRealtime(displayDuration);

        // The text rides the backdrop out rather than fading separately - one curtain lift
        // reveals the stage underneath.
        yield return FadeGroup(canvasGroup, 1f, 0f, fadeDuration);

        routine = null;
        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    // Fades the title up while settling it down from an oversized pop to its resting scale -
    // reads as the announcement landing with a bit of weight instead of flatly appearing.
    private IEnumerator PopTextIn()
    {
        if (textGroup == null && textRect == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            if (textGroup != null) textGroup.alpha = p;
            if (textRect != null)
                textRect.localScale = Vector3.one * Mathf.Lerp(popStartScale, 1f, EaseOutBack(p));
            yield return null;
        }

        if (textGroup != null) textGroup.alpha = 1f;
        if (textRect != null) textRect.localScale = Vector3.one;
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

    private static CanvasGroup GetOrAddGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    private static float EaseOutBack(float x)
    {
        const float overshoot = 1.2f;
        const float c3 = overshoot + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + overshoot * m * m;
    }
}
