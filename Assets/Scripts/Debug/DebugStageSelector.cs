#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// 테스트 전용 스테이지 점프 패널(F1로 토글). 릴리스 빌드에는 존재하지 않는다.
//
// 씬에 미리 배치하지 않고 런타임에 스스로 생성한다. 예전에는 씬에 GameObject로 올려뒀는데,
// 그러면 릴리스 빌드에서 클래스만 사라지고 씬에는 그 컴포넌트의 직렬화 데이터가 남아
// 로드할 때마다 다음 에러가 찍혔다:
//   "A scripted object (probably DebugStageSelector?) has a different serialization layout
//    when loading. (Read 32 bytes but expected 60 bytes)"
// 씬에 아무 흔적도 남기지 않으면 릴리스 빌드가 참조할 것 자체가 없어 문제가 성립하지 않는다.
// 참조도 직렬화 필드 대신 실행 시점에 찾으므로 인스펙터 연결이 필요 없다.
public class DebugStageSelector : MonoBehaviour
{
    private const Key ToggleKey = Key.F1;

    private StageManager stageManager;
    private TitleScreenView titleScreen;

    // 처음에는 숨겨진 상태로 시작한다 - 디버그용 편의 기능일 뿐이라 토글 키를 누르기도 전에
    // 플레이어를 맞이하거나(혹은 모든 스크린샷에 나타나거나) 해서는 안 된다.
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

        // 하드코딩 대신 서브스테이지 개수에서 크기를 산출한다: 스테이지당 10개일 때 기존 고정 260px
        // 영역은 행 대부분을 잘라버렸다. 라벨은 서브스테이지 번호(보스는 "B")뿐이라 10개가 한 줄에 들어간다.
        const float cellWidth = 30f;
        const float rowLabelWidth = 56f;
        // GUILayout은 요소마다 ~2px 간격을 넣고 GUI.skin.box도 자체 패딩을 더하므로 둘 다 감안해야
        // 한다 - 버튼 순수 너비만으로 잡았을 때 마지막("B") 버튼이 오른쪽 가장자리에서 잘려나갔다.
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
