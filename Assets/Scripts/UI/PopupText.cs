using UnityEngine;

// Builds a floating drop/pickup label: one bold TextMesh with a black outline faked from
// eight offset copies drawn behind it.
//
// The outline is the whole point of this class. These popups float over live gameplay - a
// bright sky in stage 1, dark ground and heavy bloom in stage 2, hit VFX flashing
// underneath - so no single fill color stays legible against all of it, and darkening the
// grade colors until they survive a bright background just makes the five grades converge
// on mud. A black stroke supplies the contrast independently of whatever is behind it,
// which frees the fill to stay as saturated as the grade ramp wants.
//
// Eight copies rather than a shader: TextMesh's built-in font material has no outline
// support, and the alternative - a TextMeshPro SDF font asset - would mean authoring a font
// asset for a project that has none and no other TMP usage. These popups spawn a couple at
// a time and live about a second, so nine small quads each is not worth a second text
// pipeline.
public static class PopupText
{
    // Outline offset as a fraction of the glyph's actual height. Thick enough to still read
    // while the popup is scaled down at spawn (see StatDropPopupMotion's pop-in), thin enough
    // not to close up the holes in the glyphs.
    private const float OutlineWidthFactor = 0.07f;

    // Fallback only, for the case where the generated mesh isn't measurable yet: a TextMesh's
    // height in local units is characterSize * fontSize / this, NOT characterSize (measured
    // across both values - the ratio is exactly proportional to fontSize).
    private const float HeightPerCharacterSizeUnit = 8.955f;

    private static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
    };

    // Adds the label (and its outline children) to an already-positioned GameObject, so the
    // caller keeps control of parenting/placement. Returns the main TextMesh.
    public static TextMesh Build(GameObject go, Font font, string text, int fontSize, float characterSize, Color color)
    {
        TextMesh main = Configure(go, font, text, fontSize, characterSize);
        main.color = ToVertexColor(color);
        // The built-in font shader is ZTest Always / ZWrite Off, so depth cannot order the
        // fill against its own outline - sortingOrder is what actually decides, and the fill
        // has to land on top.
        main.GetComponent<MeshRenderer>().sortingOrder = 1;

        // Measured off the generated mesh rather than derived from characterSize. A TextMesh's
        // rendered height is characterSize * fontSize / ~9, so scaling the stroke by
        // characterSize alone made it about 2% of the glyph - a stroke that thin simply does
        // not survive being drawn, which is why the first pass at this had no visible outline.
        // localBounds (not bounds) so parent scale/billboard rotation can't skew it.
        float glyphHeight = main.GetComponent<Renderer>().localBounds.size.y;
        if (glyphHeight <= 0.0001f) glyphHeight = characterSize * fontSize / HeightPerCharacterSizeUnit;
        float width = glyphHeight * OutlineWidthFactor;

        foreach (Vector2 dir in OutlineOffsets)
        {
            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(go.transform, false);
            outlineGo.transform.localPosition = new Vector3(dir.x * width, dir.y * width, 0f);

            TextMesh outline = Configure(outlineGo, font, text, fontSize, characterSize);
            outline.color = Color.black;
            outline.GetComponent<MeshRenderer>().sortingOrder = 0;
        }

        return main;
    }

    // TextMesh carries its color as MESH VERTEX COLORS, and Unity only auto-converts sRGB->
    // linear for material color properties - never for vertex colors. So in a linear-space
    // project the authored value is taken as though it were already linear and gamma-encoded
    // on the way out, which lightens it sharply: Legendary's (0.20, 0.90, 0.15) displayed as
    // (0.48, 0.95, 0.42), a pale mint. That single conversion is what made every grade look
    // washed out no matter how saturated the authored color was - the whole "the text is too
    // faint" problem, not the fill color choices.
    //
    // Pre-warping to linear here cancels that encode, so the grade colors land on screen as
    // the values actually written in GradeVisuals.
    private static Color ToVertexColor(Color srgb)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
    }

    private static TextMesh Configure(GameObject go, Font font, string text, int fontSize, float characterSize)
    {
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.GetComponent<MeshRenderer>().material = font.material;
        // fontSize is texture-space resolution (must stay an int); characterSize is the
        // actual on-screen scale.
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.text = text;
        return tm;
    }
}
