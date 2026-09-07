using Benjathemaker;
using UnityEngine;

public partial class DropPickup
{
    private Renderer[] renderers;
    // 아우라 쿼드 제외, 메시 렌더러만 -> EquipmentPreviewRig가 실제 실루엣에 맞춰 아이콘을 잡는다
    public Renderer[] VisualRenderers => renderers;

    private float ComputeBottomOffset()
    {
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        return transform.position.y - combined.min.y;
    }

    private void SpawnVisual()
    {
        DropPickupConfigSO.Entry entry = config.Get(kind, statType, equipType);

        if (entry.prefab == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            return;
        }

        GameObject visual = Instantiate(entry.prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(entry.euler);
        visual.transform.localScale = Vector3.one * entry.scale;

        if (kind == Kind.StatPotion)
        {
            SimpleGemsAnim anim = visual.GetComponent<SimpleGemsAnim>();
            if (anim != null) Destroy(anim);
        }

        renderers = visual.GetComponentsInChildren<Renderer>();

        // 임포트 재질 위에 덮어씌운다 -> FBX가 텍스처 없이 들어와도 보이게
        if (entry.material != null)
        {
            visualMaterialInstance = new Material(entry.material)
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
        aura.transform.localScale = Vector3.one * config.auraSize;

        auraMaterial = CreateAdditiveGlowMaterial(color * (config.auraBrightnessMax * strength));
        aura.GetComponent<MeshRenderer>().material = auraMaterial;

        aura.AddComponent<Billboard>();
        aura.AddComponent<DropPickupAuraMotion>();

        SpawnGroundGlow(color, strength);
        SpawnSparkles(color, strength);
    }

    // 실시간 라이트는 URP 추가 라이트 상한(4개)에 걸려 드랍이 겹치면 일부가 빠진다 -> 바닥에 눕힌 쿼드로 대체
    private void SpawnGroundGlow(Color color, float strength)
    {
        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "GroundGlow";
        Destroy(glow.GetComponent<Collider>());

        glow.transform.SetParent(transform, false);
        glow.transform.localScale = Vector3.one * config.groundGlowSize;
        // 눕혀서 바닥을 향하게. 지면과 겹쳐 깜빡이지 않도록 살짝 띄운다
        glow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        glow.AddComponent<DropPickupGroundGlow>()
            .Initialize(ResolveGroundY() + GroundGlowLift);

        groundGlowMaterial = CreateAdditiveGlowMaterial(color * (config.groundGlowBrightness * strength));
        glow.GetComponent<MeshRenderer>().material = groundGlowMaterial;

        glow.AddComponent<DropPickupAuraMotion>();
    }

    private void SpawnSparkles(Color color, float strength)
    {
        if (config.sparkleCount <= 0) return;

        sparkleMaterial = CreateAdditiveGlowMaterial(color * (config.sparkleBrightnessMax * strength), SparkleTexture);

        var ring = new GameObject("SparkleRing");
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.AddComponent<DropPickupSparkleRing>().Initialize(config.sparkleOrbitSpeed);

        for (int i = 0; i < config.sparkleCount; i++)
        {
            GameObject sparkle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sparkle.name = "Sparkle" + i;
            Destroy(sparkle.GetComponent<Collider>());

            float angle = (360f / config.sparkleCount) * i;
            sparkle.transform.SetParent(ring.transform, false);
            sparkle.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * (Vector3.up * config.sparkleOrbitRadius);
            sparkle.transform.localScale = Vector3.one * config.sparkleSize;

            sparkle.GetComponent<MeshRenderer>().material = sparkleMaterial;

            sparkle.AddComponent<Billboard>();
            sparkle.AddComponent<DropPickupSparkle>()
                   .Initialize(config.sparkleSize, config.sparkleBlinkSpeed, (float)i / config.sparkleCount);
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

        // 가산 블렌딩이라 안개색이 더해진다 -> 텍스처가 0인 가장자리까지 밝아져 쿼드 크기만 한 사각형이 드러난다
        mat.DisableKeyword("FOG_LINEAR");
        mat.DisableKeyword("FOG_EXP");
        mat.DisableKeyword("FOG_EXP2");

        return mat;
    }

    // [0,1] 구간에서 1->0으로 부드럽게 감쇠하며, 더 좁은 감쇠를 위해 제곱한다.
    private static float Falloff(float d)
    {
        float f = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
        return f * f;
    }

    private static float Needle(float d) => Mathf.Clamp01(1f - Mathf.Clamp01(d) * 14f);

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
        if (groundGlowMaterial != null) Destroy(groundGlowMaterial);
        if (sparkleMaterial != null) Destroy(sparkleMaterial);
        if (visualMaterialInstance != null) Destroy(visualMaterialInstance);
    }

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
}
