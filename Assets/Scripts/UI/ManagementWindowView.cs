using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The management window behind the top-left button: a tab column down the left side (Stat,
// Equip, Stone) and one content pane to the right of it. Built entirely in code, matching how
// the rest of this UI is authored (TitleScreenView, EquipmentPanelView).
//
// Each tab is a plain child object that gets shown or hidden - not rebuilt - so state that
// lives in a tab (the equip panel's preview rigs, the stone tab's last roll result) survives
// switching away and back.
public class ManagementWindowView : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    [SerializeField] private StoneDropManager stoneDropManager;
    [SerializeField] private StageManager stageManager;
    // Same prefab the equip tab's embedded EquipmentPanelView previews from.
    [SerializeField] private EquipmentDropPickup previewPickupPrefab;

    // Which main stage unlocks each tab. Stat is available from the very start (index 0 has
    // no entry - it's never gated); Equip and Stone open exactly when their own drop starts
    // dropping (EquipmentDropManager and StoneDropManager's own unlock stages), so the tab
    // never advertises a system before there is anything in it to show.
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
    // Index 0 (Stat) starts true and never needs to change; 1 and 2 flip on once
    // RefreshTabUnlocks sees the stage that unlocks them.
    private readonly bool[] tabUnlocked = { true, false, false };
    private int activeTab;

    // Stat tab
    private Text statAtk;
    private Text statHp;

    // Stone tab
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
        // Applies the locked look before the very first paint - without this the two locked
        // tabs would flash in unlocked for a frame at their built-in TabIdle color.
        RefreshTabUnlocks();
        SelectTab(0);
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerStatsChanged += HandleStatsChanged;
        GameEvents.OnStonesChanged += HandleStonesChanged;
        GameEvents.OnEquipmentPickedUp += HandleEquipmentChanged;
        GameEvents.OnStageChanged += HandleStageChangedForTabs;
        // The window sits inactive for most of a run, missing OnStageChanged entirely while
        // closed - re-derived from StageManager's own current stage on every open instead of
        // trusting an event that could easily have fired while nobody was listening.
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

    // Unlocks each tab once its own stage is reached, and never re-locks one already open -
    // DebugJumpTo can walk the stage counter backward while testing, and a tab that had
    // already unlocked shouldn't vanish because of that.
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

        // The active tab can never legitimately be a locked one post-unlock (locked tabs
        // refuse selection - see SelectTab), so this only ever fires right after Awake, before
        // RefreshTabUnlocks has run once. Belt-and-suspenders against building on tab 0 ever
        // changing.
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

    // Spend a stone, roll a grade and a value inside it, and keep the roll only if it beats
    // what the slot already carries. The update is automatic - there is no confirm step,
    // because a roll that can only ever improve the slot has nothing to decide.
    private void Reroll(StatType statType)
    {
        if (stoneDropManager == null || equipmentDropManager == null) return;
        if (!stoneDropManager.TryConsumeStone(statType)) return;

        // Captured before TryApplyOption overwrites it, so a grade-up can be detected
        // afterward purely from what the slot used to hold.
        StatGrade? previousGrade = equipmentDropManager.GetOptionGrade(statType);

        StatGrade grade = GradeRoller.RollStoneOption();
        float value = EquipmentOptionTable.Roll(statType, grade);
        bool applied = equipmentDropManager.TryApplyOption(statType, grade, value);

        string label = statType == StatType.Attack ? "ATK" : "HP";
        rollResultLine.text = $"{grade}   {label} +{EquipmentOptionTable.Format(statType, value)}";
        // Colored by the grade that rolled, so what came up reads before the number does.
        // Muted instead when the roll didn't beat the current option - the applied/discarded
        // wording used to live on a second line below this one.
        rollResultLine.color = applied ? GradeVisuals.GetPopupTextColor(grade) : Muted;

        // Only a slot's grade actually climbing gets the flourish - a same-or-lower-grade
        // roll (even one that still applied, e.g. a bigger value in the same grade) is a
        // routine result, not a moment worth punching up.
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

    // Punch-scale + a quick white flash settling back to the grade's own color - the same
    // "something good just happened" beat other games give a rarity upgrade, built from
    // nothing but this row's own Text/RectTransform so it needs no extra VFX assets.
    private IEnumerator PlayGradeUpFlourish(StatGrade grade)
    {
        RectTransform rt = rollResultLine.rectTransform;
        Color settleColor = GradeVisuals.GetPopupTextColor(grade);

        float t = 0f;
        while (t < FlourishDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / FlourishDuration);

            // Punch out fast, ease back to rest - most of the scale's life is spent settling,
            // not peaking, so it reads as a snap rather than a slow balloon.
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
        // Locked tabs already report Button.interactable = false, which stops a real click
        // from reaching this - but SelectTab is also called directly (RefreshTabUnlocks'
        // own safety call, the initial Awake selection), so the guard has to live here too,
        // not just on the button.
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
        // Swallows clicks so the gameplay behind the window can't be hit through it.
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

    // Just the two live player stats - the equipment breakdown that used to sit under them
    // is already the Equip tab's whole job, and repeating it here only invited the two
    // readouts to disagree.
    private void BuildStatPage(Transform parent)
    {
        statAtk = CreateRow(parent, "Atk", 0, 22, Color.white);
        statHp = CreateRow(parent, "Hp", 1, 22, Color.white);
    }

    // Hosts a second EquipmentPanelView so the tab shows exactly what the top-right button
    // shows, rather than a hand-copied readout that could drift from it.
    private void BuildEquipPage(Transform parent)
    {
        var go = new GameObject("EquipPanel", typeof(RectTransform));
        // Inactive first: EquipmentPanelView.Awake builds itself from these references, and
        // adding the component to an already-active object would run that before Configure.
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
