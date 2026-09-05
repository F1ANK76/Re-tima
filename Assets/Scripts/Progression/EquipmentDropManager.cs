using UnityEngine;

public class EquipmentDropManager : DropSource
{
    private StatGrade? equippedSwordGrade;
    private StatGrade? equippedShieldGrade;

    public StatGrade? EquippedSwordGrade => equippedSwordGrade;
    public StatGrade? EquippedShieldGrade => equippedShieldGrade;

    // Equip 탭에 표시되는 값 - 장착 등급만으로 결정된다.
    public float SwordEquipmentBonus => equippedSwordGrade.HasValue ? GetGradeSwordAtk(equippedSwordGrade.Value) : 0f;
    public float ShieldEquipmentBonus => equippedShieldGrade.HasValue ? GetGradeShieldHp(equippedShieldGrade.Value) : 0f;

    private static float GetGradeSwordAtk(StatGrade grade)
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

    private static float GetGradeShieldHp(StatGrade grade)
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

    public override void RollAndSpawn(Vector3 position)
    {
        EquipmentType equipType = Random.value < 0.5f ? EquipmentType.Sword : EquipmentType.Shield;
        StatGrade grade = GradeRoller.Roll();

        SpawnPickup(position).InitializeEquipment(equipType, grade, player.transform, combatLoop, ApproachSpeed, this);
    }

    public void CompleteDrop(EquipmentType equipType, StatGrade grade)
    {
        if (equipType == EquipmentType.Sword)
        {
            float oldBonus = SwordEquipmentBonus;

            if (!equippedSwordGrade.HasValue || grade > equippedSwordGrade.Value) equippedSwordGrade = grade;

            player.IncreaseAttack(SwordEquipmentBonus - oldBonus);
        }
        else
        {
            float oldBonus = ShieldEquipmentBonus;

            if (!equippedShieldGrade.HasValue || grade > equippedShieldGrade.Value) equippedShieldGrade = grade;

            player.IncreaseMaxHp(ShieldEquipmentBonus - oldBonus);
        }

        // 장착 상태를 바꾸지 않는 픽업도 알린다 - 주웠다는 사실 자체는 팝업으로 보여줘야 한다.
        GameEvents.RaiseEquipmentPickedUp(equipType, grade);
    }
}
