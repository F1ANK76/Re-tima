using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class StageBannerView : MonoBehaviour
{
    [SerializeField] private Text bannerText;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float popStartScale = 1.6f;
    // 오프닝 카드가 비어있는 상태로 얼마나 머무른 뒤 타이틀이 애니메이션으로 나타나는지 -
    // 이렇게 해야 커튼이 끊긴 프레임이 아니라 의도된 박자로 읽힌다.
    [SerializeField] private float openingHoldDuration = 0.4f;

    private CanvasGroup canvasGroup;
    private RectTransform textRect;
    private Coroutine routine;

    // 세션 첫 안내는 완전히 불투명한 커튼 뒤에서 시작해 타이틀 전에 게임이 보이지 않게 한다.
    // 이후 안내(새 서브스테이지, 사망 후 재시작)는 이미 보고 있던 게임플레이 위로 페이드인된다.
    private bool hasShownOnce;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (bannerText != null)
        {
            textRect = bannerText.rectTransform;
            // 씬이 어떤 텍스트로 저장되어 있었든 상관없이 빈 상태로 시작한다.
            if (textGroup != null) textGroup.alpha = 0f;
        }

        // 배너는 기본적으로 비활성화 상태로 시작한다 - 안내가 필요할 때 Show()를 호출한다.
        gameObject.SetActive(false);
    }

    public void Show(string text, Action onComplete)
    {
        // 새 안내는 대기 중이던 이전 안내를 대체한다. 안 그러면 이전 루틴의 SetActive(false)가
        // 도중에 걸려 콜백 전에 새 루틴을 죽이고, 그 루틴이 쥔 스폰이 영영 일어나지 않는다.
        Cancel();

        if (bannerText != null) bannerText.text = text;

        bool openOnBlack = !hasShownOnce;
        hasShownOnce = true;

        // 오브젝트를 활성화하는 것과 같은 호출에서 불투명하게 설정하여, 오프닝으로 들어가는
        // 도중에 씬이 가려지지 않은 채로 렌더링되는 프레임이 절대 생기지 않게 한다.
        canvasGroup.alpha = openOnBlack ? 1f : 0f;
        if (textGroup != null) textGroup.alpha = 0f;
        if (textRect != null) textRect.localScale = Vector3.one * popStartScale;
        gameObject.SetActive(true);
        routine = StartCoroutine(PlayThenHide(openOnBlack, onComplete));
    }

    // 콜백 없이 대기 중인 안내를 폐기한다 - 사망으로 배너 뒤의 스폰이 무효화될 때 쓴다.
    // 애니메이션 없이 즉시: 사망 후엔 재시작 시퀀스가 페이드 완료를 기다릴 이유가 없다.
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

    private IEnumerator PlayThenHide(bool openOnBlack, Action onComplete)
    {
        // 한 프레임 건너뛴다: Play 모드 진입(또는 씬 로드 완료) 프레임의 deltaTime이 매우 커서,
        // 배너 시간에 포함시키면 노출 시간과 아래 페이드가 짧아진다. 대기는 realtime으로 해서
        // 지속 시간이 실제 시계 기준으로 정확히 유지되게 한다.
        yield return null;

        if (openOnBlack)
        {
            // Show()에서 이미 완전히 검게 만들어 놓았으니, 타이틀이 나타나기 전에 잠깐
            // 머무르게 해서 커튼이 의도된 것처럼 보이게 한다.
            yield return new WaitForSecondsRealtime(openingHoldDuration);
        }
        else
        {
            yield return FadeGroup(canvasGroup, 0f, 1f, fadeDuration);
        }

        yield return PopTextIn();
        yield return new WaitForSecondsRealtime(displayDuration);

        // 텍스트는 따로 페이드아웃되지 않고 배경과 함께 사라진다 - 커튼이 한 번에 걷히면서
        // 그 아래의 스테이지가 드러난다.
        yield return FadeGroup(canvasGroup, 1f, 0f, fadeDuration);

        routine = null;
        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    // 타이틀을 과장된 팝 크기에서 원래 크기로 가라앉히면서 동시에 페이드인시킨다 - 그냥
    // 밋밋하게 나타나는 대신 안내가 약간의 무게감을 가지고 도착하는 것처럼 보인다.
    private IEnumerator PopTextIn()
    {
        if (textGroup == null && textRect == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            if (textGroup != null) textGroup.alpha = p;
            if (textRect != null)
                textRect.localScale = Vector3.one * Mathf.LerpUnclamped(popStartScale, 1f, EaseOutBack(p));
            yield return null;
        }

        if (textGroup != null) textGroup.alpha = 1f;
        if (textRect != null) textRect.localScale = Vector3.one;
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float fromAlpha, float toAlpha, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(t / duration));
            yield return null;
        }

        group.alpha = toAlpha;
    }

    private static float EaseOutBack(float x)
    {
        const float overshoot = 1.2f;
        const float c3 = overshoot + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + overshoot * m * m;
    }
}
