using System.Collections;
using UnityEngine;

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

    private const float UltimateClipFallbackLength = 24f / 30f;
    private const float LandingFraction = 19f / 24f;
    private const float ExplosionVfxDuration = 0.5f;

    private const int UnlockStage = 2;

    private float chargeTimer;
    private Monster currentMonster;
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

    private void Start()
    {
        gaugeView.gameObject.SetActive(IsUnlocked);
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

        gaugeView.SetFraction(0f);
        gaugeView.gameObject.SetActive(IsUnlocked);
    }

    private void Update()
    {
        // 스킬 해금 전이면 사용 불가
        if (!IsUnlocked) return;

        if (stageBanner.activeSelf) return;

        chargeTimer = Mathf.Min(chargeTimer + Time.deltaTime, ChargeDuration);
        gaugeView.SetFraction(chargeTimer / ChargeDuration);

        bool targetInRange = currentMonster != null && currentMonster.HasArrived;
        bool combatLoopBusy = combatLoop.IsSuspended;
        bool chargeReady = chargeTimer >= ChargeDuration && targetInRange && !combatLoopBusy;

        combatLoop.SuppressNewAttacks = chargeReady;

        if (chargeReady && IsSafeToInterrupt())
        {
            chargeTimer = 0f;
            combatLoop.SuppressNewAttacks = false;
            ultimateRoutine = StartCoroutine(PlayUltimate());
        }
    }

    private bool IsSafeToInterrupt()
    {
        if (combatLoop.HasPendingAttack) return false;

        Animator animator = weaponSwing.PlayerAnimator;
        if (animator.IsInTransition(0)) return false;

        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(PlayerAnimStates.Idle) || state.IsName(PlayerAnimStates.Running);
    }

    private IEnumerator PlayUltimate()
    {
        combatLoop.PushSuspend();

        Animator animator = weaponSwing.PlayerAnimator;
        animator.Play(PlayerAnimStates.Ultimate, 0, 0f);

        // 이 컨트롤러는 스테이트 이름과 클립 이름이 같아서 상수 하나로 둘 다 가리킨다.
        float ultimateLength = AnimClipTiming.ResolveClipLength(
            animator, PlayerAnimStates.Ultimate, UltimateClipFallbackLength);
        float landingDelay = ultimateLength * LandingFraction;

        yield return new WaitForSeconds(landingDelay);

        Monster target = currentMonster;
        if (target != null)
        {
            target.TakeDamage(player.Stats.AttackPower * DamageMultiplier);
        }

        StartCoroutine(PlayExplosionVfx());

        yield return new WaitForSeconds(ultimateLength - landingDelay);

        if (animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Ultimate))
        {
            animator.Play(PlayerAnimStates.Idle, 0, 0f);
        }

        combatLoop.PopSuspend();
        ultimateRoutine = null;
    }

    public void CancelUltimate()
    {
        if (ultimateRoutine == null) return;

        StopCoroutine(ultimateRoutine);
        ultimateRoutine = null;

        combatLoop.PopSuspend();
    }

    private IEnumerator PlayExplosionVfx()
    {
        explosionVfx.transform.position = player.transform.position;

        explosionVfx.Play(true);
        yield return new WaitForSeconds(ExplosionVfxDuration);
        explosionVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
