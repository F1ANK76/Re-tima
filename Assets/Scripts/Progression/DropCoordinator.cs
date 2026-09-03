using System.Collections.Generic;
using UnityEngine;

// 처치 하나당 드롭 타입 하나만 나가도록 모든 DropSource를 조율한다. 각 소스는 스스로
// OnMonsterDied를 듣지 않고, 여기서 뽑힌 소스의 RollAndSpawn만 호출당한다.
//
// 드롭 여부 자체는 dropChance 하나로만 결정된다(예전 statDropBaseChance와 같은 50%). 일단
// 드롭이 뜨기로 정해지면, 그중 어느 타입이냐를 아래 목록이 나눈다: 현재 메인 스테이지가
// unlockStage 이상인 소스만 후보가 되고, 그 안에서 weight 비율로 하나가 뽑힌다.
//
// 소스가 목록이라 새 드롭 타입은 DropSource를 상속한 파일 하나를 만들어 여기에 끌어다 놓기만
// 하면 된다 - 예전처럼 소스별 필드와 2분기 if/else를 이 파일에서 고칠 필요가 없다.
public class DropCoordinator : MonoBehaviour
{
    // 드롭 타입 하나의 등록 항목. 확률과 해금 시점이 코드 상수가 아니라 인스펙터 값이므로
    // 밸런스를 만질 때 재컴파일이 필요 없다.
    [System.Serializable]
    public class Entry
    {
        public DropSource source;

        // 후보로 올라온 소스들 사이에서의 상대 가중치. 기존 밸런스는 "몇 번째로 해금된
        // 타입이냐"를 그대로 쓴다(포션=1, 장비=2) - 그래서 최근에 해금된 타입이 더 잘 뜬다.
        [Min(0f)] public float weight = 1f;

        // 이 타입이 드롭 후보 목록에 합류하는 메인 스테이지.
        [Min(1)] public int unlockStage = 1;
    }

    [SerializeField] private List<Entry> sources = new List<Entry>();

    // 처치당 "뭔가 하나라도 뜰지"를 결정하는 전체 확률 - 스테이지나 타입 수와 무관하게 고정이다.
    [Range(0f, 1f)] [SerializeField] private float dropChance = 0.5f;

    private int currentMainStage = 1;

    // 목록이 비면 처치해도 아무것도 드롭되지 않는데, 그건 화면상 "드롭 운이 없다"와 구분이
    // 안 된다. 씬 배선이 끊긴 채로 조용히 굴러가지 않도록 시작할 때 한 번 짚고 넘어간다.
    private void Awake()
    {
        if (sources.Count == 0)
            Debug.LogWarning("DropCoordinator: 등록된 드롭 소스가 없다 - 처치해도 아무것도 드롭되지 않는다.", this);
    }

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
        // 일반 몬스터 처치 시 일정 확률로 아이템 드롭
        if (monster.Type != MonsterType.Normal) return;

        if (Random.value > dropChance) return;

        float total = 0f;
        for (int i = 0; i < sources.Count; i++)
        {
            if (IsCandidate(sources[i])) total += sources[i].weight;
        }

        if (total <= 0f) return;

        float roll = Random.value * total;

        for (int i = 0; i < sources.Count; i++)
        {
            Entry entry = sources[i];
            if (!IsCandidate(entry)) continue;

            // <= 0: Random.value가 정확히 1.0이면 roll이 total과 같아져, < 0으로는
            // 마지막 후보에서도 통과해 아무것도 뽑히지 않는다.
            roll -= entry.weight;
            if (roll <= 0f)
            {
                entry.source.RollAndSpawn(monster);
                return;
            }
        }
    }

    private bool IsCandidate(Entry entry)
        => entry != null && entry.source != null && entry.weight > 0f && currentMainStage >= entry.unlockStage;
}
