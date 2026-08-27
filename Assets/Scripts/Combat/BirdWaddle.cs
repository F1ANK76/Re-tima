using UnityEngine;

// 순수 연출용: 새 몬스터의 리그를 좀 더 웅크린 자세로 낮추고, Animator의 IsMoving
// bool이 켜져 있는 동안 좌우로 뒤뚱거리는 움직임을 더한다(Monster.SetMovement 참고).
// Animator와 같은 GameObject(리그 루트, 예: "C02")에 붙인다 - Monster 자체의
// transform은 절대 건드리지 않으므로 이동/충돌/방향 전환에는 영향이 없다.
public class BirdWaddle : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float crouchAmount = 0.08f;
    [SerializeField] private float waddleRotation = 8f;
    [SerializeField] private float waddleFrequency = 2.2f;
    [SerializeField] private float bobAmount = 0.03f;

    // 매 프레임 조회하므로 해시로 캐싱한다 - 이름 자체는 AnimParams가 원본이다.
    private static readonly int IsMovingHash = Animator.StringToHash(AnimParams.IsMoving);

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float phase;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();

        // 항상 유지되는 웅크림 - 이동 중이든 대기 중이든 낮은 자세를 유지한다.
        transform.localPosition += Vector3.down * crouchAmount;
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
    }

    private void Update()
    {
        if (animator == null || !animator.GetBool(IsMovingHash))
        {
            transform.localPosition = basePosition;
            transform.localRotation = baseRotation;
            phase = 0f;
            return;
        }

        phase += Time.deltaTime * waddleFrequency * Mathf.PI * 2f;
        float sway = Mathf.Sin(phase);

        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, sway * waddleRotation);
        transform.localPosition = basePosition + Vector3.up * (Mathf.Abs(sway) * bobAmount - bobAmount * 0.5f);
    }
}
