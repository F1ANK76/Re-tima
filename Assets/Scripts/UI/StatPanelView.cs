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

        HandlePlayerStatsChanged(player.Stats);
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerStatsChanged -= HandlePlayerStatsChanged;
    }

    private void HandlePlayerStatsChanged(PlayerStats stats)
    {
        atkValueText.text = $"ATK : {stats.AttackPower:0.0}";
        hpValueText.text = $"MAX HP : {stats.MaxHp:0}";
    }
}
