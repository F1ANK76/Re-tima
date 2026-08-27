using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 상단 중앙에서 한 번 재생되는 팝인/유지/페이드아웃 연출. 원래는 엘리트/보스 처치 시
// "CLEAR !"만 표시하는 용도였는데, 지금은 Show(text)가 엘리트/보스 *등장* 알림
// ("Elite Boss !", "Final Boss !")에도 동일한 애니메이션을 재사용한다 - 동일한 연출을
// 다른 순간, 다른 문구로 쓰는 것이다. 순수하게 연출용이다 - 아무것도 이걸 기다리지 않으므로
// unscaled time으로 동작하며, 그 밖에 무슨 일이 벌어지고 있든(승리 포즈, 다음 스테이지
// 배너) 상관없이 그저 스스로 재생을 끝낸다.
[RequireComponent(typeof(CanvasGroup))]
public class ClearBannerView : MonoBehaviour
{
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private Text label;

    [SerializeField] private float popInDuration = 0.28f;
    [SerializeField] private float holdDuration = 0.9f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    // 등장할 때 최종 크기를 넘어섰다가 다시 그 크기로 줄어든다 - 단순히 0->1로
    // 스케일하면 텍스트가 그냥 나타나는 것처럼 보여서 임팩트가 느껴지지 않는다.
    [SerializeField] private float punchScale = 1.18f;
    [SerializeField] private float startScale = 0.45f;
    // 페이드되는 동안 위로 얼마나 떠오르는지 - 그냥 훅 사라지는 게 아니라 퇴장하듯 보이게 한다.
    [SerializeField] private float riseDistance = 45f;

    private CanvasGroup canvasGroup;
    private Vector2 restPosition;
    private Coroutine routine;

    // 팝인부터 완전히 페이드아웃될 때까지의 시간 - 배너가 그냥 표시되는 게 아니라 완전히
    // 사라질 때까지 기다려야 하는 호출자는, 위 세 값이 재조정되는 순간 어긋나 버릴
    // 지속시간을 하드코딩하는 대신 이 값을 읽는다.
    public float TotalPlayDuration => popInDuration + holdDuration + fadeOutDuration;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (scaleTarget == null) scaleTarget = (RectTransform)transform;

        // 첫 재생 전에 미리 캡처해둔다 - 애니메이션 자체가 위치를 덮어써버리기 때문에,
        // 나중에 읽으면 마지막 실행이 남겨놓은 값을 그대로 읽게 된다.
        restPosition = scaleTarget.anchoredPosition;

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void Show(string text = "CLEAR !")
    {
        // 이전 연출이 아직 진행 중일 때 두 번째 호출이 들어오면, 같은 트랜스폼 위에
        // 애니메이션을 하나 더 겹쳐 쌓는 게 아니라 처음부터 다시 시작한다.
        if (routine != null) StopCoroutine(routine);

        if (label != null) label.text = text;

        gameObject.SetActive(true);
        routine = StartCoroutine(PlayRoutine());
    }

    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private IEnumerator PlayRoutine()
    {
        scaleTarget.anchoredPosition = restPosition;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popInDuration);

            canvasGroup.alpha = Mathf.Clamp01(k * 3f);
            scaleTarget.localScale = Vector3.one * PopCurve(k);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        scaleTarget.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(holdDuration);

        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeOutDuration);

            canvasGroup.alpha = 1f - k;
            scaleTarget.anchoredPosition = restPosition + Vector2.up * (riseDistance * k);
            scaleTarget.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, k);
            yield return null;
        }

        routine = null;
        canvasGroup.alpha = 0f;
        scaleTarget.localScale = Vector3.one;
        scaleTarget.anchoredPosition = restPosition;
        gameObject.SetActive(false);
    }

    // 팝 애니메이션의 처음 ~60% 구간에서는 최종 크기를 넘어서까지 커졌다가, 이후 다시 그 크기로 서서히 안착한다.
    private float PopCurve(float k)
    {
        if (k < 0.6f)
        {
            float rise = k / 0.6f;
            return Mathf.Lerp(startScale, punchScale, 1f - (1f - rise) * (1f - rise));
        }

        float settle = (k - 0.6f) / 0.4f;
        return Mathf.Lerp(punchScale, 1f, settle * settle * (3f - 2f * settle));
    }
}
