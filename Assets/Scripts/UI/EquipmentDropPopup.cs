using UnityEngine;

// Spawns a "+Normal Sword" popup above the player's own health bar every time
// GameEvents.OnEquipmentPickedUp fires - every collected sword/shield, whether or not it beats
// what's equipped, since even a duplicate now feeds the mastery meter (see
// EquipmentDropManager.CompleteDrop). The equipment counterpart to StatDropPopup, riding the
// same world-space TextMesh + Billboard convention.
public class EquipmentDropPopup : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private int fontSize = 72;
    // Flat rather than grade-ramped like StatDropPopup's: this label already spells the grade
    // out in words, so it doesn't need size carrying the same information.
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
