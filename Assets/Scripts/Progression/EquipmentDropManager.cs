using UnityEngine;

// StatDropManager와 별개의 드롭 타입 - 순수 ATK/HP 증가치 대신 실제 Sword/Shield 아이템을 준다.
// DropCoordinator가 이 타입을 드롭하기로 정했을 때만 RollAndSpawn이 호출된다. 픽업을 주우면
// 장착 슬롯이 갱신되는데, 현재 장착분보다 등급이 높을 때만이다 - 같거나 낮은 등급은 아무것도
// 바꾸지 않는다. 장비 보너스는 오직 장착 등급 하나로 결정된다.
public class EquipmentDropManager : DropSource
{
    // null은 해당 타입이 아직 아무것도 장착되지 않았음을 뜻한다 - 이미 실질적인(작지만) 보너스가
    // 있는 "Normal 등급 장착" 상태와는 구분된다.
    private StatGrade? equippedSwordGrade;
    private StatGrade? equippedShieldGrade;

    public StatGrade? EquippedSwordGrade => equippedSwordGrade;
    public StatGrade? EquippedShieldGrade => equippedShieldGrade;

    // Equip 탭에 표시되는 값 - 장착 등급만으로 결정된다.
    public float SwordEquipmentBonus => equippedSwordGrade.HasValue ? GetGradeSwordAtk(equippedSwordGrade.Value) : 0f;
    public float ShieldEquipmentBonus => equippedShieldGrade.HasValue ? GetGradeShieldHp(equippedShieldGrade.Value) : 0f;

    // 장착 장비의 고정 보상 테이블 - 검이나 방패의 등급이 기본 보너스를 그대로 결정한다.
    // 스테이지 스케일링은 전혀 없다: Legendary 검은 어느 스테이지에서 드롭됐든 항상 +2 ATK다.
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

    public override void RollAndSpawn(Monster monster)
    {
        EquipmentType equipType = Random.value < 0.5f ? EquipmentType.Sword : EquipmentType.Shield;
        StatGrade grade = GradeRoller.Roll();

        SpawnPickup(monster).InitializeEquipment(equipType, grade, player.transform, combatLoop, ApproachSpeed, this);
    }

    // 플레이어가 픽업에 도달하면 DropPickup이 호출한다. 장착분보다 높은 등급일 때만 장착
    // 아이템이 교체되고, 그 차액만 플레이어 스탯에 얹는다.
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
