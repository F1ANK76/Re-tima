using UnityEngine;

// 항상 카메라를 바라보도록 회전시킨다. 기본 대상은 게임플레이 카메라(Camera.main)지만,
// 자기만의 카메라로 따로 렌더링하는 곳(EquipmentPreviewRig의 아이콘 무대)은 SetCamera로
// 대상을 바꿔줘야 한다 - 안 그러면 저 멀리 있는 무대의 빌보드가 메인 카메라 기준으로 정렬돼
// 프리뷰 카메라 입장에서는 거의 옆면으로 선 판때기가 되고, 가산 아우라/반짝임 쿼드가
// 아이템을 관통해 반으로 갈라진 것처럼 보인다.
public class Billboard : MonoBehaviour
{
    private Transform cam;
    // Start가 Camera.main으로 덮어쓰지 않도록 하는 표시. SetCamera는 보통 Instantiate 직후
    // (즉 Start보다 먼저) 호출되므로 이 가드가 없으면 지정한 카메라가 곧바로 날아간다.
    private bool cameraAssigned;

    public void SetCamera(Camera target)
    {
        cam = target != null ? target.transform : null;
        cameraAssigned = true;
    }

    private void Start()
    {
        if (cameraAssigned) return;
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    private void LateUpdate()
    {
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }
}
