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
        material.SetTextureOffset("_BaseMap", offset);
    }
}
