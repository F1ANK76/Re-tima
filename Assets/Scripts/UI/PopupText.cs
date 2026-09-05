using UnityEngine;

public static class PopupText
{
    private const float OutlineWidthFactor = 0.07f;

    private const float HeightPerCharacterSizeUnit = 8.955f;

    private static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
    };

    public static TextMesh Build(GameObject go, Font font, string text, int fontSize, float characterSize, Color color)
    {
        TextMesh main = Configure(go, font, text, fontSize, characterSize);
        main.color = ToVertexColor(color);
        main.GetComponent<MeshRenderer>().sortingOrder = 1;

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

    private static Color ToVertexColor(Color srgb)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
    }

    private static TextMesh Configure(GameObject go, Font font, string text, int fontSize, float characterSize)
    {
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.GetComponent<MeshRenderer>().material = font.material;
        // fontSize는 텍스처 공간 해상도(반드시 int); characterSize가 실제 화면상의 크기다.
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.text = text;
        return tm;
    }
}
