using System.Collections;
using UnityEngine;

public class Monster : MonoBehaviour
{
    public MonsterType Type { get; private set; }
    public float MaxHp { get; private set; }
    public float CurrentHp { get; private set; }
    public float AttackPower { get; private set; }
    public bool HasArrived { get; private set; }

    private Transform moveTarget;
    private float moveSpeed;
    private float stoppingDistance;

    private HealthBarView healthBarCache;
    private HealthBarView HealthBar => healthBarCache ??= GetComponentInChildren<HealthBarView>();

    private WeaponSwing weaponSwingCache;
    private WeaponSwing WeaponSwing => weaponSwingCache ??= GetComponentInChildren<WeaponSwing>();

    [Header("Attack telegraph (Elite/Boss)")]
    // 공격이 시작되어 타격이 적중하기까지의 시간으로, 매 스윙마다 새로 뽑는다 - 플레이어가
    // 실제로 반응해야 하는 숫자다. 충전(윈드업) 클립은 정확히 이 시간만큼 채우도록 늘어난다.
    [SerializeField] private float minTimeToImpact = 0.5f;
    [SerializeField] private float maxTimeToImpact = 2f;
    [SerializeField] private string windUpStateName = "AttackWindUp";
    [SerializeField] private string idleStateName = "Idle";

    [SerializeField] private string windUpClipName = "Footman_Attack01_WindUp";
    [SerializeField] private string plainAttackClipName = "Attack01";
    [SerializeField] private float windUpClipFallbackLength = 0.3f;
    [SerializeField] private float plainAttackClipFallbackLength = 1.333f;
    // 윈드업 클립 자체의 타임라인에서 타격이 어느 지점에 적중하는지를, 원래(늘리기 전)
    // 길이에 대한 비율로 나타낸 값이다 - 별도의 타격 클립은 없고, 충전 동작 자체의
    // 착지 비트(뛰어올랐다가 쿵 하고 내려찍는 순간)가 곧 타격이다. PowerUpNoWeapon의
    // 경우 이는 2.933초 길이 중 t=2.7초 부근의 강한 웅크림 동작이다.
    [SerializeField] private float windUpImpactFraction = 0.92f;
    // PowerUpNoWeapon은 시작 부분에 강한 팔 휘두름(몇 프레임 만에 약 74도)이 있는데,
    // 이것만으로도 방망이를 휘두르는 것처럼 보인다 - 그래서 애니메이터의 Idle/Run ->
    // AttackWindUp 전환은 이 지점만큼 건너뛰고 클립에 진입한다. 저 전환들에 설정된
    // 오프셋과 반드시 일치해야 한다: 실제로 재생되는 건 (임팩트 - 진입) 구간뿐이므로,
    // 아래의 충전 속도는 전체 클립이 아니라 이 구간을 기준으로 계산된다.
    [SerializeField] private float windUpEntryOffset = 0.15f;
    // 타격이 적중한 뒤 다음 충전이 시작되기 전까지 두는 여유 시간. 이게 없으면 한
    // 공격의 착지 스윙이 다음 공격의 진입 동작으로 바로 이어져버려서, 방망이를
    // 연달아 두 번 휘두르는 것처럼 보였다.
    [SerializeField] private float postAttackPause = 0.45f;

    // 두 번째 텔레그래프 패턴(보스 전용) - 보스가 항상 같은 방식으로만 예고하지 않도록
    // 매 스윙마다 무작위로 선택된다. 첫 번째 패턴과 달리 이쪽은 충전도, 무작위 지연도
    // 없다: 그저 손대지 않은 클립을 원래 속도로 재생할 뿐이며, 타격은 그 고정된
    // 타임라인 안에서 창이 실제로 닿는 순간에 적중한다.
    [SerializeField] private string attack2StateName = "Attack02";
    [SerializeField] private string attack2ClipName = "Attack02";
    [SerializeField] private float attack2ClipFallbackLength = 1.333f;
    [SerializeField] private float attack2ImpactTime = 0.533f;

    private const string AttackSpeedParam = "AttackSpeed";
    private const string WindUpSpeedParam = "WindUpSpeed";
    private const string UseTelegraphParam = "UseTelegraph";
    private const string AttackPattern2Param = "AttackPattern2";

    [Header("Ultimate attack (Elite/Boss)")]
    // 플레이어 자신의 궁극기를 거울처럼 반영한다: 몬스터가 싸우는 동안 일정한
    // 타이머로 충전되다가, 일반 스윙 대신 한 번 무겁고 패링 불가능한 타격으로 발동한다.
    [SerializeField] private float ultimateChargeDuration = 5f;
    [SerializeField] private float ultimateDamageMultiplier = 3f;
    [SerializeField] private string ultimateStateName = "Attack03";
    // Footman_Attack03 클립의 내부 테이크 이름은 (헷갈리게도) "Victory"로 되어 있어서,
    // 무관한 Victory 스테이트 자체의 동명 클립과 충돌한다 - 애니메이터의 클립 목록에서
    // 이름으로 찾으면 모호해지므로, 다른 값들처럼 ResolveClipLength를 거치지 않고
    // 그냥 클립의 실제 길이를 직접 적어둔 것이다.
    [SerializeField] private float ultimateClipFallbackLength = 10f / 3f;
    [SerializeField] private GameObject ultimateImpactVfxPrefab;
    [SerializeField] private float ultimateImpactVfxLifetime = 3f;
    // 임팩트 이펙트의 재생 속도. Hovl의 크리스탈은 쇼케이스 씬에 맞춰진 속도로
    // 떨어지는데, 이는 (이미 빨라진) 공격 애니메이션 옆에서 보면 느리게 늘어져 보인다.
    [SerializeField] private float ultimateImpactVfxSpeed = 2f;
    [SerializeField] private UltimateGaugeView ultimateGaugeView;

    [Header("Ultimate laser barrage (Boss)")]
    // Hovl "Laser AOE" - 위에 빔 기둥이 솟은 지면 테크서클 이펙트. null이면 예전 동작
    // (클립 끝에 무거운 타격 한 번)으로 대체된다.
    [SerializeField] private GameObject ultimateLaserPrefab;
    // 탄막이 발사를 계속하는 시간. Laser AOE 자체의 5초 방출 구간과 맞춰서, 유지되는
    // 애니메이션과 데미지 틱, 이펙트가 모두 함께 끝나도록 한다.
    [SerializeField] private float ultimateBarrageDuration = 5f;
    // 플레이어 뒤쪽을 중심으로 한 타격의 고리로, 순서대로 이동하며 발동시켜서 전부
    // 한꺼번에 적중하는 게 아니라 점점 조여드는 휩쓸기처럼 보이게 한다.
    [SerializeField] private int ultimateLaserCount = 8;
    [SerializeField] private float ultimateLaserRingRadius = 2.2f;
    [SerializeField] private float ultimateLaserBehindDistance = 2.6f;
    [SerializeField] private float ultimateLaserScale = 1f;
    // Laser AOE에서 가장 오래 사는 파티클의 수명(4초) - 스폰된 오브젝트는 자신의 방출이
    // 끝난 뒤에도 이만큼은 더 살아있어야 하며, 그렇지 않으면 꼬리 부분이 페이드 도중에
    // 잘려버린다.
    [SerializeField] private float ultimateLaserLingerSeconds = 4f;
    // ultimateLaserCount가 1이면 탄막은 플레이어 발밑 지면에 전체 지속시간 동안 유지되는
    // 단일 서클이 되며, 한 덩어리의 데미지를 고리 위치들에 나눠 주는 대신 고정된 주기로
    // (AttackPower에 비례하지 않는) 고정값 데미지를 틱마다 준다.
    [SerializeField] private float ultimateTickDamage = 5f;
    [SerializeField] private float ultimateTickInterval = 0.5f;

    [Header("Hit impact (on the player, every landed attack)")]
    // 이펙트를 보여주지 않아야 하는 프리팹에서는 null이다(기본값) - 스테이지 2 로스터만
    // "Stones hit"에 연결되어 있으므로, 스테이지 1 몬스터는 예전과 완전히 동일하게
    // 데미지를 준다.
    [SerializeField] private GameObject hitImpactVfxPrefab;
    [SerializeField] private float hitImpactVfxLifetime = 3f;
    // Hovl 피격 이펙트는 밝은 레이어(glow/flash)를 1초 사이클 중 대략 처음 0.1초에 걸쳐
    // 방출하는데, 원래 속도로는 타격이 적중했다고 인식되기도 전에 끝나버린다. 1보다
    // 작은 값으로 그 버스트를 늘려서 타격이 실제로 느껴지게 한다; 위의 수명 값은 이
    // 값으로 나눠서 실제 재생 시간을 그대로 추적하도록 한다.
    [SerializeField] private float hitImpactVfxSpeed = 0.4f;
    // 데모 씬 기본값보다 크게 키운 값이다. 기본값은 이 게임의 뒤로 빠진 전투 프레이밍이
    // 아니라 클로즈업 카메라에 맞춰 조정된 것이기 때문이다.
    [SerializeField] private float hitImpactVfxScale = 1.6f;

    private const string AttackUltimateParam = "AttackUltimate";
    // 탄막이 지속되는 동안 Attack03을 계속 열어둔다 - 이 상태를 벗어나는 건 클립 자체의
    // 0.333초 길이가 아니라 이 값에 의해 결정된다(컨트롤러의 Attack03 -> Idle 전환 참고).
    private const string UltimateActiveParam = "UltimateActive";
    private float ultimateChargeTimer;

    private bool animatorSearched;
    private Animator characterAnimator;
    // 모든 몬스터 모델이 리그/AnimatorController를 갖추고 있는 건 아니다(아직 순수
    // 정적 임포트 상태인 것도 있다) - 이게 없는 모델은 아래의 코드로 짠 돌진/축소
    // 연출로 대체된다.
    private Animator CharacterAnimator
    {
        get
        {
            if (!animatorSearched)
            {
                animatorSearched = true;
                characterAnimator = GetComponentInChildren<Animator>();
            }
            return characterAnimator;
        }
    }

    private PlayerCharacter playerCache;
    private PlayerCharacter Player => playerCache ??= moveTarget != null ? moveTarget.GetComponent<PlayerCharacter>() : null;

    private bool bossLoopStarted;
    private bool normalLoopStarted;
    private float attackInterval;

    // 코드로 짠 돌진 대체 연출의 속도 조절에 쓰이며, WeaponSwing도 없고 빌려올
    // WeaponSwing 보고 지연값도 없는 몬스터의 임팩트 지연 대체값으로도 쓰인다.
    [SerializeField] private float fallbackAttackImpactDelay = 0.3f;
    [SerializeField] private float deathAnimDuration = 1f;

    public bool IsDead => CurrentHp <= 0f;

    public void Initialize(MonsterType type, float hp, float attack, float attackInterval = 0.4f)
    {
        Type = type;
        MaxHp = hp;
        CurrentHp = hp;
        AttackPower = attack;
        this.attackInterval = attackInterval;

        HealthBar?.SetHealth(CurrentHp, MaxHp);
    }

    // 몬스터를 파괴하지 않고 공격 루프만 멈춘다 - 플레이어가 죽었을 때 쓰이며, 그래야
    // 전투 도중 사라지거나 이미 쓰러진 플레이어를 계속 공격하는 대신, 몬스터가 죽음
    // 애니메이션 동안 그냥 가만히 서 있게 된다.
    public void StopAttacking()
    {
        StopAllCoroutines();
    }

    public void PlayAttackAnimation()
    {
        CharacterAnimator?.SetTrigger(AnimParams.Attack);
        WeaponSwing?.PlaySwing();

        if (CharacterAnimator == null && WeaponSwing == null)
        {
            StartCoroutine(LungeAttack());
        }
    }

public void SetMovement(Transform target, float speed, float stopDistance)
    {
        moveTarget = target;
        moveSpeed = speed;
        // 더 큰 몬스터는 더 멀리서 멈춰야 한다, 그렇지 않으면 캡슐이 실제로 닿기도
        // 전에 커진 몸이 시각적으로 플레이어와 겹치거나 파묻어버린다.
        stoppingDistance = stopDistance * transform.localScale.x;
        HasArrived = false;
        CharacterAnimator?.SetBool(AnimParams.IsMoving, true);
    }

    private void Update()
    {
        if (HasArrived || moveTarget == null) return;

        Vector3 toTarget = moveTarget.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= stoppingDistance)
        {
            HasArrived = true;
            CharacterAnimator?.SetBool(AnimParams.IsMoving, false);

            // 엘리트와 보스는 둘 다 패링 결투로 싸운다: 예고 동작을 보인 뒤 타격한다.
            // 일반 몬스터는 그냥 고정된 타이머로 서로 타격을 주고받는다.
            if (!bossLoopStarted && (Type == MonsterType.Boss || Type == MonsterType.Elite))
            {
                bossLoopStarted = true;
                StartCoroutine(TelegraphAttackLoop());

                // 보스 전용: 엘리트는 위의 텔레그래프 결투는 공유하지만, 게이지로
                // 충전되는 강력한 타격은 공유하지 않는다 - 그건 보스 전투만의
                // 고유한 상승 요소로 남는다.
                if (Type == MonsterType.Boss) StartCoroutine(UltimateChargeLoop());
            }
            else if (!normalLoopStarted && Type == MonsterType.Normal)
            {
                normalLoopStarted = true;
                StartCoroutine(NormalAttackLoop());
            }

            return;
        }

        transform.position += toTarget.normalized * moveSpeed * Time.deltaTime;
        transform.forward = toTarget.normalized;
    }

    // 리그/Animator도, 휘두를 WeaponSwing 검도 없는 모델을 위한 대체 공격 연출 -
    // 플레이어를 향해 앞으로 갔다가 다시 뒤로 돌아오는 짧은 돌진.
    private IEnumerator LungeAttack()
    {
        Vector3 restPosition = transform.position;
        Vector3 lungeTarget = restPosition + transform.forward * 0.3f;
        float half = fallbackAttackImpactDelay * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(restPosition, lungeTarget, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(lungeTarget, restPosition, t / half);
            yield return null;
        }

        transform.position = restPosition;
    }

    // Animator/Die 스테이트가 없는 모델을 위한 대체 죽음 연출: 그냥 뿅 하고 사라지는
    // 대신 점점 줄어들며 사라진다.
    private IEnumerator ShrinkAndDestroy()
    {
        Vector3 startScale = transform.localScale;
        float duration = 0.4f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t / duration);
            yield return null;
        }

        Destroy(gameObject);
    }

    // 엘리트/보스: 무작위 길이만큼 충전하며, 충전 클립 자체의 착지 비트(뛰어올랐다가
    // 쿵 하고 내려찍는 순간)가 곧 타격이다 - 별도의 타격 클립은 없다. 그 전 어느
    // 시점에든 패링이 성공하면 무효화된다.
    private IEnumerator TelegraphAttackLoop()
    {
        CharacterAnimator?.SetBool(UseTelegraphParam, true);

        float windUpLength = ResolveClipLength(windUpClipName, windUpClipFallbackLength);
        float attack2Length = ResolveClipLength(attack2ClipName, attack2ClipFallbackLength);

        while (!IsDead)
        {
            // 사이클 중간이 아니라 매 사이클의 맨 처음에 확인하므로, 이 검사는 아직
            // 시작하지 않은 스윙만 대체할 수 있을 뿐 - 이미 진행 중인 타격을 끊는
            // 일은 절대 없다.
            if (ultimateChargeTimer >= ultimateChargeDuration)
            {
                yield return PlayUltimateAttack();
                ultimateChargeTimer = 0f;
                // 아래의 다른 두 패턴이 받는 것과 같은 간격이다 - 이 분기는 예전에
                // 이 부분을 건너뛰고 바로 `continue`했었는데, 그러면 궁극기가 아무런
                // 틈도 없이 곧바로 다음 충전으로 이어졌다.
                if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
                continue;
            }

            Animator animator = CharacterAnimator;

            // 두 번째 패턴을 섞어 쓰는 건 보스뿐이다 - 엘리트는 항상 같은 방식으로만
            // 예고한다. 매 스윙마다 독립적으로 뽑으므로, 보스는 같은 패턴을 연달아
            // 낼 수도 있다.
            bool usePattern2 = Type == MonsterType.Boss && Random.value < 0.5f;
            animator?.SetBool(AttackPattern2Param, usePattern2);

            string activeState;
            float activeLength;
            float impactTime;

            if (usePattern2)
            {
                // 충전도, 무작위 지연도 없다 - 그저 손대지 않은 클립을 원래 속도로
                // 재생할 뿐이며, 타격은 창이 실제로 닿는 순간에 적중한다.
                activeState = attack2StateName;
                activeLength = attack2Length;
                impactTime = attack2ImpactTime;
                animator?.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();
            }
            else
            {
                // 별도의 타격 클립은 없다 - 윈드업 클립 자체의 착지 비트(windUpImpactFraction)가
                // 정확히 timeToImpact 시점에 오도록 늘려지며, 두 번째 애니메이션으로
                // 넘기는 대신 그 지점에서 타격이 발동한다.
                activeState = windUpStateName;
                activeLength = windUpLength;
                impactTime = windUpLength * windUpImpactFraction;

                float timeToImpact = Random.Range(minTimeToImpact, maxTimeToImpact);
                // 실제로 재생되는 건 진입 오프셋부터 임팩트 비트까지의 구간뿐이므로,
                // 충전은 그 구간을 기준으로 늘려진다 - 대신 전체 클립 길이를 기준으로
                // 스케일링하면 건너뛴 부분만큼 타격이 일찍 적중해버릴 것이다.
                float playedFraction = Mathf.Max(0.05f, windUpImpactFraction - windUpEntryOffset);
                float chargeDuration = Mathf.Max(0.05f, timeToImpact / playedFraction);

                animator?.SetFloat(WindUpSpeedParam, windUpLength / chargeDuration);
                animator?.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();
            }

            if (animator != null)
            {
                // 스톱워치 대신 애니메이터를 따라간다. 트리거는 머신이 다시 Idle로
                // 돌아갈 때까지 소비되지 않고, 스테이트 블렌딩도 자체적인 오차를
                // 더하므로, 미리 계산해둔 스케줄로는 창이 시각적으로 도착하기 전에
                // 데미지가 먼저 들어가 버린다.
                yield return WaitForStrikeImpact(animator, activeState, activeLength, impactTime);
            }
            else
            {
                yield return new WaitForSeconds(impactTime);
            }

            if (IsDead) yield break;

            bool parried = ParryManager.Instance != null && ParryManager.Instance.TryConsumeParry();

            if (!parried && Player != null)
            {
                SpawnHitImpactVfx();
                Player.TakeDamage(AttackPower);
            }

            if (animator != null)
            {
                yield return WaitUntilIdle(animator);
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(0f, activeLength - impactTime));
            }

            // 한 공격의 착지 스윙과 다음 공격의 진입 동작을 분리해줘서, 연달아 두 번
            // 충전하는 게 하나의 이중 스윙처럼 뭉개져 보이지 않게 한다.
            if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
        }
    }

    // 창이 플레이어에게 닿을 만큼 타격 스테이트가 충분히 재생될 때까지 실행된다.
    private IEnumerator WaitForStrikeImpact(Animator animator, string strikeState, float strikeLength, float impactTime)
    {
        const float SafetyTimeout = 8f;
        float elapsed = 0f;

        while (!IsDead && elapsed < SafetyTimeout)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(strikeState) && state.normalizedTime * strikeLength >= impactTime) yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // 머신이 진짜로 다시 idle 상태가 될 때까지 대기해서, 다음 Attack 트리거가 타격
    // 도중에 큐잉되지 않고 즉시 반영되도록 한다.
    private IEnumerator WaitUntilIdle(Animator animator)
    {
        const float SafetyTimeout = 5f;
        float elapsed = 0f;

        while (!IsDead && elapsed < SafetyTimeout)
        {
            if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName(idleStateName)) yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // 공격 루프 자체의 진행 속도와는 독립적으로 실행되어, 긴 스윙(또는 그 충전)이
    // 게이지를 절대 지연시키지 않는다 - 이것이 결정하는 건 오직 다음 루프 반복에서
    // *언제* 일반 스윙 대신 궁극기를 발동할 수 있는지뿐이다.
    private IEnumerator UltimateChargeLoop()
    {
        while (!IsDead)
        {
            ultimateChargeTimer = Mathf.Min(ultimateChargeTimer + Time.deltaTime, ultimateChargeDuration);
            ultimateGaugeView?.SetFraction(ultimateChargeTimer / ultimateChargeDuration);
            yield return null;
        }
    }

    // 무겁고 패링 불가능한 공격. 레이저 프리팹이 연결되어 있으면 지속적인 탄막으로
    // 바뀐다: Attack03(반복 자동 발사)이 ultimateBarrageDuration 동안 유지되는 동안,
    // 플레이어 뒤쪽 지면에서 레이저 타격의 고리가 순서대로 이동하며 각각 자기 몫의
    // 데미지를 준다 - 플레이어는 한 번의 타격이 아니라 여러 번 관통당하는 셈이다.
    // 프리팹이 없으면 원래의 끝에 한 방 타격하는 동작으로 대체된다.
    private IEnumerator PlayUltimateAttack()
    {
        Animator animator = CharacterAnimator;
        bool barrage = ultimateLaserPrefab != null;

        if (animator != null)
        {
            if (barrage) animator.SetBool(UltimateActiveParam, true);
            // 여기서 일반 스윙의 트리거가 아직 소비되지 않은 채 남아있을 수 있다;
            // 그대로 세팅된 채 두면 탄막이 머신을 놓아주는 순간 엉뚱한 공격이 발동한다.
            animator.ResetTrigger(AnimParams.Attack);
            animator.SetTrigger(AttackUltimateParam);

            // 트리거는 애니메이터 자체의 업데이트 패스가 돌기 전까지 소비되지 않으므로,
            // 현재 상태는 한 프레임 동안 여전히 Idle/Run으로 읽힐 수 있다 - 머신이
            // Attack03을 벗어나기를 기다리기 전에, 먼저 실제로 진입했는지부터
            // 기다려야 한다. 그렇지 않으면 공격이 재생되지도 않은 채로 WaitUntilIdle을
            // 바로 통과해버릴 것이다.
            yield return WaitForStateToStart(animator, ultimateStateName);
        }
        else if (!barrage)
        {
            yield return new WaitForSeconds(ultimateClipFallbackLength);
        }

        if (barrage)
        {
            yield return FireUltimateLaserRing();

            // idle 대기 전에 해제한다. Attack03을 벗어나는 전환을 결정하는 게 바로 이
            // bool이기 때문이다 - 세팅된 채로 두면 보스가 발사 포즈에 갇혀버린다.
            if (animator != null)
            {
                animator.SetBool(UltimateActiveParam, false);
                yield return WaitUntilIdle(animator);
            }
            yield break;
        }

        if (animator != null) yield return WaitUntilIdle(animator);

        if (IsDead) yield break;

        if (Player != null)
        {
            SpawnUltimateImpactVfx();
            SpawnHitImpactVfx();
            Player.TakeDamage(AttackPower * ultimateDamageMultiplier);
        }
    }

    // 고리를 한 번에 하나씩 순서대로 발동시켜서 전부 한꺼번에 적중하는 대신 플레이어
    // 주위를 휩쓸도록 한다. 궁극기의 총 데미지는 변하지 않는다 - 그저 타격들에 나눠질
    // 뿐이며, 각각이 하나의 탄환이 관통하는 것처럼 느껴진다.
    private IEnumerator FireUltimateLaserRing()
    {
        int count = Mathf.Max(1, ultimateLaserCount);

        if (count == 1)
        {
            yield return FireUltimateLaserSingle();
            yield break;
        }

        float interval = Mathf.Max(0f, ultimateBarrageDuration) / count;
        float damagePerHit = AttackPower * ultimateDamageMultiplier / count;

        for (int i = 0; i < count; i++)
        {
            if (IsDead) yield break;
            if (Player == null) yield break;

            SpawnUltimateLaser(i, count);
            // 다른 모든 적중한 타격이 쓰는 것과 같은 플레이어 위 버스트 이펙트를
            // 재사용해서, 각 관통이 지면의 서클로만 보이지 않고 플레이어 자신에게도
            // 나타나게 한다.
            SpawnHitImpactVfx();
            // 의도적으로 ParryManager를 거치지 않는다 - 궁극기는 원래부터 패링 불가능한
            // 공격이었으며, 패링 한 번으로 취소할 수 있는 탄막이라면 그것이 대체한
            // 단일 타격보다도 약해질 것이다.
            Player.TakeDamage(damagePerHit);

            yield return new WaitForSeconds(interval);
        }
    }

    // 탄막 전체 동안 플레이어 발밑에 유지되는 단일 지면 서클로, 원샷 타격의 고리를
    // 순회하는 대신 고정된 주기로 고정값 데미지를 틱으로 준다.
    private IEnumerator FireUltimateLaserSingle()
    {
        if (Player == null) yield break;

        SpawnUltimateLaserUnderPlayer();

        float interval = Mathf.Max(0.05f, ultimateTickInterval);
        float elapsed = 0f;

        while (elapsed < ultimateBarrageDuration)
        {
            yield return new WaitForSeconds(interval);
            elapsed += interval;

            if (IsDead) yield break;
            if (Player == null) yield break;

            SpawnHitImpactVfx();
            // 의도적으로 ParryManager를 거치지 않는다 - FireUltimateLaserRing 참고.
            Player.TakeDamage(ultimateTickDamage);
        }
    }

    private void SpawnUltimateLaserUnderPlayer()
    {
        if (Player == null) return;

        Vector3 spawn = Player.transform.position;
        Collider playerCollider = Player.GetComponent<Collider>();
        spawn.y = playerCollider != null ? playerCollider.bounds.min.y : spawn.y;

        SpawnUltimateLaserAt(spawn);
    }

    private void SpawnUltimateLaser(int index, int count)
    {
        if (ultimateLaserPrefab == null || Player == null) return;

        Transform target = Player.transform;

        // "뒤쪽"은 이 몬스터가 공격해 들어가는 선을 기준으로 측정되므로, 고리는 둘
        // 사이가 아니라 플레이어의 등 쪽에서 감싸는 형태가 된다.
        Vector3 away = target.position - transform.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

        Vector3 center = target.position + away * ultimateLaserBehindDistance;

        float angle = index / (float)count * Mathf.PI * 2f;
        Vector3 spawn = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ultimateLaserRingRadius;

        // 이펙트의 테크서클은 자신의 원점에 위치하므로, 그 원점이 지면에 닿아야 한다 -
        // 플레이어의 transform은 발이 아니라 캡슐의 중심이다.
        Collider playerCollider = Player.GetComponent<Collider>();
        spawn.y = playerCollider != null ? playerCollider.bounds.min.y : target.position.y;

        SpawnUltimateLaserAt(spawn);
    }

    private void SpawnUltimateLaserAt(Vector3 spawn)
    {
        if (ultimateLaserPrefab == null) return;

        GameObject vfx = Instantiate(ultimateLaserPrefab, spawn, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * ultimateLaserScale;

        // Laser AOE의 11개 시스템은 전부 반복 재생으로 되어 있다(데모 씬에서 계속
        // 실행되도록 만들어졌기 때문) - 그대로 두면 스폰된 각 고리가 영원히 발사될
        // 것이다.
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
        }

        Destroy(vfx, ultimateBarrageDuration + ultimateLaserLingerSeconds);
    }

    private IEnumerator WaitForStateToStart(Animator animator, string stateName)
    {
        const float SafetyTimeout = 2f;
        float elapsed = 0f;

        while (!IsDead && elapsed < SafetyTimeout)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)) yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // 지속되는 자식 오브젝트로 유지하는 대신 매번 새로 스폰한다 - 플레이어 자신의
    // 피격 VFX와 달리, 여러 몬스터가 동시에 같은 플레이어에게 이걸 적중시킬 수 있고,
    // 공유된 자식 오브젝트라면 깔끔하게 겹쳐 쌓이는 대신 하나의 Play()/Stop()을
    // 두고 서로 다투게 될 것이다.
    private void SpawnUltimateImpactVfx()
    {
        if (ultimateImpactVfxPrefab == null || Player == null) return;

        // 이펙트의 크레이터/버스트는 자신의 원점에 위치하므로, 그 원점이 지면에
        // 닿아야 한다. 플레이어의 transform은 발이 아니라 캡슐의 중심이므로, 거기에
        // 바로 스폰하면 크리스탈이 허리 높이에서 터져버려 - 다 떨어지기도 전에
        // 터지는 것처럼 보였다.
        Vector3 impactPoint = Player.transform.position;
        Collider playerCollider = Player.GetComponent<Collider>();
        if (playerCollider != null) impactPoint.y = playerCollider.bounds.min.y;

        GameObject vfx = Instantiate(ultimateImpactVfxPrefab, impactPoint, Quaternion.identity);

        // Hovl 이펙트들은 반복 재생 시스템으로 되어 있어서(데모 씬에서 계속 실행되도록
        // 만들어졌기 때문), 한 번만 사용해도 자신의 파괴 타이머 도중에 낙하 연출
        // 전체가 재시작되어 이펙트가 두 번 발동한 것처럼 보일 것이다. 한 번의 사용 =
        // 한 사이클.
        float speed = Mathf.Max(0.01f, ultimateImpactVfxSpeed);
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            // 시스템별로 설정해야 하므로, 모든 서브 이미터(크리스탈, 플래시, 스파크,
            // 연기)에 다 세팅해야 한다 - 하나라도 1배속으로 남으면 나머지 이펙트보다
            // 뒤처지게 된다.
            main.simulationSpeed = speed;
        }

        // 이펙트가 이제 원래 재생 시간의 일부 만에 끝나버리므로, 정리 타이머도 함께
        // 줄여야 한다. 그렇지 않으면 텅 빈 오브젝트가 그대로 남아있게 된다.
        Destroy(vfx, ultimateImpactVfxLifetime / speed);
    }

    // 적중한 모든 공격(일반 스윙, 텔레그래프 타격, 궁극기 전부)과 함께 발동한다 -
    // SpawnUltimateImpactVfx와 달리 이건 발밑의 크레이터가 아니라 타격이 맞닿는
    // 느낌을 줘야 하므로, 플레이어의 발이 아니라 중심에서 나타난다.
    private void SpawnHitImpactVfx()
    {
        if (hitImpactVfxPrefab == null || Player == null) return;

        GameObject vfx = Instantiate(hitImpactVfxPrefab, Player.transform.position, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * hitImpactVfxScale;

        float speed = Mathf.Max(0.01f, hitImpactVfxSpeed);

        // 궁극기 자체의 임팩트 프리팹처럼 반복되는 데모 씬 시스템으로 되어 있다 -
        // 한 번의 타격은 뒤에 계속 실행되는 시스템이 아니라 한 번의 버스트로 느껴져야
        // 한다.
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            // 궁극기 임팩트와 마찬가지로 시스템별로 설정한다: 서브 이미터 하나라도
            // 1배속으로 남으면 나머지보다 먼저 끝나버려서 한 번의 버스트처럼 보이는
            // 느낌이 깨진다.
            main.simulationSpeed = speed;
        }

        // 이펙트를 느리게 하면 실제 재생 시간이 늘어나므로, 정리 타이머도 함께 늘려야
        // 한다. 그렇지 않으면 버스트 도중에 오브젝트가 파괴돼버린다.
        Destroy(vfx, hitImpactVfxLifetime / speed);
    }

    private float ResolveClipLength(string clipName, float fallback)
    {
        Animator animator = CharacterAnimator;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == clipName && clip.length > 0f) return clip.length;
            }
        }

        return fallback;
    }

    // 일반 몬스터 전용: 플레이어의 공격 틱과 무관하게 자기 자신의 고정 타이머로
    // 공격하므로, 플레이어의 치명타가 이 몬스터 자신의 스윙을 선점하는 일은 절대
    // 없다.
    private IEnumerator NormalAttackLoop()
    {
        // 일반 몬스터는 손대지 않은 원본 클립을 그대로 재생한다 - 충전도, 분리도 없다.
        CharacterAnimator?.SetBool(UseTelegraphParam, false);

        // 한 번의 공격 간격 안에 눌러 담는다, 그렇지 않으면 다음 공격이 클립이 끝나기도
        // 전에 재트리거해버려서 클립이 절대 끝까지 재생되지 못하고 몬스터가 그냥
        // 씰룩거리기만 하게 된다.
        float clipLength = ResolveClipLength(plainAttackClipName, plainAttackClipFallbackLength);
        if (attackInterval > 0.01f)
        {
            CharacterAnimator?.SetFloat(AttackSpeedParam, clipLength / attackInterval);
        }

        while (!IsDead)
        {
            PlayAttackAnimation();

            float impactDelay = WeaponSwing != null ? WeaponSwing.AttackImpactDelay : fallbackAttackImpactDelay;
            yield return new WaitForSeconds(impactDelay);

            if (IsDead) yield break;

            if (Player != null)
            {
                SpawnHitImpactVfx();
                Player.TakeDamage(AttackPower);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, attackInterval - impactDelay));
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        HealthBar?.SetHealth(CurrentHp, MaxHp);
        GameEvents.RaiseMonsterDamaged(this, amount);

        if (IsDead)
        {
            GameEvents.RaiseMonsterDied(this);

            if (CharacterAnimator != null)
            {
                CharacterAnimator.SetTrigger(AnimParams.Die);
                Destroy(gameObject, deathAnimDuration);
            }
            else
            {
                StartCoroutine(ShrinkAndDestroy());
            }
        }
    }
}
