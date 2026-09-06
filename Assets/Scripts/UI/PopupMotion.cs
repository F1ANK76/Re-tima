using TMPro;
using UnityEngine;

// 팝업 텍스트 공통 모션: 튀어나오며 커지고, 떠오르며 사라진다.
public class PopupMotion : MonoBehaviour
{
    // 데미지 숫자: 짧게 뜨고 빨리 사라진다
    public static PopupMotion AttachDamage(GameObject go) => Attach(go, 0.5f, 0.25f, 0.30f);

    // 획득 알림: 읽을 시간이 필요해 더 오래 머문다
    public static PopupMotion AttachPickup(GameObject go) => Attach(go, 1.5f, 0.3f, 0.55f);

    // 재생이 끝나면 파괴 대신 비활성화하고 여기로 돌려보낸다
    public Transform RecycleParent { get; set; }

    private const float PopFraction = 0.2f;
    private const float StartScale = 0.4f;

    private float duration;
    private float riseDistance;
    private float opaqueFraction;

    private TMP_Text[] labels;
    private Color[] baseColors;
    private Vector3 startPos;
    private float elapsed;

    private static PopupMotion Attach(GameObject go, float duration, float riseDistance, float opaqueFraction)
    {
        PopupMotion m = go.AddComponent<PopupMotion>();
        m.duration = duration;
        m.riseDistance = riseDistance;
        m.opaqueFraction = opaqueFraction;
        m.Init();
        return m;
    }

    // AddComponent가 Awake를 즉시 부르므로 값 주입 후 여기서 초기화한다
    private void Init()
    {
        labels = GetComponentsInChildren<TMP_Text>();
        baseColors = new Color[labels.Length];
        for (int i = 0; i < labels.Length; i++) baseColors[i] = labels[i].color;

        startPos = transform.localPosition;
        transform.localScale = Vector3.one * StartScale;
    }

    // 풀에서 다시 꺼낼 때 -> 라벨은 그대로 두고 모션만 되감는다
    public void Replay()
    {
        elapsed = 0f;
        startPos = transform.localPosition;
        transform.localScale = Vector3.one * StartScale;

        // 첫 Update 전에 한 프레임이 그려질 수 있다 -> 지난 재생의 투명도를 미리 지운다
        for (int i = 0; i < labels.Length; i++) labels[i].color = baseColors[i];
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float popT = Mathf.Clamp01(t / PopFraction);
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(StartScale, 1f, Easing.OutBack(popT));

        transform.localPosition = startPos + Vector3.up * (riseDistance * Easing.OutQuad(t));

        float alpha = 1f - Mathf.Clamp01((t - opaqueFraction) / Mathf.Max(0.0001f, 1f - opaqueFraction));
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            Color c = baseColors[i];
            c.a = alpha;
            labels[i].color = c;
        }

        if (t >= 1f) Finish();
    }

    private void Finish()
    {
        if (RecycleParent != null)
        {
            transform.SetParent(RecycleParent, false);
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
