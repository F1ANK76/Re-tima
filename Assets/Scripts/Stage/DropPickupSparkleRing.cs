using UnityEngine;

// 전체 반짝임 고리를 천천히 회전시킨다. 개별 깜빡임과는 별개로 동작하여,
// 반짝임들이 화면상 고정된 위치에 계속 머물지 않고 아이템 주변을 떠돈다.
public class DropPickupSparkleRing : MonoBehaviour
{
    private float degreesPerSecond;

    public void Initialize(float degreesPerSecond) => this.degreesPerSecond = degreesPerSecond;

    private void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
