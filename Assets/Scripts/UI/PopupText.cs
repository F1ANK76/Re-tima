using TMPro;
using UnityEngine;

// 아웃라인은 셰이더가 그린다 -> 텍스트 하나로 렌더러 1개
public static class PopupText
{
    private const float OutlineWidth = 0.2f;

    // TextMesh의 fontSize×characterSize를 같은 크기의 TMP fontSize로 옮기는 계수
    public const float FontSizeScale = 0.93f;

    public static TMP_Text Build(GameObject go, string text, int fontSize, float characterSize, Color color)
    {
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSharedMaterial = OutlineMaterial;
        tmp.fontSize = fontSize * characterSize * FontSizeScale;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = color;
        tmp.text = text;
        return tmp;
    }

    // 팝업마다 머티리얼을 새로 만들지 않도록 하나만 만들어 공유한다
    private static Material outlineMaterial;
    private static Material OutlineMaterial
    {
        get
        {
            if (outlineMaterial != null) return outlineMaterial;

            outlineMaterial = new Material(TMP_Settings.defaultFontAsset.material);
            outlineMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            return outlineMaterial;
        }
    }
}
