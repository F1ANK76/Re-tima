using UnityEngine;

[CreateAssetMenu(fileName = "StageConfig", menuName = "UltimaSquad/Stage Config")]
public class StageConfigSO : ScriptableObject
{
    [Header("Combat Timing")]
    public float tickInterval = 0.5f;
    public float normalMonsterAttackInterval = 0.4f;

    [Header("Monster Scaling")]
    public float hpGrowthPerStage = 0.15f;
    public float atkGrowthPerStage = 0.10f;

    [Header("Stat Drop Progression")]
    // Chance a kill drops a random stat boost at all. Climbs with the main stage (not the
    // sub-stage): stage 1 = 50%, stage 2 = 60%, stage 3 = 70%, ...
    [Range(0f, 1f)] public float statDropBaseChance = 0.5f;
    [Range(0f, 1f)] public float statDropChancePerStage = 0.1f;

    [Header("Stat Drop Amounts - ATK")]
    public float atkAmountNormal = 0.1f;
    public float atkAmountRare = 0.3f;
    public float atkAmountEpic = 0.5f;
    public float atkAmountUnique = 1f;
    public float atkAmountLegendary = 2f;

    [Header("Stat Drop Amounts - HP")]
    public float hpAmountNormal = 1f;
    public float hpAmountRare = 3f;
    public float hpAmountEpic = 5f;
    public float hpAmountUnique = 10f;
    public float hpAmountLegendary = 20f;


    [Header("Equipment Drop Progression")]
    // Chance a kill drops a sword/shield at all - independent roll from the stat drop above.
    // Climbs with the main stage: stage 1 = 30%, stage 2 = 35%, ... The grade's stat bonus
    // itself is a fixed table (see EquipmentGradeBonus), not stage-scaled.
    [Range(0f, 1f)] public float equipDropBaseChance = 0.3f;
    [Range(0f, 1f)] public float equipDropChancePerStage = 0.05f;

    [Header("Monster Movement")]
    // The world's walking pace, and deliberately shared: stat potions and equipment pickups
    // home in at this speed too, and GroundScroller/BackdropScroller have their scroll speeds
    // derived from it so the ground travels at exactly the rate the player appears to walk.
    // Changing this rescales all of that together - which is the point, but it means it is
    // NOT the knob for "monsters should arrive sooner".
    public float monsterMoveSpeed = 2f;
    // Monster walk-in speed only, as a multiple of the pace above. Separate because monster
    // approach is dead time (the player just waits for the fight to start) while the pickup
    // suction and the ground scroll are the "walking forward" illusion - speeding the monsters
    // up should not make potions snap in twice as fast or desync the ground from the walk.
    public float monsterApproachMultiplier = 2f;
    public float meleeRange = 1.3f;

    // What MonsterSpawner actually hands to a monster. Kept as a property so the two fields
    // can never drift apart at the three separate spawn call sites.
    public float MonsterApproachSpeed => monsterMoveSpeed * monsterApproachMultiplier;

}
