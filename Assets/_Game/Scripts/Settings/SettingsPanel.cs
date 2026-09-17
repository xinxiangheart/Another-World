using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 全局设置面板：显示 / 画质 / 界面 / 音频。
///
/// 设计要点
/// · 用代码在运行时构建 UGUI —— 不依赖编辑器、不占场景资源、**不改动任何既有 UI 的层级与布局**。
/// · 画布口径与全项目统一：ScreenSpaceOverlay + 1920×1080 + Match Height，
///   与主相机「垂直 FOV 固定」属于同一条缩放公式 —— 因此任何分辨率下都不偏移、不错位。
/// · DontDestroyOnLoad 单例，跨场景保留；首次 Open 时才构建。
///
/// 打开方式
///   SettingsLauncher —— 自动接线场景里名为 "Setting" 的按钮（Lobby 已预留该按钮）
///   SettingsRuntime  —— F10 全局热键
///   任意脚本         —— SettingsPanel.Open() / Close() / Toggle()
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    // ===================== 单例 / 入口 =====================

    static SettingsPanel _instance;

    public static SettingsPanel Instance
    {
        get
        {
            if (_instance == null) Create();
            return _instance;
        }
    }

    public static bool IsOpen { get { return _instance != null && _instance._open; } }

    public static void Open()   { Instance.SetOpen(true); }
    public static void Close()  { if (_instance != null) _instance.SetOpen(false); }
    public static void Toggle() { Instance.SetOpen(!Instance._open); }

    static void Create()
    {
        var found = FindObjectOfType<SettingsPanel>();
        if (found != null) { _instance = found; return; }

        var go = new GameObject("SettingsCanvas");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SettingsPanel>();
        _instance.BuildUI();
    }

    /// <summary>
    /// 往一个已经存在的面板里注入一个「游戏设置」按钮（Game 场景的投降面板用）。
    /// 只新增一个子物体，不动宿主面板的任何既有子物体与布局。
    /// </summary>
    public static void AttachButtonTo(GameObject hostPanel, string label = "游戏设置")
    {
        if (hostPanel == null) return;
        if (hostPanel.transform.Find("GameSettingsEntry") != null) return;
        Instance.InjectButton(hostPanel.transform, label);
    }

    // ===================== 配色 / 尺寸（画布单位，参考分辨率 1920×1080） =====================

    // 浅色（白底）主题：面板纯白，其余靠「灰阶差」而不是靠边框分层。
    // 遮罩保持深色 —— 看板/大厅背景本身接近纯白，没有深色压底就看不出面板边界。
    static readonly Color ColBackdrop = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color ColPanel    = new Color(1.000f, 1.000f, 1.000f, 1f);   // 面板底：纯白
    static readonly Color ColBox      = new Color(0.953f, 0.953f, 0.961f, 1f);   // 数值框：#F3F3F5
    static readonly Color ColBtn      = new Color(0.898f, 0.898f, 0.914f, 1f);   // 步进按钮：#E5E5E9
    static readonly Color ColAccent   = new Color(0.720f, 0.530f, 0.160f, 1f);   // 强调色：#B88729（标题 + 主按钮底）
    static readonly Color ColText     = new Color(0.130f, 0.125f, 0.145f, 1f);   // 正文：近黑
    static readonly Color ColSub      = new Color(0.450f, 0.440f, 0.480f, 1f);   // 次级文字 / 分组标题
    static readonly Color ColOff      = new Color(0.780f, 0.780f, 0.800f, 1f);   // 禁用态
    static readonly Color ColInk      = new Color(1.000f, 1.000f, 1.000f, 1f);   // 主按钮上的字（白字压强调色底）

    // 注入到别人面板里的入口按钮（Game 场景投降面板）用自己的一套：
    // 那个面板本身是深色的，跟着白底主题走会变成一块亮斑，视觉上很突兀。
    static readonly Color ColHostBtn  = new Color(0.235f, 0.224f, 0.263f, 1f);
    static readonly Color ColHostTxt  = new Color(0.925f, 0.906f, 0.878f, 1f);

    const float W = 1000f, H = 900f;
    const float Pad = 36f, RowH = 54f, HeadH = 38f, Gap = 6f;
    const float LabelW = 320f, StepW = 64f, ValueW = 320f;

    // ===================== 状态 =====================

    GameObject _root;
    bool _open;
    TMP_FontAsset _font;
    readonly List<Action> _refresh = new List<Action>();

    TextMeshProUGUI _vWindowMode, _vResolution, _vVSync, _vFrameCap, _vQuality, _vUiScale, _vMaster, _vSfx;
    TextMeshProUGUI _vBudget;

    // ===================== 构建 =====================

    void BuildUI()
    {
        EnsureEventSystem();
        _font = ResolveFont();

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        GameSettings.ApplyScalerTo(gameObject.AddComponent<CanvasScaler>());
        gameObject.AddComponent<GraphicRaycaster>();
        _root = gameObject;

        // —— 遮罩：挡住下层输入；点击空白处关闭 ——
        var blocker = NewImage("Blocker", transform, ColBackdrop);
        Fill(blocker.rectTransform);
        var blockerBtn = blocker.gameObject.AddComponent<Button>();
        blockerBtn.transition = Selectable.Transition.None;
        blockerBtn.onClick.AddListener(() => SetOpen(false));

        // —— 面板主体 ——
        var panel = NewImage("Panel", transform, ColPanel);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(W, H);

        var title = NewText("Title", prt, "设置", 46f, ColAccent, TextAlignmentOptions.Left);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
               new Vector2(Pad, -92f), new Vector2(-Pad, -28f));

        var close = NewButton("Close", prt, "X", 34f, ColBtn, ColText, () => SetOpen(false));
        var clrt = close.GetComponent<RectTransform>();
        clrt.anchorMin = clrt.anchorMax = new Vector2(1f, 1f);
        clrt.pivot = new Vector2(1f, 1f);
        clrt.anchoredPosition = new Vector2(-28f, -28f);
        clrt.sizeDelta = new Vector2(56f, 56f);

        // —— 内容区（纵向排布） ——
        var content = new GameObject("Content", typeof(RectTransform));
        content.layer = 5;
        content.transform.SetParent(prt, false);
        Anchor(content.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
               new Vector2(Pad, Pad), new Vector2(-Pad, -104f));
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = Gap;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var ct = content.transform;

        // ---------- 显示 ----------
        BuildHeader(ct, "显示");

        BuildStepper(ct, "窗口模式", out _vWindowMode, out var pWin, out var nWin);
        pWin.onClick.AddListener(() => GameSettings.StepWindowMode(-1));
        nWin.onClick.AddListener(() => GameSettings.StepWindowMode(1));
        _refresh.Add(() =>
        {
            int i = Mathf.Clamp((int)GameSettings.WindowMode, 0, GameSettings.WindowModeNames.Length - 1);
            _vWindowMode.text = GameSettings.WindowModeNames[i];
        });

        BuildStepper(ct, "分辨率", out _vResolution, out var pRes, out var nRes);
        pRes.onClick.AddListener(() => GameSettings.StepResolution(-1));
        nRes.onClick.AddListener(() => GameSettings.StepResolution(1));
        _refresh.Add(() =>
        {
            bool windowed = GameSettings.WindowMode == WindowModeSetting.Windowed;
            _vResolution.text = GameSettings.ResolutionLabel();
            _vResolution.color = windowed ? ColText : ColSub;
            pRes.interactable = windowed;
            nRes.interactable = windowed;
        });

        BuildStepper(ct, "垂直同步", out _vVSync, out var pVs, out var nVs);
        pVs.onClick.AddListener(() => GameSettings.SetVSync(false));
        nVs.onClick.AddListener(() => GameSettings.SetVSync(true));
        _refresh.Add(() =>
        {
            _vVSync.text = GameSettings.VSync ? "开" : "关";
        });

        BuildStepper(ct, "帧率上限", out _vFrameCap, out var pFps, out var nFps);
        pFps.onClick.AddListener(() => GameSettings.StepFrameCap(-1));
        nFps.onClick.AddListener(() => GameSettings.StepFrameCap(1));
        _refresh.Add(() =>
        {
            _vFrameCap.text = GameSettings.FrameCapLabel();
            bool on = !GameSettings.VSync;
            _vFrameCap.color = on ? ColText : ColSub;
            pFps.interactable = nFps.interactable = on;
        });

        // ---------- 画质 ----------
        BuildHeader(ct, "画质");

        BuildStepper(ct, "画质档位", out _vQuality, out var pQ, out var nQ);
        pQ.onClick.AddListener(() => GameSettings.StepQuality(-1));
        nQ.onClick.AddListener(() => GameSettings.StepQuality(1));
        _refresh.Add(() =>
        {
            int i = Mathf.Clamp(GameSettings.QualityTier, 0, GameSettings.QualityNames.Length - 1);
            _vQuality.text = GameSettings.QualityNames[i];
        });

        // ---------- 界面 ----------
        BuildHeader(ct, "界面");

        BuildStepper(ct, "界面缩放", out _vUiScale, out var pU, out var nU);
        pU.onClick.AddListener(() => GameSettings.StepUiScale(-1));
        nU.onClick.AddListener(() => GameSettings.StepUiScale(1));
        _refresh.Add(() => _vUiScale.text = GameSettings.PercentLabel(GameSettings.UiScale));

        // 只读信息行：像素预算。显示当前屏幕下文字能拿到多少设备像素 —— 清晰与否看这一行，
        // 拖动 / 缩放窗口时由 ScreenSizeChanged → Changed → RefreshAll 自动重算。
        BuildInfo(ct, out _vBudget);
        _refresh.Add(() => _vBudget.text = GameSettings.DescribePixelBudget());

        // ---------- 音频 ----------
        BuildHeader(ct, "音频");

        BuildStepper(ct, "主音量", out _vMaster, out var pM, out var nM);
        pM.onClick.AddListener(() => GameSettings.StepMasterVolume(-1));
        nM.onClick.AddListener(() => GameSettings.StepMasterVolume(1));
        _refresh.Add(() => _vMaster.text = GameSettings.PercentLabel(GameSettings.MasterVolume));

        BuildStepper(ct, "音效", out _vSfx, out var pS, out var nS);
        pS.onClick.AddListener(() => GameSettings.StepSfxVolume(-1));
        nS.onClick.AddListener(() => GameSettings.StepSfxVolume(1));
        _refresh.Add(() => _vSfx.text = GameSettings.PercentLabel(GameSettings.SfxVolume));

        // ---------- 底部按钮 ----------
        var foot = NewRow(ct, 60f);
        var reset = NewButton("Reset", foot, "恢复默认", 26f, ColBtn, ColText, () => GameSettings.ResetToDefaults());
        var rr = reset.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = new Vector2(0f, 0.5f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.anchoredPosition = new Vector2(4f, 0f);
        rr.sizeDelta = new Vector2(220f, 52f);

        var done = NewButton("Done", foot, "关闭", 26f, ColAccent, ColInk, () => SetOpen(false));
        var dr = done.GetComponent<RectTransform>();
        dr.anchorMin = dr.anchorMax = new Vector2(1f, 0.5f);
        dr.pivot = new Vector2(1f, 0.5f);
        dr.anchoredPosition = new Vector2(-4f, 0f);
        dr.sizeDelta = new Vector2(200f, 52f);

        GameSettings.Changed += RefreshAll;

        // 构建完立刻收起：静态入口（SettingsLauncher / SettingsPanel.Instance）可能在进场景时就创建面板，
        // 不收起的话会一进游戏就弹出来。
        _root.SetActive(false);
    }

    /// <summary>把「游戏设置」按钮挂到宿主面板底部（不动宿主的任何既有子物体）。</summary>
    void InjectButton(Transform host, string label)
    {
        if (_font == null) _font = ResolveFont();

        var btn = NewButton("GameSettingsEntry", host, label, 28f, ColHostBtn, ColHostTxt,
                            () => SettingsPanel.Open());
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(240f, 90f);
        rt.SetAsLastSibling();
        Debug.Log("[SettingsPanel] 已把「" + label + "」按钮挂到 " + host.name + " 底部");
    }

    // ===================== 开关 =====================

    void SetOpen(bool open)
    {
        if (_open == open) return;
        _open = open;
        if (_root != null) _root.SetActive(open);
        if (open)
        {
            RefreshAll();
            transform.SetAsLastSibling();
        }
    }

    void OnEnable()
    {
        RefreshAll();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= RefreshAll;
        if (_instance == this) _instance = null;
    }

    void RefreshAll()
    {
        for (int i = 0; i < _refresh.Count; i++)
        {
            try { _refresh[i](); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // ===================== 子控件工厂 =====================

    void BuildHeader(Transform parent, string text)
    {
        var row = NewRow(parent, HeadH);
        var t = NewText("Header", row, text, 26f, ColSub, TextAlignmentOptions.Left);
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -8f));
    }

    void BuildStepper(Transform parent, string label,
                      out TextMeshProUGUI value, out Button prev, out Button next)
    {
        var row = NewRow(parent, RowH);

        var lab = NewText("Label", row, label, 30f, ColText, TextAlignmentOptions.Left);
        Anchor(lab.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
               new Vector2(8f, 0f), new Vector2(LabelW, 0f));

        var box = NewImage("Box", row, ColBox);
        var brt = box.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(StepW * 2f + ValueW, RowH - 10f);

        prev = NewButton("Prev", brt, "<", 30f, ColBtn, ColText, null);
        var prt = prev.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
        prt.pivot = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(3f, 0f);
        prt.sizeDelta = new Vector2(StepW - 6f, RowH - 16f);

        next = NewButton("Next", brt, ">", 30f, ColBtn, ColText, null);
        var nrt = next.GetComponent<RectTransform>();
        nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0.5f);
        nrt.pivot = new Vector2(1f, 0.5f);
        nrt.anchoredPosition = new Vector2(-3f, 0f);
        nrt.sizeDelta = new Vector2(StepW - 6f, RowH - 16f);

        value = NewText("Value", brt, "", 28f, ColText, TextAlignmentOptions.Center);
        Anchor(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(StepW, 0f), new Vector2(-StepW, 0f));
    }

    /// <summary>只读信息行：像素预算（当前屏幕下 24pt 正文能拿到多少设备像素 + 档位）。</summary>
    void BuildInfo(Transform parent, out TextMeshProUGUI value)
    {
        // 高度取 34：内容区总高 760（面板 900 - 上下内边距），加上这一行后仍有余量，不会把底部按钮顶出面板。
        var row = NewRow(parent, RowH - 20f);
        value = NewText("Info", row, "", GameSettings.DesignBodyFontSize, ColSub, TextAlignmentOptions.Left);
        Anchor(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
    }

    RectTransform NewRow(Transform parent, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;
        return go.GetComponent<RectTransform>();
    }

    Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
                           Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Button NewButton(string name, Transform parent, string label, float fontSize,
                     Color bg, Color labelColor, Action onClick)
    {
        var img = NewImage(name, parent, Color.white);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        var colors = btn.colors;
        colors.normalColor = bg;
        // 白底主题：悬停 / 按下都往暗里走。
        // （深色主题那套「乘 1.45 提亮」在白底上会直接顶到纯白，按钮一悬停就消失。）
        colors.highlightedColor = new Color(bg.r * 0.94f, bg.g * 0.94f, bg.b * 0.94f, 1f);
        colors.pressedColor = new Color(bg.r * 0.86f, bg.g * 0.86f, bg.b * 0.86f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = ColOff;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        var t = NewText("Label", img.rectTransform, label, fontSize, labelColor, TextAlignmentOptions.Center);
        Fill(t.rectTransform);

        if (onClick != null) btn.onClick.AddListener(() => onClick());
        return btn;
    }

    // ===================== 工具 =====================

    static void Fill(RectTransform rt)
    {
        Anchor(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        DontDestroyOnLoad(go);
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    /// <summary>取一个「认中文」的字体：优先用场景里已在用的字体，其次 Resources，最后 TMP 默认。</summary>
    static TMP_FontAsset ResolveFont()
    {
        var all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            var f = t.font;
            if (f != null && f.HasCharacter('设')) return f;
        }

        var res = Resources.Load<TMP_FontAsset>("Fonts/NotoSerifCJKsc-Regular SDF");
        if (res != null) return res;

        return TMP_Settings.defaultFontAsset;
    }
}
