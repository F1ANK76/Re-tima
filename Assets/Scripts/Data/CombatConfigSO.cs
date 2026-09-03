using UnityEngine;

// 전투의 박자와 거리. 스테이지 번호와는 무관한 전역 튜닝 값이라 에셋도 하나뿐이다
// (예전 이름 StageConfigSO는 스테이지별 설정처럼 읽혀서 바꿨다).
//
// 드롭 수치는 StatDropTableSO로 옮겼다 - 전투 타이밍과 아이템 밸런스는 같이 만질 일이 없다.
[CreateAssetMenu(fileName = "CombatConfig", menuName = "Retima/Combat Config")]
public class CombatConfigSO : ScriptableObject
{
    [Header("Attack Timing")]
    // 플레이어가 몇 초마다 한 대씩 때리는가(첫 타격은 도착 즉시라 예외 - CombatLoop 참고).
    public float tickInterval = 0.5f;
    // 몬스터 쪽 공격 간격. 종류와 무관하게 전부 이 값을 쓴다.
    public float normalMonsterAttackInterval = 0.4f;

    [Header("Monster Movement")]
    // 몬스터가 화면 밖 스폰 지점에서 근접 사거리(meleeRange)까지 걸어오는 데 걸려야 하는 시간.
    // 실제 속도(MonsterSpawner.ApproachSpeed 참고)는 이 시간과 씬의 실제 스폰~플레이어 거리로부터
    // 역산되므로, 스폰 지점이나 플레이어 마커를 옮겨도 이 값은 그대로 "도달까지 걸리는 시간"으로
    // 유지된다.
    public float monsterApproachDuration = 1f;
    // MonsterSpawner가 몬스터를 화면 밖으로 스폰시키는, 스폰 지점 기준 추가 거리.
    public float offscreenSpawnDistance = 8f;
    // 몬스터가 플레이어 앞에서 멈추는 거리. 큰 몬스터는 자기 스케일에 비례해 더 멀리 멈춘다
    // (Monster.SetMovement 참고).
    public float meleeRange = 1.3f;
}
