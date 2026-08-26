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
    // Time from the attack starting to the hit landing, picked fresh each swing - the
    // number the player actually has to react to. The charge (windup) clip is stretched
    // to fill exactly this long.
    [SerializeField] private float minTimeToImpact = 0.5f;
    [SerializeField] private float maxTimeToImpact = 2f;
    [SerializeField] private string windUpStateName = "AttackWindUp";
    [SerializeField] private string idleStateName = "Idle";

    [SerializeField] private string windUpClipName = "Footman_Attack01_WindUp";
    [SerializeField] private string plainAttackClipName = "Attack01";
    [SerializeField] private float windUpClipFallbackLength = 0.3f;
    [SerializeField] private float plainAttackClipFallbackLength = 1.333f;
    // How far into the windup clip's own timeline the hit lands, as a fraction of its
    // native (unstretched) length - there's no separate strike clip, the charge's own
    // landing beat (it jumps then slams back down) IS the hit. For PowerUpNoWeapon this
    // is the hard crouch around t=2.7s of its 2.933s length.
    [SerializeField] private float windUpImpactFraction = 0.92f;
    // PowerUpNoWeapon opens with a hard arm whip (~74 degrees in a couple of frames) that
    // reads as a bat swing in its own right - so the animator's Idle/Run -> AttackWindUp
    // transitions enter the clip this far in, skipping it. Must match the offset set on
    // those transitions: only the (impact - entry) slice actually plays, so the charge
    // speed below is derived from that slice rather than the whole clip.
    [SerializeField] private float windUpEntryOffset = 0.15f;
    // Breathing room after a hit lands before the next charge may begin. Without it the
    // landing sweep of one attack ran straight into the next attack's entry, which read as
    // the bat being swung twice in a row.
    [SerializeField] private float postAttackPause = 0.45f;

    // Second telegraph pattern (Boss only) - picked randomly each swing so the boss
    // doesn't always telegraph the same way. Unlike the first pattern, this one has no
    // charge/randomized delay: it just plays the untouched clip at its native speed, and
    // the hit lands whenever the spear actually connects in that fixed timeline.
    [SerializeField] private string attack2StateName = "Attack02";
    [SerializeField] private string attack2ClipName = "Attack02";
    [SerializeField] private float attack2ClipFallbackLength = 1.333f;
    [SerializeField] private float attack2ImpactTime = 0.533f;

    private const string AttackSpeedParam = "AttackSpeed";
    private const string WindUpSpeedParam = "WindUpSpeed";
    private const string UseTelegraphParam = "UseTelegraph";
    private const string AttackPattern2Param = "AttackPattern2";

    [Header("Ultimate attack (Elite/Boss)")]
    // Mirrors the player's own ultimate: charges on a flat timer while the monster is
    // fighting, then fires once as a heavy, unparryable hit instead of a regular swing.
    [SerializeField] private float ultimateChargeDuration = 5f;
    [SerializeField] private float ultimateDamageMultiplier = 3f;
    [SerializeField] private string ultimateStateName = "Attack03";
    // The Footman_Attack03 clip's internal take is (confusingly) named "Victory", which
    // collides with the unrelated Victory state's own clip of the same name - looking it
    // up by name through the animator's clip list would be ambiguous, so this is just the
    // clip's real length instead of going through ResolveClipLength like the others.
    [SerializeField] private float ultimateClipFallbackLength = 10f / 3f;
    [SerializeField] private GameObject ultimateImpactVfxPrefab;
    [SerializeField] private float ultimateImpactVfxLifetime = 3f;
    // Playback rate for the impact effect. The Hovl crystals fall at a pace tuned for a
    // showcase scene, which drags next to the (already sped-up) attack animation.
    [SerializeField] private float ultimateImpactVfxSpeed = 2f;
    [SerializeField] private UltimateGaugeView ultimateGaugeView;

    [Header("Ultimate laser barrage (Boss)")]
    // Hovl "Laser AOE" - a ground tech-circle with a beam column above it. Null falls back
    // to the old behaviour (one heavy hit at the end of the clip).
    [SerializeField] private GameObject ultimateLaserPrefab;
    // How long the barrage keeps firing. Matched to Laser AOE's own 5s emission window so
    // the held animation, the damage ticks and the effect all end together.
    [SerializeField] private float ultimateBarrageDuration = 5f;
    // A ring of strikes centred behind the player, walked around in order so it reads as a
    // sweep closing in rather than everything landing at once.
    [SerializeField] private int ultimateLaserCount = 8;
    [SerializeField] private float ultimateLaserRingRadius = 2.2f;
    [SerializeField] private float ultimateLaserBehindDistance = 2.6f;
    [SerializeField] private float ultimateLaserScale = 1f;
    // Laser AOE's longest particle lifetime (4s) - the spawned object has to outlive its own
    // emission by this much or the tail is cut off mid-fade.
    [SerializeField] private float ultimateLaserLingerSeconds = 4f;
    // When ultimateLaserCount is 1 the barrage becomes a single circle held on the ground
    // under the player for its whole duration, ticking a flat (not AttackPower-scaled) amount
    // of damage on a fixed clock instead of splitting one lump sum across ring positions.
    [SerializeField] private float ultimateTickDamage = 5f;
    [SerializeField] private float ultimateTickInterval = 0.5f;

    [Header("Hit impact (on the player, every landed attack)")]
    // Null on a prefab that shouldn't show one (default) - only the stage-2 roster has this
    // wired to "Stones hit", so stage-1 monsters land damage exactly as before.
    [SerializeField] private GameObject hitImpactVfxPrefab;
    [SerializeField] private float hitImpactVfxLifetime = 3f;
    // The Hovl hit effects emit their bright layers (glow/flash) over roughly the first 0.1s
    // of a 1s cycle, which at native speed is over before it registers as a hit landing.
    // Under 1 stretches that burst out so the blow actually reads; the lifetime above is
    // divided by this so cleanup still tracks the real runtime.
    [SerializeField] private float hitImpactVfxSpeed = 0.4f;
    // Sized up from the demo-scene default, which is tuned for a close-up camera rather than
    // this game's pulled-back combat framing.
    [SerializeField] private float hitImpactVfxScale = 1.6f;

    private const string AttackTriggerParam = "Attack";
    private const string AttackUltimateParam = "AttackUltimate";
    // Holds Attack03 open for the whole barrage - its exit is gated on this rather than on
    // the clip's own 0.333s length (see the Attack03 -> Idle transition in the controller).
    private const string UltimateActiveParam = "UltimateActive";
    private float ultimateChargeTimer;

    private bool animatorSearched;
    private Animator characterAnimator;
    // Not every monster model ships with a rig/AnimatorController (some are still raw
    // static imports) - models without one fall back to the coded lunge/shrink below.
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

    // Used for pacing the coded lunge fallback, and as the impact-delay fallback for
    // monsters with neither a WeaponSwing nor a WeaponSwing-reported delay to borrow.
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

    // Halts the attack loop without destroying the monster - used when the player dies,
    // so the monster just stands there for the death animation instead of vanishing
    // mid-fight or continuing to swing at a player that's already lying on the ground.
    public void StopAttacking()
    {
        StopAllCoroutines();
    }

    public void PlayAttackAnimation()
    {
        CharacterAnimator?.SetTrigger(AttackTriggerParam);
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
        // Bigger monsters need to stop farther away, or their scaled-up body visually
        // overlaps/buries the player before the capsules are actually touching.
        stoppingDistance = stopDistance * transform.localScale.x;
        HasArrived = false;
        CharacterAnimator?.SetBool("IsMoving", true);
    }

    private void Update()
    {
        if (HasArrived || moveTarget == null) return;

        Vector3 toTarget = moveTarget.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= stoppingDistance)
        {
            HasArrived = true;
            CharacterAnimator?.SetBool("IsMoving", false);

            // Elite and Boss both fight as parry duels: they telegraph, then strike.
            // Normal monsters just trade hits on a fixed timer.
            if (!bossLoopStarted && (Type == MonsterType.Boss || Type == MonsterType.Elite))
            {
                bossLoopStarted = true;
                StartCoroutine(TelegraphAttackLoop());

                // Boss-only: the elite shares the telegraph duel above, but not the
                // gauge-charged heavy hit - that stays the boss encounter's own escalation.
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

    // Fallback attack flourish for models with neither a rig/Animator nor a WeaponSwing
    // sword to swing - a quick forward-and-back lunge toward the player.
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

    // Fallback death flourish for models with no Animator/Die state: shrink away instead
    // of just popping out of existence.
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

    // Elite/Boss: charge for a random stretch, and the charge clip's own landing beat
    // (it jumps then slams back down) is the hit - no separate strike clip. A parry
    // landed any time before that negates it.
    private IEnumerator TelegraphAttackLoop()
    {
        CharacterAnimator?.SetBool(UseTelegraphParam, true);

        float windUpLength = ResolveClipLength(windUpClipName, windUpClipFallbackLength);
        float attack2Length = ResolveClipLength(attack2ClipName, attack2ClipFallbackLength);

        while (!IsDead)
        {
            // Checked at the top of every cycle rather than the middle of one, so this can
            // only ever replace a swing that hasn't started yet - never cut off a strike
            // already in flight.
            if (ultimateChargeTimer >= ultimateChargeDuration)
            {
                yield return PlayUltimateAttack();
                ultimateChargeTimer = 0f;
                // Same spacing the other two patterns get below - this branch used to
                // `continue` straight past it, so an ultimate ran into the next charge
                // with no gap at all.
                if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
                continue;
            }

            Animator animator = CharacterAnimator;

            // Only the Boss has a second pattern to mix in - Elite always telegraphs the
            // same way. Each swing rolls independently, so the boss can repeat a pattern.
            bool usePattern2 = Type == MonsterType.Boss && Random.value < 0.5f;
            animator?.SetBool(AttackPattern2Param, usePattern2);

            string activeState;
            float activeLength;
            float impactTime;

            if (usePattern2)
            {
                // No charge, no randomized delay - just the untouched clip at its own
                // pace, with the hit landing whenever the spear actually connects.
                activeState = attack2StateName;
                activeLength = attack2Length;
                impactTime = attack2ImpactTime;
                animator?.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();
            }
            else
            {
                // No separate strike clip - the windup is stretched so its own landing
                // beat (windUpImpactFraction) lands right at timeToImpact, and the hit
                // fires there instead of handing off to a second animation.
                activeState = windUpStateName;
                activeLength = windUpLength;
                impactTime = windUpLength * windUpImpactFraction;

                float timeToImpact = Random.Range(minTimeToImpact, maxTimeToImpact);
                // Only the slice from the entry offset to the impact beat actually plays,
                // so the charge is stretched against that slice - scaling by the full
                // clip length instead would land the hit early by the skipped portion.
                float playedFraction = Mathf.Max(0.05f, windUpImpactFraction - windUpEntryOffset);
                float chargeDuration = Mathf.Max(0.05f, timeToImpact / playedFraction);

                animator?.SetFloat(WindUpSpeedParam, windUpLength / chargeDuration);
                animator?.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();
            }

            if (animator != null)
            {
                // Follow the animator instead of a stopwatch. The trigger isn't consumed
                // until the machine is back in Idle, and the state blends add their own
                // slop, so a precomputed schedule lands the damage before the spear
                // visually arrives.
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

            // Separates one attack's landing sweep from the next one's entry, so two
            // charges in a row don't blur into a single double-swing.
            if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
        }
    }

    // Runs until the strike state has played far enough for the spear to reach the player.
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

    // Holds until the machine is genuinely idle again, so the next Attack trigger is
    // picked up immediately instead of queueing mid-strike.
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

    // Runs independently of the attack loop's own pacing so a long swing (or its charge)
    // never delays the gauge - it only gates *when* the next loop iteration is allowed to
    // fire the ultimate instead of a normal swing.
    private IEnumerator UltimateChargeLoop()
    {
        while (!IsDead)
        {
            ultimateChargeTimer = Mathf.Min(ultimateChargeTimer + Time.deltaTime, ultimateChargeDuration);
            ultimateGaugeView?.SetFraction(ultimateChargeTimer / ultimateChargeDuration);
            yield return null;
        }
    }

    // Heavy unparryable attack. With a laser prefab wired it becomes a sustained barrage:
    // Attack03 (looping auto-fire) is held for ultimateBarrageDuration while a ring of laser
    // strikes walks around the ground behind the player, each one landing its own share of
    // the damage - the player is shot through repeatedly rather than taking a single hit.
    // Without a prefab it falls back to the original one-hit-at-the-end behaviour.
    private IEnumerator PlayUltimateAttack()
    {
        Animator animator = CharacterAnimator;
        bool barrage = ultimateLaserPrefab != null;

        if (animator != null)
        {
            if (barrage) animator.SetBool(UltimateActiveParam, true);
            // A regular swing's trigger can still be sitting unconsumed here; left set it
            // fires a stray attack the moment the barrage releases the machine.
            animator.ResetTrigger(AttackTriggerParam);
            animator.SetTrigger(AttackUltimateParam);

            // The trigger isn't consumed until the animator's own update pass, so the
            // current state can still read as Idle/Run for a frame - wait for the machine
            // to actually enter Attack03 before waiting for it to leave again, or this
            // would fall straight through WaitUntilIdle without the attack ever playing.
            yield return WaitForStateToStart(animator, ultimateStateName);
        }
        else if (!barrage)
        {
            yield return new WaitForSeconds(ultimateClipFallbackLength);
        }

        if (barrage)
        {
            yield return FireUltimateLaserRing();

            // Released before the idle wait, since the transition out of Attack03 is what
            // this bool gates - leaving it set would strand the boss in its firing pose.
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

    // Walks the ring one strike at a time so it sweeps around the player instead of all
    // landing together. The ultimate's total damage is unchanged - it's just split across
    // the strikes, so each one reads as a single bullet punching through.
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
            // Reuses the same on-player burst every other landed hit uses, so each pierce
            // registers on the player themselves and not only as a circle on the ground.
            SpawnHitImpactVfx();
            // Deliberately not routed through ParryManager - the ultimate has always been
            // unparryable, and a barrage the player could cancel with one parry would be
            // weaker than the single hit it replaced.
            Player.TakeDamage(damagePerHit);

            yield return new WaitForSeconds(interval);
        }
    }

    // Single ground circle, held under the player for the whole barrage, ticking a flat
    // amount of damage on a fixed clock rather than walking a ring of one-shot strikes.
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
            // Deliberately not routed through ParryManager - see FireUltimateLaserRing.
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

        // "Behind" is measured along the line this monster is attacking down, so the ring
        // frames the player from their back instead of sitting between the two of them.
        Vector3 away = target.position - transform.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

        Vector3 center = target.position + away * ultimateLaserBehindDistance;

        float angle = index / (float)count * Mathf.PI * 2f;
        Vector3 spawn = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ultimateLaserRingRadius;

        // The effect's tech-circle sits at its own origin, so that origin has to land on the
        // ground - the player's transform is the capsule's centre, not their feet.
        Collider playerCollider = Player.GetComponent<Collider>();
        spawn.y = playerCollider != null ? playerCollider.bounds.min.y : target.position.y;

        SpawnUltimateLaserAt(spawn);
    }

    private void SpawnUltimateLaserAt(Vector3 spawn)
    {
        if (ultimateLaserPrefab == null) return;

        GameObject vfx = Instantiate(ultimateLaserPrefab, spawn, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * ultimateLaserScale;

        // Every one of Laser AOE's 11 systems ships looping (built to be left running in its
        // demo scene) - left alone each spawned ring would fire forever.
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

    // Spawned fresh each time rather than kept as a persistent child - unlike the player's
    // own hit VFX, several monsters could land this on the same player, and a shared child
    // object would have them fighting over one Play()/Stop() instead of layering cleanly.
    private void SpawnUltimateImpactVfx()
    {
        if (ultimateImpactVfxPrefab == null || Player == null) return;

        // The effect's crater/burst sits at its own origin, so that origin has to land on
        // the ground. The player's transform is the capsule's centre, not its feet, so
        // spawning straight on it detonated the crystals at waist height - they read as
        // bursting before they had finished falling.
        Vector3 impactPoint = Player.transform.position;
        Collider playerCollider = Player.GetComponent<Collider>();
        if (playerCollider != null) impactPoint.y = playerCollider.bounds.min.y;

        GameObject vfx = Instantiate(ultimateImpactVfxPrefab, impactPoint, Quaternion.identity);

        // The Hovl effects ship as looping systems (built to be left running in their demo
        // scene), so a single cast would restart the whole crash-down partway through its
        // own destroy timer and read as the effect firing twice. One cast = one cycle.
        float speed = Mathf.Max(0.01f, ultimateImpactVfxSpeed);
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            // Per-system, so every sub-emitter (crystals, flash, sparks, smoke) has to be
            // set - one left at 1x would trail behind the rest of the effect.
            main.simulationSpeed = speed;
        }

        // The effect now finishes in a fraction of its authored runtime, so the cleanup
        // timer has to shrink with it or the emptied object lingers.
        Destroy(vfx, ultimateImpactVfxLifetime / speed);
    }

    // Fired alongside every landed attack (regular swing, telegraphed strike, and ultimate
    // alike) - unlike SpawnUltimateImpactVfx this lands on the player's centre, not their
    // feet, since it's meant to read as the blow connecting rather than a crater underfoot.
    private void SpawnHitImpactVfx()
    {
        if (hitImpactVfxPrefab == null || Player == null) return;

        GameObject vfx = Instantiate(hitImpactVfxPrefab, Player.transform.position, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * hitImpactVfxScale;

        float speed = Mathf.Max(0.01f, hitImpactVfxSpeed);

        // Ships as a looping demo-scene system like the ultimate's own impact prefab - one
        // hit should read as one burst, not a system left running behind it.
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            // Per-system, same as the ultimate impact: one sub-emitter left at 1x would
            // finish ahead of the rest and break the single-burst read.
            main.simulationSpeed = speed;
        }

        // Slowing the effect down stretches its real runtime, so the cleanup timer has to
        // grow with it or the object is destroyed mid-burst.
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

    // Normal-only: attacks on its own fixed timer, independent of the player's attack
    // tick, so a lethal player hit can never preempt this monster's own swing.
    private IEnumerator NormalAttackLoop()
    {
        // Normal monsters play the untouched original clip - no charge, no split.
        CharacterAnimator?.SetBool(UseTelegraphParam, false);

        // Squeeze it into one attack interval, otherwise the next attack retriggers the
        // clip partway through and it never finishes - the monster just twitches.
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
                CharacterAnimator.SetTrigger("Die");
                Destroy(gameObject, deathAnimDuration);
            }
            else
            {
                StartCoroutine(ShrinkAndDestroy());
            }
        }
    }
}
