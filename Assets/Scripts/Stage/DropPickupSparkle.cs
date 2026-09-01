using UnityEngine;

// 별빛 반짝임 하나의 깜빡임. localScale만 제어한다 - 아이템의 모든 반짝임이 단일 머티리얼을
// 공유하므로, 별마다 머티리얼 인스턴스를 만들지 않고 각자 위상으로 깜빡이게 하려면
// 스케일을 애니메이션하는 방법을 쓴다.
public class DropPickupSparkle : MonoBehaviour
{
    // 사인을 높은 거듭제곱으로 올려 부드러운 파형을 좁은 스파이크로 바꾼다: 긴 어두운 간격
    // 사이의 짧고 빠른 섬광, 이것이 맥동과 반짝임을 구분짓는다.
    private const float BlinkSharpness = 5f;
    // 절대 완전히 사라지지 않는다; 별이 정말 아무것도 없는 상태에서 갑자기
    // 튀어나오면 마치 오류(글리치)처럼 보인다.
    private const float MinScaleFactor = 0.12f;

    private float restScale;
    private float blinkSpeed;
    private float phase;
    private float elapsed;

    public void Initialize(float restScale, float blinkSpeed, float phase)
    {
        this.restScale = restScale;
        this.blinkSpeed = blinkSpeed;
        this.phase = phase;

        transform.localScale = Vector3.one * (restScale * MinScaleFactor);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float wave = Mathf.Sin((elapsed * blinkSpeed + phase) * Mathf.PI * 2f) * 0.5f + 0.5f;
        float spike = Mathf.Pow(wave, BlinkSharpness);

        transform.localScale = Vector3.one * (restScale * Mathf.Lerp(MinScaleFactor, 1f, spike));
    }
}
