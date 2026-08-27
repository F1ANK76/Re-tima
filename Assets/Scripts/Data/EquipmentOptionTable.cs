using UnityEngine;

// 스톤 리롤이 장비에 부여할 수 있는 추가 옵션의 값 범위.
//
// 등급당 고정 수치인 EquipmentGradeBonus와 달리, 옵션은 해당 등급 내에서 하나의 범위(RANGE)를
// 굴린다. 그래서 Legendary 두 개를 굴려도 서로 같지 않으며, 좋은 값 하나를 얻은 뒤에도 계속
// 리롤할 이유가 생긴다. 슬롯이 이미 가진 값을 능가하는 롤만 유지되므로, 시간이 지나 평균으로
// 수렴하는 게 아니라 이 편차 자체가 의미를 갖게 된다.
//
// 각 구간은 EquipmentGradeBonus 자체의 등급별 수치를 압도하지 않도록 그 옆에 나란히 위치한다.
// 인접한 등급끼리는 겹치지 않고 경계에서 맞닿으며(Normal 1-2, Rare 2-3, ...), 그래서 롤의
// 가치를 결정하는 지배적 요인은 여전히 등급이다 - 운 좋은 낮은 등급이라 해봐야 잘해야 다음
// 등급과 동률일 뿐, 확실히 능가할 수는 없다.
public static class EquipmentOptionTable
{
    // 빨간 스톤 -> 검의 ATK 옵션, ATK는 RedVial로 HP는 GreenVial로 드롭되는 게임의 기존
    // 색상 체계와 일치시켰다.
    private static readonly Vector2[] AttackRanges =
    {
        new Vector2(1f, 2f),        // Normal
        new Vector2(2f, 3f),        // Rare
        new Vector2(3f, 5f),        // Epic
        new Vector2(5f, 7f),        // Unique
        new Vector2(7f, 10f),       // Legendary
    };

    // 초록 스톤 -> 방패의 HP 옵션.
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

        // ATK는 소수점 첫째 자리까지 표시된다(플레이어 자신의 공격력이 소수 값이기 때문); HP는
        // 항상 정수이므로, 여기서 반올림을 해주지 않으면 원래 정수인 스탯인데 롤된 옵션이
        // "+41.7 HP"처럼 표시되는 일이 생긴다.
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
