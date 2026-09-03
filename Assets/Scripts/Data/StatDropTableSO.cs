using UnityEngine;

// 등급별 스탯 포션 수치. 값과 조회를 같이 둬서 등급 추가 시 한 곳만 고친다.
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
