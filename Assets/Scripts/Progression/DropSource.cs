using UnityEngine;

public abstract class DropSource : MonoBehaviour
{
    [SerializeField] protected PlayerCharacter player;
    [SerializeField] protected DropPickup pickupPrefab;
    [SerializeField] protected CombatLoop combatLoop;

    protected static float ApproachSpeed => MonsterSpawner.ApproachSpeed;

    protected DropPickup SpawnPickup(Vector3 position)
        => Instantiate(pickupPrefab, position, Quaternion.identity);

    // DropCoordinator가 이 타입을 드롭하기로 정했을 때, 몬스터가 죽은 자리를 넘겨 호출한다.
    public abstract void RollAndSpawn(Vector3 position);
}
