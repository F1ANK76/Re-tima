using UnityEngine;

// 상승/페이드/팝 애니메이션을 분리해서 StatDropPopup은 오브젝트만 만들어 넘긴다 - 각 팝업이
// 독립적으로 애니메이션하고 스스로 정리하므로, 드롭이 겹쳐 팝업이 동시에 떠 있어도 문제없다.
public class StatDropPopupMotion : MonoBehaviour
{
    [SerializeField] private float duration = 1.5f;
    // 일부러 짧게 잡았다: 이건 폭이 겨우 ~0.5 유닛인 체력바 위 로컬 공간에서 떠다니므로,
    // 멀리 날아가는 게 아니라 제자리에서 작게 팡 터지는 느낌으로 읽혀야 한다.
    [SerializeField] private float riseDistance = 0.3f;
    [SerializeField] private float popStartScale = 0.4f;
    // 페이드 시작 전까지 완전 불투명을 유지하는 시간 비율. 팝업은 실제로 읽혀야 하므로 넉넉히
    // 잡았다 - 0.30이었을 때는 숫자를 읽을 불투명 구간이 0.5초에도 한참 못 미쳤다.
    [SerializeField] private float opaqueFraction = 0.55f;

    // 루트만이 아니라 하위 모든 TextMesh를 대상으로 한다 - PopupText가 외곽선을 자식 사본
    // 8개로 만들어서, 채우기만 페이드하면 숫자가 사라진 뒤 검은 실루엣만 공중에 남는다.
    private TextMesh[] labels;
    private Color[] baseColors;
    private Vector3 startPos;
    private float elapsed;

    private void Awake()
    {
        labels = GetComponentsInChildren<TextMesh>();
        // 한 번만 캡처한다: alpha는 매 프레임 갱신되므로, 각 라벨의 현재 색을 다시 읽으면
        // 이미 여기서 써놓은 값을 그대로 되읽는 셈이 된다.
        baseColors = new Color[labels.Length];
        for (int i = 0; i < labels.Length; i++) baseColors[i] = labels[i].color;

        startPos = transform.localPosition;
        transform.localScale = Vector3.one * popStartScale;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 처음 5분의 1 구간에서 빠르게 펀치 인 하고, 나머지는 꾸준히 상승한다 - 나타나는
        // 순간 팡 튀는 게 이걸 그냥 떠다니는 텍스트가 아니라 터지는 느낌으로 만들어준다.
        float popT = Mathf.Clamp01(t / 0.2f);
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(popStartScale, 1f, Easing.OutBack(popT));

        transform.localPosition = startPos + Vector3.up * (riseDistance * Easing.OutQuad(t));

        float alpha = 1f - Mathf.Clamp01((t - opaqueFraction) / Mathf.Max(0.0001f, 1f - opaqueFraction));
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            Color c = baseColors[i];
            c.a = alpha;
            labels[i].color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
