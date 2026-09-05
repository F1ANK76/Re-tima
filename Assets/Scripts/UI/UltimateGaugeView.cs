using UnityEngine;

public class UltimateGaugeView : MonoBehaviour
{
    [SerializeField] private Transform fillTransform;

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
