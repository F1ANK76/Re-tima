using UnityEngine;

[CreateAssetMenu(fileName = "StageConfig", menuName = "Retima/Stage Config")]
public class StageConfigSO : ScriptableObject
{
    [Header("Combat Timing")]
    public float tickInterval = 0.5f;
    public float normalMonsterAttackInterval = 0.4f;

    [Header("Monster Scaling")]
    public float hpGrowthPerStage = 0.15f;
    public float atkGrowthPerStage = 0.10f;

    [Header("Stat Drop Amounts - ATK")]
    public float atkAmountNormal = 0.1f;
    public float atkAmountRare = 0.3f;
    public float atkAmountEpic = 0.5f;
    public float atkAmountUnique = 1f;
    public float atkAmountLegendary = 2f;

    [Header("Stat Drop Amounts - HP")]
    public float hpAmountNormal = 5f;
    public float hpAmountRare = 10f;
    public float hpAmountEpic = 30f;
    public float hpAmountUnique = 50f;
    public float hpAmountLegendary = 100f;


    [Header("Monster Movement")]
    // 몬스터가 화면 밖 스폰 지점에서 근접 사거리(meleeRange)까지 걸어오는 데 걸려야 하는 시간.
    // 실제 속도(MonsterSpawner.ApproachSpeed 참고)는 이 시간과 씬의 실제 스폰~플레이어 거리로부터
    // 역산되므로, 스폰 지점이나 플레이어 마커를 옮겨도 이 값은 그대로 "도달까지 걸리는 시간"으로
    // 유지된다.
    public float monsterApproachDuration = 1f;
    // MonsterSpawner가 몬스터를 화면 밖으로 스폰시키는, 스폰 지점 기준 추가 거리.
    public float offscreenSpawnDistance = 8f;
    public float meleeRange = 1.3f;

}
