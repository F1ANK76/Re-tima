using UnityEngine;

public static class GradeRoller
{
    public static StatGrade Roll()
    {
        const float normal = 60f;
        const float rare = 30f;
        const float epic = 7f;
        const float unique = 2.5f;
        // legendary = 0.5f, 아래에서 암묵적으로 폴백 처리된다.

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
