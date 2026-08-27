using UnityEngine;

// 떠다니는 드롭/획득 라벨: 굵은 TextMesh 하나에, 뒤에 8방향으로 살짝 어긋나게 그린 사본들로
// 검은 외곽선을 흉내낸다.
//
// 이 외곽선이 핵심이다. 팝업은 1스테이지의 밝은 하늘, 2스테이지의 어두운 지면과 강한 블룸,
// 번쩍이는 피격 VFX 위에 뜨는데 - 무엇과 겹쳐도 보이는 단일 채우기 색은 없고, 밝은 배경을
// 버티려 등급 색을 어둡게 하면 다섯 등급이 다 흙탕물이 된다. 검은 스트로크는 뒤와 무관하게
// 대비를 만들어주니, 채우기는 등급 색상 램프의 채도를 그대로 쓸 수 있다.
//
// 셰이더 대신 8개 사본인 이유: TextMesh의 내장 폰트 머티리얼은 외곽선 미지원이고, 대안인
// TextMeshPro SDF는 TMP를 전혀 안 쓰는 이 프로젝트에 폰트 애셋을 새로 제작해야 한다. 한 번에
// 몇 개씩 뜨고 수명도 1초인 팝업에 개당 9개 쿼드면 별도 텍스트 파이프라인을 둘 가치가 없다.
public static class PopupText
{
    // 외곽선 오프셋을 글리프 실제 높이에 대한 비율로. 스폰 시 축소된 상태에서도
    // (StatDropPopupMotion의 팝인) 읽힐 만큼 두껍되, 글리프의 구멍은 막지 않을 만큼 얇다.
    private const float OutlineWidthFactor = 0.07f;

    // 메시를 아직 측정할 수 없을 때만 쓰는 대체값: 로컬 단위에서 TextMesh의 높이는
    // characterSize가 아니라 characterSize * fontSize / 이 값(둘 다 바꿔 측정, fontSize 정비례).
    private const float HeightPerCharacterSizeUnit = 8.955f;

    private static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
    };

    // 이미 위치가 정해진 GameObject에 라벨(과 외곽선 자식들)을 추가하므로 부모 설정/배치는
    // 호출자가 계속 제어한다. 메인 TextMesh를 반환.
    public static TextMesh Build(GameObject go, Font font, string text, int fontSize, float characterSize, Color color)
    {
        TextMesh main = Configure(go, font, text, fontSize, characterSize);
        main.color = ToVertexColor(color);
        // 내장 폰트 셰이더는 ZTest Always / ZWrite Off라 깊이값으로는 채우기와 외곽선의
        // 순서를 정할 수 없다 - sortingOrder가 결정하며, 채우기가 반드시 위에 와야 한다.
        main.GetComponent<MeshRenderer>().sortingOrder = 1;

        // characterSize에서 유도하지 않고 생성된 메시에서 직접 측정한다. 렌더링 높이가
        // characterSize * fontSize / ~9라서 characterSize만으로 스트로크를 정하면 글리프의 약
        // 2%로 얇아 보이지도 않았다 - 첫 구현에서 외곽선이 전혀 안 보였던 원인이다. bounds가
        // 아니라 localBounds인 건 부모 스케일/빌보드 회전이 값을 왜곡하지 못하게 하려고.
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

    // TextMesh는 색을 MESH VERTEX COLOR로 들고 있는데, Unity는 머티리얼 컬러 프로퍼티만
    // sRGB->linear로 변환하고 버텍스 컬러는 절대 변환하지 않는다. 리니어 색공간 프로젝트에선
    // 작성값이 이미 리니어인 양 취급돼 출력 시 감마 인코딩이 걸려 확 밝아진다: Legendary의
    // (0.20, 0.90, 0.15)가 화면엔 옅은 민트색 (0.48, 0.95, 0.42)로 나온다. 채도 높게 작성한
    // 모든 등급이 흐릿했던 건 채우기 색 선택이 아니라 이 변환 하나 때문 - "텍스트가 너무
    // 연하다" 문제 전체의 원인이었다. 미리 linear로 워핑해 인코딩을 상쇄하면, 화면의 등급
    // 색이 GradeVisuals에 작성된 값 그대로 나온다.
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
