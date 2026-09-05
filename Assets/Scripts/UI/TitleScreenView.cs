using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    private Vector3 originalSubjectPosition;

    private void Awake()
    {
        // 맨 위에 그리기
        transform.SetAsLastSibling();

        canvasGroup = GetComponent<CanvasGroup>();

        originalSubjectPosition = centerWhileShowing.position;

        titleLabel.text = titleText;
        playButton.onClick.AddListener(HandlePlayPressed);
    }

    public void Show(Action playCallback)
    {
        onPlay = playCallback;
        playPressed = false;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        playButton.interactable = true;
        HideHud();
        CenterSubject();
        gameObject.SetActive(true);
    }

    private void CenterSubject()
    {
        Camera cam = Camera.main;

        Vector3 p = originalSubjectPosition;
        p.x = cam.transform.position.x;
        centerWhileShowing.position = p;
    }

    private void RestoreSubject()
    {
        centerWhileShowing.position = originalSubjectPosition;
    }

    private void HandlePlayPressed()
    {
        if (playPressed) return;      // 중복 클릭 차단
        playPressed = true;

        HideAndFadeOut();             // 타이틀 치우기 시작 (기다리지 않음)

        onPlay?.Invoke();             // ★ 게임 시작 — StageManager.BeginRun()
        onPlay = null;                // 콜백 버리기
    }

    public void Dismiss()
    {
        if (playPressed) return;
        playPressed = true;

        onPlay = null;
        HideAndFadeOut();
    }

    private void HideAndFadeOut()
    {
        playButton.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RestoreHud();
        RestoreSubject();

        StartCoroutine(FadeOutAndHide());
    }

    private void HideHud()
    {
        hiddenPriorState = new bool[hideWhileShowing.Length];
        for (int i = 0; i < hideWhileShowing.Length; i++)
        {
            hiddenPriorState[i] = hideWhileShowing[i].activeSelf;
            hideWhileShowing[i].SetActive(false);
        }
    }

    private void RestoreHud()
    {
        if (hiddenPriorState == null) return;

        for (int i = 0; i < hideWhileShowing.Length && i < hiddenPriorState.Length; i++)
        {
            hideWhileShowing[i].SetActive(hiddenPriorState[i]);
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
