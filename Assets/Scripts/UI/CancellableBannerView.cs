using UnityEngine;

// 코루틴 하나로 재생되고, 재생 도중 취소될 수 있는 배너의 공통 뼈대.
//
// ClearBannerView(스테이지 클리어)와 StageBannerView(스테이지 안내) 둘만 이 계약을 공유한다 -
// 사망으로 배너 뒤의 흐름이 무효화되면 애니메이션을 끝까지 재생하지 않고 즉시 치워야 한다.
// FailBannerView와 GameCompleteView는 취소 대상이 아니라(각각 호출자가 끝까지 await 하고,
// 버튼 입력을 기다린다) 여기 묶지 않았다.
//
// SerializeField는 하나도 올리지 않는다. 두 배너는 지속시간/스케일 값의 이름과 개수가 서로
// 달라서, 억지로 통일하면 씬에 저장된 값이 끊긴다 - 공유할 가치가 있는 건 아래 배선뿐이다.
[RequireComponent(typeof(CanvasGroup))]
public abstract class CancellableBannerView : MonoBehaviour
{
    protected CanvasGroup canvasGroup;
    protected Coroutine routine;

    // 진행 중인 재생을 버리고 배너를 즉시 숨긴다 - 애니메이션 없이 바로다. 취소가 필요한
    // 상황(사망)에서는 재시작 시퀀스가 페이드 완료를 기다려 줄 이유가 없다.
    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
