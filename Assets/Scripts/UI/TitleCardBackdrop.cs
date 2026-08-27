using UnityEngine;

// 스테이지 안내 카드가 사용하는 밝은 파스텔톤 새벽 하늘을 코드로 만든다. 색조는 씬 자체의
// 하늘(상단은 #334A93, 지평선 근처는 #785F98로 샘플링)을 따르되 훨씬 밝게 끌어올렸는데,
// 게임 자체가 밝고 경쾌한 느낌이라 실제 색을 그대로 맞추면 오히려 우울해 보이기 때문이다.
// 그림으로 직접 제작하지 않고 코드로 절차적으로 그리는 이유는, 마찬가지로 코드로 구성되는
// 이 UI의 나머지 부분과 동기화된 상태를 유지하기 위해서다.
public static class TitleCardBackdrop
{
    // 한 번의 패스로 만들 수 있을 만큼 작은 크기다; 전체 화면으로 확대할 때 쓰는 바일리니어
    // 업스케일링이 별을 딱딱한 픽셀이 아니라 부드러운 광채로 만들어준다.
    const int Width = 512;
    const int Height = 288;

    // 씬의 남보라색을 파스텔톤으로 끌어올렸다: 하늘 위쪽은 페리윙클색이고 지평선으로 갈수록
    // 따뜻한 핑크색으로 떨어진다.
    static readonly Color SkyTop = new Color(0.482f, 0.541f, 0.820f);
    static readonly Color SkyHorizon = new Color(0.957f, 0.780f, 0.827f);

    // 하늘과 별만 있으며, 타이틀과 경쟁할 만한 요소는 없다.
    public static Sprite Create()
    {
        var pixels = new Color[Width * Height];
        var rng = NewRng();

        PaintSky(pixels, rng);
        PaintStars(pixels, rng, 120, 0.28f);

        return Finish(pixels);
    }

    // 고정된 시드: 이 아트는 나타날 때마다 항상 동일해야 하며, "Stage 1-2"를 보는 플레이어가
    // "Stage 1-1"과 다른 하늘을 봤다고 느끼지 않도록 한다.
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
            // 페리윙클색 쪽으로 치우치게 하여, 핑크색이 프레임 가운데까지 번지지 않고
            // 지평선 띠로만 남게 한다 - 프레임 가운데는 흰색 텍스트가 잘 보여야 하는
            // 영역이다.
            Color row = Color.Lerp(SkyHorizon, SkyTop, Mathf.Pow(t, 0.6f));

            // 최하위 비트보다도 작은 지터를 준다; 이게 없으면 이렇게 완만한 그라데이션은
            // 1080p 화면에서 눈에 띄는 밴딩 현상이 생긴다.
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

            // 별은 밝은 지평선 위에서는 잘 보이지 않으므로, 핑크색 영역 전체에 안 보이는
            // 별을 그냥 흩뿌려두는 대신 아래로 갈수록 개수를 줄인다.
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

                a *= a; // 중심부를 더 또렷하게 만들고 가장자리는 서서히 흐려지게 한다.
                Blend(pixels, x, y, color, a * strength);
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
