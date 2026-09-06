using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private int fontSize = 32;
    [SerializeField] private float characterSize = 0.1f;

    private static readonly Color TextColor = new Color(1f, 0.95f, 0.85f);
    private Transform poolRoot;

    private void Awake()
    {
        poolRoot = new GameObject("DamagePopupPool").transform;
        poolRoot.SetParent(transform, false);
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

        GameObject go = Rent();
        go.transform.SetParent(anchor, false);
        // 연속으로 몇 번 맞았을 때 숫자가 완전히 겹치지 않도록 수평으로 흩어지게 한다.
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        go.GetComponent<TMP_Text>().text = amount.ToString("0.#");

        go.SetActive(true);
        go.GetComponent<PopupMotion>().Replay();
    }

    // 재생 중인 팝업은 몬스터 밑에 붙어 있다 -> 풀 루트에 남은 자식이 곧 여분이다
    private GameObject Rent()
    {
        if (poolRoot.childCount > 0) return poolRoot.GetChild(0).gameObject;

        return Create();
    }

    private GameObject Create()
    {
        var go = new GameObject("DamagePopup");

        var tm = go.AddComponent<TextMeshPro>();
        tm.fontSize = fontSize * characterSize * PopupText.FontSizeScale;
        tm.alignment = TextAlignmentOptions.Center;
        tm.textWrappingMode = TextWrappingModes.NoWrap;
        tm.color = TextColor;

        go.AddComponent<Billboard>();
        PopupMotion.AttachDamage(go).RecycleParent = poolRoot;

        return go;
    }
}
