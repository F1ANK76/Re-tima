using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Stage 1 roster")]
    [SerializeField] private MonsterDefinitionSO monsterDefinition;
    [SerializeField] private MonsterDefinitionSO eliteDefinition;
    [SerializeField] private MonsterDefinitionSO bossDefinition;

    [Header("Stage 2+ roster")]
    // 각 메인 스테이지마다 고유한 몬스터 구성을 가진다: 위쪽의 보병들은 스테이지 1의 것이고,
    // 이것들은 스테이지 2부터 그 역할을 넘겨받는다. 슬롯을 비워두면 조용히 스테이지 1 항목으로
    // 대체되므로, 절반만 채워진 구성이어도 실행 중 오류를 내는 대신 무언가는 스폰된다.
    [SerializeField] private MonsterDefinitionSO stage2MonsterDefinition;
    [SerializeField] private MonsterDefinitionSO stage2EliteDefinition;
    [SerializeField] private MonsterDefinitionSO stage2BossDefinition;

    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform playerTransform;

    // 새 구성은 정확히 스테이지 2 전용이지, "스테이지 2 이후 전부"가 아니다 - 스테이지 3은
    // 세 번째 몬스터 구성을 새로 도입하는 대신 의도적으로 스테이지 1의 보병들을 다시
    // 불러오며, 단지 스테이지 3의 수치로 싸우게 할 뿐이다. 아래의 GetNormalHp/GetNormalAttack이
    // 이미 mainStage를 기준으로 값을 정하므로, 스테이지별 구성 작업 없이도 동일한 프리팹이
    // 스테이지 3의 HP와 공격력을 그대로 갖고 등장한다.
    private const int BirdRosterStage = 2;

    private static bool UsesBirdRoster(int mainStage) => mainStage == BirdRosterStage;

    private MonsterDefinitionSO ResolveNormal(int mainStage) =>
        UsesBirdRoster(mainStage) && stage2MonsterDefinition != null ? stage2MonsterDefinition : monsterDefinition;

    private MonsterDefinitionSO ResolveElite(int mainStage) =>
        UsesBirdRoster(mainStage) && stage2EliteDefinition != null ? stage2EliteDefinition : eliteDefinition;

    private MonsterDefinitionSO ResolveBoss(int mainStage) =>
        UsesBirdRoster(mainStage) && stage2BossDefinition != null ? stage2BossDefinition : bossDefinition;

    // 몬스터는 실제 교전 지점보다 더 오른쪽 - 화면 밖 - 에서 스폰되며, 제자리에 갑자기
    // 나타나거나 순간이동하지 않고 평소 이동 속도/애니메이션 그대로 걸어 들어온다.
    [SerializeField] private float offscreenSpawnDistance = 8f;

    [SerializeField] private float eliteScale = 1.5f;
    [SerializeField] private float bossScale = 2f;

    [Header("HP curve")]
    // 엘리트 한 마리는 같은 서브스테이지의 일반 몬스터 이 개수만큼의 가치를 가진다.
    [SerializeField] private float eliteHpMultiplier = 10f;
    // 보스는 플레이어가 도달하기 전 마지막으로 싸운 엘리트의 이 배수만큼의 가치를 가진다.
    [SerializeField] private float bossHpMultiplier = 3f;

    [Header("Attack curve")]
    // 공격력은 메인 스테이지에만 의존하므로, 한 스테이지 안의 모든 서브스테이지는
    // 동일한 세기로 공격한다: 엘리트와 보스는 그 스테이지의 일반 몬스터 공격력에
    // 배수를 곱한 값일 뿐이다.
    [SerializeField] private float eliteAttackMultiplier = 2f;
    [SerializeField] private float bossAttackMultiplier = 5f;

    // 서브스테이지별 HP 곡선에 대한 단일 진실 공급원(single source of truth)으로,
    // 엘리트/보스 배수는 항상 해당 스테이지의 일반 몬스터가 실제로 갖는 값을 기준으로 한다.
    public static float GetNormalHp(int mainStage, int subStage) => (mainStage - 1) * 10 + subStage;

    // 공격력도 마찬가지다: 스테이지 1의 일반 몬스터는 1의 피해를, 스테이지 2는 2를 주는 식이다.
    public static float GetNormalAttack(int mainStage) => mainStage;

    public Monster SpawnNormal(int mainStage, int subStage)
    {
        MonsterDefinitionSO def = ResolveNormal(mainStage);
        if (def == null || def.prefab == null)
        {
            Debug.LogError("MonsterSpawner: normal monster definition or its prefab is not assigned.");
            return null;
        }

        Monster monster = SpawnOffscreen(def.prefab, 1f);

        monster.Initialize(def.monsterType, GetNormalHp(mainStage, subStage), GetNormalAttack(mainStage), stageConfig.normalMonsterAttackInterval);
        monster.SetMovement(playerTransform, stageConfig.MonsterApproachSpeed, stageConfig.meleeRange);

        GameEvents.RaiseMonsterSpawned(monster);
        return monster;
    }

    public Monster SpawnBoss(int mainStage, int lastEliteSubStage)
    {
        MonsterDefinitionSO def = ResolveBoss(mainStage);
        if (def == null || def.prefab == null)
        {
            Debug.LogError("MonsterSpawner: boss definition or its prefab is not assigned.");
            return null;
        }

        Monster monster = SpawnOffscreen(def.prefab, bossScale);

        float hp = GetEliteHp(mainStage, lastEliteSubStage) * bossHpMultiplier;
        monster.Initialize(def.monsterType, hp, GetNormalAttack(mainStage) * bossAttackMultiplier);
        monster.SetMovement(playerTransform, stageConfig.MonsterApproachSpeed, stageConfig.meleeRange);

        GameEvents.RaiseMonsterSpawned(monster);
        return monster;
    }

    public Monster SpawnElite(int mainStage, int subStage)
    {
        MonsterDefinitionSO def = ResolveElite(mainStage);
        if (def == null || def.prefab == null)
        {
            Debug.LogError("MonsterSpawner: elite definition or its prefab is not assigned.");
            return null;
        }

        Monster monster = SpawnOffscreen(def.prefab, eliteScale);

        float hp = GetEliteHp(mainStage, subStage);
        monster.Initialize(def.monsterType, hp, GetNormalAttack(mainStage) * eliteAttackMultiplier, stageConfig.normalMonsterAttackInterval);
        monster.SetMovement(playerTransform, stageConfig.MonsterApproachSpeed, stageConfig.meleeRange);

        GameEvents.RaiseMonsterSpawned(monster);
        return monster;
    }

    private float GetEliteHp(int mainStage, int subStage) => GetNormalHp(mainStage, subStage) * eliteHpMultiplier;

    private Monster SpawnOffscreen(GameObject prefab, float scaleMultiplier)
    {
        Vector3 spawnFrom = spawnPoint.position + new Vector3(offscreenSpawnDistance, 0f, 0f);
        GameObject instance = Instantiate(prefab, spawnFrom, spawnPoint.rotation);
        // 대입이 아니라 곱하기: 일부 프리팹(예: 최종 보스)은 이미 자체적인 기본 스케일을
        // 갖고 있으며, 이 값은 그것을 덮어쓰는 게 아니라 그 위에 누적되어야 한다.
        instance.transform.localScale *= scaleMultiplier;
        SnapToGround(instance);

        Monster monster = instance.GetComponent<Monster>();
        if (monster == null)
        {
            monster = instance.AddComponent<Monster>();
        }

        return monster;
    }

    // spawnPoint.position.y는 스케일 1인 캡슐이 바닥에 놓였을 때를 기준으로 보정되어
    // 있다; 더 큰 프리팹(보스, 최종 보스)은 캡슐의 바닥이 바닥 속으로 파묻히지 않고
    // 정확히 지면에 닿도록 자기 스케일에 비례해서 피벗을 올려줘야 한다.
    private void SnapToGround(GameObject instance)
    {
        Vector3 pos = instance.transform.position;
        pos.y = spawnPoint.position.y * instance.transform.localScale.y;
        instance.transform.position = pos;
    }
}
