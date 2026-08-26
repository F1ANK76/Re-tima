using System.Collections;
using UnityEngine;

public class WeaponSwing : MonoBehaviour
{
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingDuration = 0.15f;

    // Optional - only the player's sword has one wired up; monsters swing without it.
    [SerializeField] private ParticleSystem slashVfx;

    // How long the attack animation takes to land its hit. Matches Attack01_SwordAndShiled's
    // actual clip length (frames 0-16 at 30fps, per its FBX import data) - tune in the
    // Inspector if the attack clip changes.
    [SerializeField] private float attackImpactDelay = 16f / 30f;
    public float AttackImpactDelay => attackImpactDelay;

    // Total time for Attack01 to finish AND blend back into Idle: the Attack01 -> Idle
    // transition in SwordAndShieldStance.controller starts at 90% of the clip and blends
    // over 0.15s, so re-triggering before this elapses would cut the current swing off
    // mid-animation. Retriggers that come in faster than this are skipped visually - the
    // gameplay attack tick/damage timing is untouched.
    [SerializeField] private float attackAnimSettleDuration = 0.9f * (16f / 30f) + 0.15f;
    private float nextAnimTriggerAllowedTime;

    private Quaternion restRotation;
    private Coroutine activeSwing;
    private Coroutine slashVfxRoutine;

    private bool animatorSearched;
    private Animator characterAnimator;

    // The character model (with its own Animator) lives as a sibling under the same
    // parent as this swing pivot, not as an ancestor - so we search sideways via the
    // parent rather than GetComponentInParent.
    public Animator CharacterAnimator
    {
        get
        {
            if (!animatorSearched)
            {
                animatorSearched = true;
                characterAnimator = transform.parent != null ? transform.parent.GetComponentInChildren<Animator>() : null;
            }
            return characterAnimator;
        }
    }

    private void Awake()
    {
        restRotation = transform.localRotation;
    }

    public void PlaySwing()
    {
        if (activeSwing != null) StopCoroutine(activeSwing);
        activeSwing = StartCoroutine(SwingRoutine());

        if (slashVfx != null)
        {
            // Cut off short so a fresh swing always starts its own clean burst instead of
            // layering on top of whatever the interrupted swing's slash was still playing.
            if (slashVfxRoutine != null) StopCoroutine(slashVfxRoutine);
            slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slashVfxRoutine = StartCoroutine(PlaySlashVfxAtImpact());
        }

        if (CharacterAnimator != null && Time.time >= nextAnimTriggerAllowedTime)
        {
            CharacterAnimator.SetTrigger("Attack");
            nextAnimTriggerAllowedTime = Time.time + attackAnimSettleDuration;
        }
    }

    // Called when something else (a parry) needs to cut the current swing short instead
    // of letting it play out - snaps the blade back to rest and clears the slash immediately
    // rather than waiting for it to reach its own scheduled impact/settle timing.
    public void CancelSwing()
    {
        if (activeSwing != null)
        {
            StopCoroutine(activeSwing);
            activeSwing = null;
        }
        transform.localRotation = restRotation;

        if (slashVfxRoutine != null)
        {
            StopCoroutine(slashVfxRoutine);
            slashVfxRoutine = null;
        }
        if (slashVfx != null) slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // Timed off the same clip data as the animation itself (not the blade prop's own
    // quick rotation) - the slash shows right as the blade actually lands, and clears
    // the instant the attack animation finishes settling back to Idle.
    private IEnumerator PlaySlashVfxAtImpact()
    {
        yield return new WaitForSeconds(attackImpactDelay);
        slashVfx.Play(true);

        yield return new WaitForSeconds(attackAnimSettleDuration - attackImpactDelay);
        slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        slashVfxRoutine = null;
    }

    private IEnumerator SwingRoutine()
    {
        // Start from the blade's current rotation, not restRotation, so an attack
        // that interrupts a still-playing swing continues smoothly instead of snapping back first.
        Quaternion startRotation = transform.localRotation;

        // Swing around the pivot's LOCAL X axis. Both the player and the monsters have their
        // local X running along the camera's view axis (in opposite world directions, since they
        // face each other), so this sweeps the blade across the screen plane where it stays fully
        // visible - and the same angle automatically produces a mirrored swing for each side.
        // Rotating around local Z instead swept the blade toward/away from the camera, where it
        // foreshortened into a stub and looked like it vanished mid-attack.
        Quaternion swungRotation = restRotation * Quaternion.Euler(swingAngle, 0f, 0f);
        float half = swingDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRotation, swungRotation, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(swungRotation, restRotation, t / half);
            yield return null;
        }

        transform.localRotation = restRotation;
        activeSwing = null;
    }
}
