using System.Collections;
using UnityEngine;

// CombatLoop보다 먼저 실행되어야 한다: 두 Update()가 같은 프레임에 각자의 임계값(틱 간격 /
// 충전 완료)을 넘길 수 있고 먼저 실행되는 쪽이 그 프레임을 가져간다. 순서를 강제하지 않으면
// IsSuspended 세팅 전에 CombatLoop의 틱이 일반 스윙을 적중시켜 일반 타격과 궁극기가 동시 발동한다.
[DefaultExecutionOrder(-100)]
public class UltimateManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private WeaponSwing weaponSwing;
    [SerializeField] private UltimateGaugeView gaugeView;
    [SerializeField] private ParticleSystem explosionVfx;
    [SerializeField] private GameObject stageBanner;
    [SerializeField] private CombatLoop combatLoop;

    private const float ChargeDuration = 15f;
    private const float DamageMultiplier = 3f;

    private const float UltimateAnimDuration = 24f / 30f;
    // 클립 자체의 루트 모션 커브상 캐릭터는 24프레임 중 19프레임(30fps)째에 이미 지면 높이로
    // 돌아와 있다 - 전체 길이에서 대충 추측한 값이 아니라 실제 착지 순간이다.
    private const float LandingDelay = 19f / 30f;
    // 폭발을 계속 방출시킬 시간. 시스템 자체 파티클(연기)은 최대 1초까지 살아있으므로 이 값은
    // 버스트보다 길기만 하면 된다 - 실제 시각적 길이를 의미하지 않는다.
    private const float ExplosionVfxDuration = 0.5f;

    // 궁극기는 스테이지 1 보스 클리어 보상이므로 스테이지 1 내내 완전 비활성(게이지도 숨김)이다 -
    // 클리어 시 해금을 알리는 StageManager.SkillUnlockStage 참고.
    private const int UnlockStage = 2;

    private float chargeTimer;
    private Monster currentMonster;
    // 패링이 애니메이터를 가로채 궁극기를 하드컷했을 때 ParryManager가 CancelUltimate()로
    // 멈춰 세울 수 있도록 실행 중인 코루틴을 들고 있는다.
    private Coroutine ultimateRoutine;
    private int currentMainStage = 1;
    private bool IsUnlocked => currentMainStage >= UnlockStage;

    private void OnEnable()
    {
        GameEvents.OnMonsterSpawned += HandleMonsterSpawned;
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterSpawned -= HandleMonsterSpawned;
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnStageChanged -= HandleStageChanged;
    }

    // 첫 OnStageChanged는 StageManager가 스테이지 1-1을 알린 뒤, 즉 실행 후 몇 프레임 지나 도착한다 -
    // 이게 없으면 그 첫 이벤트가 게이지를 숨기기 전까지 화면에 잠깐 번쩍인다.
    private void Start()
    {
        if (gaugeView != null) gaugeView.gameObject.SetActive(IsUnlocked);
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        currentMonster = monster;
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (currentMonster == monster) currentMonster = null;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
        chargeTimer = 0f;
        if (gaugeView != null)
        {
            gaugeView.SetFraction(0f);
            // 비워둔 채로 놔두지 않고 아예 숨긴다: 스테이지 1 내내 항상 0%인 바는
            // 잠긴 스킬이 아니라 고장난 게이지처럼 보인다.
            gaugeView.gameObject.SetActive(IsUnlocked);
        }
    }

    private void Update()
    {
        // 해금 전에는 충전도, 발동도, CombatLoop 억제도 없다 - 스테이지 1은 이 스킬이 존재하기
        // 전과 완전히 동일하게 진행된다.
        if (!IsUnlocked) return;

        // 스테이지 배너는 뭔가 스폰되기 전 몇 초간 화면을 덮는다 - 그 사이 충전되면 플레이어가
        // 보지도 못하는 빌드업을 낭비하는 셈이므로, 배너가 사라질 때까지 0에서 대기시킨다.
        if (stageBanner != null && stageBanner.activeSelf) return;

        // 가득 찬 이후로도 계속 흐르게 두지 않고 클램프한다 - 충전이 끝나면 게이지를 100%로 유지한
        // 채 몬스터가 근처에 있고 현재 애니메이션이 끝나기를 기다릴 뿐이다.
        chargeTimer = Mathf.Min(chargeTimer + Time.deltaTime, ChargeDuration);
        if (gaugeView != null) gaugeView.SetFraction(chargeTimer / ChargeDuration);

        // 단순히 "몬스터가 존재한다"만으로는 부족하다 - 실제로 근접 사거리 안에 있어야
        // 하며, 이는 CombatLoop가 일반 타격을 날리기 전에 확인하는 것과 같은 조건이다.
        bool targetInRange = currentMonster != null && currentMonster.HasArrived;
        // CombatLoop는 이 클래스가 궁극기용으로 세팅할 때뿐 아니라 자신의 승리/죽음 포즈에도 이
        // 플래그를 세운다 - 보스전은 킬 확정 순간 게이지가 거의 항상 꽉 차 있을 만큼 길어서, 그
        // 포즈 도중 궁극기가 나가면 트리거 기반 전환 위에 hard-Play()로 그대로 덮어써버린다.
        bool combatLoopBusy = combatLoop != null && combatLoop.IsSuspended;
        bool chargeReady = chargeTimer >= ChargeDuration && targetInRange && !combatLoopBusy;

        // 게이지가 가득 차는 순간부터 CombatLoop가 새 일반 스윙을 시작하지 못하게 막는다 - 아니면
        // 아래에서 안전한 타이밍을 기다리는 동안 스윙이 몇 번이고 이어지는데, 애니메이터가 진짜
        // 안전한 틈에 머무는 시간은 한 스윙이 정착하고 다음 틱이 재트리거하기까지 한두 프레임뿐이다.
        if (combatLoop != null) combatLoop.SuppressNewAttacks = chargeReady;

        if (chargeReady && IsSafeToInterrupt())
        {
            chargeTimer = 0f;
            if (combatLoop != null) combatLoop.SuppressNewAttacks = false;
            ultimateRoutine = StartCoroutine(PlayUltimate());
        }
    }

    // Idle이나 달리기 사이클에서만 끼어들 수 있다 - 공격/패링/리포스트 도중에는 절대 안 되며,
    // 그래야 일반 타격/방어/카운터가 항상 스스로 끝까지 재생된다.
    private bool IsSafeToInterrupt()
    {
        // 애니메이터 상태보다 먼저 체크한다: PlaySwing() 직후 애니메이터가 공격 상태라고 보고하기까지
        // 한두 프레임 동안 스윙 데미지가 여전히 진행 중일 수 있고, 바로 그 틈에 끼어드는 것이 일반
        // 타격과 궁극기가 함께 적중하던 문제의 원인이었다.
        if (combatLoop != null && combatLoop.HasPendingAttack) return false;

        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator == null) return true;
        if (animator.IsInTransition(0)) return false;

        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(PlayerAnimStates.Idle) || state.IsName(PlayerAnimStates.Running);
    }

    private IEnumerator PlayUltimate()
    {
        // 애니메이션 재생 중만이 아니라 시퀀스 전체를 억제한다 - 아니면 CombatLoop의 틱이 궁극기와
        // 같은 박자에 일반 스윙(과 그 Attack 트리거)을 적중시키는, 이 코드가 막으려는 "둘 다 동시
        // 발동" 버그가 난다. `enabled` 대신 IsSuspended를 쓰는 이유: `enabled`를 토글하면 CombatLoop의
        // OnDisable이 OnMonsterDied 구독을 해제해, 이 궁극기의 킬이 타겟을 정리하지 못하고 재개 후에도
        // 시체를 계속 공격하게 된다.
        if (combatLoop != null) combatLoop.PushSuspend();

        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator != null) animator.Play(PlayerAnimStates.Ultimate, 0, 0f);

        yield return new WaitForSeconds(LandingDelay);

        Monster target = currentMonster;
        if (player != null && target != null)
        {
            target.TakeDamage(player.Stats.AttackPower * DamageMultiplier);
        }

        if (explosionVfx != null) StartCoroutine(PlayExplosionVfx());

        yield return new WaitForSeconds(UltimateAnimDuration - LandingDelay);

        if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Ultimate))
        {
            animator.Play(PlayerAnimStates.Idle, 0, 0f);
        }

        if (combatLoop != null) combatLoop.PopSuspend();
        ultimateRoutine = null;
    }

    // 패링이 애니메이터를 Defend로 하드컷하는 순간 ParryManager가 호출한다. StopCoroutine은 대기 중인
    // 코드를 실행해주지 않으므로 - 아직 데미지/폭발 전이면 그대로 증발시키고, 서스펜드 해제와 코루틴
    // 정리는 여기서 직접 해준다. 이미 데미지가 나간 뒤라면(랜딩 딜레이 경과 후) 그 데미지 자체는
    // 되돌리지 않는다 - 취소는 "아직 안 벌어진 것"만 막을 수 있다.
    public void CancelUltimate()
    {
        if (ultimateRoutine == null) return;

        StopCoroutine(ultimateRoutine);
        ultimateRoutine = null;

        if (combatLoop != null) combatLoop.PopSuspend();
    }

    private IEnumerator PlayExplosionVfx()
    {
        if (player != null) explosionVfx.transform.position = player.transform.position;

        explosionVfx.Play(true);
        yield return new WaitForSeconds(ExplosionVfxDuration);
        // StopEmittingAndClear가 아니라 StopEmitting. Clear는 아직 날아다니는 파티클까지 지웠는데,
        // 연기는 1초를 꽉 채워 살도록 만들었지만 이 대기는 그보다 짧아서 폭발이 도중에 잘려 한 프레임
        // 만에 사라졌다 - 궁극기가 큰 기술로 느껴지지 않던 주된 이유. 이제 방출만 멈추고 이미 공중에
        // 나간 것들은 스스로 재생을 마치고 사라진다.
        explosionVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
