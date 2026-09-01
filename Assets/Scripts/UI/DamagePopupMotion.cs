using UnityEngine;

// 플레이어 스탯 팝업보다 훨씬 짧게 상승/페이드/팝을 재생한다 - 데미지 숫자는 곧 몇 번 더 얻어맞을
// 가능성이 큰 적 위에 오래 머무르지 않고 빠르게 눈에 들어왔다 사라져야 한다.
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
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(popStartScale, 1f, Easing.OutBack(popT));

        transform.localPosition = startPos + Vector3.up * (riseDistance * Easing.OutQuad(t));

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
}
