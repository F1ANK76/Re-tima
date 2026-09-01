using UnityEngine;

// 아우라의 페이드인과 idle 맥동. DropPickup이 던지기/달려가기 시퀀스에만 집중하도록 분리했고,
// 그 시퀀스의 어느 박자에서든 DropPickup이 매 프레임 개입하지 않아도 광채가 알아서 계속
// 숨쉬게 하기 위한 것이다.
public class DropPickupAuraMotion : MonoBehaviour
{
    // 아이템 자체의 등장 애니메이션과 맞춰서, 메시가 아직 커지는 중인데 광채만
    // 갑자기 최고 강도로 켜지지 않고 아이템과 함께 나타나도록 한다.
    [SerializeField] private float fadeInDuration = 0.25f;
    // 지속적인 숨쉬기 진폭. 원래 0.14였는데, 그 진폭으로는 맥동이 아이템의 약 1.5초 수명 동안
    // 눈에 거의 보이지 않아 더 깊게 잡았다. 그래도 스트로브가 아닌 은은한 부풀림 정도다 -
    // 날카로운 "반짝"은 반짝임(sparkle)들의 몫이다(DropPickupSparkle 참고).
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
        // material이 아니라 sharedMaterial: SpawnAura가 이 아이템 전용 머티리얼을 이미 할당해뒀는데,
        // 인스턴싱 게터는 *복사본*을 돌려주고 DropPickup의 OnDestroy는 그 복사본을
        // 모르므로 절대 정리되지 않는다.
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
