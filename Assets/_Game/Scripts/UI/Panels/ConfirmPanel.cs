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

    /// <summary>确认弹窗是否正在展示（供 TurnButtonGate 判定"确认中禁止结束回合/抽牌"）。
    /// 用 panelRoot 的实际激活状态派生，Show/Hide 漏掉任何一条路径都不会残留锁。</summary>
    public bool IsShowing => panelRoot != null && panelRoot.activeInHierarchy;

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
        // AI 环境：不弹确认框，自动 onYes
        if (SimpleAI.IsAIEvaluating)
        {
            onYesCallback?.Invoke();
            return;
        }

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