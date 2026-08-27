#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// 테스트 전용 스테이지 점프 패널이다. 릴리스 빌드에는 절대 컴파일되지 않는다(위의 #if 참고) -
// 플레이 씬의 아무 GameObject에나 붙이고 StageManager를 할당해서 사용한다.
public class DebugStageSelector : MonoBehaviour
{
    [SerializeField] private StageManager stageManager;
    // 선택 사항: 메인 메뉴에서 곧장 점프할 경우, Play 버튼을 눌렀을 때와 정확히 똑같이
    // 전환되어야 한다(메뉴 UI, Play 버튼, HUD 숨김이 모두 함께 사라진다) - 방금 점프한
    // 스테이지 위에 그 오버레이가 그대로 남아있게 두면 안 된다.
    [SerializeField] private TitleScreenView titleScreen;
    // 프로젝트의 Player Settings에서 Active Input Handling이 새 Input System 전용으로
    // 설정되어 있어서, 레거시 Input 클래스가 아니라 Keyboard를 통해 읽어야 한다.
    [SerializeField] private Key toggleKey = Key.F1;

    // 처음에는 숨겨진 상태로 시작한다 - 이 패널은 디버그용 편의 기능일 뿐, 토글 키를 누르기도
    // 전에 플레이어를 맞이하거나(혹은 모든 스크린샷에 나타나거나) 해서는 안 된다.
    private bool visible = false;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible || stageManager == null) return;

        // 하드코딩이 아니라 서브스테이지 개수에서 크기를 산출한다: 스테이지당 서브스테이지가
        // 10개일 때 기존의 고정 260px 영역은 행 대부분을 잘라버렸다. 라벨은 그냥 서브스테이지
        // 번호(보스는 "B")뿐이라 10개가 한 줄에 그대로 들어간다.
        const float cellWidth = 30f;
        const float rowLabelWidth = 56f;
        // GUILayout은 모든 요소 사이에 ~2px의 간격을 넣고 GUI.skin.box도 자체 패딩을 추가하므로,
        // 영역은 이 둘을 모두 감안해야 한다 - 버튼의 순수 너비만으로 크기를 잡으면 마지막("B")
        // 버튼이 오른쪽 가장자리에서 잘려나갔다.
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
