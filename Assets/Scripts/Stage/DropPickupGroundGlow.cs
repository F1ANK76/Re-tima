using UnityEngine;

// 아이템이 튀어오르는 동안에도 빛 자국은 바닥에 붙어 있어야 한다
public class DropPickupGroundGlow : MonoBehaviour
{
    private float groundY;

    public void Initialize(float groundY) => this.groundY = groundY;

    private void LateUpdate()
    {
        Vector3 p = transform.position;
        p.y = groundY;
        transform.position = p;
    }
}
