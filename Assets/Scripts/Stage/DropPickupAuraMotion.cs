using UnityEngine;

public class DropPickupAuraMotion : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float pulseAmplitude = 0.3f;
    [SerializeField] private float pulseSpeed = 3f;

    private Material material;
    // 예산에서 밀린 드랍은 라이트가 없다
    private Light auraLight;
    private Color baseTint;
    private float baseIntensity;
    private float elapsed;

    // 머티리얼을 붙인 뒤에 AddComponent한다 -> Awake 시점에 이미 준비돼 있다
    private void Awake()
    {
        material = GetComponent<MeshRenderer>().sharedMaterial;
        baseTint = material.GetColor("_BaseColor");

        auraLight = GetComponent<Light>();
        if (auraLight != null) baseIntensity = auraLight.intensity;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float fade = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
        float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed * Mathf.PI) * pulseAmplitude;

        float factor = fade * pulse;

        // 알파는 감쇠 전용이라 건드리지 않는다 -> 밝기는 rgb만
        Color tint = baseTint * factor;
        tint.a = 1f;

        material.SetColor("_BaseColor", tint);
        if (auraLight != null) auraLight.intensity = baseIntensity * factor;
    }
}
