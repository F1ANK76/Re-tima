using System;

public static class GameEvents
{
    public static event Action<Monster> OnMonsterSpawned;
    public static event Action<Monster> OnMonsterDied;
    // Fires on every point of player-dealt damage - both the normal swing (PlayerCharacter.
    // Attack) and the ultimate (UltimateManager) land on Monster.TakeDamage, so this single
    // hook covers both without either system needing its own damage-number plumbing.
    public static event Action<Monster, float> OnMonsterDamaged;
    public static event Action<PlayerStats> OnPlayerStatsChanged;
    public static event Action<int, int> OnStageChanged;
    public static event Action<int> OnBossGaugeChanged;
    // Fires whenever a monster kill rolls a successful stat drop, carrying the rolled
    // grade/stat/amount so UI (popup, stat panel) can react without re-deriving the roll.
    public static event Action<StatGrade, StatType, float> OnStatDropGained;
    // Fires on every collected sword/shield, carrying that pickup's own type/grade - whether
    // or not it actually beat the equipped grade, it always feeds the mastery meter (see
    // EquipmentDropManager.CompleteDrop), so every pickup is worth announcing.
    public static event Action<EquipmentType, StatGrade> OnEquipmentPickedUp;
    // Fires whenever a slot's stone count changes - carrying the new total and the delta, so
    // a pickup popup can say "+4" while a counter readout just takes the total.
    public static event Action<StatType, int, int> OnStonesChanged;
    public static event Action OnPlayerDied;

    // Phase 2 hook (parry) - declared only, nothing raises or subscribes yet.
    public static event Action<bool> OnParryResult;

    public static void RaiseMonsterSpawned(Monster monster) => OnMonsterSpawned?.Invoke(monster);
    public static void RaiseMonsterDied(Monster monster) => OnMonsterDied?.Invoke(monster);
    public static void RaiseMonsterDamaged(Monster monster, float amount) => OnMonsterDamaged?.Invoke(monster, amount);
    public static void RaisePlayerStatsChanged(PlayerStats stats) => OnPlayerStatsChanged?.Invoke(stats);
    public static void RaiseStageChanged(int mainStage, int subStage) => OnStageChanged?.Invoke(mainStage, subStage);
    public static void RaiseBossGaugeChanged(int percent) => OnBossGaugeChanged?.Invoke(percent);
    public static void RaiseStatDropGained(StatGrade grade, StatType statType, float amount) => OnStatDropGained?.Invoke(grade, statType, amount);
    public static void RaiseEquipmentPickedUp(EquipmentType equipType, StatGrade grade) => OnEquipmentPickedUp?.Invoke(equipType, grade);
    public static void RaiseStonesChanged(StatType statType, int total, int delta) => OnStonesChanged?.Invoke(statType, total, delta);
    public static void RaisePlayerDied() => OnPlayerDied?.Invoke();
    public static void RaiseParryResult(bool success) => OnParryResult?.Invoke(success);
}
