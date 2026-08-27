using UnityEngine;

// 떠다니는 드롭/획득 라벨을 만든다: 굵은 TextMesh 하나에, 그 뒤에 8방향으로 살짝 어긋나게
// 그린 사본들로 검은 외곽선을 흉내낸다.
//
// 이 클래스의 핵심은 바로 이 외곽선이다. 이 팝업들은 실제 게임플레이 위에 떠 있는데 -
// 1스테이지의 밝은 하늘, 2스테이지의 어두운 지면과 강한 블룸, 그 아래에서 번쩍이는 피격
// VFX - 그 어떤 것과 겹쳐도 항상 잘 보이는 단일 채우기 색은 존재하지 않고, 밝은 배경에서도
// 버티도록 등급 색을 어둡게 만들면 다섯 등급이 결국 다 흙탕물처럼 비슷해져 버린다. 검은
// 스트로크는 뒤에 무엇이 있든 그와 무관하게 대비를 만들어주므로, 채우기 색은 등급 색상
// 램프가 원하는 만큼 채도를 그대로 유지할 수 있다.
//
// 셰이더 대신 8개의 사본을 쓰는 이유: TextMesh의 내장 폰트 머티리얼은 외곽선을 지원하지
// 않고, 대안인 TextMeshPro SDF 폰트 애셋을 쓰려면 TMP를 전혀 쓰지 않는 이 프로젝트에
// 폰트 애셋을 새로 제작해야 한다. 이 팝업들은 한 번에 몇 개씩만 스폰되고 수명도 1초 정도라,
// 개당 9개의 작은 쿼드 정도는 별도의 텍스트 파이프라인을 둘 만한 가치가 없다.
public static class PopupText
{
    // 외곽선 오프셋을 글리프의 실제 높이에 대한 비율로 표현한 값. 스폰 시 팝업이 축소된
    // 상태에서도(StatDropPopupMotion의 팝인 참고) 여전히 읽히도록 충분히 두껍되, 글리프의
    // 구멍(속이 빈 부분)을 막아버리지 않을 만큼 얇다.
    private const float OutlineWidthFactor = 0.07f;

    // 생성된 메시를 아직 측정할 수 없는 경우에만 쓰는 대체값: 로컬 단위에서 TextMesh의
    // 높이는 characterSize 자체가 아니라 characterSize * fontSize / 이 값이다
    // (두 값을 모두 바꿔가며 측정한 결과 - 이 비율은 fontSize에 정확히 비례한다).
    private const float HeightPerCharacterSizeUnit = 8.955f;

    private static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
    };

    // 이미 위치가 정해진 GameObject에 라벨(과 그 외곽선 자식들)을 추가하므로, 부모 설정/배치는
    // 호출자가 계속 제어한다. 메인 TextMesh를 반환한다.
    public static TextMesh Build(GameObject go, Font font, string text, int fontSize, float characterSize, Color color)
    {
        TextMesh main = Configure(go, font, text, fontSize, characterSize);
        main.color = ToVertexColor(color);
        // 내장 폰트 셰이더는 ZTest Always / ZWrite Off이므로 깊이값으로는 채우기와 외곽선의
        // 그리기 순서를 정할 수 없다 - 실제로 순서를 결정하는 건 sortingOrder이고, 채우기가
        // 반드시 위에 와야 한다.
        main.GetComponent<MeshRenderer>().sortingOrder = 1;

        // characterSize에서 유도하지 않고 생성된 메시에서 직접 측정한다. TextMesh의 렌더링
        // 높이는 characterSize * fontSize / ~9이므로, characterSize만으로 스트로크 크기를
        // 정하면 글리프의 약 2%밖에 안 됐다 - 그 정도로 얇은 스트로크는 그려봐야 보이지도
        // 않는데, 이 때문에 처음 구현했을 때 외곽선이 전혀 보이지 않았던 것이다.
        // bounds가 아니라 localBounds를 쓰는 이유는 부모의 스케일이나 빌보드 회전이
        // 값을 왜곡시키지 못하게 하기 위해서다.
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

    // TextMesh는 색을 MESH VERTEX COLORS로 가지고 있는데, Unity는 머티리얼 컬러 프로퍼티에
    // 대해서만 sRGB->linear 자동 변환을 해주고 버텍스 컬러는 절대 변환해주지 않는다. 그래서
    // 리니어 색공간 프로젝트에서는 작성한 값이 마치 이미 리니어인 것처럼 취급되어 출력 시
    // 감마 인코딩이 걸려버리고, 그 결과 색이 확 밝아진다: Legendary의 (0.20, 0.90, 0.15)가
    // 화면에는 옅은 민트색인 (0.48, 0.95, 0.42)로 표시된다. 작성한 색이 아무리 채도가
    // 높아도 모든 등급이 흐릿하게 보였던 건 바로 이 변환 하나 때문이었다 - "텍스트가 너무
    // 연하다"는 문제 전체의 원인이지, 채우기 색 선택 자체의 문제가 아니었다.
    //
    // 여기서 미리 linear로 워핑해두면 그 인코딩이 상쇄되어, 화면에 나타나는 등급 색이
    // GradeVisuals에 실제로 작성된 값 그대로 표시된다.
    private static Color ToVertexColor(Color srgb)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
    }

    private static TextMesh Configure(GameObject go, Font font, string text, int fontSize, float characterSize)
    {
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.GetComponent<MeshRenderer>().material = font.material;
        // fontSize는 텍스처 공간 해상도다(반드시 int여야 한다); characterSize가 실제
        // 화면상의 크기다.
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.text = text;
        return tm;
    }
}
