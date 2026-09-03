using System.Collections;
using UnityEngine;

public class CombatLoop : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private WeaponSwing weaponSwing;
    [SerializeField] private GroundScroller groundScroller;
    [SerializeField] private BackdropScroller backdropScroller;

    private Monster currentMonster;
    private float tickTimer;
    private bool hasOpenedOnCurrent;
    private Coroutine pendingDamageRoutine;

    // `enabled`를 끄는 대신 이 카운터로 틱을 멈춘다 - 컴포넌트를 비활성화하면 OnDisable이
    // OnMonsterDied 구독을 해제해서, 정지 중 킬이 나면 죽은 몬스터를 계속 공격하게 된다.
    //
    // bool이 아니라 카운터인 이유: 궁극기의 억제와 그 궁극기가 유발한 승리 시퀀스가 겹친다.
    // bool을 저장/복원했다면 승리 시퀀스가 궁극기로부터 true를 캡처했다가 되돌려써서
    // 플래그가 영구 고착되고, 플레이어가 Idle로 서서 맞기만 했다.
    private int suspendCount;
    public bool IsSuspended => suspendCount > 0;

    public void PushSuspend() => suspendCount++;
    public void PopSuspend() => suspendCount = Mathf.Max(0, suspendCount - 1);

    // 리스폰/디버그 점프 전용 강제 리셋. 두 경로의 StopAllCoroutines가 시퀀스를 자기
    // Pop 전에 끊을 수 있어 카운터가 고착된다. suppressMovement도 PlayVictorySequence가
    // 끝에서만 해제하므로 같은 이유로 함께 되돌린다.
    public void ClearSuspend()
    {
        suspendCount = 0;
        suppressMovement = false;
    }

    // 새 틱만 막고 진행 중인 것은 건드리지 않는다(UltimateManager가 세팅). 애니메이터는
    // 스윙 직후의 틈에 한두 프레임만 머무르므로, 새 틱이 자유롭게 공격을 재트리거하면
    // 궁극기가 발동할 타이밍을 놓친다.
    public bool SuppressNewAttacks { get; set; }
    // 리포스트 지속 동안 ParryManager가 유지. 위 플래그와 분리한 이유: UltimateManager가
    // 매 프레임 그걸 덮어써서, 공유하면 다음 프레임에 억제가 풀려 리포스트가 일반 스윙에
    // 묻힌다.
    public bool RiposteInProgress { get; set; }
    // 승리 포즈가 "다음 몬스터에게 달려가기"로 즉시 덮어써지는 것을 막는다.
    private bool suppressMovement;

    // 흡수 중인 물약이 남아있는 동안 플레이어를 제자리에 세워둔다. 동시에 여러 개가
    // 날아다닐 수 있어 suspendCount처럼 카운터다.
    private int idleHoldCount;
    public bool IsIdleHeld => idleHoldCount > 0;
    public void PushIdleHold() => idleHoldCount++;
    public void PopIdleHold() => idleHoldCount = Mathf.Max(0, idleHoldCount - 1);
    // 필드 초기화(리스폰/디버그 점프)용 강제 리셋. 두 경로가 픽업을 Destroy하는데 던져지는
    // 중인 픽업은 PopIdleHold에 도달하지 못하고, 새어나간 hold를 풀어줄 대상이 사라져
    // 플레이어가 영구히 idle에 묶인다. 이 시점엔 픽업이 전부 정리된 뒤라 0으로 밀어도 안전하다.
    public void ClearIdleHold() => idleHoldCount = 0;

    // 틱 발동부터 데미지 적중까지 true. 트리거 소비가 즉시 일어나지 않아 애니메이터 상태는
    // PlaySwing보다 한 프레임 뒤처지므로, UltimateManager는 상태 대신 이걸 확인한다.
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

    // StageManager가 방금 플레이어를 죽인 몬스터를 파괴하고 체크포인트에서 다시 스폰하므로,
    // DoTick이 사라질 몬스터를 계속 틱질하지 않도록 오래된 참조를 버린다.
    private void HandlePlayerDied()
    {
        // 없으면 치명타 이전에 대기 중이던 스윙이 그 뒤에 적중해, 깔끔한 패배가 아니라
        // 더블 KO처럼 보인다.
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

    // 승리 포즈를 재생하고 실제로 끝난 뒤 리턴한다 - StageManager가 이걸 기다렸다가
    // 다음 배너/스폰을 진행한다.
    public IEnumerator PlayVictorySequence()
    {
        Animator animator = weaponSwing != null ? weaponSwing.PlayerAnimator : null;
        if (animator == null) yield break;

        // 궁극기 게이지는 30초로 채워져 킬 순간 거의 항상 가득이다. 막지 않으면
        // UltimateManager의 animator.Play()(트리거가 아니라 뒤에 큐잉되지 않는다)가
        // Victory 전환을 짓밟고, 아래 대기는 포즈를 한 번도 못 본 채 타임아웃된다.
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

    // 죽음 포즈 재생 후 리턴(이후는 애셋 팩의 Die01_Stay -> GetUp -> Run 체인이 알아서
    // 넘어간다) - StageManager는 이때까지 체크포인트 리스폰을 보류한다.
    public IEnumerator PlayDeathSequence()
    {
        Animator animator = weaponSwing != null ? weaponSwing.PlayerAnimator : null;
        if (animator == null) yield break;

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

    // 매 프레임 두 가지를 판단한다: (1) 지금 달리는 모션이어야 하나, (2) 지금 한 대 칠 타이밍인가.
    // 실제 전투 동작(스윙 재생, 데미지 적중)은 여기가 아니라 DoTick이 시작하는 코루틴이 수행한다 -
    // 이 함수는 "언제 시작할지"만 재는 감시자다.
    private void Update()
    {
        // 배경 갱신 여부 결정
        UpdateWorldScroll();

        // 연출 재생 중이면 전투 로직 전체를 쉰다
        if (IsSuspended) return;

        // 이동이 억제되지 않고 제자리 상태도 아니고 몬스터도 도착하지 않은 상태라면 달린다.
        bool shouldBeMoving = !suppressMovement && !IsIdleHeld && (currentMonster == null || !currentMonster.HasArrived);

        // 그 값을 적용
        if (weaponSwing != null) weaponSwing.PlayerAnimator?.SetBool(AnimParams.IsMoving, shouldBeMoving);

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

        // 두 번째 타격부터는 stageConfig.tickInterval(초)마다 한 대씩. 아직 시간이 안 찼으면 대기.
        tickTimer += Time.deltaTime;
        if (tickTimer < stageConfig.tickInterval) return;

        tickTimer = 0f;
        DoTick(); // 스윙 애니메이션 재생 → 칼이 닿는 타이밍에 맞춰 실제 데미지(코루틴)
    }

    // 캐릭터가 실제로 달리기 상태일 때만 스크롤한다 - 궁극기/승리/죽음은 모두 캐릭터를
    // 제자리에 붙잡으므로 바닥과 배경도 멈춰야 한다.
    //
    // 이동 플래그가 아니라 Animator 상태로 판단하는 이유: 전환이 정착하는 데 시간이
    // 걸려서, 플래그 기준으로는 캐릭터가 눈에 띄게 달리기 전에 바닥이 먼저 미끄러졌다.
    private void UpdateWorldScroll()
    {
        bool isRunning = false;

        // 달리기 상태 여부를 그대로 배경·바닥 스크롤에 반영
        if (!IsSuspended && weaponSwing != null && weaponSwing.PlayerAnimator != null)
            isRunning = weaponSwing.PlayerAnimator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Running);

        if (groundScroller != null) groundScroller.IsScrolling = isRunning;
        if (backdropScroller != null) backdropScroller.IsScrolling = isRunning;
    }

    private void DoTick()
    {
        Monster target = currentMonster;
        if (target == null) return;

        if (weaponSwing != null) weaponSwing.PlaySwing(); // 칼 휘두르는 애님
        pendingDamageRoutine = StartCoroutine(DealDamageAfterSwing(target));
    }

    private IEnumerator DealDamageAfterSwing(Monster target)
    {
        float delay = weaponSwing != null ? weaponSwing.AttackImpactDelay : 0f;
        yield return new WaitForSeconds(delay);

        // 딜레이 후 실제 플레이어 평타 공격
        if (target != null) player.Attack(target);
        pendingDamageRoutine = null;
    }

    // 패링 입력 순간 ParryManager가 호출 - 진행 중이던 일반 스윙은 포즈가 Defend로
    // 전환된 뒤에 타격을 적중시키거나 슬래시를 보여줘서는 안 된다.
    public void CancelPendingAttack()
    {
        if (pendingDamageRoutine != null)
        {
            StopCoroutine(pendingDamageRoutine);
            pendingDamageRoutine = null;
        }

        if (weaponSwing != null) weaponSwing.CancelSwing();
    }
}
