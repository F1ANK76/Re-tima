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

    // 다른 시스템(궁극기)이 `enabled`를 건드리지 않고도 틱 루프를 일시정지할 수 있게
    // 한다 - 컴포넌트를 비활성화하면 OnDisable이 실행되어 OnMonsterDied 구독이
    // 해제되므로, 일시정지 중에 킬이 나면 currentMonster가 절대 정리되지 않고
    // 재개 후 이미 죽은 오래된 타겟을 계속 공격하게 된다.
    //
    // 단순한 bool이 아니라 참조 카운트 방식이다. 궁극기의 suspend는 자신이 일으키는
    // 승리 시퀀스와 자주 겹친다 - 궁극기의 10배 타격이 LandingDelay 시점에 몬스터를
    // 죽이지만 궁극기 자신은 아직 재생할 꼬리 부분이 남아있어서, 두 억제가 동시에
    // 걸리는 상황이 생겼다. 저장 후 복원하는 bool 방식이었다면, 승리 시퀀스가 아직
    // 실행 중인 궁극기로부터 "억제되어 있었음 = true"를 캡처했다가 자신이 끝날 때
    // 그대로 다시 써버려서 플래그가 영구히 걸린 채로 남았을 것이다: 그러면 플레이어는
    // 다음 몬스터에게 두들겨 맞는 동안 Idle 상태로 서서 아무 공격도 하지 못했을 것이다.
    private int suspendCount;
    public bool IsSuspended => suspendCount > 0;

    public void PushSuspend() => suspendCount++;
    public void PopSuspend() => suspendCount = Mathf.Max(0, suspendCount - 1);

    // 리스폰 경로 전용 강제 리셋: 죽음(또는 디버그 스테이지 점프)은 StageManager에서
    // StopAllCoroutines를 실행하는데, 이는 시퀀스가 자기 자신의 suspend를 pop하기도
    // 전에 끊어버릴 수 있다. suppressMovement도 같은 이유로 함께 다룬다 -
    // PlayVictorySequence가 이걸 세팅하고 자기 시퀀스의 끝에서만 해제하므로, 똑같은
    // 인터럽트가 이 값을 true로 고정시킬 수 있고, 그러면 Update()가 캐릭터를 다시
    // 달리기 상태로 되돌리는 게 영구히 막혀버린다. 이런 강제 리셋 이후에는 전투와
    // 이동이 항상 살아있어야 하므로, 중단된 코루틴을 믿는 대신 여기서 둘 다 강제로
    // 되돌려 놓는다.
    public void ClearSuspend()
    {
        suspendCount = 0;
        suppressMovement = false;
    }
    // 게이지가 가득 차고 타겟이 사거리 안에 들어오면 UltimateManager가 세팅한다:
    // 새로운 틱이 시작되는 것을 막지만(이미 진행 중인 것은 건드리지 않는다) 궁극기를
    // 발동할 안전한 타이밍을 기다리는 시간이 스윙 몇 번 더 이어지며 늘어지지 않도록
    // 한다 - 애니메이터는 한 스윙이 끝난 직후의 짧은 틈에서 겨우 한두 프레임만
    // 머무르므로, 새 틱이 자유롭게 공격 상태를 재트리거할 수 있다면 궁극기의 체크가
    // 그 틈을 놓치기 십상이다.
    public bool SuppressNewAttacks { get; set; }
    // 성공한 패링의 리포스트가 지속되는 동안 ParryManager가 유지한다. 위의
    // SuppressNewAttacks와는 의도적으로 분리된 플래그다: UltimateManager가 매 프레임
    // 자신의 게이지 상태에 따라 그 플래그를 다시 써버리므로, 패링이 그걸 같이 쓰면
    // 바로 다음 프레임에 억제가 풀려버려 리포스트가 일반 스윙 밑에 깔려 재생될 것이다.
    public bool RiposteInProgress { get; set; }
    // true인 동안에는, 아직 근처에 몬스터가 없더라도 Update()가 플레이어를 다시
    // 달리기 상태로 되돌리지 않는다 - 승리 포즈가 "다음 몬스터에게 달려가기" 동작에
    // 바로 덮어써지는 것을 막아준다.
    private bool suppressMovement;

    // 스탯 물약이 땅에 있거나 플레이어를 향해 유도되는 동안 유지된다 - 아직 흡수 중인
    // 물약이 있는 동안에는 플레이어가 벌써 다음 몬스터를 향해 달려가지 않고 제자리에서
    // 기다린다. 여러 개가 동시에 날아다닐 수도 있으므로 위의 suspendCount처럼
    // 참조 카운트 방식이다.
    private int idleHoldCount;
    public bool IsIdleHeld => idleHoldCount > 0;
    public void PushIdleHold() => idleHoldCount++;
    public void PopIdleHold() => idleHoldCount = Mathf.Max(0, idleHoldCount - 1);
    // StageManager의 필드 초기화(죽음으로 인한 리스폰, 디버그 스테이지 점프)를 위한
    // 강제 리셋: 두 경로 모두 필드에 남아있는 픽업을 직접 Destroy()하는데, 여기에는
    // 아직 던져지는 도중이라 자신의 PopIdleHold에 도달하지 못한 픽업도 포함된다 -
    // Destroy는 그럴 기회를 절대 주지 않고, 새어나간 hold는 이후 플레이어를 영원히
    // idle 상태에 묶어두게 된다. 그것을 풀어줄 대상이 더 이상 살아있지 않기 때문이다.
    // 여기서는 조건 없이 0으로 초기화해도 안전하다: 초기화가 이 함수를 호출하는
    // 시점에는 이걸 잡고 있었을 만한 픽업이 이미 전부 사라진 뒤이기 때문이다.
    public void ClearIdleHold() => idleHoldCount = 0;

    // 일반 스윙의 틱이 발동하는 순간부터 실제로 데미지가 적중할 때까지 true다.
    // 애니메이터 자체의 상태는 PlaySwing()보다 한 프레임 정도 뒤처질 수 있으므로
    // (트리거 소비가 즉시 일어나지 않는다), UltimateManager는 일반 타격이 아직
    // 진행 중인지 알기 위해 애니메이터 상태에만 의존하지 않고 이걸 직접 확인한다.
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

    // StageManager는 방금 플레이어를 죽인 몬스터를 파괴하고 체크포인트에서 새로운
    // 조우를 리스폰시킨다 - DoTick이 곧 사라질 몬스터를 계속 틱질하지 않도록 오래된
    // 참조를 버린다.
    private void HandlePlayerDied()
    {
        // 이게 없으면, 치명타가 적중하기 전에 이미 대기 중이던 스윙이 그 이후에도
        // 그대로 적중해버린다 - 플레이어를 죽인 것과 같은 교환에서 몬스터도 죽어버려서,
        // 깔끔한 패배가 아니라 더블 KO처럼 보이게 된다.
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

    // 플레이어의 승리 포즈를 재생하고 실제로 끝날 때까지 기다린 뒤에 리턴한다 -
    // StageManager는 이게 완료될 때까지 다음 배너/스폰을 보류한다.
    public IEnumerator PlayVictorySequence()
    {
        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator == null) yield break;

        // 이 시퀀스 동안 궁극기도 함께 보류시킨다: 게이지는 보스전과 같은 30초에 걸쳐
        // 채워지므로 킬이 나는 순간 거의 항상 가득 차 있다. 막아두지 않으면
        // UltimateManager의 hard animator.Play()(트리거가 아니라서 이것 뒤에 큐잉될
        // 수 없다)가 발동하는 즉시 Victory 전환을 짓밟아버리고, 아래의 대기는 포즈가
        // 한 번도 보이지 못한 채 타임아웃돼버린다.
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

    // 플레이어의 죽음 포즈를 재생하고 끝날 때까지 기다린다(이후에는 애셋 팩 자체의
    // Die01_Stay -> GetUp -> Run 체인으로 알아서 넘어간다) - StageManager는 이 함수가
    // 리턴할 때까지 체크포인트 되감기/리스폰을 보류한다.
    public IEnumerator PlayDeathSequence()
    {
        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator == null) yield break;

        // PlayVictorySequence와 같은 가드이고 이유도 같다: 치명타가 적중하는 순간
        // 발동할 준비가 된 궁극기는 이 트리거 위에도 똑같이 hard-Play()로 덮어써버린다.
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
        // suspend 체크보다 먼저 실행되며, 의도적으로 그렇게 했다: 궁극기, 승리, 죽음
        // 시퀀스는 모두 PushSuspend를 호출하는데, 여기서 일찍 리턴해버리면 IsScrolling이
        // 마지막 값에 그대로 고정되던 문제가 있었다. 궁극기가 시작될 때 캐릭터가 마침
        // 달리는 중이었다면, 그 궁극기 전체가 재생되는 동안 세상이 계속 미끄러지듯
        // 흘러갔을 것이다.
        UpdateWorldScroll();

        if (IsSuspended) return;

        // 실제로 근접 사거리 안에 싸울 몬스터가 들어올 때까지 계속 달린다 - 몬스터가
        // 단순히 존재하는 것(아직 화면 밖에서 걸어 들어오는 중)만으로는 멈출 이유가
        // 안 되고, 도착해서 타격을 주고받을 준비가 된 몬스터만이 그 이유가 된다.
        bool shouldBeMoving = !suppressMovement && !IsIdleHeld && (currentMonster == null || !currentMonster.HasArrived);
        if (weaponSwing != null) weaponSwing.CharacterAnimator?.SetBool(AnimParams.IsMoving, shouldBeMoving);

        if (SuppressNewAttacks || RiposteInProgress) return;

        if (currentMonster == null || !currentMonster.HasArrived) return;

        // 첫 타격은 tickInterval을 온전히 다 기다리지 않고 몬스터가 도착하는 즉시
        // 적중한다.
        if (!hasOpenedOnCurrent)
        {
            hasOpenedOnCurrent = true;
            tickTimer = 0f;
            DoTick();
            return;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer < stageConfig.tickInterval) return;

        tickTimer = 0f;
        DoTick();
    }

    // 세상은 캐릭터가 실제로 달리기 상태일 때만 스크롤되며, 루프가 억제되어 있는
    // 동안에는 절대 스크롤되지 않는다 - 궁극기, 승리 포즈, 죽음 애니메이션 모두
    // 캐릭터를 제자리에 붙잡아두므로, 바닥과 배경도 함께 멈춰야 한다.
    //
    // 이동 플래그가 아니라 Animator의 현재 상태를 기준으로 판단한다: Idle<->달리기
    // 전환(그리고 아직 Idle로 블렌딩되는 중인 공격)은 정착하는 데 약간의 시간이
    // 걸리는데, 플래그를 기준으로 삼았더니 캐릭터가 눈에 띄게 달리기 시작하기도
    // 전에 바닥이 먼저 미끄러지기 시작했다.
    private void UpdateWorldScroll()
    {
        bool isRunning = false;
        if (!IsSuspended && weaponSwing != null && weaponSwing.CharacterAnimator != null)
            isRunning = weaponSwing.CharacterAnimator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Running);

        if (groundScroller != null) groundScroller.IsScrolling = isRunning;
        if (backdropScroller != null) backdropScroller.IsScrolling = isRunning;
    }

    private void DoTick()
    {
        // 지역 변수로 캡처해둔다: 공격이 동기적으로 OnMonsterDied를 트리거할 수 있고,
        // 그러면 다음 몬스터가 스폰되면서 여기로 돌아오기 전에 currentMonster가
        // 덮어써지기 때문이다.
        Monster target = currentMonster;
        if (target == null) return;

        if (weaponSwing != null) weaponSwing.PlaySwing();
        pendingDamageRoutine = StartCoroutine(DealDamageAfterSwing(target));
    }

    private IEnumerator DealDamageAfterSwing(Monster target)
    {
        float delay = weaponSwing != null ? weaponSwing.AttackImpactDelay : 0f;
        yield return new WaitForSeconds(delay);

        if (target != null) player.Attack(target);
        pendingDamageRoutine = null;
    }

    // 플레이어가 패링을 누르는 순간 ParryManager가 호출한다 - 이미 진행 중이던 일반
    // 스윙은, 포즈가 Defend로 전환된 이후에는 타격을 적중시키거나 슬래시를 계속
    // 보여줘서는 안 된다.
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
