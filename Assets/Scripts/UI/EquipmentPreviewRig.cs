using UnityEngine;

public class EquipmentPreviewRig : MonoBehaviour
{
    // 아이콘은 1920 기준 96px로 보인다. 캔버스가 화면에 맞춰 커지니 4K에서 192px -> 거기까지 선명하게
    private const int TextureResolution = 192;
    // 1920에서는 2배로 찍어 줄이는 셈이라 MSAA 없이도 테두리가 뭉개지지 않는다
    private const int TextureAntiAliasing = 1;

    private const float CameraFieldOfView = 45f;
    private const float FrameFill = 0.62f;
    private const float MinBoundsRadius = 0.05f;

    private DropPickup pickupPrefab;
    private DropPickup current;
    // 지금 세워둔 등급. 리그 하나가 한 슬롯 전담이라 등급만 보면 같은 프리뷰인지 알 수 있다
    private StatGrade? shownGrade;
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
        // 패널을 열 때마다, 그리고 다른 슬롯을 주웠을 때도 Show가 불린다 -> 같은 프리뷰면 그냥 둔다
        if (shownGrade == grade) return;

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
        shownGrade = grade;
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

        // 파괴는 프레임 끝에 처리된다 -> 같은 프레임에 새 프리뷰가 들어오면 둘이 겹쳐 보인다
        current.gameObject.SetActive(false);
        Destroy(current.gameObject);

        current = null;
        shownGrade = null;
    }

    private void OnDestroy()
    {
        if (previewCamera != null) previewCamera.targetTexture = null;
        if (renderTexture != null) Destroy(renderTexture);
    }
}
