using UnityEngine;

/// <summary>
/// 设置运行时宿主：启动时应用一次玩家设置，之后负责
/// · F10 全局开关设置面板
/// · 监听屏幕尺寸变化（窗口被拖动 / 切换全屏 / 分辨率变更）→ 重算界面缩放
///
/// 与 GPUOptimizer 的分工：GPUOptimizer 只做「不进设置的渲染开关」（MSAA / HDR）；
/// 画质档、帧率、垂直同步、窗口模式全部由 GameSettings 负责，两边不再互相覆盖。
///
/// 自动创建，无需挂到场景。
/// </summary>
public class SettingsRuntime : MonoBehaviour
{
    public static SettingsRuntime Instance { get; private set; }

    /// <summary>开关设置面板的全局热键（不与项目已有的 Esc / Ctrl+Shift+X 冲突）。</summary>
    public static KeyCode ToggleKey = KeyCode.F10;

    int _lastWidth, _lastHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SettingsRuntime");
        go.AddComponent<SettingsRuntime>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameSettings.Load();
        GameSettings.ApplyAll();

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        // 启动时报一次像素预算：屏幕太小 / 窗口太小时一眼就能在 Console 里看到，
        // 不用去猜「为什么文字糊」。判据见 GameSettings.DescribePixelBudget()。
        Debug.Log("[清晰度] " + GameSettings.DescribePixelBudget());
    }

    void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) SettingsPanel.Toggle();

        // 窗口尺寸变化（拖动窗口 / 分辨率变更 / 切换显示器）→ 重算一次界面缩放
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            GameSettings.NotifyScreenSizeChanged();
        }
    }
}
