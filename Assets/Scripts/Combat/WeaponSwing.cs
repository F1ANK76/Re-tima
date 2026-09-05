using System.Collections;
using UnityEngine;

public class WeaponSwing : MonoBehaviour
{
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingDuration = 0.15f;

    // 선택 사항 - 플레이어의 검에만 연결되어 있고, 몬스터는 이것 없이 스윙한다.
    [SerializeField] private ParticleSystem slashVfx;
    [SerializeField] private float slashVfxHoldDuration = 0.5f;

    [SerializeField] private string attackClipName = "Attack01_SwordAndShiled";
    [SerializeField] private float attackClipLengthFallback = 16f / 30f;

    private const float AttackImpactFraction = 0.356f;
    public float AttackImpactDelay => AnimClipTiming.ResolveClipTime(
        PlayerAnimator, attackClipName, attackClipLengthFallback, AttackImpactFraction);

    private const float AttackToIdleExitTime = 0.9f;
    private const float IdleBlendDuration = 0.15f;

    private float AttackAnimSettleDuration => AttackToIdleExitTime * ResolveClipLength(attackClipName, attackClipLengthFallback) + IdleBlendDuration;

    private float nextAnimTriggerAllowedTime;

    private Quaternion restRotation;
    private Coroutine activeSwing;
    private Coroutine slashVfxRoutine;

    private bool animatorSearched;
    private Animator playerAnimator;

    public Animator PlayerAnimator
    {
        get
        {
            if (!animatorSearched)
            {
                animatorSearched = true;
                playerAnimator = transform.parent.GetComponentInChildren<Animator>();
            }
            return playerAnimator;
        }
    }

    private void Awake()
    {
        restRotation = transform.localRotation;
    }

    private float ResolveClipLength(string clipName, float fallback)
        => AnimClipTiming.ResolveClipLength(PlayerAnimator, clipName, fallback);

    public void PlaySwing()
    {
        if (activeSwing != null) StopCoroutine(activeSwing);
        activeSwing = StartCoroutine(SwingRoutine());

        if (slashVfx != null)
        {
            if (slashVfxRoutine != null) StopCoroutine(slashVfxRoutine);
            slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slashVfxRoutine = StartCoroutine(PlaySlashVfxAtImpact());
        }

        if (Time.time >= nextAnimTriggerAllowedTime)
        {
            PlayerAnimator.SetTrigger(AnimParams.Attack);
            nextAnimTriggerAllowedTime = Time.time + AttackAnimSettleDuration;
        }
    }

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

    private IEnumerator PlaySlashVfxAtImpact()
    {
        yield return new WaitForSeconds(AttackImpactDelay);
        slashVfx.Play(true);

        yield return new WaitForSeconds(slashVfxHoldDuration);
        slashVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        slashVfxRoutine = null;
    }

    private IEnumerator SwingRoutine()
    {
        Quaternion startRotation = transform.localRotation;

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
