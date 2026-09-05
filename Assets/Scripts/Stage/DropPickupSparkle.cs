using UnityEngine;

public class DropPickupSparkle : MonoBehaviour
{
    private const float BlinkSharpness = 5f;
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
