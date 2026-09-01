using System.Collections;
using UnityEngine;

public class WeaponSwing : MonoBehaviour
{
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingDuration = 0.15f;

    // 선택 사항 - 플레이어의 검에만 연결되어 있고, 몬스터는 이것 없이 스윙한다.
    [SerializeField] private ParticleSystem slashVfx;
    // slashVfx 프리팹(Hovl "Snow Slash")은 Slash/Sparks/Snowflakes/Flash 4개 서브 파티클로
    // 구성되고, 그중 가장 긴 startLifetime이 0.5초다(실측 확인) - 이보다 짧게 유지하면
    // 눈송이/스파크가 자기 수명을 다 채우기도 전에 강제로 잘려서 제대로 안 쌓인 채 사라진다.
    // swingDuration(0.15초)에 맞춰 유지하던 이전 값(0.075초)은 이 이펙트가 자라나기엔
    // 턱없이 부족했다.
    [SerializeField] private float slashVfxHoldDuration = 0.5f;

    [SerializeField] private string attackClipName = "Attack01_SwordAndShiled";
    // 클립을 이름으로 못 찾았을 때만 쓰는 대체값 - Attack01_SwordAndShiled의 클립 길이(FBX
    // 임포트 데이터 기준 30fps에서 0-16 프레임)와 일치.
    [SerializeField] private float attackClipLengthFallback = 16f / 30f;

    // Attack01 클립의 실제 팔 회전 커브(Right Arm Front-Back)를 뽑아 확인한 값 - 클립 앞쪽
    // 32%는 검을 뒤로 빼는 예비동작(값이 계속 음수)이고, 35.6% 지점에서 음수->양수로 넘어가며
    // 실제로 앞을 향해 휘두르기 시작한다(0.5333초 클립 기준 0.19초). 데미지/슬래시는 이 "진짜로
    // 휘두르기 시작하는 순간"에 맞춰야 한다 - 코드 스윙 자체의 절반 지점(swingDuration*0.5)은
    // 이 스켈레톤 애니메이션과 아무 상관 없는 별개의 타이밍이라, 그 값을 쓰면 예비동작이 채
    // 끝나기도 전에 슬래시가 떴다.
    //
    // 초가 아니라 비율로 들고 있는 이유: 클립이 다른 길이로 교체돼도 임팩트가 비례해 따라가고,
    // 실제 길이는 아래에서 클립에서 직접 읽으므로 코드와 클립이 어긋날 여지가 줄어든다.
    private const float AttackImpactFraction = 0.356f;
    public float AttackImpactDelay => AnimClipTiming.ResolveClipTime(
        CharacterAnimator, attackClipName, attackClipLengthFallback, AttackImpactFraction);

    // SwordAndShieldStance.controller의 Attack01 -> Idle 전환 설정 그대로다: 클립의 이 지점에서
    // 전환이 시작되어, 이만큼의 시간에 걸쳐 블렌딩된다. 둘 다 컨트롤러의 전환(Transition) 자체에
    // 박힌 값이라 런타임 API로는 읽을 수 없어 상수로 들고 있다 - 컨트롤러에서 전환 설정을 바꾸면
    // 여기도 같이 맞춰야 한다.
    private const float AttackToIdleExitTime = 0.9f;
    private const float IdleBlendDuration = 0.15f;

    // Attack01 스켈레톤 클립이 Idle로 블렌딩되어 돌아오기까지의 총 시간 - 이 전에 재트리거하면
    // 스켈레톤 애니메이션이 도중에 끊긴다(더 빠른 재트리거는 시각적으로만 무시되고 공격 틱/데미지
    // 타이밍에는 영향 없음). 위의 AttackImpactDelay(시각적 타격 타이밍)와는 별개 개념이라, 여기는
    // 실제 클립 길이를 그대로 읽어온다.
    private float AttackAnimSettleDuration => AttackToIdleExitTime * ResolveClipLength(attackClipName, attackClipLengthFallback) + IdleBlendDuration;

    private float nextAnimTriggerAllowedTime;

    private Quaternion restRotation;
    private Coroutine activeSwing;
    private Coroutine slashVfxRoutine;

    private bool animatorSearched;
    private Animator characterAnimator;

    // 캐릭터 모델(자체 Animator 보유)은 이 스윙 피벗의 조상이 아니라 같은 부모 아래 형제(sibling)다
    // - 그래서 GetComponentInParent 대신 부모를 거쳐 옆으로 검색한다.
    public Animator CharacterAnimator
    {
        get
        {
            if (!animatorSearched)
            {
                animatorSearched = true;
                characterAnimator = transform.parent != null ? transform.parent.GetComponentInChildren<Animator>() : null;
            }
            return characterAnimator;
        }
    }

    private void Awake()
    {
        restRotation = transform.localRotation;
    }

    private float ResolveClipLength(string clipName, float fallback)
        => AnimClipTiming.ResolveClipLength(CharacterAnimator, clipName, fallback);

    public void PlaySwing()
    {
        if (activeSwing != null) StopCoroutine(activeSwing);
        activeSwing = StartCoroutine(SwingRoutine());

        if (slashVfx != null)
        {
            // 새 스윙이 항상 자신만의 깨끗한 이펙트로 시작하도록 짧게 끊는다 - 중단된 스윙의
            // 슬래시가 아직 재생 중이던 것 위에 겹쳐 쌓이지 않도록.
            if (slashVfxRoutine != null) StopCoroutine(slashVfxRoutine);
            slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slashVfxRoutine = StartCoroutine(PlaySlashVfxAtImpact());
        }

        if (CharacterAnimator != null && Time.time >= nextAnimTriggerAllowedTime)
        {
            CharacterAnimator.SetTrigger(AnimParams.Attack);
            nextAnimTriggerAllowedTime = Time.time + AttackAnimSettleDuration;
        }
    }

    // 다른 무언가(패링)가 진행 중인 스윙을 끝까지 재생시키지 않고 끊어야 할 때 호출 - 예정된
    // 타격/정착 타이밍을 기다리지 않고 즉시 칼날을 원위치로 되돌리고 슬래시를 제거한다.
    public void CancelSwing()
    {
        if (activeSwing != null)
        {
            StopCoroutine(activeSwing);
            activeSwing = null;
        }
        transform.localRotation = restRotation;

        if (slashVfxRoutine != null)
        {
            StopCoroutine(slashVfxRoutine);
            slashVfxRoutine = null;
        }
        if (slashVfx != null) slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // 시작 타이밍은 블레이드 프롭 회전(SwingRoutine)에 맞춘다 - 슬래시는 블레이드가 최대로
    // 뻗는 타격 순간에 나타난다. 근데 얼마나 오래 떠 있을지는 스윙 타이밍이 아니라 이
    // 파티클 이펙트 자체의 수명(slashVfxHoldDuration)에 맞춰야 한다 - 스윙은 0.15초 만에
    // 끝나지만 이 이펙트는 그보다 훨씬 오래(최대 0.5초) 재생돼야 제대로 보인다.
    private IEnumerator PlaySlashVfxAtImpact()
    {
        yield return new WaitForSeconds(AttackImpactDelay);
        slashVfx.Play(true);

        yield return new WaitForSeconds(slashVfxHoldDuration);
        // Clear가 아니라 Emitting만 멈춘다 - 이미 튀어나온 파티클(수명 최대 0.5초)이
        // 강제로 삭제되지 않고 자연스럽게 페이드되며 남은 수명을 마치게 한다.
        slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        slashVfxRoutine = null;
    }

    private IEnumerator SwingRoutine()
    {
        // restRotation이 아니라 블레이드의 현재 회전값에서 시작한다 - 그래야 아직 재생 중인
        // 스윙을 끊고 들어온 공격이 먼저 원위치로 튕기지 않고 부드럽게 이어진다.
        Quaternion startRotation = transform.localRotation;

        // 피벗의 로컬 X축 기준 스윙. 플레이어와 몬스터 모두 로컬 X축이 카메라 시선축을 따라 놓여
        // 있어(서로 마주보므로 월드 방향은 반대) 블레이드가 화면 평면을 가로질러 완전히 보이게
        // 휘둘러지고, 같은 각도 하나로 양쪽이 자동 좌우 대칭된다. 로컬 Z축으로 돌리면 블레이드가
        // 카메라 쪽으로/에서 멀어지며 휘둘러져 원근 단축으로 짧아 보이고 공격 중 사라지는 듯했다.
        Quaternion swungRotation = restRotation * Quaternion.Euler(swingAngle, 0f, 0f);
        float half = swingDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRotation, swungRotation, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(swungRotation, restRotation, t / half);
            yield return null;
        }

        transform.localRotation = restRotation;
        activeSwing = null;
    }
}
