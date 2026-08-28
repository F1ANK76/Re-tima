using System.Collections;
using UnityEngine;

// 패링 시도(방어 자세) 전체 동안 켜져 있는 방패 이펙트 - 성공 여부와 무관하게, 버튼을 누른
// 순간부터 표시된다. "성공했을 때만" 뜨는 이펙트가 아니다: 실제 성공 연출(반격 애니메이션,
// 텔레포트 이펙트)은 ParryManager.TryConsumeParry()가 별도로 재생한다.
public class ParryShieldEffect : MonoBehaviour
{
    [SerializeField] private float displayDuration = 0.8f;
    private Coroutine activeRoutine;

    public void Show()
    {
        gameObject.SetActive(true);
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        gameObject.SetActive(false);
        activeRoutine = null;
    }
}
