using UnityEngine;

public class GroundScroller : ScrollSource
{
    private Material material;
    private Vector2 offset;

    private void Awake()
    {
        material = GetComponent<Renderer>().material;
    }

    // 지면은 판이 고정이고 텍스처만 흐른다
    protected override void Scroll(float step)
    {
        offset.x -= step;
        // 텍스처가 1 주기로 반복 -> 무한히 누적하면 float 정밀도만 깎여 스크롤이 끊긴다
        offset.x %= 1f;
        material.SetTextureOffset("_BaseMap", offset);
    }
}
