using UnityEngine;

// GameEvents.OnEquipmentPickedUp가 발생할 때마다 플레이어 자신의 체력바 위에 "+Normal Sword"
// 팝업을 띄운다 - 장착 중인 것보다 더 나은지 여부와 무관하게, 수집된 모든 검/방패에 대해
// 표시한다. 중복 아이템조차 이제는 숙련도(mastery) 게이지를 채우기 때문이다
// (EquipmentDropManager.CompleteDrop 참고). StatDropPopup의 장비 버전으로, 동일한
// 월드 스페이스 TextMesh + Billboard 방식을 그대로 사용한다.
public class EquipmentDropPopup : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private int fontSize = 72;
    // StatDropPopup처럼 등급에 따라 크기가 변하지 않고 고정이다: 이 라벨은 이미 등급을
    // 단어로 명시하고 있으므로, 크기로 같은 정보를 또 전달할 필요가 없다.
    [SerializeField] private float characterSize = 0.062f;

    private Font font;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnEnable()
    {
        GameEvents.OnEquipmentPickedUp += HandleEquipmentPickedUp;
    }

    private void OnDisable()
    {
        GameEvents.OnEquipmentPickedUp -= HandleEquipmentPickedUp;
    }

    private void HandleEquipmentPickedUp(EquipmentType equipType, StatGrade grade)
    {
        if (anchor == null) return;

        var go = new GameObject("EquipmentDropPopup");
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        PopupText.Build(go, font, $"+{grade} {GetEquipLabel(equipType)}", fontSize, characterSize,
            GradeVisuals.GetPopupTextColor(grade));

        go.AddComponent<Billboard>();
        go.AddComponent<StatDropPopupMotion>();
    }

    private static string GetEquipLabel(EquipmentType equipType) => equipType == EquipmentType.Sword ? "Sword" : "Shield";
}
