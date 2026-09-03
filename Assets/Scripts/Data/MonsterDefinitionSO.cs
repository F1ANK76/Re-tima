using UnityEngine;

// 몬스터 한 종류의 겉모습과 배수. 스테이지 곡선은 MonsterSpawner에 있다.
// 종류(일반/엘리트/보스)는 로스터의 어느 슬롯에 꽂혔는지가 정한다.
[CreateAssetMenu(fileName = "MonsterDefinition", menuName = "Retima/Monster Definition")]
public class MonsterDefinitionSO : ScriptableObject
{
    public GameObject prefab;

    // 같은 서브스테이지 일반 몹 = 1
    [Min(0f)] public float hpMultiplier = 1f;
    [Min(0f)] public float attackMultiplier = 1f;

    // 프리팹 스케일에 곱해진다. 근접 사거리도 같이 비례해서 늘어난다.
    [Min(0.01f)] public float scale = 1f;
}
