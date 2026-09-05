#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugStageSelector : MonoBehaviour
{
    private const Key ToggleKey = Key.F1;

    private StageManager stageManager;
    private TitleScreenView titleScreen;

    private bool visible;

    // 씬 로드가 끝난 뒤 자동으로 자기 자신을 만든다 - 씬마다 손으로 올려둘 필요가 없다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        var go = new GameObject("DebugStageSelector(Auto)");
        go.AddComponent<DebugStageSelector>();
    }

    private void Awake()
    {
        stageManager = FindFirstObjectByType<StageManager>();
        // 타이틀 화면은 Play를 누르면 비활성화되므로 비활성 오브젝트까지 포함해 찾는다.
        titleScreen = FindFirstObjectByType<TitleScreenView>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[ToggleKey].wasPressedThisFrame) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible || stageManager == null) return;

        const float cellWidth = 30f;
        const float rowLabelWidth = 56f;
        const float cellSpacing = 2f;
        const float boxPadding = 40f;
        float width = rowLabelWidth + StageManager.BossSubStage * (cellWidth + cellSpacing) + boxPadding;
        float height = 60f + StageManager.MaxMainStage * 26f;

        GUILayout.BeginArea(new Rect(10, Screen.height - height - 10, width, height), GUI.skin.box);
        GUILayout.Label($"Stage Jump ({ToggleKey} to hide) - now {stageManager.MainStage}-{stageManager.SubStage}");

        for (int main = 1; main <= StageManager.MaxMainStage; main++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Stage {main}", GUILayout.Width(rowLabelWidth));
            for (int sub = 1; sub <= StageManager.BossSubStage; sub++)
            {
                string label = sub == StageManager.BossSubStage ? "B" : sub.ToString();
                if (GUILayout.Button(label, GUILayout.Width(cellWidth)))
                {
                    if (titleScreen != null) titleScreen.Dismiss();
                    stageManager.DebugJumpTo(main, sub);
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }
}
#endif
