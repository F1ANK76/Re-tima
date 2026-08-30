using UnityEngine;

// StatPotionPickup의 장비 버전. 던지기/안착/달려가서 줍기 세 박자와 등급 아우라/반짝임은
// 공용 베이스(DropPickup, StatPotionPickup.cs에 있다)가 전부 처리하고, 여기서는 비주얼과
// 접촉 시 효과만 다르게 채운다: 스탯별 유리병 대신 등급 색이 입혀진 Sword/Shield 메시 하나.
//
// 장착/숙련 효과는 드롭 시점이 아니라 접촉 시 EquipmentDropManager.CompleteDrop에서
// 적용되므로, 드롭됐지만 끝내 닿지 못한 아이템(플레이어가 먼저 죽는 등)은 절대 효과를
// 지급하지 않는다.
public class EquipmentDropPickup : DropPickup
{
    [Header("Visual (per equipment type)")]
    // 타입별로 메시 하나씩, StatPotionPickup의 atkVisualPrefab/hpVisualPrefab과 동일한 방식이다 -
    // 희귀도는 등급별로 다른 모델이 아니라 베이스의 아우라/반짝임/크기 램프만으로 표현된다.
    [SerializeField] private GameObject swordVisualPrefab;
    [SerializeField] private GameObject shieldVisualPrefab;
    // 임포트로 딸려온 재질이 무엇이든 그 위에 덮어씌운다 - 생성된 FBX는 PBR 텍스처를 외부
    // 사이드카 경로(model.fbm 폴더)로 참조하는데 다운로드한 .fbx 하나만으로는 그게 없어,
    // 텍스처 없는 재질로 임포트되어 화면에 보이지 않게 렌더링된다. 여기서 덮어씌우면 FBX
    // 임포트가 어떻게 해석되든 재질 설정이 그와 무관하게 유지된다.
    [SerializeField] private Material swordMaterial;
    [SerializeField] private Material shieldMaterial;
    // 방패 메시는 XZ 평면에 눕혀진 원반(면이 위를 향함)으로 제작되어, 그대로 떨어뜨리면
    // 바닥에 놓인 그릇처럼 보인다. 세우는 회전은 애셋별 프레이밍이라 드롭 로직에 안 박는다.
    [SerializeField] private Vector3 swordVisualEuler = Vector3.zero;
    [SerializeField] private Vector3 shieldVisualEuler = new Vector3(90f, 0f, 0f);

    // SpawnAura가 붙이는 아우라/반짝임 쿼드가 아니라 메시 자체의 렌더러만 - 모든 등급/메시에
    // 맞는 고정 카메라 거리를 추측하는 대신, EquipmentPreviewRig가 아이템의 실제 실루엣에
    // 맞춰 UI 아이콘을 프레이밍하도록 노출한다.
    public Renderer[] VisualRenderers => renderers;

    private EquipmentType equipType;
    private EquipmentDropManager dropManager;
    // 등급 색을 입힌 공용 sword/shield 재질의 인스턴스별 복사본 - 활성화된 모든 픽업이
    // 같은 공용 애셋을 참조하므로 원본 애셋 자체는 절대 건드리지 않아야 한다.
    private Material visualMaterialInstance;

    public void Initialize(EquipmentType equipType, StatGrade grade, Transform player, CombatLoop combatLoop, float approachSpeed, EquipmentDropManager dropManager)
    {
        this.equipType = equipType;
        this.dropManager = dropManager;

        BeginDropSequence(grade, player, combatLoop, approachSpeed);
    }

    protected override void SpawnVisual()
    {
        GameObject prefab = equipType == EquipmentType.Sword ? swordVisualPrefab : shieldVisualPrefab;

        if (prefab == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            return;
        }

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(equipType == EquipmentType.Sword ? swordVisualEuler : shieldVisualEuler);
        visual.transform.localScale = Vector3.one * visualBaseScale;

        renderers = visual.GetComponentsInChildren<Renderer>();

        Material overrideMaterial = equipType == EquipmentType.Sword ? swordMaterial : shieldMaterial;
        if (overrideMaterial != null)
        {
            // 공유가 아니라 인스턴스화: _BaseColor 틴트는 재질의 원래 값에 그대로 곱해지므로,
            // 공용 애셋을 수정해버리면 화면에 있는 모든 sword/shield의 색까지 함께 바뀐다.
            visualMaterialInstance = new Material(overrideMaterial)
            {
                color = GradeVisuals.GetColor(grade)
            };

            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = visualMaterialInstance;
        }
    }

    protected override void ApplyEffect()
    {
        if (dropManager != null) dropManager.CompleteDrop(equipType, grade);
    }

    // 베이스가 자기 아우라/반짝임 재질을 정리하므로, 여기서는 이 클래스가 만든
    // 등급 색 재질 복사본만 추가로 수거한다.
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (visualMaterialInstance != null) Destroy(visualMaterialInstance);
    }
}
