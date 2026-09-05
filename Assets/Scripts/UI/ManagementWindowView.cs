using UnityEngine;
using UnityEngine.UI;

public class ManagementWindowView : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    [SerializeField] private StageManager stageManager;
    // Equip 탭에 임베드된 EquipmentPanelView가 프리뷰에 사용하는 것과 동일한 프리팹이다.
    [SerializeField] private DropPickup previewPickupPrefab;
    [SerializeField] private EquipmentPanelView equipmentPanelPrefab;

    [SerializeField] private Button closeButton;

    [SerializeField] private Image[] tabButtons;
    [SerializeField] private Button[] tabButtonComponents;
    [SerializeField] private Text[] tabLabels;
    [SerializeField] private GameObject[] tabPages;

    private static readonly int[] TabUnlockStage = { 1, 2 };

    private const float Pad = 14f;

    private static readonly Color TabIdle = new Color(0.2f, 0.2f, 0.25f);
    private static readonly Color TabActive = new Color(0.38f, 0.32f, 0.56f);
    private static readonly Color TabLocked = new Color(0.13f, 0.13f, 0.15f);

    private readonly bool[] tabUnlocked = { true, false };
    private int activeTab;

    // Stat 탭
    [SerializeField] private Text statAtk;
    [SerializeField] private Text statHp;

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);

        for (int i = 0; i < tabButtonComponents.Length; i++)
        {
            int captured = i;
            tabButtonComponents[i].onClick.AddListener(() => SelectTab(captured));
        }

        BuildEquipPage(tabPages[1].transform);

        RefreshTabUnlocks();
        SelectTab(0);
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerStatsChanged += HandleStatsChanged;
        GameEvents.OnEquipmentPickedUp += HandleEquipmentChanged;
        GameEvents.OnStageChanged += HandleStageChangedForTabs;
        RefreshTabUnlocks();
        RefreshAll();
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerStatsChanged -= HandleStatsChanged;
        GameEvents.OnEquipmentPickedUp -= HandleEquipmentChanged;
        GameEvents.OnStageChanged -= HandleStageChangedForTabs;
    }

    private void HandleStatsChanged(PlayerStats stats) => RefreshStat();
    private void HandleEquipmentChanged(EquipmentType t, StatGrade g) => RefreshAll();
    private void HandleStageChangedForTabs(int mainStage, int subStage) => RefreshTabUnlocks();

    private void RefreshTabUnlocks()
    {
        int stage = stageManager.MainStage;

        for (int i = 1; i < tabUnlocked.Length; i++)
        {
            if (tabUnlocked[i] || stage < TabUnlockStage[i]) continue;

            tabUnlocked[i] = true;
            tabButtonComponents[i].interactable = true;
            tabLabels[i].color = Color.white;
        }

        if (!tabUnlocked[activeTab]) SelectTab(0);

        ApplyTabColors();
    }

    private void ApplyTabColors()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            tabButtons[i].color = !tabUnlocked[i] ? TabLocked : (i == activeTab ? TabActive : TabIdle);
        }
    }

    public void Close() => gameObject.SetActive(false);

    private void RefreshAll()
    {
        RefreshStat();
    }

    private void RefreshStat()
    {
        PlayerStats stats = player.Stats;
        statAtk.text = $"ATK          {stats.AttackPower:0.0}";
        statHp.text = $"MAX HP       {stats.MaxHp:0}";
    }

    private void SelectTab(int index)
    {
        if (!tabUnlocked[index]) return;

        activeTab = index;
        for (int i = 0; i < tabPages.Length; i++)
        {
            tabPages[i].SetActive(i == index);
        }
        ApplyTabColors();
        RefreshAll();
    }

    private void BuildEquipPage(Transform parent)
    {
        var panel = Instantiate(equipmentPanelPrefab);
        var rt = panel.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Pad, -Pad);

        panel.Configure(equipmentDropManager, previewPickupPrefab);
        panel.gameObject.SetActive(true);
    }
}
