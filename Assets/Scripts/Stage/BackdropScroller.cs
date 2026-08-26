using UnityEngine;

// Moves a fixed set of decoration segments together along -X and recycles any segment that
// scrolls fully past the left edge back to the right, so the backdrop appears to repeat forever
// without needing unique geometry for the whole path.
public class BackdropScroller : MonoBehaviour
{
    [SerializeField] private Transform[] segments;
    [SerializeField] private float scrollSpeed = 0.6f;
    [SerializeField] private float segmentWidth = 14f;
    [SerializeField] private float recycleDistance = 21f;
    [SerializeField] private float speedEaseTime = 0.4f;
    [SerializeField] private float paceVariation = 0.08f;
    // See GroundScroller: SmoothDamp's asymptotic tail keeps the backdrop creeping long
    // after the character has stopped. Snapping the last sliver to zero ends the stop
    // cleanly, and both scrollers use the same threshold so the two layers halt together.
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    // IsScrolling flips on/off constantly as the character starts/stops running (see
    // CombatLoop), and snapping straight to full speed each time read as a mechanical
    // conveyor belt. Easing the factor instead makes each start/stop feel like an actual
    // acceleration, and the Perlin wobble on top keeps a steady walk from feeling perfectly
    // metronomic even while it's fully running.
    private float speedFactor;
    private float speedFactorVelocity;
    private float paceSeed;

    private void Awake()
    {
        paceSeed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        if (segments == null || segments.Length == 0) return;

        float targetFactor = IsScrolling ? 1f : 0f;
        speedFactor = Mathf.SmoothDamp(speedFactor, targetFactor, ref speedFactorVelocity, speedEaseTime);

        // Only snap while coming to a stop - snapping on the way up would kill the ease-in.
        if (!IsScrolling && speedFactor < stopSnapThreshold)
        {
            speedFactor = 0f;
            speedFactorVelocity = 0f;
        }
        if (speedFactor < 0.0001f) return;

        float pace = 1f + (Mathf.PerlinNoise(Time.time * 0.15f + paceSeed, 0f) - 0.5f) * 2f * paceVariation;
        float wrapDistance = segmentWidth * segments.Length;
        float step = scrollSpeed * pace * speedFactor * Time.deltaTime;

        foreach (Transform segment in segments)
        {
            Vector3 pos = segment.position;
            pos.x -= step;
            if (pos.x < -recycleDistance) pos.x += wrapDistance;
            segment.position = pos;
        }
    }
}
