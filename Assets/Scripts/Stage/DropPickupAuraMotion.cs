using UnityEngine;

public class DropPickupAuraMotion : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float pulseAmplitude = 0.3f;
    [SerializeField] private float pulseSpeed = 3f;

    private Material material;
    private Light auraLight;
    private Color baseTint;
    private float baseIntensity;
    private float elapsed;

    private void Awake()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null) material = meshRenderer.sharedMaterial;
        auraLight = GetComponent<Light>();

        if (material != null) baseTint = material.GetColor("_BaseColor");
        if (auraLight != null) baseIntensity = auraLight.intensity;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float fade = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
        float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed * Mathf.PI) * pulseAmplitude;
        float factor = fade * pulse;

        if (material != null) material.SetColor("_BaseColor", baseTint * factor);
        if (auraLight != null) auraLight.intensity = baseIntensity * factor;
    }
}
