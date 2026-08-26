using UnityEngine;

// Sits alongside StatDropManager as a second, independent drop roll off the same kill event:
// where StatDropManager hands out raw ATK/HP boosts, this hands out an actual Sword/Shield.
// Two things happen on every pickup of a type, independently:
//   1. Auto-equip - only if the pickup's grade beats what's already equipped.
//   2. Mastery progress - always, regardless of (1). A same-or-lower-grade pickup still fills
//      part of an uncapped meter that converts to +1 ATK / +10 HP per 100% crossed, which is
//      what gives a "useless" duplicate a reason to exist.
public class EquipmentDropManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private EquipmentDropPickup pickupPrefab;
    [SerializeField] private CombatLoop combatLoop;

    private int currentMainStage = 1;

    // Null means nothing of that type is equipped yet - distinct from "equipped at Normal",
    // which already has a real (if small) bonus.
    private StatGrade? equippedSwordGrade;
    private StatGrade? equippedShieldGrade;

    // Uncapped mastery meters, in percent - deliberately never wrapped or clamped, same
    // reasoning as the old fragment system: 350% is a legitimate late-run reading.
    private float swordMasteryPercent;
    private float shieldMasteryPercent;
    private int swordLevel;
    private int shieldLevel;

    // The extra option a stone reroll socketed into each slot, plus the grade that produced
    // it (kept only so the UI can color the value by what rolled it). Replaces rather than
    // accumulates: a slot always holds the single best roll it has ever seen.
    private float swordAtkOption;
    private float shieldHpOption;
    private StatGrade? swordAtkOptionGrade;
    private StatGrade? shieldHpOptionGrade;


    public StatGrade? EquippedSwordGrade => equippedSwordGrade;
    public StatGrade? EquippedShieldGrade => equippedShieldGrade;
    public int SwordLevel => swordLevel;
    public int ShieldLevel => shieldLevel;
    // The meter's position within the level currently in progress (0-100), for a gauge to
    // draw - as opposed to the uncapped lifetime total the level count is derived from.
    public float SwordMasteryProgressPercent => swordMasteryPercent % 100f;
    public float ShieldMasteryProgressPercent => shieldMasteryPercent % 100f;
    public float SwordAtkOption => swordAtkOption;
    public float ShieldHpOption => shieldHpOption;
    public StatGrade? SwordAtkOptionGrade => swordAtkOptionGrade;
    public StatGrade? ShieldHpOptionGrade => shieldHpOptionGrade;
    public float GetOption(StatType statType) => statType == StatType.Attack ? swordAtkOption : shieldHpOption;
    public StatGrade? GetOptionGrade(StatType statType) => statType == StatType.Attack ? swordAtkOptionGrade : shieldHpOptionGrade;

    // Grade + mastery only, with no stone option folded in - this is what the Equip tab shows,
    // so "equipment" there reads as exactly the sword/shield itself. The Stone tab is the only
    // place the option is displayed (see ManagementWindowView.OptionLine).
    public float SwordEquipmentBonus => GradeBonus(equippedSwordGrade, EquipmentGradeBonus.GetSwordAtk) + swordLevel * EquipmentGradeBonus.SwordLevelAtkBonus;
    public float ShieldEquipmentBonus => GradeBonus(equippedShieldGrade, EquipmentGradeBonus.GetShieldHp) + shieldLevel * EquipmentGradeBonus.ShieldLevelHpBonus;

    // The TRUE total actually applied to the player's stats - equipment bonus plus the stone
    // option. Used for the old-vs-new delta math below and in TryApplyOption, never for
    // display: the option still has to affect real gameplay even though it's hidden from this
    // tab's readout.
    public float SwordAtkBonus => SwordEquipmentBonus + swordAtkOption;
    public float ShieldHpBonus => ShieldEquipmentBonus + shieldHpOption;

    private static float GradeBonus(StatGrade? grade, System.Func<StatGrade, float> lookup) => grade.HasValue ? lookup(grade.Value) : 0f;

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

    private const int UnlockStage = 2;
    // Equipment is stage 2's drop and stage 2's only - stage 3 hands the grind over to
    // StoneDropManager the same way stage 1 handed it to this class (see StatDropManager's
    // own StatDropMaxStage, which is the identical cap one stage earlier). Without this the
    // "one drop type per stage" progression silently broke at stage 3, where equipment kept
    // dropping alongside the crystals.
    private const int EquipDropMaxStage = 2;

    private void HandleMonsterDied(Monster monster)
    {
        // Elite/Boss are the gate that closes out a (sub)stage, not a source of loot - only
        // the normal-monster grind in between them drops anything.
        if (monster.Type != MonsterType.Normal) return;

        // No drops at all until stage 2 - stage 1 is the stat-farming stage. From stage 2 on,
        // the base chance applies at that stage and climbs from there (stage 2 = 30%, stage 3
        // = 35%, ...).
        if (currentMainStage < UnlockStage) return;
        if (currentMainStage > EquipDropMaxStage) return;

        float dropChance = Mathf.Clamp01(stageConfig.equipDropBaseChance + stageConfig.equipDropChancePerStage * (currentMainStage - UnlockStage));
        if (Random.value > dropChance) return;

        EquipmentType equipType = Random.value < 0.5f ? EquipmentType.Sword : EquipmentType.Shield;
        StatGrade grade = GradeRoller.Roll();

        // No prefab/player to drop onto: still resolve the roll (a real upgrade still equips
        // and pays out) rather than silently losing it because the visual isn't wired up yet.
        if (pickupPrefab == null || player == null)
        {
            CompleteDrop(equipType, grade);
            return;
        }

        EquipmentDropPickup pickup = Instantiate(pickupPrefab, monster.transform.position, Quaternion.identity);
        // Same speed monsters walk in at, matching StatPotionPickup's approach so both drop
        // types read as the same forward travel.
        pickup.Initialize(equipType, grade, player.transform, combatLoop, stageConfig.monsterMoveSpeed, this);
    }

    // Called by EquipmentDropPickup once the player actually reaches it. Every pickup fills
    // its type's mastery meter regardless of grade; only a grade that beats what's equipped
    // additionally swaps the equipped item. Both contributions are applied as a single delta
    // to the player's stat, so a pickup that does neither (a duplicate below a level-up
    // threshold) costs nothing and one that does both isn't double-counted.
    public void CompleteDrop(EquipmentType equipType, StatGrade grade)
    {
        if (equipType == EquipmentType.Sword)
        {
            float oldBonus = SwordAtkBonus;

            swordMasteryPercent += EquipmentGradeBonus.GetProgressPercent(grade);
            swordLevel = Mathf.FloorToInt(swordMasteryPercent / 100f);

            if (!equippedSwordGrade.HasValue || grade > equippedSwordGrade.Value) equippedSwordGrade = grade;

            if (player != null) player.IncreaseAttack(SwordAtkBonus - oldBonus);
        }
        else
        {
            float oldBonus = ShieldHpBonus;

            shieldMasteryPercent += EquipmentGradeBonus.GetProgressPercent(grade);
            shieldLevel = Mathf.FloorToInt(shieldMasteryPercent / 100f);

            if (!equippedShieldGrade.HasValue || grade > equippedShieldGrade.Value) equippedShieldGrade = grade;

            if (player != null) player.IncreaseMaxHp(ShieldHpBonus - oldBonus);
        }

        // Every pickup is worth announcing now - even one that doesn't change what's equipped
        // still moved the mastery meter.
        GameEvents.RaiseEquipmentPickedUp(equipType, grade);
    }


    // Commits a rolled option, but only if it beats what the slot already carries. Strictly
    // greater-than, and enforced here rather than left to the UI - "a slot keeps its best
    // option" is a rule about the data, so it lives with the data.
    public bool TryApplyOption(StatType statType, StatGrade grade, float value)
    {
        if (statType == StatType.Attack)
        {
            if (value <= swordAtkOption) return false;

            // Captured before the write and applied as a delta - the same shape CompleteDrop
            // uses, so the player's stat tracks the option without this needing to know what
            // the rest of the bonus is made of.
            float oldBonus = SwordAtkBonus;
            swordAtkOption = value;
            swordAtkOptionGrade = grade;
            if (player != null) player.IncreaseAttack(SwordAtkBonus - oldBonus);
        }
        else
        {
            if (value <= shieldHpOption) return false;

            float oldBonus = ShieldHpBonus;
            shieldHpOption = value;
            shieldHpOptionGrade = grade;
            if (player != null) player.IncreaseMaxHp(ShieldHpBonus - oldBonus);
        }

        GameEvents.RaiseEquipmentPickedUp(
            statType == StatType.Attack ? EquipmentType.Sword : EquipmentType.Shield,
            equippedSwordGrade ?? StatGrade.Normal);
        return true;
    }
}
