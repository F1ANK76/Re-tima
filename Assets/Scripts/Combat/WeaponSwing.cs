using System.Collections;
using UnityEngine;

public class WeaponSwing : MonoBehaviour
{
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingDuration = 0.15f;

    // 선택 사항 - 플레이어의 검에만 연결되어 있고, 몬스터는 이것 없이 스윙한다.
    [SerializeField] private ParticleSystem slashVfx;

    // 공격 애니메이션이 타격을 적중시키기까지 걸리는 시간. Attack01_SwordAndShiled의
    // 실제 클립 길이(FBX 임포트 데이터 기준 30fps에서 0-16 프레임)와 일치한다 - 공격 클립이
    // 바뀌면 인스펙터에서 조정할 것.
    [SerializeField] private float attackImpactDelay = 16f / 30f;
    public float AttackImpactDelay => attackImpactDelay;

    // Attack01이 끝나고 Idle로 블렌딩되어 돌아오기까지 걸리는 총 시간: SwordAndShieldStance.controller의
    // Attack01 -> Idle 전환은 클립의 90% 지점에서 시작해 0.15초에 걸쳐 블렌딩되므로, 이 시간이
    // 지나기 전에 재트리거하면 현재 스윙이 애니메이션 도중에 잘려버린다. 이보다 빠르게 들어오는
    // 재트리거는 시각적으로만 무시된다 - 게임플레이상의 공격 틱/데미지 타이밍에는 영향을 주지 않는다.
    [SerializeField] private float attackAnimSettleDuration = 0.9f * (16f / 30f) + 0.15f;
    private float nextAnimTriggerAllowedTime;

    private Quaternion restRotation;
    private Coroutine activeSwing;
    private Coroutine slashVfxRoutine;

    private bool animatorSearched;
    private Animator characterAnimator;

    // 캐릭터 모델(자체 Animator를 가진)은 이 스윙 피벗의 조상이 아니라, 같은 부모 아래의
    // 형제(sibling)로 존재한다 - 그래서 GetComponentInParent 대신 부모를 거쳐
    // 옆으로 검색한다.
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

    // 다른 무언가(패링)가 현재 스윙을 끝까지 재생시키지 않고 중간에 끊어야 할 때 호출된다 -
    // 예정된 타격/정착 타이밍에 도달할 때까지 기다리지 않고 즉시 칼날을 원위치로 되돌리고
    // 슬래시를 제거한다.
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

    // (블레이드 프롭 자체의 빠른 회전이 아니라) 애니메이션 자체와 동일한 클립 데이터를
    // 기준으로 타이밍을 맞춘다 - 슬래시는 블레이드가 실제로 닿는 순간에 나타나고,
    // 공격 애니메이션이 Idle로 정착을 마치는 즉시 사라진다.
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

        // 피벗의 로컬 X축을 기준으로 스윙한다. 플레이어와 몬스터 모두 로컬 X축이 카메라의
        // 시선축을 따라 놓여 있어서(서로 마주보고 있으므로 월드 방향은 반대) 블레이드가 화면
        // 평면을 가로질러 완전히 보이는 상태로 휘둘러진다 - 같은 각도만으로도 양쪽 모두
        // 자동으로 좌우 대칭된 스윙이 만들어진다. 대신 로컬 Z축을 기준으로 회전시키면
        // 블레이드가 카메라 쪽으로/에서 멀어지는 방향으로 휘둘러져 원근 단축으로 짧아 보이고
        // 공격 도중 사라지는 것처럼 보였다.
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
