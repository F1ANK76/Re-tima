using UnityEngine;

public class DropPickupAuraMotion : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float pulseAmplitude = 0.3f;
    [SerializeField] private float pulseSpeed = 3f;

    private Material material;
    private Color baseTint;
    private float elapsed;

    // 머티리얼을 붙인 뒤에 AddComponent한다 -> Awake 시점에 이미 준비돼 있다
    private void Awake()
    {
        material = GetComponent<MeshRenderer>().sharedMaterial;
        baseTint = material.GetColor("_BaseColor");
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float fade = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
        float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed * Mathf.PI) * pulseAmplitude;

        material.SetColor("_BaseColor", baseTint * (fade * pulse));
    }
}
