using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴: 실제 진행 중인 씬 위에 게임 이름과 Play 버튼만 얹는다 - 아무것도 누르기 전부터
// 주인공은 이미 숲을 달리고 있으니, 메뉴가 게임의 그림이 아니라 게임 그 자체다. UI(배경 스크림,
// 낮/밤 하늘 그라데이션, 버튼 베벨)는 씬에 미리 구성되어 있고(생성된 이미지는
// Assets/Textures/GeneratedUI에 구운 에셋), 이 스크립트는 그 위에서 표시/숨김과 페이드만 담당한다.
[RequireComponent(typeof(CanvasGroup))]
public class TitleScreenView : MonoBehaviour
{
    [SerializeField] private string titleText = "Re:tima";
    [SerializeField] private float fadeOutDuration = 0.45f;

    // 숨기는 UI 요소
    [SerializeField] private GameObject[] hideWhileShowing;

    // 플레이어 전투 시 위치값
    [SerializeField] private Transform centerWhileShowing;

    [SerializeField] private Text titleLabel;
    [SerializeField] private Button playButton;

    private CanvasGroup canvasGroup;
    private Action onPlay;
    private bool[] hiddenPriorState;
    private bool playPressed;
    // Show()는 게임당 한 번뿐이라 여기서 한 번만 캡처하면 된다 - Camera.main이 아직 없어
    // CenterSubject가 조기 종료돼도 RestoreSubject가 되돌릴 값은 항상 확보되어 있다.
    private Vector3 originalSubjectPosition;

    private void Awake()
    {
        // 맨 위에 그리기
        transform.SetAsLastSibling();

        canvasGroup = GetComponent<CanvasGroup>();

        if (centerWhileShowing != null) originalSubjectPosition = centerWhileShowing.position;

        if (titleLabel != null) titleLabel.text = titleText;
        if (playButton != null) playButton.onClick.AddListener(HandlePlayPressed);
    }

    public void Show(Action playCallback)
    {
        onPlay = playCallback;
        playPressed = false;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        if (playButton != null) playButton.interactable = true;
        HideHud();
        CenterSubject();
        gameObject.SetActive(true);
    }

    private void CenterSubject()
    {
        Camera cam = Camera.main;
        if (centerWhileShowing == null || cam == null) return;

        // 카메라의 x 좌표에 맞추면, 씬의 전투 간격이 어떻게 되어 있든 그를 중앙선에
        // 위치시킬 수 있다.
        Vector3 p = originalSubjectPosition;
        p.x = cam.transform.position.x;
        centerWhileShowing.position = p;
    }

    private void RestoreSubject()
    {
        if (centerWhileShowing == null) return;

        centerWhileShowing.position = originalSubjectPosition;
    }

    private void HandlePlayPressed()
    {
        // 아래의 interactable 끄기는 두 번째 클릭은 막아도 두 번째 호출은 막지 못한다 - 런이
        // 두 번 시작되거나, 이미 비활성화한 오브젝트에 페이드 코루틴이 또 생겨선 안 된다.
        if (playPressed) return;
        playPressed = true;

        HideAndFadeOut();

        // 페이드 후가 아니라 전에 발동한다: 스테이지 카드가 이 화면 아래에서 불투명하게
        // 올라오므로, 게임플레이가 잠깐 번쩍이지 않고 곧바로 "Stage 1-1"로 넘어가 보인다.
        onPlay?.Invoke();
        onPlay = null;
    }

    // Play와 동일하게 숨기되 핸드오프 콜백은 뺀다 - 이미 자체 스폰 플로우를 진행하는
    // 진입점(디버그 스테이지 점프 패널)이 Play의 시작 절차 반복 없이 타이틀만 치울 때 쓴다.
    public void Dismiss()
    {
        if (playPressed) return;
        playPressed = true;

        onPlay = null;
        HideAndFadeOut();
    }

    private void HideAndFadeOut()
    {
        if (playButton != null) playButton.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 런 시작 전에 복원해서, StageManager 자체의 반복 토글 표시/숨김이 한 프레임 뒤에
        // 취소되는 게 아니라 최종 반영된다. 주인공도 같은 타이밍에 전투 자리로 돌아가는데,
        // 바로 이 프레임에 아래의 스테이지 카드가 불투명해지므로 이 이동은 보이지 않는다.
        RestoreHud();
        RestoreSubject();

        StartCoroutine(FadeOutAndHide());
    }

    private void HideHud()
    {
        if (hideWhileShowing == null) return;

        hiddenPriorState = new bool[hideWhileShowing.Length];
        for (int i = 0; i < hideWhileShowing.Length; i++)
        {
            if (hideWhileShowing[i] == null) continue;
            hiddenPriorState[i] = hideWhileShowing[i].activeSelf;
            hideWhileShowing[i].SetActive(false);
        }
    }

    private void RestoreHud()
    {
        if (hideWhileShowing == null || hiddenPriorState == null) return;

        for (int i = 0; i < hideWhileShowing.Length && i < hiddenPriorState.Length; i++)
        {
            if (hideWhileShowing[i] != null) hideWhileShowing[i].SetActive(hiddenPriorState[i]);
        }
        hiddenPriorState = null;
    }

    private IEnumerator FadeOutAndHide()
    {
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
