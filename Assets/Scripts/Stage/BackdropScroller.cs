using UnityEngine;

// 고정된 장식 세그먼트 집합을 -X 방향으로 함께 이동시키고, 왼쪽 가장자리를 완전히 지나간
// 세그먼트는 오른쪽으로 재활용하여, 전체 경로에 걸친 고유한 지오메트리 없이도 배경이
// 영원히 반복되는 것처럼 보이게 한다.
public class BackdropScroller : MonoBehaviour
{
    [SerializeField] private Transform[] segments;
    [SerializeField] private float scrollSpeed = 0.6f;
    [SerializeField] private float segmentWidth = 14f;
    [SerializeField] private float recycleDistance = 21f;
    [SerializeField] private float speedEaseTime = 0.4f;
    [SerializeField] private float paceVariation = 0.08f;
    // GroundScroller 참고: SmoothDamp의 점근적 꼬리 때문에 캐릭터가 멈춘 후에도 배경이
    // 한참 동안 계속 기어간다. 마지막 남은 값을 0으로 스냅하면 정지가 깔끔하게 끝나며,
    // 두 스크롤러가 같은 임계값을 사용하므로 두 레이어가 함께 멈춘다.
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    // IsScrolling은 캐릭터가 달리기를 시작/정지할 때마다 계속 켜졌다 꺼졌다 한다
    // (CombatLoop 참고). 매번 곧바로 최고 속도로 스냅되면 마치 기계식 컨베이어 벨트처럼
    // 보인다. 대신 이 값을 이징 처리하면 시작/정지가 실제 가속처럼 느껴지고, 여기에
    // 더해진 펄린 노이즈 흔들림은 완전히 달리는 중에도 일정한 걸음걸이가 지나치게
    // 기계적으로 느껴지지 않도록 해준다.
    private float speedFactor;
    private float speedFactorVelocity;
    private float paceSeed;

    private void Awake()
    {
        paceSeed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        if (segments == null || segments.Length == 0) return;

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
