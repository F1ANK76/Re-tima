using UnityEngine;

// Value ranges for the extra option a stone reroll can put on a piece of equipment.
//
// Unlike EquipmentGradeBonus (a fixed number per grade), an option rolls a RANGE inside its
// grade, so two Legendary rolls are not interchangeable and there is a reason to keep
// rerolling after the first good one. Only a roll that beats what the slot already carries is
// kept, which is what makes the spread matter instead of averaging out over time.
//
// Bands sit alongside EquipmentGradeBonus's own per-grade numbers rather than dwarfing them.
// Adjacent grades touch at their shared edge (Normal 1-2, Rare 2-3, ...) rather than
// overlapping, so grade is still the dominant factor in a roll's value - a lucky low grade
// can at best tie the next grade up, never clear beat it.
public static class EquipmentOptionTable
{
    // Red stone -> the sword's ATK option, matching the game's existing color language where
    // ATK drops as a RedVial and HP as a GreenVial.
    private static readonly Vector2[] AttackRanges =
    {
        new Vector2(1f, 2f),        // Normal
        new Vector2(2f, 3f),        // Rare
        new Vector2(3f, 5f),        // Epic
        new Vector2(5f, 7f),        // Unique
        new Vector2(7f, 10f),       // Legendary
    };

    // Green stone -> the shield's HP option.
    private static readonly Vector2[] HpRanges =
    {
        new Vector2(5f, 10f),       // Normal
        new Vector2(10f, 20f),      // Rare
        new Vector2(20f, 30f),      // Epic
        new Vector2(30f, 50f),      // Unique
        new Vector2(50f, 100f),     // Legendary
    };

    public static float Roll(StatType statType, StatGrade grade)
    {
        Vector2 range = GetRange(statType, grade);
        float value = Random.Range(range.x, range.y);

        // ATK is carried and displayed to one decimal (the player's own attack power is
        // fractional); HP is only ever a whole number, so rounding here stops a rolled option
        // showing as "+41.7 HP" against an otherwise integer stat.
        return statType == StatType.Attack ? Mathf.Round(value * 10f) / 10f : Mathf.Round(value);
    }

    public static Vector2 GetRange(StatType statType, StatGrade grade)
    {
        var table = statType == StatType.Attack ? AttackRanges : HpRanges;
        int i = Mathf.Clamp((int)grade, 0, table.Length - 1);
        return table[i];
    }

    public static string Format(StatType statType, float value) =>
        statType == StatType.Attack ? value.ToString("0.#") : value.ToString("0");
}
