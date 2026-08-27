using UnityEngine;

// 예전 AP 시스템을 대체한다: 스탯은 더 이상 플레이어가 수동으로 업그레이드하는 소모성 재화에서
// 나오지 않고, 처치마다 랜덤 스탯 상승치가 직접 드롭될 확률로 나온다. 드롭 발생 확률과 뽑히는
// 희귀도 모두 런이 상위 메인 스테이지에 도달할수록 올라간다.
public class StatDropManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private StatPotionPickup potionPrefab;
    [SerializeField] private CombatLoop combatLoop;

    private int currentMainStage = 1;

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

    // 스탯 드롭은 stage 1만의 개념이다 - stage 2부터는 대신 EquipmentDropManager(그쪽의
    // UnlockStage 참고)에게 그라인드를 넘기므로 둘은 절대 겹치지 않는다.
    private const int StatDropMaxStage = 1;

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
    }

    private void HandleMonsterDied(Monster monster)
    {
        // Elite/Boss는 (서브)스테이지를 마무리 짓는 관문일 뿐 전리품의 출처가 아니다 - 그 사이의
        // 일반 몬스터 그라인드에서만 뭔가가 드롭된다.
        if (monster.Type != MonsterType.Normal) return;

        if (currentMainStage > StatDropMaxStage) return;

        // 기본 확률 50%, 첫 메인 스테이지 이후로는 스테이지당 +10%p (1-1=50%, stage2=60%, ...).
        float dropChance = Mathf.Clamp01(stageConfig.statDropBaseChance + stageConfig.statDropChancePerStage * (currentMainStage - 1));
        if (Random.value > dropChance) return;

        // 현재는 ATK/HP만 존재하므로 단순 50/50이다 - 하드코딩된 동전 던지기 대신 1-in-N
        // 형태로 작성해서, 나중에 세 번째 스탯이 추가되더라도 이 롤의 범위만 넓히면 되게 했다.
        const int statTypeCount = 2;
        StatType statType = Random.Range(0, statTypeCount) == 0 ? StatType.Attack : StatType.Hp;

        StatGrade grade = GradeRoller.Roll();
        float amount = GetAmount(statType, grade);

        // 스탯은 즉시 적용되지 않는다 - 몬스터가 죽은 위치에 드롭되는 포션에 넘겨지고, 그 포션이
        // 플레이어에게 도달했을 때만 지급된다(StatPotionPickup 참고). prefab이나 player가 없으면
        // 넘겨줄 대상 자체가 없어 드롭도 없다.
        if (potionPrefab == null || player == null) return;

        StatPotionPickup potion = Instantiate(potionPrefab, monster.transform.position, Quaternion.identity);
        // 몬스터가 걸어 들어오는 속도와 동일하게 맞춰서, 포션이 플레이어 쪽으로 미끄러져
        // 들어오는 것도 별개의 무관한 속도가 아니라 같은 전진 이동으로 보이게 한다.
        potion.Initialize(statType, grade, amount, player.transform, combatLoop, stageConfig.monsterMoveSpeed);
    }

    private float GetAmount(StatType statType, StatGrade grade)
    {
        if (statType == StatType.Attack)
        {
            switch (grade)
            {
                case StatGrade.Normal: return stageConfig.atkAmountNormal;
                case StatGrade.Rare: return stageConfig.atkAmountRare;
                case StatGrade.Epic: return stageConfig.atkAmountEpic;
                case StatGrade.Unique: return stageConfig.atkAmountUnique;
                default: return stageConfig.atkAmountLegendary;
            }
        }

        switch (grade)
        {
            case StatGrade.Normal: return stageConfig.hpAmountNormal;
            case StatGrade.Rare: return stageConfig.hpAmountRare;
            case StatGrade.Epic: return stageConfig.hpAmountEpic;
            case StatGrade.Unique: return stageConfig.hpAmountUnique;
            default: return stageConfig.hpAmountLegendary;
        }
    }
}
