using UnityEngine;
using UnityEngine.UI;

// Equip-status readout: one row per slot (Sword, Shield), each showing the actual equipped 3D
// asset - grade aura included, via EquipmentPreviewRig - next to a "Rare Sword ATK + 3" line
// on top and, right under that, "Lv.2" beside a slim mastery gauge (EquipmentMasteryGaugeView,
// embedded rather than a standalone HUD bar) showing progress toward the next level. Built in
// code at first activation (the panel starts hidden; Unity defers Awake on inactive
// GameObjects until then) rather than from hand-placed scene children, matching
// TitleScreenView's approach elsewhere in this UI.
public class EquipmentPanelView : MonoBehaviour
{
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    // Same prefab EquipmentDropManager drops into the world - the panel shows a stationary
    // instance of the exact same visual, not a separate icon asset.
    [SerializeField] private EquipmentDropPickup previewPickupPrefab;

    private const int IconSize = 96;
    private const float RowHeight = 118f;
    private const float PanelWidth = 460f;
    private const float LabelHeight = 48f;
    private const float GaugeHeight = 24f;
    private const float LevelTextWidth = 56f;
    // Matches the row's own top/bottom margin (see BuildRow) so all four edges of the
    // panel keep the same breathing room around the icon and text.
    private const float Padding = 20f;
    private const float IconTextGap = 16f;
    // A thin frame around each icon so an empty slot still reads as a slot rather than a
    // blank gap - and once something's equipped, its color doubles as a second, always-
    // visible readout of the grade (matching the label's own color).
    private const float IconBorderThickness = 2f;
    private static readonly Color EmptySlotBorderColor = new Color(0.55f, 0.55f, 0.6f, 0.65f);
    private static readonly Color IconSlotBackgroundColor = new Color(0.16f, 0.16f, 0.19f, 0.9f);
    // Two display stages placed far from the playable area (and far enough apart from each
    // other) so neither their point lights nor their preview cameras have anything else to
    // pick up.
    private static readonly Vector3 SwordStagePosition = new Vector3(500f, 5f, 0f);
    private static readonly Vector3 ShieldStagePosition = new Vector3(500f, -5f, 0f);
    // Every panel instance gets its own slice of that far-away staging area. The management
    // window hosts a SECOND EquipmentPanelView alongside the top-right one, and two panels
    // sharing a stage position would put both sets of items inside each other's preview
    // camera - each icon would render the wrong item, or both at once.
    private const float StageInstanceSpacing = 50f;
    private static int stageInstanceCounter;
    private int stageInstanceIndex;
    // Both mastery gauges fill the same sky blue - a per-slot color wasn't reading as
    // meaningful, just inconsistent next to each other.
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

    // Lets the management window build a panel in code. Must be called while the object is
    // still inactive - Awake (and therefore Build, which reads these) runs the moment a
    // component is added to an ACTIVE GameObject.
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

        // SwordEquipmentBonus/ShieldEquipmentBonus, not SwordAtkBonus/ShieldHpBonus - this
        // panel is the Equip tab's whole content, which shows pure equipment stats only. The
        // stone option that also feeds into the player's real total is the Stone tab's job to
        // display (see ManagementWindowView.OptionLine).
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
        // Left white regardless of grade - the mastery level tracks independently of grade
        // (still shown, and can already be above zero, even before anything of this type is
        // equipped), so it deliberately doesn't borrow the grade's color.
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

        // Frame, then a filled slot background, then the icon on top - drawn in that order
        // (later siblings render above earlier ones) so an unequipped slot still shows as a
        // thin colored square rather than nothing at all.
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

        // Top: "{grade} {type} {stat} + {bonus}", full width. Bottom: "Lv.N" beside the gauge,
        // both left-aligned with the icon's right edge and sharing the row's vertical centre
        // as their combined midpoint.
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
        // Inactive until Configure below has run: EquipmentMasteryGaugeView.Awake (which reads
        // these same fields to decide its layout) is deferred on an inactive GameObject, so
        // adding the component here can't race Configure setting the fields it depends on.
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
