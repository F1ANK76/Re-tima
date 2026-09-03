using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 플레이어가 사망할 때 재생된다: 화면 전체를 어둡게 깔고 그 위로 펀치 인 되는 "FAIL !" 라벨을
// 띄운 뒤, 잠시 유지했다가 페이드 아웃한다 - 어두워짐과 텍스트가 함께 나타나고 사라지도록
// 하나의 CanvasGroup으로 제어한다. ClearBannerView와 달리 이건 fire-and-forget이 아니다:
// StageManager가 Play()에서 yield하므로 체크포인트 리와인드/리스폰은 이 연출이 끝난 뒤에야 일어난다.
[RequireComponent(typeof(CanvasGroup))]
public class FailBannerView : MonoBehaviour
{
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private Text label;

    [SerializeField] private float popInDuration = 0.25f;
    [SerializeField] private float holdDuration = 1f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    // 등장할 때 최종 크기를 넘어섰다가 다시 그 크기로 줄어든다 - 단순히 0->1로
    // 스케일하면 텍스트가 그냥 나타나는 것처럼 보여서 임팩트가 느껴지지 않는다.
    [SerializeField] private float punchScale = 1.15f;
    [SerializeField] private float startScale = 0.5f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    // unscaled time로 동작하므로 사망 시점에 Time.timeScale이 어떤 값이든 항상 동일하게
    // 재생된다 (어차피 이 시점엔 다른 무엇도 동작하고 있지 않을 것으로 예상된다).
    public IEnumerator Play(string text = "FAIL !")
    {
        label.text = text;

        scaleTarget.localScale = Vector3.one * startScale;
        gameObject.SetActive(true);

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popInDuration);

            canvasGroup.alpha = k;
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
            yield return null;
        }

        canvasGroup.alpha = 0f;
        scaleTarget.localScale = Vector3.one;
        gameObject.SetActive(false);
    }
}
