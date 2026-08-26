using UnityEngine;
using UnityEngine.UI;

public class HudController : MonoBehaviour
{
    [SerializeField] private Text stageText;

    private int currentMainStage = 1;
    private int currentSubStage = 1;

    private void OnEnable()
    {
        GameEvents.OnStageChanged += HandleStageChanged;
        GameEvents.OnMonsterSpawned += HandleMonsterSpawned;
        GameEvents.OnMonsterDied += HandleMonsterDied;
    }

    private void OnDisable()
    {
        GameEvents.OnStageChanged -= HandleStageChanged;
        GameEvents.OnMonsterSpawned -= HandleMonsterSpawned;
        GameEvents.OnMonsterDied -= HandleMonsterDied;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
        currentSubStage = subStage;
        if (stageText != null) stageText.text = $"Stage {mainStage}-{subStage}";
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        if (monster.Type == MonsterType.Boss && stageText != null)
        {
            stageText.text = $"Stage {currentMainStage}-Boss";
        }
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (monster.Type == MonsterType.Boss && stageText != null)
        {
            stageText.text = $"Stage {currentMainStage}-{currentSubStage}";
        }
    }
}
