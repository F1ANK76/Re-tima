using UnityEngine;

public class HealthBarView : MonoBehaviour
{
    [SerializeField] private Transform fillTransform;

    public void SetHealth(float current, float max)
    {
        if (fillTransform == null || max <= 0f) return;

        float fraction = Mathf.Clamp01(current / max);
        Vector3 scale = fillTransform.localScale;
        scale.x = fraction;
        fillTransform.localScale = scale;
    }
}
