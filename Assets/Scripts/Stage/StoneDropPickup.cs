using System.Collections;
using UnityEngine;

// 스테이지 3의 드롭이며, 이미 StatPotionPickup(스테이지 1)과 EquipmentDropPickup
// (스테이지 2)이 있는 계열의 세 번째 구성원이다. 동일한 순서의 동일한 세 박자 -
// 던지고 튕기며 안착, 짧은 정지, 그리고 접촉 시 효과를 지급하는 평평한 지면
// 레벨의 달려가서 줍기 - 를 사용하여 세 스테이지의 픽업 모두가 "플레이어가
// 그 위를 달려서 지나간다"는 동일한 동작으로 읽히게 한다.
//
// 다른 두 형제와 달리, 스톤에는 등급이 없다: 드롭되는 모든 크리스탈은 크기도
// 같은 동일한 스톤이며, 유일하게 다른 것은 둘 중 어느 쪽이냐는 것뿐이다.
// 빨강은 ATK 스톤, 초록은 HP 스톤으로, 스테이지 1이 이미 확립한 RedVial/
// GreenVial 색상 언어를 그대로 따른다 - 그래서 아우라는 희귀도를 표현하는 게
// 아니라 크리스탈 색에 맞춰져 있다.
//
// 정적 메시가 아니라 Hovl Studio의 Crystal effect 프리팹을 비주얼로 사용하기
// 때문에 생기는 또 하나의 차이점: 이 크리스탈은 파티클로 그려진다(루트
// 시스템이 Crystal1 메시를 렌더링한다), 그래서 스폰 시점에는 살아있는
// 파티클이 없어 렌더러 바운드가 비어있다 - 형제 픽업들이 바닥에 딱 맞게
// 놓기 위해 쓰는 바운드 측정 트릭이 여기서는 0을 반환한다. 대신 미리
// 정해둔 고정 오프셋을 사용한다.
public class StoneDropPickup : MonoBehaviour
{
    [Header("Toss + bounce (beat 1)")]
    // StatPotionPickup과 같은 궤적 형태지만, 물약의 1.4/1.1보다 눈에 띄게 더
    // 멀리, 더 높이 던져진다 - 스톤은 더 크고 무겁게 느껴지는 오브젝트이며,
    // 더 긴 비행이 그 느낌을 살려준다. landDuration도 이에 맞춰 늘렸는데, 같은
    // 0.75초 안에 더 먼 거리를 이동시키면 세게 던져진 게 아니라 그냥 빨리
    // 감기한 것처럼 보이기 때문이다.
    [SerializeField] private float landDuration = 0.9f;
    [SerializeField] private float tossDistance = 2.3f;
    [SerializeField] private float tossHeight = 2f;

    [Header("Run-over (beats 2-3)")]
    [SerializeField] private float settleHoldDuration = 0.2f;
    [SerializeField] private float pickupRadius = 0.45f;
    [SerializeField] private float approachTimeout = 6f;

    private float approachSpeed = 5f;
    // tossDistance에 더해지는 추가 거리로, 한 번의 처치로 스톤이 여러 개 동시에
    // 드롭될 때 인스턴스별로 설정된다 - 한 번에 쏟아지는 스톤들이 이전 것보다
    // 하나씩 더 뒤에 떨어지도록 하여, 그룹 전체가 같은 지점에 쌓이는 게 아니라
    // 흩뿌려진 궤적처럼 보이게 한다.
    private float extraTossDistance;

    [Header("Visual (per option type)")]
    // Hovl Studio의 "Crystal effect red" / "Crystal effect green". 둘 다 playOnAwake가
    // 설정된 독립적인 반복 재생 시스템이므로, 인스턴스화하기만 하면 그걸로 충분하다.
    [SerializeField] private GameObject attackVisualPrefab;
    [SerializeField] private GameObject hpVisualPrefab;
    // 모든 스톤이 같은 크기다 - 등급이 없으므로 이 값을 다르게 할 요소가 없다.
    [SerializeField] private float stoneScale = 0.825f;
    // 크리스탈의 피벗이 지면 위로 얼마나 떠 있는지를, 절대 거리가 아니라
    // stoneScale의 배수로 나타낸다. 크리스탈의 피벗-바닥 거리는 스케일에 정확히
    // 비례한다(실측: 스케일 0.55일 때 0.607, 0.825일 때 0.910 - 동일한 1.103
    // 비율), 그래서 고정된 상승값은 딱 한 크기에서만 맞고 그보다 큰 크기에서는
    // 스톤이 파묻혀버린다. 원래 0.55/0.35 조합에서 스톤이 놓이던 모습을 기준으로
    // 튜닝했다; 파티클의 원본 바운드는 눈에 보이는 크리스탈보다 더 넓게
    // 잡히므로 실제보다 커 보인다.
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
    // StatPotionPickup과 동일한 이유: HandlePlayerDied가 던지는 도중에 코루틴을
    // 끊어버릴 수 있어, 자신의 PopIdleHold 호출에 도달하기도 전에 멈출 수
    // 있다.
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

    // StatPotionPickup.HandlePlayerDied 참고 - 플레이어를 죽인 처치와 동일한
    // 처치에서 나온 드롭은 시체를 향해 계속 미끄러져 들어가(그리고 지급하지)
    // 않고 그 자리에서 멈춰야 한다.
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
