using UnityEngine;
using UnityEngine.UI;

// 장비 상태 표시부: 슬롯(Sword, Shield)마다 한 줄씩, EquipmentPreviewRig를 통해 실제 장착된
// 3D 애셋을(등급 오라 포함) 보여주고, 그 옆 위쪽에 "Rare Sword ATK + 3" 같은 줄, 바로 그
// 아래에 다음 레벨까지의 진행도를 보여주는 얇은 숙련도 게이지(EquipmentMasteryGaugeView,
// 독립 HUD 바가 아니라 여기에 임베드된 형태) 옆에 "Lv.2"를 표시한다. 미리 배치된 씬 자식
// 오브젝트가 아니라 최초 활성화 시점에 코드로 빌드한다(패널은 처음엔 숨겨져 있고, Unity는
// 비활성 GameObject의 Awake를 그 시점까지 지연시킨다) - 이는 이 UI의 다른 곳에서
// TitleScreenView가 쓰는 방식과 동일하다.
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
    // 각 아이콘 둘레에 얇은 테두리를 둬서, 빈 슬롯도 그냥 빈 공간이 아니라 슬롯으로 보이게
    // 한다 - 그리고 무언가 장착되면 이 테두리 색이 라벨 색과 맞춰지면서 등급을 항상 보여주는
    // 두 번째 표시 수단으로도 겸한다.
    private const float IconBorderThickness = 2f;
    private static readonly Color EmptySlotBorderColor = new Color(0.55f, 0.55f, 0.6f, 0.65f);
    private static readonly Color IconSlotBackgroundColor = new Color(0.16f, 0.16f, 0.19f, 0.9f);
    // 플레이 영역에서 멀리 떨어진(그리고 서로도 충분히 떨어진) 두 개의 전시용 스테이지 -
    // 각각의 포인트 라이트나 프리뷰 카메라가 원치 않는 다른 것을 비추지 않도록 하기 위함이다.
    private static readonly Vector3 SwordStagePosition = new Vector3(500f, 5f, 0f);
    private static readonly Vector3 ShieldStagePosition = new Vector3(500f, -5f, 0f);
    // 모든 패널 인스턴스는 그 먼 전시 영역에서 자기만의 구역을 할당받는다. 관리 창은
    // 우측 상단 패널과는 별개로 두 번째 EquipmentPanelView를 함께 띄우는데, 두 패널이
    // 스테이지 위치를 공유하면 서로의 아이템이 상대방의 프리뷰 카메라 안에 들어가 버려서
    // 각 아이콘이 엉뚱한 아이템을 렌더링하거나 둘 다 한꺼번에 비치게 된다.
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

    // 관리 창이 코드로 패널을 빌드할 수 있게 해준다. 오브젝트가 아직 비활성 상태일 때
    // 호출해야 한다 - 컴포넌트가 이미 활성화된 GameObject에 추가되는 순간 Awake(그리고
    // 이 필드들을 읽는 Build)가 즉시 실행되기 때문이다.
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

        // SwordAtkBonus/ShieldHpBonus가 아니라 SwordEquipmentBonus/ShieldEquipmentBonus를
        // 쓴다 - 이 패널은 Equip 탭의 전체 콘텐츠이며, 순수한 장비 스탯만 보여준다. 플레이어의
        // 실제 총합에도 반영되는 스톤 옵션은 Stone 탭이 표시할 몫이다
        // (ManagementWindowView.OptionLine 참고).
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
        // 등급과 무관하게 항상 흰색으로 둔다 - 숙련도 레벨은 등급과 독립적으로 진행되며
        // (해당 타입의 아이템이 하나도 장착되기 전이라도 표시되고 이미 0보다 클 수 있다),
        // 그래서 일부러 등급 색을 빌려오지 않는다.
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

        // 프레임, 그다음 채워진 슬롯 배경, 그리고 그 위에 아이콘 - 이 순서로 그린다
        // (나중에 추가된 형제일수록 위에 그려진다) 그래서 장착되지 않은 슬롯도 아무것도
        // 없는 게 아니라 얇게 색이 있는 사각형으로 보인다.
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

        // 위쪽 줄: "{grade} {type} {stat} + {bonus}", 전체 너비. 아래쪽 줄: 게이지 옆에
        // "Lv.N" - 둘 다 아이콘의 오른쪽 가장자리에 왼쪽 정렬되며, 두 줄을 합친 중점이
        // 행의 세로 중앙과 일치한다.
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
        // 아래 Configure가 실행될 때까지 비활성 상태로 둔다: EquipmentMasteryGaugeView.Awake는
        // (이 레이아웃을 결정할 때 같은 필드들을 읽는데) 비활성 GameObject에서는 지연되므로,
        // 여기서 컴포넌트를 추가하는 시점이 Configure가 의존 필드를 설정하는 시점과
        // 경합할 일이 없다.
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
