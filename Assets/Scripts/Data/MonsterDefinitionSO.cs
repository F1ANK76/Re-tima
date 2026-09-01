using UnityEngine;

// 몬스터 한 종류가 "무엇으로 스폰되는가"만 정한다. HP/공격력은 여기 없다 - 그건 전적으로
// MonsterSpawner의 스테이지 커브(GetNormalHp/GetNormalAttack와 elite/boss 배수)가 정한다.
//
// 예전에는 baseHp/baseAttack/monsterName도 들고 있었지만 읽는 곳이 한 군데도 없었다. 밸런스
// 손잡이처럼 생겨서 실제로는 아무 효과가 없는 필드라, 없는 것보다 나쁘다고 보고 지웠다.
[CreateAssetMenu(fileName = "MonsterDefinition", menuName = "Retima/Monster Definition")]
public class MonsterDefinitionSO : ScriptableObject
{
    public MonsterType monsterType = MonsterType.Normal;
    public GameObject prefab;
}
