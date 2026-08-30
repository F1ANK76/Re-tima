using UnityEngine;

// 처치 하나당 드롭 타입 하나만 나가도록 두 드롭 매니저(StatDropManager/EquipmentDropManager)를
// 조율한다. 각 매니저는 더 이상 스스로 OnMonsterDied를 듣지 않고, 여기서 뽑힌 타입의
// RollAndSpawn만 호출당한다.
//
// 드롭 여부 자체는 dropChance 하나로만 결정된다(예전 statDropBaseChance와 같은 50%). 일단
// 드롭이 뜨기로 정해지면, 그중 어느 타입이냐를 나눈다: 활성 타입 목록은 현재 메인 스테이지에
// 따라 늘어난다 - 1스테는 포션만, 2스테부터 장비도 함께 뜬다. 그 안에서 어느 게 뜰지는 "그
// 타입이 몇 번째로 해금된 스테이지의 것이냐"를 그대로 가중치로 쓴 랜덤 롤이 정한다(포션=1,
// 장비=2) - 그래서 항상 가장 최근에 해금된 타입이 그 안에서 가장 잘 뜬다.
public class DropCoordinator : MonoBehaviour
{
    [SerializeField] private StatDropManager statDropManager;
    [SerializeField] private EquipmentDropManager equipmentDropManager;

    // 처치당 "뭔가 하나라도 뜰지"를 결정하는 전체 확률 - 스테이지나 타입 수와 무관하게 고정이다.
    [Range(0f, 1f)] [SerializeField] private float dropChance = 0.5f;

    private int currentMainStage = 1;

    private void OnEnable()
    {
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
    }

    private void HandleMonsterDied(Monster monster)
    {
        // Elite/Boss는 (서브)스테이지를 마무리 짓는 관문일 뿐 전리품의 출처가 아니다 - 그 사이의
        // 일반 몬스터 그라인드에서만 뭔가가 드롭된다.
        if (monster.Type != MonsterType.Normal) return;

        if (Random.value > dropChance) return;

        float statWeight = statDropManager != null ? 1f : 0f;
        float equipWeight = (currentMainStage >= EquipmentDropManager.UnlockStage && equipmentDropManager != null) ? 2f : 0f;

        float total = statWeight + equipWeight;
        if (total <= 0f) return;

        float roll = Random.value * total;

        if (roll < statWeight)
        {
            statDropManager.RollAndSpawn(monster);
        }
        else
        {
            equipmentDropManager.RollAndSpawn(monster);
        }
    }
}
