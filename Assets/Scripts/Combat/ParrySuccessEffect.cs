using System.Collections;
using UnityEngine;

public class ParrySuccessEffect : MonoBehaviour
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
