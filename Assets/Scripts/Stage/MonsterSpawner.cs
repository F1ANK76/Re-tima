using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Stage 1 roster")]
    [SerializeField] private MonsterDefinitionSO monsterDefinition;
    [SerializeField] private MonsterDefinitionSO eliteDefinition;
    [SerializeField] private MonsterDefinitionSO bossDefinition;

    [Header("Stage 2+ roster")]
    // Each main stage gets its own cast: the footmen above are stage 1's, these take over
    // from stage 2 on. Left empty, a slot silently falls back to the stage 1 entry, so a
    // half-populated roster still spawns something instead of erroring out mid-run.
    [SerializeField] private MonsterDefinitionSO stage2MonsterDefinition;
    [SerializeField] private MonsterDefinitionSO stage2EliteDefinition;
    [SerializeField] private MonsterDefinitionSO stage2BossDefinition;

    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform playerTransform;

    // The bird cast is stage 2's specifically, NOT "stage 2 and everything after" - stage 3
    // deliberately brings the stage 1 footmen back rather than introducing a third roster,
    // and simply fights them at stage 3's numbers. GetNormalHp/GetNormalAttack below already
    // key off mainStage, so the same prefabs arrive with stage 3 HP and damage without any
    // per-stage roster work.
    private const int BirdRosterStage = 2;

    private static bool UsesBirdRoster(int mainStage) => mainStage == BirdRosterStage;

    private MonsterDefinitionSO ResolveNormal(int mainStage) =>
        UsesBirdRoster(mainStage) && stage2MonsterDefinition != null ? stage2MonsterDefinition : monsterDefinition;

    private MonsterDefinitionSO ResolveElite(int mainStage) =>
        UsesBirdRoster(mainStage) && stage2EliteDefinition != null ? stage2EliteDefinition : eliteDefinition;

    private MonsterDefinitionSO ResolveBoss(int mainStage) =>
        UsesBirdRoster(mainStage) && stage2BossDefinition != null ? stage2BossDefinition : bossDefinition;

    // Monsters spawn further right than the real engage point - off-screen - and simply
    // walk in the whole way using their normal move speed/animation, instead of popping
    // into place or snapping into position.
    [SerializeField] private float offscreenSpawnDistance = 8f;

    [SerializeField] private float eliteScale = 1.5f;
    [SerializeField] private float bossScale = 2f;

    [Header("HP curve")]
    // An elite is worth this many of the normal monsters standing in the same substage.
    [SerializeField] private float eliteHpMultiplier = 10f;
    // The boss is worth this many of the last elite the player fought before reaching it.
    [SerializeField] private float bossHpMultiplier = 3f;

    [Header("Attack curve")]
    // Attack keys off the main stage only, so every substage inside a stage hits equally
    // hard: elite and boss are just multiples of what the normal monsters there deal.
    [SerializeField] private float eliteAttackMultiplier = 2f;
    [SerializeField] private float bossAttackMultiplier = 5f;

    // Single source of truth for the per-substage HP curve, so the elite/boss multipliers
    // are always relative to what normal monsters in that stage actually have.
    public static float GetNormalHp(int mainStage, int subStage) => (mainStage - 1) * 10 + subStage;

    // Likewise for attack: stage 1 normals hit for 1, stage 2 for 2, and so on.
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
        // Multiply, don't replace: some prefabs (e.g. the final boss) already bake in
        // their own base scale, and this must stack on top of that, not override it.
        instance.transform.localScale *= scaleMultiplier;
        SnapToGround(instance);

        Monster monster = instance.GetComponent<Monster>();
        if (monster == null)
        {
            monster = instance.AddComponent<Monster>();
        }

        return monster;
    }

    // spawnPoint.position.y is calibrated for a scale-1 capsule resting on the ground;
    // bigger prefabs (boss, final boss) need their pivot raised proportionally to their
    // own scale so the capsule's bottom still lands exactly on the ground instead of sinking in.
    private void SnapToGround(GameObject instance)
    {
        Vector3 pos = instance.transform.position;
        pos.y = spawnPoint.position.y * instance.transform.localScale.y;
        instance.transform.position = pos;
    }
}
