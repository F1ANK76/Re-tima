using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 좌측 상단 버튼 뒤에 뜨는 관리 창: 왼쪽 세로 탭 열(Stat, Equip, Stone)과 그 오른쪽의 콘텐츠
// 영역 하나. UI의 다른 부분(TitleScreenView, EquipmentPanelView)과 동일하게 전부 코드로 빌드.
//
// 각 탭은 다시 빌드되는 게 아니라 보이거나 숨겨지기만 하는 자식 오브젝트다 - 그래서 탭 안에
// 남은 상태(장비 패널의 프리뷰 리그, 스톤 탭의 마지막 굴림 결과)는 왕복해도 그대로 유지된다.
public class ManagementWindowView : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    [SerializeField] private StoneDropManager stoneDropManager;
    [SerializeField] private StageManager stageManager;
    // Equip 탭에 임베드된 EquipmentPanelView가 프리뷰에 사용하는 것과 동일한 프리팹이다.
    [SerializeField] private EquipmentDropPickup previewPickupPrefab;

    // 각 탭이 어느 메인 스테이지에서 잠금 해제되는지. Stat은 처음부터 열려 있고(인덱스 0은
    // 무의미 - 절대 잠기지 않는다), Equip과 Stone은 각각의 드롭이 실제로 시작되는
    // 시점(EquipmentDropManager와 StoneDropManager 자체의 잠금 해제 스테이지)에 정확히 열려서,
    // 보여줄 것도 없는 상태에서 탭이 시스템을 먼저 광고하는 일은 없다.
    private static readonly int[] TabUnlockStage = { 1, 2, 3 };

    private const float WindowWidth = 760f;
    private const float WindowHeight = 470f;
    private const float TabColumnWidth = 150f;
    private const float TabHeight = 54f;
    private const float TitleHeight = 52f;
    private const float Pad = 14f;

    private static readonly Color WindowBackground = new Color(0.09f, 0.09f, 0.12f, 0.97f);
    private static readonly Color ContentBackground = new Color(0.14f, 0.14f, 0.18f, 0.95f);
    private static readonly Color TabIdle = new Color(0.2f, 0.2f, 0.25f);
    private static readonly Color TabActive = new Color(0.38f, 0.32f, 0.56f);
    private static readonly Color TabLocked = new Color(0.13f, 0.13f, 0.15f);
    private static readonly Color TabLockedLabel = new Color(0.4f, 0.4f, 0.44f);
    private static readonly Color ButtonReady = new Color(0.34f, 0.29f, 0.5f);
    private static readonly Color ButtonDisabled = new Color(0.22f, 0.22f, 0.25f);
    private static readonly Color AtkColor = new Color(1f, 0.34f, 0.26f);
    private static readonly Color HpColor = new Color(0.3f, 0.88f, 0.36f);
    private static readonly Color Muted = new Color(0.62f, 0.62f, 0.68f);

    private Font font;
    private readonly GameObject[] tabPages = new GameObject[3];
    private readonly Image[] tabButtons = new Image[3];
    private readonly Button[] tabButtonComponents = new Button[3];
    private readonly Text[] tabLabels = new Text[3];
    // 인덱스 0(Stat)은 처음부터 true이고 절대 바뀔 필요가 없다; 1과 2는 RefreshTabUnlocks가
    // 각자를 잠금 해제하는 스테이지를 감지하는 순간 켜진다.
    private readonly bool[] tabUnlocked = { true, false, false };
    private int activeTab;

    // Stat 탭
    private Text statAtk;
    private Text statHp;

    // Stone 탭
    private Text atkStoneCount;
    private Text hpStoneCount;
    private Text atkOptionLine;
    private Text hpOptionLine;
    private Button atkRerollButton;
    private Button hpRerollButton;
    private Image atkRerollImage;
    private Image hpRerollImage;
    private Text rollResultLine;
    private Coroutine rollResultFlourish;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
        // 첫 페인트가 일어나기 전에 잠긴 외형을 적용한다 - 이게 없으면 잠긴 두 탭이 한
        // 프레임 동안 기본 TabIdle 색으로 잠금 해제된 것처럼 잠깐 번쩍이게 된다.
        RefreshTabUnlocks();
        SelectTab(0);
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerStatsChanged += HandleStatsChanged;
        GameEvents.OnStonesChanged += HandleStonesChanged;
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
        GameEvents.OnStonesChanged -= HandleStonesChanged;
        GameEvents.OnEquipmentPickedUp -= HandleEquipmentChanged;
        GameEvents.OnStageChanged -= HandleStageChangedForTabs;
    }

    private void HandleStatsChanged(PlayerStats stats) => RefreshStat();
    private void HandleStonesChanged(StatType t, int total, int delta) => RefreshStone();
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
        RefreshStone();
    }

    private void RefreshStat()
    {
        if (statAtk == null || player == null) return;

        PlayerStats stats = player.Stats;
        statAtk.text = $"ATK          {stats.AttackPower:0.0}";
        statHp.text = $"MAX HP       {stats.MaxHp:0}";
    }

    private void RefreshStone()
    {
        if (atkStoneCount == null) return;

        int atk = stoneDropManager != null ? stoneDropManager.AttackStones : 0;
        int hp = stoneDropManager != null ? stoneDropManager.HpStones : 0;

        atkStoneCount.text = $"ATK STONE   x{atk}";
        hpStoneCount.text = $"HP STONE    x{hp}";

        atkOptionLine.text = OptionLine(StatType.Attack);
        atkOptionLine.color = OptionColor(StatType.Attack);
        hpOptionLine.text = OptionLine(StatType.Hp);
        hpOptionLine.color = OptionColor(StatType.Hp);

        SetButtonEnabled(atkRerollButton, atkRerollImage, atk > 0);
        SetButtonEnabled(hpRerollButton, hpRerollImage, hp > 0);
    }

    private string OptionLine(StatType statType)
    {
        if (equipmentDropManager == null) return "current  -";

        float value = equipmentDropManager.GetOption(statType);
        if (value <= 0f) return "current  none";

        StatGrade? grade = equipmentDropManager.GetOptionGrade(statType);
        string label = statType == StatType.Attack ? "ATK" : "HP";
        return $"current  {grade}  {label} +{EquipmentOptionTable.Format(statType, value)}";
    }

    private Color OptionColor(StatType statType)
    {
        if (equipmentDropManager == null) return Muted;
        StatGrade? grade = equipmentDropManager.GetOptionGrade(statType);
        return grade.HasValue ? GradeVisuals.GetPopupTextColor(grade.Value) : Muted;
    }

    private static void SetButtonEnabled(Button button, Image image, bool enabled)
    {
        if (button != null) button.interactable = enabled;
        if (image != null) image.color = enabled ? ButtonReady : ButtonDisabled;
    }

    // 스톤 하나를 소모해 등급과 그 안의 값을 굴리고, 슬롯에 이미 있는 것보다 나을 때만
    // 적용한다. 확인 절차 없이 자동 반영 - 슬롯을 개선시킬 수만 있는 굴림에는 결정할 게 없다.
    private void Reroll(StatType statType)
    {
        if (stoneDropManager == null || equipmentDropManager == null) return;
        if (!stoneDropManager.TryConsumeStone(statType)) return;

        // TryApplyOption이 덮어쓰기 전에 미리 캡처해둔다 - 그래야 이후에 슬롯이 이전에
        // 가지고 있던 값만으로 등급 상승 여부를 판별할 수 있다.
        StatGrade? previousGrade = equipmentDropManager.GetOptionGrade(statType);

        StatGrade grade = GradeRoller.RollStoneOption();
        float value = EquipmentOptionTable.Roll(statType, grade);
        bool applied = equipmentDropManager.TryApplyOption(statType, grade, value);

        string label = statType == StatType.Attack ? "ATK" : "HP";
        rollResultLine.text = $"{grade}   {label} +{EquipmentOptionTable.Format(statType, value)}";
        // 굴려서 나온 등급 색을 입혀 숫자보다 결과 등급이 먼저 눈에 들어오게 한다. 현재
        // 옵션을 넘지 못했으면 Muted - 예전엔 적용됨/버려짐 문구가 아래 두 번째 줄에 있었다.
        rollResultLine.color = applied ? GradeVisuals.GetPopupTextColor(grade) : Muted;

        // 슬롯의 등급이 실제로 올라갈 때만 연출한다 - 같은 등급 이하의 굴림(같은 등급 안에서
        // 값만 더 커져 적용된 경우조차)은 평범한 결과일 뿐 강조할 순간이 아니다.
        bool gradeUp = applied && (!previousGrade.HasValue || grade > previousGrade.Value);
        if (gradeUp)
        {
            if (rollResultFlourish != null) StopCoroutine(rollResultFlourish);
            rollResultFlourish = StartCoroutine(PlayGradeUpFlourish(grade));
        }

        RefreshStone();
        RefreshStat();
    }

    private static readonly Color FlashColor = Color.white;
    private const float FlourishDuration = 0.35f;
    private const float PunchScale = 1.45f;

    // 펀치 스케일 + 빠른 흰색 플래시가 등급 고유 색으로 가라앉는 연출 - 다른 게임들이 희귀도
    // 업그레이드에 주는 "뭔가 좋은 일이 일어났다" 느낌을, 이 행 자체의 Text/RectTransform
    // 만으로 만들어 별도의 VFX 애셋이 필요 없다.
    private IEnumerator PlayGradeUpFlourish(StatGrade grade)
    {
        RectTransform rt = rollResultLine.rectTransform;
        Color settleColor = GradeVisuals.GetPopupTextColor(grade);

        float t = 0f;
        while (t < FlourishDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / FlourishDuration);

            // 빠르게 튀어나갔다가 서서히 안착한다 - 스케일 변화 대부분이 정점 유지가 아니라
            // 다시 안착하는 데 쓰이므로, 천천히 부푸는 게 아니라 스냅처럼 읽힌다.
            float scale = 1f + (PunchScale - 1f) * (1f - p) * (1f - p);
            rt.localScale = Vector3.one * scale;

            rollResultLine.color = Color.Lerp(FlashColor, settleColor, p);

            yield return null;
        }

        rt.localScale = Vector3.one;
        rollResultLine.color = settleColor;
        rollResultFlourish = null;
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

    private void Build()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(WindowWidth, WindowHeight);

        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = WindowBackground;
        // 클릭을 모두 삼켜서 창 뒤의 게임플레이가 이걸 통해 클릭되지 않도록 한다.
        bg.raycastTarget = true;

        BuildTitleBar();
        BuildTabs();
        BuildPages();
    }

    private void BuildTitleBar()
    {
        var title = CreateText(transform, "Title", "MANAGEMENT", 24, TextAnchor.MiddleLeft, Color.white);
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = new Vector2(Pad + 8f, -TitleHeight);
        trt.offsetMax = new Vector2(-70f, 0f);

        var closeGo = new GameObject("CloseButton", typeof(RectTransform));
        closeGo.transform.SetParent(transform, false);
        var img = closeGo.AddComponent<Image>();
        img.color = new Color(0.4f, 0.22f, 0.24f);
        var btn = closeGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Close);

        var crt = closeGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(1f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-Pad, -Pad);
        crt.sizeDelta = new Vector2(38f, 32f);

        var x = CreateText(closeGo.transform, "X", "X", 18, TextAnchor.MiddleCenter, Color.white);
        Stretch(x.GetComponent<RectTransform>());
    }

    private void BuildTabs()
    {
        string[] names = { "Stat", "Equip", "Stone" };
        for (int i = 0; i < names.Length; i++)
        {
            var go = new GameObject(names[i] + "Tab", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<Image>();
            img.color = TabIdle;
            tabButtons[i] = img;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = tabUnlocked[i];
            tabButtonComponents[i] = btn;
            int captured = i;
            btn.onClick.AddListener(() => SelectTab(captured));

            var brt = go.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(Pad, -TitleHeight - i * (TabHeight + 6f));
            brt.sizeDelta = new Vector2(TabColumnWidth - Pad, TabHeight);

            var label = CreateText(go.transform, "Label", names[i].ToUpper(), 20, TextAnchor.MiddleCenter,
                tabUnlocked[i] ? Color.white : TabLockedLabel);
            Stretch(label.GetComponent<RectTransform>());
            tabLabels[i] = label;
        }
    }

    private void BuildPages()
    {
        for (int i = 0; i < tabPages.Length; i++)
        {
            var page = new GameObject("Page_" + i, typeof(RectTransform));
            page.transform.SetParent(transform, false);

            var img = page.AddComponent<Image>();
            img.color = ContentBackground;

            var prt = page.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.offsetMin = new Vector2(TabColumnWidth, Pad);
            prt.offsetMax = new Vector2(-Pad, -TitleHeight);

            tabPages[i] = page;
        }

        BuildStatPage(tabPages[0].transform);
        BuildEquipPage(tabPages[1].transform);
        BuildStonePage(tabPages[2].transform);
    }

    // 실시간 플레이어 스탯 두 개만. 예전에 그 아래 있던 장비 세부 내역은 이제 Equip 탭이
    // 온전히 담당하며, 여기서 반복해봐야 두 표시 값이 어긋날 여지만 생긴다.
    private void BuildStatPage(Transform parent)
    {
        statAtk = CreateRow(parent, "Atk", 0, 22, Color.white);
        statHp = CreateRow(parent, "Hp", 1, 22, Color.white);
    }

    // 두 번째 EquipmentPanelView를 호스팅하여, 손으로 복사해서 어긋날 수 있는 표시 값이
    // 아니라 우측 상단 버튼이 보여주는 것과 정확히 동일한 내용을 이 탭에서도 보여준다.
    private void BuildEquipPage(Transform parent)
    {
        var go = new GameObject("EquipPanel", typeof(RectTransform));
        // 먼저 비활성화한다: EquipmentPanelView.Awake는 이 참조들로 스스로를 빌드하는데,
        // 이미 활성인 오브젝트에 컴포넌트를 추가하면 그게 Configure보다 먼저 실행된다.
        go.SetActive(false);
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Pad, -Pad);

        var panel = go.AddComponent<EquipmentPanelView>();
        panel.Configure(equipmentDropManager, previewPickupPrefab);
        go.SetActive(true);
    }

    private void BuildStonePage(Transform parent)
    {
        BuildStoneRow(parent, StatType.Attack, 0, AtkColor, out atkStoneCount, out atkOptionLine,
            out atkRerollButton, out atkRerollImage);
        BuildStoneRow(parent, StatType.Hp, 1, HpColor, out hpStoneCount, out hpOptionLine,
            out hpRerollButton, out hpRerollImage);

        var divider = CreateRow(parent, "ResultDivider", 4, 15, Muted);
        divider.text = "- LAST REROLL -";

        rollResultLine = CreateRow(parent, "RollResult", 5, 24, Muted);
        rollResultLine.text = "-";
    }

    private void BuildStoneRow(Transform parent, StatType statType, int index, Color color,
        out Text countText, out Text optionText, out Button button, out Image buttonImage)
    {
        float rowTop = -Pad - index * 84f;

        countText = CreateText(parent, statType + "Count", "", 21, TextAnchor.MiddleLeft, color);
        var crt = countText.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(0f, 1f);
        crt.pivot = new Vector2(0f, 1f);
        crt.anchoredPosition = new Vector2(Pad, rowTop);
        crt.sizeDelta = new Vector2(280f, 28f);

        optionText = CreateText(parent, statType + "Option", "", 16, TextAnchor.MiddleLeft, Muted);
        var ort = optionText.GetComponent<RectTransform>();
        ort.anchorMin = new Vector2(0f, 1f);
        ort.anchorMax = new Vector2(0f, 1f);
        ort.pivot = new Vector2(0f, 1f);
        ort.anchoredPosition = new Vector2(Pad, rowTop - 30f);
        ort.sizeDelta = new Vector2(320f, 24f);

        var btnGo = new GameObject(statType + "Reroll", typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);
        buttonImage = btnGo.AddComponent<Image>();
        buttonImage.color = ButtonReady;
        button = btnGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        StatType captured = statType;
        button.onClick.AddListener(() => Reroll(captured));

        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.anchoredPosition = new Vector2(-Pad, rowTop - 6f);
        brt.sizeDelta = new Vector2(128f, 40f);

        var label = CreateText(btnGo.transform, "Label", "REROLL", 17, TextAnchor.MiddleCenter, Color.white);
        Stretch(label.GetComponent<RectTransform>());
    }

    private Text CreateRow(Transform parent, string name, int index, int size, Color color)
    {
        var text = CreateText(parent, name, "", size, TextAnchor.MiddleLeft, color);
        var rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(Pad, 0f);
        rt.offsetMax = new Vector2(-Pad, 0f);
        rt.anchoredPosition = new Vector2(0f, -Pad - index * 38f);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 34f);
        return text;
    }

    private Text CreateText(Transform parent, string name, string content, int size,
        TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = color;
        text.text = content;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
