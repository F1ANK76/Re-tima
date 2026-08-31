using UnityEngine;
using UnityEngine.UI;

// 장비 상태 표시부. 슬롯(Sword, Shield)당 한 줄: EquipmentPreviewRig로 렌더한 실제 장착
// 애셋(등급 오라 포함), 그 옆에 "Rare Sword ATK + 3" 줄. (숙련도 게이지/레벨 표시는 제거됨 -
// EquipmentDropManager의 숙련도 시스템이 비활성화되어 더 이상 보여줄 값이 없다.)
// 두 줄의 레이아웃(아이콘 프레임/슬롯/라벨)은 프리팹으로 미리 지어져 있다 - 이 스크립트는 그
// 참조만 들고 값을 채운다. 프리뷰 카메라/렌더 텍스처(EquipmentPreviewRig)만은 정적 에셋으로
// 구울 수 없는 실제 런타임 렌더 타겟이라 여전히 코드로 스폰한다.
[RequireComponent(typeof(RectTransform))]
public class EquipmentPanelView : MonoBehaviour
{
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    // EquipmentDropManager가 월드에 드롭하는 것과 동일한 프리팹이다 - 패널은 별도의 아이콘
    // 애셋이 아니라, 정지 상태로 배치된 동일 비주얼의 인스턴스를 보여준다.
    [SerializeField] private DropPickup previewPickupPrefab;

    [SerializeField] private RawImage swordIcon;
    [SerializeField] private RawImage shieldIcon;
    [SerializeField] private Image swordIconFrame;
    [SerializeField] private Image shieldIconFrame;
    [SerializeField] private Text swordLabel;
    [SerializeField] private Text shieldLabel;

    // 아이콘 둘레의 얇은 테두리 색 - 빈 슬롯도 빈 공간이 아니라 슬롯으로 읽히고, 장착 시엔
    // 라벨 색과 맞춰져 등급을 항상 보여주는 두 번째 표시 수단을 겸한다.
    private static readonly Color EmptySlotBorderColor = new Color(0.55f, 0.55f, 0.6f, 0.65f);
    // 플레이 영역에서 멀리 떨어진(그리고 서로도 충분히 떨어진) 두 개의 전시용 스테이지 -
    // 각각의 포인트 라이트나 프리뷰 카메라가 원치 않는 다른 것을 비추지 않도록 하기 위함이다.
    private static readonly Vector3 SwordStagePosition = new Vector3(500f, 5f, 0f);
    private static readonly Vector3 ShieldStagePosition = new Vector3(500f, -5f, 0f);
    // 패널 인스턴스마다 그 먼 전시 영역 안에서 자기 구역을 받는다 - 스테이지 위치를 공유하면
    // 서로의 아이템이 상대 프리뷰 카메라에 들어가 엉뚱한 아이템이 찍히거나 둘 다 함께 비친다.
    // 지금은 관리 창의 Equip 탭 하나만 이 패널을 띄우지만, 동시에 여러 인스턴스가 떠도
    // 안전하도록 카운터 기반으로 짜여 있다.
    private const float StageInstanceSpacing = 50f;
    private static int stageInstanceCounter;
    private int stageInstanceIndex;

    private EquipmentPreviewRig swordRig;
    private EquipmentPreviewRig shieldRig;

    // 관리 창이 패널을 활성화하기 전에 의존성을 채워 넣을 수 있게 해준다. 오브젝트가 비활성일
    // 때 호출해야 한다 - 활성 GameObject라면 Awake(그리고 이 필드를 읽는 BuildRigs)가 이미
    // 먼저 실행됐을 것이다.
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

    // 프리뷰 카메라 + 렌더 텍스처는 매 인스턴스마다 새로 만들어지는 실제 런타임 리소스라
    // 정적 에셋으로 대체할 수 없다 - 여기만 코드로 남는다.
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
        return rig;
    }
}
