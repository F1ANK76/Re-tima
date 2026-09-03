using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    // 메인 스테이지 하나의 몬스터 구성
    [System.Serializable]
    public class StageRoster
    {
        public MonsterDefinitionSO normal;
        public MonsterDefinitionSO elite;
        public MonsterDefinitionSO boss;
    }

    // 스테이지별 몬스터 정보
    [SerializeField] private List<StageRoster> rosters = new List<StageRoster>();

    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform playerTransform;

    // 몬스터(그리고 몬스터와 같은 속도로 끌려오는 스탯 포션/장비/스톤 픽업)가 실제로 쓰는 속도.
    // 씬에 배치된 spawnPoint/playerTransform의 실제 위치로부터 역산하므로, 두 마커 사이의 간격이
    // (지금 씬처럼) offscreenSpawnDistance와 다르더라도 "스폰부터 도달까지 monsterApproachDuration초"가
    // 항상 정확히 지켜진다.
    public static float ApproachSpeed { get; private set; }

    private void Awake()
    {
        ApproachSpeed = ComputeApproachSpeed();
    }

    private float ComputeApproachSpeed()
    {
        Vector3 spawnFrom = spawnPoint.position + new Vector3(stageConfig.offscreenSpawnDistance, 0f, 0f);
        Vector3 toPlayer = playerTransform.position - spawnFrom;
        toPlayer.y = 0f;

        // 근접 사거리(meleeRange)만큼은 실제로 걸어갈 필요가 없으므로 총 거리에서 뺀다 - 스케일
        // 1(일반 몬스터) 기준이라, 더 일찍 멈추는 엘리트/보스는 같은 속도로도 목표 시간보다
        // 조금 더 빨리 도착한다.
        float travelDistance = Mathf.Max(0.01f, toPlayer.magnitude - stageConfig.meleeRange);
        return stageConfig.monsterApproachDuration > 0f ? travelDistance / stageConfig.monsterApproachDuration : 0f;
    }

    // 해당 스테이지에 해당하는 몬스터 타입의 정보를 반환
    private MonsterDefinitionSO Resolve(int mainStage, MonsterType type)
    {
        int index = mainStage - 1;
        StageRoster roster;

        if (index >= 0 && index < rosters.Count && rosters[index] != null)
        {
            // 이 스테이지 전용 로스터가 있으면 그걸 쓴다.
            roster = rosters[index];
        }
        else
        {
            // 없어도 스폰은 해야 하니 첫 항목(기본 구성)으로 대신한다.
            roster = rosters[0];
        }

        return Pick(roster, type);
    }

    private static MonsterDefinitionSO Pick(StageRoster roster, MonsterType type)
    {
        switch (type)
        {
            case MonsterType.Normal: return roster.normal;
            case MonsterType.Elite: return roster.elite;
            case MonsterType.Boss: return roster.boss;
            default: return null;
        }
    }

    [SerializeField] private float eliteScale = 1.5f;
    [SerializeField] private float bossScale = 2f;

    [Header("HP curve")]
    // 엘리트 한 마리는 같은 서브스테이지의 일반 몬스터 이 개수만큼의 가치를 가진다.
    [SerializeField] private float eliteHpMultiplier = 10f;
    // 보스는 플레이어가 도달하기 전 마지막으로 싸운 엘리트의 이 배수만큼의 가치를 가진다.
    [SerializeField] private float bossHpMultiplier = 3f;

    [Header("Attack curve")]
    // 공격력도 HP와 같은 서브스테이지 곡선을 따른다 - 한 메인 스테이지 안에서도 서브스테이지가
    // 오를수록 세진다. 엘리트는 그 값의 배수, 보스는 HP처럼 직전 엘리트 공격력의 배수다.
    // 일반 몹 공격력이 절반으로 낮아진 만큼(GetNormalAttack 참고) 엘리트가 예전과 같은 실제
    // 공격력을 유지하도록 이 배수를 2배로 올렸다 - 보스는 엘리트 값을 그대로 이어받으므로
    // bossAttackMultiplier는 손댈 필요가 없다.
    [SerializeField] private float eliteAttackMultiplier = 10f;
    [SerializeField] private float bossAttackMultiplier = 1.5f;

    // 서브스테이지별 HP/공격력 곡선에 대한 단일 진실 공급원(single source of truth)으로,
    // 엘리트/보스 배수는 항상 해당 스테이지의 일반 몬스터가 실제로 갖는 값을 기준으로 한다.
    // 메인 스테이지당 실제 일반 서브스테이지 개수(StageManager.BossSubStage - 1)만큼만 칸을
    // 예약해서, 클리어 순서 그대로 1씩 오른다 - 1-1=1, 1-2=2, 2-1=3, 2-2=4, 3-1=5, ...
    // (BossSubStage가 바뀌면 이 칸 수도 자동으로 같이 바뀐다.)
    private static float GetNormalValue(int mainStage, int subStage) => (mainStage - 1) * (StageManager.BossSubStage - 1) + subStage;

    public static float GetNormalHp(int mainStage, int subStage) => GetNormalValue(mainStage, subStage);

    // HP와 같은 곡선의 절반 - 1-1=0.5, 1-2=1, 2-1=1.5, 2-2=2. 엘리트/보스 공격력은 이 값을
    // 기준으로 배수를 곱하므로(위 주석 참고) 함께 절반으로 낮아진다.
    public static float GetNormalAttack(int mainStage, int subStage) => GetNormalValue(mainStage, subStage) * 0.5f;

    // 몬스터 스폰 함수
    public void Spawn(int mainStage, int subStage, MonsterType type)
    {
        float scale, hp, attack;

        switch (type)
        {
            case MonsterType.Elite:
                scale = eliteScale;
                hp = GetEliteHp(mainStage, subStage);
                attack = GetEliteAttack(mainStage, subStage);
                break;

            // 보스는 자기 곡선이 따로 없다 - 직전에 싸운 엘리트 값을 이어받아 배수만 곱한다.
            case MonsterType.Boss:
                scale = bossScale;
                hp = GetEliteHp(mainStage, subStage) * bossHpMultiplier;
                attack = GetEliteAttack(mainStage, subStage) * bossAttackMultiplier;
                break;

            case MonsterType.Normal:
            default:
                scale = 1f;
                hp = GetNormalHp(mainStage, subStage);
                attack = GetNormalAttack(mainStage, subStage);
                break;
        }

        MonsterDefinitionSO def = Resolve(mainStage, type);

        // 스폰
        Monster monster = SpawnOffscreen(def.prefab, scale);

        // 데이터 주입
        monster.Initialize(def.monsterType, hp, attack, stageConfig.normalMonsterAttackInterval);

        // 이동 시작
        monster.SetMovement(playerTransform, ApproachSpeed, stageConfig.meleeRange);

        GameEvents.RaiseMonsterSpawned(monster);
    }

    private float GetEliteHp(int mainStage, int subStage) => GetNormalHp(mainStage, subStage) * eliteHpMultiplier;
    private float GetEliteAttack(int mainStage, int subStage) => GetNormalAttack(mainStage, subStage) * eliteAttackMultiplier;

    private Monster SpawnOffscreen(GameObject prefab, float scaleMultiplier)
    {
        Vector3 spawnFrom = spawnPoint.position + new Vector3(stageConfig.offscreenSpawnDistance, 0f, 0f);
        GameObject instance = Instantiate(prefab, spawnFrom, spawnPoint.rotation);
        // 대입이 아니라 곱하기: 일부 프리팹(예: 최종 보스)은 이미 자체적인 기본 스케일을
        // 갖고 있으며, 이 값은 그것을 덮어쓰는 게 아니라 그 위에 누적되어야 한다.
        instance.transform.localScale *= scaleMultiplier;
        SnapToGround(instance);

        return instance.GetComponent<Monster>();
    }

    // spawnPoint.position.y는 스케일 1 캡슐이 바닥에 놓인 기준값이다; 더 큰 프리팹(보스, 최종 보스)은
    // 캡슐 밑면이 바닥에 파묻히지 않고 지면에 닿도록 자기 스케일에 비례해 피벗을 올려야 한다.
    private void SnapToGround(GameObject instance)
    {
        Vector3 pos = instance.transform.position;
        pos.y = spawnPoint.position.y * instance.transform.localScale.y;
        instance.transform.position = pos;
    }
}
