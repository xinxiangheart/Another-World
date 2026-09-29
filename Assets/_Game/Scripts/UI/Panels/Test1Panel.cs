using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game 场景的卡牌详情面板 —— 悬停 2D 手牌（<see cref="CardHover"/>）、或右键 / 在 3D 卡牌上停留
/// 一会儿（<see cref="Card3DHover.hoverDetailDelay"/>）时弹出。
/// </summary>
/// <remarks>2026-09-29 重做。用户原话：「将 Game 场景里的详情面板从之前的一个文本框变为和 lobby
/// 场景里的卡牌点击后的详情类似」。版面照大厅那份（<c>Assets/_Game/Scripts/UI/Lobby/LobbyCardDetailPanel.cs</c>）：
/// 左边一张放大的卡面、右边一栏一块自己的底板。四条差别都是这次点名的：
///
/// ① **不遮战场十二格**：卡面贴最左（中心 <see cref="FaceCx"/>）、属性栏贴最右（左沿 <see cref="ColL"/>），
///    中间那条整条让给 <c>BoardSlot</c> 的 12 个格子。十二格在 1920x1080 下的屏幕占地实测
///    x 约 640–1280、y（自上往下）约 125–955 —— 卡面右沿 376、属性栏左沿 1332，两边分别让开 264 / 52 画布单位。
/// ② **不做任何模糊**：大厅那份的幕是 <c>AnotherWorld/UIScrimBlur</c>（GrabPass + 9x9 高斯），这里一律不用。
///    <see cref="ScrimAlpha"/> 默认 **0**（连平底压暗都不铺）；调大就是一个平底压暗，**仍然不模糊**。
/// ③ **几乎是瞬间，但留一下收势**：不排 <c>LobbyCardsIntro</c> 那套滑入（大厅是 0.30 s + 逐栏错开 0.055 s），
///    只留 <see cref="IntroDur"/> = **0.16 s** 的一次收势：整体淡入 0.09 s 就满不透明，
///    卡面从左、右栏从右各移进来 <see cref="IntroSlide"/>，卡面另带 0.92 -> 1 的缩放。
/// ④ **触发沿用 Game 原样**：<see cref="Show"/> / <see cref="Hide"/> / <see cref="RefreshIfOpen"/>
///    三个入口签名一字未改 —— 2D 手牌悬停、3D 停留 / 右键、选定面板（<c>CardDisplayPanel</c>）、
///    板面刷新（<c>BoardSlot</c> / <c>BoardSyncManager</c>）全部照旧。
///
/// 数据取的是**实时实例**（<see cref="CardInstance"/>），不是模板值：受伤 / 减费 / 增益 / 赋予特性 /
/// 状态 / 附着物都跟着当前局面走（旧的文本框就是这么取的，不能退回模板）。
///
/// 面板全程 <c>raycastTarget = false</c>（旧件就是这么做的）：它是悬停提示，不许拦射线 ——
/// 目标选择时鼠标要能穿透到下面的格子。
/// </remarks>
public class Test1Panel : MonoBehaviour
{
    public static Test1Panel Instance { get; private set; }

    [Header("场景里的旧件（保留引用：新布局在第一帧把它们顶掉）")]
    public GameObject panelRoot;
    public TextMeshProUGUI infoText;

    /// <summary>当前正在显示的卡片实例，板面同步时用它重画。</summary>
    CardInstance _cachedInstance;

    // ═══════════════════════════════════════════════════════════════════════
    // 几何（**画布单位** · 1920x1080 参考分辨率；Canvas = CardCanvas / ScreenSpaceCamera）
    // 1 画布单位 = scaleFactor 个屏幕 px（实测 1475x830 窗口下 0.7682，与大厅那份同口径）。
    // 战场 12 格在画布上的占地实测 x 640.3-1279.7：卡面右沿 376、右栏左沿 1332，中间整条留给棋盘。
    // ═══════════════════════════════════════════════════════════════════════
    const float FaceScale = 3.36f;     // 卡面放大倍数（= 大厅那份 4.2 x 0.8；83.33x146.33 -> 280x491.7）
                                       // 2026-09-29 用户：「左边显示的卡牌大小整体缩小至 0.8 倍」
    const float FaceCx    = 236f;      // 卡面中心 x —— 0.8 倍后左沿落在 96（卡面实宽 280）
    const float FaceCy    = 406f;      // 卡面中心 y —— 上沿落在 160，与右栏上沿（ColT）同一条线
                                       // 2026-09-29 用户：「左侧卡牌缩小后更靠近左上」
    const float ColL      = 1332f;     // 属性栏左沿（右沿 1332 + 524 = 1856，距屏幕右边 64）
    const float ColT      = 160f;      // 属性栏上沿（自上往下）
    const float ColW      = 524f;      // 比大厅那份窄 36：左沿右移 36 给战场十二格让位，右沿 1856 不动
    const float ColH      = 792f;

    // ── 入场（2026-09-29：用户要「虽说快速但仍应有部分动画」）────────────────
    // 0.16 s 走完，且 0.09 s 就满不透明 ⇒ 观感仍是「瞬间弹出」，只是带一下收势。
    const float IntroDur    = 0.16f;   // 入场总时长（秒）—— 别超过 ~0.2，否则就不像「瞬间」了
    const float IntroFade   = 0.09f;   // 整体淡入到不透明的时刻
    const float IntroDelayC = 0.04f;   // 右栏比卡面晚一点起手
    const float IntroSlide  = 20f;     // 各自从外向内移进来的距离（画布单位）
    const float IntroScale  = 0.92f;   // 卡面起始缩放（相对最终）

    const float CellH     = 62f;       // 属性行高
    const float RowGap    = 14f;       // 行距
    const float SecGap    = 22f;       // 「属性」与「特性」两段之间额外多出来的一点
    const float ColGapX   = 10f;       // 同一栏两格之间的间隔
    const float CellMinW  = 160f;      // 一格的宽按自己的内容算，再短也给这么宽
    const float CellMaxW  = 380f;      // 再长也封顶
    const float CellPadR  = 16f;       // 值右边留的内缩
    const float LabelX    = 16f;       // 属性名左内缩
    const float LabelGap  = 18f;       // 名与值之间的间隔
    const float CellFont  = 23f;
    const float LabelFont = 18f;
    // 特性：底板贴着文字（与 3D 卡牌悬停标签 HoverTagLabel 同口径）—— 单行放得下就紧贴文字，
    // 放不下按 TraitMaxW 折行、底板同时长高。
    const float TraitFont = 21f, TraitPadX = 16f, TraitPadY = 14f, TraitHMin = 62f, TraitMaxW = 500f;

    const float CardW = 83.33f;        // = Card00_New_2D.prefab 根 RectTransform 的 sizeDelta
    const float CardH = 146.33f;

    /// <summary>全屏压暗幕的 alpha。<b>0 = 不铺</b>（默认：悬停提示不遮战场十二格）。
    /// 调大就多一层平底压暗 —— <b>不是模糊</b>，大厅那份的高斯 GrabPass 幕这里一律不用。</summary>
    const float ScrimAlpha = 0f;

    // ── 色（照 AGENTS「界面 / 场景美术方向」那张表，不另起一套）──────────────
    static readonly Color PlateColor = new Color32(21, 29, 41, 235);    // 与大厅底板中心同色（#151D29）
    static readonly Color Cream      = new Color32(240, 232, 210, 255);
    static readonly Color Gold       = new Color32(228, 203, 132, 255);
    static readonly Color Steel      = new Color32(142, 162, 180, 255);

    bool _built;
    CanvasGroup _group;
    float _introT = 999f;      // >= IntroDur = 没在动画；Show 时归 0 重播
    GameObject _face;
    RectTransform _faceHolder, _content;
    TMP_FontAsset _font;

    struct Cell
    {
        public string label, value;
        public Color color;
        public Cell(string l, string v, Color c) { label = l; value = v; color = c; }
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 悬停提示不拦射线：目标选择时让悬停 / 点击穿透到下方格子（旧行为，照旧）
        if (panelRoot != null)
        {
            foreach (var g in panelRoot.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }
        Hide();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // 入场动画：全程用 unscaledDeltaTime（择牌 / 展示把 timeScale 压到 0 时也能走完）。
    // 走完就直接 return，每帧开销就是一次比较。
    void Update()
    {
        if (_introT >= IntroDur) return;
        _introT += Time.unscaledDeltaTime;
        ApplyIntro();
    }

    static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

    void ApplyIntro()
    {
        float t = _introT;
        if (_group != null) _group.alpha = Mathf.Clamp01(t / IntroFade);
        float eFace = EaseOut(t / IntroDur);
        float eCol  = EaseOut((t - IntroDelayC) / (IntroDur - IntroDelayC));
        if (_faceHolder != null)
        {
            _faceHolder.localScale = Vector3.one * Mathf.Lerp(IntroScale, 1f, eFace);
            _faceHolder.anchoredPosition = new Vector2(FaceCx - IntroSlide * (1f - eFace), -FaceCy);
        }
        if (_content != null)
            _content.anchoredPosition = new Vector2(ColL + IntroSlide * (1f - eCol), -ColT);

        if (t >= IntroDur)   // 收尾到精确终态，免得留下 0.9999 的残差
        {
            if (_group != null) _group.alpha = 1f;
            if (_faceHolder != null) { _faceHolder.localScale = Vector3.one; _faceHolder.anchoredPosition = new Vector2(FaceCx, -FaceCy); }
            if (_content != null) _content.anchoredPosition = new Vector2(ColL, -ColT);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 公开入口（签名与改动前完全一致）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>面板正开着就用最新的实例重画（板面同步 / 特性赋予时调用）。</summary>
    public void RefreshIfOpen()
    {
        if (panelRoot != null && panelRoot.activeSelf && _cachedInstance != null)
            Show(_cachedInstance);
    }

    public void Show(CardInstance instance)
    {
        if (instance == null) return;
        _cachedInstance = instance;

        Build();
        if (panelRoot == null) return;

        bool wasOpen = panelRoot.activeSelf;
        if (!wasOpen) panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        Render(instance);

        // 只在「关 -> 开」这一拍播入场：板面同步 / 刷新重画不该再抖一下
        if (!wasOpen) { _introT = 0f; ApplyIntro(); }
    }

    public void Hide()
    {
        _introT = IntroDur;   // 取消进行中的入场（关就干脆关，不留残影）
        ClearFace();       // 放大卡面是 Instantiate 出来的，收起来免得堆着
        ClearRows();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 骨架
    // ═══════════════════════════════════════════════════════════════════════

    void Build()
    {
        if (_built) return;
        _built = true;
        if (panelRoot == null) panelRoot = gameObject;

        // 旧件退场：场景里的「金边盒子 + 一整段 infoText」不再用，新布局自带每格底板
        var legacyImg = panelRoot.GetComponent<Image>();
        if (legacyImg != null) legacyImg.enabled = false;
        for (int i = panelRoot.transform.childCount - 1; i >= 0; i--)
            panelRoot.transform.GetChild(i).gameObject.SetActive(false);

        RectTransform root = panelRoot.GetComponent<RectTransform>();
        if (root == null) root = panelRoot.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;          // 铺满整屏，子件按屏幕 px 摆
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.localScale = Vector3.one;

        // 入场淡入用的层；不拦射线（与全件同口径）
        _group = panelRoot.GetComponent<CanvasGroup>();
        if (_group == null) _group = panelRoot.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        _font = ResolveFont();

        if (ScrimAlpha > 0f)
        {
            RectTransform scrim = NewRect(panelRoot.transform, "Scrim", Vector2.zero, new Vector2(0.5f, 0.5f),
                                          Vector2.zero, Vector2.zero);
            scrim.anchorMin = Vector2.zero;
            scrim.anchorMax = Vector2.one;
            scrim.offsetMin = Vector2.zero;
            scrim.offsetMax = Vector2.zero;
            var img = scrim.gameObject.AddComponent<Image>();
            img.color = new Color(0.027f, 0.043f, 0.070f, ScrimAlpha);   // 平底压暗，无模糊
            img.raycastTarget = false;
        }

        // 左：放大的卡面（面板最左）
        _faceHolder = NewRect(panelRoot.transform, "Face", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                              new Vector2(FaceCx, -FaceCy), new Vector2(CardW, CardH));

        // 右：属性栏（面板最右，整条避开棋盘）
        _content = NewRect(panelRoot.transform, "Info", new Vector2(0f, 1f), new Vector2(0f, 1f),
                           new Vector2(ColL, -ColT), new Vector2(ColW, ColH));
    }

    TMP_FontAsset ResolveFont()
    {
        if (infoText != null && infoText.font != null) return infoText.font;
        TMP_FontAsset f = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSerifCJKsc-Bold SDF");
        if (f != null) return f;
        return TMP_Settings.defaultFontAsset;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 填内容
    // ═══════════════════════════════════════════════════════════════════════

    void Render(CardInstance ci)
    {
        ClearRows();

        CardData d = CardDatabase.Instance?.GetTemplate(ci.templateID);
        if (d == null) d = ci.sourceTemplate;

        EnsureFace(d, ci);

        bool isSpell = d != null && d.cardType == CardType.Spell;
        float y = 0f;

        // 第 1 栏：ID / 名称 / 类别
        y = AddRow(y, new[]
        {
            new Cell("ID",   string.IsNullOrEmpty(ci.templateID) ? "-" : ci.templateID, Gold),
            new Cell("名称", d != null ? d.cardName : ci.templateID, Tone(ci.GetNameColor())),
            new Cell("类别", d != null ? CategoryLabel(d) : "-", Cream),
        });

        if (isSpell)
        {
            // 法术没有生命 / 攻击 / 阶位 ⇒ 只有前缀 + 费用
            y = AddRow(y, new[]
            {
                new Cell("前缀", PrefixLabel(ci), Cream),
                new Cell("费用", CostText(ci, d), Tone(ci.GetCostColor())),
            });
        }
        else
        {
            // 第 2 栏：前缀 / 费用 / 阶位
            y = AddRow(y, new[]
            {
                new Cell("前缀", PrefixLabel(ci), Cream),
                new Cell("费用", CostText(ci, d), Tone(ci.GetCostColor())),
                new Cell("阶位", TierText(ci, d), Cream),
            });

            // 第 3 栏：生命值 / 攻击力
            y = AddRow(y, new[]
            {
                new Cell("生命值", HealthText(ci, d), Tone(ci.GetHealthColor())),
                new Cell("攻击力", AttackText(ci, d), Tone(ci.GetAttackColor())),
            });
        }

        y += SecGap - RowGap;      // 属性段与特性段之间多留一点

        // 第 4 栏起：一条特性一栏（法术则是「效果」那几行）
        List<string> rows = new List<string>();
        if (isSpell)
        {
            if (d != null) SplitLines(d.effect, rows);
        }
        else
        {
            rows.AddRange(TraitLines(ci));
        }
        if (rows.Count == 0) rows.Add("无");
        for (int i = 0; i < rows.Count; i++) y = AddTraitRow(y, rows[i]);

        // 状态 / 附着物 / 计数（旧文本框里有，这里照旧给出来，只是换成一条一栏）
        List<string> st = StatusLines(ci);
        for (int i = 0; i < st.Count; i++) y = AddTraitRow(y, st[i]);
        List<string> at = AttachLines(ci);
        for (int i = 0; i < at.Count; i++) y = AddTraitRow(y, at[i]);
        if (ci.templateID == "01534")
            y = AddTraitRow(y, "当前计数：" + ci.totalDamageTaken);

        // 悬停提示不拦射线：面板里的件（含卡面预制体自带的、以及重画时才生成的状态/前缀小图标）
        // 一律关掉 —— 必须放在最后：早扫的话重画时新生成的那几件会漏掉（实测漏 2-6 件）
        foreach (var g in panelRoot.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    /// <summary>一栏（若干格）：每格的宽按自己的内容算，从栏左依次摆开，不再把整条右栏拉满。</summary>
    float AddRow(float y, Cell[] cells)
    {
        int n = cells.Length;
        var row = NewRect(_content, "Row", new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(0f, -y), new Vector2(ColW, CellH));

        var cellRT = new RectTransform[n];
        var lab = new TextMeshProUGUI[n];
        var val = new TextMeshProUGUI[n];
        var widths = new float[n];
        float labMax = 0f;

        // 先建件 + 量宽（临时都摆在 0，量完再挪）
        for (int i = 0; i < n; i++)
        {
            cellRT[i] = NewRect(row, "Cell_" + cells[i].label, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                Vector2.zero, new Vector2(CellMinW, CellH));
            var plate = cellRT[i].gameObject.AddComponent<Image>();
            plate.color = PlateColor;
            plate.raycastTarget = false;

            lab[i] = NewText(cellRT[i], "Label", LabelX, 0f, 200f, CellH, LabelFont, Steel);
            lab[i].text = cells[i].label;
            val[i] = NewText(cellRT[i], "Value", 0f, 0f, 200f, CellH, CellFont, cells[i].color);
            val[i].text = cells[i].value;

            labMax = Mathf.Max(labMax, Measure(lab[i], cells[i].label).x);
        }

        // 同一栏里属性名左对齐、值的起点统一（由最宽的那个名决定）
        float valX = LabelX + labMax + LabelGap;
        float sumW = 0f;
        for (int i = 0; i < n; i++)
        {
            float vw = Measure(val[i], cells[i].value).x;
            widths[i] = Mathf.Clamp(valX + vw + CellPadR, CellMinW, CellMaxW);
            sumW += widths[i];
        }
        // 本栏放不下就等比收一下（值都是短串，正常走不到这里）
        float avail = ColW - (n - 1) * ColGapX;
        if (sumW > avail && sumW > 0f)
        {
            float k = avail / sumW;
            for (int i = 0; i < n; i++) widths[i] = Mathf.Max(60f, widths[i] * k);
        }

        float x = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = widths[i];
            cellRT[i].sizeDelta = new Vector2(w, CellH);
            cellRT[i].anchoredPosition = new Vector2(x, 0f);
            lab[i].rectTransform.anchoredPosition = new Vector2(LabelX, 0f);
            val[i].rectTransform.anchoredPosition = new Vector2(valX, 0f);
            val[i].rectTransform.sizeDelta = new Vector2(Mathf.Max(24f, w - valX - CellPadR), CellH);
            x += w + ColGapX;
        }
        row.sizeDelta = new Vector2(Mathf.Max(0f, x - ColGapX), CellH);
        return y + CellH + RowGap;
    }

    /// <summary>一条特性一栏：底板贴着文字长度（单行放得下就紧贴文字，放不下按 TraitMaxW 折行且底板长高）。</summary>
    float AddTraitRow(float y, string text)
    {
        float contentMaxW = TraitMaxW - TraitPadX * 2f;

        var row = NewRect(_content, "Trait", new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(0f, -y), new Vector2(TraitMaxW, TraitHMin));
        var plate = row.gameObject.AddComponent<Image>();
        plate.color = PlateColor;
        plate.raycastTarget = false;

        var t = NewText(row, "Text", TraitPadX, -TraitPadY, contentMaxW, TraitHMin - TraitPadY * 2f, TraitFont, Cream);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.text = text;

        Vector2 natural = Measure(t, text);
        float contentW, contentH;
        if (natural.x > contentMaxW)
        {
            t.enableWordWrapping = true;
            Vector2 wrapped = t.GetPreferredValues(text, contentMaxW, float.MaxValue);
            contentW = contentMaxW;
            contentH = wrapped.y;
        }
        else
        {
            contentW = natural.x;
            contentH = natural.y;
        }

        float plateW = Mathf.Min(TraitMaxW, contentW + TraitPadX * 2f + 2f);
        float plateH = Mathf.Max(TraitHMin, contentH + TraitPadY * 2f);
        row.sizeDelta = new Vector2(plateW, plateH);
        t.rectTransform.sizeDelta = new Vector2(Mathf.Max(24f, plateW - TraitPadX * 2f),
                                                Mathf.Max(24f, plateH - TraitPadY * 2f));
        return y + plateH + RowGap;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 放大卡面（模板建件 -> 用实时实例画一遍）
    // ═══════════════════════════════════════════════════════════════════════

    void EnsureFace(CardData d, CardInstance ci)
    {
        if (_faceHolder == null) return;

        if (_face != null)
        {
            var ex = _face.GetComponent<CardInstance>();
            if (ex != null && d != null && ex.templateID == d.templateID)
            {
                RefreshFace(ci);       // 同一张卡（板面同步重画）：只刷新数值，不重建
                return;
            }
            ClearFace();
        }
        if (d == null) return;

        // 卡面预制体：优先 Player 上那两个（运行时的权威来源），退到 CardData 里挂的那份
        GameObject prefab = null;
        if (Player.Instance != null)
            prefab = d.cardType == CardType.Spell ? Player.Instance.spellCardPrefab2D : Player.Instance.cardPrefab2D;
        if (prefab == null)
            prefab = d.cardType == CardType.Spell ? d.spell2DPrefab : d.card2DPrefab;
        if (prefab == null) return;

        _face = Instantiate(prefab, _faceHolder);
        _face.name = "DetailFace_" + d.templateID;

        // 战斗件关掉（与大厅 LobbyCardPreview.Create 同口径）：这里只做展示
        var view = _face.GetComponent<CardView>(); if (view != null) view.enabled = false;
        var drag = _face.GetComponent<CardDrag>(); if (drag != null) drag.enabled = false;
        var hover = _face.GetComponent<CardHover>(); if (hover != null) hover.enabled = false;

        var inst = _face.GetComponent<CardInstance>();
        if (inst == null) inst = _face.AddComponent<CardInstance>();
        inst.InitFromTemplate(d, 0);

        RectTransform frt = _face.GetComponent<RectTransform>();
        frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
        frt.pivot = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(CardW, CardH);
        frt.anchoredPosition = new Vector2(CardW * 0.5f, -CardH * 0.5f);   // 摆在 holder 正中
        frt.localScale = Vector3.one * FaceScale;

        RefreshFace(ci);
    }

    void RefreshFace(CardInstance ci)
    {
        if (_face == null) return;
        var inst = _face.GetComponent<CardInstance>();
        if (inst == null) return;
        if (ci != null) inst.CopyFrom(ci);     // 实时数值：受伤 / 减费 / 增益 / 赋予特性

        var dn = _face.GetComponent<CardDisplay2DNew>();
        if (dn != null) { dn.RefreshWithInstance(inst); return; }
        var dold = _face.GetComponent<CardDisplay2D>();
        if (dold != null) dold.RefreshWithInstance(inst);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 文案
    // ═══════════════════════════════════════════════════════════════════════

    static string CategoryLabel(CardData d)
    {
        if (d.cardType == CardType.Spell)
        {
            string sub = d.spellType == SpellType.Evil ? "邪恶"
                       : d.spellType == SpellType.Counter ? "反制" : "普通";
            return "法术·" + sub;
        }
        string kind = d.summonType == SummonType.Hero ? "英雄"
                    : d.summonType == SummonType.ChosenOne ? "神选者" : "特殊";
        return "召唤物·" + kind;
    }

    static string PrefixLabel(CardInstance ci)
    {
        return ci == null || string.IsNullOrEmpty(ci.prefixes) ? "无" : ci.prefixes;
    }

    static string CostText(CardInstance ci, CardData d)
    {
        int cost = ci.GetDisplayCost();
        int baseCost = d != null ? d.baseCost : cost;
        return cost == baseCost ? cost.ToString() : cost + "（基础 " + baseCost + "）";
    }

    static string TierText(CardInstance ci, CardData d)
    {
        int tier = ci.currentTier;
        int baseTier = d != null ? d.baseTier : tier;
        return tier == baseTier ? tier.ToString() : tier + "（基础 " + baseTier + "）";
    }

    static string HealthText(CardInstance ci, CardData d)
    {
        string s = ci.currentHealth + "/" + ci.currentMaxHealth;
        int baseH = d != null ? d.baseHealth : ci.currentMaxHealth;
        if (ci.currentMaxHealth != baseH) s += "（基础 " + baseH + "）";
        if (ci.hasShield) s += " 护盾";
        return s;
    }

    static string AttackText(CardInstance ci, CardData d)
    {
        int atk = ci.Attack;
        int baseAtk = d != null ? d.baseAttack : atk;
        return atk == baseAtk ? atk.ToString() : atk + "（基础 " + baseAtk + "）";
    }

    /// <summary>特性条目：固有 + 获得的赋予特性；被禁制 / 沉默的附上原因（照旧口径）。</summary>
    static List<string> TraitLines(CardInstance ci)
    {
        var lines = new List<string>();
        if (ci == null) return lines;
        var entries = ci.GetVisibleTraitEntries();
        bool fullySilenced = TraitBanQuery.IsFullySilenced(ci);
        for (int i = 0; i < entries.Count; i++)
        {
            string s = CardInstance.FormatTraitEntry(i + 1, entries[i]);
            string reason = "";
            if (fullySilenced) reason = TraitBanQuery.FullSilenceReason(ci);
            else if (entries[i].attributes != null)
            {
                foreach (var a in entries[i].attributes)
                {
                    string r = TraitBanQuery.ClassBanReason(ci, a);
                    if (r.Length > 0) { reason = r; break; }
                }
            }
            if (reason.Length > 0) s += "\n　" + reason;
            lines.Add(s);
        }
        return lines;
    }

    /// <summary>状态区：本卡被施加的每条来源状态 -> 「· 描述（来源：卡名）」。无状态返回空表。</summary>
    static List<string> StatusLines(CardInstance ci)
    {
        var body = new List<string>();
        if (ci == null || ci.activeStatuses == null) return body;
        foreach (var a in ci.activeStatuses)
        {
            if (a == null) continue;
            string text = TraitBanQuery.StatusWithSource(a);
            if (string.IsNullOrEmpty(text)) continue;
            body.Add("· " + text.Replace("\n", " / "));
        }
        var lines = new List<string>();
        for (int i = 0; i < body.Count; i++)
            lines.Add((i == 0 ? "状态：" : "　　") + body[i]);
        return lines;
    }

    /// <summary>附着物：本卡是宿主时，把挂在它身上的附着卡列出来。</summary>
    static List<string> AttachLines(CardInstance ci)
    {
        var lines = new List<string>();
        if (ci == null || ci.isAttached) return lines;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return lines;
        BoardSlot[] slots = bm.GetAllSlots();
        if (slots == null) return lines;

        int hostSlotID = -1;
        for (int i = 0; i < 12 && i < slots.Length; i++)
        {
            if (slots[i]?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            {
                hostSlotID = slots[i].slotID;
                break;
            }
        }
        if (hostSlotID == -1) return lines;

        foreach (GameObject obj in bm.attachedModels)
        {
            if (obj == null) continue;
            CardInstance aci = obj.GetComponent<Card3DInstance>()?.cardInstance;
            if (aci == null || !aci.isAttached || aci.hostSlotID != hostSlotID) continue;
            CardData at = CardDatabase.Instance?.GetTemplate(aci.templateID);
            if (at != null && !string.IsNullOrEmpty(at.traits))
                lines.Add("附着物：" + at.cardName + "：" + at.traits);
        }
        return lines;
    }

    static void SplitLines(string raw, List<string> into)
    {
        if (string.IsNullOrEmpty(raw)) return;
        string[] parts = raw.Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            string s = parts[i].Trim();
            if (s.Length > 0) into.Add(s);
        }
    }

    /// <summary>卡面那套规则色里的「默认」是纯白，压深底上换成奶油色（同一个白，只是不刺眼）。</summary>
    static Color Tone(Color c)
    {
        return (c.r > 0.99f && c.g > 0.99f && c.b > 0.99f) ? Cream : c;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 清理
    // ═══════════════════════════════════════════════════════════════════════

    void ClearFace()
    {
        if (_face == null) return;
        Kill(_face);
        _face = null;
    }

    void ClearRows()
    {
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--) Kill(_content.GetChild(i).gameObject);
    }

    static void Kill(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 小工具
    // ═══════════════════════════════════════════════════════════════════════

    static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    TextMeshProUGUI NewText(Transform parent, string name, float x, float y, float w, float h, float size, Color color)
    {
        RectTransform rt = NewRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = "";
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>量一段文字的单行自然宽（先禁折行）。TMP 的 GetPreferredValues 跨版本稳定，
    /// 不依赖布局刷新 —— 大厅那份与 Game 的 HoverTagLabel 都这么量。</summary>
    static Vector2 Measure(TMP_Text t, string s)
    {
        t.enableWordWrapping = false;
        return t.GetPreferredValues(s, float.MaxValue, float.MaxValue);
    }
}
