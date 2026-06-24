using UnityEngine;
using UnityEngine.UI;

public class ReplaceOrAttachModal : MonoBehaviour
{
    // 1. 使用静态属性，确保 Instance 在任何时候被访问都能被正确找到
    public static ReplaceOrAttachModal Instance
    {
        get
        {
            if (_instance == null)
            {
                // 如果内存里没有，就全场景找一个，并直接激活它
                _instance = FindObjectOfType<ReplaceOrAttachModal>(true);
                if (_instance != null)
                {
                    // 强制激活，确保 Awake 能执行，Instance 能被赋值
                    _instance.gameObject.SetActive(true);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }
    private static ReplaceOrAttachModal _instance;

    [Header("UI组件")]
    public GameObject panelRoot;
    public Button replaceButton;
    public Button attachButton;

    private System.Action onReplaceSelected;
    private System.Action onAttachSelected;
    private bool isListenersReady = false;

    void Awake()
    {
        // 2. 终极的单例保护：允许覆盖，解决物体被重新激活后 Instance 丢失的问题
        _instance = this;
        panelRoot.SetActive(false);

        // 在 Awake 里直接绑定监听器是最安全的，因为无论物体是动态创建还是静态放置，Awake 都会在 SetActive(true) 时执行
        BindListeners();
    }

    void Start()
    {
        // 作为双重保险，如果 Awake 没执行，这里再尝试一次
        if (_instance == null)
        {
            _instance = this;
        }
        BindListeners();
    }

    // 确保按钮监听器只被绑定一次
    private void BindListeners()
    {
        if (isListenersReady) return;
        isListenersReady = true;

        replaceButton.onClick.RemoveAllListeners();
        attachButton.onClick.RemoveAllListeners();

        replaceButton.onClick.AddListener(() =>
        {
            onReplaceSelected?.Invoke();
            Hide();
        });
        attachButton.onClick.AddListener(() =>
        {
            onAttachSelected?.Invoke();
            Hide();
        });
    }

    public void Show(System.Action onReplace, System.Action onAttach)
    {
        // 3. 终极调试日志，帮你一秒定位问题
        if (panelRoot == null) Debug.LogError("致命错误：panelRoot 未绑定！");
        if (replaceButton == null) Debug.LogError("致命错误：replaceButton 未绑定！");
        if (attachButton == null) Debug.LogError("致命错误：attachButton 未绑定！");

        onReplaceSelected = onReplace;
        onAttachSelected = onAttach;
        panelRoot.SetActive(true);

        // 如果面板被激活了，但监听器还没绑定（可能Awake没有正确执行），这里强制绑定
        BindListeners();

        // 4. 将面板移到Canvas的最顶层，防止被其他UI遮挡
        panelRoot.transform.SetAsLastSibling();
        Debug.Log($"弹窗已显示。Instance是否为空: {Instance == null}");
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }
}