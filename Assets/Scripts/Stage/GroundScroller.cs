using UnityEngine;

// 순전히 시각 효과일 뿐: 플레이어와 바닥은 실제로는 전혀 움직이지 않으므로, 바닥 텍스처를
// 플레이어 발밑에서 뒤로 스크롤시켜 전진하는 것처럼 눈속임한다. 실제로 이동감을 만들어내는
// 것은 스크롤되는 배경 나무들(BackdropScroller)이므로, 바닥 자체는 눈에 띄는 타일링 패턴이
// 필요 없이 단색으로 남아있어도 된다.
public class GroundScroller : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private float speedEaseTime = 0.4f;
    [SerializeField] private float paceVariation = 0.08f;
    // SmoothDamp는 목표값에 점근적으로 접근하기 때문에, 마지막 남은 속도 한 조각이
    // speedEaseTime이 암시하는 것보다 훨씬 오래 남아있는다 - 이는 캐릭터가 눈에 띄게 멈춘
    // 후에도 바닥이 계속 미끄러지는 것처럼 보이게 만든다. 이 값 아래로 내려가면 정지가
    // 끝난 것으로 간주하고 0으로 스냅시켜 그 꼬리를 잘라낸다.
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    private Material material;
    private Vector2 offset;

    // BackdropScroller와 동일한 방식, 동일한 이유로 이징 처리한다: IsScrolling이 캐릭터가
    // 달리기를 시작/정지할 때마다 곧바로 최고 속도로 전환되면, 걷기로 자연스럽게 전환되는
    // 것이 아니라 발밑의 바닥이 갑자기 덜컹거리는 것처럼 보인다.
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

        // 플레이어는 +X 방향으로 달리므로, 전체 세계는 -X 방향으로 이동하는 것처럼 보여야 한다:
        // BackdropScroller도 세그먼트를 그 방향으로 움직이며, 바닥도 이에 맞춰야 한다.
        //
        // 여기서 빼기가 맞는 이유는 서로 상쇄되는 두 번의 부호 반전 때문이다:
        //   1. 텍스처의 한 지점은 uv_mesh = (feature - offset) / tiling 위치에 있으므로,
        //      offset과 반대 방향으로 움직이고,
        //   2. Unity 내장 Plane은 +X를 따라 u가 감소한다(-5 모서리가 u=1, +5 모서리가 u=0),
        //      그래서 uv_mesh가 낮을수록 월드 X는 더 커진다.
        // 결과적으로: offset을 줄이면 -> feature는 -X로 이동하며, 이것이 우리가 원하는 결과다.
        // 이걸 +=로 바꾸면 바닥은 오른쪽으로 가는데 그 위의 모든 것은 왼쪽으로 가버렸다.
        float pace = 1f + (Mathf.PerlinNoise(Time.time * 0.15f + paceSeed, 0f) - 0.5f) * 2f * paceVariation;
        offset.x -= scrollSpeed * pace * speedFactor * Time.deltaTime;
        material.SetTextureOffset("_BaseMap", offset);
    }
}
