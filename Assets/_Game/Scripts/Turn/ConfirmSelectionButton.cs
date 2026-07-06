using UnityEngine;
using UnityEngine.UI;

public class ConfirmSelectionButton : MonoBehaviour
{
    public static ConfirmSelectionButton Instance { get; private set; }

    private Button button;
    private CanvasGroup canvasGroup;
    private System.Action onConfirm;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        button.onClick.AddListener(OnClick);
        Hide();
    }

    void OnClick()
    {
        onConfirm?.Invoke();
        Hide();
    }

    public void Show(System.Action onConfirmCallback)
    {
        onConfirm = onConfirmCallback;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        onConfirm = null;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}