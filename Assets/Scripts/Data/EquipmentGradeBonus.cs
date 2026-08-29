// 장착 장비의 고정 보상 테이블 - 검이나 방패의 등급이 기본 보너스를 그대로 결정한다.
// 그와 별개로, 해당 타입의 모든 획득(업그레이드 여부와 무관하게)은 별도의
// 상한 없는 숙련도(mastery) 게이지(EquipmentDropManager 참고)를 채우며, 100%를 가득 채울 때마다
// +1 ATK / +10 HP로 전환된다 - 이것이 동급 이하 등급 아이템을 주워도 의미가 있게 만드는 이유다. 여기에는
// 스테이지 스케일링이 전혀 없다: Legendary 검은 어느 스테이지에서 드롭됐든 항상 +5 ATK다.
public static class EquipmentGradeBonus
{
    // 등급과 무관하게 숙련도 레벨당 고정으로 부여되는 스탯.
    public const float SwordLevelAtkBonus = 1f;
    public const float ShieldLevelHpBonus = 10f;

    // 이 등급의 아이템 하나를 주웠을 때 숙련도 게이지가 채워지는 비율 - 의도적으로 해당 등급의
    // 스탯 보너스와 비례하지 않게 했다(Legendary는 Normal의 ATK 20배지만 숙련도 상승은 겨우 10배),
    // 그래서 낮은 등급을 반복해서 파밍해도 시간이 지나면 여전히 유의미하게 기여한다.
    public static float GetProgressPercent(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 20f;
            case StatGrade.Rare: return 50f;
            case StatGrade.Epic: return 100f;
            case StatGrade.Unique: return 150f;
            default: return 200f;
        }
    }


    public static float GetSwordAtk(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 0.3f;
            case StatGrade.Rare: return 0.5f;
            case StatGrade.Epic: return 1f;
            case StatGrade.Unique: return 1.5f;
            default: return 2f;
        }
    }

    public static float GetShieldHp(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 10f;
            case StatGrade.Rare: return 15f;
            case StatGrade.Epic: return 20f;
            case StatGrade.Unique: return 25f;
            default: return 30f;
        }
    }
}
