using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class StageMoodController : MonoBehaviour
{
    // Skybox/Cubemap Blend 셰이더에 종속된 이름 -> 바꿀 여지가 없다
    private const string SkyBlendProperty = "_CubemapTransition";

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
    [SerializeField] private float daySkyBlend = 0f;
    [SerializeField] private float nightSkyBlend = 0.85f;

    [Header("Fog")]
    [SerializeField] private Color dayFogColor = new Color(0.78f, 0.85f, 0.92f);
    [SerializeField] private float dayFogDensity = 0.012f;
    [SerializeField] private Color nightFogColor = new Color(0.13f, 0.15f, 0.30f);
    [SerializeField] private float nightFogDensity = 0.025f;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 1.2f;
    [SerializeField] private int nightStage = 2;

    private float nightAmount = -1f;
    private Coroutine transition;
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
        skyInstance = new Material(RenderSettings.skybox);
        RenderSettings.skybox = skyInstance;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        float target = mainStage == nightStage ? 1f : 0f;

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

        dayVolume.weight = 1f - nightAmount;
        nightVolume.weight = nightAmount;

        sun.color = Color.Lerp(daySunColor, nightSunColor, nightAmount);
        sun.intensity = Mathf.Lerp(daySunIntensity, nightSunIntensity, nightAmount);

        RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, nightAmount);

        skyInstance.SetFloat(SkyBlendProperty, Mathf.Lerp(daySkyBlend, nightSkyBlend, nightAmount));

        RenderSettings.fogColor = Color.Lerp(dayFogColor, nightFogColor, nightAmount);
        RenderSettings.fogDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, nightAmount);
    }

    private void OnDestroy()
    {
        Destroy(skyInstance);
    }
}
