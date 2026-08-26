using UnityEngine;

// Purely cosmetic: the player and ground never actually move, so this fakes forward
// travel by scrolling the ground texture backward under the player's feet. The scrolling
// backdrop trees (BackdropScroller) are what actually sell the motion, so the ground itself
// stays a flat color instead of needing a visible tiling pattern of its own.
public class GroundScroller : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private float speedEaseTime = 0.4f;
    [SerializeField] private float paceVariation = 0.08f;
    // SmoothDamp approaches its target asymptotically, so the last sliver of speed lingers
    // for far longer than speedEaseTime suggests - which reads as the ground still sliding
    // after the character has visibly stopped. Below this factor a stop is treated as
    // finished and snapped to zero, cutting that tail off.
    [SerializeField] private float stopSnapThreshold = 0.04f;

    public bool IsScrolling { get; set; } = true;

    private Material material;
    private Vector2 offset;

    // Eased the same way as BackdropScroller, and for the same reason: IsScrolling snapping
    // straight to full speed every time the character starts/stops running read as the
    // ground jerking under them rather than easing into a walk.
    private float speedFactor;
    private float speedFactorVelocity;
    private float paceSeed;

    private void Awake()
    {
        material = GetComponent<Renderer>().material;
        paceSeed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        float targetFactor = IsScrolling ? 1f : 0f;
        speedFactor = Mathf.SmoothDamp(speedFactor, targetFactor, ref speedFactorVelocity, speedEaseTime);

        // Only snap while coming to a stop - snapping on the way up would kill the ease-in.
        if (!IsScrolling && speedFactor < stopSnapThreshold)
        {
            speedFactor = 0f;
            speedFactorVelocity = 0f;
        }
        if (speedFactor < 0.0001f) return;

        // The player runs toward +X, so the whole world has to read as travelling -X:
        // BackdropScroller moves its segments that way and the ground must match.
        //
        // Subtracting is correct here, and the reason is two sign flips that cancel:
        //   1. a texture feature sits where uv_mesh = (feature - offset) / tiling, so it
        //      moves OPPOSITE to the offset, and
        //   2. Unity's built-in Plane has u DECREASING along +X (its -5 corner is u=1,
        //      its +5 corner is u=0), so a lower uv_mesh is a HIGHER world X.
        // Net: offset down -> feature moves -X, which is what we want. Flipping this to +=
        // sent the ground right while everything on it went left.
        float pace = 1f + (Mathf.PerlinNoise(Time.time * 0.15f + paceSeed, 0f) - 0.5f) * 2f * paceVariation;
        offset.x -= scrollSpeed * pace * speedFactor * Time.deltaTime;
        material.SetTextureOffset("_BaseMap", offset);
    }
}
