using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class EquipmentPanelView : MonoBehaviour
{
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    [SerializeField] private DropPickup previewPickupPrefab;

    [SerializeField] private RawImage swordIcon;
    [SerializeField] private RawImage shieldIcon;
    [SerializeField] private Image swordIconFrame;
    [SerializeField] private Image shieldIconFrame;
    [SerializeField] private Text swordLabel;
    [SerializeField] private Text shieldLabel;

    private static readonly Color EmptySlotBorderColor = new Color(0.55f, 0.55f, 0.6f, 0.65f);
    private static readonly Vector3 SwordStagePosition = new Vector3(500f, 5f, 0f);
    private static readonly Vector3 ShieldStagePosition = new Vector3(500f, -5f, 0f);
    private const float StageInstanceSpacing = 50f;
    private static int stageInstanceCounter;
    private int stageInstanceIndex;

    private EquipmentPreviewRig swordRig;
    private EquipmentPreviewRig shieldRig;

    public void Configure(EquipmentDropManager manager, DropPickup previewPrefab)
    {
        equipmentDropManager = manager;
        previewPickupPrefab = previewPrefab;
    }

    private void Awake()
    {
        stageInstanceIndex = stageInstanceCounter++;
        BuildRigs();
    }

    private void OnEnable()
    {
        GameEvents.OnEquipmentPickedUp += HandleEquipmentPickedUp;

        // Refresh보다 앞에 -> 꺼진 리그 밑에서는 프리뷰의 코루틴이 시작되지 않는다
        SetRigsActive(true);
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.OnEquipmentPickedUp -= HandleEquipmentPickedUp;

        // 리그는 씬 루트에 있어 패널과 함께 안 꺼진다 -> 직접 끈다.
        // 안 끄면 아이콘 카메라 2대가 창을 닫은 뒤에도 매 프레임 렌더타겟을 갱신한다
        SetRigsActive(false);
    }

    private void SetRigsActive(bool active)
    {
        swordRig.gameObject.SetActive(active);
        shieldRig.gameObject.SetActive(active);
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

    private void BuildRigs()
    {
        Vector3 stageOffset = new Vector3(0f, 0f, stageInstanceIndex * StageInstanceSpacing);
        swordRig = BuildRig("SwordPreviewRig", SwordStagePosition + stageOffset);
        shieldRig = BuildRig("ShieldPreviewRig", ShieldStagePosition + stageOffset);
        swordIcon.texture = swordRig.Texture;
        shieldIcon.texture = shieldRig.Texture;
    }

    private EquipmentPreviewRig BuildRig(string name, Vector3 stagePosition)
    {
        var go = new GameObject(name);
        go.transform.position = stagePosition;

        var rig = go.AddComponent<EquipmentPreviewRig>();
        rig.Initialize(previewPickupPrefab);

        // 패널이 열릴 때 켠다 -> Equip 탭을 안 열면 렌더링 비용이 0이다
        go.SetActive(false);
        return rig;
    }
}
