using UnityEngine;

// Builds the game's title art in code: a bright pastel dawn sky shared by the stage-
// announcement card and the main menu. The hues follow the scene's own sky (sampled at
// #334A93 up top through #785F98 near the horizon) but lifted well above it, because the game
// itself reads bright and cheerful and a literal match would land as gloomy. Drawn
// procedurally rather than authored as art so it stays in sync with the rest of this UI,
// which is likewise built from code.
public static class TitleCardBackdrop
{
    // Small enough to build in one pass; bilinear upscaling to full screen is what softens
    // the stars into glows rather than leaving them as hard pixels.
    const int Width = 512;
    const int Height = 288;

    // The scene's indigo/purple, pushed up into pastel: periwinkle overhead falling to a warm
    // pink horizon.
    static readonly Color SkyTop = new Color(0.482f, 0.541f, 0.820f);
    static readonly Color SkyHorizon = new Color(0.957f, 0.780f, 0.827f);

    // Scene greens, kept light so the illustration stays as bright as the sky above it.
    static readonly Color GrassNear = new Color(0.541f, 0.729f, 0.435f);
    static readonly Color GrassFar = new Color(0.639f, 0.804f, 0.529f);
    static readonly Color TreeNear = new Color(0.180f, 0.396f, 0.286f);
    static readonly Color TreeFar = new Color(0.400f, 0.588f, 0.478f);
    // Characters read as flat cutouts so they stay legible against the busy treeline.
    static readonly Color Figure = new Color(0.145f, 0.114f, 0.208f);

    // The stage-announcement card: sky and stars only, nothing to compete with the title.
    public static Sprite Create()
    {
        var pixels = new Color[Width * Height];
        var rng = NewRng();

        PaintSky(pixels, rng);
        PaintStars(pixels, rng, 120, 0.28f);

        return Finish(pixels);
    }

    // The main menu: the same sky, plus the forest, the hero and the monster he is walking
    // toward - enough of the game on one screen to read what it is before pressing Play.
    public static Sprite CreateTitleScene()
    {
        var pixels = new Color[Width * Height];
        var rng = NewRng();

        PaintSky(pixels, rng);
        // Lifted off the horizon so none land inside the treeline.
        PaintStars(pixels, rng, 90, 0.42f);

        int groundTop = Mathf.RoundToInt(Height * 0.17f);
        PaintForest(pixels, rng, groundTop);
        PaintGround(pixels, groundTop);
        PaintHero(pixels, Width * 0.24f, groundTop);
        PaintMonster(pixels, Width * 0.74f, groundTop);

        return Finish(pixels);
    }

    // Fixed seed: the art should look identical every time it appears, so a player seeing
    // "Stage 1-2" doesn't register a different sky than "Stage 1-1" had.
    static System.Random NewRng() => new System.Random(20260814);

    static Sprite Finish(Color[] pixels)
    {
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f));
    }

    static void PaintSky(Color[] pixels, System.Random rng)
    {
        for (int y = 0; y < Height; y++)
        {
            float t = (float)y / (Height - 1);
            // Biased toward the periwinkle so the pink stays a horizon band rather than
            // washing over the middle of the frame, where white text has to stay legible.
            Color row = Color.Lerp(SkyHorizon, SkyTop, Mathf.Pow(t, 0.6f));

            // A sub-LSB jitter; without it a gradient this shallow bands visibly across a
            // 1080p stretch.
            for (int x = 0; x < Width; x++)
            {
                float n = ((float)rng.NextDouble() - 0.5f) / 255f;
                pixels[y * Width + x] = new Color(row.r + n, row.g + n, row.b + n, 1f);
            }
        }
    }

    static void PaintStars(Color[] pixels, System.Random rng, int count, float lowestFrac)
    {
        for (int i = 0; i < count; i++)
        {
            float cx = (float)rng.NextDouble() * Width;
            float cy = Height * (lowestFrac + (float)rng.NextDouble() * (1f - lowestFrac));

            // Stars wash out against the bright horizon, so thin them out on the way down
            // instead of leaving invisible ones scattered across the pink.
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lowestFrac + 0.02f, lowestFrac + 0.34f, cy / Height));
            if (fade <= 0.02f) continue;

            float radius = 0.5f + (float)rng.NextDouble() * 1.0f;
            float brightness = (0.45f + (float)rng.NextDouble() * 0.55f) * fade;
            DrawDot(pixels, cx, cy, radius, Color.white, brightness);
        }
    }

    static void DrawDot(Color[] pixels, float cx, float cy, float radius, Color color, float strength)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1f));
        int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + radius + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1f));
        int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(cy + radius + 1f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(1f - d / (radius + 0.5f));
                if (a <= 0f) continue;

                a *= a; // Tightens the core and lets the edge trail off.
                Blend(pixels, x, y, color, a * strength);
            }
        }
    }

    // Two ridges: the far one is lifted toward the sky so it reads as distance, the near one
    // stays saturated. Same depth cue the 3D scene gets for free.
    static void PaintForest(Color[] pixels, System.Random rng, int groundTop)
    {
        int farBase = groundTop + Mathf.RoundToInt(Height * 0.04f);
        DrawRidge(pixels, rng, farBase, 8f, 7f, 0.08f, 0.07f, Color.Lerp(TreeFar, SkyHorizon, 0.35f));
        DrawRidge(pixels, rng, groundTop, 11f, 10f, 0.12f, 0.13f, TreeNear);
    }

    static void DrawRidge(Color[] pixels, System.Random rng, int baseY, float minHalfWidth,
                          float halfWidthRange, float minHeightFrac, float heightRange, Color color)
    {
        // Starts off-canvas so neither edge of the screen opens on a half-tree.
        float x = -halfWidthRange;
        while (x < Width + halfWidthRange)
        {
            float halfWidth = minHalfWidth + (float)rng.NextDouble() * halfWidthRange;
            float treeHeight = Height * (minHeightFrac + (float)rng.NextDouble() * heightRange);
            DrawPine(pixels, x, baseY, halfWidth, treeHeight, color);
            // Spacing under one full width, so neighbours overlap into a ridge instead of
            // standing apart as a countable row.
            x += halfWidth * (1.05f + (float)rng.NextDouble() * 0.55f);
        }
    }

    static void DrawPine(Color[] pixels, float cx, int baseY, float halfWidth, float height, Color color)
    {
        int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(baseY + height));
        for (int y = Mathf.Max(0, baseY); y <= maxY; y++)
        {
            float up = (y - baseY) / height;          // 0 at the base, 1 at the tip
            // Curved rather than a straight cone: pines carry their bulk low and taper fast
            // near the crown, which is what makes them read as pines and not as spikes.
            float spread = halfWidth * (1f - up * up * 0.55f) * (1f - up);
            if (spread <= 0f) continue;

            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - spread));
            int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + spread));
            for (int x = minX; x <= maxX; x++)
            {
                float edge = Mathf.Clamp01(spread - Mathf.Abs(x - cx));
                if (edge > 0f) Blend(pixels, x, y, color, edge);
            }
        }
    }

    static void PaintGround(Color[] pixels, int groundTop)
    {
        for (int y = 0; y < groundTop; y++)
        {
            // Slightly paler toward the horizon line, so the field recedes instead of reading
            // as one flat slab.
            Color row = Color.Lerp(GrassNear, GrassFar, (float)y / Mathf.Max(1, groundTop - 1));
            for (int x = 0; x < Width; x++) pixels[y * Width + x] = row;
        }
    }

    // Small flat cutouts rather than detailed figures - at this size the read comes from the
    // pose and the weapons, and detail would only turn to mush once it is upscaled.
    static void PaintHero(Color[] pixels, float cx, int groundY)
    {
        const float S = 1.7f;   // Sized so the standoff reads at a glance, not as background detail.
        DrawContactShadow(pixels, cx, groundY, 13f * S);

        FillCapsule(pixels, cx - 3.5f * S, groundY + 1f, cx - 3.5f * S, groundY + 11f * S, 2.2f * S, Figure);
        FillCapsule(pixels, cx + 3.5f * S, groundY + 1f, cx + 3.5f * S, groundY + 11f * S, 2.2f * S, Figure);
        FillCapsule(pixels, cx, groundY + 10f * S, cx, groundY + 22f * S, 6.0f * S, Figure);
        FillCapsule(pixels, cx, groundY + 29f * S, cx, groundY + 29f * S, 6.2f * S, Figure);

        // Sword raised toward the monster: the clearest single cue that this is a fighter.
        // The guard is kept thinner and shorter than the blade so the two don't read as two
        // crossed sticks of equal weight.
        FillCapsule(pixels, cx + 6f * S, groundY + 19f * S, cx + 23f * S, groundY + 43f * S, 1.3f * S, Figure);
        FillCapsule(pixels, cx + 3.1f * S, groundY + 21f * S, cx + 8.9f * S, groundY + 17f * S, 0.9f * S, Figure);
    }

    static void PaintMonster(Color[] pixels, float cx, int groundY)
    {
        const float S = 1.6f;
        DrawContactShadow(pixels, cx, groundY, 16f * S);

        FillCapsule(pixels, cx - 4.5f * S, groundY + 1f, cx - 4.5f * S, groundY + 13f * S, 2.8f * S, Figure);
        FillCapsule(pixels, cx + 4.5f * S, groundY + 1f, cx + 4.5f * S, groundY + 13f * S, 2.8f * S, Figure);
        FillCapsule(pixels, cx, groundY + 12f * S, cx, groundY + 30f * S, 8.5f * S, Figure);
        FillCapsule(pixels, cx, groundY + 38f * S, cx, groundY + 38f * S, 7.5f * S, Figure);

        // Spear planted on the side facing the hero, plus a shield - the silhouette the
        // player actually meets first in Stage 1. The head is a taper rather than a capsule,
        // which at this size is the difference between a spear and a lollipop.
        FillCapsule(pixels, cx - 13f * S, groundY + 2f, cx - 13f * S, groundY + 50f * S, 1.3f * S, Figure);
        FillCone(pixels, cx - 13f * S, groundY + 49f * S, 3.2f * S, 10f * S, Figure);
        FillCapsule(pixels, cx + 11f * S, groundY + 22f * S, cx + 11f * S, groundY + 30f * S, 5.0f * S, Figure);
    }

    // Without this the figures look pasted on rather than standing on the field.
    static void DrawContactShadow(Color[] pixels, float cx, int groundY, float halfWidth)
    {
        const float halfHeight = 3.0f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - halfWidth));
        int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + halfWidth));
        int minY = Mathf.Max(0, Mathf.FloorToInt(groundY - halfHeight));
        int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(groundY + halfHeight));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float nx = (x - cx) / halfWidth;
                float ny = (y - groundY) / halfHeight;
                float d = nx * nx + ny * ny;
                if (d >= 1f) continue;

                Blend(pixels, x, y, TreeNear, (1f - d) * 0.28f);
            }
        }
    }

    // A straight taper to a point, for shapes a constant-radius capsule can't express.
    static void FillCone(Color[] pixels, float cx, float baseY, float halfWidth, float height, Color color)
    {
        int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(baseY + height));
        for (int y = Mathf.Max(0, Mathf.FloorToInt(baseY)); y <= maxY; y++)
        {
            float spread = halfWidth * (1f - (y - baseY) / height);
            if (spread <= 0f) continue;

            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - spread));
            int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + spread));
            for (int x = minX; x <= maxX; x++)
            {
                float edge = Mathf.Clamp01(spread - Mathf.Abs(x - cx));
                if (edge > 0f) Blend(pixels, x, y, color, edge);
            }
        }
    }

    // One primitive covers every body part here: a line segment with a radius, which is a
    // circle when the endpoints coincide and a rounded bar otherwise.
    static void FillCapsule(Color[] pixels, float x0, float y0, float x1, float y1, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - radius - 1f));
        int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + radius + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - radius - 1f));
        int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + radius + 1f));

        float dx = x1 - x0, dy = y1 - y0;
        float lenSq = dx * dx + dy * dy;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float t = lenSq > 0f ? Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / lenSq) : 0f;
                float px = x0 + dx * t, py = y0 + dy * t;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));

                float coverage = Mathf.Clamp01(radius + 0.5f - d);
                if (coverage > 0f) Blend(pixels, x, y, color, coverage);
            }
        }
    }

    static void Blend(Color[] pixels, int x, int y, Color color, float alpha)
    {
        int idx = y * Width + x;
        pixels[idx] = Color.Lerp(pixels[idx], color, Mathf.Clamp01(alpha));
        pixels[idx].a = 1f;
    }
}
