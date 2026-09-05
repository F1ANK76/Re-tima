using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class CancellableBannerView : MonoBehaviour
{
    protected CanvasGroup canvasGroup;
    protected Coroutine routine;

    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
