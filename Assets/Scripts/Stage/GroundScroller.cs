using UnityEngine;

// 순전히 시각 효과: 플레이어도 바닥도 실제로는 움직이지 않으므로, 바닥 텍스처를 발밑에서 뒤로
// 스크롤해 전진하는 것처럼 눈속임한다. 실제 이동감은 배경 나무(BackdropScroller)가 만들어내므로
// 바닥은 눈에 띄는 타일링 패턴 없이 단색으로 남아도 된다.
public class GroundScroller : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private float speedEaseTime = 0.4f;
    [SerializeField] private float paceVariation = 0.08f;
    // SmoothDamp는 목표값에 점근적으로 접근해서 마지막 속도 한 조각이 speedEaseTime이 암시하는
    // 것보다 훨씬 오래 남는다 - 캐릭터가 눈에 띄게 멈춘 후에도 바닥이 계속 미끄러져 보인다.
    // 이 값 아래면 정지가 끝난 것으로 간주하고 0으로 스냅해 그 꼬리를 잘라낸다.
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    private Material material;
    private Vector2 offset;

    // BackdropScroller와 같은 방식, 같은 이유로 이징한다: IsScrolling이 캐릭터의 달리기 시작/정지마다
    // 곧바로 최고 속도로 전환되면, 걷기로 자연스럽게 넘어가는 게 아니라 발밑 바닥이 덜컹거려 보인다.
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

        // 플레이어가 +X로 달리므로 세계 전체가 -X로 움직여 보여야 한다: BackdropScroller도 세그먼트를
        // 그 방향으로 옮기니 바닥도 맞춰야 한다.
        //
        // 빼기가 맞는 이유는 서로 상쇄되는 두 번의 부호 반전이다:
        //   1. 텍스처의 한 지점은 uv_mesh = (feature - offset) / tiling에 있어 offset과 반대로 움직인다.
        //   2. Unity 내장 Plane은 +X를 따라 u가 감소한다(-5 모서리 u=1, +5 모서리 u=0). uv_mesh가
        //      낮을수록 월드 X가 커진다.
        // 즉 offset을 줄이면 feature가 -X로 가며, 이게 우리가 원하는 결과다. +=로 바꾸면 바닥만
        // 오른쪽으로 가고 그 위의 모든 것은 왼쪽으로 가버렸다.
        float pace = 1f + (Mathf.PerlinNoise(Time.time * 0.15f + paceSeed, 0f) - 0.5f) * 2f * paceVariation;
        offset.x -= scrollSpeed * pace * speedFactor * Time.deltaTime;
        material.SetTextureOffset("_BaseMap", offset);
    }
}
