using System;

public static class GameEvents
{
    public static event Action<Monster> OnMonsterSpawned;
    public static event Action<Monster> OnMonsterDied;
    public static event Action<Monster, float> OnMonsterDamaged;
    public static event Action<PlayerStats> OnPlayerStatsChanged;
    public static event Action<int, int> OnStageChanged;
    public static event Action<int> OnBossGaugeChanged;
    public static event Action<StatGrade, StatType, float> OnStatDropGained;
    public static event Action<EquipmentType, StatGrade> OnEquipmentPickedUp;
    public static event Action OnPlayerDied;

    public static void RaiseMonsterSpawned(Monster monster) => OnMonsterSpawned?.Invoke(monster);
    public static void RaiseMonsterDied(Monster monster) => OnMonsterDied?.Invoke(monster);
    public static void RaiseMonsterDamaged(Monster monster, float amount) => OnMonsterDamaged?.Invoke(monster, amount);
    public static void RaisePlayerStatsChanged(PlayerStats stats) => OnPlayerStatsChanged?.Invoke(stats);
    public static void RaiseStageChanged(int mainStage, int subStage) => OnStageChanged?.Invoke(mainStage, subStage);
    public static void RaiseBossGaugeChanged(int percent) => OnBossGaugeChanged?.Invoke(percent);
    public static void RaiseStatDropGained(StatGrade grade, StatType statType, float amount) => OnStatDropGained?.Invoke(grade, statType, amount);
    public static void RaiseEquipmentPickedUp(EquipmentType equipType, StatGrade grade) => OnEquipmentPickedUp?.Invoke(equipType, grade);
    public static void RaisePlayerDied() => OnPlayerDied?.Invoke();
}
