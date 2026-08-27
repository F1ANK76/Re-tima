using System.Collections;
using UnityEngine;

// 스테이지 3의 드롭. StatPotionPickup(스테이지 1), EquipmentDropPickup(스테이지 2)에 이어지는
// 세 번째 구성원으로, 같은 순서의 같은 세 박자(던져 튕기며 안착 → 짧은 정지 → 접촉 시 지급하는
// 평평한 지면 레벨의 달려가서 줍기)를 써서 세 픽업 모두 "플레이어가 그 위를 달려 지나간다"로 읽힌다.
//
// 형제들과 달리 스톤에는 등급이 없다: 드롭되는 크리스탈은 크기까지 전부 동일하고 둘 중 어느
// 쪽이냐만 다르다. 빨강=ATK, 초록=HP로 스테이지 1의 RedVial/GreenVial 색상 언어를 따르므로,
// 아우라도 희귀도가 아니라 크리스탈 색을 표현한다.
//
// 비주얼이 정적 메시가 아니라 Hovl Studio의 Crystal effect 프리팹인 데서 오는 차이: 파티클로
// 그려지므로(루트 시스템이 Crystal1 메시를 렌더링) 스폰 시점엔 살아있는 파티클이 없어 렌더러
// 바운드가 비어있다 - 형제들이 바닥 정렬에 쓰는 바운드 측정 트릭이 0을 반환해서, 대신 미리
// 정해둔 고정 오프셋을 쓴다.
public class StoneDropPickup : MonoBehaviour
{
    [Header("Toss + bounce (beat 1)")]
    // StatPotionPickup과 같은 궤적이지만 물약의 1.4/1.1보다 눈에 띄게 더 멀리·더 높이 던진다 -
    // 스톤은 더 크고 무거운 오브젝트라 긴 비행이 그 느낌을 살린다. landDuration도 맞춰 늘렸다:
    // 같은 0.75초에 더 먼 거리를 가면 세게 던진 게 아니라 빨리 감기처럼 보인다.
    [SerializeField] private float landDuration = 0.9f;
    [SerializeField] private float tossDistance = 2.3f;
    [SerializeField] private float tossHeight = 2f;

    [Header("Run-over (beats 2-3)")]
    [SerializeField] private float settleHoldDuration = 0.2f;
    [SerializeField] private float pickupRadius = 0.45f;
    [SerializeField] private float approachTimeout = 6f;

    private float approachSpeed = 5f;
    // tossDistance에 더할 추가 거리. 한 처치로 여러 개가 드롭될 때 인스턴스별로 설정해서 이전
    // 것보다 하나씩 더 뒤에 떨어지게 하고, 같은 지점에 쌓이는 대신 흩뿌려진 궤적처럼 보이게 한다.
    private float extraTossDistance;

    [Header("Visual (per option type)")]
    // Hovl Studio의 "Crystal effect red" / "Crystal effect green". 둘 다 playOnAwake가
    // 설정된 독립적인 반복 재생 시스템이므로, 인스턴스화하기만 하면 그걸로 충분하다.
    [SerializeField] private GameObject attackVisualPrefab;
    [SerializeField] private GameObject hpVisualPrefab;
    // 모든 스톤이 같은 크기다 - 등급이 없으므로 이 값을 다르게 할 요소가 없다.
    [SerializeField] private float stoneScale = 0.825f;
    // 크리스탈 피벗이 지면에서 떠 있는 높이를 절대 거리가 아니라 stoneScale 배수로 둔다.
    // 피벗-바닥 거리가 스케일에 정확히 비례하므로(실측: 0.55→0.607, 0.825→0.910, 같은 1.103
    // 비율) 고정 상승값은 딱 한 크기에서만 맞고 더 큰 크기에선 스톤이 파묻힌다. 원래 0.55/0.35
    // 조합의 모습을 기준으로 튜닝; 파티클 원본 바운드는 보이는 크리스탈보다 넓게 잡혀 커 보인다.
    [SerializeField] private float groundLiftPerScale = 0.636f;

    [Header("Aura")]
    // 크리스탈 자체와 색을 맞춰서, 헤일로가 스톤에 없는 희귀도를 표현하는 게
    // 아니라 어떤 스톤인지를 더 강조해준다.
    [SerializeField] private Color attackAuraColor = new Color(1f, 0.28f, 0.2f);
    [SerializeField] private Color hpAuraColor = new Color(0.25f, 0.9f, 0.3f);
    [SerializeField] private float auraSize = 1.5f;
    [SerializeField] private float auraBrightness = 1.7f;
    [SerializeField] private float auraLightIntensity = 3f;
    [SerializeField] private float auraLightRange = 2.6f;

    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private StatType statType;
    private Transform player;
    private CombatLoop combatLoop;
    private StoneDropManager dropManager;
    private Vector3 restScale;
    // 인스턴스마다 코드로 생성되므로, 다른 누구도 대신 수거해주지 않는다 -
    // OnDestroy 참고.
    private Material auraMaterial;
    // StatPotionPickup과 같은 이유: HandlePlayerDied가 던지는 도중 코루틴을 끊어서,
    // 자신의 PopIdleHold 호출에 도달하기도 전에 멈출 수 있다.
    private bool idleHoldActive;

    public void Initialize(StatType statType, Transform player,
        CombatLoop combatLoop, float approachSpeed, StoneDropManager dropManager, float extraTossDistance = 0f)
    {
        this.statType = statType;
        this.player = player;
        this.combatLoop = combatLoop;
        this.dropManager = dropManager;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;
        this.extraTossDistance = extraTossDistance;

        SpawnVisual();
        restScale = Vector3.one * stoneScale;
        transform.localScale = restScale;

        auraMaterial = PickupAura.Attach(transform,
            statType == StatType.Attack ? attackAuraColor : hpAuraColor,
            auraSize, auraBrightness, auraLightIntensity, auraLightRange);

        if (combatLoop != null)
        {
            combatLoop.PushIdleHold();
            idleHoldActive = true;
        }

        StartCoroutine(TossThenRunOver());
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    // StatPotionPickup.HandlePlayerDied 참고 - 플레이어를 죽인 그 처치에서 나온 드롭은 시체로
    // 계속 미끄러져 들어가 지급되지 않고, 그 자리에서 멈춰야 한다.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }

    private void SpawnVisual()
    {
        GameObject prefab = statType == StatType.Attack ? attackVisualPrefab : hpVisualPrefab;
        if (prefab == null) return;

        GameObject visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
    }

    private IEnumerator TossThenRunOver()
    {
        yield return PlayToss();

        if (combatLoop != null) combatLoop.PopIdleHold();
        idleHoldActive = false;

        yield return new WaitForSeconds(settleHoldDuration);
        yield return PlayRunOver();

        Collect();
    }

    private IEnumerator PlayToss()
    {
        Vector3 startPos = transform.position;
        float groundY = ResolveGroundY() + groundLiftPerScale * stoneScale;

        Vector3 away = Vector3.right;
        if (player != null)
        {
            Vector3 delta = startPos - player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f) away = delta.normalized;
        }

        Vector3 landingPos = startPos + away * (tossDistance + extraTossDistance);
        landingPos.y = groundY;

        transform.localScale = Vector3.zero;

        float t = 0f;
        while (t < landDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / landDuration);

            transform.localScale = restScale * Mathf.Clamp01(p / 0.3f);

            Vector3 flat = Vector3.Lerp(startPos, landingPos, EaseOutQuad(p));
            flat.y = Mathf.Lerp(startPos.y, groundY, EaseOutQuad(p)) + tossHeight * HopHeight(p);
            transform.position = flat;

            yield return null;
        }

        transform.localScale = restScale;
        transform.position = landingPos;
    }

    private IEnumerator PlayRunOver()
    {
        float restY = transform.position.y;
        float elapsed = 0f;

        while (elapsed < approachTimeout)
        {
            elapsed += Time.deltaTime;
            if (player == null) yield break;

            Vector3 pos = transform.position;

            Vector3 toPlayer = player.position - pos;
            toPlayer.y = 0f;

            if (toPlayer.magnitude <= pickupRadius) yield break;

            pos += toPlayer.normalized * approachSpeed * Time.deltaTime;
            pos.y = restY;
            transform.position = pos;

            yield return null;
        }
    }

    private float ResolveGroundY()
    {
        if (player == null) return transform.position.y;

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

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    private void Collect()
    {
        if (dropManager != null) dropManager.AddStone(statType);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
    }
}
