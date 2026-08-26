using System.Collections;
using UnityEngine;

public class PlayerCharacter : MonoBehaviour
{
    [SerializeField] private float startingMaxHp = 50f;
    [SerializeField] private float startingAttackPower = 1f;
    [SerializeField] private ParticleSystem hitVfx;
    // Hovl's aura prefab (Play On Awake, 1s natural loop length) - kept inactive by default
    // and just toggled on/off around a stat drop, rather than driven via Play()/Stop() like
    // hitVfx, since simply activating it already plays one full cycle on its own.
    [SerializeField] private GameObject buffVfx;

    // How long the hit flash stays up regardless of its own (much longer) particle
    // lifetimes - every monster type (normal/elite/boss) lands here via TakeDamage.
    private const float HitVfxDuration = 0.3f;
    private const float BuffVfxDuration = 1f;

    private PlayerStats stats;
    public PlayerStats Stats => stats ??= new PlayerStats(startingMaxHp, startingAttackPower);

    // Phase 2 hook: set true while a successful parry grants invulnerability.
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
        if (buffVfx == null) return;

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

    public void Attack(Monster target)
    {
        if (target == null) return;
        target.TakeDamage(Stats.AttackPower);
    }

    public void TakeDamage(float amount)
    {
        if (IsInvulnerable || Stats.IsDead) return;

        Stats.ApplyDamage(amount);
        NotifyStatsChanged();
        PlayHitVfx();

        if (Stats.IsDead)
        {
            // HP stays at zero for the whole death animation - StageManager revives the
            // player at full health once it finishes and the stage restarts.
            GameEvents.RaisePlayerDied();
        }
    }

    // Every stage run starts the player at full health, so a stage is always attempted
    // from a clean slate rather than with whatever was left over from the last one.
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
        if (hitVfx == null) return;

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
