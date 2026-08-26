using UnityEngine;

// Replaces the old AP system: stats no longer come from a spendable currency the player
// upgrades manually. Instead every monster kill has a chance to drop a random stat boost
// directly, with the odds (both of a drop happening at all, and of which rarity it rolls)
// climbing as the run reaches higher main stages.
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

    // Stat drops are stage 1's concept - stage 2 on hands the grind over to
    // EquipmentDropManager (see its UnlockStage) instead, so the two never overlap.
    private const int StatDropMaxStage = 1;

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
    }

    private void HandleMonsterDied(Monster monster)
    {
        // Elite/Boss are the gate that closes out a (sub)stage, not a source of loot - only
        // the normal-monster grind in between them drops anything.
        if (monster.Type != MonsterType.Normal) return;

        if (currentMainStage > StatDropMaxStage) return;

        // Base 50% chance, +10%p per main stage past the first (1-1=50%, stage2=60%, ...).
        float dropChance = Mathf.Clamp01(stageConfig.statDropBaseChance + stageConfig.statDropChancePerStage * (currentMainStage - 1));
        if (Random.value > dropChance) return;

        // Only ATK/HP exist today, so this is a flat 50/50 - written as 1-in-N rather than
        // a hardcoded coin flip so a future third stat only means widening this roll.
        const int statTypeCount = 2;
        StatType statType = Random.Range(0, statTypeCount) == 0 ? StatType.Attack : StatType.Hp;

        StatGrade grade = GradeRoller.Roll();
        float amount = GetAmount(statType, grade);

        // The stat no longer applies instantly - it's handed to a potion that drops where
        // the monster died and only pays out once it reaches the player (see
        // StatPotionPickup). No prefab/player, no drop: nothing left to hand it off to.
        if (potionPrefab == null || player == null) return;

        StatPotionPickup potion = Instantiate(potionPrefab, monster.transform.position, Quaternion.identity);
        // Same speed monsters walk in at, so the potion sliding into the player reads as the
        // same forward travel rather than a second, unrelated pace.
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
