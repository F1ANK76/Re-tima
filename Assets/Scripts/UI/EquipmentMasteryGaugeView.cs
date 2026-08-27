using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 장비 슬롯 하나의 숙련도 게이지용 얇은 바 - BossGaugeView와 같은 "차오르는" 채움 방식이지만,
// 중복/저등급 픽업이 버려지는 대신 채워주는 진행도를 표시한다(EquipmentDropManager.CompleteDrop).
// 보스 게이지와 달리 100%를 반복해서 넘도록 설계됐다: 넘을 때마다 레벨이 오르고, 바는 상한에
// 멈춰있지 않고 다시 0쪽으로 감긴다.
//
// (TitleScreenView처럼) 배경/채움/라벨을 코드로 자식 생성하지만, 독립 HUD 요소와 달리
// anchorToBottomCenter가 꺼지면 자기 RectTransform(부모 안에서의 위치/크기)은 전적으로 호출자에게
// 맡긴다 - EquipmentPanelView가 이 방식으로 각 행 아래에 하나씩 끼워 넣는다.
public class EquipmentMasteryGaugeView : MonoBehaviour
{
    [SerializeField] private EquipmentDropManager equipmentDropManager;
    [SerializeField] private EquipmentType equipType;
    [SerializeField] private Color fillColor = new Color(0.4f, 0.75f, 1f);
    // 독립 모드(anchorToBottomCenter)에서만 사용: 바 높이와 화면 하단 가장자리로부터의 거리 -
    // 호출자가 인스턴스마다 오프셋을 달리 줘서 여러 게이지를 겹치지 않게 쌓는다.
    [SerializeField] private float barHeight = 30f;
    [SerializeField] private float bottomOffset = 160f;
    // On: 이 컴포넌트가 자신의 RectTransform을 완전히 소유(하단 중앙 HUD 스트립). Off: 호출자가
    // 이미 크기/위치를 정해둔 상태(예: 패널 안의 한 행)라 충돌하지 않도록 자식들만 건드린다.
    [SerializeField] private bool anchorToBottomCenter = true;
    // Off: 퍼센트만 표시한다("45%") - 끼워 넣는 형태의 게이지는 보통 타입/레벨/등급을 이미
    // 표시하는 라벨 바로 아래에 놓이므로, 그렇지 않으면 내용이 중복된다.
    [SerializeField] private bool includeTypeAndLevelInLabel = true;
    [SerializeField] private float fillTweenDuration = 0.35f;

    private RectTransform fillRect;
    private Text label;
    private int displayedPercent;
    private int lastKnownLevel = -1;
    private Coroutine tweenRoutine;

    private void Awake()
    {
        if (anchorToBottomCenter) ConfigureStandaloneAnchors();
        Build();
    }

    // 런타임 생성 호출자(EquipmentPanelView)용: GameObject가 비활성인 동안 컴포넌트를 추가하고
    // 이 함수를 호출한 뒤에야 활성화한다 - Unity가 비활성 오브젝트의 Awake를 미루므로, Build()
    // 실행보다도, Awake가 독립 모드 기본값을 적용하기보다도 먼저 실행됨이 보장된다.
    public void Configure(EquipmentDropManager manager, EquipmentType type, Color color,
        bool anchorToBottomCenter, bool includeTypeAndLevelInLabel)
    {
        equipmentDropManager = manager;
        equipType = type;
        fillColor = color;
        this.anchorToBottomCenter = anchorToBottomCenter;
        this.includeTypeAndLevelInLabel = includeTypeAndLevelInLabel;
    }

    private void OnEnable()
    {
        GameEvents.OnEquipmentPickedUp += HandlePickedUp;
        Refresh(instant: true);
    }

    private void OnDisable()
    {
        GameEvents.OnEquipmentPickedUp -= HandlePickedUp;
    }

    private void HandlePickedUp(EquipmentType pickedType, StatGrade grade)
    {
        if (pickedType != equipType) return;
        Refresh(instant: false);
    }

    private void Refresh(bool instant)
    {
        if (equipmentDropManager == null) return;

        int level = equipType == EquipmentType.Sword ? equipmentDropManager.SwordLevel : equipmentDropManager.ShieldLevel;
        float progress = equipType == EquipmentType.Sword
            ? equipmentDropManager.SwordMasteryProgressPercent
            : equipmentDropManager.ShieldMasteryProgressPercent;
        int percent = Mathf.RoundToInt(progress);

        // 레벨업이 되면 미터가 다시 0 근처로 감긴다 - 이는 진행도가 사라지는 것이 아니라
        // 진짜 리셋(얻은 레벨이 그 보상이다)이므로, 뒤로 애니메이션되지 않고 즉시 스냅된다.
        bool levelJustChanged = level != lastKnownLevel;
        lastKnownLevel = level;

        if (instant || levelJustChanged || percent <= displayedPercent)
        {
            if (tweenRoutine != null) StopCoroutine(tweenRoutine);
            tweenRoutine = null;
            ApplyPercent(percent, level);
            return;
        }

        if (tweenRoutine != null) StopCoroutine(tweenRoutine);
        tweenRoutine = StartCoroutine(TweenTo(percent, level));
    }

    private IEnumerator TweenTo(int target, int level)
    {
        int start = displayedPercent;
        float t = 0f;

        while (t < fillTweenDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = EaseOutQuad(Mathf.Clamp01(t / fillTweenDuration));
            ApplyPercent(Mathf.RoundToInt(Mathf.Lerp(start, target, p)), level);
            yield return null;
        }

        ApplyPercent(target, level);
        tweenRoutine = null;
    }

    private void ApplyPercent(int percent, int level)
    {
        displayedPercent = percent;

        if (fillRect != null)
        {
            Vector2 anchorMax = fillRect.anchorMax;
            anchorMax.x = Mathf.Clamp01(percent / 100f);
            fillRect.anchorMax = anchorMax;
        }

        if (label != null)
        {
            if (includeTypeAndLevelInLabel)
            {
                string typeLabel = equipType == EquipmentType.Sword ? "Sword" : "Shield";
                label.text = $"{typeLabel} Lv.{level}  {percent}%";
            }
            else
            {
                label.text = $"{percent}%";
            }
        }
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    // 보스 게이지와 동일한 하단 중앙 영역이므로 독립 바는 화면 해상도와 상관없이 같은 너비를
    // 갖는다. 끼워 넣는 형태(anchorToBottomCenter off)에서는 완전히 건너뛴다.
    private void ConfigureStandaloneAnchors()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.333f, 0f);
        rt.anchorMax = new Vector2(0.667f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottomOffset);
        rt.sizeDelta = new Vector2(0f, barHeight);
    }

    private void Build()
    {
        Image background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.6f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(transform, false);
        fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGo.AddComponent<Image>();
        fillImage.color = fillColor;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        label = labelGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 16;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        var shadow = labelGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }
}
