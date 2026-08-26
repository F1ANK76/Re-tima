using UnityEngine;

// The stage 3 counterpart to StatDropPopup/EquipmentDropPopup, riding the same world-space
// outlined TextMesh (see PopupText) above the player's health bar.
//
// Stones are only counted, not spent on anything yet, so this announces the tally rather
// than a stat change - it is the entire on-screen feedback a collected crystal gets.
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
        // Colored by the crystal type rather than a grade: the stone itself has no grade once
        // banked, and red/green is what tells the player which slot the haul can be spent on.
        Color color = statType == StatType.Attack
            ? new Color(1f, 0.28f, 0.2f)
            : new Color(0.25f, 0.9f, 0.3f);

        // No number at all: a stone is always worth exactly one, so printing the count only
        // ever says "1" and adds nothing. The running total belongs in a persistent counter
        // rather than a popup that flashes past.
        PopupText.Build(go, font, $"+{label} STONE", fontSize, characterSize, color);

        go.AddComponent<Billboard>();
        go.AddComponent<StatDropPopupMotion>();
    }
}
