using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform cam;
    private bool cameraAssigned;

    public void SetCamera(Camera target)
    {
        cam = target != null ? target.transform : null;
        cameraAssigned = true;

        // 한 장만 찍는 아이콘 프리뷰는 LateUpdate를 기다릴 수 없다 -> 그 자리에서 맞춘다
        Face();
    }

    private void Start()
    {
        if (cameraAssigned) return;
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    private void LateUpdate()
    {
        Face();
    }

    private void Face()
    {
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }
}
