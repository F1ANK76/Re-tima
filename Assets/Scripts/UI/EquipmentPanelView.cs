using UnityEngine;
using UnityEngine.UI;

// 장비 상태 표시부. 슬롯(Sword, Shield)당 한 줄: EquipmentPreviewRig로 렌더한 실제 장착
// 애셋(등급 오라 포함), 그 옆에 "Rare Sword ATK + 3" 줄. (숙련도 게이지/레벨 표시는 제거됨 -
// EquipmentDropManager의 숙련도 시스템이 비활성화되어 더 이상 보여줄 값이 없다.)
// 씬 자식으로 미리 두지 않고 최초 활성화 시 코드로 빌드한다 - 패널이 숨겨진 채 시작해서
// Unity가 비활성 GameObject의 Awake를 그때까지 지연시키므로. TitleScreenView와 동일한 방식.
public class EquipmentPanelView : MonoBehaviour
{
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    // EquipmentDropManager가 월드에 드롭하는 것과 동일한 프리팹이다 - 패널은 별도의 아이콘
    // 애셋이 아니라, 정지 상태로 배치된 동일 비주얼의 인스턴스를 보여준다.
    [SerializeField] private EquipmentDropPickup previewPickupPrefab;

    private const int IconSize = 96;
    private const float RowHeight = 118f;
    private const float PanelWidth = 460f;
    private const float LabelHeight = 48f;
    // 행 자체의 상하 여백과 일치시켜(BuildRow 참고), 패널의 네 변 모두 아이콘과 텍스트
    // 주위에 동일한 여유 공간을 유지하도록 한다.
    private const float Padding = 20f;
    private const float IconTextGap = 16f;
    // 아이콘 둘레의 얇은 테두리 - 빈 슬롯도 빈 공간이 아니라 슬롯으로 읽히고, 장착 시엔
    // 라벨 색과 맞춰져 등급을 항상 보여주는 두 번째 표시 수단을 겸한다.
    private const float IconBorderThickness = 2f;
    private static readonly Color EmptySlotBorderColor = new Color(0.55f, 0.55f, 0.6f, 0.65f);
    private static readonly Color IconSlotBackgroundColor = new Color(0.16f, 0.16f, 0.19f, 0.9f);
    // 플레이 영역에서 멀리 떨어진(그리고 서로도 충분히 떨어진) 두 개의 전시용 스테이지 -
    // 각각의 포인트 라이트나 프리뷰 카메라가 원치 않는 다른 것을 비추지 않도록 하기 위함이다.
    private static readonly Vector3 SwordStagePosition = new Vector3(500f, 5f, 0f);
    private static readonly Vector3 ShieldStagePosition = new Vector3(500f, -5f, 0f);
    // 패널 인스턴스마다 그 먼 전시 영역 안에서 자기 구역을 받는다. 관리 창은 우측 상단
    // 패널과 별개로 두 번째 EquipmentPanelView를 띄우는데, 스테이지 위치를 공유하면 서로의
    // 아이템이 상대 프리뷰 카메라에 들어가 엉뚱한 아이템이 찍히거나 둘 다 함께 비친다.
    private const float StageInstanceSpacing = 50f;
    private static int stageInstanceCounter;
    private int stageInstanceIndex;

    private EquipmentPreviewRig swordRig;
    private EquipmentPreviewRig shieldRig;
    private Image swordIconFrame;
    private Image shieldIconFrame;
    private Text swordLabel;
    private Text shieldLabel;

    // 관리 창이 패널을 코드로 빌드할 수 있게 해준다. 오브젝트가 비활성일 때 호출해야 한다 -
    // 활성 GameObject에 컴포넌트를 붙이면 Awake(그리고 이 필드를 읽는 Build)가 즉시 실행된다.
    public void Configure(EquipmentDropManager manager, EquipmentDropPickup previewPrefab)
    {
        equipmentDropManager = manager;
        previewPickupPrefab = previewPrefab;
    }

    private void Awake()
    {
        stageInstanceIndex = stageInstanceCounter++;
        Build();
    }

    private void OnEnable()
    {
        GameEvents.OnEquipmentPickedUp += HandleEquipmentPickedUp;
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.OnEquipmentPickedUp -= HandleEquipmentPickedUp;
    }



    private void OnDestroy()
    {
        if (swordRig != null) Destroy(swordRig.gameObject);
        if (shieldRig != null) Destroy(shieldRig.gameObject);
    }

    private void HandleEquipmentPickedUp(EquipmentType equipType, StatGrade grade)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (equipmentDropManager == null) return;

        // Equip 탭의 전체 콘텐츠로, 장착 등급 + 숙련도로 결정되는 순수 장비 스탯만 보여준다.
        ApplyRow("Sword", equipmentDropManager.EquippedSwordGrade, EquipmentType.Sword,
            equipmentDropManager.SwordEquipmentBonus, "ATK", swordRig, swordIconFrame, swordLabel);
        ApplyRow("Shield", equipmentDropManager.EquippedShieldGrade, EquipmentType.Shield,
            equipmentDropManager.ShieldEquipmentBonus, "HP", shieldRig, shieldIconFrame, shieldLabel);
    }

    private static void ApplyRow(string typeLabel, StatGrade? grade, EquipmentType equipType,
        float bonus, string statLabel, EquipmentPreviewRig rig, Image iconFrame, Text label)
    {
        Color color;
        if (grade.HasValue)
        {
            rig.Show(equipType, grade.Value);
            color = GradeVisuals.GetColor(grade.Value);
            label.text = $"{grade.Value} {typeLabel} {statLabel} + {bonus:0.#}";
        }
        else
        {
            rig.Clear();
            color = Color.white;
            label.text = $"- {typeLabel} {statLabel} + {bonus:0.#}";
        }

        label.color = color;
        iconFrame.color = grade.HasValue ? color : EmptySlotBorderColor;
    }

    private void Build()
    {
        var panelRect = GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(PanelWidth, RowHeight * 2f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RawImage swordIcon;
        RawImage shieldIcon;
        BuildRow("SwordRow", 0, font, out swordIcon, out swordIconFrame, out swordLabel);
        BuildRow("ShieldRow", 1, font, out shieldIcon, out shieldIconFrame, out shieldLabel);


        Vector3 stageOffset = new Vector3(0f, 0f, stageInstanceIndex * StageInstanceSpacing);
        swordRig = BuildRig("SwordPreviewRig", SwordStagePosition + stageOffset);
        shieldRig = BuildRig("ShieldPreviewRig", ShieldStagePosition + stageOffset);
        swordIcon.texture = swordRig.Texture;
        shieldIcon.texture = shieldRig.Texture;
    }

    private void BuildRow(string name, int rowIndex, Font font,
        out RawImage icon, out Image iconFrame, out Text label)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(transform, false);

        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, -rowIndex * RowHeight);
        rowRt.sizeDelta = new Vector2(0f, RowHeight);

        // 프레임 -> 슬롯 배경 -> 아이콘 순서로 추가한다(나중 형제가 위에 그려짐). 덕분에
        // 비어있는 슬롯도 아무것도 없는 게 아니라 얇게 색이 있는 사각형으로 보인다.
        var frameGo = new GameObject("IconFrame", typeof(RectTransform));
        frameGo.transform.SetParent(row.transform, false);
        iconFrame = frameGo.AddComponent<Image>();
        var frameRt = iconFrame.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0f, 0.5f);
        frameRt.anchorMax = new Vector2(0f, 0.5f);
        frameRt.pivot = new Vector2(0f, 0.5f);
        frameRt.anchoredPosition = new Vector2(Padding - IconBorderThickness, 0f);
        frameRt.sizeDelta = new Vector2(IconSize + IconBorderThickness * 2f, IconSize + IconBorderThickness * 2f);

        var slotBgGo = new GameObject("IconSlotBackground", typeof(RectTransform));
        slotBgGo.transform.SetParent(row.transform, false);
        var slotBg = slotBgGo.AddComponent<Image>();
        slotBg.color = IconSlotBackgroundColor;
        var slotBgRt = slotBg.GetComponent<RectTransform>();
        slotBgRt.anchorMin = new Vector2(0f, 0.5f);
        slotBgRt.anchorMax = new Vector2(0f, 0.5f);
        slotBgRt.pivot = new Vector2(0f, 0.5f);
        slotBgRt.anchoredPosition = new Vector2(Padding, 0f);
        slotBgRt.sizeDelta = new Vector2(IconSize, IconSize);

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(row.transform, false);
        icon = iconGo.AddComponent<RawImage>();
        var iconRt = icon.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(Padding, 0f);
        iconRt.sizeDelta = new Vector2(IconSize, IconSize);

        float textX = Padding + IconSize + IconTextGap;
        float textWidth = PanelWidth - textX - Padding;

        // "{grade} {type} {stat} + {bonus}" 한 줄만 표시한다 - 아이콘 오른쪽 가장자리에 좌측
        // 정렬, 행의 세로 중앙에 맞춘다. (숙련도 게이지/레벨 줄은 제거됨.)
        label = CreateLabel(row.transform, font, "Label", textX, textWidth, LabelHeight, 0f, TextAnchor.MiddleLeft, 22);
    }

    private static Text CreateLabel(Transform parent, Font font, string name, float x, float width, float height,
        float y, TextAnchor anchor, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = Color.white;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
        return text;
    }

    private EquipmentPreviewRig BuildRig(string name, Vector3 stagePosition)
    {
        var go = new GameObject(name);
        go.transform.position = stagePosition;

        var rig = go.AddComponent<EquipmentPreviewRig>();
        rig.Initialize(previewPickupPrefab);
        return rig;
    }
}
