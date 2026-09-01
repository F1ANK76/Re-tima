using UnityEngine;

// 드롭 타입 하나(스탯 포션/장비/...)의 공통 뼈대. DropCoordinator가 "이번 처치는 이 타입"이라고
// 정했을 때만 RollAndSpawn이 호출되고, 그 안에서 무엇이 뜰지(종류·등급·수치)만 굴린다.
//
// 스폰에 필요한 참조 네 개와 Instantiate 절차는 타입마다 완전히 같아서 여기로 올렸다 - 예전에는
// StatDropManager와 EquipmentDropManager가 같은 필드 네 개와 같은 Instantiate 블록을 주석까지
// 글자 그대로 복제해 들고 있었고, 세 번째 타입을 넣으려면 그걸 한 번 더 복사해야 했다.
public abstract class DropSource : MonoBehaviour
{
    [SerializeField] protected PlayerCharacter player;
    [SerializeField] protected StageConfigSO stageConfig;
    [SerializeField] protected DropPickup pickupPrefab;
    [SerializeField] protected CombatLoop combatLoop;

    // 몬스터가 걸어 들어오는 속도와 동일하게 맞춘다 - 그래야 몬스터가 드롭을 추월하는 일 없이
    // 같은 속도로 나란히 딸려온다. 모든 드롭 타입이 같은 컨베이어 위에 있으므로 예외는 없다.
    protected static float ApproachSpeed => MonsterSpawner.ApproachSpeed;

    // 눈에 보이는 픽업을 띄울 수 있는 상태인지. false일 때 어떻게 할지는 타입마다 다르다 -
    // 포션은 넘겨줄 대상이 없으니 드롭 자체를 포기하고, 장비는 롤 결과만 즉시 확정한다.
    protected bool CanSpawnPickup => pickupPrefab != null && player != null;

    // 몬스터가 죽은 자리에 픽업 본체를 띄운다. 어떤 아이템인지 채워 넣는 초기화는 각 타입이
    // 반환된 인스턴스에 대고 직접 한다 - 픽업 종류마다 필요한 정보가 다르기 때문이다.
    protected DropPickup SpawnPickup(Monster monster)
        => Instantiate(pickupPrefab, monster.transform.position, Quaternion.identity);

    // DropCoordinator가 이 타입을 드롭하기로 정했을 때 호출한다.
    public abstract void RollAndSpawn(Monster monster);
}
