using UnityEngine;

// 장비 패널용 소형 진열장: 플레이 영역에서 멀리 떨어진 무대를 고정 카메라가 찍고, RawImage가
// 그 렌더 텍스처를 읽는다. 표시용 mesh/material/aura를 250줄쯤 중복 작성하는 대신
// DropPickup.Initialize를 재사용한다 - 던지기/굴러가기 코루틴은 첫 동기 단계 이상
// 진행되기 전에 정지되므로 아이템은 이미 "착지한" 상태로 놓이고, Update로 구동되는 오라 발광과
// 반짝임은 실제 드롭 때와 똑같이 계속 애니메이션된다.
public class EquipmentPreviewRig : MonoBehaviour
{
    // 실제 아이콘 표시 크기(EquipmentPanelView.IconSize)보다 크게 - 이 여유 해상도에 MSAA까지
    // 더해야 아이템의 1024px 텍스처가 약 96px UI 아이콘으로 축소될 때 흐릿하게 뭉개지지 않는다.
    private const int TextureResolution = 256;
    private const int TextureAntiAliasing = 4;

    private const float CameraFieldOfView = 45f;
    // 아이템의 바운딩 스피어가 수직 프레임에서 차지해야 할 비율 - 1보다 약간 작게 잡아서,
    // 메시마다 비율이 서로 다르더라도 항상 약간의 여백이 남도록 한다.
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

    // 이전에 표시되던 것을 버리고, 무대 원점에 새로 초기화된 픽업을 갈아 끼운다 -
    // 등급/타입을 바꾸는 가장 단순하고 확실한 방법은 인스턴스를 재사용하지 않는 것이다.
    public void Show(EquipmentType equipType, StatGrade grade)
    {
        Clear();
        if (pickupPrefab == null) return;

        current = Instantiate(pickupPrefab, transform.position, Quaternion.identity, transform);
        // "player"/combatLoop/dropManager는 여기서는 전부 의미가 없다 - 이것들을 사용할 던지기
        // 코루틴은 어차피 첫 프레임을 넘어서까지 실행되지 않는다(아래 참고).
        current.InitializeEquipment(equipType, grade, transform, null, 0f, null);
        current.StopAllCoroutines();

        // Initialize의 `StartCoroutine(TossThenRunOver())`는 첫 줄부터 던지기 서브 코루틴에 대한
        // `yield return`이고, Unity는 enumerator를 첫 중단 지점까지 동기 실행한다 - 위
        // StopAllCoroutines가 닿기 전에 PlayToss 앞부분("0으로 축소 후 플레이어 반대편을 향해
        // 커짐")이 이미 한 단계 실행된다. 그 잔여 위치 대신 중앙/원래 크기로 포즈를 재설정한다.
        current.transform.localPosition = Vector3.zero;
        current.transform.localScale = Vector3.one * GradeVisuals.GetPotionScale(grade);

        FrameOnVisual();
    }

    // 아이템의 실제 렌더링 바운드를 겨냥하고, 프레임에서 항상 일정 비율을 차지할 만큼만 뒤로
    // 물린다 - 없으면 길고 얇은 검과 넓적한 방패(또는 Normal과 2배 스케일된 Legendary)가
    // 아이콘 안에서 서로 다른 크기를 차지해 "일관성 없다"는 인상을 준다.
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
        // 카메라는 identity 회전(로컬 +Z를 바라봄)으로 시작한다. 메시 피벗이 기하학적 중심과
        // 일치하는 일은 거의 없으므로, 중심이 무대 원점이라 가정하지 않고 실제 중심을 다시 겨냥한다.
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
