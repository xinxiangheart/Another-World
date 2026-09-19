using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>窗口模式。</summary>
public enum WindowModeSetting
{
    BorderlessFullscreen = 0,   // 无边框全屏（推荐：Alt+Tab / Steam Overlay 最稳）
    ExclusiveFullscreen = 1,    // 独占全屏
    Windowed = 2                // 窗口化
}

/// <summary>
/// 全局设置：显示 / 画质 / 界面缩放 / 音量。持久化走 PlayerPrefs。
///
/// 缩放口径（重要）：全项目 CanvasScaler 统一为 1920×1080，matchWidthOrHeight **按宽高比切换**（见 SafeMatch）：
/// · 宽屏（≥ 16:9）Match Height(1)：画布高恒为 1080、宽随宽高比变宽 —— 相机锁垂直 FOV，两者 像素/单位 都 ∝ 屏幕高；
/// · 窄屏（< 16:9）Match Width(0)：画布宽恒为 1920、高随宽高比变高 —— 相机改锁水平 FOV，两者 像素/单位 都 ∝ 屏幕宽。
/// 两种口径下 3D 棋盘与 UI 都走同一条公式，因此在各分辨率下保持相对位置不变
/// （此前 Match 0.5 在 16:10 会错开 5.4%，21:9 错开 13%）。
///
/// 2026-09-19 改：窄屏此前固定 Match Height，画布宽只有 1080 × 宽高比（16:10 → 1728 < 1920），
/// 居中锚定 + 固定宽度的元素（Game 场景右列四个按钮 x = 810、Setting x = -813 等）会被挤出屏幕。
/// 现在窄屏改用 Match Width，画布宽恒为 1920，1920×1080 设计稿**整幅**可见，多出来的余量转给屏幕高。
///
/// 所有 Apply* 都是幂等的，可重复调用。
/// </summary>
public static class GameSettings
{
    // ---- 设计基准 ----
    public const float BaseWidth = 1920f;
    public const float BaseHeight = 1080f;
    /// <summary>宽屏（≥ 16:9）的 matchWidthOrHeight；窄屏见 SafeMatch（会切到 0）。</summary>
    public const float BaseMatch = 1f;
    public static readonly Vector2 BaseReference = new Vector2(BaseWidth, BaseHeight);

    /// <summary>
    /// 画布的 matchWidthOrHeight —— **按宽高比切换**，保证 1920×1080 设计稿整幅可见。
    /// · 宽屏（≥ 16:9）→ 1（Match Height）：画布 1920×1080 起，宽度随宽高比变宽（16:9→1920、21:9→2580）；
    /// · 窄屏（&lt; 16:9）→ 0（Match Width）：画布宽恒为 1920，高度随宽高比变高（16:10→1200、4:3→1440）。
    /// 两种口径都只是「把多出来的余量给宽还是给高」，都不会再把内容挤出屏幕。
    /// 相机必须同步切换锁定轴（见 SettingsRuntime.FittedFov）—— 两边不同步就会 3D 与 UI 错位。
    /// </summary>
    public static float SafeMatch
    {
        get { return Screen.width * 9 >= Screen.height * 16 ? 1f : 0f; }
    }

    // ---- 清晰度基准（改这几个数之前先读 DescribePixelBudget 的注释）----
    /// <summary>设计稿正文字号（Lobby 主力字号）。文字清不清就看它在屏上占几个像素。</summary>
    public const float DesignBodyFontSize = 24f;
    /// <summary>字体图集烘焙点号，必须与 Assets/_Game/Fonts/*SDF.asset 的 m_PointSize 一致（重烘图集时要同步改这里）。</summary>
    public const float TextAtlasPointSize = 36f;

    // ---- 界面缩放范围 ----
    // 0.5 = 内容最小（4K 屏上不让 UI 放得失控）；1.5 = 内容最大。
    // 注意方向：**放大 UI 会让文字更清楚**（TMP 按设备像素重栅格化），但位图会被拉大；
    // 缩小 UI 则文字与位图一起变糊。判据见 DescribePixelBudget()。
    public const float UiScaleMin = 0.5f;
    public const float UiScaleMax = 1.5f;
    public const float UiScaleStep = 0.05f;

    // ---- PlayerPrefs 键 ----
    const string K_WindowMode = "aw.disp.windowMode";
    const string K_Width      = "aw.disp.width";
    const string K_Height     = "aw.disp.height";
    const string K_VSync      = "aw.disp.vsync";
    const string K_FrameCap   = "aw.disp.frameCap";
    const string K_Quality    = "aw.gfx.quality";
    const string K_UiScale    = "aw.ui.scale";
    const string K_Master     = "aw.audio.master";
    const string K_Sfx        = "aw.audio.sfx";

    // ---- 当前值 ----
    public static WindowModeSetting WindowMode { get; private set; }
    public static int Width { get; private set; }
    public static int Height { get; private set; }
    public static bool VSync { get; private set; }
    public static int FrameCap { get; private set; }        // 0 = 不限
    public static int QualityTier { get; private set; }     // 0 低 / 1 中 / 2 高 / 3 极高
    public static float UiScale { get; private set; }
    public static float MasterVolume { get; private set; }
    public static float SfxVolume { get; private set; }

    /// <summary>任意设置变化后触发（设置面板据此刷新文字）。</summary>
    public static event Action Changed;
    /// <summary>界面缩放变化后触发（画布参考分辨率已重算）。</summary>
    public static event Action UiScaleChanged;
    /// <summary>屏幕尺寸变化后触发（窗口被拖动缩放）。</summary>
    public static event Action ScreenSizeChanged;

    public static readonly int[] FrameCapOptions = { 30, 60, 120, 144, 0 };
    public static readonly string[] QualityNames = { "低", "中", "高", "极高" };
    public static readonly string[] WindowModeNames = { "无边框全屏", "独占全屏", "窗口化" };
    /// <summary>画质档 → QualitySettings 等级（VeryLow..Ultra 共 6 档）。</summary>
    static readonly int[] QualityLevels = { 2, 3, 4, 5 };

    static bool _loaded;
    static List<Vector2Int> _resolutions;

    /// <summary>任何场景 Awake 之前先把玩家设置读进来（音效音量等读取点依赖它）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad()
    {
        Load();
    }

    // ================= 读写 =================

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        WindowMode   = (WindowModeSetting)Mathf.Clamp(PlayerPrefs.GetInt(K_WindowMode, (int)WindowModeSetting.BorderlessFullscreen), 0, 2);
        Width        = PlayerPrefs.GetInt(K_Width, 0);
        Height       = PlayerPrefs.GetInt(K_Height, 0);
        VSync        = PlayerPrefs.GetInt(K_VSync, 0) == 1;
        FrameCap     = PlayerPrefs.GetInt(K_FrameCap, 60);
        QualityTier  = Mathf.Clamp(PlayerPrefs.GetInt(K_Quality, 3), 0, 3);
        UiScale      = Mathf.Clamp(PlayerPrefs.GetFloat(K_UiScale, 1f), UiScaleMin, UiScaleMax);
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(K_Master, 1f));
        SfxVolume    = Mathf.Clamp01(PlayerPrefs.GetFloat(K_Sfx, 1f));

        if (Width <= 0 || Height <= 0)
        {
            Width = Screen.currentResolution.width;
            Height = Screen.currentResolution.height;
        }
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(K_WindowMode, (int)WindowMode);
        PlayerPrefs.SetInt(K_Width, Width);
        PlayerPrefs.SetInt(K_Height, Height);
        PlayerPrefs.SetInt(K_VSync, VSync ? 1 : 0);
        PlayerPrefs.SetInt(K_FrameCap, FrameCap);
        PlayerPrefs.SetInt(K_Quality, QualityTier);
        PlayerPrefs.SetFloat(K_UiScale, UiScale);
        PlayerPrefs.SetFloat(K_Master, MasterVolume);
        PlayerPrefs.SetFloat(K_Sfx, SfxVolume);
        PlayerPrefs.Save();
    }

    // ================= 应用 =================

    /// <summary>启动 / 恢复默认后统一走这里。</summary>
    public static void ApplyAll(bool applyDisplay = true)
    {
        Load();
        ApplyQuality();
        ApplyFrameSettings();
        ApplyAudio();
        ApplyUiScale();
        if (applyDisplay) ApplyDisplay();
        Changed?.Invoke();
    }

    public static void ApplyDisplay(bool force = false)
    {
        Load();
        FullScreenMode target;
        switch (WindowMode)
        {
            case WindowModeSetting.ExclusiveFullscreen: target = FullScreenMode.ExclusiveFullScreen; break;
            case WindowModeSetting.Windowed:            target = FullScreenMode.Windowed; break;
            default:                                    target = FullScreenMode.FullScreenWindow; break;
        }

        int w = Width, h = Height;
        if (WindowMode != WindowModeSetting.Windowed)
        {
            // 全屏档跟随桌面分辨率，避免用窗口尺寸去顶全屏
            w = Screen.currentResolution.width;
            h = Screen.currentResolution.height;
        }

        bool need = force || Screen.fullScreenMode != target;
        if (WindowMode == WindowModeSetting.Windowed)
            need |= Screen.width != w || Screen.height != h;

        if (!need) return;

        // 窗口化：不指定刷新率（0/0 = 交给系统）；全屏：沿用桌面当前刷新率。
        //
        // 用 refreshRateRatio（分子/分母）而不是已废弃的 refreshRate（int）：
        // 后者是取整过的，59.94Hz 这类非整数刷新率会被读成 60，
        // 再去请求一个显示器并不支持的刷新率，SetResolution 就可能选错模式。
        // RefreshRate 只有 numerator / denominator 两个字段（这个 Unity 版本没有构造函数），
        // default = 0/0，在 Unity 内部就等于「未指定」，与旧代码传 int 0 的语义一致。
        RefreshRate refresh = WindowMode == WindowModeSetting.Windowed
            ? default
            : Screen.currentResolution.refreshRateRatio;

        Screen.SetResolution(w, h, target, refresh);
    }

    public static void ApplyQuality()
    {
        int tier = Mathf.Clamp(QualityTier, 0, QualityLevels.Length - 1);
        int level = QualityLevels[tier];
        if (QualitySettings.names != null && QualitySettings.names.Length > 0)
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(level, true);

        // SetQualityLevel 会用质量预设重写下面两项，所以必须放在它之后：
        // · 各向异性过滤打开 → 逐纹理 aniso（卡图 = 2）才真正生效，斜视卡面不糊
        // · MSAA=2 → 卡面与文字边缘不锯齿（2D 卡牌游戏的主要画质观感就在这）
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.antiAliasing = 2;
    }

    public static void ApplyFrameSettings()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = VSync ? -1 : (FrameCap > 0 ? FrameCap : -1);
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
    }

    /// <summary>
    /// 界面缩放：把每个"跟随屏幕尺寸"的 CanvasScaler 的参考分辨率除以缩放系数。
    /// matchWidthOrHeight 同时按当前宽高比重算一遍（宽屏 Match Height / 窄屏 Match Width，见 SafeMatch），
    /// 所以拖动窗口改变宽高比、切全屏都由这里兜住。
    /// World Space 画布（棋盘槽位）不参与，否则会改变世界尺寸。
    /// </summary>
    public static void ApplyUiScale()
    {
        float s = Mathf.Clamp(UiScale, UiScaleMin, UiScaleMax);
        var scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();
        for (int i = 0; i < scalers.Length; i++)
        {
            var cs = scalers[i];
            if (cs == null) continue;
            var canvas = cs.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) continue;
            if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;

            cs.referenceResolution = new Vector2(BaseWidth / s, BaseHeight / s);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = SafeMatch;
        }
        UiScaleChanged?.Invoke();
    }

    /// <summary>把一个 CanvasScaler 初始化成项目统一口径（供运行时新建的画布使用）。</summary>
    public static void ApplyScalerTo(CanvasScaler cs)
    {
        if (cs == null) return;
        float s = Mathf.Clamp(UiScale, UiScaleMin, UiScaleMax);
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(BaseWidth / s, BaseHeight / s);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = SafeMatch;
        cs.referencePixelsPerUnit = 100f;
    }

    // ================= 清晰度（像素预算） =================

    /// <summary>
    /// 画布倍率：一个「设计单位」最终会占多少设备像素。
    ///
    /// CanvasScaler 的 scaleFactor = 屏幕基准边长 / 参考边长，基准边长与 SafeMatch 同轴：
    /// 宽屏（Match Height）= Screen.height / (1080 / UiScale)，窄屏（Match Width）= Screen.width / (1920 / UiScale)。
    /// 两者都等于 ScreenScale × UiScale。
    ///
    /// 1.00 = 与 1920×1080 设计稿 1:1（最理想）；&lt;1 表示内容被压到设计稿以下，字会变小变糊；
    /// &gt;1 表示内容被放大（字更清楚，但位图会被拉大）。
    /// </summary>
    public static float RenderScale
    {
        get
        {
            Load();
            return ScreenScale * Mathf.Clamp(UiScale, UiScaleMin, UiScaleMax);
        }
    }

    /// <summary>
    /// 屏幕本身给出的倍率（不含界面缩放）：宽屏 = 屏幕高 / 1080，窄屏 = 屏幕宽 / 1920。
    /// 它同时是「3D 像素/世界单位」与「UI 像素/画布单位」的公共因子 —— 两边同轴，3D 与 UI 才不会错位。
    /// </summary>
    public static float ScreenScale
    {
        get { return SafeMatch > 0f ? Screen.height / BaseHeight : Screen.width / BaseWidth; }
    }

    /// <summary>
    /// 一行说明当前屏幕的「像素预算」—— 这是判断清不清晰的唯一依据，改分辨率 / 界面缩放前先看它。
    ///
    /// 文字（TMP SDF）：屏上像素高 = 设计字号 × RenderScale。
    ///   · 低于 24px：笔画被抗锯齿吃掉，发糊（汉字尤其明显）；
    ///   · 24~40px：甜点区；
    ///   · 高于字体图集的烘焙点号（36）太多：距离场细节不够，边缘重新发虚。
    ///   例：24pt 正文在 RenderScale = 1.0 → 24px（可用）、1.48 → 36px（最佳）、0.52 → 12px（糊）。
    ///
    /// 图案（位图）：屏幕像素 ≤ 源像素才不会被放大；缩小显示靠 mipmap + 双线性预过滤
    ///   （导入设置由 TextureImportSettingsGuard 保证），放大显示才用 Point。
    ///
    /// 编辑器里 Game view 的实际渲染分辨率 = 视图的逻辑尺寸 × 1（150% 显示缩放时被 1.5 倍放大到屏幕上），
    /// 所以「怎么调都糊」通常是 Game view 太小，而不是项目设置的问题 —— 见 AGENTS.md「清晰度」节。
    /// </summary>
    public static string DescribePixelBudget()
    {
        Load();
        float s = RenderScale;
        int px = Mathf.RoundToInt(DesignBodyFontSize * s);
        string verdict;
        if (px > 40)       verdict = "偏大";                       // 超过图集点号，距离场细节不够
        else if (px >= 28) verdict = "最佳";
        else if (px >= 24) verdict = "可用";
        else if (px >= 19) verdict = "偏小";                       // 不是被放大糊的，是画得小
        else               verdict = "太小";

        // 低于 24px 时顺手给出「要多大才够」：界面缩放 = 24 / (设计字号 × 屏幕高/1080)
        // 注意：这里量的是**尺寸**，不是糊不糊 —— 「先渲染再放大」那种糊只有编辑器 Game 视图会有，
        // 见 Tools/设置/Game 视图清晰度诊断（编辑器侧）。
        string need = string.Empty;
        if (px < 24)
        {
            int needPct = Mathf.CeilToInt(24f / (DesignBodyFontSize * ScreenScale) * 100f);
            need = string.Format("  (拉高窗口 或 界面缩放>={0}%)", needPct);
        }
        return string.Format("{0}x{1}  倍率 {2:0.00}x  24pt={3}px  {4}{5}",
                             Screen.width, Screen.height, s, px, verdict, need);
    }

    // ================= 修改入口（面板用） =================

    public static void SetWindowMode(int index)
    {
        Load();
        WindowMode = (WindowModeSetting)Mathf.Clamp(index, 0, 2);
        Save();
        ApplyDisplay(true);
        Changed?.Invoke();
    }

    public static void StepWindowMode(int dir)
    {
        Load();
        SetWindowMode(((int)WindowMode + dir + WindowModeNames.Length) % WindowModeNames.Length);
    }

    public static void SetResolution(int w, int h)
    {
        Load();
        Width = Mathf.Max(320, w);
        Height = Mathf.Max(240, h);
        Save();
        ApplyDisplay(true);
        Changed?.Invoke();
    }

    public static void StepResolution(int dir)
    {
        Load();
        var list = GetResolutionOptions();
        if (list.Count == 0) return;
        int i = list.FindIndex(r => r.x == Width && r.y == Height);
        if (i < 0) i = 0;
        i = ((i + dir) % list.Count + list.Count) % list.Count;
        SetResolution(list[i].x, list[i].y);
    }

    public static void SetVSync(bool on)
    {
        Load();
        VSync = on;
        Save();
        ApplyFrameSettings();
        Changed?.Invoke();
    }

    public static void StepFrameCap(int dir)
    {
        Load();
        int i = Array.IndexOf(FrameCapOptions, FrameCap);
        if (i < 0) i = 1;
        i = ((i + dir) % FrameCapOptions.Length + FrameCapOptions.Length) % FrameCapOptions.Length;
        FrameCap = FrameCapOptions[i];
        Save();
        ApplyFrameSettings();
        Changed?.Invoke();
    }

    public static void StepQuality(int dir)
    {
        Load();
        QualityTier = ((QualityTier + dir) % QualityNames.Length + QualityNames.Length) % QualityNames.Length;
        Save();
        ApplyQuality();
        Changed?.Invoke();
    }

    public static void StepUiScale(int dir)
    {
        Load();
        float s = Mathf.Round((UiScale + dir * UiScaleStep) * 100f) / 100f;
        if (s < UiScaleMin - 0.001f) s = UiScaleMax;     // 到头就环绕
        if (s > UiScaleMax + 0.001f) s = UiScaleMin;
        UiScale = Mathf.Clamp(s, UiScaleMin, UiScaleMax);
        Save();
        ApplyUiScale();
        Changed?.Invoke();
    }

    public static void StepMasterVolume(int dir)
    {
        Load();
        MasterVolume = Mathf.Clamp01(Mathf.Round((MasterVolume + dir * 0.05f) * 100f) / 100f);
        Save();
        ApplyAudio();
        Changed?.Invoke();
    }

    public static void StepSfxVolume(int dir)
    {
        Load();
        SfxVolume = Mathf.Clamp01(Mathf.Round((SfxVolume + dir * 0.05f) * 100f) / 100f);
        Save();
        Changed?.Invoke();
    }

    public static void ResetToDefaults()
    {
        WindowMode = WindowModeSetting.BorderlessFullscreen;
        Width = Screen.currentResolution.width;
        Height = Screen.currentResolution.height;
        VSync = false;
        FrameCap = 60;
        QualityTier = 3;
        UiScale = 1f;
        MasterVolume = 1f;
        SfxVolume = 1f;
        Save();
        ApplyQuality();
        ApplyFrameSettings();
        ApplyAudio();
        ApplyUiScale();
        ApplyDisplay(true);
        Changed?.Invoke();
    }

    // ================= 查询 =================

    /// <summary>可选分辨率列表（按宽高去重、从大到小）。</summary>
    public static List<Vector2Int> GetResolutionOptions()
    {
        if (_resolutions != null && _resolutions.Count > 0) return _resolutions;

        var list = new List<Vector2Int>();
        var seen = new HashSet<long>();
        var raw = Screen.resolutions;
        if (raw != null)
        {
            for (int i = 0; i < raw.Length; i++)
            {
                long key = ((long)raw[i].width << 32) | (uint)raw[i].height;
                if (seen.Add(key)) list.Add(new Vector2Int(raw[i].width, raw[i].height));
            }
        }

        if (list.Count == 0)
        {
            list.Add(new Vector2Int(3840, 2160));
            list.Add(new Vector2Int(3440, 1440));
            list.Add(new Vector2Int(2560, 1600));
            list.Add(new Vector2Int(2560, 1440));
            list.Add(new Vector2Int(1920, 1200));
            list.Add(new Vector2Int(1920, 1080));
            list.Add(new Vector2Int(1600, 900));
            list.Add(new Vector2Int(1366, 768));
            list.Add(new Vector2Int(1280, 720));
        }

        list.Sort((a, b) =>
        {
            int byArea = (b.x * b.y).CompareTo(a.x * a.y);
            return byArea != 0 ? byArea : b.x.CompareTo(a.x);
        });
        _resolutions = list;
        return list;
    }

    public static string ResolutionLabel()
    {
        Load();
        if (WindowMode != WindowModeSetting.Windowed)
            // 注意：分隔符用 ASCII "x" —— 字体图集（GameCharacters.txt）里没有 U+00D7「×」，用了会变豆腐块
            return string.Format("跟随桌面 {0}x{1}", Screen.currentResolution.width, Screen.currentResolution.height);
        return string.Format("{0}x{1}", Width, Height);
    }

    public static string FrameCapLabel()
    {
        Load();
        return FrameCap > 0 ? FrameCap.ToString() : "不限";
    }

    public static string PercentLabel(float v01)
    {
        return Mathf.RoundToInt(v01 * 100f) + "%";
    }

    /// <summary>屏幕尺寸变化（窗口拖动）后由 ScreenChangeWatcher 调用。</summary>
    public static void NotifyScreenSizeChanged()
    {
        ApplyUiScale();
        ScreenSizeChanged?.Invoke();
        Changed?.Invoke();
    }
}
