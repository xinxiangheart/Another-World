using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConfirmPanel : MonoBehaviour
{
    public static ConfirmPanel Instance { get; private set; }

    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public Button yesButton;
    public Button noButton;

    private System.Action onYes;
    private System.Action onNo;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        panelRoot.SetActive(false);
    }

    void Start()
    {
        yesButton.onClick.AddListener(() =>
        {
            onYes?.Invoke();
            Hide();
        });
        noButton.onClick.AddListener(() =>
        {
            onNo?.Invoke();
            Hide();
        });
    }

    public void Show(string message, System.Action onYesCallback, System.Action onNoCallback = null)
    {
        titleText.text = message;
        onYes = onYesCallback;
        onNo = onNoCallback;
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }
}