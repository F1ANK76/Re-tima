#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// Test-only stage jump panel. Never compiled into release builds (see the #if above) -
// attach to any GameObject in the play scene and assign the StageManager to use it.
public class DebugStageSelector : MonoBehaviour
{
    [SerializeField] private StageManager stageManager;
    // Optional: jumping straight from the main menu should hand off exactly like pressing
    // Play does (menu chrome, Play button, and HUD-hiding all clear together) rather than
    // leaving that overlay sitting on top of the stage it just jumped to.
    [SerializeField] private TitleScreenView titleScreen;
    // The project's Player Settings has Active Input Handling set to the new Input System
    // only, so this has to read via Keyboard rather than the legacy Input class.
    [SerializeField] private Key toggleKey = Key.F1;

    // Starts hidden - the panel is a debug convenience, not something that should greet the
    // player (or show up in every screenshot) before they ever press the toggle key.
    private bool visible = false;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible || stageManager == null) return;

        // Sized from the substage count rather than hardcoded: at ten substages per stage the
        // old fixed 260px area clipped most of the row. Labels are just the substage number
        // (and "B" for the boss) so ten of them still fit on one line.
        const float cellWidth = 30f;
        const float rowLabelWidth = 56f;
        // GUILayout puts ~2px of spacing between every element and GUI.skin.box adds its own
        // padding, so the area has to budget for both - sizing it to the raw button widths
        // clipped the last ("B") button off the right edge.
        const float cellSpacing = 2f;
        const float boxPadding = 40f;
        float width = rowLabelWidth + StageManager.BossSubStage * (cellWidth + cellSpacing) + boxPadding;
        float height = 60f + StageManager.MaxMainStage * 26f;

        GUILayout.BeginArea(new Rect(10, Screen.height - height - 10, width, height), GUI.skin.box);
        GUILayout.Label($"Stage Jump ({toggleKey} to hide) - now {stageManager.MainStage}-{stageManager.SubStage}");

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
