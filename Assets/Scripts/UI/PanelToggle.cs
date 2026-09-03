using UnityEngine;
using UnityEngine.UI;

public class PanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(Toggle);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(Toggle);
    }

    private void Toggle()
    {
        panel.SetActive(!panel.activeSelf);
    }
}
