using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageBannerView : MonoBehaviour
{
    [SerializeField] private Text bannerText;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float popStartScale = 1.6f;
    // 오프닝 카드가 비어있는 상태로 얼마나 머무른 뒤 타이틀이 애니메이션으로 나타나는지 -
    // 이렇게 해야 커튼이 끊긴 프레임이 아니라 의도된 박자로 읽힌다.
    [SerializeField] private float openingHoldDuration = 0.4f;
    // OFF면 Image에 원래 지정된 단색 그대로 사용한다.
    [SerializeField] private bool useGeneratedBackdrop = true;

    private CanvasGroup canvasGroup;
    private CanvasGroup textGroup;
    private RectTransform textRect;
    private Coroutine routine;
    private Sprite generatedBackdrop;

    // 세션에서 처음 나오는 안내는 완전히 불투명한 커튼 뒤에서 시작해, 타이틀이 나타나기 전에
    // 게임이 보이는 일이 없도록 한다. 이후에 나오는 안내(새 서브스테이지, 사망 후 재시작)는
    // 플레이어가 이미 보고 있던 게임플레이 위로 서서히 페이드인된다.
    private bool hasShownOnce;

    private void Awake()
    {
        // 배경과 텍스트를 따로 페이드시키려면 각각에 그룹이 필요하다.
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (bannerText != null)
        {
            textRect = bannerText.rectTransform;
            textGroup = bannerText.gameObject.AddComponent<CanvasGroup>();
            // 씬이 어떤 텍스트로 저장되어 있었든 상관없이 빈 상태로 시작한다.
            textGroup.alpha = 0f;
        }

        // true시 런타임에 배경을 생성하고, false면 에디터에서 지정한 단색을 그대로 사용한다.
        if (useGeneratedBackdrop) ApplyGeneratedBackdrop();

        // 배너는 기본적으로 비활성화 상태로 시작한다 - 안내가 필요할 때 Show()를 호출한다.
        gameObject.SetActive(false);
    }

    // 배너가 활성화될 때마다 런타임에 배경을 생성한다
    private void ApplyGeneratedBackdrop()
    {
        Image image = GetComponent<Image>();
        if (image == null) return;

        generatedBackdrop = TitleCardBackdrop.Create();
        image.sprite = generatedBackdrop;
        // 원래 지정된 fill 색은 순검정이었는데, 그대로 두면 곱셈 블렌딩으로 그림이 지워져
        // 버린다.
        image.color = Color.white;

        // 밝은 카드를 쓰는 대가로 흰 텍스트가 파스텔 배경 위에 놓이게 되는데, 부드러운 자두색
        // 그림자를 넣으면 뒤의 하늘을 어둡게 만들지 않으면서 타이틀의 대비를 되찾을 수 있다.
        if (bannerText != null && bannerText.GetComponent<Shadow>() == null)
        {
            Shadow shadow = bannerText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.16f, 0.11f, 0.25f, 0.55f);
            shadow.effectDistance = new Vector2(4f, -4f);
        }
    }

    private void OnDestroy()
    {
        if (generatedBackdrop == null) return;

        // 런타임에 만들어졌으므로, 이 뒤에 있는 텍스처를 대신 정리해줄 것이 아무것도 없다.
        Destroy(generatedBackdrop.texture);
        Destroy(generatedBackdrop);
    }

    public void Show(string text, Action onComplete)
    {
        // 새 안내는 대기 중이던 이전 안내를 대체한다. 이렇게 하지 않으면 이전 루틴의
        // SetActive(false)가 진행 도중에 걸려 콜백이 실행되기 전에 새 루틴을 죽여버리고,
        // 그 루틴이 붙잡고 있던 스폰이 영영 일어나지 않는다.
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

    // 콜백을 실행하지 않고 대기 중인 안내를 폐기한다 - 사망으로 인해 배너 뒤에 대기 중이던
    // 스폰이 무효화될 때 사용한다. 애니메이션 없이 즉시 처리한다: 사망 후에는 재시작
    // 시퀀스의 나머지가 진행되기 전에 페이드가 끝날 때까지 기다릴 필요가 없다.
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
        // 먼저 한 프레임을 건너뛴다: Play 모드에 진입하는(또는 씬 로드가 끝나는) 프레임은
        // deltaTime이 매우 크며, 이를 배너 시간에 포함시키면 화면 노출 시간(그리고 아래의
        // 페이드들)이 짧아진다. 실시간(realtime) 대기를 사용해 지속 시간이 실제 시계 기준으로
        // 정확하게 유지되도록 한다.
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
