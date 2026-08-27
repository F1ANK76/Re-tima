using UnityEngine;

// 드롭된 픽업이 놓여있는 광채 효과: 가산 블렌딩된 빌보드 헤일로 쿼드와, 주변 바닥에
// 같은 색 빛을 던지는 포인트 라이트로 구성되어, 오브젝트 뒤에 데칼을 붙여놓은 것이
// 아니라 실제로 빛을 *내뿜는* 것처럼 보이게 한다.
//
// StoneDropPickup이 이 코드의 세 번째 사본을 필요로 하게 되면서 여기로 추출했다.
// StatPotionPickup과 EquipmentDropPickup은 여전히 각자의 private 버전을 갖고 있다 -
// 이 둘은 정상 동작하고 있고 등급에 따라 구동되므로, 호출부 하나 추가하자고
// 다시 작성하는 위험을 감수할 가치가 없었다; 이곳은 앞으로 새로 추가될 것들을
// 위한 공용 저장소다.
public static class PickupAura
{
    // 헤일로(와 그 라이트)를 픽업에 부착한다. 생성한 머티리얼을 반환하며, 호출자가
    // 소유권을 가지고 반드시 Destroy해야 한다 - 코드로 생성된 것이라 다른 누구도
    // 대신 수거해주지 않는다.
    public static Material Attach(Transform parent, Color color, float size,
        float brightness, float lightIntensity, float lightRange)
    {
        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
        aura.name = "PickupAura";
        // CreatePrimitive는 콜라이더를 기본으로 붙여서 나온다; 이건 순전히 시각적인
        // 요소일 뿐이고 여기서는 물리를 전혀 사용하지 않는다(픽업 판정은 거리 계산이다).
        Object.Destroy(aura.GetComponent<Collider>());

        aura.transform.SetParent(parent, false);
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localScale = Vector3.one * size;

        Material material = CreateAdditiveGlowMaterial(color * brightness);
        aura.GetComponent<MeshRenderer>().material = material;

        Light light = aura.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = lightIntensity;
        light.range = lightRange;
        // 그림자 끔: 작은 소품이 전투 위에 실제 그림자를 드리우면 광채가 끌어야 할 것보다
        // 훨씬 더 시선을 빼앗는다(게다가 섀도우맵 비용도 든다).
        light.shadows = LightShadows.None;

        aura.AddComponent<Billboard>();
        aura.AddComponent<StatPotionAuraMotion>();

        return material;
    }

    private static Material CreateAdditiveGlowMaterial(Color tint)
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
        // 어느 면이 뷰어를 향하는지는 그 규약에 달려있다 - 양면을 모두 그리면 어느
        // 쪽이든 헤일로가 확실히 보인다.
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetTexture("_BaseMap", GlowTexture);
        mat.SetColor("_BaseColor", tint);

        return mat;
    }

    // 이 코드를 사용하는 모든 픽업이 공유하는 하나의 부드러운 방사형 감쇠 텍스처 -
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
