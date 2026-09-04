using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 상단 중앙에서 한 번 재생되는 팝인/유지/페이드아웃 연출.
// 처치 시 "CLEAR !", 등장 예고 시 "Elite Boss !"/"Stage Boss !" -> 같은 연출을 다른 문구로 재사용.
// 아무도 이걸 기다리지 않는다 -> unscaled time으로 돌며, 무슨 일이 벌어지든 스스로 끝낸다.
public class ClearBannerView : CancellableBannerView
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

    private Vector2 restPosition;

    // 팝인부터 완전히 페이드아웃될 때까지의 시간 - 표시되는 게 아니라 완전히 사라질 때까지 기다려야
    // 하는 호출자는, 위 세 값이 재조정되면 어긋날 지속시간을 하드코딩하는 대신 이 값을 읽는다.
    public float TotalPlayDuration => popInDuration + holdDuration + fadeOutDuration;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

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

        label.text = text;

        gameObject.SetActive(true);
        routine = StartCoroutine(PlayRoutine());
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
            scaleTarget.localScale = Vector3.one * Easing.PopCurve(k, startScale, punchScale);
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
}
