using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The main menu: the game's name and a Play button over the live scene, so the hero is
// already running through the forest before the player presses anything - the menu shows the
// actual game rather than a picture of it. Built in code so it owns its own chrome, with
// nothing to wire up by hand and nothing that can drift out of sync with the palette.
public class TitleScreenView : MonoBehaviour
{
    [SerializeField] private string titleText = "Re:tima";
    [SerializeField] private float fadeOutDuration = 0.45f;
    // OFF swaps the live scene for the painted title illustration.
    [SerializeField] private bool showLiveScene = true;
    // The gameplay HUD, which has nothing to say until the run starts. Hidden by the menu
    // rather than by each element knowing about it, and restored to exactly the state it was
    // found in - some of these (the stat panel) start closed and must stay that way.
    [SerializeField] private GameObject[] hideWhileShowing;
    // The hero, who stands off-centre in combat to leave the right half for the monster. On
    // the menu there is no monster, so he is slid to the middle of frame and put back before
    // the run starts - behind the stage card, which is opaque by then.
    [SerializeField] private Transform centerWhileShowing;

    private CanvasGroup canvasGroup;
    private Button playButton;
    private Sprite backdropSprite;
    private Sprite buttonSprite;
    private Sprite skySplitSprite;
    private Action onPlay;
    private bool[] hiddenPriorState;
    private bool playPressed;
    private Vector3 centeredRestorePosition;
    private bool hasCentered;

    private void Awake()
    {
        // Drawn last means drawn on top: the menu has to cover the gameplay HUD sitting
        // earlier in the Canvas, without each of those objects needing to know about it.
        transform.SetAsLastSibling();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

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

        centeredRestorePosition = centerWhileShowing.position;
        hasCentered = true;

        // Matching the camera's x puts him on the centre line, whatever the scene's combat
        // spacing happens to be.
        Vector3 p = centeredRestorePosition;
        p.x = cam.transform.position.x;
        centerWhileShowing.position = p;
    }

    private void RestoreSubject()
    {
        if (!hasCentered || centerWhileShowing == null) return;

        centerWhileShowing.position = centeredRestorePosition;
        hasCentered = false;
    }

    private void HandlePlayPressed()
    {
        // Clearing interactable below stops a second click, but not a second call - and the
        // run must not be started twice, nor a fade coroutine spawned on an object this has
        // already deactivated.
        if (playPressed) return;
        playPressed = true;

        HideAndFadeOut();

        // Fired before the fade rather than after: the stage card comes up opaque underneath
        // this screen, so what the player sees is the menu handing off to "Stage 1-1" rather
        // than a bare frame of gameplay flashing in between.
        onPlay?.Invoke();
        onPlay = null;
    }

    // Same hide as pressing Play, minus the handoff callback - for entry points that already
    // drive their own spawn flow (the debug stage-jump panel) and only need the title chrome
    // out of the way, not a second copy of whatever Play would have kicked off.
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

        // Restored before the run starts, so StageManager's own show/hide of the repeat
        // toggle gets the last word rather than being undone a frame later. The hero goes
        // back to his combat mark in the same beat - the stage card below turns opaque in
        // this same frame, so the move is never on screen.
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
        var rect = GetComponent<RectTransform>();
        Stretch(rect);

        // Over the live scene this is only a scrim - dark at the very top and bottom, clear
        // through the middle - so the title and the button stay readable without hiding the
        // gameplay they are sitting on.
        backdropSprite = showLiveScene ? CreateEdgeScrim() : TitleCardBackdrop.CreateTitleScene();
        Image backdrop = GetComponent<Image>();
        if (backdrop == null) backdrop = gameObject.AddComponent<Image>();
        backdrop.sprite = backdropSprite;
        backdrop.color = Color.white;
        // The menu must swallow clicks so nothing behind it reacts while it is up. This holds
        // even where the scrim is fully transparent, which is why it stays a raycast target.
        backdrop.raycastTarget = true;

        // Only over the live scene: the painted TitleCardBackdrop already draws its own single
        // sky and doesn't need a second one layered on top of it. Added before the title/button
        // below so it lands behind them in sibling order.
        if (showLiveScene) BuildDayNightSkySplit();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildTitle(font);
        BuildPlayButton(font);
    }

    // A day sky on the left fading into a night sky on the right, pinned above the live
    // scene's own treeline - a preview of the two stages (1 = day, 2 = night) before the
    // player has picked either. The real skybox has no notion of "half day, half night" (its
    // blend is a single scene-wide slider - see StageMoodController), so this fakes it with a
    // painted band that fades to fully transparent by the time it reaches the trees, leaving
    // the actual live ground/character underneath completely untouched.
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
        // Tall enough that the opaque part of the band (see vanishFrac below) covers the
        // whole open-sky area above the treeline (~330px at this canvas's 1920x1080
        // reference resolution) before it starts fading - the split should read as the sky
        // itself being divided, not a strip along the very top of the screen.
        rt.sizeDelta = new Vector2(0f, 460f);
    }

    // Same palette StageMoodController actually cross-fades to at runtime (its sun/fog
    // colors), so the title's painted split reads as the same two moods the game itself
    // shows, not a third, unrelated color scheme invented just for this screen.
    private static readonly Color DaySkyTop = new Color(0.35f, 0.55f, 0.92f);
    private static readonly Color DaySkyHorizon = new Color(0.85f, 0.88f, 0.93f);
    private static readonly Color NightSkyTop = new Color(0.08f, 0.09f, 0.24f);
    private static readonly Color NightSkyHorizon = new Color(0.36f, 0.25f, 0.42f);

    private static Sprite CreateDayNightSkySplitSprite()
    {
        const int width = 480, height = 230;
        // Centered seam. Narrow on purpose - this should read as the sky cut cleanly in
        // half, not a wide gradient where neither side looks like a distinct sky. Still
        // softened over a few pixels rather than a single hard column, so a stretched
        // display doesn't alias it into a jagged line.
        const float seamHalfWidth = 0.025f;
        // Below this height fraction the band is fully transparent. Kept small (as opposed
        // to fading over most of the band) so the two skies stay solid and clearly split
        // through virtually the whole open-sky area, with only a short hand-off right at
        // the very bottom of the band - which BuildDayNightSkySplit sizes to land at the
        // treeline - blending into the live scene instead of ending on a hard edge.
        const float vanishFrac = 0.12f;

        var rng = new System.Random(20260824);
        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float ty = (float)y / (height - 1);
            // Same easing TitleCardBackdrop's own sky gradient uses, so the two painted skies
            // in this game share one gradient shape.
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
        // Matched to the sprite so the artwork isn't stretched out of proportion.
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

        // Centred on the raised face rather than the whole sprite, so the side wall and the
        // drop shadow don't drag the caption low.
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

    // Only 4px wide: nothing varies horizontally, and the Image stretches it across the
    // screen regardless.
    private static Sprite CreateEdgeScrim()
    {
        const int width = 4, height = 256;
        var tint = new Color(0.098f, 0.071f, 0.157f);
        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);   // 0 at the bottom, 1 at the top
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

    // Layout of the generated button sprite, in texture pixels from the bottom edge.
    private const int ButtonWidth = 340;
    private const int ButtonHeight = 118;
    private const float ButtonFaceTop = 114f;
    private const float ButtonFaceBottom = 24f;
    private const float ButtonLipBottom = 10f;
    private const float ButtonRadius = 26f;

    // A key with actual thickness: a lit face on top, a dark side wall showing beneath it,
    // and a contact shadow under that. Unity's default UI sprite is a flat hard rectangle,
    // which reads as unstyled next to the scene behind it.
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

                // Cast onto the ground the button sits on, so it lifts off the backdrop.
                float shadowDist = RoundedBoxDistance(px, py, 14f, 2f, ButtonWidth - 14f, 20f, 14f);
                float shadowAlpha = Mathf.Clamp01(-shadowDist / 9f) * 0.30f;
                if (shadowAlpha > 0f) result = new Color(shadow.r, shadow.g, shadow.b, shadowAlpha);

                // The side wall, drawn full height - only the strip below the face shows.
                float lipCoverage = Coverage(RoundedBoxDistance(px, py, 0f, ButtonLipBottom,
                    ButtonWidth, ButtonFaceTop, ButtonRadius));
                if (lipCoverage > 0f) result = Over(new Color(lip.r, lip.g, lip.b, lipCoverage), result);

                float faceCoverage = Coverage(RoundedBoxDistance(px, py, 0f, ButtonFaceBottom,
                    ButtonWidth, ButtonFaceTop, ButtonRadius));
                if (faceCoverage > 0f)
                {
                    float t = Mathf.InverseLerp(ButtonFaceBottom, ButtonFaceTop, py);
                    Color face = Color.Lerp(faceBottom, faceTop, t);

                    // A bright rim along the top edge reads as the light source above.
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

    // Signed distance to a rounded box: negative inside, positive outside.
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

    // Built at runtime, so nothing else will collect the textures behind them.
    private static void DestroyGenerated(Sprite sprite)
    {
        if (sprite == null) return;
        Destroy(sprite.texture);
        Destroy(sprite);
    }
}
