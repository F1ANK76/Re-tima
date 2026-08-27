using System.Collections;
using UnityEngine;

public class WeaponSwing : MonoBehaviour
{
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingDuration = 0.15f;

    // 선택 사항 - 플레이어의 검에만 연결되어 있고, 몬스터는 이것 없이 스윙한다.
    [SerializeField] private ParticleSystem slashVfx;

    // 공격 애니메이션이 타격을 적중시키기까지의 시간. Attack01_SwordAndShiled의 실제 클립 길이
    // (FBX 임포트 데이터 기준 30fps에서 0-16 프레임)와 일치 - 공격 클립이 바뀌면 인스펙터에서 조정.
    [SerializeField] private float attackImpactDelay = 16f / 30f;
    public float AttackImpactDelay => attackImpactDelay;

    // Attack01이 끝나고 Idle로 블렌딩되어 돌아오기까지의 총 시간: SwordAndShieldStance.controller의
    // Attack01 -> Idle 전환이 클립의 90% 지점에서 시작해 0.15초간 블렌딩되므로, 이 전에 재트리거하면
    // 스윙이 애니메이션 도중에 잘린다. 더 빠른 재트리거는 시각적으로만 무시되고, 게임플레이상의
    // 공격 틱/데미지 타이밍에는 영향이 없다.
    [SerializeField] private float attackAnimSettleDuration = 0.9f * (16f / 30f) + 0.15f;
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
            nextAnimTriggerAllowedTime = Time.time + attackAnimSettleDuration;
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

    // 블레이드 프롭 자체의 빠른 회전이 아니라 애니메이션과 동일한 클립 데이터에 타이밍을 맞춘다 -
    // 슬래시는 블레이드가 실제로 닿는 순간 나타나고, 공격 애니메이션이 Idle로 정착을 마치면 사라진다.
    private IEnumerator PlaySlashVfxAtImpact()
    {
        yield return new WaitForSeconds(attackImpactDelay);
        slashVfx.Play(true);

        yield return new WaitForSeconds(attackAnimSettleDuration - attackImpactDelay);
        slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
