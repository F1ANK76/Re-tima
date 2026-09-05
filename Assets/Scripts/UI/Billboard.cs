using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform cam;
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
