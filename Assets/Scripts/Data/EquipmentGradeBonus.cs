// Fixed reward table for equipped gear - a sword or shield's grade decides its base bonus
// outright. On top of that, every pickup of that type (upgrade or not) feeds a separate
// uncapped mastery meter (see EquipmentDropManager) that converts to +1 ATK / +10 HP per full
// 100% crossed - this is what gives a same-or-lower-grade pickup a reason to exist. No stage
// scaling on any of this: a Legendary sword is always +20 ATK regardless of what stage it
// dropped on.
public static class EquipmentGradeBonus
{
    // Flat stat granted per mastery level, independent of grade.
    public const float SwordLevelAtkBonus = 1f;
    public const float ShieldLevelHpBonus = 10f;

    // Percent of the mastery meter a single pickup of this grade fills - deliberately not
    // proportional to the grade's own stat bonus (Legendary is 20x Normal's ATK but only 50x
    // its mastery gain), so grinding low grades still contributes meaningfully over time.
    public static float GetProgressPercent(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 1f;
            case StatGrade.Rare: return 3f;
            case StatGrade.Epic: return 10f;
            case StatGrade.Unique: return 30f;
            default: return 50f;
        }
    }


    public static float GetSwordAtk(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 1f;
            case StatGrade.Rare: return 3f;
            case StatGrade.Epic: return 5f;
            case StatGrade.Unique: return 10f;
            default: return 20f;
        }
    }

    public static float GetShieldHp(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 10f;
            case StatGrade.Rare: return 30f;
            case StatGrade.Epic: return 50f;
            case StatGrade.Unique: return 100f;
            default: return 200f;
        }
    }
}
