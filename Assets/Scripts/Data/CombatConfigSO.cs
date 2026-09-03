using UnityEngine;

// 전투의 박자와 거리. 스테이지와 무관한 전역 값이라 에셋은 하나뿐이다.
// 드롭 수치는 StatDropTableSO.
[CreateAssetMenu(fileName = "CombatConfig", menuName = "Retima/Combat Config")]
public class CombatConfigSO : ScriptableObject
{
    [Header("Attack Timing")]
    // 플레이어 공격 간격 (첫 타격만 도착 즉시)
    public float tickInterval = 0.5f;
    // 몬스터 공격 간격 (종류 무관)
    public float normalMonsterAttackInterval = 0.4f;

    [Header("Monster Movement")]
    // 스폰부터 도달까지 걸리는 시간. 속도는 이 값에서 역산한다.
    public float monsterApproachDuration = 1f;
    // 스폰 지점에서 화면 밖으로 더 밀어내는 거리
    public float offscreenSpawnDistance = 8f;
    // 몬스터가 멈추는 거리. 큰 몬스터는 스케일만큼 더 멀리.
    public float meleeRange = 1.3f;
}
