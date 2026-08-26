using System.Collections;
using UnityEngine;

// Must run before CombatLoop: both Update() methods can cross their respective thresholds
// (tick interval / charge full) on the same frame, and whichever runs first wins that frame.
// Without a forced order, CombatLoop could still land its own tick's normal swing before
// this sets IsSuspended, producing a normal hit and the ultimate firing together.
[DefaultExecutionOrder(-100)]
public class UltimateManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private WeaponSwing weaponSwing;
    [SerializeField] private UltimateGaugeView gaugeView;
    [SerializeField] private ParticleSystem explosionVfx;
    [SerializeField] private GameObject stageBanner;
    [SerializeField] private CombatLoop combatLoop;

    private const float ChargeDuration = 30f;
    private const float DamageMultiplier = 3f;

    private const string UltimateStateName = "JumpFull_Spin_RM_SwordAndShield";
    private const string IdleStateName = "Idle_Battle_SwordAndShield";
    // Matches CombatLoop's own RunningStateName - the other state it's safe to cut into.
    private const string RunningStateName = "MoveFWD_Normal_InPlace_SwordAndShield";
    private const float UltimateAnimDuration = 24f / 30f;
    // The clip's own root-motion curve has the character back at ground height by frame
    // 19 of 24 (30fps) - that's the actual landing instant, not a guess off the total length.
    private const float LandingDelay = 19f / 30f;
    // How long to let the explosion keep emitting. The systems' own particles live up to 1s
    // (the smoke), so this only has to outlast the bursts - it is NOT the visual length.
    private const float ExplosionVfxDuration = 0.5f;

    // The ultimate is the reward for clearing stage 1's boss, so it stays completely inert
    // (and its gauge hidden) for the whole of stage 1 - see StageManager.SkillUnlockStage,
    // which announces the unlock on that clear.
    private const int UnlockStage = 2;

    private float chargeTimer;
    private Monster currentMonster;
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

    // The first OnStageChanged only arrives once StageManager announces stage 1-1, which is a
    // few frames into the run - without this the gauge would flash on screen before that
    // first event hides it.
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
            // Hidden outright rather than left sitting empty: an always-0% bar through the
            // whole of stage 1 reads as a broken gauge, not a locked ability.
            gaugeView.gameObject.SetActive(IsUnlocked);
        }
    }

    private void Update()
    {
        // Nothing charges, nothing fires, and CombatLoop is never suppressed before the
        // unlock - stage 1 plays exactly as it did before this ability existed.
        if (!IsUnlocked) return;

        // The stage announcement covers the screen for a couple seconds before anything
        // actually spawns - charging through that would waste the buildup on a moment the
        // player never sees, so hold at 0 until the banner clears.
        if (stageBanner != null && stageBanner.activeSelf) return;

        // Clamped rather than left to run past full - once charged it just waits (gauge
        // held at 100%) for a monster to be around and the current animation to clear.
        chargeTimer = Mathf.Min(chargeTimer + Time.deltaTime, ChargeDuration);
        if (gaugeView != null) gaugeView.SetFraction(chargeTimer / ChargeDuration);

        // Not just "a monster exists" - it has to actually be in melee range, the same
        // gate CombatLoop uses before it'll throw a normal hit.
        bool targetInRange = currentMonster != null && currentMonster.HasArrived;
        // CombatLoop raises this for its own victory/death poses too, not just when this
        // class raises it for the ultimate itself - a boss fight is long enough that the
        // gauge is routinely full right as the kill lands, and firing into that pose would
        // hard-Play() straight over the trigger-based transition into it.
        bool combatLoopBusy = combatLoop != null && combatLoop.IsSuspended;
        bool chargeReady = chargeTimer >= ChargeDuration && targetInRange && !combatLoopBusy;

        // Blocks CombatLoop from starting any further normal swings the moment the gauge is
        // full - otherwise the wait below for a safe window can stretch across several more
        // swings, since the animator only sits in a genuinely safe gap for a frame or two
        // between one swing settling and the next tick re-triggering it.
        if (combatLoop != null) combatLoop.SuppressNewAttacks = chargeReady;

        if (chargeReady && IsSafeToInterrupt())
        {
            chargeTimer = 0f;
            if (combatLoop != null) combatLoop.SuppressNewAttacks = false;
            StartCoroutine(PlayUltimate());
        }
    }

    // Only allowed to cut in from Idle or the run cycle - never mid-attack, mid-parry, or
    // mid-riposte, so a normal hit/defend/counter always gets to finish on its own.
    private bool IsSafeToInterrupt()
    {
        // Checked ahead of the animator state: a swing's damage can still be in flight for a
        // frame or two after PlaySwing() before the animator actually reports being in the
        // attack state, and cutting in during that gap is exactly what produced both a normal
        // hit and the ultimate landing together.
        if (combatLoop != null && combatLoop.HasPendingAttack) return false;

        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator == null) return true;
        if (animator.IsInTransition(0)) return false;

        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(IdleStateName) || state.IsName(RunningStateName);
    }

    private IEnumerator PlayUltimate()
    {
        // Suspended for the whole sequence, not just while the animation plays - otherwise
        // CombatLoop's own tick can land a normal swing (and its Attack trigger) in the same
        // beat as the ultimate, which is exactly the "both fire at once" bug this closes.
        // Uses the IsSuspended flag rather than `enabled` - toggling `enabled` would run
        // CombatLoop's OnDisable and drop its OnMonsterDied subscription, so a kill landed
        // by this very ultimate would never clear its target and it'd keep swinging at the
        // corpse once resumed.
        if (combatLoop != null) combatLoop.PushSuspend();

        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator != null) animator.Play(UltimateStateName, 0, 0f);

        yield return new WaitForSeconds(LandingDelay);

        Monster target = currentMonster;
        if (player != null && target != null)
        {
            target.TakeDamage(player.Stats.AttackPower * DamageMultiplier);
        }

        if (explosionVfx != null) StartCoroutine(PlayExplosionVfx());

        yield return new WaitForSeconds(UltimateAnimDuration - LandingDelay);

        if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(UltimateStateName))
        {
            animator.Play(IdleStateName, 0, 0f);
        }

        if (combatLoop != null) combatLoop.PopSuspend();
    }

    private IEnumerator PlayExplosionVfx()
    {
        if (player != null) explosionVfx.transform.position = player.transform.position;

        explosionVfx.Play(true);
        yield return new WaitForSeconds(ExplosionVfxDuration);
        // StopEmitting, NOT StopEmittingAndClear. Clearing deleted every particle still in
        // flight, and since the smoke is authored to live a full second while this waited only
        // 0.3s, the explosion was being cut off partway and vanishing in a single frame - the
        // main reason the ultimate didn't read as a big move. Now the bursts stop but whatever
        // is already airborne plays out and fades on its own.
        explosionVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
