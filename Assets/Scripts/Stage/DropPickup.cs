using UnityEngine;

public partial class DropPickup : MonoBehaviour
{
    public enum Kind
    {
        StatPotion,
        Equipment
    }

    // 종류별 애셋 + 모션/이펙트 튜닝값 전부
    [SerializeField] private DropPickupConfigSO config;

    // 몬스터 걷는 속도와 같아야 한다 -> 다르면 플레이어가 두 속도로 달리는 것처럼 보인다
    private float approachSpeed = 5f;

    // 첫 던지기 + 줄어드는 반동들. 각 도약은 지면->지면 사인 곡선. HopDurations 합 = 1
    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    // Initialize에서 정해진다 -> 비주얼 조회와 지급 내용을 함께 결정
    private Kind kind;

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
