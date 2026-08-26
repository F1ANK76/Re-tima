using UnityEngine;

// Shared grade -> color ramp for anything that needs to read a drop's rarity visually
// (stat drop popup text, the potion pickup's own tint/glow). Single source of truth so the
// two never drift apart into showing different colors for the same grade.
public static class GradeVisuals
{
    // Normal->white, Rare->sky blue, Epic->purple, Unique->yellow, Legendary->light green.
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

    // Fill color for the floating pickup popups (see PopupText), as opposed to the softer
    // ramp above that tints auras and panel borders. Same five-color identity, pushed to full
    // saturation: the pale ramp reads as washed-out grey-ish text once it's a small glyph over
    // live gameplay rather than a glow around an object.
    //
    // Saturated rather than merely dark. Darkening these far enough to survive a bright sky
    // pushes blue/purple/green toward the same muddy near-black and the grades stop being
    // tellable apart - contrast is PopupText's black outline's job, so these only have to
    // carry hue.
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

    // Multiplier against a caller's own "Epic" baseline size - Epic sits in the middle of
    // the five grades, so it's the pivot the ramp scales up/down from rather than an
    // endpoint. A caller applies this to whatever size it already uses for Epic.
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

    // How strongly a grade should announce itself, normalized 0..1 across the five grades.
    // Callers multiply their own tuned maximums by this (aura brightness, light intensity,
    // emissive strength), so "subtle at Normal, unmistakable at Legendary" is one ramp here
    // rather than five hand-set numbers per effect. Never returns 0 - even a Normal drop
    // carries a faint aura, it just has to read as the floor of the scale.
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

    // Multiplier against a caller's own "Normal" baseline size - unlike GetSizeScale above
    // (which pivots on Epic, the middle grade), a dropped object's size reads best as an
    // endpoint ramp: Normal is whatever size the caller already authored, and everything
    // rarer is visibly bigger than that, not scaled down from something above it.
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
