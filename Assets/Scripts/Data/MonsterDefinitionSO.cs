using UnityEngine;

[CreateAssetMenu(fileName = "MonsterDefinition", menuName = "UltimaSquad/Monster Definition")]
public class MonsterDefinitionSO : ScriptableObject
{
    public string monsterName = "Monster";
    public MonsterType monsterType = MonsterType.Normal;
    public float baseHp = 20f;
    public float baseAttack = 2f;
    public GameObject prefab;
}
