using System.Collections;
using Benjathemaker;
using UnityEngine;

// 기존의 즉시 적용 방식 대신 StatDropManager가 스폰한다. 순서대로 세 박자로 진행된다:
//   1. 몬스터를 지나쳐 던져진 물약(ATK는 RedVial, HP는 GreenVial - SpawnVisual 참고)이
//      바닥에 튕기며 안착하는 동안 플레이어는 완전히 멈춰(idle) 있는다.
//   2. 물약이 안착하는 순간, 플레이어는 다시 움직임 상태로 풀려난다.
//   3. 물약이 지면 높이에서 남은 거리를 좁혀 접촉 시 효과를 지급한다.
// 이 게임에서 플레이어는 실제로는 어디로도 걸어가지 않는다(CombatLoop의 "달리기"는
// 제자리 고정 애니메이션이며, 이동한다는 착각은 GroundScroller/BackdropScroller가
// 만들어낸다 - 그쪽 참고), 그래서 3번 박자는 대신 물약을 움직여야 한다: 물약은
// 몬스터가 걷는 것과 정확히 같은 속도로, 바닥을 따라 평평하게, 일정한 속도로
// 미끄러져 들어오는데, 이는 곧 달리는 플레이어를 향해 세계가 다가오는 것과 같다.
// 의도적으로 유도/자석 끌림 방식이 아니다 - 플레이어를 향해 가속하는 것도, 바닥에서
// 떠오르는 것도 전혀 없다. 왜냐하면 여기서 읽혀야 하는 것은 물약이 플레이어에게
// 날아가는 게 아니라 플레이어가 그 위를 달려서 지나가는 것이기 때문이다.
public class StatPotionPickup : MonoBehaviour
{
    [Header("Toss + bounce (beat 1)")]
    // 던져지고 안착하기까지 전체 과정, 시작부터 정지까지.
    [SerializeField] private float landDuration = 0.75f;
    // 사망 지점을 지나 플레이어 반대 방향으로 얼마나 멀리 던져지는지 - 몬스터는
    // 그 둘 사이에서 죽으므로, 이 값이 물약을 몬스터 "뒤편"에 놓이게 만드는 요인이다.
    [SerializeField] private float tossDistance = 1.4f;
    // 첫 포물선 궤적의 정점. 이후의 모든 재반동은 이 값의 일부다(HopHeights 참고).
    [SerializeField] private float tossHeight = 1.1f;

    [Header("Run-over (beats 2-3)")]
    // 2번 박자: 안착한 물약이 이 시간만큼 그대로 정지해 있는다. GroundScroller/
    // BackdropScroller는 곧바로 최고 속도로 스냅되지 않고 약 0.4초에 걸쳐 스크롤
    // 속도를 이징한다. 즉 플레이어는 풀려난 직후에도 잠시 동안 속도를 붙이는 중인데,
    // 그동안 물약이 가만히 멈춰있어야 둘이 같은 동작으로 읽힌다.
    [SerializeField] private float settleHoldDuration = 0.2f;
    // 발밑에 있다고 간주할 만큼 충분히 가까운 거리. 평면(XZ만)으로 비교한다: 물약은
    // 바닥에 놓여있는 반면 플레이어의 transform은 캡슐의 중심이므로, 진짜 3D 거리로
    // 비교하면 이렇게 작은 값까지 절대 내려가지 않는다.
    [SerializeField] private float pickupRadius = 0.45f;
    // 그저 안전장치일 뿐이다. 접근 속도라면 이만큼 오래 걸릴 일은 원래 없어야 한다;
    // 이게 없으면 player가 null이거나 도달 불가능할 때 물약이 영원히 미끄러지게 된다.
    [SerializeField] private float approachTimeout = 6f;

    // StatDropManager가 StageConfigSO.monsterMoveSpeed 값으로 설정한다 - 물약은
    // 몬스터가 걸어 들어오는 것과 같은 속도로 거리를 좁혀야 한다. 그렇지 않으면
    // 플레이어가 두 가지 다른 속도로 달리는 것처럼 보이게 된다.
    private float approachSpeed = 5f;

    [Header("Visual (per stat type)")]
    // ATK는 RedVial로, HP는 GreenVial로 드롭된다 - 물약 자체의 색은 완전히 그대로
    // 둔다(등급 틴트/발광 없음), 그래야 등급에 관계없이 ATK인지 HP인지 한눈에
    // 구분된다. 희귀도는 대신 아래의 아우라에서 전적으로 표현된다.
    [SerializeField] private GameObject atkVisualPrefab;
    [SerializeField] private GameObject hpVisualPrefab;
    // 두 물약 모두 스케일 1일 때 높이가 약 2.23이다(예전 아이콘 메시보다 훨씬
    // 크고 얇다) - 이것이 등급 램프(GetPotionScale)가 그 이후로 확대해나가는
    // 기준이 되는 "Normal" 크기다.
    [SerializeField] private float visualBaseScale = 0.4f;

    [Header("Grade aura")]
    // 세 아우라 레이어 모두 GradeVisuals.GetAuraStrength(Normal일 때 0.2, Legendary일 때
    // 1)를 이 상한값들에 곱해서 구동되므로, 등급은 크기만이 아니라 광채의 깊이로도
    // 표현된다. 크기는 의도적으로 이 램프에 포함시키지 않았다 - 물약 메시는 이미
    // 등급에 따라 커지고 있어서(GetPotionScale), 헤일로까지 그 위에 함께 키우면
    // 중복되어 버린다.
    [SerializeField] private float auraSize = 1.5f;
    // 가산 헤일로 밝기. 등급 색상에 곱해지므로, Normal 드롭은 흐릿한 흰색 번짐으로,
    // Legendary는 짙은 초록빛 블룸으로 보인다.
    [SerializeField] private float auraBrightnessMax = 1.7f;
    // 물약은 주변 바닥에도 색이 있는 빛을 던진다 - 이것이 뒤에 데칼을 붙여놓은 게
    // 아니라 실제로 아우라를 *내뿜는* 것처럼 보이게 만드는 요소다.
    [SerializeField] private float auraLightIntensityMax = 3f;
    [SerializeField] private float auraLightRange = 2.6f;

    [Header("Twinkle sparkles")]
    // 물약 주위를 도는 별빛 반짝임들로, 각각 서로 다른 위상 오프셋으로 깜빡여서
    // 항상 무언가는 빛을 반사하고 있는 것처럼 보인다 - 헤일로 하나가 맥동하면
    // "빛나는" 느낌이지만, 이건 "반짝반짝"하는 느낌을 준다. 동기화하지 않고
    // 어긋나게 배치한다: 별 네 개가 동시에 번쩍이면 반짝임이 아니라 그냥 깜빡이는
    // 조명 하나처럼 보이게 된다.
    [SerializeField] private int sparkleCount = 4;
    // 루트 로컬 단위이므로, 물약 자체의 등급 스케일에 맞춰 고리도 함께 넓어진다.
    [SerializeField] private float sparkleOrbitRadius = 0.5f;
    [SerializeField] private float sparkleSize = 0.6f;
    // 가산 별들은 이 크기에서 반짝임으로 인지되려면 부드러운 헤일로보다 더 강한
    // 임팩트가 필요하다.
    [SerializeField] private float sparkleBrightnessMax = 2.4f;
    // 별 하나당 초당 완전한 깜빡임 사이클 수.
    [SerializeField] private float sparkleBlinkSpeed = 1.1f;
    // 고리 전체가 천천히 회전하여, 반짝임들이 화면상 고정된 위치에 박혀있지 않게 한다.
    [SerializeField] private float sparkleOrbitSpeed = 35f;

    // 연속된 도약: 첫 번째 던지기, 그 다음 점점 줄어드는 재반동들. 각 도약은 정확히
    // 지면 높이에서 시작해 지면 높이로 돌아오는 사인 곡선 궤적이라, 튕길 때마다
    // 바닥에서 뚝뚝 끊기는 형태가 아니라 끊김 없이 이어지는 접촉으로 보인다.
    // 지속시간들의 합은 1이다.
    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private Renderer[] renderers;
    private StatType statType;
    private StatGrade grade;
    private float amount;
    private Transform player;
    private CombatLoop combatLoop;
    // PushIdleHold가 호출되었는데 아직 PopIdleHold로 짝이 맞춰지지 않았는지를 추적한다 -
    // 아래의 HandlePlayerDied가 TossThenRunOver를 세 박자 중 어느 시점에서든
    // StopAllCoroutines로 끊어버릴 수 있기 때문에 필요하다. 던지는 도중 자신의
    // PopIdleHold 호출에 도달하기도 전에 끊기는 경우도 포함된다. 이게 없으면 던지는
    // 도중 죽었을 때 CombatLoop의 idle hold가 영구히 켜진 채로 남아, 리스폰 후에도
    // 플레이어가 다시는 달릴 수 없게 된다.
    private bool idleHoldActive;
    private Vector3 restScale;
    // restScale 상태에서 피벗부터 메시 바닥까지의 거리로, 추측이 아니라 실측한 값이다 -
    // 등급이 높을수록 메시는 피벗을 중심으로 양방향으로 커지므로, 고정된 상승값을
    // 쓰면 큰 물약일수록 커진 만큼 바닥 아래로 파묻혀버린다. 인스턴스마다 이 값을
    // 직접 측정하면 등급별로 손으로 조정할 상수 없이도 모든 크기에서 바닥에 딱
    // 맞게 놓인다.
    private float restBottomOffset;
    // 인스턴스마다 코드로 생성된다(등급별로 색조가 다르므로), 이 오브젝트가 사라질
    // 때 다른 누구도 대신 수거해주지 않는다 - OnDestroy 참고.
    private Material auraMaterial;
    // 이 물약의 모든 반짝임이 공유하는 재질 하나 - auraMaterial과 같은 이유로,
    // 코드로 생성되어 다른 누구도 대신 수거해주지 않는다(OnDestroy 참고).
    private Material sparkleMaterial;

    public void Initialize(StatType statType, StatGrade grade, float amount, Transform player, CombatLoop combatLoop, float approachSpeed)
    {
        this.statType = statType;
        this.grade = grade;
        this.amount = amount;
        this.player = player;
        this.combatLoop = combatLoop;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;

        SpawnVisual();
        // 프리팹에 제작된 그대로의 스케일이 Normal 크기다 - 더 희귀한 등급은 단지 색만
        // 다른 게 아니라 그 위에 눈에 띄게 더 큰 물약으로 보인다.
        restScale = transform.localScale * GradeVisuals.GetPotionScale(grade);

        transform.localScale = restScale;
        restBottomOffset = ComputeBottomOffset();

        // 반드시 ComputeBottomOffset 이후에 호출해야 한다: 헤일로 쿼드는 물약보다 훨씬
        // 아래까지 뻗어있어서, 그 바운드까지 포함해서 측정하면 물약 전체가 자기 자신의
        // 광채 높이만큼 공중에 떠버린다.
        SpawnAura();

        // 여기서 1번 박자가 시작된다: CombatLoop.Update()가 이 값을 읽어, 물약이 아직
        // 공중에 있는 동안에는 플레이어를 계속 달리게 두지 않고 idle 상태로 붙잡아둔다.
        if (combatLoop != null)
        {
            combatLoop.PushIdleHold();
            idleHoldActive = true;
        }

        StartCoroutine(TossThenRunOver());
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    // 몬스터 처치와 플레이어 자신의 사망이 같은 순간에 일어날 수 있다 - 방금 이걸
    // 떨어뜨린 몬스터가 플레이어를 죽인 것과 같은 타격에 죽는 경우다. 그냥 두면 이
    // 픽업의 코루틴은 StageManager의 사망 시퀀스와 완전히 무관하게 계속 실행되고
    // (StageManager의 StopAllCoroutines는 자기 자신의 코루틴에만 영향을 미치지, 이
    // 오브젝트의 코루틴에는 미치지 않는다), 그래서 물약이 이미 죽은 플레이어에게
    // 계속 미끄러져 들어가고 심지어 스탯까지 지급해버렸다. 여기서 제자리에 멈춰
    // 세우는 것이 사망 시퀀스가 실제로 보여줘야 하는 모습이다.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }

    // 현재 적용된 스케일 기준으로, 이 transform의 피벗에서부터 렌더링된 메시의
    // 바닥까지의 거리. 비주얼이 여러 파츠로 이루어진 경우를 대비해 모든 렌더러를
    // 합산한다.
    private float ComputeBottomOffset()
    {
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        return transform.position.y - combined.min.y;
    }

    // 이 드롭의 스탯 타입에 맞는 RedVial/GreenVial을 자식 오브젝트로 인스턴스화한다 -
    // 이제는 손으로 색을 입힌 공용 아이콘 메시가 아니라 애셋 자체가 그 정체성을
    // 갖고 있다.
    private void SpawnVisual()
    {
        GameObject prefab = statType == StatType.Attack ? atkVisualPrefab : hpVisualPrefab;
        if (prefab == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            return;
        }

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * visualBaseScale;

        // 이 애셋에는 자체적인 idle 회전/부유/스케일 루프가 기본으로 딸려있다 - 아래의
        // PlayToss/PlayRunOver가 이미 매 프레임 이 오브젝트의 위치와 스케일을 제어하고
        // 있으므로, 이걸 그대로 두면 둘이 같은 transform을 두고 서로 충돌하게 된다.
        SimpleGemsAnim anim = visual.GetComponent<SimpleGemsAnim>();
        if (anim != null) Destroy(anim);

        renderers = visual.GetComponentsInChildren<Renderer>();
    }

    // 빌보드 오브젝트 하나에 두 레이어: 가산 헤일로 쿼드(눈에 보이는 광채)와
    // 포인트 라이트(바닥에 던지는 색이 있는 빛). 둘 다 색상은 등급에서, 세기는
    // GetAuraStrength에서 가져온다.
    private void SpawnAura()
    {
        float strength = GradeVisuals.GetAuraStrength(grade);
        Color color = GradeVisuals.GetColor(grade);

        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
        aura.name = "GradeAura";
        // CreatePrimitive는 콜라이더를 기본으로 붙여서 나온다; 이건 순전히 시각적인
        // 오브젝트일 뿐이고 여기서는 물리를 전혀 사용하지 않는다(픽업 판정은 거리
        // 계산이다, PlayRunOver 참고).
        Destroy(aura.GetComponent<Collider>());

        aura.transform.SetParent(transform, false);
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localScale = Vector3.one * auraSize;

        auraMaterial = CreateAdditiveGlowMaterial(color * (auraBrightnessMax * strength));
        aura.GetComponent<MeshRenderer>().material = auraMaterial;

        Light light = aura.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = auraLightIntensityMax * strength;
        light.range = auraLightRange;
        // 그림자 끔: 이건 그냥 광채일 뿐인데, 작은 소품이 전투 위에 실제 그림자를
        // 드리우면 "은은하게"가 허용하는 것보다 훨씬 더 시선을 빼앗는다(게다가
        // 섀도우맵 비용도 든다).
        light.shadows = LightShadows.None;

        aura.AddComponent<Billboard>();
        aura.AddComponent<StatPotionAuraMotion>();

        SpawnSparkles(color, strength);
    }

    // 깜빡이는 별빛들로 이루어진 고리. 모두 하나의 머티리얼을 공유하며(같은
    // 색조), 그래서 별마다의 반짝임은 순전히 transform 스케일만으로 표현된다 -
    // 색상을 애니메이션시키기 위해 별마다 머티리얼 인스턴스를 따로 만들 필요
    // 없이 독립적인 위상을 가질 수 있다.
    private void SpawnSparkles(Color color, float strength)
    {
        if (sparkleCount <= 0) return;

        sparkleMaterial = CreateAdditiveGlowMaterial(color * (sparkleBrightnessMax * strength), SparkleTexture);

        var ring = new GameObject("SparkleRing");
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.AddComponent<StatPotionSparkleRing>().Initialize(sparkleOrbitSpeed);

        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject sparkle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sparkle.name = "Sparkle" + i;
            Destroy(sparkle.GetComponent<Collider>());

            float angle = (360f / sparkleCount) * i;
            sparkle.transform.SetParent(ring.transform, false);
            sparkle.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * (Vector3.up * sparkleOrbitRadius);
            sparkle.transform.localScale = Vector3.one * sparkleSize;

            sparkle.GetComponent<MeshRenderer>().material = sparkleMaterial;

            sparkle.AddComponent<Billboard>();
            // 위상을 고리 전체에 분산시킨다: 별 i는 별 i-1보다 1/count 사이클만큼 늦게
            // 시작하므로, 깜빡임들이 동시에 번쩍이지 않고 서로를 쫓아가듯 이어진다.
            sparkle.AddComponent<StatPotionSparkle>()
                   .Initialize(sparkleSize, sparkleBlinkSpeed, (float)i / sparkleCount);
        }
    }

    private static Material CreateAdditiveGlowMaterial(Color tint, Texture2D texture = null)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        // 가산 투명: 이 머티리얼은 평소 그 값들을 자동으로 맞춰주는 셰이더 GUI가 아니라
        // 코드로 생성되므로, URP에서는 surface/blend 속성과 이에 맞는 키워드/큐 설정을
        // 직접 손으로 맞춰줘야 한다.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        // 양면 렌더링: Billboard는 로컬 +Z가 카메라 반대쪽을 향하도록 정렬하므로, 쿼드의
        // 어느 면이 결국 뷰어를 향하는지는 그 규약에 달려있다 - 양면을 모두 그리면
        // 어느 쪽이든 헤일로가 확실히 보인다.
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetTexture("_BaseMap", texture != null ? texture : GlowTexture);
        mat.SetColor("_BaseColor", tint);

        return mat;
    }

    // 모든 물약의 모든 반짝임이 공유하는 4갈래 별빛 텍스처. 부드러운 방사형 뭉치
    // (GlowTexture)는 그냥 램프처럼 보인다; 아래의 십자 모양 감쇠가 이 크기에서
    // 실제로 반짝임처럼 보이게 만드는 요소다.
    private static Texture2D sparkleTexture;
    private static Texture2D SparkleTexture
    {
        get
        {
            if (sparkleTexture != null) return sparkleTexture;

            const int size = 64;
            sparkleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 쿼드의 절반 크기로 정규화하여, 뾰족한 끝이 가장자리까지 닿도록 한다.
                    float dx = Mathf.Abs(x - center) / center;
                    float dy = Mathf.Abs(y - center) / center;

                    // 십자로 교차하는 두 개의 바늘: 각 팔은 자기 축을 따라서는 밝음을 유지하지만
                    // 그 축을 가로지르는 방향으로는 급격히 감쇠한다. 이것이 마름모가 아니라
                    // 별 모양의 뾰족함을 만들어내는 요소다.
                    float horizontal = Falloff(dx) * Needle(dy);
                    float vertical = Falloff(dy) * Needle(dx);
                    // 둥근 중심부를 두어, 팔들이 이음매가 아니라 밝은 중심에서 만나도록 한다.
                    float core = Falloff(Mathf.Sqrt(dx * dx + dy * dy) * 2.6f);

                    float v = Mathf.Clamp01(horizontal + vertical + core);
                    pixels[y * size + x] = new Color(v, v, v, v);
                }
            }

            sparkleTexture.SetPixels(pixels);
            sparkleTexture.Apply();
            return sparkleTexture;
        }
    }

    // [0,1] 구간에서 1->0으로 부드럽게 감쇠하며, 더 좁은 감쇠를 위해 제곱한다.
    private static float Falloff(float d)
    {
        float f = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
        return f * f;
    }

    // 팔의 폭을 가로질러 매우 좁게 감쇠한다 - 축에서 조금만 벗어나도 거의
    // 즉시 사라진다.
    private static float Needle(float d) => Mathf.Clamp01(1f - Mathf.Clamp01(d) * 14f);

    // 지금까지 드롭된 모든 물약이 공유하는 하나의 부드러운 방사형 감쇠 텍스처 -
    // 텍스처 자체는 전부 동일하고, 색조만 다르다(그건 인스턴스별 머티리얼에 있다).
    private static Texture2D glowTexture;
    private static Texture2D GlowTexture
    {
        get
        {
            if (glowTexture != null) return glowTexture;

            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // 제곱한 smoothstep: 밝은 중심부가 쿼드의 가장자리에 닿기 훨씬 전에 이미
                    // 0으로 감쇠되어, 헤일로에 딱딱한 사각형 경계가 절대 보이지 않는다.
                    float falloff = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(r));
                    falloff *= falloff;

                    pixels[y * size + x] = new Color(falloff, falloff, falloff, falloff);
                }
            }

            glowTexture.SetPixels(pixels);
            glowTexture.Apply();
            return glowTexture;
        }
    }

    private IEnumerator TossThenRunOver()
    {
        yield return PlayToss();

        // 2번 박자: 던지는 동안은 "제자리에 머물기"가 주도권을 갖고 있었다; 이를
        // 풀어주면 플레이어는 곧바로 CombatLoop의 기본 상태인 "범위 내에 아무것도
        // 없으면 계속 이동"으로 돌아간다.
        if (combatLoop != null) combatLoop.PopIdleHold();
        idleHoldActive = false;

        yield return new WaitForSeconds(settleHoldDuration);
        yield return PlayRunOver();

        Collect();
    }

    // 플레이어로부터 멀어지는 포물선을 그리며 튕겨서 정지하기까지, 전부
    // landDuration 안에 이루어진다.
    private IEnumerator PlayToss()
    {
        Vector3 startPos = transform.position;
        float groundY = ResolveGroundY() + restBottomOffset;

        // 플레이어에서 물약으로 이어지는 직선을 따라 곧장 뒤쪽으로 - 몬스터가
        // 어디서 죽든 물약은 항상 옆이 아니라 그 너머 쪽에 떨어지게 된다.
        Vector3 away = Vector3.right;
        if (player != null)
        {
            Vector3 delta = startPos - player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f) away = delta.normalized;
        }

        Vector3 landingPos = startPos + away * tossDistance;
        landingPos.y = groundY;

        transform.localScale = Vector3.zero;

        float t = 0f;
        while (t < landDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / landDuration);

            // 전체 시퀀스가 아니라 첫 포물선 궤적 동안 완전한 크기로 커진다 - 처음
            // 바닥에 닿을 때는 이미 완전히 형성되어 있어야 한다.
            transform.localScale = restScale * Mathf.Clamp01(p / 0.3f);

            // 감속: 대부분의 거리는 첫 번째 도약에서 이동하고, 이후 재반동들은 거의
            // 앞으로 나아가지 않는다 - 이것이 던져진 물체가 속도를 잃어가는 모습이다.
            Vector3 flat = Vector3.Lerp(startPos, landingPos, EaseOutQuad(p));
            flat.y = Mathf.Lerp(startPos.y, groundY, EaseOutQuad(p)) + tossHeight * HopHeight(p);
            transform.position = flat;

            yield return null;
        }

        transform.localScale = restScale;
        transform.position = landingPos;
    }

    // 지면에 고정된 채 일정한 속도로 접근한다 - 달리는 플레이어를 스쳐 지나가는
    // 세계와 같은 느낌이다. 정해진 시간이 아니라 물약이 발밑에 올 때까지
    // 실행되므로, 실제로 이동해야 할 거리가 얼마나 오래 걸릴지를 결정한다.
    private IEnumerator PlayRunOver()
    {
        float restY = transform.position.y;
        float elapsed = 0f;

        while (elapsed < approachTimeout)
        {
            elapsed += Time.deltaTime;
            if (player == null) yield break;

            Vector3 pos = transform.position;

            // 평면화: 물약은 바닥 평면상에서 플레이어의 위치를 추적하며 이동하는 내내
            // 자기 안착 높이를 유지한다. 그래서 떠오르지 않고 미끄러진다.
            Vector3 toPlayer = player.position - pos;
            toPlayer.y = 0f;

            if (toPlayer.magnitude <= pickupRadius) yield break;

            pos += toPlayer.normalized * approachSpeed * Time.deltaTime;
            pos.y = restY;
            transform.position = pos;

            yield return null;
        }
    }

    // 이 게임의 다른 모든 것이 기준으로 삼는 지면 라인은 플레이어 자신의 콜라이더
    // 바닥이다(Monster.SpawnUltimateImpactVfx도 정확히 이렇게 한다) - 레이캐스트할
    // 바닥 콜라이더가 따로 없다. 이게 없을 경우엔 스폰 높이로 대체해서 던지기
    // 동작이 이상해지지 않도록 한다.
    private float ResolveGroundY()
    {
        if (player == null) return transform.position.y;

        Collider playerCollider = player.GetComponent<Collider>();
        return playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
    }

    // 도약 시퀀스 전체에 걸친 정규화된 높이: 첫 포물선 정점에서 1, 매 지면
    // 접촉 시와 정지 상태에서 0.
    private static float HopHeight(float p)
    {
        float cursor = 0f;

        for (int i = 0; i < HopDurations.Length; i++)
        {
            float span = HopDurations[i];
            if (p < cursor + span || i == HopDurations.Length - 1)
            {
                float local = Mathf.Clamp01((p - cursor) / span);
                return HopHeights[i] * Mathf.Sin(local * Mathf.PI);
            }
            cursor += span;
        }

        return 0f;
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    private void Collect()
    {
        PlayerCharacter pc = player != null ? player.GetComponent<PlayerCharacter>() : null;
        if (pc != null)
        {
            if (statType == StatType.Attack) pc.IncreaseAttack(amount);
            else pc.IncreaseMaxHp(amount);
        }

        // 기존의 "+N STAT" 팝업과 플레이어의 버프 아우라 VFX를 둘 다 구동한다 - 이제는
        // 처치 순간이 아니라 픽업 순간에 이것들이 반응한다. 여기서 의도적으로 별도의
        // 포즈를 넣지 않았다: 플레이어는 달리는 중이며(PlayRunOver 참고) 그 상태를
        // 유지해야 한다 - 이걸 먹는 것은 달리기를 멈추는 게 아니라 달리기 중의 한
        // 박자일 뿐이다.
        GameEvents.RaiseStatDropGained(grade, statType, amount);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
        if (sparkleMaterial != null) Destroy(sparkleMaterial);
    }
}

// 아우라 자체의 페이드인과 idle 맥동 효과로, StatPotionPickup이 던지기/달려가기
// 시퀀스에만 집중할 수 있도록 별도로 분리해두었다 - 또한 그 시퀀스의 매 박자마다
// StatPotionPickup이 매 프레임 이 효과를 직접 구동하지 않아도 광채가 알아서
// 계속 숨쉬듯 움직이게 하기 위해서다.
public class StatPotionAuraMotion : MonoBehaviour
{
    // 물약 자체의 등장 애니메이션과 맞춰서, 메시가 아직 커지는 중인데 광채만
    // 갑자기 최고 강도로 켜지지 않고 물약과 함께 나타나도록 한다.
    [SerializeField] private float fadeInDuration = 0.25f;
    // 지속적인 숨쉬기 효과. 원래 값이던 0.14보다 더 깊게 잡았다 - 그 진폭에서는
    // 맥동이 기술적으로는 동작했지만 물약의 약 1.5초 수명 동안 눈에 거의 보이지
    // 않았다. 여전히 확 튀는 스트로브가 아니라 은은한 부풀림 정도다; 날카로운
    // "반짝"은 반짝임(sparkle)들의 몫이다(StatPotionSparkle 참고).
    [SerializeField] private float pulseAmplitude = 0.3f;
    [SerializeField] private float pulseSpeed = 3f;

    private Material material;
    private Light auraLight;
    private Color baseTint;
    private float baseIntensity;
    private float elapsed;

    private void Awake()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        // material이 아니라 sharedMaterial: SpawnAura가 이미 이 물약 하나만을 위해
        // 만들어진 머티리얼을 할당해뒀는데, 인스턴싱 게터를 쓰면 *복사본*을 돌려받게
        // 되고 StatPotionPickup의 OnDestroy는 그 복사본의 존재를 모르므로 절대
        // 정리되지 않는다.
        if (meshRenderer != null) material = meshRenderer.sharedMaterial;
        auraLight = GetComponent<Light>();

        if (material != null) baseTint = material.GetColor("_BaseColor");
        if (auraLight != null) baseIntensity = auraLight.intensity;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float fade = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
        float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed * Mathf.PI) * pulseAmplitude;
        float factor = fade * pulse;

        if (material != null) material.SetColor("_BaseColor", baseTint * factor);
        if (auraLight != null) auraLight.intensity = baseIntensity * factor;
    }
}

// 전체 반짝임 고리를 천천히 회전시킨다. 개별 깜빡임과는 별개로 동작하여,
// 반짝임들이 화면상 고정된 위치에 계속 머물지 않고 물약 주변을 떠돈다.
public class StatPotionSparkleRing : MonoBehaviour
{
    private float degreesPerSecond;

    public void Initialize(float degreesPerSecond) => this.degreesPerSecond = degreesPerSecond;

    private void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}

// 별빛 반짝임 하나의 깜빡임을 담당. localScale만 제어한다 - 물약의 모든
// 반짝임이 단일 머티리얼을 공유하므로, 별마다 머티리얼 인스턴스를 따로
// 만들지 않고도 각자 자기 위상으로 깜빡이게 하려면 스케일을 애니메이션시키는
// 방법을 쓴다.
public class StatPotionSparkle : MonoBehaviour
{
    // 사인 값을 높은 거듭제곱으로 올리면 부드러운 파형이 좁은 스파이크로
    // 바뀐다: 긴 어두운 간격 사이에 짧고 빠른 섬광이 생기는데, 이것이 맥동과
    // 반짝임을 구분짓는 요소다.
    private const float BlinkSharpness = 5f;
    // 절대 완전히 사라지지 않는다; 별이 정말 아무것도 없는 상태에서 갑자기
    // 튀어나오면 마치 오류(글리치)처럼 보인다.
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
