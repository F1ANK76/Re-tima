using System;

public static class GameEvents
{
    public static event Action<Monster> OnMonsterSpawned;
    public static event Action<Monster> OnMonsterDied;
    // 플레이어가 가하는 모든 피해 지점마다 발생한다 - 일반 공격(PlayerCharacter.Attack)과
    // 궁극기(UltimateManager) 모두 결국 Monster.TakeDamage로 귀결되므로, 이 훅 하나로
    // 두 시스템 모두를 커버하며 각자 자체적인 데미지 표시 배관을 만들 필요가 없다.
    public static event Action<Monster, float> OnMonsterDamaged;
    public static event Action<PlayerStats> OnPlayerStatsChanged;
    public static event Action<int, int> OnStageChanged;
    public static event Action<int> OnBossGaugeChanged;
    // 몬스터 처치가 스탯 드롭 성공 롤을 만들어낼 때마다 발생하며, 롤된 등급/스탯/수치를 함께
    // 실어 보내서 UI(팝업, 스탯 패널)가 그 롤을 다시 계산하지 않고도 반응할 수 있게 한다.
    public static event Action<StatGrade, StatType, float> OnStatDropGained;
    // 획득한 모든 sword/shield에서 발생하며, 그 픽업 자체의 타입/등급을 실어 보낸다 - 실제로
    // 장착된 등급을 넘었는지 여부와 무관하게 항상 숙련도 게이지에 반영되므로
    // (EquipmentDropManager.CompleteDrop 참고), 모든 픽업이 알릴 가치가 있다.
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
