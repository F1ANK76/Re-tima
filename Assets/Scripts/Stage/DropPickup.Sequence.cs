using System.Collections;
using UnityEngine;

// DropPickup의 세 박자: 몬스터가 죽은 자리에서 튀어 오르고(1), 플레이어 쪽으로 딸려오다가(2),
// 닿으면 효과를 지급하고 사라진다(3). 매 프레임 위치를 만지는 코드는 전부 여기 모여 있고,
// 무엇을 지급하는지(ApplyEffect)는 DropPickup.cs가 정한다.
public partial class DropPickup
{
    private IEnumerator TossThenRunOver()
    {
        yield return PlayToss();

        // 2번 박자: 던지는 동안 주도권을 갖고 있던 idle hold를 풀면, 플레이어는 곧바로
        // CombatLoop의 기본 상태인 "범위 내에 아무것도 없으면 계속 이동"으로 돌아간다.
        if (combatLoop != null) combatLoop.PopIdleHold();
        idleHoldActive = false;

        yield return new WaitForSeconds(settleHoldDuration);
        yield return PlayRunOver();

        Collect();
    }

    // 플레이어로부터 멀어지는 포물선을 그리며 튕겨서 정지하기까지, 전부
    // landDuration 안에 이루어진다.
    private IEnumerator PlayToss()
    {
        Vector3 startPos = transform.position;
        float groundY = ResolveGroundY() + restBottomOffset;

        // 플레이어에서 아이템으로 이어지는 직선을 따라 곧장 뒤쪽으로 - 몬스터가
        // 어디서 죽든 아이템은 항상 옆이 아니라 그 너머 쪽에 떨어지게 된다.
        Vector3 away = Vector3.right;
        if (player != null)
        {
            Vector3 delta = startPos - player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f) away = delta.normalized;
        }

        Vector3 landingPos = startPos + away * tossDistance;
        landingPos.y = groundY;

        transform.localScale = Vector3.zero;

        float t = 0f;
        while (t < landDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / landDuration);

            // 전체 시퀀스가 아니라 첫 포물선 궤적 동안 완전한 크기로 커진다 - 처음
            // 바닥에 닿을 때는 이미 완전히 형성되어 있어야 한다.
            transform.localScale = restScale * Mathf.Clamp01(p / 0.3f);

            // 감속: 대부분의 거리는 첫 번째 도약에서 이동하고, 이후 재반동들은 거의
            // 앞으로 나아가지 않는다 - 이것이 던져진 물체가 속도를 잃어가는 모습이다.
            Vector3 flat = Vector3.Lerp(startPos, landingPos, Easing.OutQuad(p));
            flat.y = Mathf.Lerp(startPos.y, groundY, Easing.OutQuad(p)) + tossHeight * HopHeight(p);
            transform.position = flat;

            yield return null;
        }

        transform.localScale = restScale;
        transform.position = landingPos;
    }

    // 지면에 붙은 등속 접근 - 달리는 플레이어를 스쳐 지나가는 세계 같은 느낌. 정해진 시간이
    // 아니라 아이템이 발밑에 올 때까지 돌므로, 실제 이동해야 할 거리가 소요 시간을 결정한다.
    private IEnumerator PlayRunOver()
    {
        float restY = transform.position.y;
        float elapsed = 0f;

        while (elapsed < approachTimeout)
        {
            elapsed += Time.deltaTime;
            if (player == null) yield break;

            Vector3 pos = transform.position;

            // 평면화: 아이템은 바닥 평면상에서 플레이어의 위치를 추적하며 이동하는 내내
            // 자기 안착 높이를 유지한다. 그래서 떠오르지 않고 미끄러진다.
            Vector3 toPlayer = player.position - pos;
            toPlayer.y = 0f;

            if (toPlayer.magnitude <= pickupRadius) yield break;

            pos += toPlayer.normalized * approachSpeed * Time.deltaTime;
            pos.y = restY;
            transform.position = pos;

            yield return null;
        }
    }

    // 이 게임의 지면 라인 기준은 플레이어 콜라이더의 바닥이다(Monster.SpawnUltimateImpactVfx도
    // 동일) - 레이캐스트할 바닥 콜라이더가 따로 없다. 콜라이더가 없으면 스폰 높이로 대체해
    // 던지기 동작이 이상해지지 않게 한다.
    private float ResolveGroundY()
    {
        if (player == null) return transform.position.y;

        Collider playerCollider = player.GetComponent<Collider>();
        return playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
    }

    // 도약 시퀀스 전체에 걸친 정규화된 높이: 첫 포물선 정점에서 1, 매 지면
    // 접촉 시와 정지 상태에서 0.
    private static float HopHeight(float p)
    {
        float cursor = 0f;

        for (int i = 0; i < HopDurations.Length; i++)
        {
            float span = HopDurations[i];
            if (p < cursor + span || i == HopDurations.Length - 1)
            {
                float local = Mathf.Clamp01((p - cursor) / span);
                return HopHeights[i] * Mathf.Sin(local * Mathf.PI);
            }
            cursor += span;
        }

        return 0f;
    }

    private void Collect()
    {
        ApplyEffect();
        Destroy(gameObject);
    }
}
