using System.Collections;
using UnityEngine;

public partial class Monster
{
    private IEnumerator TelegraphAttackLoop()
    {
        try
        {
        MonsterAnimator.SetBool(UseTelegraphParam, true);

        float windUpLength = ResolveClipLength(windUpClipName, windUpClipFallbackLength);
        float attack2Length = ResolveClipLength(attack2ClipName, attack2ClipFallbackLength);

        while (!IsDead)
        {
            if (ultimateChargeTimer >= ultimateChargeDuration)
            {
                yield return PlayUltimateAttack();
                ultimateChargeTimer = 0f;
                if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
                continue;
            }

            Animator animator = MonsterAnimator;

            bool usePattern2 = Type == MonsterType.Boss && Random.value < 0.5f;
            animator.SetBool(AttackPattern2Param, usePattern2);

            string activeState;
            float activeLength;
            float impactTime;

            if (usePattern2)
            {
                activeState = attack2StateName;
                activeLength = attack2Length;
                impactTime = attack2ImpactTime;
                animator.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();
            }
            else
            {
                activeState = windUpStateName;
                activeLength = windUpLength;
                impactTime = windUpLength * windUpImpactFraction;

                float chargeDuration = Mathf.Max(0.05f, Random.Range(minTimeToImpact, maxTimeToImpact));
                float chargeSpanFraction = Mathf.Max(0.05f, windUpChargeEndFraction - windUpEntryOffset);

                animator.SetFloat(WindUpSpeedParam, chargeSpanFraction * windUpLength / chargeDuration);
                animator.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();

                StartCoroutine(RestoreWindUpSpeedAfterCharge(animator));
            }

            yield return WaitForStrikeImpact(animator, activeState, activeLength, impactTime);

            if (IsDead) yield break;

            bool parried = ParryManager.Instance != null && ParryManager.Instance.TryConsumeParry();

            if (!parried) Player.TakeDamage(AttackPower);

            yield return WaitUntilIdle(animator);

            if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
        }
        }
        finally
        {
            if (!IsDead) bossLoopStarted = false;
        }
    }

    private IEnumerator RestoreWindUpSpeedAfterCharge(Animator animator)
    {
        const float SafetyTimeout = 5f;
        float elapsed = 0f;

        while (elapsed < SafetyTimeout && !animator.GetCurrentAnimatorStateInfo(0).IsName(windUpStateName))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        while (elapsed < SafetyTimeout)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(windUpStateName) || state.normalizedTime >= windUpChargeEndFraction) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        animator.SetFloat(WindUpSpeedParam, 1f);
    }

    private IEnumerator WaitForStrikeImpact(Animator animator, string strikeState, float strikeLength, float impactTime)
    {
        const float SafetyTimeout = 8f;
        float elapsed = 0f;
        bool enteredStrikeState = false;

        while (!IsDead && elapsed < SafetyTimeout)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            if (state.IsName(strikeState))
            {
                enteredStrikeState = true;
                if (state.normalizedTime * strikeLength >= impactTime) yield break;
            }
            else if (enteredStrikeState)
            {
                // 프레임 샘플링이 좁은 임팩트 구간을 건너뛴 경우 - 이미 지나갔으므로 바로 종료.
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

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

    private IEnumerator UltimateChargeLoop()
    {
        while (!IsDead)
        {
            ultimateChargeTimer = Mathf.Min(ultimateChargeTimer + Time.deltaTime, ultimateChargeDuration);
            ultimateGaugeView?.SetFraction(ultimateChargeTimer / ultimateChargeDuration);
            yield return null;
        }
    }

    // 무겁고 패링 불가능한 공격 - 클립 끝에 한 방 무겁게 적중한다.
    private IEnumerator PlayUltimateAttack()
    {
        Animator animator = MonsterAnimator;

        animator.ResetTrigger(AnimParams.Attack);
        animator.SetTrigger(AttackUltimateParam);

        yield return WaitForStateToStart(animator, ultimateStateName);
        yield return WaitUntilIdle(animator);

        if (IsDead) yield break;

        SpawnUltimateImpactVfx();
        Player.TakeDamage(AttackPower * ultimateDamageMultiplier);
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

    private IEnumerator NormalAttackLoop()
    {
        try
        {
        // 일반 몬스터는 손대지 않은 원본 클립을 그대로 재생한다 - 충전도, 분리도 없다.
        MonsterAnimator.SetBool(UseTelegraphParam, false);

        float clipLength = ResolveClipLength(plainAttackClipName, plainAttackClipFallbackLength);
        if (attackInterval > 0.01f)
        {
            MonsterAnimator.SetFloat(AttackSpeedParam, clipLength / attackInterval);
        }

        while (!IsDead)
        {
            PlayAttackAnimation();

            // 칼이 닿는 시점에 데미지. 클립이 압축된 만큼 비율로 환산한다.
            float impactDelay = plainAttackImpactFraction * attackInterval;
            yield return new WaitForSeconds(impactDelay);

            if (IsDead) yield break;

            Player.TakeDamage(AttackPower);

            yield return new WaitForSeconds(Mathf.Max(0f, attackInterval - impactDelay));
        }
        }
        finally
        {
            if (!IsDead) normalLoopStarted = false;
        }
    }
}
