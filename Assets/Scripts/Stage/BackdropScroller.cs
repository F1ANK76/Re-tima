using UnityEngine;

public class BackdropScroller : MonoBehaviour
{
    [SerializeField] private Transform[] segments;
    [SerializeField] private float scrollSpeed = 0.6f;
    [SerializeField] private float segmentWidth = 14f;
    [SerializeField] private float recycleDistance = 21f;
    [SerializeField] private float speedEaseTime = 0.4f;
    [SerializeField] private float paceVariation = 0.08f;
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    private float speedFactor;
    private float speedFactorVelocity;
    private float paceSeed;

    private void Awake()
    {
        paceSeed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        float targetFactor = IsScrolling ? 1f : 0f;
        speedFactor = Mathf.SmoothDamp(speedFactor, targetFactor, ref speedFactorVelocity, speedEaseTime);

        // 정지하는 동안에만 스냅한다 - 속도가 올라가는 도중에 스냅하면 이즈인 효과가 죽어버린다.
        if (!IsScrolling && speedFactor < stopSnapThreshold)
        {
            speedFactor = 0f;
            speedFactorVelocity = 0f;
        }
        if (speedFactor < 0.0001f) return;

        float pace = 1f + (Mathf.PerlinNoise(Time.time * 0.15f + paceSeed, 0f) - 0.5f) * 2f * paceVariation;
        float wrapDistance = segmentWidth * segments.Length;
        float step = scrollSpeed * pace * speedFactor * Time.deltaTime;

        foreach (Transform segment in segments)
        {
            Vector3 pos = segment.position;
            pos.x -= step;
            if (pos.x < -recycleDistance) pos.x += wrapDistance;
            segment.position = pos;
        }
    }
}
