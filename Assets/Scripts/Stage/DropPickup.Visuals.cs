using Benjathemaker;
using UnityEngine;

public partial class DropPickup
{
    private float ComputeBottomOffset()
    {
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        return transform.position.y - combined.min.y;
    }

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
            SimpleGemsAnim anim = visual.GetComponent<SimpleGemsAnim>();
            if (anim != null) Destroy(anim);
        }

        renderers = visual.GetComponentsInChildren<Renderer>();

        if (kind == Kind.Equipment)
        {
            Material overrideMaterial = equipType == EquipmentType.Sword ? swordMaterial : shieldMaterial;
            if (overrideMaterial != null)
            {
                visualMaterialInstance = new Material(overrideMaterial)
                {
                    color = GradeVisuals.GetColor(grade)
                };

                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = visualMaterialInstance;
            }
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
        aura.AddComponent<DropPickupAuraMotion>();

        SpawnSparkles(color, strength);
    }

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
            sparkle.AddComponent<DropPickupSparkle>()
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
