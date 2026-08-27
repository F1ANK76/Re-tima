using UnityEngine;

// 방금 피격당한 몬스터의 HealthBar 위에 떠오르는 데미지 숫자 - 플레이어가 입힌 모든 데미지는
// Monster.TakeDamage를 거쳐 들어오므로(일반 공격과 궁극기 모두 여기로 도달한다), 어느 쪽이
// 때렸는지 신경 쓸 필요 없이 이 리스너 하나로 둘 다 처리할 수 있다.
public class DamagePopup : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private int fontSize = 32;
    [SerializeField] private float characterSize = 0.1f;

    private static readonly Color TextColor = new Color(1f, 0.95f, 0.85f);
    private Font font;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnEnable()
    {
        GameEvents.OnMonsterDamaged += HandleMonsterDamaged;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterDamaged -= HandleMonsterDamaged;
    }

    private void HandleMonsterDamaged(Monster monster, float amount)
    {
        if (monster == null || amount <= 0f) return;

        // 몬스터의 루트 transform이 아니라 자신의 HealthBar 아래에 부모로 붙인다 - HealthBar는
        // 이미 각 몬스터 타입의 머리 위에 오도록 조정되어 있고(Elite/Boss의 커진
        // transform.localScale에 맞춰 자동으로 스케일도 조정된다), 몬스터 타입별로 높이
        // 오프셋을 따로 만드는 대신 같은 앵커를 그대로 사용한다.
        HealthBarView healthBar = monster.GetComponentInChildren<HealthBarView>();
        Transform anchor = healthBar != null ? healthBar.transform : monster.transform;

        var go = new GameObject("DamagePopup");
        go.transform.SetParent(anchor, false);
        // 연속으로 몇 번 맞았을 때 숫자가 완전히 겹치지 않도록 수평으로 흩어지게 한다.
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.GetComponent<MeshRenderer>().material = font.material;
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = TextColor;
        // ATK는 이제 소수 단위(0.1/0.3/0.5/...)로 증가하므로 예를 들어 1.4 같은 데미지도 나올
        // 수 있다 - 정수로 반올림하면 작은 피해가 1이나 심지어 0으로 잘못 표시될 수 있다.
        tm.text = amount.ToString("0.#");

        go.AddComponent<Billboard>();
        go.AddComponent<DamagePopupMotion>();
    }
}

// 플레이어 자신의 스탯 팝업보다 훨씬 짧은 시간 동안 상승/페이드/팝 애니메이션을 재생한다 -
// 데미지 숫자는 곧이어 몇 번 더 얻어맞을 가능성이 큰 적 위에서 오래 머무르지 않고 빠르게
// 눈에 들어왔다가 사라져야 한다.
public class DamagePopupMotion : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float riseDistance = 0.25f;
    [SerializeField] private float popStartScale = 0.4f;

    private TextMesh label;
    private Vector3 startPos;
    private float elapsed;

    private void Awake()
    {
        label = GetComponent<TextMesh>();
        startPos = transform.localPosition;
        transform.localScale = Vector3.one * popStartScale;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 처음 5분의 1 구간에서는 빠르게 튀어나오고, 나머지 구간에서는 꾸준히 상승한다 -
        // 나타나는 순간 팍 튀어야 그냥 떠다니는 텍스트가 아니라 타격이 들어간 것처럼 보인다.
        float popT = Mathf.Clamp01(t / 0.2f);
        transform.localScale = Vector3.one * Mathf.Lerp(popStartScale, 1f, EaseOutBack(popT));

        transform.localPosition = startPos + Vector3.up * (riseDistance * EaseOutQuad(t));

        // 팝 애니메이션이 진행되는 동안은 완전 불투명을 유지하다가, 나머지 약 70% 구간에 걸쳐
        // 페이드아웃된다.
        float alpha = 1f - Mathf.Clamp01((t - 0.30f) / 0.70f);
        if (label != null)
        {
            Color c = label.color;
            c.a = alpha;
            label.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    private static float EaseOutBack(float x)
    {
        const float overshoot = 1.6f;
        const float c3 = overshoot + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + overshoot * m * m;
    }
}
