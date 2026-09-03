using UnityEngine;

// 몬스터 한 종류를 정의한다: 무엇으로 스폰되고, 같은 서브스테이지의 일반 몬스터에 비해
// 얼마나 크고 센가.
//
// 스테이지가 오를수록 강해지는 곡선 자체는 여기 없다 - 그건 몬스터의 속성이 아니라 진행도의
// 속성이라 MonsterSpawner.GetNormalHp/GetNormalAttack이 갖고 있다. 여기 있는 배수는 그 위에
// 곱해지는 "이 종류는 일반 몹 몇 마리 값어치인가"다.
//
// 어떤 종류인지(일반/엘리트/보스)는 이 파일이 아니라 MonsterSpawner의 로스터에서 어느 슬롯에
// 꽂혔는지가 정한다 - 여기에 또 적어두면 슬롯과 어긋날 수 있는 두 번째 진실 공급원이 된다.
[CreateAssetMenu(fileName = "MonsterDefinition", menuName = "Retima/Monster Definition")]
public class MonsterDefinitionSO : ScriptableObject
{
    public GameObject prefab;

    // 같은 서브스테이지의 일반 몬스터를 1로 놓은 배수. 일반 몬스터 정의는 1로 둔다.
    [Min(0f)] public float hpMultiplier = 1f;
    [Min(0f)] public float attackMultiplier = 1f;

    // 프리팹이 이미 갖고 있는 스케일 위에 곱해진다(MonsterSpawner.SpawnOffscreen 참고).
    // 큰 몬스터는 근접 사거리도 이 값에 비례해 멀어진다(Monster.SetMovement 참고).
    [Min(0.01f)] public float scale = 1f;
}
