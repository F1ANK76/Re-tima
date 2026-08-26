using UnityEngine;

// One miniature "display case" for the equip panel: a small stationary camera looking at a
// tiny stage far from the playable area, rendering to a texture a RawImage reads from.
//
// Reuses EquipmentDropPickup's own mesh/material/aura spawning (Initialize) for the displayed
// item rather than duplicating that ~250 lines - the toss/run-over coroutine it would normally
// start is stopped before it gets to run more than its opening (synchronous) step, so the item
// just sits there already "landed", but its aura glow and sparkle twinkle (driven by Update,
// not the coroutine) keep animating exactly as they do on a real drop.
public class EquipmentPreviewRig : MonoBehaviour
{
    // Higher than the icon actually displays at (see EquipmentPanelView.IconSize) - the extra
    // resolution plus MSAA is what keeps the item's own 1024px texture from reading as a
    // blurry smear once downsized into a ~96px UI icon.
    private const int TextureResolution = 256;
    private const int TextureAntiAliasing = 4;

    private const float CameraFieldOfView = 45f;
    // How much of the vertical frame the item's bounding sphere should fill - short of 1 so
    // there is always a small margin, regardless of how a given mesh's proportions differ
    // from another's.
    private const float FrameFill = 0.62f;
    private const float MinBoundsRadius = 0.05f;

    private EquipmentDropPickup pickupPrefab;
    private EquipmentDropPickup current;
    private Camera previewCamera;
    private RenderTexture renderTexture;

    public RenderTexture Texture => renderTexture;

    public void Initialize(EquipmentDropPickup prefab)
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

    // Swaps in a freshly-initialized pickup at the stage origin, discarding whatever was
    // showing before - simplest correct way to change grade/type is to not reuse the instance.
    public void Show(EquipmentType equipType, StatGrade grade)
    {
        Clear();
        if (pickupPrefab == null) return;

        current = Instantiate(pickupPrefab, transform.position, Quaternion.identity, transform);
        // "player"/combatLoop/dropManager are all irrelevant here - the toss coroutine that
        // would use them never gets to run past its opening frame (see below).
        current.Initialize(equipType, grade, transform, null, 0f, null);
        current.StopAllCoroutines();

        // Initialize starts that coroutine with `StartCoroutine(TossThenRunOver())`, whose
        // first line is itself a `yield return` on the toss sub-coroutine - Unity runs an
        // enumerator synchronously up to its first suspension point, so PlayToss's opening
        // "shrink to zero, then grow toward a point away from the player" already executed
        // one partial step before the StopAllCoroutines call above could reach it. Resetting
        // the resting pose here is what actually lands it centred and at full size instead of
        // wherever that partial step left it.
        current.transform.localPosition = Vector3.zero;
        current.transform.localScale = Vector3.one * GradeVisuals.GetPotionScale(grade);

        FrameOnVisual();
    }

    // Points the camera at the item's actual rendered bounds and backs it off just far enough
    // to fill a consistent fraction of the frame - without this, a tall thin sword and a wide
    // flat shield (or a Normal vs. a 2x-scaled Legendary) would each occupy a different amount
    // of the icon, which is what "인is inconsistent" reads as.
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
        // Camera starts at identity rotation (facing local +Z); re-aim it at the item's actual
        // centre rather than assuming that centre sits exactly on the stage origin, since a
        // mesh's pivot is rarely its geometric middle.
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
