using UnityEngine;

// 드롭 희귀도를 시각화하는 모든 곳(스탯 드롭 팝업 텍스트, 포션 픽업의 틴트/글로우)이 공유하는
// 등급 -> 색상 램프. 단일 진실 공급원으로 두어 같은 등급이 서로 다른 색으로 어긋나지 않게 한다.
public static class GradeVisuals
{
    // Normal->흰색, Rare->하늘색, Epic->보라색, Unique->노란색, Legendary->연두색.
    public static Color GetColor(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return Color.white;
            case StatGrade.Rare: return new Color(0.4f, 0.75f, 1f);
            case StatGrade.Epic: return new Color(0.65f, 0.3f, 0.95f);
            case StatGrade.Unique: return new Color(1f, 0.85f, 0.1f);
            default: return new Color(0.6f, 0.9f, 0.25f);
        }
    }

    // 오라/패널 테두리를 물들이는 위쪽 램프와 달리 떠오르는 픽업 팝업(PopupText)의 채우기 색상 -
    // 같은 다섯 색 정체성을 완전 채도로 밀어붙였다. 옅은 램프는 오브젝트 주변 글로우로는 괜찮지만
    // 게임플레이 위의 작은 글리프가 되는 순간 흐릿한 회색 텍스트처럼 보인다.
    //
    // 어둡게 하는 대신 채도를 높였다: 밝은 하늘 배경에서 보일 만큼 어둡게 하면 파랑/보라/초록이
    // 탁한 거의 검정으로 수렴해 등급 구분이 사라진다. 대비는 PopupText의 검은 외곽선이 담당하므로
    // 여기서는 색조(hue)만 전달하면 된다.
    public static Color GetPopupTextColor(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return Color.white;
            case StatGrade.Rare: return new Color(0.15f, 0.45f, 1f);
            case StatGrade.Epic: return new Color(0.58f, 0.18f, 1f);
            case StatGrade.Unique: return new Color(1f, 0.78f, 0f);
            default: return new Color(0.2f, 0.9f, 0.15f);
        }
    }

    // 호출자가 이미 Epic용으로 쓰는 기준 크기에 곱하는 배율 - Epic은 다섯 등급 중 정중앙이므로
    // 끝점이 아니라 램프가 위아래로 스케일링되는 축(pivot)이 된다.
    public static float GetSizeScale(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 0.55f;
            case StatGrade.Rare: return 0.75f;
            case StatGrade.Epic: return 1f;
            case StatGrade.Unique: return 1.35f;
            default: return 1.8f;
        }
    }

    // 등급이 자신을 얼마나 강하게 드러내야 하는지 다섯 등급에 걸쳐 0..1로 정규화한 값. 호출자가
    // 튜닝한 최대값(오라 밝기, 라이트 세기, 이미시브 강도)에 곱해 쓰므로 "Normal은 은은하게,
    // Legendary는 확실하게" 규칙이 이펙트마다 손으로 정한 다섯 숫자가 아니라 이 램프 하나로 관리된다.
    // 0은 반환하지 않는다 - Normal 드롭도 희미한 오라를 갖고, 이 스케일의 최저값으로만 읽히면 된다.
    public static float GetAuraStrength(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 0.2f;
            case StatGrade.Rare: return 0.4f;
            case StatGrade.Epic: return 0.6f;
            case StatGrade.Unique: return 0.8f;
            default: return 1f;
        }
    }

    // 호출자의 "Normal" 기준 크기에 곱하는 배율 - 중간 등급 Epic을 축으로 삼는 위쪽 GetSizeScale과
    // 달리 드롭 오브젝트 크기는 끝점 램프가 가장 자연스럽다: Normal은 호출자가 이미 만들어 둔 크기
    // 그대로고, 희귀 등급은 위에서 축소되는 게 아니라 그보다 눈에 띄게 커진다.
    public static float GetPotionScale(StatGrade grade)
    {
        switch (grade)
        {
            case StatGrade.Normal: return 1f;
            case StatGrade.Rare: return 1.15f;
            case StatGrade.Epic: return 1.35f;
            case StatGrade.Unique: return 1.6f;
            default: return 2f;
        }
    }
}
