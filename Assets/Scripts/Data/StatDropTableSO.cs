using UnityEngine;

// 스탯 포션이 등급별로 얼마나 올려주는가. 값과 그 조회를 한곳에 둔다 - 등급을 하나 늘릴 때
// 필드와 switch가 같은 파일 안에 있어야 한쪽만 고치는 일이 없다.
[CreateAssetMenu(fileName = "StatDropTable", menuName = "Retima/Stat Drop Table")]
public class StatDropTableSO : ScriptableObject
{
    [Header("ATK")]
    public float atkNormal = 0.1f;
    public float atkRare = 0.2f;
    public float atkEpic = 0.3f;
    public float atkUnique = 0.5f;
    public float atkLegendary = 1f;

    [Header("HP")]
    public float hpNormal = 3f;
    public float hpRare = 5f;
    public float hpEpic = 10f;
    public float hpUnique = 20f;
    public float hpLegendary = 30f;

    public float GetAmount(StatType statType, StatGrade grade)
    {
        if (statType == StatType.Attack)
        {
            switch (grade)
            {
                case StatGrade.Normal: return atkNormal;
                case StatGrade.Rare: return atkRare;
                case StatGrade.Epic: return atkEpic;
                case StatGrade.Unique: return atkUnique;
                default: return atkLegendary;
            }
        }

        switch (grade)
        {
            case StatGrade.Normal: return hpNormal;
            case StatGrade.Rare: return hpRare;
            case StatGrade.Epic: return hpEpic;
            case StatGrade.Unique: return hpUnique;
            default: return hpLegendary;
        }
    }
}
