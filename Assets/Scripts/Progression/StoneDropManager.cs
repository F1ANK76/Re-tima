using System.Collections;
using UnityEngine;

// Stage 3's drop roll, the third in the sequence each main stage hands the grind over to:
// stage 1 drops stat potions (StatDropManager), stage 2 drops equipment
// (EquipmentDropManager), and stage 3 drops crystals. Only one of the three is ever live at a
// time, which is what keeps a later stage from burying the player in three drop types at once.
//
// Stones currently only accumulate - picking one up adds to a running count and nothing else.
// Whatever eventually spends them can read the counts off this class; the drop, the pickup
// motion and the tally are deliberately finished and self-contained without it.
public class StoneDropManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private StoneDropPickup stonePrefab;
    [SerializeField] private CombatLoop combatLoop;

    // Flat, deliberately not stage-scaled like the other two managers' curves: stage 3 is
    // currently the last authored stage, so there is no "later stage" for a climbing rate to
    // ramp into.
    [Range(0f, 1f)][SerializeField] private float stoneDropChance = 0.5f;
    // Even split between the two crystals. Red/green follows the color language stage 1
    // already set with its RedVial/GreenVial potions - red is the ATK-side stone, green the
    // HP-side one.
    [Range(0f, 1f)][SerializeField] private float attackStoneChance = 0.5f;

    public const int UnlockStage = 3;

    private int currentMainStage = 1;

    // One running tally per crystal color. Kept here rather than on the equipment manager
    // because nothing about a stone touches equipment yet - this is the stone system's own
    // state, and the only thing that currently happens to a collected stone.
    // Starts pre-stocked rather than at zero, so the Stone tab has something to reroll with
    // immediately instead of waiting on stage 3's drop rate.
    private int attackStones = 100;
    private int hpStones = 100;

    public int AttackStones => attackStones;
    public int HpStones => hpStones;
    public int GetStones(StatType statType) => statType == StatType.Attack ? attackStones : hpStones;

    private void OnEnable()
    {
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
    }

    private void HandleMonsterDied(Monster monster)
    {
        // Elite/Boss close out a substage rather than feed the grind - same rule the other
        // two drop managers follow, so only normal kills ever drop anything.
        if (monster.Type != MonsterType.Normal) return;

        if (currentMainStage < UnlockStage) return;

        if (Random.value > stoneDropChance) return;

        // The only thing a drop rolls is which of the two stones it is - stones have no
        // grades, so every red is identical to every other red.
        StatType statType = Random.value < attackStoneChance ? StatType.Attack : StatType.Hp;

        // No prefab/player to drop onto: still bank the stones so the drop is not silently
        // lost because the visual isn't wired up yet - the same fallback the sibling managers
        // use for their own missing-prefab case.
        int count = RollStoneCount();
        if (stonePrefab == null || player == null)
        {
            for (int i = 0; i < count; i++) AddStone(statType);
            return;
        }

        StartCoroutine(SpawnStoneStream(monster.transform.position, statType, count));
    }

    // 60/30/10 - a kill only occasionally spills more than one stone, so a 2-3 streak still
    // reads as a notably good kill rather than the norm.
    private static int RollStoneCount()
    {
        float roll = Random.value;
        if (roll < 0.6f) return 1;
        if (roll < 0.9f) return 2;
        return 3;
    }

    // Every stone in the burst shares the statType this kill rolled. Staggered rather than
    // spawned all at once, and each landing a little further back than the last, so a
    // multi-stone kill reads as a short trail streaming out behind the monster instead of a
    // single stack of overlapping crystals.
    private IEnumerator SpawnStoneStream(Vector3 position, StatType statType, int count)
    {
        const float SpawnStagger = 0.12f;
        const float TrailSpacing = 0.55f;

        for (int i = 0; i < count; i++)
        {
            // A death mid-stream (StageManager's own respawn resets the player before this
            // finishes) shouldn't keep raining stones onto a checkpoint the player hasn't
            // even reached yet.
            if (player == null || player.Stats.IsDead) yield break;

            StoneDropPickup stone = Instantiate(stonePrefab, position, Quaternion.identity);
            // Same pace monsters walk in at, matching both sibling pickups so every drop type
            // reads as the same forward travel.
            stone.Initialize(statType, player.transform, combatLoop,
                stageConfig != null ? stageConfig.monsterMoveSpeed : 0f, this, i * TrailSpacing);

            if (i < count - 1) yield return new WaitForSeconds(SpawnStagger);
        }
    }

    // Spends one stone of that type. Returns false when there is nothing to spend, so the
    // caller can't roll for free.
    public bool TryConsumeStone(StatType statType)
    {
        if (statType == StatType.Attack)
        {
            if (attackStones <= 0) return false;
            attackStones--;
        }
        else
        {
            if (hpStones <= 0) return false;
            hpStones--;
        }

        GameEvents.RaiseStonesChanged(statType, GetStones(statType), -1);
        return true;
    }

    // Called by StoneDropPickup once the player actually runs over the crystal.
    public void AddStone(StatType statType)
    {
        if (statType == StatType.Attack) attackStones++;
        else hpStones++;

        GameEvents.RaiseStonesChanged(statType, GetStones(statType), 1);
    }
}
