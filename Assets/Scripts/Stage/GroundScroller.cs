using UnityEngine;

public class GroundScroller : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private float speedEaseTime = 0.4f;
    [SerializeField] private float paceVariation = 0.08f;
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    private Material material;
    private Vector2 offset;

    private float speedFactor;
    private float speedFactorVelocity;
    private float paceSeed;

    private void Awake()
    {
        material = GetComponent<Renderer>().material;
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
        offset.x -= scrollSpeed * pace * speedFactor * Time.deltaTime;
        material.SetTextureOffset("_BaseMap", offset);
    }
}
