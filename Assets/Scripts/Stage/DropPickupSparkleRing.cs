using UnityEngine;

public class DropPickupSparkleRing : MonoBehaviour
{
    private float degreesPerSecond;

    public void Initialize(float degreesPerSecond) => this.degreesPerSecond = degreesPerSecond;

    private void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
