using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Stage 1 plays in daylight, stage 2 at night. Post-processing alone can't sell that - the
// grade only re-tints whatever light is already there - so this drives four things together
// and cross-fades them as one:
//   * two global Volumes (day/night grade), blended by weight rather than swapped, so the
//     colour shift is continuous instead of popping on the frame the stage changes
//   * the sun: colour + intensity (warm bright -> pale cool moonlight)
//   * the sky: the skybox's own day/night cubemap blend slider
//   * fog: colour + density, which is what actually gives the tree layers depth
//
// Lives on a always-active object and listens to GameEvents.OnStageChanged. The transition
// runs on unscaled time so it still plays while the stage banner has gameplay paused.
public class StageMoodController : MonoBehaviour
{
    [Header("Volumes (both global, same priority)")]
    [SerializeField] private Volume dayVolume;
    [SerializeField] private Volume nightVolume;

    [Header("Sun")]
    [SerializeField] private Light sun;
    [SerializeField] private Color daySunColor = new Color(1f, 0.96f, 0.89f);
    [SerializeField] private float daySunIntensity = 2f;
    [SerializeField] private Color nightSunColor = new Color(0.62f, 0.70f, 0.95f);
    [SerializeField] private float nightSunIntensity = 0.85f;

    [Header("Ambient (Skybox mode uses this as a multiplier)")]
    [SerializeField] private float dayAmbientIntensity = 1f;
    [SerializeField] private float nightAmbientIntensity = 0.65f;

    [Header("Sky")]
    // The project's skybox is Skybox/Cubemap Blend with a day cubemap in _Tex and a night one
    // in _Tex_Blend, so the whole day->night sky is this single 0..1 slider.
    [SerializeField] private string skyBlendProperty = "_CubemapTransition";
    [SerializeField] private float daySkyBlend = 0f;
    [SerializeField] private float nightSkyBlend = 0.85f;

    [Header("Fog")]
    [SerializeField] private bool driveFog = true;
    [SerializeField] private Color dayFogColor = new Color(0.78f, 0.85f, 0.92f);
    [SerializeField] private float dayFogDensity = 0.012f;
    [SerializeField] private Color nightFogColor = new Color(0.13f, 0.15f, 0.30f);
    [SerializeField] private float nightFogDensity = 0.025f;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 1.2f;
    // The one main stage that plays at night, NOT a "this stage and up" threshold - stage 3
    // brings stage 1's daylight forest back along with its roster (see MonsterSpawner, which
    // gates its own bird cast on exactly this stage), so night is stage 2's alone. Keeping
    // this a single stage rather than a floor is what lets the run read as day -> night ->
    // day instead of going dark permanently after stage 2.
    [SerializeField] private int nightStage = 2;

    // 0 = full day, 1 = full night. Held so a stage change mid-transition continues from
    // wherever the blend actually is rather than snapping back.
    private float nightAmount = -1f;
    private Coroutine transition;
    // Instanced so writing the blend slider can't dirty the shared skybox asset on disk.
    private Material skyInstance;

    private void OnEnable()
    {
        GameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void Awake()
    {
        if (RenderSettings.skybox != null)
        {
            skyInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyInstance;
        }

        if (driveFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        float target = mainStage == nightStage ? 1f : 0f;

        // First stage of the run has nothing to fade from - land on the target immediately so
        // the game doesn't open mid-crossfade.
        if (nightAmount < 0f)
        {
            Apply(target);
            return;
        }

        if (Mathf.Approximately(nightAmount, target)) return;

        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        float from = nightAmount;
        float t = 0f;

        while (t < transitionDuration)
        {
            // Unscaled: the stage banner suspends gameplay, and the mood shift should still
            // be running underneath it rather than freezing until combat resumes.
            t += Time.unscaledDeltaTime;
            Apply(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / transitionDuration)));
            yield return null;
        }

        Apply(target);
        transition = null;
    }

    private void Apply(float night)
    {
        nightAmount = Mathf.Clamp01(night);

        // Weight-blended rather than profile-swapped: URP blends the two profiles' overrides,
        // so every graded value moves together instead of one set cutting to the other.
        if (dayVolume != null) dayVolume.weight = 1f - nightAmount;
        if (nightVolume != null) nightVolume.weight = nightAmount;

        if (sun != null)
        {
            sun.color = Color.Lerp(daySunColor, nightSunColor, nightAmount);
            sun.intensity = Mathf.Lerp(daySunIntensity, nightSunIntensity, nightAmount);
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, nightAmount);

        if (skyInstance != null && skyInstance.HasProperty(skyBlendProperty))
            skyInstance.SetFloat(skyBlendProperty, Mathf.Lerp(daySkyBlend, nightSkyBlend, nightAmount));

        if (driveFog)
        {
            RenderSettings.fogColor = Color.Lerp(dayFogColor, nightFogColor, nightAmount);
            RenderSettings.fogDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, nightAmount);
        }
    }

    private void OnDestroy()
    {
        if (skyInstance != null) Destroy(skyInstance);
    }
}
