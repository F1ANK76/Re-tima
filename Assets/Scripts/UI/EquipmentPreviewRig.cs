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

        // 아이콘은 멈춰 있으므로 매 프레임 찍을 이유가 없다 -> 카메라는 끄고 필요할 때만 Render를 부른다
        previewCamera.enabled = false;
        // 빈 칸도 한 번은 찍어야 한다 -> 안 찍은 렌더타겟에는 아무 내용도 없다
        previewCamera.Render();
    }

    public void Show(EquipmentType equipType, StatGrade grade)
    {
        // 패널을 열 때마다, 그리고 다른 슬롯을 주웠을 때도 Show가 불린다 -> 같은 프리뷰면 그냥 둔다
        if (shownGrade == grade) return;

        DestroyCurrent();
        if (pickupPrefab == null) return;

        current = Instantiate(pickupPrefab, transform.position, Quaternion.identity, transform);
        current.InitializeEquipment(equipType, grade, transform, null, 0f, null);
        current.StopAllCoroutines();

        current.transform.localPosition = Vector3.zero;
        current.transform.localScale = Vector3.one * GradeVisuals.GetPotionScale(grade);

        // 카메라를 옮긴 뒤에 굳혀야 한다 -> 빌보드가 옮기기 전 카메라를 보고 돌아간다
        FrameOnVisual();
        FreezePreview();

        previewCamera.Render();
        shownGrade = grade;
    }

    // 아이콘은 움직이지 않는다 -> 연출을 다 올라온 모습으로 세워두고 멈춘 뒤 한 장만 찍는다
    private void FreezePreview()
    {
        foreach (Billboard billboard in current.GetComponentsInChildren<Billboard>(true))
        {
            // SetCamera가 그 자리에서 방향까지 맞춘다 -> LateUpdate를 기다리지 않는다
            billboard.SetCamera(previewCamera);
            billboard.enabled = false;
        }

        foreach (DropPickupAuraMotion aura in current.GetComponentsInChildren<DropPickupAuraMotion>(true)) aura.FreezeAtRest();
        foreach (DropPickupSparkle sparkle in current.GetComponentsInChildren<DropPickupSparkle>(true)) sparkle.FreezeAtRest();
        foreach (DropPickupSparkleRing ring in current.GetComponentsInChildren<DropPickupSparkleRing>(true)) ring.enabled = false;
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

        DestroyCurrent();
        // 빈 칸으로 다시 찍는다 -> 안 찍으면 마지막 아이템이 그대로 남아 있다
        previewCamera.Render();
    }

    private void DestroyCurrent()
    {
        if (current == null) return;

        // 파괴는 프레임 끝에 처리된다 -> 지금 찍는 그림에 옛 아이템이 남지 않게 먼저 숨긴다
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
