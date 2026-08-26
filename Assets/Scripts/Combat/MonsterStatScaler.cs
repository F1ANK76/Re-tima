using UnityEngine;

public static class MonsterStatScaler
{
    public static (float hp, float attack) GetScaledStats(int stage, MonsterDefinitionSO definition, StageConfigSO config)
    {
        int stageIndex = Mathf.Max(0, stage - 1);
        float hp = definition.baseHp * Mathf.Pow(1f + config.hpGrowthPerStage, stageIndex);
        float attack = definition.baseAttack * Mathf.Pow(1f + config.atkGrowthPerStage, stageIndex);
        return (hp, attack);
    }
}
