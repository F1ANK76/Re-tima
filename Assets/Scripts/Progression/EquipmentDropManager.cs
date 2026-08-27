using UnityEngine;

// 같은 처치 이벤트에서 StatDropManager와 독립적으로 도는 두 번째 드롭 롤 - 순수 ATK/HP
// 증가치 대신 실제 Sword/Shield 아이템을 준다. 픽업 하나당 두 가지가 독립적으로 일어난다:
//   1. 자동 장착 - 픽업 등급이 현재 장착분보다 높을 때만.
//   2. 숙련도 진행 - (1)과 무관하게 항상. 같거나 낮은 등급도 상한 없는 게이지를 채우고,
//      100%를 넘길 때마다 +1 ATK / +10 HP로 전환된다. "쓸모없는" 중복 픽업의 존재 이유다.
public class EquipmentDropManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private EquipmentDropPickup pickupPrefab;
    [SerializeField] private CombatLoop combatLoop;

    private int currentMainStage = 1;

    // null은 해당 타입이 아직 아무것도 장착되지 않았음을 뜻한다 - 이미 실질적인(작지만) 보너스가
    // 있는 "Normal 등급 장착" 상태와는 구분된다.
    private StatGrade? equippedSwordGrade;
    private StatGrade? equippedShieldGrade;

    // 퍼센트 단위의 상한 없는 숙련도 게이지 - 예전 파편 시스템과 같은 이유로 의도적으로 랩어라운드나
    // 클램프를 하지 않는다: 350%도 후반 런에서는 정상적인 값이다.
    private float swordMasteryPercent;
    private float shieldMasteryPercent;
    private int swordLevel;
    private int shieldLevel;

    // 스톤 리롤로 슬롯에 박은 추가 옵션 값 + 그 값을 만든 등급(UI 색 표시용으로만 보관).
    // 누적이 아니라 대체다: 슬롯은 지금까지 나온 것 중 가장 좋은 롤 하나만 유지한다.
    private float swordAtkOption;
    private float shieldHpOption;
    private StatGrade? swordAtkOptionGrade;
    private StatGrade? shieldHpOptionGrade;


    public StatGrade? EquippedSwordGrade => equippedSwordGrade;
    public StatGrade? EquippedShieldGrade => equippedShieldGrade;
    public int SwordLevel => swordLevel;
    public int ShieldLevel => shieldLevel;
    // 게이지가 그려야 할, 현재 진행 중인 레벨 내에서의 게이지 위치(0-100) - 레벨 수를
    // 산출하는 데 쓰이는 상한 없는 누적 총량과는 다른 값이다.
    public float SwordMasteryProgressPercent => swordMasteryPercent % 100f;
    public float ShieldMasteryProgressPercent => shieldMasteryPercent % 100f;
    public float SwordAtkOption => swordAtkOption;
    public float ShieldHpOption => shieldHpOption;
    public StatGrade? SwordAtkOptionGrade => swordAtkOptionGrade;
    public StatGrade? ShieldHpOptionGrade => shieldHpOptionGrade;
    public float GetOption(StatType statType) => statType == StatType.Attack ? swordAtkOption : shieldHpOption;
    public StatGrade? GetOptionGrade(StatType statType) => statType == StatType.Attack ? swordAtkOptionGrade : shieldHpOptionGrade;

    // 스톤 옵션을 뺀 등급 + 숙련도만 - Equip 탭에 표시되는 값이라 여기서 "장비"는 sword/shield
    // 자체만을 뜻한다. 옵션은 Stone 탭에만 표시된다(ManagementWindowView.OptionLine 참고).
    public float SwordEquipmentBonus => GradeBonus(equippedSwordGrade, EquipmentGradeBonus.GetSwordAtk) + swordLevel * EquipmentGradeBonus.SwordLevelAtkBonus;
    public float ShieldEquipmentBonus => GradeBonus(equippedShieldGrade, EquipmentGradeBonus.GetShieldHp) + shieldLevel * EquipmentGradeBonus.ShieldLevelHpBonus;

    // 플레이어 스탯에 실제 적용되는 총합(장비 보너스 + 스톤 옵션). 아래 old-vs-new 델타 계산과
    // TryApplyOption 전용이고 표시에는 안 쓴다 - 옵션은 이 탭에서 숨겨도 게임플레이엔 반영된다.
    public float SwordAtkBonus => SwordEquipmentBonus + swordAtkOption;
    public float ShieldHpBonus => ShieldEquipmentBonus + shieldHpOption;

    private static float GradeBonus(StatGrade? grade, System.Func<StatGrade, float> lookup) => grade.HasValue ? lookup(grade.Value) : 0f;

    private void OnEnable()
    {
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
    }

    private const int UnlockStage = 2;
    // 장비는 stage 2에서만 드롭되고, stage 3부터는 StoneDropManager에게 그라인드를 넘긴다
    // (한 스테이지 앞서 같은 역할을 하는 StatDropManager의 StatDropMaxStage 참고). 없으면
    // "스테이지당 드롭 타입 하나" 규칙이 stage 3에서 깨져 크리스탈과 장비가 같이 드롭됐다.
    private const int EquipDropMaxStage = 2;

    private void HandleMonsterDied(Monster monster)
    {
        // Elite/Boss는 (서브)스테이지를 마무리 짓는 관문일 뿐 전리품의 출처가 아니다 - 그 사이의
        // 일반 몬스터 그라인드에서만 뭔가가 드롭된다.
        if (monster.Type != MonsterType.Normal) return;

        // stage 1은 스탯 파밍 스테이지라 드롭이 전혀 없다. stage 2에 기본 확률이 적용되고
        // 그 이후로 계속 오른다(stage 2 = 30%, stage 3 = 35%, ...).
        if (currentMainStage < UnlockStage) return;
        if (currentMainStage > EquipDropMaxStage) return;

        float dropChance = Mathf.Clamp01(stageConfig.equipDropBaseChance + stageConfig.equipDropChancePerStage * (currentMainStage - UnlockStage));
        if (Random.value > dropChance) return;

        EquipmentType equipType = Random.value < 0.5f ? EquipmentType.Sword : EquipmentType.Shield;
        StatGrade grade = GradeRoller.Roll();

        // prefab/player가 없어도 롤은 그대로 처리한다(업그레이드면 장착·보상 지급까지) -
        // 비주얼이 아직 연결되지 않았다는 이유로 조용히 유실되게 두지 않는다.
        if (pickupPrefab == null || player == null)
        {
            CompleteDrop(equipType, grade);
            return;
        }

        EquipmentDropPickup pickup = Instantiate(pickupPrefab, monster.transform.position, Quaternion.identity);
        // 몬스터가 걸어 들어오는 속도와 동일하게 맞춘다 - StatPotionPickup의 방식과 일치시켜서
        // 두 드롭 타입 모두 같은 전진 이동으로 보이게 한다.
        pickup.Initialize(equipType, grade, player.transform, combatLoop, stageConfig.monsterMoveSpeed, this);
    }

    // 플레이어가 픽업에 도달하면 EquipmentDropPickup이 호출한다. 등급과 무관하게 해당 타입의
    // 숙련도 게이지는 항상 차고, 장착분보다 높은 등급일 때만 장착 아이템도 교체된다. 두 기여분을
    // 단일 델타로 적용하므로 레벨업 임계값 아래의 중복은 무비용이고, 둘 다 걸려도 중복 집계 없다.
    public void CompleteDrop(EquipmentType equipType, StatGrade grade)
    {
        if (equipType == EquipmentType.Sword)
        {
            float oldBonus = SwordAtkBonus;

            swordMasteryPercent += EquipmentGradeBonus.GetProgressPercent(grade);
            swordLevel = Mathf.FloorToInt(swordMasteryPercent / 100f);

            if (!equippedSwordGrade.HasValue || grade > equippedSwordGrade.Value) equippedSwordGrade = grade;

            if (player != null) player.IncreaseAttack(SwordAtkBonus - oldBonus);
        }
        else
        {
            float oldBonus = ShieldHpBonus;

            shieldMasteryPercent += EquipmentGradeBonus.GetProgressPercent(grade);
            shieldLevel = Mathf.FloorToInt(shieldMasteryPercent / 100f);

            if (!equippedShieldGrade.HasValue || grade > equippedShieldGrade.Value) equippedShieldGrade = grade;

            if (player != null) player.IncreaseMaxHp(ShieldHpBonus - oldBonus);
        }

        // 이제 모든 픽업은 알릴 가치가 있다 - 장착 상태를 바꾸지 않는 픽업이라도 숙련도
        // 게이지는 움직였기 때문이다.
        GameEvents.RaiseEquipmentPickedUp(equipType, grade);
    }


    // 롤된 옵션을 슬롯의 기존 값보다 높을 때만(엄격한 초과 >) 확정한다. UI가 아니라 여기서
    // 강제한다 - "슬롯은 최선의 옵션을 유지한다"는 데이터 규칙이라 데이터와 함께 있어야 한다.
    public bool TryApplyOption(StatType statType, StatGrade grade, float value)
    {
        if (statType == StatType.Attack)
        {
            if (value <= swordAtkOption) return false;

            // 쓰기 전에 캡처해서 델타로 적용 - CompleteDrop과 같은 방식이며, 나머지 보너스의
            // 구성을 몰라도 플레이어 스탯이 옵션을 그대로 추적할 수 있다.
            float oldBonus = SwordAtkBonus;
            swordAtkOption = value;
            swordAtkOptionGrade = grade;
            if (player != null) player.IncreaseAttack(SwordAtkBonus - oldBonus);
        }
        else
        {
            if (value <= shieldHpOption) return false;

            float oldBonus = ShieldHpBonus;
            shieldHpOption = value;
            shieldHpOptionGrade = grade;
            if (player != null) player.IncreaseMaxHp(ShieldHpBonus - oldBonus);
        }

        GameEvents.RaiseEquipmentPickedUp(
            statType == StatType.Attack ? EquipmentType.Sword : EquipmentType.Shield,
            equippedSwordGrade ?? StatGrade.Normal);
        return true;
    }
}
