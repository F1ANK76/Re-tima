using UnityEngine;

public partial class DropPickup : MonoBehaviour
{
    public enum Kind
    {
        StatPotion,
        Equipment
    }

    [Header("Kind")]
    // 프리팹마다 고정 -> 비주얼 슬롯 선택과 지급 내용을 함께 결정
    [SerializeField] private Kind kind = Kind.StatPotion;

    [Header("Toss + bounce (beat 1)")]
    // 던지기~안착 전체 시간
    [SerializeField] private float landDuration = 0.75f;
    // 플레이어 반대쪽으로 던지는 거리 -> 아이템이 몬스터 뒤편에 놓인다
    [SerializeField] private float tossDistance = 1.4f;
    // 첫 포물선 정점. 이후 반동은 HopHeights 배율
    [SerializeField] private float tossHeight = 1.1f;

    [Header("Run-over (beats 2-3)")]
    // 안착 후 정지 시간 -> 배경 스크롤이 속도 붙는 동안 아이템도 멈춰 있어야 한다
    [SerializeField] private float settleHoldDuration = 0.2f;
    // 획득 판정 반경. XZ 평면 -> 플레이어 transform이 캡슐 중심이라 3D 거리로는 안 닿는다
    [SerializeField] private float pickupRadius = 0.45f;

    // 몬스터 걷는 속도와 같아야 한다 -> 다르면 플레이어가 두 속도로 달리는 것처럼 보인다
    private float approachSpeed = 5f;

    [Header("Visual")]
    [SerializeField] private float visualBaseScale = 0.4f;
    // 종류별 프리팹/재질/회전. 등급 틴트는 아우라 담당 -> 여기엔 종류 구분만 있다
    [SerializeField] private DropVisualTableSO visualTable;

    [Header("Grade aura")]
    [SerializeField] private float auraSize = 1.5f;
    // 가산 헤일로 밝기 (등급 색에 곱해짐)
    [SerializeField] private float auraBrightnessMax = 1.7f;
    // 바닥에 깔리는 빛 자국. 아이템이 지면에서 떠 보이지 않게 하는 요인
    [SerializeField] private float groundGlowSize = 2.6f;
    [SerializeField] private float groundGlowBrightness = 1.1f;
    private const float GroundGlowLift = 0.02f;

    [Header("Twinkle sparkles")]
    // 위상을 서로 어긋나게 줄 것 -> 동시에 번쩍이면 반짝임이 아니라 깜빡이는 조명 하나가 된다
    [SerializeField] private int sparkleCount = 4;
    // 로컬 단위 -> 등급 스케일에 맞춰 고리도 넓어진다
    [SerializeField] private float sparkleOrbitRadius = 0.5f;
    [SerializeField] private float sparkleSize = 0.6f;
    // 헤일로보다 강해야 반짝임으로 인지된다
    [SerializeField] private float sparkleBrightnessMax = 2.4f;
    // 별 하나당 초당 깜빡임 횟수
    [SerializeField] private float sparkleBlinkSpeed = 1.1f;
    // 고리 전체 회전 -> 반짝임이 한자리에 박히지 않게
    [SerializeField] private float sparkleOrbitSpeed = 35f;

    // 첫 던지기 + 줄어드는 반동들. 각 도약은 지면->지면 사인 곡선. HopDurations 합 = 1
    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private StatGrade grade;
    private Transform player;
    private CombatLoop combatLoop;

    // kind == StatPotion일 때만 의미가 있다.
    private StatType statType;
    private float amount;

    // kind == Equipment일 때만 의미가 있다.
    private EquipmentType equipType;
    private EquipmentDropManager dropManager;

    // Push/Pop 짝 추적 -> 던지는 중 죽어 코루틴이 끊기면 idle hold가 영구히 남아 못 달리게 된다
    private bool idleHoldActive;
    private Vector3 restScale;
    // 피벗~메시 바닥 실측값 -> 등급마다 크기가 달라 고정값을 쓰면 큰 아이템이 바닥에 파묻힌다
    private float restBottomOffset;
    // 인스턴스마다 코드 생성 -> OnDestroy에서 직접 수거해야 한다
    private Material auraMaterial;
    private Material groundGlowMaterial;
    // 이 아이템의 반짝임 전부가 공유. auraMaterial과 같이 OnDestroy에서 수거
    private Material sparkleMaterial;
    // 공용 sword/shield 재질의 인스턴스별 복사본 -> 원본 애셋은 건드리면 안 된다
    private Material visualMaterialInstance;

    // StatDropManager가 스폰할 때 호출한다.
    public void InitializeStatPotion(StatType statType, StatGrade grade, float amount, Transform player, CombatLoop combatLoop, float approachSpeed)
    {
        kind = Kind.StatPotion;
        this.statType = statType;
        this.amount = amount;

        BeginDropSequence(grade, player, combatLoop, approachSpeed);
    }

    // EquipmentDropManager가 스폰할 때(그리고 EquipmentPreviewRig가 UI 아이콘용으로) 호출한다.
    public void InitializeEquipment(EquipmentType equipType, StatGrade grade, Transform player, CombatLoop combatLoop, float approachSpeed, EquipmentDropManager dropManager)
    {
        kind = Kind.Equipment;
        this.equipType = equipType;
        this.dropManager = dropManager;

        BeginDropSequence(grade, player, combatLoop, approachSpeed);
    }

    private void BeginDropSequence(StatGrade grade, Transform player, CombatLoop combatLoop, float approachSpeed)
    {
        this.grade = grade;
        this.player = player;
        this.combatLoop = combatLoop;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;

        SpawnVisual();
        // 프리팹 크기 = Normal -> 등급이 높을수록 커진다
        restScale = transform.localScale * GradeVisuals.GetPotionScale(grade);

        transform.localScale = restScale;
        restBottomOffset = ComputeBottomOffset();

        // ComputeBottomOffset 이후여야 한다 -> 헤일로 바운드까지 재면 아이템이 공중에 뜬다
        SpawnAura();

        // 1번 박자 시작 -> 아이템이 공중인 동안 플레이어를 idle로 붙잡는다
        if (combatLoop != null)
        {
            combatLoop.PushIdleHold();
            idleHoldActive = true;
        }

        StartCoroutine(TossThenRunOver());
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    // 몬스터와 플레이어가 같은 타격에 죽을 수 있다 -> 안 멈추면 죽은 플레이어에게 지급된다
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }


    // 접촉 시 실제 지급
    private void ApplyEffect()
    {
        if (kind == Kind.StatPotion)
        {
            PlayerCharacter pc = player.GetComponent<PlayerCharacter>();

            if (statType == StatType.Attack) pc.IncreaseAttack(amount);
            else pc.IncreaseMaxHp(amount);

            GameEvents.RaiseStatDropGained(grade, statType, amount);
        }
        else
        {
            if (dropManager != null) dropManager.CompleteDrop(equipType, grade);
        }
    }
}
