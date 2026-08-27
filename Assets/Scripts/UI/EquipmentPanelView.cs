using UnityEngine;
using UnityEngine.UI;

// 장비 상태 표시부. 슬롯(Sword, Shield)당 한 줄: EquipmentPreviewRig로 렌더한 실제 장착
// 애셋(등급 오라 포함), 그 옆 위쪽에 "Rare Sword ATK + 3" 줄, 아래에 "Lv.2" + 숙련도 게이지
// (EquipmentMasteryGaugeView, 독립 HUD 바가 아닌 임베드 형태 - 다음 레벨까지의 진행도).
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
    private const float GaugeHeight = 24f;
    private const float LevelTextWidth = 56f;
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
    // 두 숙련도 게이지 모두 동일한 하늘색으로 채워진다 - 슬롯마다 색을 달리해봐야 의미
    // 있게 읽히지 않고, 그저 서로 일관성 없어 보이기만 했다.
    private static readonly Color SwordGaugeColor = new Color(0.4f, 0.75f, 1f);
    private static readonly Color ShieldGaugeColor = new Color(0.4f, 0.75f, 1f);

    private EquipmentPreviewRig swordRig;
    private EquipmentPreviewRig shieldRig;
    private Image swordIconFrame;
    private Image shieldIconFrame;
    private Text swordLabel;
    private Text shieldLabel;
    private Text swordLevelText;
    private Text shieldLevelText;

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

        // SwordAtkBonus/ShieldHpBonus가 아니라 SwordEquipmentBonus/ShieldEquipmentBonus - 이
        // 패널은 Equip 탭의 전체 콘텐츠로 순수 장비 스탯만 보여준다. 플레이어 실제 총합에도
        // 반영되는 스톤 옵션은 Stone 탭 몫(ManagementWindowView.OptionLine 참고).
        ApplyRow("Sword", equipmentDropManager.EquippedSwordGrade, EquipmentType.Sword,
            equipmentDropManager.SwordLevel, equipmentDropManager.SwordEquipmentBonus, "ATK", swordRig, swordIconFrame, swordLabel, swordLevelText);
        ApplyRow("Shield", equipmentDropManager.EquippedShieldGrade, EquipmentType.Shield,
            equipmentDropManager.ShieldLevel, equipmentDropManager.ShieldEquipmentBonus, "HP", shieldRig, shieldIconFrame, shieldLabel, shieldLevelText);
    }

    private static void ApplyRow(string typeLabel, StatGrade? grade, EquipmentType equipType,
        int level, float bonus, string statLabel, EquipmentPreviewRig rig, Image iconFrame, Text label, Text levelText)
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
        // 등급과 무관하게 항상 흰색 - 숙련도 레벨은 등급과 독립적으로 진행되고(해당 타입을
        // 하나도 장착하기 전에도 표시되며 이미 0보다 클 수 있다) 그래서 등급 색을 안 빌린다.
        levelText.color = Color.white;
        levelText.text = $"Lv.{level}";
    }

    private void Build()
    {
        var panelRect = GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(PanelWidth, RowHeight * 2f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RawImage swordIcon;
        RawImage shieldIcon;
        BuildRow("SwordRow", 0, font, EquipmentType.Sword, SwordGaugeColor, out swordIcon, out swordIconFrame, out swordLabel, out swordLevelText);
        BuildRow("ShieldRow", 1, font, EquipmentType.Shield, ShieldGaugeColor, out shieldIcon, out shieldIconFrame, out shieldLabel, out shieldLevelText);


        Vector3 stageOffset = new Vector3(0f, 0f, stageInstanceIndex * StageInstanceSpacing);
        swordRig = BuildRig("SwordPreviewRig", SwordStagePosition + stageOffset);
        shieldRig = BuildRig("ShieldPreviewRig", ShieldStagePosition + stageOffset);
        swordIcon.texture = swordRig.Texture;
        shieldIcon.texture = shieldRig.Texture;
    }

    private void BuildRow(string name, int rowIndex, Font font, EquipmentType equipType, Color gaugeColor,
        out RawImage icon, out Image iconFrame, out Text label, out Text levelText)
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
        float topY = (LabelHeight + GaugeHeight) * 0.5f - LabelHeight * 0.5f + 3f;
        float bottomY = -(LabelHeight + GaugeHeight) * 0.5f + GaugeHeight * 0.5f - 3f;

        // 위쪽 줄은 "{grade} {type} {stat} + {bonus}"로 전체 너비, 아래쪽 줄은 게이지 옆 "Lv.N".
        // 둘 다 아이콘 오른쪽 가장자리에 좌측 정렬, 두 줄 합친 중점이 행의 세로 중앙에 맞는다.
        label = CreateLabel(row.transform, font, "Label", textX, textWidth, LabelHeight, topY, TextAnchor.MiddleLeft, 22);
        levelText = CreateLabel(row.transform, font, "LevelText", textX, LevelTextWidth, GaugeHeight, bottomY, TextAnchor.MiddleLeft, 18);

        float gaugeX = textX + LevelTextWidth + 6f;
        float gaugeWidth = textWidth - LevelTextWidth - 6f;
        BuildGauge(row.transform, equipType, gaugeColor, gaugeX, gaugeWidth, bottomY);
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

    private void BuildGauge(Transform parent, EquipmentType equipType, Color gaugeColor, float x, float width, float y)
    {
        var go = new GameObject("MasteryGauge", typeof(RectTransform));
        // 아래 Configure까지 비활성으로 둔다: EquipmentMasteryGaugeView.Awake는 레이아웃을
        // 정할 때 Configure가 채우는 같은 필드를 읽는데, 비활성 GameObject에서는 Awake가
        // 지연되므로 AddComponent 시점과 Configure 시점이 경합하지 않는다.
        go.SetActive(false);
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, GaugeHeight);

        var gauge = go.AddComponent<EquipmentMasteryGaugeView>();
        gauge.Configure(equipmentDropManager, equipType, gaugeColor, anchorToBottomCenter: false, includeTypeAndLevelInLabel: false);

        go.SetActive(true);
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
