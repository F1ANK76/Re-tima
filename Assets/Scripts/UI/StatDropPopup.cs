using UnityEngine;

// GameEvents.OnStatDropGained가 발생할 때마다 플레이어 체력바 위에 "+N STAT" 팝업을 띄운다 -
// 등급은 텍스트가 아니라 색과 크기로 표현한다. Canvas Text가 아니라 Billboard를 붙인 월드
// 스페이스 TextMesh를 쓴다 - 다른 떠다니는 전투 텍스트(ParrySuccessText, 체력바)의 관례다.
public class StatDropPopup : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    // 동일한 체력바 위에서 ParrySuccessText 자체의 배치와 맞춰서, 둘이 같은 계열의 떠다니는
    // 텍스트로 읽히게 한다.
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.65f, 0f);
    // 둘은 곱해진다: 렌더링 높이 = characterSize * fontSize / ~9이라 fontSize도 품질 옵션이
    // 아니라 characterSize와 똑같이 화면상 크기를 좌우한다. 일부러 "높은 fontSize + 낮은
    // characterSize"로 잡았다 - 36/0.13과 화면 크기는 같지만 글리프를 두 배 해상도로
    // 래스터화해 흐려짐을 막는다(WebGL 빌드에서 특히 안 좋게 보인다).
    //
    // characterSize는 Epic 등급의 크기다 - GradeVisuals.GetSizeScale이 여기서 위아래로 조정하고
    // (다섯 등급 중 Epic이 가운데), 가장 작은 Normal(0.55배)이 이 팝업의 가독성 하한선을 정한다.
    [SerializeField] private int fontSize = 72;
    [SerializeField] private float characterSize = 0.065f;

    // Hovl의 "Sparks explode <color>" 프리팹 아무거나 베이스로 쓸 수 있다 - 내장 색은 런타임에
    // 버려지고 교체되므로(SpawnSparkleBurst 참고) 어떤 변형이 들어가든 상관없다.
    [SerializeField] private ParticleSystem sparkleBurstPrefab;

    private Font font;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnEnable()
    {
        GameEvents.OnStatDropGained += HandleStatDropGained;
    }

    private void OnDisable()
    {
        GameEvents.OnStatDropGained -= HandleStatDropGained;
    }

    private void HandleStatDropGained(StatGrade grade, StatType statType, float amount)
    {
        if (anchor == null) return;

        var go = new GameObject("StatDropPopup");
        go.transform.SetParent(anchor, false);
        // 드롭이 연달아 몇 개 들어와도 팝업들이 완전히 겹치지 않도록 가로로 흩어지게 한다.
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        // ATK는 소수 단위(0.1/0.3/0.5/...)로 증가한다 - 정수로 반올림하면 Normal/Rare/Epic
        // 등급의 ATK 드롭이 전부 "+0 ATK"로 표시되어 버린다. HP는 어차피 항상 정수다.
        string amountText = statType == StatType.Attack ? amount.ToString("0.#") : amount.ToString("0");
        string text = $"+{amountText} {GetStatAbbreviation(statType)}";

        PopupText.Build(go, font, text, fontSize,
            characterSize * GradeVisuals.GetSizeScale(grade),
            GradeVisuals.GetPopupTextColor(grade));

        go.AddComponent<Billboard>();
        go.AddComponent<StatDropPopupMotion>();

        if (sparkleBurstPrefab != null) SpawnSparkleBurst(go.transform, grade);
    }

    private static string GetStatAbbreviation(StatType statType) => statType == StatType.Attack ? "ATK" : "HP";

    // 팝업의 자식으로 붙여 같은 스케일 펀치로 튀어나오고, StatDropPopupMotion이 팝업을 파괴할
    // 때 함께 정리된다 - 여기서 따로 관리할 수명이 없다.
    //
    // 미리 정해진 색상 변형 중 가까운 걸 고르는 대신 정확한 등급 색으로 다시 물들인다 - 팩에는
    // blue/green/pink/red/white/yellow뿐이라 GradeVisuals의 Epic 보라색이 없고, 손으로 고른
    // "그런대로 비슷한" 대체품은 색 램프가 나중에 바뀔 때마다 GradeVisuals와 어긋난다.
    private void SpawnSparkleBurst(Transform parent, StatGrade grade)
    {
        ParticleSystem burst = Instantiate(sparkleBurstPrefab, parent);
        burst.transform.localPosition = Vector3.zero;

        // 팝업 텍스트 자체의 크기가 이미 사용하고 있는 것과 동일한 램프다 - Legendary
        // 버스트는 단순히 색조만 다른 게 아니라 Normal보다 더 크고 더 조밀하게 보인다.
        float strength = GradeVisuals.GetAuraStrength(grade);
        burst.transform.localScale = Vector3.one * strength;

        Color color = GradeVisuals.GetColor(grade);

        var main = burst.main;
        // 원본 프리팹은 lengthInSec마다 계속 반복해서 버스트를 재생한다 - 스탯 획득당
        // 깔끔하게 한 번만 터지는 게 연출로 보이고, 반복 재생되면 오작동처럼 보인다.
        main.loop = false;
        main.startColor = color;

        var colorOverLifetime = burst.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var emission = burst.emission;
        ParticleSystem.Burst particleBurst = emission.GetBurst(0);
        particleBurst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(4f, 50f * strength));
        emission.SetBurst(0, particleBurst);
    }
}
