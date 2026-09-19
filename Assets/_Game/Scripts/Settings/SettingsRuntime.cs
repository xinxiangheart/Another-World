using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 设置运行时宿主：启动时应用一次玩家设置，之后负责
/// · F10 全局开关设置面板
/// · 监听屏幕尺寸变化（窗口被拖动 / 切换全屏 / 分辨率变更）→ 重算界面缩放
/// · 按宽高比给相机套 FOV（与画布同一条公式，见下）
///
/// 与 GPUOptimizer 的分工：GPUOptimizer 只做「不进设置的渲染开关」（MSAA / HDR）；
/// 画质档、帧率、垂直同步、窗口模式全部由 GameSettings 负责，两边不再互相覆盖。
///
/// 相机宽高比适配（2026-09-19）：画布的 matchWidthOrHeight 按宽高比切换（GameSettings.SafeMatch），
/// 相机必须切到同一根轴，否则 3D 棋盘与 UI 会在窄屏上错开：
/// · 宽屏（≥ 16:9）画布 Match Height（像素/单位 ∝ 屏幕高）→ 相机锁**垂直** FOV，场景原样；
/// · 窄屏（&lt; 16:9）画布 Match Width（像素/单位 ∝ 屏幕宽）→ 相机锁**水平** FOV，视野往上下长，
///   16:10 下 50° → 54.8°、16:9 以下越窄 FOV 越大，棋盘左右始终与改前逐像素一致。
///
/// 自动创建，无需挂到场景。
/// </summary>
public class SettingsRuntime : MonoBehaviour
{
    public static SettingsRuntime Instance { get; private set; }

    /// <summary>开关设置面板的全局热键（不与项目已有的 Esc / Ctrl+Shift+X 冲突）。</summary>
    public static KeyCode ToggleKey = KeyCode.F10;

    /// <summary>设计稿宽高比（16:9）。比它窄的屏锁水平视角，比它宽的屏锁垂直视角。</summary>
    public const float DesignAspect = 16f / 9f;

    int _lastWidth, _lastHeight;

    // 相机基准值：第一次见到某个相机时记下它场景里的设计值，之后每次都从基准值算，不会一层层叠加。
    // 相机随场景销毁重建，所以场景加载时清空 —— 否则复用的 instanceID 会读到上一个场景的基准。
    readonly Dictionary<int, float> _baseFov = new Dictionary<int, float>();
    readonly Dictionary<int, float> _baseOrthoSize = new Dictionary<int, float>();

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

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        GameSettings.Load();
        GameSettings.ApplyAll();

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        // 首个场景不会触发 sceneLoaded（订阅发生在它加载完之后），所以这里手动套一次。
        ApplyCameraAspect();

        // 启动时报一次像素预算：屏幕太小 / 窗口太小时一眼就能在 Console 里看到，
        // 不用去猜「为什么文字糊」。判据见 GameSettings.DescribePixelBudget()。
        Debug.Log("[清晰度] " + GameSettings.DescribePixelBudget());
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _baseFov.Clear();
        _baseOrthoSize.Clear();
        ApplyCameraAspect();
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
            ApplyCameraAspect();
        }
    }

    // ================= 相机宽高比适配 =================

    /// <summary>把当前宽高比套到场上所有相机上（幂等，只改 FOV / orthographicSize，不动位置与朝向）。</summary>
    void ApplyCameraAspect()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        var cams = UnityEngine.Object.FindObjectsOfType<Camera>();
        for (int i = 0; i < cams.Length; i++)
        {
            var cam = cams[i];
            if (cam == null) continue;

            int id = cam.GetInstanceID();
            float baseFov, baseSize;
            if (!_baseFov.TryGetValue(id, out baseFov))
            {
                // 场景里没有任何脚本写 fieldOfView，所以此刻读到的一定是设计值。
                baseFov = cam.fieldOfView;
                baseSize = cam.orthographicSize;
                _baseFov[id] = baseFov;
                _baseOrthoSize[id] = baseSize;
            }
            else
            {
                baseSize = _baseOrthoSize[id];
            }

            cam.fieldOfView = FittedFov(baseFov, aspect);
            if (cam.orthographic) cam.orthographicSize = FittedOrthoSize(baseSize, aspect);
        }
    }

    /// <summary>
    /// 由设计 FOV 与当前宽高比算出该用的 FOV：宽屏原样返回（锁垂直），窄屏把水平视角钉住（锁水平）。
    /// 水平半角 tan = tan(baseFov/2) × 16:9，与宽高比无关 —— 这正是画布 Match Width 的同一根轴。
    /// </summary>
    public static float FittedFov(float baseFov, float aspect)
    {
        if (aspect >= DesignAspect) return baseFov;
        float half = Mathf.Atan(Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad) * DesignAspect / aspect) * Mathf.Rad2Deg;
        return Mathf.Min(170f, 2f * half);      // 极窄窗口兜个底，免得 FOV 顶到 180
    }

    /// <summary>正交相机的同款换算：锁水平半宽（halfWidth = size × aspect）。</summary>
    public static float FittedOrthoSize(float baseSize, float aspect)
    {
        if (aspect >= DesignAspect) return baseSize;
        return baseSize * DesignAspect / aspect;
    }
}
