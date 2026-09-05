using UnityEngine;

public class EquipmentDropPopup : MonoBehaviour
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
        GameEvents.OnEquipmentPickedUp += HandleEquipmentPickedUp;
    }

    private void OnDisable()
    {
        GameEvents.OnEquipmentPickedUp -= HandleEquipmentPickedUp;
    }

    private void HandleEquipmentPickedUp(EquipmentType equipType, StatGrade grade)
    {
        var go = new GameObject("EquipmentDropPopup");
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = localOffset + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);

        PopupText.Build(go, font, $"+{grade} {GetEquipLabel(equipType)}", fontSize, characterSize,
            GradeVisuals.GetPopupTextColor(grade));

        go.AddComponent<Billboard>();
        PopupMotion.AttachPickup(go);
    }

    private static string GetEquipLabel(EquipmentType equipType) => equipType == EquipmentType.Sword ? "Sword" : "Shield";
}
