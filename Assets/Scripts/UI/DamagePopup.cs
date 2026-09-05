using UnityEngine;

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
        tm.text = amount.ToString("0.#");

        go.AddComponent<Billboard>();
        PopupMotion.AttachDamage(go);
    }
}
