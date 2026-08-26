using System.Collections;
using UnityEngine;

public class CombatLoop : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private WeaponSwing weaponSwing;
    [SerializeField] private GroundScroller groundScroller;
    [SerializeField] private BackdropScroller backdropScroller;

    private const string RunningStateName = "MoveFWD_Normal_InPlace_SwordAndShield";
    private const string VictoryStateName = "Victory_Battle_SwordAndShield";
    private const string VictoryTriggerParam = "Victory";
    private const string DieStateName = "Die01_SwordAndShield";
    private const string DieTriggerParam = "Die";

    private Monster currentMonster;
    private float tickTimer;
    private bool hasOpenedOnCurrent;
    private Coroutine pendingDamageRoutine;

    // Lets other systems (the ultimate) pause the tick loop without touching `enabled` -
    // disabling the component would run OnDisable and drop the OnMonsterDied subscription,
    // so a kill landed while paused would never clear currentMonster and the loop would
    // keep swinging at a stale, already-dead target once it resumed.
    //
    // Reference counted rather than a plain bool. The ultimate's suspend routinely overlaps
    // the victory sequence it causes - its 10x hit kills the monster at LandingDelay while
    // the ultimate still has its own tail left to play - so both were suspending at once.
    // With a save-then-restore bool, the victory sequence captured "was suspended = true"
    // from the still-running ultimate and wrote that back when it finished, stranding the
    // flag on permanently: the player then stood in Idle and never threw another attack
    // while the next encounter beat on them.
    private int suspendCount;
    public bool IsSuspended => suspendCount > 0;

    public void PushSuspend() => suspendCount++;
    public void PopSuspend() => suspendCount = Mathf.Max(0, suspendCount - 1);

    // Hard reset for the respawn path only: a death (or a debug stage jump) runs
    // StopAllCoroutines on StageManager, which can cut a sequence off before it ever pops its
    // own suspend. suppressMovement rides along for the same reason - PlayVictorySequence sets
    // it and only clears it at its own tail end, so the exact same interruption can leave it
    // stuck true, which permanently blocks Update() from ever driving the character back into
    // its running state. Combat and movement are always meant to be live after a hard reset
    // like this, so both are forced back on here instead of trusting the interrupted coroutine.
    public void ClearSuspend()
    {
        suspendCount = 0;
        suppressMovement = false;
    }
    // Set by UltimateManager once its gauge is full and a target is in range: blocks new
    // ticks from starting (but doesn't touch one already in flight) so the wait for a safe
    // window to fire the ultimate can't stretch across several more swings - the animator can
    // sit in its brief post-swing gap for only a frame or two, easy to miss entirely if a new
    // tick is free to re-trigger the attack state before the ultimate's check catches it.
    public bool SuppressNewAttacks { get; set; }
    // Held by ParryManager for the length of a successful parry's riposte. Deliberately a
    // separate flag from SuppressNewAttacks above: UltimateManager rewrites that one every
    // frame from its own gauge state, so a parry sharing it would have its suppression
    // cleared on the very next frame and the riposte would play under a normal swing.
    public bool RiposteInProgress { get; set; }
    // While true, Update() won't drive the player back into the running state even
    // though there's no monster around yet - keeps the victory pose from being
    // immediately overridden by the "run to the next encounter" behavior.
    private bool suppressMovement;

    // Held by a stat potion for as long as it's on the ground/homing in - the player waits
    // in place instead of already running toward the next encounter while one is still being
    // absorbed. Reference counted like suspendCount above, in case more than one is ever in
    // flight at once.
    private int idleHoldCount;
    public bool IsIdleHeld => idleHoldCount > 0;
    public void PushIdleHold() => idleHoldCount++;
    public void PopIdleHold() => idleHoldCount = Mathf.Max(0, idleHoldCount - 1);
    // Hard reset for StageManager's field-wipe (death respawn, debug stage jump): both paths
    // directly Destroy() any pickup still on the field, including one that pushed this
    // mid-toss and hasn't reached its own PopIdleHold yet - Destroy never gives it that
    // chance, and the leaked hold pins the player in idle forever afterward, since nothing is
    // left alive to ever pop it. Safe to zero unconditionally here: by the time the wipe
    // calls this, every pickup that could have been holding it is already gone.
    public void ClearIdleHold() => idleHoldCount = 0;

    // True from the instant a normal swing's tick fires until its damage actually lands.
    // The animator's own state can lag a frame behind PlaySwing() (trigger consumption isn't
    // instant), so UltimateManager checks this directly rather than relying solely on
    // animator state to know a normal hit is still in flight.
    public bool HasPendingAttack => pendingDamageRoutine != null;

    private void OnEnable()
    {
        GameEvents.OnMonsterSpawned += HandleMonsterSpawned;
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterSpawned -= HandleMonsterSpawned;
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    // StageManager destroys the monster that just killed the player and respawns a fresh
    // encounter at the checkpoint - drop the stale reference so DoTick doesn't keep
    // ticking against a monster that's about to be gone.
    private void HandlePlayerDied()
    {
        // Without this, a swing already queued before the killing blow landed still connects
        // afterward - the monster could die in the same exchange that killed the player,
        // reading as a double KO instead of a clean loss.
        CancelPendingAttack();

        currentMonster = null;
        hasOpenedOnCurrent = false;
        tickTimer = 0f;
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        currentMonster = monster;
        tickTimer = 0f;
        hasOpenedOnCurrent = false;
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (currentMonster != monster) return;

        currentMonster = null;
    }

    // Plays the player's victory pose and waits for it to actually finish before
    // returning - StageManager holds the next banner/spawn until this completes.
    public IEnumerator PlayVictorySequence()
    {
        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator == null) yield break;

        // Also holds off the ultimate for the sequence: its gauge fills over the same 30s a
        // boss fight takes, so it's routinely full right as the kill lands. Left unguarded,
        // UltimateManager's hard animator.Play() (not a trigger - it can't be queued behind
        // this one) stomps the Victory transition the instant it fires, and the wait below
        // times out with the pose never having shown at all.
        PushSuspend();

        suppressMovement = true;
        animator.SetBool("IsMoving", false);
        animator.SetTrigger(VictoryTriggerParam);

        const float SafetyTimeout = 5f;
        float elapsed = 0f;
        while (elapsed < SafetyTimeout && !animator.GetCurrentAnimatorStateInfo(0).IsName(VictoryStateName))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < SafetyTimeout &&
               (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).IsName(VictoryStateName)))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        suppressMovement = false;
        PopSuspend();
    }

    // Plays the player's death pose and waits for it to finish (it hands off into the
    // pack's own Die01_Stay -> GetUp -> Run chain on its own after that) - StageManager
    // holds the checkpoint rewind/respawn until this returns.
    public IEnumerator PlayDeathSequence()
    {
        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator == null) yield break;

        // Same guard as PlayVictorySequence, and the same reason: an ultimate ready to fire
        // the instant the killing blow lands would hard-Play() over this trigger too.
        PushSuspend();

        animator.SetTrigger(DieTriggerParam);

        const float SafetyTimeout = 5f;
        float elapsed = 0f;
        while (elapsed < SafetyTimeout && !animator.GetCurrentAnimatorStateInfo(0).IsName(DieStateName))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < SafetyTimeout &&
               (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).IsName(DieStateName)))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        PopSuspend();
    }

    private void Update()
    {
        // Driven before the suspend check, and deliberately so: the ultimate, victory and
        // death sequences all PushSuspend, and returning early used to leave IsScrolling
        // stuck at whatever it last was. If the character happened to be running when an
        // ultimate started, the world kept sliding for the entire cast.
        UpdateWorldScroll();

        if (IsSuspended) return;

        // Keep running until there's actually a monster in melee range to fight - a
        // monster merely existing (still walking in from off-screen) isn't a reason to
        // stop, only one that has arrived and is ready to trade hits is.
        bool shouldBeMoving = !suppressMovement && !IsIdleHeld && (currentMonster == null || !currentMonster.HasArrived);
        if (weaponSwing != null) weaponSwing.CharacterAnimator?.SetBool("IsMoving", shouldBeMoving);

        if (SuppressNewAttacks || RiposteInProgress) return;

        if (currentMonster == null || !currentMonster.HasArrived) return;

        // Opening hit lands the instant the monster arrives, instead of waiting
        // out a full tickInterval first.
        if (!hasOpenedOnCurrent)
        {
            hasOpenedOnCurrent = true;
            tickTimer = 0f;
            DoTick();
            return;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer < stageConfig.tickInterval) return;

        tickTimer = 0f;
        DoTick();
    }

    // The world only scrolls while the character is actually in their run state, and never
    // while the loop is suspended - an ultimate, a victory pose or a death animation all
    // hold the character in place, so the ground and backdrop have to hold too.
    //
    // Read off the Animator's current state rather than the movement flag: the
    // Idle<->running transition (and any attack still blending back to Idle) takes a moment
    // to settle, and keying off the flag started the ground sliding before the character
    // visibly began to run.
    private void UpdateWorldScroll()
    {
        bool isRunning = false;
        if (!IsSuspended && weaponSwing != null && weaponSwing.CharacterAnimator != null)
            isRunning = weaponSwing.CharacterAnimator.GetCurrentAnimatorStateInfo(0).IsName(RunningStateName);

        if (groundScroller != null) groundScroller.IsScrolling = isRunning;
        if (backdropScroller != null) backdropScroller.IsScrolling = isRunning;
    }

    private void DoTick()
    {
        // Captured locally: attacking may synchronously trigger OnMonsterDied,
        // which spawns the next monster and overwrites currentMonster before we return here.
        Monster target = currentMonster;
        if (target == null) return;

        if (weaponSwing != null) weaponSwing.PlaySwing();
        pendingDamageRoutine = StartCoroutine(DealDamageAfterSwing(target));
    }

    private IEnumerator DealDamageAfterSwing(Monster target)
    {
        float delay = weaponSwing != null ? weaponSwing.AttackImpactDelay : 0f;
        yield return new WaitForSeconds(delay);

        if (target != null) player.Attack(target);
        pendingDamageRoutine = null;
    }

    // Called by ParryManager the instant the player presses parry - a normal swing already
    // in flight shouldn't land its hit (or keep showing its slash) once the pose has been
    // cut over to Defend.
    public void CancelPendingAttack()
    {
        if (pendingDamageRoutine != null)
        {
            StopCoroutine(pendingDamageRoutine);
            pendingDamageRoutine = null;
        }

        if (weaponSwing != null) weaponSwing.CancelSwing();
    }
}
