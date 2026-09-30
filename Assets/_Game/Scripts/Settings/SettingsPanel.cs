using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局设置面板：左侧分类 tab + 右侧内容，右下角是「随场景变」的动作按钮
///（Welcome = 退出游戏 / Lobby = 开始界面 / Game = 投降，见 RefreshSceneAction）。
///
/// 分类：显示 / 画质 / 界面 / 音频 / 游戏 / 社交与隐私 / 账号与数据
/// （后三类是 2026-09-30 预留的槽位，内容还在后续版本）。
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

    // ===================== 配色 / 尺寸（画布单位，参考分辨率 1920×1080） =====================

    // ===== 深蓝黑石面 + 金细线 =====
    // 与 Game 战场的 3D 棋盘同源（AGENTS.md「界面 / 场景美术方向」）：
    // 平底 + 一条金细线 + 只差 6–8 级的平板；不要倒角 / 内阴影 / 外发光。
    // 色值直接抄 AGENTS.md 的表，不另起一套。
    static readonly Color ColBackdrop = Hex("06090E", 0.88f);   // 遮罩：墨
    static readonly Color ColPanel    = Hex("0D141F");          // 外壳底（棋盘石面均色）
    static readonly Color ColPlate    = Hex("121B27");          // 左右两块底板（卡身底板亮端）
    static readonly Color ColBox      = Hex("1E2938");          // 数值框 / 选中的 tab（面板渐变亮端）
    static readonly Color ColBtn      = Hex("18222F");          // 步进按钮 / 未选中的 tab
    static readonly Color ColAccent   = Hex("C8A44A");          // 金
    static readonly Color ColAccentLit= Hex("E4CB84");          // 金亮（悬停 / 按下）
    static readonly Color ColAccentDim= Hex("8A6F2E");          // 暗金（细线）
    static readonly Color ColText     = Hex("D6C298");          // 正文：奶油
    static readonly Color ColSub      = Hex("8EA2B4");          // 次级文字：钢
    static readonly Color ColOff      = Hex("3A4657");          // 禁用态
    static readonly Color ColInk      = Hex("06090E");          // 压在金底上的字：墨

    // 通用关闭叉（与大厅那套弹窗同一件，Tools/cardframe/LobbyUIv1.ps1 出）
    const float CloseSize = 60f;   // 与大厅弹窗的通用叉同尺寸（LobbyUIBuilder.SubPanelCloseSize）
    const string CloseIconPath      = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/Icon_Close.png";
    const string CloseIconHoverPath = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/Icon_CloseHover.png";

    // ---- 尺寸（画布单位，参考分辨率 1920×1080）----
    // 面板「近乎全屏」：1920 留 80、1080 留 60。
    const float W = 1760f, H = 960f;
    const float Pad = 40f;          // 外壳内边距
    const float Inner = 20f;        // 底板内边距
    const float HeadBand = 128f;    // 标题带高度（标题 + 金细线）
    const float RowH = 66f, Gap = 8f;
    const float LabelW = 360f, StepW = 64f, ValueW = 520f;
    const float FootH = 96f;        // 底板底部留给按钮那一行的净空

    // ---- 左侧分类栏（单独一块底板，与主体分开）----
    // 内容区宽 = W - Pad*2 - TabW - TabGapW - Inner*2 = 1320。
    // 加分类：TabNames 里加一个名字 + BuildUI 里补一行 AddTab + 再写一个 Build*Tab。
    const float TabW = 300f, TabGapW = 20f;
    const float TabH = 64f;

    // ---- 开启动画 ----
    const float SlideFrom = -320f;  // 从下方多少像素滑上来
    const float SlideTime = 0.34f;

    /// <summary>#RRGGBB → Color（AGENTS.md 的色值直接抄进来）。</summary>
    static Color Hex(string hex, float a = 1f)
    {
        Color c = Color.white;
        ColorUtility.TryParseHtmlString(hex[0] == '#' ? hex : "#" + hex, out c);
        c.a = a;
        return c;
    }

    /// <summary>提亮（深色主题的悬停态）。</summary>
    static Color Brighten(Color c, float k)
    {
        return new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);
    }

    static readonly string[] TabNames =
        { "显示", "画质", "界面", "音频", "游戏", "社交与隐私", "账号与数据" };

    // ===================== 状态 =====================

    GameObject _root;
    bool _open;
    TMP_FontAsset _font;
    readonly List<Action> _refresh = new List<Action>();

    TextMeshProUGUI _vWindowMode, _vResolution, _vVSync, _vFrameCap, _vQuality, _vUiScale, _vMaster, _vSfx;
    TextMeshProUGUI _vBudget;

    // 左侧分类栏
    readonly List<Button> _tabs = new List<Button>();
    readonly List<TextMeshProUGUI> _tabTexts = new List<TextMeshProUGUI>();
    readonly List<GameObject> _tabBodies = new List<GameObject>();
    readonly List<Image> _tabBars = new List<Image>();
    int _activeTab = -1;

    // 底部场景动作（左下那颗随场景变的按钮）
    Button _sceneBtn;
    TextMeshProUGUI _sceneTxt;

    // 外观 / 开启动画
    RectTransform _prt;          // 面板本体（滑的就是它）
    CanvasGroup _group;
    Coroutine _slide;

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

        // —— 面板主体：近乎全屏的外壳 ——
        var panel = NewImage("Panel", transform, ColPanel);
        var prt = panel.rectTransform;
        _prt = prt;
        _group = panel.gameObject.AddComponent<CanvasGroup>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(W, H);

        var title = NewText("Title", prt, "设置", 46f, ColAccent, TextAlignmentOptions.Left);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
               new Vector2(Pad, -100f), new Vector2(-Pad, -36f));

        // 「设置」两个字的右下角：当前版本（小字，钢色）。量出标题实际宽度再往后排，字体换了也不会叠。
        var ver = NewText("Version", prt, CurrentVersionText(), 24f, ColSub, TextAlignmentOptions.BottomLeft);
        float titleW = title.GetPreferredValues("设置").x;
        if (!(titleW > 10f)) titleW = 46f * 2f;              // 兜底：46pt 两个汉字 ≈ 92
        Anchor(ver.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
               new Vector2(Pad + titleW + 14f, -92f), new Vector2(Pad + titleW + 14f + 260f, -58f));

        // 标题下一条金细线（AGENTS.md：平底 + 一条金细线）
        var rule = NewImage("Rule", prt, ColAccentDim);
        rule.raycastTarget = false;
        Anchor(rule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
               new Vector2(Pad, -110f), new Vector2(-Pad, -108f));

        // 右上角关闭：复用正式的叉 UI（Icon_Close + 悬停态 Icon_CloseHover）；取不到就退回文字 X
        var close = NewCloseButton(prt, () => SetOpen(false));
        var clrt = close.GetComponent<RectTransform>();
        clrt.anchorMin = clrt.anchorMax = new Vector2(1f, 1f);
        clrt.pivot = new Vector2(1f, 1f);
        clrt.anchoredPosition = new Vector2(-Pad, -30f);
        clrt.sizeDelta = new Vector2(CloseSize, CloseSize);   // 60：与大厅那套弹窗的叉同尺寸

        // —— 左侧分类栏：独立一块底板——
        var barPlate = NewImage("TabPlate", prt, ColPlate);
        Anchor(barPlate.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
               new Vector2(Pad, Pad), new Vector2(Pad + TabW, -HeadBand));

        var bar = new GameObject("Tabs", typeof(RectTransform));
        bar.layer = 5;
        bar.transform.SetParent(barPlate.transform, false);
        Anchor(bar.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
               new Vector2(Inner, Inner + FootH), new Vector2(-Inner, -Inner));   // 底部空给「恢复默认」
        var vlgBar = bar.AddComponent<VerticalLayoutGroup>();
        vlgBar.childAlignment = TextAnchor.UpperLeft;
        vlgBar.spacing = Gap;
        vlgBar.childControlWidth = true;
        vlgBar.childControlHeight = true;
        vlgBar.childForceExpandWidth = true;
        vlgBar.childForceExpandHeight = false;

        // —— 右侧主体：另一块底板（分开的两块）——
        var bodyPlate = NewImage("BodyPlate", prt, ColPlate);
        Anchor(bodyPlate.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
               new Vector2(Pad + TabW + TabGapW, Pad), new Vector2(-Pad, -HeadBand));

        var stage = new GameObject("Content", typeof(RectTransform));
        stage.layer = 5;
        stage.transform.SetParent(bodyPlate.transform, false);
        Anchor(stage.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
               new Vector2(Inner, Inner + FootH), new Vector2(-Inner, -Inner));

        BuildDisplayTab(AddTab(bar.transform, stage.transform, TabNames[0]));
        BuildGraphicsTab(AddTab(bar.transform, stage.transform, TabNames[1]));
        BuildInterfaceTab(AddTab(bar.transform, stage.transform, TabNames[2]));
        BuildAudioTab(AddTab(bar.transform, stage.transform, TabNames[3]));
        BuildGameTab(AddTab(bar.transform, stage.transform, TabNames[4]));
        BuildSocialTab(AddTab(bar.transform, stage.transform, TabNames[5]));
        BuildAccountTab(AddTab(bar.transform, stage.transform, TabNames[6]));
        SelectTab(0);

        // —— 恢复默认：左侧分类栏那块底板的左下角 ——
        var reset = NewButton("Reset", barPlate.transform, "恢复默认", 28f, ColBtn, ColText,
                              () => GameSettings.ResetToDefaults());
        var rrt = reset.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0f, 0f);
        rrt.anchorMax = new Vector2(1f, 0f);
        rrt.pivot = new Vector2(0.5f, 0f);
        rrt.anchoredPosition = new Vector2(0f, Inner);
        rrt.sizeDelta = new Vector2(-Inner * 2f, 60f);
        AddLabelFx(reset, ColText, ColAccent, ColAccentLit);

        // —— 场景动作（退出游戏 / 开始界面 / 投降）：右下角 ——
        var foot = new GameObject("Footer", typeof(RectTransform));
        foot.layer = 5;
        foot.transform.SetParent(bodyPlate.transform, false);
        Anchor(foot.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
               new Vector2(Inner, Inner), new Vector2(-Inner, Inner + 64f));

        _sceneBtn = NewButton("SceneAction", foot.transform, "", 28f, ColBtn, ColText, null);
        _sceneTxt = _sceneBtn.GetComponentInChildren<TextMeshProUGUI>();
        AddLabelFx(_sceneBtn, ColText, ColAccent, ColAccentLit);
        var srt = _sceneBtn.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 0.5f);
        srt.pivot = new Vector2(1f, 0.5f);
        srt.anchoredPosition = new Vector2(-2f, 0f);
        srt.sizeDelta = new Vector2(300f, 60f);

        RefreshSceneAction();

        GameSettings.Changed += RefreshAll;

        // 构建完立刻收起：静态入口（SettingsLauncher / SettingsPanel.Instance）可能在进场景时就创建面板，
        // 不收起的话会一进游戏就弹出来。
        _root.SetActive(false);
    }

    // ===================== 分类栏 =====================

    /// <summary>
    /// 建一个分类：左侧一颗 tab 按钮 + 右侧一块内容区（默认收起）。
    /// 以后往某个分类里加设置项，只改它自己的 Build*Tab，不碰其它分类。
    /// </summary>
    Transform AddTab(Transform bar, Transform stage, string name)
    {
        var btn = NewButton("Tab_" + name, bar, name, 30f, ColBtn, ColSub, null);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = TabH; le.preferredHeight = TabH; le.flexibleHeight = 0f;

        // 选中时左侧亮起来的一条金细线（没被选就藏起来）。
        var accent = NewImage("Accent", btn.transform, ColAccent);
        accent.raycastTarget = false;
        Anchor(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
               new Vector2(0f, 0f), new Vector2(5f, 0f));

        int index = _tabs.Count;
        btn.onClick.AddListener(() => SelectTab(index));
        _tabs.Add(btn);
        _tabTexts.Add(btn.GetComponentInChildren<TextMeshProUGUI>());
        _tabBars.Add(accent);

        var body = new GameObject("Tab_" + name, typeof(RectTransform));
        body.layer = 5;
        body.transform.SetParent(stage, false);
        Anchor(body.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var vlg = body.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = Gap;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        body.SetActive(false);
        _tabBodies.Add(body);
        return body.transform;
    }

    void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == _activeTab) return;
        _activeTab = index;

        for (int i = 0; i < _tabs.Count; i++)
        {
            bool on = i == index;
            _tabBodies[i].SetActive(on);
            _tabBars[i].enabled = on;

            var bg = on ? ColBox : ColBtn;
            var c = _tabs[i].colors;
            c.normalColor = bg;
            c.highlightedColor = Brighten(bg, 1.18f);   // 深色主题：悬停往亮里走
            c.pressedColor = new Color(bg.r * 0.84f, bg.g * 0.84f, bg.b * 0.84f, 1f);
            c.selectedColor = c.highlightedColor;
            _tabs[i].colors = c;

            _tabTexts[i].color = on ? ColAccent : ColSub;
        }
    }

    // ===================== 各分类内容 =====================
    // 分类标题就是 tab 上那颗字，内容区里不再重复一遍 BuildHeader。

    void BuildDisplayTab(Transform ct)
    {
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
    }

    void BuildGraphicsTab(Transform ct)
    {
        BuildStepper(ct, "画质档位", out _vQuality, out var pQ, out var nQ);
        pQ.onClick.AddListener(() => GameSettings.StepQuality(-1));
        nQ.onClick.AddListener(() => GameSettings.StepQuality(1));
        _refresh.Add(() =>
        {
            int i = Mathf.Clamp(GameSettings.QualityTier, 0, GameSettings.QualityNames.Length - 1);
            _vQuality.text = GameSettings.QualityNames[i];
        });

        BuildNote(ct, "后续版本提供：抗锯齿 / 阴影 / 粒子密度 / 卡面预加载开关。", 90f);
    }

    void BuildInterfaceTab(Transform ct)
    {
        BuildStepper(ct, "界面缩放", out _vUiScale, out var pU, out var nU);
        pU.onClick.AddListener(() => GameSettings.StepUiScale(-1));
        nU.onClick.AddListener(() => GameSettings.StepUiScale(1));
        _refresh.Add(() => _vUiScale.text = GameSettings.PercentLabel(GameSettings.UiScale));

        // 只读信息行：像素预算。显示当前屏幕下文字能拿到多少设备像素 —— 清晰与否看这一行，
        // 拖动 / 缩放窗口时由 ScreenSizeChanged → Changed → RefreshAll 自动重算。
        BuildInfo(ct, out _vBudget);
        _refresh.Add(() => _vBudget.text = GameSettings.DescribePixelBudget());

        BuildNote(ct, "后续版本提供：正文字号 / 悬停延迟 / 手牌显示方式 / 动画速度 / 减少动态（视差 · 闪烁 · 惯性）。", 130f);
    }

    void BuildAudioTab(Transform ct)
    {
        BuildStepper(ct, "主音量", out _vMaster, out var pM, out var nM);
        pM.onClick.AddListener(() => GameSettings.StepMasterVolume(-1));
        nM.onClick.AddListener(() => GameSettings.StepMasterVolume(1));
        _refresh.Add(() => _vMaster.text = GameSettings.PercentLabel(GameSettings.MasterVolume));

        BuildStepper(ct, "音效", out _vSfx, out var pS, out var nS);
        pS.onClick.AddListener(() => GameSettings.StepSfxVolume(-1));
        nS.onClick.AddListener(() => GameSettings.StepSfxVolume(1));
        _refresh.Add(() => _vSfx.text = GameSettings.PercentLabel(GameSettings.SfxVolume));

        BuildNote(ct, "后续版本提供：音乐音量（AudioManager 已有 musicVolume，只是还没接到这里）/ 语音音量。", 130f);
    }

    void BuildGameTab(Transform ct)
    {
        BuildNote(ct, "后续版本提供：\n· 默认卡组\n· 对战 AI 难度（轻松 / 普通 / 困难）\n· 确认弹窗倒计时时长\n· 快捷键（设置面板当前为 F10）\n· 重置新手教程", 240f);
    }

    void BuildSocialTab(Transform ct)
    {
        BuildNote(ct, "后续版本提供：\n· 好友申请开关\n· 对局邀请提醒\n· 在线状态可见性\n· 允许陌生人邀请\n· 黑名单入口", 240f);
    }

    void BuildAccountTab(Transform ct)
    {
        BuildNote(ct, "后续版本提供：\n· Steam 连接状态\n· 异界号（复制）\n· 云端存档开关\n· 导入 / 导出卡组\n· 清空本地数据", 240f);
    }

    /// <summary>灰色说明行（占位分类 / 未接入项用），会换行。</summary>
    void BuildNote(Transform parent, string text, float height)
    {
        var row = NewRow(parent, height);
        var t = NewText("Note", row, text, 26f, ColSub, TextAlignmentOptions.TopLeft);
        t.enableWordWrapping = true;
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -6f));
    }

    /// <summary>取一张 UI 贴图（Texture2D 口径，与大厅那套弹窗一致 —— Icon_Close 导入成 Default 而不是 Sprite）。
    /// 编辑器里直接读工程；打包后回落到 Resources/UI/&lt;名&gt;。</summary>
    static Texture2D LoadUiTex(string path, string resName)
    {
#if UNITY_EDITOR
        var t = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t != null) return t;
#endif
        return Resources.Load<Texture2D>("UI/" + resName);
    }

    /// <summary>右上角关闭：能取到 Icon_Close 就用它（RawImage + 悬停换贴图），拿不到退回文字 X。</summary>
    Button NewCloseButton(Transform parent, Action onClick)
    {
        var tex = LoadUiTex(CloseIconPath, "Icon_Close");
        if (tex == null) return NewButton("Close", parent, "X", 34f, ColBtn, ColText, onClick);

        var go = new GameObject("Close", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        var raw = go.AddComponent<RawImage>();
        raw.texture = tex;

        var hover = LoadUiTex(CloseIconHoverPath, "Icon_CloseHover");
        var swap = go.AddComponent<IconSwapHover>();
        swap.icon = raw;
        swap.normal = tex;
        swap.hover = hover != null ? hover : tex;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = raw;
        btn.transition = Selectable.Transition.None;   // 悬停 / 按下靠换贴图，不靠 ColorTint
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        return btn;
    }

    /// <summary>RawImage 版悬停换贴图（叉这类图标用）。</summary>
    class IconSwapHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RawImage icon;
        public Texture normal, hover;

        void OnDisable() { if (icon != null && normal != null) icon.texture = normal; }
        public void OnPointerEnter(PointerEventData e) { if (icon != null && hover != null) icon.texture = hover; }
        public void OnPointerExit(PointerEventData e)  { if (icon != null && normal != null) icon.texture = normal; }
    }

    /// <summary>给按钮挂上「悬停变金 / 按下更亮」的标签染色（底色仍归 Button 自己的 ColorTint 管）。</summary>
    void AddLabelFx(Button btn, Color idle, Color hover, Color pressed)
    {
        var fx = btn.gameObject.AddComponent<LabelTint>();
        fx.button = btn;
        fx.label = btn.GetComponentInChildren<TextMeshProUGUI>();
        fx.idle = idle;
        fx.hover = hover;
        fx.pressed = pressed;
    }

    /// <summary>标签染色用的极小组件：只改文字色，不碰按钮底色。</summary>
    class LabelTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                      IPointerDownHandler, IPointerUpHandler
    {
        public Button button;
        public TextMeshProUGUI label;
        public Color idle, hover, pressed;

        bool Live { get { return label != null && (button == null || button.interactable); } }

        void OnEnable() { if (label != null) label.color = idle; }

        public void OnPointerEnter(PointerEventData e) { if (Live) label.color = hover; }
        public void OnPointerExit(PointerEventData e)  { if (label != null) label.color = idle; }
        public void OnPointerDown(PointerEventData e)  { if (Live) label.color = pressed; }
        public void OnPointerUp(PointerEventData e)    { if (Live) label.color = hover; }
    }

    // ===================== 场景动作（左下角那颗） =====================

    /// <summary>按当前场景换那颗按钮：Welcome 退出游戏 / Lobby 开始界面 / Game 投降，其它场景隐藏。</summary>
    void RefreshSceneAction()
    {
        if (_sceneBtn == null) return;

        switch (SceneManager.GetActiveScene().name)
        {
            case "Welcome": SetSceneAction("退出游戏", SceneActionQuit);         break;
            case "Lobby":   SetSceneAction("开始界面", SceneActionBackToTitle);  break;
            case "Game":    SetSceneAction("投降",     SceneActionSurrender);    break;
            default:        _sceneBtn.gameObject.SetActive(false);               break;
        }
    }

    void SetSceneAction(string label, Action onClick)
    {
        _sceneTxt.text = label;
        _sceneBtn.onClick.RemoveAllListeners();
        _sceneBtn.onClick.AddListener(() => onClick());
        _sceneBtn.gameObject.SetActive(true);
    }

    void SceneActionQuit()
    {
        Debug.Log("[SettingsPanel] 退出游戏");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SceneActionBackToTitle()
    {
        SetOpen(false);
        var lobby = FindObjectOfType<LobbyManager>();
        if (lobby != null) { lobby.ReturnToWelcome(); return; }   // Lobby 走黑条扫屏
        SceneTransition.LoadScene("Welcome");
    }

    void SceneActionSurrender()
    {
        SetOpen(false);
        var sb = FindObjectOfType<SettingsButton>();
        if (sb != null) { sb.Surrender(); return; }               // 联机 / 离线两条路它自己分
        GameEndPanel.Instance?.OnPlayerDied(true);                // 兜底：离线直接结算
    }

    // ===================== 开关 =====================

    void SetOpen(bool open)
    {
        if (_open == open) return;
        _open = open;

        if (open)
        {
            RefreshSceneAction();   // 面板是 DontDestroyOnLoad，开启时场景可能已经换了
            RefreshAll();
            transform.SetAsLastSibling();
            if (_prt != null) _prt.anchoredPosition = new Vector2(0f, SlideFrom);
            if (_group != null) _group.alpha = 0.4f;
        }
        else if (_slide != null) { StopCoroutine(_slide); _slide = null; }

        if (_root != null) _root.SetActive(open);

        if (open) _slide = StartCoroutine(SlideIn());
    }

    // ===================== 开启动画 =====================

    /// <summary>从下方 SlideFrom 处往上滑到位（快起慢收，SlideTime 秒）。</summary>
    IEnumerator SlideIn()
    {
        float t = 0f;
        while (t < SlideTime)
        {
            t += Time.unscaledDeltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / SlideTime), 3f);
            if (_prt != null) _prt.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(SlideFrom, 0f, e));
            if (_group != null) _group.alpha = Mathf.LerpUnclamped(0.4f, 1f, e);
            yield return null;
        }

        if (_prt != null) _prt.anchoredPosition = Vector2.zero;
        if (_group != null) _group.alpha = 1f;
        _slide = null;
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

    void BuildStepper(Transform parent, string label,
                      out TextMeshProUGUI value, out Button prev, out Button next)
    {
        var row = NewRow(parent, RowH);

        var lab = NewText("Label", row, label, 32f, ColText, TextAlignmentOptions.Left);
        Anchor(lab.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
               new Vector2(8f, 0f), new Vector2(LabelW, 0f));

        var box = NewImage("Box", row, ColBox);
        var brt = box.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(StepW * 2f + ValueW, RowH - 10f);

        prev = NewButton("Prev", brt, "<", 32f, ColBtn, ColText, null);
        var prt = prev.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
        prt.pivot = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(3f, 0f);
        prt.sizeDelta = new Vector2(StepW - 6f, RowH - 16f);

        next = NewButton("Next", brt, ">", 32f, ColBtn, ColText, null);
        var nrt = next.GetComponent<RectTransform>();
        nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0.5f);
        nrt.pivot = new Vector2(1f, 0.5f);
        nrt.anchoredPosition = new Vector2(-3f, 0f);
        nrt.sizeDelta = new Vector2(StepW - 6f, RowH - 16f);

        value = NewText("Value", brt, "", 30f, ColText, TextAlignmentOptions.Center);
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
        // 深蓝黑主题：悬停往亮里走、按下往暗里走（白底那套「悬停压暗」在这里会糊成一团）。
        colors.highlightedColor = Brighten(bg, 1.18f);
        colors.pressedColor = new Color(bg.r * 0.84f, bg.g * 0.84f, bg.b * 0.84f, 1f);
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

    /// <summary>当前版本号：先读 Resources/version.txt（发版 workflow 会用 tag 覆写它），
    /// 编辑器里没有这个文件就退回场景里的 UpdateManager，最后退回 Application.version。</summary>
    static string CurrentVersionText()
    {
        var asset = Resources.Load<TextAsset>("version");
        if (asset != null && !string.IsNullOrWhiteSpace(asset.text)) return asset.text.Trim();

        var um = FindObjectOfType<UpdateManager>();
        if (um != null && !string.IsNullOrWhiteSpace(um.currentVersion)) return um.currentVersion.Trim();

        return Application.version;
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
