using System.Collections;
using UnityEngine;

public class PlayerCharacter : MonoBehaviour
{
    [SerializeField] private float startingMaxHp = 20f;
    [SerializeField] private float startingAttackPower = 0.5f;
    [SerializeField] private ParticleSystem hitVfx;
    // Hovl의 오라 프리팹(Play On Awake, 자체 루프 길이 1초) - 기본은 비활성이고 스탯 드롭 시점에
    // 켜고 끄기만 한다. hitVfx처럼 Play()/Stop()으로 제어하지 않는 이유는, 활성화만으로 이미 한
    // 사이클이 온전히 재생되기 때문이다.
    [SerializeField] private GameObject buffVfx;

    // 피격 플래시 자체의 (훨씬 긴) 파티클 수명과 무관하게 얼마나 떠 있을지 - 모든
    // 몬스터 타입(normal/elite/boss)의 공격이 TakeDamage를 거쳐 여기로 들어온다.
    private const float HitVfxDuration = 0.3f;
    private const float BuffVfxDuration = 1f;

    private PlayerStats stats;
    public PlayerStats Stats => stats ??= new PlayerStats(startingMaxHp, startingAttackPower);

    // 2단계용 훅: 패링 성공으로 무적이 부여되는 동안 true로 설정된다.
    public bool IsInvulnerable { get; set; } = false;

    private HealthBarView healthBarCache;
    private HealthBarView HealthBar => healthBarCache ??= GetComponentInChildren<HealthBarView>();

    private Coroutine hitVfxRoutine;
    private Coroutine buffVfxRoutine;

    private void OnEnable()
    {
        GameEvents.OnStatDropGained += HandleStatDropGained;
    }

    private void OnDisable()
    {
        GameEvents.OnStatDropGained -= HandleStatDropGained;
    }

    private void Start()
    {
        NotifyStatsChanged();
    }

    private void HandleStatDropGained(StatGrade grade, StatType statType, float amount)
    {
        PlayBuffVfx();
    }

    private void PlayBuffVfx()
    {
        if (buffVfxRoutine != null) StopCoroutine(buffVfxRoutine);
        buffVfxRoutine = StartCoroutine(PlayBuffVfxRoutine());
    }

    private IEnumerator PlayBuffVfxRoutine()
    {
        buffVfx.SetActive(true);
        yield return new WaitForSeconds(BuffVfxDuration);

        buffVfx.SetActive(false);
        buffVfxRoutine = null;
    }

    public void Attack(Monster target, float damageMultiplier = 1f)
    {
        if (target == null) return;
        target.TakeDamage(Stats.AttackPower * damageMultiplier);
    }

    public void TakeDamage(float amount)
    {
        if (IsInvulnerable || Stats.IsDead) return;

        Stats.ApplyDamage(amount);
        NotifyStatsChanged();
        PlayHitVfx();

        if (Stats.IsDead)
        {
            // HP는 죽음 애니메이션이 재생되는 내내 0으로 유지된다 - StageManager가
            // 애니메이션이 끝나면 플레이어를 풀피로 되살리고 스테이지를 재시작한다.
            GameEvents.RaisePlayerDied();
        }
    }

    // 모든 스테이지 시도는 플레이어를 풀피 상태로 시작한다 - 그래야 이전 판에서
    // 남은 상태가 아니라 항상 깨끗한 상태로 스테이지를 시도하게 된다.
    public void RestoreToFullHp()
    {
        Stats.ResetToFull();
        NotifyStatsChanged();
    }

    public void IncreaseAttack(float amount)
    {
        Stats.IncreaseAttack(amount);
        NotifyStatsChanged();
    }

    public void IncreaseMaxHp(float amount)
    {
        Stats.IncreaseMaxHp(amount);
        NotifyStatsChanged();
    }

    private void PlayHitVfx()
    {
        if (hitVfxRoutine != null) StopCoroutine(hitVfxRoutine);
        hitVfxRoutine = StartCoroutine(PlayHitVfxRoutine());
    }

    private IEnumerator PlayHitVfxRoutine()
    {
        hitVfx.Play(true);
        yield return new WaitForSeconds(HitVfxDuration);

        hitVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        hitVfxRoutine = null;
    }

    private void NotifyStatsChanged()
    {
        GameEvents.RaisePlayerStatsChanged(Stats);
        HealthBar?.SetHealth(Stats.CurrentHp, Stats.MaxHp);
    }
}
