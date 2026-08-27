using System.Collections;
using UnityEngine;

// StatPotionPickup에 대응하는 장비 버전으로, 동일한 세 박자(던지기, 안착, 달려가서 줍기)와
// 동일한 등급 아우라/반짝임 표현을 사용한다 - 다른 점은 시각적 요소뿐이다: 스탯별 유리병
// 대신 등급에 따라 색이 입혀진 단일 Sword 또는 Shield 메시를 사용한다. 장착/숙련 효과는
// 드롭 시점이 아니라 접촉 시 EquipmentDropManager.CompleteDrop에서 적용되므로, 드롭됐지만
// 끝내 닿지 못한 아이템(플레이어가 먼저 죽는 등)은 절대 효과를 지급하지 않는다.
public class EquipmentDropPickup : MonoBehaviour
{
    [Header("Toss + bounce (beat 1)")]
    [SerializeField] private float landDuration = 0.75f;
    [SerializeField] private float tossDistance = 1.4f;
    [SerializeField] private float tossHeight = 1.1f;

    [Header("Run-over (beats 2-3)")]
    [SerializeField] private float settleHoldDuration = 0.2f;
    [SerializeField] private float pickupRadius = 0.45f;
    [SerializeField] private float approachTimeout = 6f;

    private float approachSpeed = 5f;

    [Header("Visual (per equipment type)")]
    // 타입별로 메시 하나씩, StatPotionPickup의 atkVisualPrefab/hpVisualPrefab과 동일한 방식이다 -
    // 희귀도는 등급별로 다른 모델이 아니라 아래의 아우라/반짝임/크기 램프만으로 표현된다.
    [SerializeField] private GameObject swordVisualPrefab;
    [SerializeField] private GameObject shieldVisualPrefab;
    [SerializeField] private float visualBaseScale = 0.4f;
    // 메시가 임포트될 때 딸려오는 재질이 무엇이든 그 위에 덮어씌운다 - 생성된 FBX들은
    // PBR 텍스처를 외부 사이드카 파일 경로(model.fbm 폴더)로 참조하는데, 다운로드한 .fbx
    // 파일 하나만으로는 그게 딸려오지 않으므로 임포트된 재질에는 텍스처가 없어 화면에
    // 보이지 않게 렌더링된다. 여기서 덮어씌우면 FBX 임포트가 어떻게 해석되든 재질
    // 설정이 그와 무관하게 유지된다.
    [SerializeField] private Material swordMaterial;
    [SerializeField] private Material shieldMaterial;
    // 방패 메시는 XZ 평면에 눕혀진 원반 형태로 제작되어 있어(면이 위를 향함), 제작된
    // 그대로 떨어뜨리면 방패가 아니라 바닥에 놓인 그릇처럼 보인다. 똑바로 세우도록
    // 회전시키는 것은 애셋별 프레이밍 문제이지, 드롭 로직에 하드코딩할 사항이 아니다.
    [SerializeField] private Vector3 swordVisualEuler = Vector3.zero;
    [SerializeField] private Vector3 shieldVisualEuler = new Vector3(90f, 0f, 0f);

    [Header("Grade aura")]
    [SerializeField] private float auraSize = 1.5f;
    [SerializeField] private float auraBrightnessMax = 1.7f;
    [SerializeField] private float auraLightIntensityMax = 3f;
    [SerializeField] private float auraLightRange = 2.6f;

    [Header("Twinkle sparkles")]
    [SerializeField] private int sparkleCount = 4;
    [SerializeField] private float sparkleOrbitRadius = 0.5f;
    [SerializeField] private float sparkleSize = 0.6f;
    [SerializeField] private float sparkleBrightnessMax = 2.4f;
    [SerializeField] private float sparkleBlinkSpeed = 1.1f;
    [SerializeField] private float sparkleOrbitSpeed = 35f;

    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private Renderer[] renderers;
    // SpawnAura에서 나중에 추가되는 아우라/반짝임 쿼드가 아니라 메시 자체의 렌더러(들)만을
    // 가리킨다 - 모든 등급/메시에 맞는 고정 카메라 거리를 추측하는 대신 EquipmentPreviewRig가
    // 아이템의 실제 실루엣에 맞춰 UI 아이콘을 프레이밍할 수 있도록 외부에 노출한다.
    public Renderer[] VisualRenderers => renderers;
    private EquipmentType equipType;
    private StatGrade grade;
    private Transform player;
    private CombatLoop combatLoop;
    private EquipmentDropManager dropManager;
    // 왜 필요한지는 StatPotionPickup의 동일한 필드를 참고 - HandlePlayerDied가 던지는
    // 도중에 StopAllCoroutines를 호출할 수 있어, 이 코드가 자신의 PopIdleHold 호출에
    // 도달하기도 전에 멈춰버릴 수 있다.
    private bool idleHoldActive;
    private Vector3 restScale;
    private float restBottomOffset;
    private Material auraMaterial;
    private Material sparkleMaterial;
    // 등급에 따라 색이 입혀진, 공용 sword/shield 재질의 인스턴스별 복사본이다 - 현재
    // 활성화된 모든 픽업이 동일한 공용 애셋을 참조하므로, 그 원본 애셋 자체는 절대
    // 건드리지 않아야 한다.
    private Material visualMaterialInstance;

    public void Initialize(EquipmentType equipType, StatGrade grade, Transform player, CombatLoop combatLoop, float approachSpeed, EquipmentDropManager dropManager)
    {
        this.equipType = equipType;
        this.grade = grade;
        this.player = player;
        this.combatLoop = combatLoop;
        this.dropManager = dropManager;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;

        SpawnVisual();
        restScale = transform.localScale * GradeVisuals.GetPotionScale(grade);

        transform.localScale = restScale;
        restBottomOffset = ComputeBottomOffset();

        SpawnAura();

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

    // StatPotionPickup.HandlePlayerDied 참고 - 동일한 버그(이 픽업의 코루틴이
    // StageManager의 사망 시퀀스와 무관하게 계속 실행되어 이미 죽은 플레이어에게 계속
    // 다가가거나 효과를 지급해버림)에 대한 동일한 수정이다.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }

    private float ComputeBottomOffset()
    {
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        return transform.position.y - combined.min.y;
    }

    private void SpawnVisual()
    {
        GameObject prefab = equipType == EquipmentType.Sword ? swordVisualPrefab : shieldVisualPrefab;

        if (prefab == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            return;
        }

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(equipType == EquipmentType.Sword ? swordVisualEuler : shieldVisualEuler);
        visual.transform.localScale = Vector3.one * visualBaseScale;

        renderers = visual.GetComponentsInChildren<Renderer>();

        Material overrideMaterial = equipType == EquipmentType.Sword ? swordMaterial : shieldMaterial;
        if (overrideMaterial != null)
        {
            // 공유가 아니라 인스턴스화한다: 아래에서 _BaseColor를 틴트하면 이 재질이 원래
            // 갖고 있던 값에 그대로 곱해지므로, 공용 애셋을 수정해버리면 이것뿐 아니라 현재
            // 화면에 있는 모든 sword/shield의 색이 바뀌어버린다.
            visualMaterialInstance = new Material(overrideMaterial)
            {
                color = GradeVisuals.GetColor(grade)
            };

            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = visualMaterialInstance;
        }
    }

    private void SpawnAura()
    {
        float strength = GradeVisuals.GetAuraStrength(grade);
        Color color = GradeVisuals.GetColor(grade);

        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
        aura.name = "GradeAura";
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
        light.shadows = LightShadows.None;

        aura.AddComponent<Billboard>();
        aura.AddComponent<StatPotionAuraMotion>();

        SpawnSparkles(color, strength);
    }

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
            sparkle.AddComponent<StatPotionSparkle>()
                   .Initialize(sparkleSize, sparkleBlinkSpeed, (float)i / sparkleCount);
        }
    }

    private static Material CreateAdditiveGlowMaterial(Color tint, Texture2D texture = null)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetTexture("_BaseMap", texture != null ? texture : GlowTexture);
        mat.SetColor("_BaseColor", tint);

        return mat;
    }

    // StatPotionPickup이 만드는 것과 정확히 같은 별빛/방사형 광채 텍스처를 사용하지만,
    // 두 클래스가 클래스 계층을 공유하지 않으므로 여기서 독립적으로 생성한다(클래스별
    // private static 캐시) - 어차피 64x64짜리 저렴한 텍스처라 공용 애셋으로 연결할
    // 가치가 없다.
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
                    float dx = Mathf.Abs(x - center) / center;
                    float dy = Mathf.Abs(y - center) / center;

                    float horizontal = Falloff(dx) * Needle(dy);
                    float vertical = Falloff(dy) * Needle(dx);
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

    private static float Falloff(float d)
    {
        float f = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
        return f * f;
    }

    private static float Needle(float d) => Mathf.Clamp01(1f - Mathf.Clamp01(d) * 14f);

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

        if (combatLoop != null) combatLoop.PopIdleHold();
        idleHoldActive = false;

        yield return new WaitForSeconds(settleHoldDuration);
        yield return PlayRunOver();

        Collect();
    }

    private IEnumerator PlayToss()
    {
        Vector3 startPos = transform.position;
        float groundY = ResolveGroundY() + restBottomOffset;

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

            transform.localScale = restScale * Mathf.Clamp01(p / 0.3f);

            Vector3 flat = Vector3.Lerp(startPos, landingPos, EaseOutQuad(p));
            flat.y = Mathf.Lerp(startPos.y, groundY, EaseOutQuad(p)) + tossHeight * HopHeight(p);
            transform.position = flat;

            yield return null;
        }

        transform.localScale = restScale;
        transform.position = landingPos;
    }

    private IEnumerator PlayRunOver()
    {
        float restY = transform.position.y;
        float elapsed = 0f;

        while (elapsed < approachTimeout)
        {
            elapsed += Time.deltaTime;
            if (player == null) yield break;

            Vector3 pos = transform.position;

            Vector3 toPlayer = player.position - pos;
            toPlayer.y = 0f;

            if (toPlayer.magnitude <= pickupRadius) yield break;

            pos += toPlayer.normalized * approachSpeed * Time.deltaTime;
            pos.y = restY;
            transform.position = pos;

            yield return null;
        }
    }

    private float ResolveGroundY()
    {
        if (player == null) return transform.position.y;

        Collider playerCollider = player.GetComponent<Collider>();
        return playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
    }

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
        if (dropManager != null) dropManager.CompleteDrop(equipType, grade);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
        if (sparkleMaterial != null) Destroy(sparkleMaterial);
        if (visualMaterialInstance != null) Destroy(visualMaterialInstance);
    }
}
