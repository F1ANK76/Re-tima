using UnityEngine;

public class StatDropPopup : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private int fontSize = 72;
    [SerializeField] private float characterSize = 0.065f;

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
        var go = new GameObject("StatDropPopup");
        go.transform.SetParent(anchor, false);
        // 드롭이 연달아 몇 개 들어와도 팝업들이 완전히 겹치지 않도록 가로로 흩어지게 한다.
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        string amountText = statType == StatType.Attack ? amount.ToString("0.#") : amount.ToString("0");
        string text = $"+{amountText} {GetStatAbbreviation(statType)}";

        PopupText.Build(go, font, text, fontSize,
            characterSize * GradeVisuals.GetSizeScale(grade),
            GradeVisuals.GetPopupTextColor(grade));

        go.AddComponent<Billboard>();
        go.AddComponent<StatDropPopupMotion>();

        SpawnSparkleBurst(go.transform, grade);
    }

    private static string GetStatAbbreviation(StatType statType) => statType == StatType.Attack ? "ATK" : "HP";

    private void SpawnSparkleBurst(Transform parent, StatGrade grade)
    {
        ParticleSystem burst = Instantiate(sparkleBurstPrefab, parent);
        burst.transform.localPosition = Vector3.zero;

        float strength = GradeVisuals.GetAuraStrength(grade);
        burst.transform.localScale = Vector3.one * strength;

        Color color = GradeVisuals.GetColor(grade);

        var main = burst.main;
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
