using UnityEngine;

public class EquipmentPreviewRig : MonoBehaviour
{
    private const int TextureResolution = 256;
    private const int TextureAntiAliasing = 4;

    private const float CameraFieldOfView = 45f;
    private const float FrameFill = 0.62f;
    private const float MinBoundsRadius = 0.05f;

    private DropPickup pickupPrefab;
    private DropPickup current;
    private Camera previewCamera;
    private RenderTexture renderTexture;

    public RenderTexture Texture => renderTexture;

    public void Initialize(DropPickup prefab)
    {
        pickupPrefab = prefab;

        renderTexture = new RenderTexture(TextureResolution, TextureResolution, 16)
        {
            name = name + "_RT",
            antiAliasing = TextureAntiAliasing,
            filterMode = FilterMode.Bilinear
        };

        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(transform, false);

        previewCamera = camGo.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.fieldOfView = CameraFieldOfView;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = 20f;
        previewCamera.targetTexture = renderTexture;
    }

    public void Show(EquipmentType equipType, StatGrade grade)
    {
        Clear();
        if (pickupPrefab == null) return;

        current = Instantiate(pickupPrefab, transform.position, Quaternion.identity, transform);
        current.InitializeEquipment(equipType, grade, transform, null, 0f, null);
        current.StopAllCoroutines();

        current.transform.localPosition = Vector3.zero;
        current.transform.localScale = Vector3.one * GradeVisuals.GetPotionScale(grade);

        foreach (Billboard billboard in current.GetComponentsInChildren<Billboard>(true))
        {
            billboard.SetCamera(previewCamera);
        }

        FrameOnVisual();
    }

    private void FrameOnVisual()
    {
        Renderer[] visualRenderers = current.VisualRenderers;
        if (visualRenderers == null || visualRenderers.Length == 0) return;

        Bounds bounds = visualRenderers[0].bounds;
        for (int i = 1; i < visualRenderers.Length; i++) bounds.Encapsulate(visualRenderers[i].bounds);

        float radius = Mathf.Max(bounds.extents.magnitude, MinBoundsRadius);
        float halfFovRad = CameraFieldOfView * 0.5f * Mathf.Deg2Rad;
        float distance = (radius / Mathf.Sin(halfFovRad)) / FrameFill;

        previewCamera.transform.position = bounds.center - previewCamera.transform.forward * distance;
        previewCamera.transform.rotation = Quaternion.LookRotation(bounds.center - previewCamera.transform.position);
    }

    public void Clear()
    {
        if (current == null) return;
        Destroy(current.gameObject);
        current = null;
    }

    private void OnDestroy()
    {
        if (previewCamera != null) previewCamera.targetTexture = null;
        if (renderTexture != null) Destroy(renderTexture);
    }
}
