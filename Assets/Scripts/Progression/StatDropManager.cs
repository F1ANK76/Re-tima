using UnityEngine;

public class StatDropManager : DropSource
{
    [SerializeField] private StatDropTableSO dropTable;

    public override void RollAndSpawn(Vector3 position)
    {
        const int statTypeCount = 2;
        StatType statType = Random.Range(0, statTypeCount) == 0 ? StatType.Attack : StatType.Hp;

        StatGrade grade = GradeRoller.Roll();
        float amount = dropTable.GetAmount(statType, grade);

        SpawnPickup(position).InitializeStatPotion(statType, grade, amount, player.transform, combatLoop, ApproachSpeed);
    }
}
