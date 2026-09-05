using UnityEngine;

public class DamagePopupMotion : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float riseDistance = 0.25f;
    [SerializeField] private float popStartScale = 0.4f;

    private TextMesh label;
    private Vector3 startPos;
    private float elapsed;

    private void Awake()
    {
        label = GetComponent<TextMesh>();
        startPos = transform.localPosition;
        transform.localScale = Vector3.one * popStartScale;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float popT = Mathf.Clamp01(t / 0.2f);
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(popStartScale, 1f, Easing.OutBack(popT));

        transform.localPosition = startPos + Vector3.up * (riseDistance * Easing.OutQuad(t));

        float alpha = 1f - Mathf.Clamp01((t - 0.30f) / 0.70f);
        if (label != null)
        {
            Color c = label.color;
            c.a = alpha;
            label.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
