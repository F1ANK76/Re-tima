using System.Collections;
using UnityEngine;

public partial class DropPickup
{
    private IEnumerator TossThenRunOver()
    {
        yield return PlayToss();

        if (combatLoop != null) combatLoop.PopIdleHold();
        idleHoldActive = false;

        yield return new WaitForSeconds(settleHoldDuration);
        yield return PlayRunOver();
    }

    private IEnumerator PlayToss()
    {
        Vector3 startPos = transform.position;
        float groundY = ResolveGroundY() + restBottomOffset;

        Vector3 delta = startPos - player.position;
        delta.y = 0f;

        Vector3 away = Vector3.right;
        if (delta.sqrMagnitude > 0.0001f) away = delta.normalized;

        Vector3 landingPos = startPos + away * tossDistance;
        landingPos.y = groundY;

        transform.localScale = Vector3.zero;

        float t = 0f;
        while (t < landDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / landDuration);

            transform.localScale = restScale * Mathf.Clamp01(p / 0.3f);

            Vector3 flat = Vector3.Lerp(startPos, landingPos, Easing.OutQuad(p));
            flat.y = Mathf.Lerp(startPos.y, groundY, Easing.OutQuad(p)) + tossHeight * HopHeight(p);
            transform.position = flat;

            yield return null;
        }

        transform.localScale = restScale;
        transform.position = landingPos;
    }

    // 플레이어한테 아이템 빨려들어 가게 하기
    private IEnumerator PlayRunOver()
    {
        float restY = transform.position.y;

        while (true)
        {
            Vector3 pos = transform.position;

            Vector3 toPlayer = player.position - pos;
            toPlayer.y = 0f;

            // 아이템이 플레이어 범위 내에 들어오면 수집하는 판정
            if (toPlayer.magnitude <= pickupRadius) break;

            pos += toPlayer.normalized * approachSpeed * Time.deltaTime;
            pos.y = restY;
            transform.position = pos;

            yield return null;
        }

        Collect();
    }

    private float ResolveGroundY()
    {
        Collider playerCollider = player.GetComponent<Collider>();
        return playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
    }

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
