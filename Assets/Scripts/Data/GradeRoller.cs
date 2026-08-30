using UnityEngine;

// Normal/Rare/Epic/Unique/Legendary 등급으로 드롭되는 모든 것이 공유하는 가중치 등급 롤. 스탯 드롭과
// 장비 조각 모두 스테이지 1부터 이 확률을 그대로 쓰며 스테이지 제한이 없다(이전 버전은 스테이지 2/3
// 미만에서 Unique/Legendary를 Normal로 합쳤으나, 그 제한은 의도적으로 제거했다).
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
