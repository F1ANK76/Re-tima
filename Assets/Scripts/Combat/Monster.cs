using UnityEngine;

public partial class Monster : MonoBehaviour
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
    // 클립 안에서 칼이 닿는 지점(0~1). 클립은 attackInterval로 압축 재생되므로
    // 실제 타격 시각은 이 비율 x attackInterval이다.
    [Range(0f, 1f)] [SerializeField] private float plainAttackImpactFraction = 0.4f;
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
    [SerializeField] private GameObject ultimateImpactVfxPrefab;
    [SerializeField] private float ultimateImpactVfxLifetime = 3f;
    // 임팩트 이펙트의 재생 속도. Hovl의 크리스탈은 쇼케이스 씬에 맞춰진 속도로
    // 떨어지는데, 이는 (이미 빨라진) 공격 애니메이션 옆에서 보면 느리게 늘어져 보인다.
    [SerializeField] private float ultimateImpactVfxSpeed = 2f;
    [SerializeField] private UltimateGaugeView ultimateGaugeView;

    private const string AttackUltimateParam = "AttackUltimate";
    private float ultimateChargeTimer;

    private bool animatorSearched;
    private Animator monsterAnimator;
    // 몬스터 모델의 Animator는 프리팹 루트가 아니라 그 아래 FBX 모델에 붙어 있다.
    private Animator MonsterAnimator
    {
        get
        {
            if (!animatorSearched)
            {
                animatorSearched = true;
                monsterAnimator = GetComponentInChildren<Animator>();
            }
            return monsterAnimator;
        }
    }

    private PlayerCharacter playerCache;
    private PlayerCharacter Player => playerCache ??= moveTarget.GetComponent<PlayerCharacter>();

    private bool bossLoopStarted;
    private bool normalLoopStarted;
    private float attackInterval;

    // 몬스터가 스윙을 시작하고 실제로 타격이 들어가기까지의 지연.
    [SerializeField] private float attackImpactDelay = 0.3f;
    [SerializeField] private float deathAnimDuration = 1f;

    // 이 몬스터의 사체가 실제로 화면에서 사라지기까지 걸리는 시간 - Die 클립 길이 그대로다.
    // StageManager가 다음 스폰 딜레이를 여기에 맞춰서, 사체가 사라지는 순간과 다음 몬스터
    // 등장이 겹치지 않는다.
    public float DeathVisualDuration => deathAnimDuration;

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
        MonsterAnimator.SetTrigger(AnimParams.Attack);
    }

    public void SetMovement(Transform target, float speed, float stopDistance)
    {
        moveTarget = target;
        moveSpeed = speed;
        // 더 큰 몬스터는 더 멀리서 멈춰야 한다, 그렇지 않으면 캡슐이 실제로 닿기도
        // 전에 커진 몸이 시각적으로 플레이어와 겹치거나 파묻어버린다.
        stoppingDistance = stopDistance * transform.localScale.x;
        HasArrived = false;
        MonsterAnimator.SetBool(AnimParams.IsMoving, true);
    }

    private void Update()
    {
        if (moveTarget == null) return;

        if (!HasArrived)
        {
            Vector3 toTarget = moveTarget.position - transform.position;
            toTarget.y = 0f;

            // 도착하지 않았다면 플레이어 한테 이동
            if (toTarget.magnitude > stoppingDistance)
            {
                transform.position += toTarget.normalized * moveSpeed * Time.deltaTime;
                transform.forward = toTarget.normalized;
                return;
            }

            // 도착 시 멈춤
            HasArrived = true;
            MonsterAnimator.SetBool(AnimParams.IsMoving, false);
        }

        // 죽음 연출 중(무적)에는 멈춰둔 공격 루프를 다시 켜지 않는다.
        if (Player.IsInvulnerable) return;

        // 엘리트와 보스는 둘 다 패링 결투로 싸운다: 예고 동작을 보인 뒤 타격한다.
        // 일반 몬스터는 그냥 고정된 타이머로 서로 타격을 주고받는다.
        if (!bossLoopStarted && (Type == MonsterType.Boss || Type == MonsterType.Elite))
        {
            bossLoopStarted = true;

            // 엘리트, 보스 차징 공격
            StartCoroutine(TelegraphAttackLoop());

            // 보스 궁극기
            if (Type == MonsterType.Boss) StartCoroutine(UltimateChargeLoop());
        }
        else if (!normalLoopStarted && Type == MonsterType.Normal)
        {
            normalLoopStarted = true;
            
            // 일반 몬스터 평타
            StartCoroutine(NormalAttackLoop());
        }
    }

    private float ResolveClipLength(string clipName, float fallback)
        => AnimClipTiming.ResolveClipLength(MonsterAnimator, clipName, fallback);

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        HealthBar?.SetHealth(CurrentHp, MaxHp);
        GameEvents.RaiseMonsterDamaged(this, amount);

        if (IsDead)
        {
            GameEvents.RaiseMonsterDied(this);

            MonsterAnimator.SetTrigger(AnimParams.Die);
            Destroy(gameObject, deathAnimDuration);
        }
    }
}
