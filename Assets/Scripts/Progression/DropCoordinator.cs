using System.Collections.Generic;
using UnityEngine;

public class DropCoordinator : MonoBehaviour
{
    // 넣은 순서가 곧 해금 순서이자 가중치다 -> 0번은 1스테/1, 1번은 2스테/2, ...
    [SerializeField] private List<DropSource> sources = new List<DropSource>();

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
        // 일반 몬스터만 드롭한다
        if (monster.Type != MonsterType.Normal) return;

        // 1단계: 뭐라도 뜨는가
        if (Random.value > dropChance) return;

        // 2단계: 어느 타입이 뜨는가
        DropSource picked = PickSource();
        if (picked == null) return;

        // 3단계: 그 타입 안에서 무엇이 뜨는가
        picked.RollAndSpawn(monster.transform.position);
    }

    private DropSource PickSource()
    {
        // 소스가 스테이지보다 적으면 있는 만큼만
        int candidateCount = Mathf.Min(currentMainStage, sources.Count);

        // 1 + 2 + ... + candidateCount
        int totalWeight = candidateCount * (candidateCount + 1) / 2;

        int point = Random.Range(0, totalWeight);
        int cellEnd = 0;

        for (int i = 0; i < candidateCount; i++)
        {
            // 칸 끝을 하나씩 밀면서 -> 점이 이 칸 안에 들어오면 당첨
            cellEnd += i + 1;
            if (point < cellEnd) return sources[i];
        }

        return null;
    }
}
