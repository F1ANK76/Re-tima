using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 스테이지 1은 낮, 스테이지 2는 밤. 후처리만으로는 표현이 안 된다 - 그레이딩은 이미 있는
// 빛에 색조만 다시 입힐 뿐이다. 그래서 이 스크립트가 네 가지를 함께 크로스페이드시킨다:
//   * 전역 Volume 두 개(낮/밤 그레이딩)를 교체가 아니라 weight로 블렌딩 - 스테이지가
//     바뀌는 프레임에 색이 뚝 끊기지 않고 연속적으로 변한다
//   * 태양: 색상 + 강도 (따뜻하고 밝은 빛 -> 창백하고 차가운 달빛)
//   * 하늘: 스카이박스 자체의 낮/밤 큐브맵 블렌드 슬라이더
//   * 안개: 색상 + 밀도, 이것이 실제로 나무 레이어들에 깊이감을 준다
//
// 항상 활성인 오브젝트에 붙어 GameEvents.OnStageChanged를 구독한다. 전환은 언스케일드
// 타임이라 스테이지 배너가 게임플레이를 정지시킨 동안에도 계속 재생된다.
public class StageMoodController : MonoBehaviour
{
    [Header("Volumes (both global, same priority)")]
    [SerializeField] private Volume dayVolume;
    [SerializeField] private Volume nightVolume;

    [Header("Sun")]
    [SerializeField] private Light sun;
    [SerializeField] private Color daySunColor = new Color(1f, 0.96f, 0.89f);
    [SerializeField] private float daySunIntensity = 2f;
    [SerializeField] private Color nightSunColor = new Color(0.62f, 0.70f, 0.95f);
    [SerializeField] private float nightSunIntensity = 0.85f;

    [Header("Ambient (Skybox mode uses this as a multiplier)")]
    [SerializeField] private float dayAmbientIntensity = 1f;
    [SerializeField] private float nightAmbientIntensity = 0.65f;

    [Header("Sky")]
    // 이 프로젝트 스카이박스는 Skybox/Cubemap Blend 셰이더로 _Tex에 낮, _Tex_Blend에 밤
    // 큐브맵을 담는다. 따라서 낮->밤 하늘 전체가 이 0~1 슬라이더 하나로 표현된다.
    [SerializeField] private string skyBlendProperty = "_CubemapTransition";
    [SerializeField] private float daySkyBlend = 0f;
    [SerializeField] private float nightSkyBlend = 0.85f;

    [Header("Fog")]
    [SerializeField] private bool driveFog = true;
    [SerializeField] private Color dayFogColor = new Color(0.78f, 0.85f, 0.92f);
    [SerializeField] private float dayFogDensity = 0.012f;
    [SerializeField] private Color nightFogColor = new Color(0.13f, 0.15f, 0.30f);
    [SerializeField] private float nightFogDensity = 0.025f;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 1.2f;
    // 밤은 정확히 이 메인 스테이지 하나뿐, "이 스테이지 이상"이라는 하한선이 아니다 -
    // 스테이지 3은 스테이지 1의 낮 숲과 그 몬스터 구성을 그대로 다시 쓰므로(MonsterSpawner가
    // 새 구성을 딱 그 스테이지에만 적용하는 것 참고) 밤은 스테이지 2만의 것이다. 하한선으로
    // 두면 스테이지 2 이후 영원히 어두워져서 낮 -> 밤 -> 낮으로 읽히는 진행이 깨진다.
    [SerializeField] private int nightStage = 2;

    // 0 = 완전한 낮, 1 = 완전한 밤. 전환 도중 스테이지가 바뀌어도 처음으로 되돌아가지
    // 않고 실제 블렌드 값이 있는 지점부터 이어서 진행하기 위해 보관해둔다.
    private float nightAmount = -1f;
    private Coroutine transition;
    // 인스턴스화해두어 블렌드 슬라이더 값을 쓰더라도 디스크상의 공용 스카이박스
    // 애셋을 오염시키지 않도록 한다.
    private Material skyInstance;

    private void OnEnable()
    {
        GameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void Awake()
    {
        skyInstance = new Material(RenderSettings.skybox);
        RenderSettings.skybox = skyInstance;

        if (driveFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        float target = mainStage == nightStage ? 1f : 0f;

        // 런의 첫 스테이지는 페이드해올 이전 상태가 없다 - 게임이 크로스페이드 도중에
        // 시작된 것처럼 보이지 않도록 목표값으로 즉시 이동시킨다.
        if (nightAmount < 0f)
        {
            Apply(target);
            return;
        }

        if (Mathf.Approximately(nightAmount, target)) return;

        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        float from = nightAmount;
        float t = 0f;

        while (t < transitionDuration)
        {
            // 언스케일드: 스테이지 배너가 게임플레이를 일시정지시켜도, 분위기 전환은 전투가
            // 재개될 때까지 멈춰있지 않고 그 아래에서 계속 진행되어야 한다.
            t += Time.unscaledDeltaTime;
            Apply(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / transitionDuration)));
            yield return null;
        }

        Apply(target);
        transition = null;
    }

    private void Apply(float night)
    {
        nightAmount = Mathf.Clamp01(night);

        // 프로필 교체가 아니라 weight 블렌딩: URP가 두 프로필의 오버라이드 값을 블렌딩해주므로
        // 한 세트가 다른 세트로 뚝 끊기지 않고 모든 그레이딩 값이 함께 움직인다.
        dayVolume.weight = 1f - nightAmount;
        nightVolume.weight = nightAmount;

        sun.color = Color.Lerp(daySunColor, nightSunColor, nightAmount);
        sun.intensity = Mathf.Lerp(daySunIntensity, nightSunIntensity, nightAmount);

        RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, nightAmount);

        if (skyInstance.HasProperty(skyBlendProperty))
            skyInstance.SetFloat(skyBlendProperty, Mathf.Lerp(daySkyBlend, nightSkyBlend, nightAmount));

        if (driveFog)
        {
            RenderSettings.fogColor = Color.Lerp(dayFogColor, nightFogColor, nightAmount);
            RenderSettings.fogDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, nightAmount);
        }
    }

    private void OnDestroy()
    {
        Destroy(skyInstance);
    }
}
