using Benjathemaker;
using UnityEngine;

// DropPickup의 겉모습 - 종류에 맞는 메시, 등급 색 오라, 별빛 반짝임을 만들어 붙인다.
// 코드로 만든 머티리얼은 아무도 대신 수거해주지 않으므로 OnDestroy도 여기가 책임진다.
//
// 던지기/달려가기 시퀀스(DropPickup.Sequence.cs)와는 restScale, restBottomOffset,
// 머티리얼 세 개를 통해서만 만난다 - 그 상태는 전부 DropPickup.cs가 들고 있다.
public partial class DropPickup
{
    // 현재 스케일 기준으로 이 transform의 피벗에서 렌더링된 메시 바닥까지의 거리. 비주얼이
    // 여러 파츠일 수 있으므로 모든 렌더러를 합산한다.
    private float ComputeBottomOffset()
    {
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        return transform.position.y - combined.min.y;
    }

    // 종류에 맞는 메시를 자식으로 인스턴스화한다. 물약은 애셋 자체가 색 정체성을 갖고 있어
    // 그대로 쓰고, 장비는 공용 메시에 등급 색 재질을 입힌다.
    private void SpawnVisual()
    {
        GameObject prefab = kind == Kind.StatPotion
            ? (statType == StatType.Attack ? atkVisualPrefab : hpVisualPrefab)
            : (equipType == EquipmentType.Sword ? swordVisualPrefab : shieldVisualPrefab);

        if (prefab == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            return;
        }

        Quaternion localRotation = kind == Kind.StatPotion
            ? Quaternion.identity
            : Quaternion.Euler(equipType == EquipmentType.Sword ? swordVisualEuler : shieldVisualEuler);

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = localRotation;
        visual.transform.localScale = Vector3.one * visualBaseScale;

        if (kind == Kind.StatPotion)
        {
            // 이 애셋에는 자체 idle 회전/부유/스케일 루프가 딸려있다 - PlayToss/PlayRunOver가
            // 이미 매 프레임 위치와 스케일을 제어하므로 두면 같은 transform을 두고 충돌한다.
            SimpleGemsAnim anim = visual.GetComponent<SimpleGemsAnim>();
            if (anim != null) Destroy(anim);
        }

        renderers = visual.GetComponentsInChildren<Renderer>();

        if (kind == Kind.Equipment)
        {
            Material overrideMaterial = equipType == EquipmentType.Sword ? swordMaterial : shieldMaterial;
            if (overrideMaterial != null)
            {
                // 공유가 아니라 인스턴스화: _BaseColor 틴트는 재질의 원래 값에 그대로 곱해지므로,
                // 공용 애셋을 수정해버리면 화면에 있는 모든 sword/shield의 색까지 함께 바뀐다.
                visualMaterialInstance = new Material(overrideMaterial)
                {
                    color = GradeVisuals.GetColor(grade)
                };

                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = visualMaterialInstance;
            }
        }
    }

    // 빌보드 오브젝트 하나에 두 레이어: 가산 헤일로 쿼드(눈에 보이는 광채)와 포인트
    // 라이트(바닥에 던지는 색광). 둘 다 색상은 등급에서, 세기는 GetAuraStrength에서 온다.
    private void SpawnAura()
    {
        float strength = GradeVisuals.GetAuraStrength(grade);
        Color color = GradeVisuals.GetColor(grade);

        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
        aura.name = "GradeAura";
        // CreatePrimitive는 콜라이더를 붙여서 나온다. 순전히 시각용이고 물리는 전혀 쓰지
        // 않는다(픽업 판정은 거리 계산이다 - PlayRunOver 참고).
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
        // 그림자 끔: 작은 소품이 전투 위에 실제 그림자를 드리우면 "은은하게"가 허용하는
        // 것보다 훨씬 시선을 빼앗고, 섀도우맵 비용도 든다.
        light.shadows = LightShadows.None;

        aura.AddComponent<Billboard>();
        aura.AddComponent<DropPickupAuraMotion>();

        SpawnSparkles(color, strength);
    }

    // 깜빡이는 별빛 고리. 모두 머티리얼 하나를 공유하므로(같은 색조) 별마다의 반짝임은
    // 순전히 transform 스케일로 표현된다 - 색을 애니메이션하려 별마다 머티리얼 인스턴스를
    // 만들 필요 없이 독립적인 위상을 갖는다.
    private void SpawnSparkles(Color color, float strength)
    {
        if (sparkleCount <= 0) return;

        sparkleMaterial = CreateAdditiveGlowMaterial(color * (sparkleBrightnessMax * strength), SparkleTexture);

        var ring = new GameObject("SparkleRing");
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.AddComponent<DropPickupSparkleRing>().Initialize(sparkleOrbitSpeed);

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
            sparkle.AddComponent<DropPickupSparkle>()
                   .Initialize(sparkleSize, sparkleBlinkSpeed, (float)i / sparkleCount);
        }
    }

    private static Material CreateAdditiveGlowMaterial(Color tint, Texture2D texture = null)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        // 가산 투명: 값들을 자동으로 맞춰주는 셰이더 GUI 없이 코드로 생성하는 머티리얼이라,
        // URP에서는 surface/blend 속성과 이에 맞는 키워드/렌더큐를 직접 맞춰줘야 한다.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        // 양면 렌더링: Billboard가 로컬 +Z를 카메라 반대쪽으로 정렬하므로 쿼드의 어느 면이
        // 뷰어를 향하는지는 그 규약에 달려있다 - 양면을 다 그려야 헤일로가 확실히 보인다.
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetTexture("_BaseMap", texture != null ? texture : GlowTexture);
        mat.SetColor("_BaseColor", tint);

        return mat;
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

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
        if (sparkleMaterial != null) Destroy(sparkleMaterial);
        if (visualMaterialInstance != null) Destroy(visualMaterialInstance);
    }

    // 모든 아이템의 모든 반짝임이 공유하는 4갈래 별빛 텍스처. 부드러운 방사형 뭉치
    // (GlowTexture)는 그냥 램프로 보인다 - 아래 십자 감쇠가 이 크기에서 반짝임으로 읽히게 한다.
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

                    // 십자로 교차하는 두 바늘: 각 팔은 자기 축을 따라선 밝지만 축을 가로질러선
                    // 급격히 감쇠한다. 마름모가 아니라 별 모양의 뾰족함이 여기서 나온다.
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


    // 지금까지 드롭된 모든 아이템이 공유하는 하나의 부드러운 방사형 감쇠 텍스처 -
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
}
