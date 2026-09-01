using UnityEngine;

// UI 연출이 공유하는 이징 곡선. 같은 함수가 파일마다 복사돼 있으면 한쪽만 고쳤을 때
// 연출이 조용히 어긋난다 - 실제로 EaseOutBack이 네 파일에 각각 박혀 있었고, 그중 하나만
// overshoot 값이 달랐다(아래 기본값 설명 참고).
public static class Easing
{
    public static float OutQuad(float x) => 1f - (1f - x) * (1f - x);

    // overshoot는 목표치를 얼마나 넘어갔다 돌아올지를 정한다. 기본값 1.6은 팝업/게이지가
    // 쓰는 값이고, 스테이지 배너는 더 얌전한 1.2를 넘겨 쓴다.
    //
    // 주의: 반환값이 1을 넘어가는 구간이 이 곡선의 존재 이유다. 호출부에서 Mathf.Lerp에
    // 그대로 넣으면 t가 0~1로 클램프되면서 그 튕김이 통째로 사라진다 - 오버슈트를 살리려면
    // Mathf.LerpUnclamped를 써야 한다.
    public static float OutBack(float x, float overshoot = 1.6f)
    {
        float c3 = overshoot + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + overshoot * m * m;
    }

    // 배너 팝인: 처음 ~60% 구간에서는 최종 크기를 넘어서까지 커졌다가, 이후 다시 그 크기로
    // 서서히 안착한다. OutBack과 달리 시작/펀치 크기를 직접 받아 최종 배율까지 계산해 준다.
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
