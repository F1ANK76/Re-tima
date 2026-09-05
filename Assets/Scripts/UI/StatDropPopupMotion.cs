using UnityEngine;

public class StatDropPopupMotion : MonoBehaviour
{
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private float riseDistance = 0.3f;
    [SerializeField] private float popStartScale = 0.4f;
    [SerializeField] private float opaqueFraction = 0.55f;

    private TextMesh[] labels;
    private Color[] baseColors;
    private Vector3 startPos;
    private float elapsed;

    private void Awake()
    {
        labels = GetComponentsInChildren<TextMesh>();
        baseColors = new Color[labels.Length];
        for (int i = 0; i < labels.Length; i++) baseColors[i] = labels[i].color;

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

        float alpha = 1f - Mathf.Clamp01((t - opaqueFraction) / Mathf.Max(0.0001f, 1f - opaqueFraction));
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            Color c = baseColors[i];
            c.a = alpha;
            labels[i].color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
