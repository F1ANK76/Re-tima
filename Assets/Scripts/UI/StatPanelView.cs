using UnityEngine;
using UnityEngine.UI;

public class StatPanelView : MonoBehaviour
{
    [SerializeField] private Text atkValueText;
    [SerializeField] private Text hpValueText;

    [SerializeField] private PlayerCharacter player;

    private void OnEnable()
    {
        GameEvents.OnPlayerStatsChanged += HandlePlayerStatsChanged;

        // The panel starts hidden, so it misses whatever broadcast already went out
        // before it was opened (e.g. PlayerCharacter's Start()) - pull the current
        // values directly instead of showing stale placeholder text until the next change.
        if (player != null) HandlePlayerStatsChanged(player.Stats);
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerStatsChanged -= HandlePlayerStatsChanged;
    }

    private void HandlePlayerStatsChanged(PlayerStats stats)
    {
        // ATK now accrues in fractional steps (0.1/0.3/0.5/...) - a whole-number display would
        // sit unchanged through most drops, so this always shows one decimal place.
        if (atkValueText != null) atkValueText.text = $"ATK : {stats.AttackPower:0.0}";
        if (hpValueText != null) hpValueText.text = $"MAX HP : {stats.MaxHp:0}";
    }
}
