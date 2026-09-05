using System.Collections;
using UnityEngine;

public class CombatLoop : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private CombatConfigSO combatConfig;
    [SerializeField] private WeaponSwing weaponSwing;
    [SerializeField] private GroundScroller groundScroller;
    [SerializeField] private BackdropScroller backdropScroller;

    private Monster currentMonster;
    private float tickTimer;
    private bool hasOpenedOnCurrent;
    private Coroutine pendingDamageRoutine;

    private int suspendCount;
    public bool IsSuspended => suspendCount > 0;

    public void PushSuspend() => suspendCount++;
    public void PopSuspend() => suspendCount = Mathf.Max(0, suspendCount - 1);

    public void ClearSuspend()
    {
        suspendCount = 0;
        suppressMovement = false;
    }

    public bool SuppressNewAttacks { get; set; }
    public bool RiposteInProgress { get; set; }
    // 승리 포즈가 "다음 몬스터에게 달려가기"로 즉시 덮어써지는 것을 막는다.
    private bool suppressMovement;

    private int idleHoldCount;
    public bool IsIdleHeld => idleHoldCount > 0;
    public void PushIdleHold() => idleHoldCount++;
    public void PopIdleHold() => idleHoldCount = Mathf.Max(0, idleHoldCount - 1);
    public void ClearIdleHold() => idleHoldCount = 0;

    public bool HasPendingAttack => pendingDamageRoutine != null;

    private void OnEnable()
    {
        GameEvents.OnMonsterSpawned += HandleMonsterSpawned;
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterSpawned -= HandleMonsterSpawned;
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    private void HandlePlayerDied()
    {
        CancelPendingAttack();

        currentMonster = null;
        hasOpenedOnCurrent = false;
        tickTimer = 0f;
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        currentMonster = monster;
        tickTimer = 0f;
        hasOpenedOnCurrent = false;
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (currentMonster != monster) return;

        currentMonster = null;
    }

    public IEnumerator PlayVictorySequence()
    {
        Animator animator = weaponSwing.PlayerAnimator;

        PushSuspend();

        suppressMovement = true;
        animator.SetBool(AnimParams.IsMoving, false);
        animator.SetTrigger(AnimParams.Victory);

        const float SafetyTimeout = 5f;
        float elapsed = 0f;
        while (elapsed < SafetyTimeout && !animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Victory))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < SafetyTimeout &&
               (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Victory)))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        suppressMovement = false;
        PopSuspend();
    }

    public IEnumerator PlayDeathSequence()
    {
        Animator animator = weaponSwing.PlayerAnimator;

        // PlayVictorySequence와 같은 이유의 가드 - 발동 직전의 궁극기가 이 트리거도 덮어쓴다.
        PushSuspend();

        animator.SetTrigger(AnimParams.Die);

        const float SafetyTimeout = 5f;
        float elapsed = 0f;
        while (elapsed < SafetyTimeout && !animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Die))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < SafetyTimeout &&
               (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Die)))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        PopSuspend();
    }

    private void Update()
    {
        // 배경 갱신 여부 결정
        UpdateWorldScroll();

        // 연출 재생 중이면 전투 로직 전체를 쉰다
        if (IsSuspended) return;

        // 이동이 억제되지 않고 제자리 상태도 아니고 몬스터도 도착하지 않은 상태라면 달린다.
        bool shouldBeMoving = !suppressMovement && !IsIdleHeld && (currentMonster == null || !currentMonster.HasArrived);

        // 그 값을 적용
        weaponSwing.PlayerAnimator.SetBool(AnimParams.IsMoving, shouldBeMoving);

        // 궁극기 발동 대기중이거나 패링 반격 중일때는 공격 X
        if (SuppressNewAttacks || RiposteInProgress) return;

        // 때릴 상대가 없거나, 아직 사거리 밖(걸어오는 중)이면 공격 X
        if (currentMonster == null || !currentMonster.HasArrived) return;

        // 이 몬스터에게 첫 타격인가? 그렇다면 tickInterval을 기다리지 않고 도착 즉시 때린다
        if (!hasOpenedOnCurrent)
        {
            hasOpenedOnCurrent = true;
            tickTimer = 0f;
            DoTick();
            return;
        }

        // 두 번째 타격부터는 combatConfig.tickInterval(초)마다 한 대씩. 아직 시간이 안 찼으면 대기.
        tickTimer += Time.deltaTime;
        if (tickTimer < combatConfig.tickInterval) return;

        tickTimer = 0f;
        DoTick(); // 스윙 애니메이션 재생 → 칼이 닿는 타이밍에 맞춰 실제 데미지(코루틴)
    }

    private void UpdateWorldScroll()
    {
        bool isRunning = false;

        // 달리기 상태 여부를 그대로 배경·바닥 스크롤에 반영
        if (!IsSuspended)
            isRunning = weaponSwing.PlayerAnimator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Running);

        groundScroller.IsScrolling = isRunning;
        backdropScroller.IsScrolling = isRunning;
    }

    private void DoTick()
    {
        Monster target = currentMonster;
        if (target == null) return;

        weaponSwing.PlaySwing(); // 칼 휘두르는 애님
        pendingDamageRoutine = StartCoroutine(DealDamageAfterSwing(target));
    }

    private IEnumerator DealDamageAfterSwing(Monster target)
    {
        float delay = weaponSwing.AttackImpactDelay;
        yield return new WaitForSeconds(delay);

        // 딜레이 후 실제 플레이어 평타 공격
        if (target != null) player.Attack(target);
        pendingDamageRoutine = null;
    }

    public void CancelPendingAttack()
    {
        if (pendingDamageRoutine != null)
        {
            StopCoroutine(pendingDamageRoutine);
            pendingDamageRoutine = null;
        }

        weaponSwing.CancelSwing();
    }
}
