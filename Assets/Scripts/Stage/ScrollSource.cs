using UnityEngine;

// 배경 레이어들의 공통 스크롤 구동. 흐른 거리만 계산해 넘기고, 무엇을 움직일지는 하위가 정한다.
public abstract class ScrollSource : MonoBehaviour
{
    // 노이즈를 읽어나가는 속도 -> 출렁임의 주기
    private const float PaceNoiseSpeed = 0.15f;

    // 레이어끼리 같은 리듬으로 출렁여야 한다 -> 시드를 공유한다.
    // static 초기화자에서는 Random 호출이 막혀 있어 첫 Update에서 한 번만 잡는다.
    private static float paceSeed = -1f;
    private static float PaceSeed
    {
        get
        {
            if (paceSeed < 0f) paceSeed = Random.Range(0f, 100f);
            return paceSeed;
        }
    }

    // 단위는 하위 구현마다 다르다 (월드 거리 / UV 오프셋)
    [SerializeField] private float scrollSpeed = 1f;
    [SerializeField] private float speedEaseTime = 0.14f;
    [SerializeField] private float paceVariation = 0.08f;
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    private float speedFactor;
    private float speedFactorVelocity;

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

        float pace = 1f + (Mathf.PerlinNoise(Time.time * PaceNoiseSpeed + PaceSeed, 0f) - 0.5f) * 2f * paceVariation;
        Scroll(scrollSpeed * pace * speedFactor * Time.deltaTime);
    }

    // 이번 프레임에 흐른 양
    protected abstract void Scroll(float step);
}
