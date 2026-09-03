using UnityEngine;

public class UltimateGaugeView : MonoBehaviour
{
    [SerializeField] private Transform fillTransform;

    // Fill은 부모의 절반 너비 오프셋에 위치한 중심 피벗 쿼드다 (HealthBarView의 구성 방식과
    // 동일). 런타임 transform에서 값을 캡처하지 않고 여기서 직접 값을 지정하는 이유는,
    // SetFraction 자체가 이 transform의 scale/position을 덮어써 버려서 첫 호출 이후에는
    // 런타임 캡처로 되읽을 값이 남아있지 않기 때문이다.
    [SerializeField] private float fullWidth = 0.8f;

    public void SetFraction(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);

        Vector3 scale = fillTransform.localScale;
        scale.x = fullWidth * fraction;
        fillTransform.localScale = scale;

        Vector3 pos = fillTransform.localPosition;
        pos.x = fullWidth * 0.5f * fraction;
        fillTransform.localPosition = pos;
    }
}
