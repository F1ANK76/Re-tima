using UnityEngine;

// 방금 피격당한 몬스터의 HealthBar 위로 떠오르는 데미지 숫자. 플레이어의 모든 데미지는 일반 공격이든
// 궁극기든 Monster.TakeDamage를 거치므로, 어느 쪽이 때렸는지 신경 쓸 필요 없이 리스너 하나로 처리한다.
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

        // 몬스터 루트 transform이 아니라 자신의 HealthBar 아래에 붙인다 - HealthBar는 이미 각 몬스터
        // 타입의 머리 위로 조정돼 있고 Elite/Boss의 커진 transform.localScale에 맞춰 스케일도 따라가므로,
        // 타입별 높이 오프셋을 따로 만들지 않고 같은 앵커를 그대로 쓴다.
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
