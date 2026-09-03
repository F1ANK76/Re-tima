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

    [SerializeField] private CombatConfigSO combatConfig;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform playerTransform;

    // 몬스터·드롭 공용 이동 속도. 실제 거리 / 목표 시간으로 역산.
    public static float ApproachSpeed { get; private set; }

    private void Awake()
    {
        ApproachSpeed = ComputeApproachSpeed();
    }

    private float ComputeApproachSpeed()
    {
        Vector3 spawnFrom = spawnPoint.position + new Vector3(combatConfig.offscreenSpawnDistance, 0f, 0f);
        Vector3 toPlayer = playerTransform.position - spawnFrom;
        toPlayer.y = 0f;

        // 사거리만큼은 안 걸어도 되니 제외
        float travelDistance = Mathf.Max(0.01f, toPlayer.magnitude - combatConfig.meleeRange);
        return combatConfig.monsterApproachDuration > 0f ? travelDistance / combatConfig.monsterApproachDuration : 0f;
    }

    // 스테이지 → 로스터 → 종류별 슬롯
    private MonsterDefinitionSO Resolve(int mainStage, MonsterType type)
    {
        int index = mainStage - 1;
        StageRoster roster;

        if (index >= 0 && index < rosters.Count && rosters[index] != null)
        {
            // 전용 로스터
            roster = rosters[index];
        }
        else
        {
            // 없으면 첫 항목으로 폴백
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

    // 클리어 순서대로 1씩. 1-1=1, 1-2=2, 2-1=3, 2-2=4, ...
    private static float GetNormalValue(int mainStage, int subStage) => (mainStage - 1) * (StageManager.BossSubStage - 1) + subStage;

    public static float GetNormalHp(int mainStage, int subStage) => GetNormalValue(mainStage, subStage);

    // HP 곡선의 절반
    public static float GetNormalAttack(int mainStage, int subStage) => GetNormalValue(mainStage, subStage) * 0.5f;

    // 정의 조회 → 스탯 계산 → 화면 밖 생성 → 스탯 주입 → 이동 시작 → 이벤트 발생
    public void Spawn(int mainStage, int subStage, MonsterType type)
    {
        MonsterDefinitionSO def = Resolve(mainStage, type);

        float hp = GetNormalHp(mainStage, subStage) * def.hpMultiplier;
        float attack = GetNormalAttack(mainStage, subStage) * def.attackMultiplier;

        Monster monster = SpawnOffscreen(def.prefab, def.scale);
        monster.Initialize(type, hp, attack, combatConfig.normalMonsterAttackInterval);
        monster.SetMovement(playerTransform, ApproachSpeed, combatConfig.meleeRange);

        GameEvents.RaiseMonsterSpawned(monster);
    }

    private Monster SpawnOffscreen(GameObject prefab, float scaleMultiplier)
    {
        Vector3 spawnFrom = spawnPoint.position + new Vector3(combatConfig.offscreenSpawnDistance, 0f, 0f);
        GameObject instance = Instantiate(prefab, spawnFrom, spawnPoint.rotation);
        // 대입이 아니라 곱하기 - 프리팹 자체 스케일 위에 누적
        instance.transform.localScale *= scaleMultiplier;
        SnapToGround(instance);

        return instance.GetComponent<Monster>();
    }

    // 피벗이 몸 중앙이라, 키운 만큼 높이도 올려야 발이 땅에 닿는다
    private void SnapToGround(GameObject instance)
    {
        Vector3 pos = instance.transform.position;
        pos.y = spawnPoint.position.y * instance.transform.localScale.y;
        instance.transform.position = pos;
    }
}
