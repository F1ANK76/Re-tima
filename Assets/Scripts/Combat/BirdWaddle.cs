using UnityEngine;

// Cosmetic-only: lowers a bird monster's rig into more of a crouch and adds a
// side-to-side rocking motion while its Animator's "IsMoving" bool is set (see
// Monster.SetMovement). Attach to the same GameObject as the Animator (the rig root,
// e.g. "C02") - never touches the Monster's own transform, so movement/collision/
// facing are unaffected.
public class BirdWaddle : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float crouchAmount = 0.08f;
    [SerializeField] private float waddleRotation = 8f;
    [SerializeField] private float waddleFrequency = 2.2f;
    [SerializeField] private float bobAmount = 0.03f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float phase;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();

        // Permanent crouch - lower stance whether moving or idle.
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
