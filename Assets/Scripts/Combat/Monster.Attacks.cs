using System.Collections;
using UnityEngine;

// Monster의 공격 시퀀스 - 텔레그래프(Elite/Boss)와 일반 몹 루프, 그리고 궁극기. 상태/필드는
// 전부 Monster.cs가 들고 있고 여기는 그 위에서 도는 코루틴들만 모았다. partial로 나눈 이유는
// 클래스를 쪼개면 프리팹 6개의 컴포넌트 참조가 끊기기 때문이다 - 타입은 그대로 하나다.
public partial class Monster
{
    // 엘리트/보스: 무작위 길이만큼 충전하고, 별도의 타격 클립 없이 충전 클립 자체의 착지
    // 비트(뛰어올랐다 쿵 내려찍는 순간)가 곧 타격이다. 그 전 어느 시점의 패링으로든 무효화.
    private IEnumerator TelegraphAttackLoop()
    {
        // 이 while 루프 안 어디선가(ParryManager/Player 쪽 호출 등) 예외가 나면 코루틴은
        // StopAttacking()을 거치지 않고 조용히 죽는다 - bossLoopStarted는 true로 남고,
        // Update()의 자가복구는 그 플래그가 false일 때만 재시작하므로 몬스터가 영원히 다시
        // 공격하지 못하게 된다. try/finally로 감싸서 "죽었으면(정상 사망 제외) 무조건
        // 플래그를 되돌린다"를 보장한다.
        try
        {
        MonsterAnimator.SetBool(UseTelegraphParam, true);

        float windUpLength = ResolveClipLength(windUpClipName, windUpClipFallbackLength);
        float attack2Length = ResolveClipLength(attack2ClipName, attack2ClipFallbackLength);

        while (!IsDead)
        {
            // 사이클 중간이 아니라 맨 처음에만 확인하므로, 아직 시작하지 않은 스윙만
            // 대체할 수 있고 이미 진행 중인 타격을 끊는 일은 절대 없다.
            if (ultimateChargeTimer >= ultimateChargeDuration)
            {
                yield return PlayUltimateAttack();
                ultimateChargeTimer = 0f;
                // 아래 다른 두 패턴과 같은 간격 - 예전엔 이걸 건너뛰고 바로 `continue`해서
                // 궁극기가 아무런 틈도 없이 곧바로 다음 충전으로 이어졌다.
                if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
                continue;
            }

            Animator animator = MonsterAnimator;

            // 두 번째 패턴을 섞어 쓰는 건 보스뿐(엘리트는 항상 같은 방식으로 예고). 매 스윙
            // 독립적으로 뽑으므로 보스는 같은 패턴을 연달아 낼 수도 있다.
            bool usePattern2 = Type == MonsterType.Boss && Random.value < 0.5f;
            animator.SetBool(AttackPattern2Param, usePattern2);

            string activeState;
            float activeLength;
            float impactTime;

            if (usePattern2)
            {
                // 충전도, 무작위 지연도 없다 - 그저 손대지 않은 클립을 원래 속도로
                // 재생할 뿐이며, 타격은 창이 실제로 닿는 순간에 적중한다.
                activeState = attack2StateName;
                activeLength = attack2Length;
                impactTime = attack2ImpactTime;
                animator.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();
            }
            else
            {
                // 차징 구간(진입~windUpChargeEndFraction)만 minTimeToImpact~maxTimeToImpact
                // 사이 무작위 길이로 늘어나거나 줄어든다. 그 뒤 실제 스윙 구간(~임팩트)은 이
                // 차징 시간과 무관하게 항상 원래 속도로, 원래 길이 그대로 재생된다 - 아래
                // RestoreWindUpSpeedAfterCharge가 그 지점에서 속도를 다시 1배로 되돌려준다.
                // 그래서 공격이 시작되어 타격이 적중하기까지의 총 시간은 "차징(가변) + 스윙(고정)"이다.
                activeState = windUpStateName;
                activeLength = windUpLength;
                impactTime = windUpLength * windUpImpactFraction;

                float chargeDuration = Mathf.Max(0.05f, Random.Range(minTimeToImpact, maxTimeToImpact));
                // 차징 구간(진입 오프셋~chargeEndFraction)의 실제 재생 길이 - 이 구간만 충전
                // 속도의 대상이다.
                float chargeSpanFraction = Mathf.Max(0.05f, windUpChargeEndFraction - windUpEntryOffset);

                animator.SetFloat(WindUpSpeedParam, chargeSpanFraction * windUpLength / chargeDuration);
                animator.SetFloat(AttackSpeedParam, 1f);
                PlayAttackAnimation();

                StartCoroutine(RestoreWindUpSpeedAfterCharge(animator));
            }

            // 스톱워치 대신 애니메이터를 따라간다. 트리거는 머신이 다시 Idle로 돌아갈
            // 때까지 소비되지 않고 스테이트 블렌딩도 오차를 더해서, 미리 계산해둔
            // 스케줄로는 창이 시각적으로 도착하기 전에 데미지가 먼저 들어간다.
            yield return WaitForStrikeImpact(animator, activeState, activeLength, impactTime);

            if (IsDead) yield break;

            bool parried = ParryManager.Instance != null && ParryManager.Instance.TryConsumeParry();

            if (!parried && Player != null)
            {
                SpawnHitImpactVfx();
                Player.TakeDamage(AttackPower);
            }

            yield return WaitUntilIdle(animator);

            // 한 공격의 착지 스윙과 다음 공격의 진입 동작을 분리해줘서, 연달아 두 번
            // 충전하는 게 하나의 이중 스윙처럼 뭉개져 보이지 않게 한다.
            if (postAttackPause > 0f) yield return new WaitForSeconds(postAttackPause);
        }
        }
        finally
        {
            // 정상적으로 죽어서 루프를 빠져나온 거라면 true로 둔다 - 죽음 애니메이션이
            // 끝나 실제로 파괴되기 전까지 남은 프레임 동안 Update()가 다시 살아나 새
            // 루프를 도는 낭비를 막는다.
            if (!IsDead) bossLoopStarted = false;
        }
    }

    // 차징 구간이 끝나는 지점(windUpChargeEndFraction)까지 기다렸다가 윈드업 재생 속도를 원래
    // 속도(1배)로 되돌린다 - 그 뒤에 이어지는 실제 스윙 동작은 차징 시간과 무관하게 항상
    // 자연스러운 속도로 보이게 하기 위함.
    private IEnumerator RestoreWindUpSpeedAfterCharge(Animator animator)
    {
        const float SafetyTimeout = 5f;
        float elapsed = 0f;

        // SetTrigger 직후엔 애니메이터가 아직 실제로 AttackWindUp으로 전환되기 전이다(최소 한
        // 프레임 필요) - 그 전에 아래 루프를 바로 돌리면 "아직 Idle이니 이미 벗어났다"고
        // 착각해서 차징 속도를 세팅하자마자 곧바로 1배로 되돌려버린다. 실제로 그 상태에
        // 진입할 때까지 먼저 기다린다.
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

    // 창이 플레이어에게 닿을 만큼 타격 스테이트가 충분히 재생될 때까지 실행된다.
    //
    // "임팩트 지점을 지났다"는 판정은 두 가지로 잡는다. normalizedTime이 임팩트 비율을 넘긴
    // 프레임을 직접 보는 게 정상 경로지만, 그것만으로는 부족하다: AttackWindUp은 0.3초짜리
    // 클립인데 임팩트(0.92)부터 이탈(전이 exitTime 0.95)까지가 클립의 3%, 약 9ms라서 60fps
    // 기준 한 프레임(약 17ms)보다도 좁다. 그래서 그 사이에 프레임이 안 떨어지면 조건이 영영
    // 참이 되지 않고 아래 8초 타임아웃까지 흘러가, 몬스터가 그동안 Idle로 가만히 서 있다가
    // 뒤늦게 엉뚱한 타이밍에 데미지를 넣는다(실측: 5번 중 4번꼴로 놓침).
    //
    // 그래서 "그 스테이트에 들어갔다가 이미 빠져나왔다"도 임팩트 통과로 함께 인정한다 -
    // 스테이트를 벗어났다는 건 끝(exitTime 0.95)까지 재생됐다는 뜻이고, 그건 임팩트 지점을
    // 확실히 지난 것이다. 이 경우 판정이 최대 한 프레임 늦어질 뿐이라 눈에 띄지 않는다.
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

    // 머신이 진짜로 다시 idle 상태가 될 때까지 대기해서, 다음 Attack 트리거가 타격
    // 도중에 큐잉되지 않고 즉시 반영되도록 한다.
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

    // 공격 루프의 진행 속도와 독립적으로 돌아서 긴 스윙(또는 그 충전)이 게이지를 지연시키지
    // 않는다 - 이게 정하는 건 다음 루프 반복에서 궁극기가 일반 스윙을 대체할 시점뿐이다.
    private IEnumerator UltimateChargeLoop()
    {
        while (!IsDead)
        {
            ultimateChargeTimer = Mathf.Min(ultimateChargeTimer + Time.deltaTime, ultimateChargeDuration);
            ultimateGaugeView?.SetFraction(ultimateChargeTimer / ultimateChargeDuration);
            yield return null;
        }
    }

    // 무겁고 패링 불가능한 공격. 레이저 프리팹이 연결되어 있으면 지속 탄막이 된다:
    // Attack03(반복 자동 발사)이 ultimateBarrageDuration 동안 유지되는 사이, 플레이어 뒤쪽
    // 지면의 레이저 고리가 순서대로 이동하며 각각 자기 몫의 데미지를 준다 - 한 번의 타격이
    // 아니라 여러 번 관통당하는 셈. 프리팹이 없으면 원래의 클립 끝 한 방 타격으로 대체된다.
    private IEnumerator PlayUltimateAttack()
    {
        Animator animator = MonsterAnimator;
        bool barrage = ultimateLaserPrefab != null;

        if (barrage) animator.SetBool(UltimateActiveParam, true);
            // 여기서 일반 스윙의 트리거가 아직 소비되지 않은 채 남아있을 수 있다;
            // 그대로 세팅된 채 두면 탄막이 머신을 놓아주는 순간 엉뚱한 공격이 발동한다.
            animator.ResetTrigger(AnimParams.Attack);
            animator.SetTrigger(AttackUltimateParam);

            // 트리거는 애니메이터의 업데이트 패스가 돌기 전까지 소비되지 않아 현재 상태가
            // 한 프레임 동안 여전히 Idle/Run으로 읽힐 수 있다 - Attack03을 벗어나기를
            // 기다리기 전에 실제 진입부터 기다려야, 공격이 재생되지도 않은 채로
            // WaitUntilIdle을 바로 통과해버리는 일이 없다.
        yield return WaitForStateToStart(animator, ultimateStateName);

        if (barrage)
        {
            yield return FireUltimateLaserRing();

            // 자연스러운 전환(WaitUntilIdle)을 기다리지 않고 바로 Idle로 끊는다 - Attack03은
            // 반복 재생이라 bool만 끄고 기다리면 다음 루프 사이클이 끝날 때까지 총 쏘는 포즈가
            // 빛줄기보다 더 오래 남아있었다. VFX/데미지가 끝나는 바로 그 프레임에 애니메이션도
            // 같이 끊어야 둘이 어긋나지 않는다.
            animator.SetBool(UltimateActiveParam, false);
            animator.Play(idleStateName, 0, 0f);
            yield break;
        }

        yield return WaitUntilIdle(animator);

        if (IsDead) yield break;

        if (Player != null)
        {
            SpawnUltimateImpactVfx();
            SpawnHitImpactVfx();
            Player.TakeDamage(AttackPower * ultimateDamageMultiplier);
        }
    }

    // VFX는 플레이어 발밑에 딱 한 번만 스폰한다 - 이펙트 자체가 알아서 빛줄기를 여러 번
    // 떨어뜨리는 것처럼 보이고, 코드는 그 위에 ultimateLaserCount번만 데미지 틱을 맞춰
    // 넣는다. 궁극기 총 데미지는 그대로고 틱들에 나눠질 뿐이라, 각각이 한 발씩 박히는 느낌.
    private IEnumerator FireUltimateLaserRing()
    {
        if (Player == null) yield break;

        int count = Mathf.Max(1, ultimateLaserCount);
        SpawnUltimateLaserUnderPlayer();

        float interval = Mathf.Max(0f, ultimateBarrageDuration) / count;
        float damagePerHit = AttackPower * ultimateDamageMultiplier / count;

        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(interval);

            if (IsDead) yield break;
            if (Player == null) yield break;

            SpawnHitImpactVfx();
            // 의도적으로 ParryManager를 거치지 않는다 - 궁극기는 원래부터 패링 불가였고,
            // 패링 한 번에 취소되는 탄막이라면 그것이 대체한 단일 타격보다 약해진다.
            Player.TakeDamage(damagePerHit);
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

    private void SpawnUltimateLaserAt(Vector3 spawn)
    {
        if (ultimateLaserPrefab == null) return;

        GameObject vfx = Instantiate(ultimateLaserPrefab, spawn, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * ultimateLaserScale;

        // Laser AOE의 11개 시스템은 데모 씬에서 계속 실행되도록 전부 반복 재생이다 -
        // 그대로 두면 스폰된 각 고리가 영원히 발사된다.
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

    // 일반 몬스터 전용: 플레이어의 공격 틱과 무관하게 자기 고정 타이머로 때리므로,
    // 플레이어의 치명타가 이 몬스터의 스윙을 선점하는 일은 절대 없다 - 공격력이 무한이어도
    // 최소 한 대는 맞아야 한다는 규칙이라, 임팩트 타이밍 자체를 GuaranteedFirstStrikeMargin만큼
    // 앞당겨서 강제한다.
    private IEnumerator NormalAttackLoop()
    {
        // TelegraphAttackLoop과 같은 이유의 try/finally - Player.TakeDamage 쪽에서 예외가
        // 나도 normalLoopStarted가 정직하게 풀려서 Update()가 다음 프레임에 다시 살릴 수 있게.
        try
        {
        // 일반 몬스터는 손대지 않은 원본 클립을 그대로 재생한다 - 충전도, 분리도 없다.
        MonsterAnimator.SetBool(UseTelegraphParam, false);

        // 클립을 한 번의 공격 간격 안에 눌러 담는다 - 아니면 다음 공격이 클립이 끝나기도 전에
        // 재트리거해서 클립이 끝까지 못 가고 몬스터가 그냥 씰룩거리기만 한다.
        float clipLength = ResolveClipLength(plainAttackClipName, plainAttackClipFallbackLength);
        if (attackInterval > 0.01f)
        {
            MonsterAnimator.SetFloat(AttackSpeedParam, clipLength / attackInterval);
        }

        while (!IsDead)
        {
            PlayAttackAnimation();

            // 플레이어보다 아주 살짝 먼저 때린다(GuaranteedFirstStrikeMargin 주석 참고).
            float impactDelay = Mathf.Min(attackImpactDelay,
                Mathf.Max(0f, PlayerWeaponSwing.AttackImpactDelay - GuaranteedFirstStrikeMargin));
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
        finally
        {
            if (!IsDead) normalLoopStarted = false;
        }
    }
}
