using UnityEngine;

// 예전 AP 시스템을 대체한다: 스탯은 더 이상 플레이어가 수동으로 업그레이드하는 소모성 재화에서
// 나오지 않고, 처치마다 랜덤 스탯 상승치가 직접 드롭된다. 드롭 여부/타이밍은 DropCoordinator가
// 결정하고, 여기서는 그렇게 선택됐을 때 실제로 무엇이 드롭될지(스탯 종류, 등급, 수치)만 굴린다.
public class StatDropManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private StatPotionPickup potionPrefab;
    [SerializeField] private CombatLoop combatLoop;

    // DropCoordinator가 이 타입을 드롭하기로 정했을 때 호출한다.
    public void RollAndSpawn(Monster monster)
    {
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
