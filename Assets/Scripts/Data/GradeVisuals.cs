using UnityEngine;

public static class GradeVisuals
{
    // Normal->흰색, Rare->하늘색, Epic->보라색, Unique->노란색, Legendary->연두색.
    public static Color GetColor(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return Color.white;
            case StatGrade.Rare: return new Color(0.4f, 0.75f, 1f);
            case StatGrade.Epic: return new Color(0.65f, 0.3f, 0.95f);
            case StatGrade.Unique: return new Color(1f, 0.85f, 0.1f);
            default: return new Color(0.6f, 0.9f, 0.25f);
        }
    }

    public static Color GetPopupTextColor(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return Color.white;
            case StatGrade.Rare: return new Color(0.15f, 0.45f, 1f);
            case StatGrade.Epic: return new Color(0.58f, 0.18f, 1f);
            case StatGrade.Unique: return new Color(1f, 0.78f, 0f);
            default: return new Color(0.2f, 0.9f, 0.15f);
        }
    }

    public static float GetSizeScale(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 0.55f;
            case StatGrade.Rare: return 0.75f;
            case StatGrade.Epic: return 1f;
            case StatGrade.Unique: return 1.35f;
            default: return 1.8f;
        }
    }

    public static float GetAuraStrength(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 0.2f;
            case StatGrade.Rare: return 0.4f;
            case StatGrade.Epic: return 0.6f;
            case StatGrade.Unique: return 0.8f;
            default: return 1f;
        }
    }

    public static float GetPotionScale(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 1f;
            case StatGrade.Rare: return 1.15f;
            case StatGrade.Epic: return 1.35f;
            case StatGrade.Unique: return 1.6f;
            default: return 2f;
        }
    }
}
