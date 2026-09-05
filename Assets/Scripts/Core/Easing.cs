using UnityEngine;

public static class Easing
{
    public static float OutQuad(float x) => 1f - (1f - x) * (1f - x);

    public static float OutBack(float x, float overshoot = 1.6f)
    {
        float c3 = overshoot + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + overshoot * m * m;
    }

    public static float PopCurve(float k, float startScale, float punchScale)
    {
        if (k < 0.6f)
        {
            float rise = k / 0.6f;
            return Mathf.Lerp(startScale, punchScale, 1f - (1f - rise) * (1f - rise));
        }

        float settle = (k - 0.6f) / 0.4f;
        return Mathf.Lerp(punchScale, 1f, settle * settle * (3f - 2f * settle));
    }
}
