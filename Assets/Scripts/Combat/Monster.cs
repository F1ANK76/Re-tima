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
    [SerializeField] private float minTimeToImpact = 0.5f;
    [SerializeField] private float maxTimeToImpact = 2f;
    [SerializeField] private string windUpStateName = "AttackWindUp";
    [SerializeField] private string idleStateName = "Idle";

    [SerializeField] private string windUpClipName = "Footman_Attack01_WindUp";
    [SerializeField] private string plainAttackClipName = "Attack01";
    [SerializeField] private float windUpClipFallbackLength = 0.3f;
    [SerializeField] private float plainAttackClipFallbackLength = 1.333f;
    [Range(0f, 1f)] [SerializeField] private float plainAttackImpactFraction = 0.4f;
    [SerializeField] private float windUpImpactFraction = 0.92f;
    [SerializeField] private float windUpEntryOffset = 0.15f;
    [SerializeField] private float windUpChargeEndFraction = 0.64f;
    [SerializeField] private float postAttackPause = 0.45f;

    [SerializeField] private string attack2StateName = "Attack02";
    [SerializeField] private string attack2ClipName = "Attack02";
    [SerializeField] private float attack2ClipFallbackLength = 1.333f;
    [SerializeField] private float attack2ImpactTime = 0.533f;

    private const string AttackSpeedParam = "AttackSpeed";
    private const string WindUpSpeedParam = "WindUpSpeed";
    private const string UseTelegraphParam = "UseTelegraph";
    private const string AttackPattern2Param = "AttackPattern2";

    [Header("Ultimate attack (Elite/Boss)")]
    [SerializeField] private float ultimateChargeDuration = 5f;
    [SerializeField] private float ultimateDamageMultiplier = 3f;
    [SerializeField] private string ultimateStateName = "Attack03";
    [SerializeField] private GameObject ultimateImpactVfxPrefab;
    [SerializeField] private float ultimateImpactVfxLifetime = 3f;
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
