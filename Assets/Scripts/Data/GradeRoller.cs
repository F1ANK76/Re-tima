using UnityEngine;

// Normal/Rare/Epic/Unique/Legendary 등급으로 드롭되는 모든 것이 공유하는 가중치 등급 롤. 스탯 드롭과
// 장비 조각 모두 스테이지 1부터 이 확률을 그대로 쓰며 스테이지 제한이 없다(이전 버전은 스테이지 2/3
// 미만에서 Unique/Legendary를 Normal로 합쳤으나, 그 제한은 의도적으로 제거했다).
public static class GradeRoller
{
    public static StatGrade Roll()
    {
        const float normal = 55f;
        const float rare = 30f;
        const float epic = 10f;
        const float unique = 4f;
        // legendary = 1f, 아래에서 암묵적으로 폴백 처리된다.

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

    // 스톤 리롤 전용의 더 엄격한 두 번째 테이블(ManagementWindowView의 Stone 탭 참고). 위 Roll()을
    // 재사용하지 않고 별도 확률을 둔 이유는, 리롤 범위를 조이는 것이 우연히 같은 다섯 등급 구조를
    // 공유하는 스탯 포션이나 장비 드롭까지 함께 조여버리지 않게 하기 위함이다.
    public static StatGrade RollStoneOption()
    {
        const float normal = 70f;
        const float rare = 25f;
        const float epic = 3f;
        const float unique = 1.5f;
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
