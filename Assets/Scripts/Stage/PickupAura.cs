using UnityEngine;

// The glow a dropped pickup sits inside: an additive billboarded halo quad plus a point light
// that throws the same color onto the ground around it, so the object reads as *emitting*
// light rather than having a decal pasted behind it.
//
// Extracted here because StoneDropPickup needed the third copy of it. StatPotionPickup and
// EquipmentDropPickup still carry their own private versions - those two are working and
// grade-driven, and rewriting them was not worth the risk to add one caller; this is the
// shared home for anything new.
public static class PickupAura
{
    // Attaches the halo (and its light) to a pickup. Returns the material it created, which
    // the caller owns and must Destroy - it is built in code, so nothing else will collect it.
    public static Material Attach(Transform parent, Color color, float size,
        float brightness, float lightIntensity, float lightRange)
    {
        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
        aura.name = "PickupAura";
        // CreatePrimitive ships a collider; this is purely visual and nothing here uses
        // physics (pickup is a distance check).
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
        // Shadows off: a small prop casting real shadows over the fight would draw far more
        // attention than a glow should (and costs a shadow map).
        light.shadows = LightShadows.None;

        aura.AddComponent<Billboard>();
        aura.AddComponent<StatPotionAuraMotion>();

        return material;
    }

    private static Material CreateAdditiveGlowMaterial(Color tint)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        // Additive transparent: URP needs the surface/blend properties AND the matching
        // keyword/queue set by hand, since this material is built in code rather than through
        // the shader GUI that normally keeps those in sync.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        // Both faces: Billboard orients local +Z away from the camera, so which side of the
        // quad faces the viewer depends on that convention - drawing both makes the halo
        // visible regardless.
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetTexture("_BaseMap", GlowTexture);
        mat.SetColor("_BaseColor", tint);

        return mat;
    }

    // One soft radial falloff shared by every pickup that uses this - the texture is identical
    // for all of them, only the tint differs (that lives on the per-instance material).
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

                    // Squared smoothstep: bright core falling off to nothing well inside the
                    // quad's edge, so the halo never shows a hard square boundary.
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
