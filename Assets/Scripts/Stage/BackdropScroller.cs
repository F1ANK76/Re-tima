using UnityEngine;

public class BackdropScroller : ScrollSource
{
    [SerializeField] private Transform[] segments;

    // 세그먼트 배치에서 유도 -> 배치를 옮겨도 값이 어긋나지 않는다
    private float wrapDistance;
    private float recycleDistance;

    private void Awake()
    {
        float segmentWidth = segments[1].position.x - segments[0].position.x;
        wrapDistance = segmentWidth * segments.Length;
        recycleDistance = wrapDistance * 0.5f;
    }

    // 왼쪽으로 흘리고, 화면 밖으로 나간 세그먼트를 오른쪽 끝으로 돌린다
    protected override void Scroll(float step)
    {
        foreach (Transform segment in segments)
        {
            Vector3 pos = segment.position;
            pos.x -= step;
            if (pos.x < -recycleDistance) pos.x += wrapDistance;
            segment.position = pos;
        }
    }
}
