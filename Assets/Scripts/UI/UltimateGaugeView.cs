using UnityEngine;

public class UltimateGaugeView : MonoBehaviour
{
    [SerializeField] private Transform fillTransform;

    // Fill is a center-pivoted quad sitting at its parent's half-width offset (mirrors
    // HealthBarView's construction) - authored here rather than captured from the live
    // transform, since SetFraction itself overwrites that transform's scale/position and
    // a runtime capture would have nothing left to read back after the first call.
    [SerializeField] private float fullWidth = 0.8f;

    public void SetFraction(float fraction)
    {
        if (fillTransform == null) return;

        fraction = Mathf.Clamp01(fraction);

        Vector3 scale = fillTransform.localScale;
        scale.x = fullWidth * fraction;
        fillTransform.localScale = scale;

        Vector3 pos = fillTransform.localPosition;
        pos.x = fullWidth * 0.5f * fraction;
        fillTransform.localPosition = pos;
    }
}
