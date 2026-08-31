using UnityEngine;
using UnityEngine.UI;

// 좌측 상단 버튼 뒤에 뜨는 관리 창: 왼쪽 세로 탭 열(Stat, Equip)과 그 오른쪽의 콘텐츠
// 영역 하나. 창의 레이아웃(타이틀바/탭/페이지 배경)은 씬에 미리 지어져 있다 - 이 스크립트는
// 탭 전환/잠금 해제/스탯 갱신 같은 상태만 다룬다. Equip 탭 안의 EquipmentPanelView만 예외로,
// 프리팹을 런타임에 그대로 심는다(BuildEquipPage 참고) - 우측 상단 버튼의 패널과 같은
// 프리팹을 쓰므로 손으로 복사해서 두 버전이 어긋날 일이 없다.
//
// Stone 탭은 itch.io 1차 출시 범위(StageManager.MaxMainStage = 2)에 스톤 시스템 해금
// 스테이지(3)가 포함되지 않아 제거했다 - 영원히 잠긴 채로만 보이는 탭을 노출하지 않기 위함.
//
// 각 탭은 다시 빌드되는 게 아니라 보이거나 숨겨지기만 하는 자식 오브젝트다 - 그래서 탭 안에
// 남은 상태(장비 패널의 프리뷰 리그)는 왕복해도 그대로 유지된다.
public class ManagementWindowView : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    [SerializeField] private StageManager stageManager;
    // Equip 탭에 임베드된 EquipmentPanelView가 프리뷰에 사용하는 것과 동일한 프리팹이다.
    [SerializeField] private DropPickup previewPickupPrefab;
    // Equip 탭에 그대로 심을 EquipmentPanelView 프리팹 - 우측 상단 버튼이 보여주는 것과
    // 동일한 레이아웃을 코드로 다시 짓지 않고 재사용한다.
    [SerializeField] private EquipmentPanelView equipmentPanelPrefab;

    [SerializeField] private Button closeButton;

    // 씬에 미리 배치된 탭 버튼/라벨/페이지 - 인덱스 0 = Stat, 1 = Equip. 순서와 개수가 바뀔 일이
    // 없어서(Stone 탭은 영구 제거됨) 굳이 코드로 반복 생성하지 않는다.
    [SerializeField] private Image[] tabButtons;
    [SerializeField] private Button[] tabButtonComponents;
    [SerializeField] private Text[] tabLabels;
    [SerializeField] private GameObject[] tabPages;

    // 각 탭이 어느 메인 스테이지에서 잠금 해제되는지. Stat은 처음부터 열려 있고(인덱스 0은
    // 무의미 - 절대 잠기지 않는다), Equip은 그 드롭이 실제로 시작되는 시점(EquipmentDropManager
    // 자체의 잠금 해제 스테이지)에 정확히 열려서, 보여줄 것도 없는 상태에서 탭이 시스템을 먼저
    // 광고하는 일은 없다.
    private static readonly int[] TabUnlockStage = { 1, 2 };

    // Equip 탭 안에 EquipmentPanelView 프리팹 인스턴스를 배치할 때만 쓰인다 - 그 외 레이아웃은
    // 전부 씬에 고정돼 있다.
    private const float Pad = 14f;

    // 탭 버튼은 잠금/선택 상태에 따라 런타임에 계속 다시 칠해진다(ApplyTabColors) - 그래서
    // 초기 저작값이 아니라 여기 상수로 남아 있다.
    private static readonly Color TabIdle = new Color(0.2f, 0.2f, 0.25f);
    private static readonly Color TabActive = new Color(0.38f, 0.32f, 0.56f);
    private static readonly Color TabLocked = new Color(0.13f, 0.13f, 0.15f);

    // 인덱스 0(Stat)은 처음부터 true이고 절대 바뀔 필요가 없다; 1은 RefreshTabUnlocks가
    // 잠금 해제하는 스테이지를 감지하는 순간 켜진다.
    private readonly bool[] tabUnlocked = { true, false };
    private int activeTab;

    // Stat 탭
    [SerializeField] private Text statAtk;
    [SerializeField] private Text statHp;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        for (int i = 0; i < tabButtonComponents.Length; i++)
        {
            if (tabButtonComponents[i] == null) continue;
            int captured = i;
            tabButtonComponents[i].onClick.AddListener(() => SelectTab(captured));
        }

        BuildEquipPage(tabPages[1].transform);

        // 첫 페인트가 일어나기 전에 잠긴 외형을 적용한다 - 이게 없으면 잠긴 두 탭이 한
        // 프레임 동안 기본 TabIdle 색으로 잠금 해제된 것처럼 잠깐 번쩍이게 된다.
        RefreshTabUnlocks();
        SelectTab(0);
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerStatsChanged += HandleStatsChanged;
        GameEvents.OnEquipmentPickedUp += HandleEquipmentChanged;
        GameEvents.OnStageChanged += HandleStageChangedForTabs;
        // 이 창은 플레이 도중 대부분 비활성이라 닫혀 있는 동안의 OnStageChanged를 아예
        // 놓친다 - 아무도 구독하지 않는 사이 지나갔을 이벤트를 신뢰하는 대신, 열릴 때마다
        // StageManager의 현재 스테이지를 기준으로 다시 계산한다.
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

    // 자신의 해금 스테이지에 도달하면 열리고, 한번 열린 탭은 절대 다시 잠기지 않는다 -
    // 테스트 중 DebugJumpTo가 스테이지 카운터를 거꾸로 되돌려도 이미 열렸던 탭이 그
    // 때문에 사라져서는 안 되기 때문이다.
    private void RefreshTabUnlocks()
    {
        int stage = stageManager != null ? stageManager.MainStage : 1;

        for (int i = 1; i < tabUnlocked.Length; i++)
        {
            if (tabUnlocked[i] || stage < TabUnlockStage[i]) continue;

            tabUnlocked[i] = true;
            if (tabButtonComponents[i] != null) tabButtonComponents[i].interactable = true;
            if (tabLabels[i] != null) tabLabels[i].color = Color.white;
        }

        // 잠긴 탭은 선택을 거부하므로(SelectTab 참고) 해금 이후 활성 탭이 잠겨 있을 수는
        // 없다 - 즉 Awake 직후 이 함수가 처음 도는 시점에만 발동한다. 탭 0으로 빌드된다는
        // 가정이 언젠가 바뀌더라도 대비하기 위한 이중 안전장치다.
        if (!tabUnlocked[activeTab]) SelectTab(0);

        ApplyTabColors();
    }

    private void ApplyTabColors()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
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
        if (statAtk == null || player == null) return;

        PlayerStats stats = player.Stats;
        statAtk.text = $"ATK          {stats.AttackPower:0.0}";
        statHp.text = $"MAX HP       {stats.MaxHp:0}";
    }

    private void SelectTab(int index)
    {
        // 잠긴 탭은 Button.interactable = false라 실제 클릭은 여기까지 못 오지만, SelectTab은
        // 직접 호출되기도 한다(RefreshTabUnlocks의 안전 호출, Awake의 초기 선택) - 그래서
        // 가드가 버튼뿐 아니라 여기에도 있어야 한다.
        if (!tabUnlocked[index]) return;

        activeTab = index;
        for (int i = 0; i < tabPages.Length; i++)
        {
            if (tabPages[i] != null) tabPages[i].SetActive(i == index);
        }
        ApplyTabColors();
        RefreshAll();
    }

    // EquipmentPanelView 프리팹을 그대로 심는다 - 같은 레이아웃을 손으로 복사해서 두
    // 버전이 서로 어긋나게 두지 않는다.
    private void BuildEquipPage(Transform parent)
    {
        if (equipmentPanelPrefab == null) return;

        // 프리팹 자체가 비활성 상태로 저장돼 있다: EquipmentPanelView.Awake는 이 참조들로
        // 스스로를 빌드하는데, 활성 상태로 인스턴스화하면 그게 Configure보다 먼저 실행된다.
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
