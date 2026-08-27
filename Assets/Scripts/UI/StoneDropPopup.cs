using UnityEngine;

// StatDropPopup/EquipmentDropPopup의 스테이지 3 버전으로, 플레이어 체력바 위에 동일한 월드 스페이스
// 외곽선 TextMesh(PopupText 참고)를 쓴다. 스톤은 아직 아무 데도 소비되지 않고 개수만 세므로 스탯
// 변화가 아니라 누적 개수를 알려준다 - 수집된 크리스탈이 받는 화면상 피드백은 이게 전부다.
public class StoneDropPopup : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private int fontSize = 72;
    [SerializeField] private float characterSize = 0.062f;

    private Font font;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnEnable()
    {
        GameEvents.OnStonesChanged += HandleStonesChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnStonesChanged -= HandleStonesChanged;
    }

    private void HandleStonesChanged(StatType statType, int total, int delta)
    {
        if (delta <= 0) return;
        if (anchor == null) return;

        var go = new GameObject("StoneDropPopup");
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        string label = statType == StatType.Attack ? "ATK" : "HP";
        // 등급이 아니라 크리스탈 종류로 색을 정한다: 스톤은 일단 저장되고 나면 등급이
        // 없으며, 이 획득분을 어느 슬롯에 쓸 수 있는지를 빨강/초록으로 알려준다.
        Color color = statType == StatType.Attack
            ? new Color(1f, 0.28f, 0.2f)
            : new Color(0.25f, 0.9f, 0.3f);

        // 숫자를 아예 표시하지 않는다: 스톤은 항상 정확히 1개씩이라 개수를 찍어도 매번 "1"뿐이다.
        // 누적 총합은 잠깐 반짝이고 사라지는 팝업이 아니라 상시 표시되는 카운터가 담당할 몫이다.
        PopupText.Build(go, font, $"+{label} STONE", fontSize, characterSize, color);

        go.AddComponent<Billboard>();
        go.AddComponent<StatDropPopupMotion>();
    }
}
