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
    // 차징(대기) 구간 자체의 길이로, 매 스윙마다 새로 뽑는다 - 플레이어가 실제로 반응해야
    // 하는 숫자다. 그 뒤 실제 스윙(내려찍기) 구간은 이 값과 무관하게 항상 원래 속도/길이
    // 그대로 이어지므로, 공격 시작부터 타격까지의 총 시간은 "이 값 + 스윙 고정 시간"이다.
    [SerializeField] private float minTimeToImpact = 0.5f;
    [SerializeField] private float maxTimeToImpact = 2f;
    [SerializeField] private string windUpStateName = "AttackWindUp";
    [SerializeField] private string idleStateName = "Idle";

    [SerializeField] private string windUpClipName = "Footman_Attack01_WindUp";
    [SerializeField] private string plainAttackClipName = "Attack01";
    [SerializeField] private float windUpClipFallbackLength = 0.3f;
    [SerializeField] private float plainAttackClipFallbackLength = 1.333f;
    // 별도 타격 클립 없이 윈드업 클립의 착지 비트(뛰어올랐다 쿵 내려찍는 순간)가 곧
    // 타격이다 - 그 지점을 늘리기 전 원래 클립 길이에 대한 비율로 표기. PowerUpNoWeapon은
    // 2.933초 길이 중 t=2.7초 부근의 강한 웅크림 동작.
    [SerializeField] private float windUpImpactFraction = 0.92f;
    // PowerUpNoWeapon 시작부의 강한 팔 휘두름(몇 프레임 만에 약 74도)만으로도 방망이를
    // 휘두르는 것처럼 보여서, Idle/Run -> AttackWindUp 전환은 이만큼 건너뛰고 진입한다.
    // 그 전환들의 오프셋과 반드시 일치해야 한다: 실제 재생 구간은 (임팩트 - 진입)뿐이라
    // 아래의 충전 속도도 전체 클립이 아닌 이 구간 기준으로 계산된다.
    [SerializeField] private float windUpEntryOffset = 0.15f;
    // 차징(대기) 구간이 끝나고 실제로 휘두르기 시작하는 지점 - 클립 길이에 대한 비율. 이 지점
    // 전까지만 timeToImpact에 맞춰 재생 속도가 늘어나거나 줄고, 이 지점부터 임팩트까지는 항상
    // 원래 속도(1배)로 재생된다 - 그래야 차징이 얼마나 길든 짧든 실제로 내려찍는 스윙 자체의
    // 체감은 항상 똑같다. PowerUpNoWeapon 커브를 직접 뽑아본 값(약 64%, t=1.87초까지는 거의
    // 안 움직이는 대기 동작, 그 뒤로 빠르게 꺾이는 실제 스윙).
    [SerializeField] private float windUpChargeEndFraction = 0.64f;
    // 타격 후 다음 충전까지의 여유. 없으면 한 공격의 착지 스윙이 다음 공격의 진입 동작으로
    // 바로 이어져서, 방망이를 연달아 두 번 휘두르는 것처럼 보였다.
    [SerializeField] private float postAttackPause = 0.45f;

    // 두 번째 텔레그래프 패턴(보스 전용) - 예고가 늘 똑같지 않도록 매 스윙 무작위로 고른다.
    // 첫 패턴과 달리 충전도 무작위 지연도 없이 손대지 않은 클립을 원래 속도로 재생하고,
    // 타격은 그 고정된 타임라인 안에서 창이 실제로 닿는 순간에 적중한다.
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
    // Footman_Attack03의 내부 테이크 이름이 (헷갈리게도) "Victory"라 무관한 Victory
    // 스테이트의 동명 클립과 충돌한다 - 애니메이터 클립 목록의 이름 조회가 모호해지므로
    // ResolveClipLength를 거치지 않고 클립의 실제 길이를 직접 적었다.
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
    // VFX는 플레이어 발밑에 딱 한 번만 스폰한다 - 그 안에서 빛줄기가 여러 번 떨어지는 건
    // 이펙트 자체의 연출이고, 코드는 그 위에 정확히 이 횟수만큼만 데미지 틱을 맞춰 넣는다.
    [SerializeField] private int ultimateLaserCount = 8;
    [SerializeField] private float ultimateLaserScale = 1f;
    // Laser AOE에서 가장 오래 사는 파티클의 수명(4초) - 방출이 끝난 뒤에도 이만큼은 더
    // 살려둬야 꼬리 부분이 페이드 도중에 잘리지 않는다.
    [SerializeField] private float ultimateLaserLingerSeconds = 4f;

    [Header("Hit impact (on the player, every landed attack)")]
    // 이펙트를 보여주지 않을 프리팹에서는 null(기본값) - 스테이지 2 로스터만 "Stones hit"에
    // 연결되어 있어서 스테이지 1 몬스터는 예전과 완전히 동일하게 데미지를 준다.
    [SerializeField] private GameObject hitImpactVfxPrefab;
    [SerializeField] private float hitImpactVfxLifetime = 3f;
    // Hovl 피격 이펙트는 밝은 레이어(glow/flash)를 1초 사이클 중 처음 0.1초쯤에 방출해서,
    // 원래 속도로는 타격이 적중했다고 인식되기도 전에 끝난다. 1보다 작은 값으로 그 버스트를
    // 늘리고, 위의 수명은 이 값으로 나눠서 실제 재생 시간을 그대로 추적하게 한다.
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
    // 모든 몬스터 모델이 리그/AnimatorController를 갖춘 건 아니다(아직 정적 임포트인 것도
    // 있다) - 없는 모델은 아래의 코드로 짠 돌진/축소 연출로 대체된다.
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

    private WeaponSwing playerWeaponSwingCache;
    private bool playerWeaponSwingSearched;
    private WeaponSwing PlayerWeaponSwing
    {
        get
        {
            if (!playerWeaponSwingSearched)
            {
                playerWeaponSwingSearched = true;
                playerWeaponSwingCache = Player != null ? Player.GetComponentInChildren<WeaponSwing>() : null;
            }
            return playerWeaponSwingCache;
        }
    }

    private bool bossLoopStarted;
    private bool normalLoopStarted;
    private float attackInterval;

    // 코드로 짠 돌진 대체 연출의 속도 조절에 쓰이며, WeaponSwing도 없고 빌려올
    // WeaponSwing 보고 지연값도 없는 몬스터의 임팩트 지연 대체값으로도 쓰인다.
    [SerializeField] private float fallbackAttackImpactDelay = 0.3f;
    [SerializeField] private float deathAnimDuration = 1f;
    private const float ShrinkDestroyDuration = 0.4f;

    // 이 몬스터의 사체가 실제로 화면에서 사라지기까지 걸리는 시간 - Animator가 있으면
    // Die 클립 길이(deathAnimDuration), 없으면 ShrinkAndDestroy의 축소 연출 길이를 그대로
    // 반영한다. StageManager가 다음 스폰 딜레이를 여기에 맞춰서, 사체가 사라지는 순간과
    // 다음 몬스터 등장이 겹치지 않는다.
    public float DeathVisualDuration => CharacterAnimator != null ? deathAnimDuration : ShrinkDestroyDuration;

    // 공격력이 아무리 높아도(즉사여도) 플레이어가 최소 한 대는 맞아야 한다는 디자인 규칙 -
    // 그래서 일반 몬스터의 타격 판정은 플레이어의 타격 판정(AttackImpactDelay)보다 이 여유만큼
    // 항상 앞서도록 강제한다. 값 자체가 하드코딩된 두 번째 상수라 서로 어긋날 위험이 있으니,
    // 매 판정마다 플레이어의 실제 값을 직접 읽어와서 거기서 빼는 식으로 계산한다 - 그래야 나중에
    // 플레이어 쪽 타이밍이 바뀌어도 "아주 살짝 더 빠름"이 자동으로 유지된다. 겉보기엔 거의 동시에
    // 슬래시가 뜨지만(margin이 작아서 눈에 안 띔), 데미지 적용 순서는 항상 몬스터가 먼저다.
    private const float GuaranteedFirstStrikeMargin = 0.02f;

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

    // 플레이어가 죽었을 때, 몬스터를 파괴하지 않고 공격 루프만 멈춘다 - 전투 도중 사라지거나
    // 쓰러진 플레이어를 계속 때리는 대신 죽음 애니메이션 동안 가만히 서 있게 된다.
    //
    // bossLoopStarted/normalLoopStarted도 함께 꺼야 한다 - 안 그러면 이 몬스터가 죽음 시퀀스
    // 이후 파괴되지 않고 살아남는 경우(재시작 코루틴이 겹친 죽음으로 끊기는 등) Update()가
    // HasArrived 진입 순간에만 루프를 시작하는 구조라 영원히 다시 공격하지 못하게 된다.
    public void StopAttacking()
    {
        StopAllCoroutines();
        bossLoopStarted = false;
        normalLoopStarted = false;
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
        if (moveTarget == null) return;

        if (!HasArrived)
        {
            Vector3 toTarget = moveTarget.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude > stoppingDistance)
            {
                transform.position += toTarget.normalized * moveSpeed * Time.deltaTime;
                transform.forward = toTarget.normalized;
                return;
            }

            HasArrived = true;
            CharacterAnimator?.SetBool(AnimParams.IsMoving, false);
        }

        // "방금 도착한 프레임"만이 아니라 도착한 뒤 매 프레임 재확인한다 - StopAttacking()
        // 이후에도 파괴되지 않고 살아남는 몬스터가(죽음 시퀀스 코루틴이 겹쳐 끊기는 등) 다시는
        // 공격을 시작 못 하는 일이 없도록, 플레이어가 무적(죽음 시퀀스 중)이 아닌 한 멈춰있던
        // 루프를 스스로 재시작하게 한다.
        if (Player != null && Player.IsInvulnerable) return;

        // 엘리트와 보스는 둘 다 패링 결투로 싸운다: 예고 동작을 보인 뒤 타격한다.
        // 일반 몬스터는 그냥 고정된 타이머로 서로 타격을 주고받는다.
        if (!bossLoopStarted && (Type == MonsterType.Boss || Type == MonsterType.Elite))
        {
            bossLoopStarted = true;
            StartCoroutine(TelegraphAttackLoop());

            // 보스 전용: 엘리트도 위 텔레그래프 결투는 공유하지만 게이지로 충전되는
            // 강타는 아니다 - 그건 보스 전투만의 고유한 상승 요소로 남는다.
            if (Type == MonsterType.Boss) StartCoroutine(UltimateChargeLoop());
        }
        else if (!normalLoopStarted && Type == MonsterType.Normal)
        {
            normalLoopStarted = true;
            StartCoroutine(NormalAttackLoop());
        }
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
        float duration = ShrinkDestroyDuration;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t / duration);
            yield return null;
        }

        Destroy(gameObject);
    }

    // 엘리트/보스: 무작위 길이만큼 충전하고, 별도의 타격 클립 없이 충전 클립 자체의 착지
    // 비트(뛰어올랐다 쿵 내려찍는 순간)가 곧 타격이다. 그 전 어느 시점의 패링으로든 무효화.
    private IEnumerator TelegraphAttackLoop()
    {
        CharacterAnimator?.SetBool(UseTelegraphParam, true);

        float windUpLength = ResolveClipLength(windUpClipName, windUpClipFallbackLength);
        float attack2Length = ResolveClipLength(attack2ClipName, attack2ClipFallbackLength);

        while (!IsDead)
        {
            // 사이클 중간이 아니라 맨 처음에만 확인하므로, 아직 시작하지 않은 스윙만
            // 대체할 수 있고 이미 진행 중인 타격을 끊는 일은 절대 없다.
            if (ultimateChargeTimer >= ultimateChargeDuration)
            {
                yield return PlayUltimateAttack();
                ultimateChargeTimer = 0f;
                // 아래 다른 두 패턴과 같은 간격 - 예전엔 이걸 건너뛰고 바로 `continue`해서
                // 궁극기가 아무런 틈도 없이 곧바로 다음 충전으로 이어졌다.
                if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
                continue;
            }

            Animator animator = CharacterAnimator;

            // 두 번째 패턴을 섞어 쓰는 건 보스뿐(엘리트는 항상 같은 방식으로 예고). 매 스윙
            // 독립적으로 뽑으므로 보스는 같은 패턴을 연달아 낼 수도 있다.
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
                // 차징 구간(진입~windUpChargeEndFraction)만 minTimeToImpact~maxTimeToImpact
                // 사이 무작위 길이로 늘어나거나 줄어든다. 그 뒤 실제 스윙 구간(~임팩트)은 이
                // 차징 시간과 무관하게 항상 원래 속도로, 원래 길이 그대로 재생된다 - 아래
                // RestoreWindUpSpeedAfterCharge가 그 지점에서 속도를 다시 1배로 되돌려준다.
                // 그래서 공격이 시작되어 타격이 적중하기까지의 총 시간은 "차징(가변) + 스윙(고정)"이다.
                activeState = windUpStateName;
                activeLength = windUpLength;
                impactTime = windUpLength * windUpImpactFraction;

                float chargeDuration = Mathf.Max(0.05f, Random.Range(minTimeToImpact, maxTimeToImpact));
                // 차징 구간(진입 오프셋~chargeEndFraction)의 실제 재생 길이 - 이 구간만 충전
                // 속도의 대상이다.
                float chargeSpanFraction = Mathf.Max(0.05f, windUpChargeEndFraction - windUpEntryOffset);

                animator?.SetFloat(WindUpSpeedParam, chargeSpanFraction * windUpLength / chargeDuration);
                animator?.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();

                if (animator != null) StartCoroutine(RestoreWindUpSpeedAfterCharge(animator));
            }

            if (animator != null)
            {
                // 스톱워치 대신 애니메이터를 따라간다. 트리거는 머신이 다시 Idle로 돌아갈
                // 때까지 소비되지 않고 스테이트 블렌딩도 오차를 더해서, 미리 계산해둔
                // 스케줄로는 창이 시각적으로 도착하기 전에 데미지가 먼저 들어간다.
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

    // 차징 구간이 끝나는 지점(windUpChargeEndFraction)까지 기다렸다가 윈드업 재생 속도를 원래
    // 속도(1배)로 되돌린다 - 그 뒤에 이어지는 실제 스윙 동작은 차징 시간과 무관하게 항상
    // 자연스러운 속도로 보이게 하기 위함.
    private IEnumerator RestoreWindUpSpeedAfterCharge(Animator animator)
    {
        const float SafetyTimeout = 5f;
        float elapsed = 0f;

        // SetTrigger 직후엔 애니메이터가 아직 실제로 AttackWindUp으로 전환되기 전이다(최소 한
        // 프레임 필요) - 그 전에 아래 루프를 바로 돌리면 "아직 Idle이니 이미 벗어났다"고
        // 착각해서 차징 속도를 세팅하자마자 곧바로 1배로 되돌려버린다. 실제로 그 상태에
        // 진입할 때까지 먼저 기다린다.
        while (elapsed < SafetyTimeout && !animator.GetCurrentAnimatorStateInfo(0).IsName(windUpStateName))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        while (elapsed < SafetyTimeout)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(windUpStateName) || state.normalizedTime >= windUpChargeEndFraction) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        animator.SetFloat(WindUpSpeedParam, 1f);
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

    // 공격 루프의 진행 속도와 독립적으로 돌아서 긴 스윙(또는 그 충전)이 게이지를 지연시키지
    // 않는다 - 이게 정하는 건 다음 루프 반복에서 궁극기가 일반 스윙을 대체할 시점뿐이다.
    private IEnumerator UltimateChargeLoop()
    {
        while (!IsDead)
        {
            ultimateChargeTimer = Mathf.Min(ultimateChargeTimer + Time.deltaTime, ultimateChargeDuration);
            ultimateGaugeView?.SetFraction(ultimateChargeTimer / ultimateChargeDuration);
            yield return null;
        }
    }

    // 무겁고 패링 불가능한 공격. 레이저 프리팹이 연결되어 있으면 지속 탄막이 된다:
    // Attack03(반복 자동 발사)이 ultimateBarrageDuration 동안 유지되는 사이, 플레이어 뒤쪽
    // 지면의 레이저 고리가 순서대로 이동하며 각각 자기 몫의 데미지를 준다 - 한 번의 타격이
    // 아니라 여러 번 관통당하는 셈. 프리팹이 없으면 원래의 클립 끝 한 방 타격으로 대체된다.
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

            // 트리거는 애니메이터의 업데이트 패스가 돌기 전까지 소비되지 않아 현재 상태가
            // 한 프레임 동안 여전히 Idle/Run으로 읽힐 수 있다 - Attack03을 벗어나기를
            // 기다리기 전에 실제 진입부터 기다려야, 공격이 재생되지도 않은 채로
            // WaitUntilIdle을 바로 통과해버리는 일이 없다.
            yield return WaitForStateToStart(animator, ultimateStateName);
        }
        else if (!barrage)
        {
            yield return new WaitForSeconds(ultimateClipFallbackLength);
        }

        if (barrage)
        {
            yield return FireUltimateLaserRing();

            // 자연스러운 전환(WaitUntilIdle)을 기다리지 않고 바로 Idle로 끊는다 - Attack03은
            // 반복 재생이라 bool만 끄고 기다리면 다음 루프 사이클이 끝날 때까지 총 쏘는 포즈가
            // 빛줄기보다 더 오래 남아있었다. VFX/데미지가 끝나는 바로 그 프레임에 애니메이션도
            // 같이 끊어야 둘이 어긋나지 않는다.
            if (animator != null)
            {
                animator.SetBool(UltimateActiveParam, false);
                animator.Play(idleStateName, 0, 0f);
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

    // VFX는 플레이어 발밑에 딱 한 번만 스폰한다 - 이펙트 자체가 알아서 빛줄기를 여러 번
    // 떨어뜨리는 것처럼 보이고, 코드는 그 위에 ultimateLaserCount번만 데미지 틱을 맞춰
    // 넣는다. 궁극기 총 데미지는 그대로고 틱들에 나눠질 뿐이라, 각각이 한 발씩 박히는 느낌.
    private IEnumerator FireUltimateLaserRing()
    {
        if (Player == null) yield break;

        int count = Mathf.Max(1, ultimateLaserCount);
        SpawnUltimateLaserUnderPlayer();

        float interval = Mathf.Max(0f, ultimateBarrageDuration) / count;
        float damagePerHit = AttackPower * ultimateDamageMultiplier / count;

        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(interval);

            if (IsDead) yield break;
            if (Player == null) yield break;

            SpawnHitImpactVfx();
            // 의도적으로 ParryManager를 거치지 않는다 - 궁극기는 원래부터 패링 불가였고,
            // 패링 한 번에 취소되는 탄막이라면 그것이 대체한 단일 타격보다 약해진다.
            Player.TakeDamage(damagePerHit);
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

    private void SpawnUltimateLaserAt(Vector3 spawn)
    {
        if (ultimateLaserPrefab == null) return;

        GameObject vfx = Instantiate(ultimateLaserPrefab, spawn, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * ultimateLaserScale;

        // Laser AOE의 11개 시스템은 데모 씬에서 계속 실행되도록 전부 반복 재생이다 -
        // 그대로 두면 스폰된 각 고리가 영원히 발사된다.
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

    // 지속되는 자식 오브젝트 대신 매번 새로 스폰한다 - 플레이어 자신의 피격 VFX와 달리
    // 여러 몬스터가 동시에 같은 플레이어에게 적중시킬 수 있어서, 공유 자식이라면 깔끔하게
    // 겹쳐 쌓이는 대신 하나의 Play()/Stop()을 두고 서로 다투게 된다.
    private void SpawnUltimateImpactVfx()
    {
        if (ultimateImpactVfxPrefab == null || Player == null) return;

        // 크레이터/버스트가 이펙트의 원점에 위치하므로 그 원점이 지면에 닿아야 한다.
        // 플레이어 transform은 발이 아니라 캡슐 중심이라, 거기 바로 스폰하면 크리스탈이
        // 허리 높이에서 터져 다 떨어지기도 전에 터지는 것처럼 보였다.
        Vector3 impactPoint = Player.transform.position;
        Collider playerCollider = Player.GetComponent<Collider>();
        if (playerCollider != null) impactPoint.y = playerCollider.bounds.min.y;

        GameObject vfx = Instantiate(ultimateImpactVfxPrefab, impactPoint, Quaternion.identity);

        // Hovl 이펙트들은 데모 씬에서 계속 실행되도록 반복 재생이라, 한 번만 써도 파괴 타이머
        // 도중에 낙하 연출 전체가 재시작되어 두 번 발동한 것처럼 보인다. 한 번 사용 = 한 사이클.
        float speed = Mathf.Max(0.01f, ultimateImpactVfxSpeed);
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            // 시스템별 설정이라 모든 서브 이미터(크리스탈, 플래시, 스파크, 연기)에 다 넣어야
            // 한다 - 하나라도 1배속으로 남으면 나머지 이펙트보다 뒤처진다.
            main.simulationSpeed = speed;
        }

        // 이펙트가 이제 원래 재생 시간의 일부 만에 끝나버리므로, 정리 타이머도 함께
        // 줄여야 한다. 그렇지 않으면 텅 빈 오브젝트가 그대로 남아있게 된다.
        Destroy(vfx, ultimateImpactVfxLifetime / speed);
    }

    // 적중한 모든 공격(일반 스윙, 텔레그래프 타격, 궁극기)과 함께 발동한다 -
    // SpawnUltimateImpactVfx와 달리 발밑 크레이터가 아니라 타격이 맞닿는 느낌이어야 하므로,
    // 플레이어의 발이 아니라 중심에서 나타난다.
    private void SpawnHitImpactVfx()
    {
        if (hitImpactVfxPrefab == null || Player == null) return;

        GameObject vfx = Instantiate(hitImpactVfxPrefab, Player.transform.position, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * hitImpactVfxScale;

        float speed = Mathf.Max(0.01f, hitImpactVfxSpeed);

        // 궁극기 임팩트 프리팹처럼 반복되는 데모 씬 시스템이다 - 한 번의 타격은 계속
        // 돌아가는 시스템이 아니라 한 번의 버스트로 느껴져야 한다.
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            // 궁극기 임팩트와 마찬가지로 시스템별 설정 - 서브 이미터 하나라도 1배속으로
            // 남으면 나머지보다 먼저 끝나서 한 번의 버스트라는 느낌이 깨진다.
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

    // 일반 몬스터 전용: 플레이어의 공격 틱과 무관하게 자기 고정 타이머로 때리므로,
    // 플레이어의 치명타가 이 몬스터의 스윙을 선점하는 일은 절대 없다 - 공격력이 무한이어도
    // 최소 한 대는 맞아야 한다는 규칙이라, 임팩트 타이밍 자체를 GuaranteedFirstStrikeMargin만큼
    // 앞당겨서 강제한다.
    private IEnumerator NormalAttackLoop()
    {
        // 일반 몬스터는 손대지 않은 원본 클립을 그대로 재생한다 - 충전도, 분리도 없다.
        CharacterAnimator?.SetBool(UseTelegraphParam, false);

        // 클립을 한 번의 공격 간격 안에 눌러 담는다 - 아니면 다음 공격이 클립이 끝나기도 전에
        // 재트리거해서 클립이 끝까지 못 가고 몬스터가 그냥 씰룩거리기만 한다.
        float clipLength = ResolveClipLength(plainAttackClipName, plainAttackClipFallbackLength);
        if (attackInterval > 0.01f)
        {
            CharacterAnimator?.SetFloat(AttackSpeedParam, clipLength / attackInterval);
        }

        while (!IsDead)
        {
            PlayAttackAnimation();

            float impactDelay = WeaponSwing != null ? WeaponSwing.AttackImpactDelay : fallbackAttackImpactDelay;
            if (PlayerWeaponSwing != null)
            {
                impactDelay = Mathf.Min(impactDelay, Mathf.Max(0f, PlayerWeaponSwing.AttackImpactDelay - GuaranteedFirstStrikeMargin));
            }
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
