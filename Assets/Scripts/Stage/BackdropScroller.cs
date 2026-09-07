using UnityEngine;

public class BackdropScroller : MonoBehaviour
{
    [SerializeField] private Transform[] segments;
    [SerializeField] private float scrollSpeed = 3f;
    [SerializeField] private float speedEaseTime = 0.14f;
    [SerializeField] private float paceVariation = 0.08f;
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    private float speedFactor;
    private float speedFactorVelocity;
    private float paceSeed;

    // 세그먼트 배치에서 유도 -> 배치를 옮겨도 값이 어긋나지 않는다
    private float wrapDistance;
    private float recycleDistance;

    private void Awake()
    {
        paceSeed = Random.Range(0f, 100f);

        float segmentWidth = segments[1].position.x - segments[0].position.x;
        wrapDistance = segmentWidth * segments.Length;
        recycleDistance = wrapDistance * 0.5f;
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
