using System.Collections;
using UnityEngine;

public class PlayerCharacter : MonoBehaviour
{
    [SerializeField] private float startingMaxHp = 20f;
    [SerializeField] private float startingAttackPower = 0.5f;
    [SerializeField] private ParticleSystem hitVfx;
    [SerializeField] private GameObject buffVfx;

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
            GameEvents.RaisePlayerDied();
        }
    }

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
