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

        // 패널은 처음에 숨겨진 상태로 시작하므로, 열리기 전에 이미 발생한 브로드캐스트
        // (예: PlayerCharacter의 Start())를 놓치게 된다 - 다음 변경이 있을 때까지 오래된
        // 플레이스홀더 텍스트를 보여주는 대신 현재 값을 직접 가져와 표시한다.
        HandlePlayerStatsChanged(player.Stats);
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerStatsChanged -= HandlePlayerStatsChanged;
    }

    private void HandlePlayerStatsChanged(PlayerStats stats)
    {
        // ATK는 이제 소수 단위(0.1/0.3/0.5/...)로 증가한다 - 정수로 표시하면 대부분의 드롭에서
        // 값이 그대로 유지되는 것처럼 보이므로, 항상 소수점 한 자리까지 표시한다.
        atkValueText.text = $"ATK : {stats.AttackPower:0.0}";
        hpValueText.text = $"MAX HP : {stats.MaxHp:0}";
    }
}
