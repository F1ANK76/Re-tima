using UnityEngine;

// Shared weighted grade roll for anything that drops in Normal/Rare/Epic/Unique/Legendary
// tiers. Both stat drops and equipment fragments use these exact odds, unlocked from stage 1
// onward - there is no stage-gating here (an earlier version folded Unique/Legendary into
// Normal below stage 2/3; that restriction was intentionally removed).
public static class GradeRoller
{
    public static StatGrade Roll()
    {
        const float normal = 55f;
        const float rare = 30f;
        const float epic = 10f;
        const float unique = 4f;
        // legendary = 1f, implicit fallback below.

        float roll = Random.value * 100f;

        if (roll < normal) return StatGrade.Normal;
        roll -= normal;

        if (roll < rare) return StatGrade.Rare;
        roll -= rare;

        if (roll < epic) return StatGrade.Epic;
        roll -= epic;

        if (roll < unique) return StatGrade.Unique;

        return StatGrade.Legendary;
    }

    // A second, stricter table for stone rerolls specifically (see ManagementWindowView's
    // Stone tab) - deliberately its own odds rather than reusing Roll() above, so tightening
    // what a reroll can hit doesn't quietly also tighten stat potion or equipment drops that
    // happen to share the same five-grade shape.
    public static StatGrade RollStoneOption()
    {
        const float normal = 70f;
        const float rare = 25f;
        const float epic = 3f;
        const float unique = 1.5f;
        // legendary = 0.5f, implicit fallback below.

        float roll = Random.value * 100f;

        if (roll < normal) return StatGrade.Normal;
        roll -= normal;

        if (roll < rare) return StatGrade.Rare;
        roll -= rare;

        if (roll < epic) return StatGrade.Epic;
        roll -= epic;

        if (roll < unique) return StatGrade.Unique;

        return StatGrade.Legendary;
    }
}
