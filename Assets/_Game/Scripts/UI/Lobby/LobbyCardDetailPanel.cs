using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>大厅「卡牌总览」里点一张卡 → 类全屏的**卡牌详情**：左半区放大卡面 + 右半区属性栏，底下是一层模糊幕。</summary>
/// <remarks>2026-09-28 用户：「做卡牌详情点击展示，为卡牌总览的卡牌做一个类全屏（背景是透明的模糊幕即可），
/// 为卡牌做悬停和点击变化，点击后屏幕左半区放放大后的卡牌卡面，右边参考 Game 场景里的详情面板，
/// 不过是每个属性都有自己的背景，最上面一栏分别是 id，名称，类别；第二栏是前缀，费用，阶位；
/// 第三栏是生命值和攻击力；从第四栏开始每一栏是一个特效（特性描述很长时背景也会随着变高而增长），
/// 点击右上角的叉或者点击空白退出这个详情」。
///
/// 对照 Game 场景那份详情（<c>Assets/_Game/Scripts/UI/Panels/Test1Panel.cs</c>，一个 <c>infoText</c> 拼多行）
/// —— **这里不用一行大文本**，而是一栏一块自己的底：属性栏每格的宽**按自己的内容量**、左起依次摆开；
/// 特性一条一栏，底板**贴着文字长度**（放得下就单行紧贴，放不下按 <see cref="TraitMaxW"/> 折行并长高），
/// 与 Game 场景 3D 卡牌悬停标签（<c>HoverTagLabel</c>）同口径。**整条右栏不拉满** —— 右边留空。
///
/// 底：<c>LobbyPanelPlate.png</c>（192x192，9-slice border 48，屏幕 1px = 贴图 3px ⇒ 圆角 16 屏幕 px）。
/// 幕：<c>Art/Shaders/UIBlurScrim.shader</c>（GrabPass 抓本件之前画完的那一屏再模糊 + 压暗）；
///     拿不到着色器就退成一整块深色半透明 —— 见 <see cref="ScrimMaterial"/>。
///
/// 入场（2026-09-29 用户：「点击出现有动画，左边是向上滑动显示，右边的每一栏都是向右滑动出现，
/// 从上到下依次滑动，每一栏整体是同时滑动的」）：**卡面一组向上、右栏一栏一组向右、组间从上到下错开** ——
/// 动作用共用的 <c>LobbyCardsIntro</c> 放，本处只排时间轴（见 <see cref="PlayIntro"/>）。
/// 动的是**栏容器**（Row / Trait），格子挂在它下面 ⇒ 一栏天然是一起动的。
///
/// 本面板挂在 <c>Panel_Cards</c> 根上、铺满整屏，所以它跟卡牌总览一样在 <c>Layer_Sub_v1</c>：
/// HUD 层（头像板 / 横栏 / 齿轮）仍然画在它之上 —— 与「左上角和右上角在全屏弹窗里仍常驻」同一条口径。
/// </remarks>
public class LobbyCardDetailPanel : MonoBehaviour
{
    public static LobbyCardDetailPanel Instance;

    // ── 几何（屏幕 px · 1920x1080 参考分辨率）────────────────────────────────
    const float FaceCx = 486f, FaceCy = 556f, FaceScale = 4.2f;   // 左半区：卡面中心 / 放大倍数（83x146 -> 349x614）
    const float ColL = 1000f, ColT = 200f, ColW = 856f, ColH = 788f;   // 右半区：属性栏（距面板右沿 64、下沿 92）
    const float CellH = 66f;            // 行高（属性栏 / 特性栏的下限）
    const float RowGap = 14f;           // 行距
    const float SecGap = 22f;           // 「属性」与「特性」两段之间额外多出来的那一点
    const float ColGapX = 12f;          // 同栏两格之间的间隔
    const float CellMinW = 186f;        // 一格的宽按**自己的内容**算：再短也给这么宽（免得挤成一坨）
    const float CellMaxW = 432f;        // 再长也封顶（值都是短串，够用）
    const float CellPadR = 16f;         // 值右边留的内缩
    const float LabelX = 18f, LabelGap = 20f;   // 属性名左内缩 / 名与值之间的间隔
    const float CellFont = 24f, LabelFont = 19f;
    // 特性：同 Game 里 3D 卡牌悬停标签（HoverTagLabel）的口径 —— 单行放得下就紧贴文字，
    // 放不下按 TraitMaxW 折行、底板同时长高。
    const float TraitFont = 22f, TraitPadX = 18f, TraitPadY = 15f, TraitHMin = 66f, TraitMaxW = 760f;

    const float CloseX = -168f, CloseY = -66f, CloseSize = 60f;   // 与卡牌总览那个叉同位置（锚右上 / 轴左上）

    // ── 入场（动作交给 LobbyCardsIntro，这里只排时间轴）──────────────────────
    const float IntroRise = 26f;        // 起手偏移量（屏幕 px）
    const float IntroDur = 0.30f;       // 单件时长
    const float IntroFaceAt = 0.02f;    // 卡面起手（与第 1 栏几乎同时）
    const float IntroRowAt = 0.02f;     // 第 1 栏起手
    const float IntroRowStep = 0.055f;  // 相邻两栏的起手间隔（从上到下依次）

    // ── 色（照 lobby-ui-v1 那一套，不另起）──────────────────────────────────
    static readonly Color Cream = new Color32(240, 232, 210, 236);
    static readonly Color Gold  = new Color32(228, 203, 132, 255);
    static readonly Color Steel = new Color32(142, 162, 180, 255);

    // ── 贴图 ────────────────────────────────────────────────────────────────
    const string PlatePath      = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/LobbyPanelPlate.png";
    const string ClosePath      = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/Icon_Close.png";
    const string CloseHoverPath = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/Icon_CloseHover.png";
    const string FontPath       = "Fonts & Materials/NotoSerifCJKsc-Bold SDF";

    [Header("字体（由卡牌总览面板填；为空时自己兜一次）")]
    public TMP_FontAsset font;

    bool _built;
    GameObject _face;
    RectTransform _faceHolder, _content;
    ScrollRect _scroll;
    Sprite _plate;
    LobbyCardsIntro _intro;                 // 入场（Play 才建，同卡牌总览那条做法）
    readonly List<RectTransform> _introRows = new List<RectTransform>();   // 本轮右栏的每一栏（一栏 = 一个 Row/Trait 容器）

    struct Cell
    {
        public string label, value;
        public Color color;
        public Cell(string l, string v, Color c) { label = l; value = v; color = c; }
    }

    void Awake()
    {
        Instance = this;
        if (font == null) font = Resources.Load<TMP_FontAsset>(FontPath);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>面板第一次点到卡时现建一个（与 <c>LobbyCardsIntro</c> 同一条思路：不进场景，免得重跑生成器把版式冲掉）。</summary>
    public static LobbyCardDetailPanel Create(Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject("Panel_CardDetail", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var p = go.AddComponent<LobbyCardDetailPanel>();
        if (font != null) p.font = font;
        p.Build();
        return p;
    }

    public bool IsOpen { get { return gameObject.activeSelf; } }

    public void Close()
    {
        ClearFace();      // 放大卡面是 Instantiate 出来的，关掉就收，免得堆着
        gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 骨架
    // ═══════════════════════════════════════════════════════════════════════

    void Build()
    {
        if (_built) return;
        _built = true;

        _plate = LoadSprite(PlatePath);

        // ① 模糊幕：点它 = 点空白 = 关。⚠ 处理器挂在**幕自己**身上，不能挂到根上 ——
        //    右半区那些属性格是从根冒泡上来的，挂根上会「点一下属性栏就把详情关了」。
        RectTransform scrim = NewRect(transform, "Scrim", Vector2.zero, new Vector2(0.5f, 0.5f),
                                      Vector2.zero, Vector2.zero);
        scrim.anchorMin = Vector2.zero;
        scrim.anchorMax = Vector2.one;
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = Vector2.zero;
        var scrimImg = scrim.gameObject.AddComponent<Image>();
        scrimImg.raycastTarget = true;
        Material mat = ScrimMaterial();
        if (mat != null) { scrimImg.material = mat; scrimImg.color = Color.white; }
        else scrimImg.color = new Color(0.027f, 0.043f, 0.070f, 0.80f);   // 拿不到着色器：退成深色半透明
        var sbtn = scrim.gameObject.AddComponent<Button>();
        sbtn.transition = Selectable.Transition.None;
        sbtn.targetGraphic = scrimImg;
        sbtn.onClick.AddListener(Close);

        // ② 左半区放大卡面（外面套一层全透明可点面：点在卡上不算「点空白」，不该关）
        _faceHolder = NewRect(transform, "Face", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                              new Vector2(FaceCx, -FaceCy), new Vector2(LobbyCardPreview.CardW, LobbyCardPreview.CardH));
        var block = _faceHolder.gameObject.AddComponent<Image>();
        block.color = new Color(0f, 0f, 0f, 0f);
        block.raycastTarget = true;

        // ③ 右半区属性栏（可滚：特性多 / 文字长时兜底；现在最长的一张也用不满一屏）
        _scroll = NewRect(transform, "Info", new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(ColL, -ColT), new Vector2(ColW, ColH)).gameObject.AddComponent<ScrollRect>();
        var vp = (RectTransform)_scroll.transform;
        vp.gameObject.AddComponent<RectMask2D>();
        var dragSurface = vp.gameObject.AddComponent<Image>();   // 拖空白也能滚（同卡牌网格那条做法）
        dragSurface.color = new Color(0f, 0f, 0f, 0f);
        dragSurface.raycastTarget = true;
        // ⚠ 这一层铺满整条右栏，把底下幕上的「点空白关详情」全挡了 ⇒ 给它配一件：
        //   **拖 = 滚（ScrollRect 自己的），单击且落点不在底板上 = 关详情**（见 LobbyColumnClick）。
        var colClick = vp.gameObject.AddComponent<LobbyColumnClick>();
        colClick.panel = this;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 40f;
        _content = NewRect(vp, "Content", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(ColW, ColH));
        _scroll.viewport = vp;
        _scroll.content = _content;

        // ④ 右上角那个通用关闭叉（Icon_Close + 悬停换贴图），位置与卡牌总览那个一模一样
        RectTransform closeRT = NewRect(transform, "Btn_Close", new Vector2(1f, 1f), new Vector2(0f, 1f),
                                        new Vector2(CloseX, CloseY), new Vector2(CloseSize, CloseSize));
        var closeImg = closeRT.gameObject.AddComponent<RawImage>();
        closeImg.texture = LoadTex(ClosePath);
        var hover = closeRT.gameObject.AddComponent<LobbyIconHover>();
        hover.icon = closeImg;
        hover.normalTexture = LoadTex(ClosePath);
        hover.hoverTexture = LoadTex(CloseHoverPath);
        var cbtn = closeRT.gameObject.AddComponent<Button>();
        cbtn.transition = Selectable.Transition.None;
        cbtn.targetGraphic = closeImg;
        cbtn.onClick.AddListener(Close);
    }

    /// <summary>模糊幕的材质。拿不到着色器返回 null（调用方退成纯色半透明）。</summary>
    static Material ScrimMaterial()
    {
        Shader sh = Shader.Find("AnotherWorld/UIScrimBlur");
        return sh != null ? new Material(sh) { hideFlags = HideFlags.DontSave } : null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 填内容
    // ═══════════════════════════════════════════════════════════════════════

    public void Open(CardData d)
    {
        if (d == null) return;
        Build();
        if (font == null) font = Resources.Load<TMP_FontAsset>(FontPath);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Render(d);
        PlayIntro();
    }

    /// <summary>把本轮建好的件排成入场：左半区卡面**向上**滑出，右栏**一栏一组、从上到下依次向右**滑出。</summary>
    void PlayIntro()
    {
        var groups = new List<LobbyCardsIntro.Group>(_introRows.Count + 1);

        // 左半区卡面：默认方向 (0,-1) = 从下方浮上来，也就是题面要的「向上滑动显示」
        if (_faceHolder != null)
            groups.Add(new LobbyCardsIntro.Group(IntroFaceAt, new List<RectTransform> { _faceHolder }));

        // 右栏：一栏一组 —— 栏里的格子跟着容器一起动（用户：「每一栏整体是同时滑动的」），
        // 组与组之间按从上到下的顺序错开。dir = (-1,0) = 从左边起手 ⇒ 看上去是向右滑出来。
        for (int i = 0; i < _introRows.Count; i++)
            groups.Add(new LobbyCardsIntro.Group(IntroRowAt + IntroRowStep * i,
                                                 new List<RectTransform> { _introRows[i] },
                                                 new Vector2(-1f, 0f)));

        LobbyCardsIntro intro = Intro();
        intro.rise = IntroRise;
        intro.dur = IntroDur;
        intro.PlayGroups(groups);
    }

    LobbyCardsIntro Intro()
    {
        if (_intro == null) _intro = GetComponent<LobbyCardsIntro>();
        if (_intro == null) _intro = gameObject.AddComponent<LobbyCardsIntro>();
        return _intro;
    }

    void Render(CardData d)
    {
        ClearFace();
        ClearRows();
        _introRows.Clear();     // 上一轮那些已经 Destroy（帧末才生效）⇒ 这里只留本轮新建的

        _face = LobbyCardPreview.Create(d, _faceHolder, FaceScale);
        if (_face != null)
        {
            var frt = _face.GetComponent<RectTransform>();
            // 卡面自己的锚是左上 / 轴心居中 ⇒ 摆到 holder 正中央
            frt.anchoredPosition = new Vector2(LobbyCardPreview.CardW * 0.5f, -LobbyCardPreview.CardH * 0.5f);
        }
        CardInstance inst = _face != null ? _face.GetComponent<CardInstance>() : null;

        bool isSpell = d.cardType == CardType.Spell;
        float y = 0f;

        // 第 1 栏：ID / 名称 / 类别
        y = AddRow(y, new[]
        {
            new Cell("ID",   d.templateID, Gold),
            new Cell("名称", d.cardName, Cream),
            new Cell("类别", CategoryLabel(d), Cream),
        });

        if (isSpell)
        {
            // 法术没有生命 / 攻击 / 阶位（阶位在库里就是费用档，写出来是重复）⇒ 只有前缀 + 费用
            y = AddRow(y, new[]
            {
                new Cell("前缀", PrefixLabel(d), Cream),
                new Cell("费用", d.baseCost.ToString(), Cream),
            });
        }
        else
        {
            // 第 2 栏：前缀 / 费用 / 阶位
            y = AddRow(y, new[]
            {
                new Cell("前缀", PrefixLabel(d), Cream),
                new Cell("费用", d.baseCost.ToString(), Cream),
                new Cell("阶位", d.baseTier.ToString(), Cream),
            });

            // 第 3 栏：生命值 / 攻击力
            y = AddRow(y, new[]
            {
                new Cell("生命值", d.baseHealth.ToString(), Cream),
                new Cell("攻击力", d.baseAttack.ToString(), Cream),
            });
        }

        y += SecGap - RowGap;      // 属性段与特性段之间多留一点

        // 第 4 栏起：一条特性一栏（法术则是「效果」那几行）
        List<string> rows = new List<string>();
        if (isSpell)
        {
            SplitLines(d.effect, rows);
        }
        else
        {
            var entries = inst != null ? inst.GetVisibleTraitEntries() : null;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++) rows.Add(CardInstance.FormatTraitEntry(i + 1, entries[i]));
        }
        if (rows.Count == 0) rows.Add("无");
        for (int i = 0; i < rows.Count; i++) y = AddTraitRow(y, rows[i]);

        _content.sizeDelta = new Vector2(ColW, Mathf.Max(ColH, y - RowGap));
        _content.anchoredPosition = Vector2.zero;     // 回顶（锚 / 轴都是左上 ⇒ 归零就是顶）
    }

    /// <summary>一栏（若干格）：**每格的宽按自己的内容算**，从栏左依次摆开、格间留 <see cref="ColGapX"/>，
    /// 不再把整条右栏拉满（用户：「不需要完全同规格和填满整个右边，可以留出空隙」）。返回下一栏的 y。</summary>
    float AddRow(float y, Cell[] cells)
    {
        int n = cells.Length;
        var row = NewRect(_content, "Row", new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(0f, -y), new Vector2(ColW, CellH));
        _introRows.Add(row);

        var cellRT = new RectTransform[n];
        var lab = new TextMeshProUGUI[n];
        var val = new TextMeshProUGUI[n];
        float labMax = 0f;

        // ① 先建件 + 量宽（临时都摆在 0，量完再挪）
        for (int i = 0; i < n; i++)
        {
            cellRT[i] = NewRect(row, "Cell_" + cells[i].label, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                new Vector2(0f, 0f), new Vector2(CellMinW, CellH));
            var plate = cellRT[i].gameObject.AddComponent<Image>();
            plate.sprite = _plate;
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            plate.raycastTarget = false;

            lab[i] = NewText(cellRT[i], "Label", LabelX, 0f, 200f, CellH, LabelFont, Steel);
            lab[i].text = cells[i].label;
            val[i] = NewText(cellRT[i], "Value", 0f, 0f, 200f, CellH, CellFont, cells[i].color);
            val[i].text = cells[i].value;

            labMax = Mathf.Max(labMax, Measure(lab[i], cells[i].label).x);
        }

        // ② 同一栏里属性名左对齐、值的起点统一（由最宽的那个名决定）
        float valX = LabelX + labMax + LabelGap;
        float x = 0f;
        for (int i = 0; i < n; i++)
        {
            float vw = Measure(val[i], cells[i].value).x;
            float w = Mathf.Clamp(valX + vw + CellPadR, CellMinW, CellMaxW);
            cellRT[i].sizeDelta = new Vector2(w, CellH);
            cellRT[i].anchoredPosition = new Vector2(x, 0f);
            lab[i].rectTransform.anchoredPosition = new Vector2(LabelX, 0f);
            val[i].rectTransform.anchoredPosition = new Vector2(valX, 0f);
            val[i].rectTransform.sizeDelta = new Vector2(Mathf.Max(24f, w - valX - CellPadR), CellH);
            x += w + ColGapX;
        }
        // ⚠ 栏容器自己收成**这一栏格子的实际总宽**（不是 ColW）—— 2026-09-29：
        //   容器的矩形同时充当「点空白关详情」判据里的**底板占地**（见 PointOverAnyRow）。
        //   留成 856 的话，文本一短，右边那一大片空白也会被算成底板 ⇒ 点它不关。
        row.sizeDelta = new Vector2(Mathf.Max(0f, x - ColGapX), CellH);
        return y + CellH + RowGap;
    }

    /// <summary>某个屏幕点有没有压在**本轮右栏的底板**上（一栏 = 一个矩形）。</summary>
    /// <remarks>2026-09-29 用户：「右侧空白区域点击取消的范围应和文字背景占地对应」。判据就是栏容器的矩形：
    /// 属性栏的容器宽已被收成**这一栏格子的实际总宽**（见 <see cref="AddRow"/>），特性栏本来就是贴着文字的宽
    /// （见 <see cref="AddTraitRow"/>）⇒ 容器矩形 = 底板占地。
    /// 画布是 Screen Space - Overlay ⇒ 相机传 null。⚠ 别改成按 <c>content.childCount</c> 取件
    /// （<c>ClearRows()</c> 用 Destroy、帧末才生效，那一帧里旧的还挂着）——用本轮的 <c>_introRows</c>。</remarks>
    public bool PointOverAnyRow(Vector2 screenPoint)
    {
        for (int i = 0; i < _introRows.Count; i++)
        {
            RectTransform rt = _introRows[i];
            if (rt == null || !rt.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null)) return true;
        }
        return false;
    }

    /// <summary>一条特性一栏：**底板贴着文字长度**（单行放得下就紧贴文字，放不下按 <see cref="TraitMaxW"/> 折行
    /// 且底板同时长高）—— 与 Game 场景 3D 卡牌悬停标签（<c>HoverTagLabel</c>）同一条口径。</summary>
    float AddTraitRow(float y, string text)
    {
        float contentMaxW = TraitMaxW - TraitPadX * 2f;

        var row = NewRect(_content, "Trait", new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(0f, -y), new Vector2(TraitMaxW, TraitHMin));
        _introRows.Add(row);
        var plate = row.gameObject.AddComponent<Image>();
        plate.sprite = _plate;
        plate.type = Image.Type.Sliced;
        plate.color = Color.white;
        plate.raycastTarget = false;

        var t = NewText(row, "Text", TraitPadX, -TraitPadY, contentMaxW, TraitHMin - TraitPadY * 2f, TraitFont, Cream);
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

        float plateW = Mathf.Min(TraitMaxW, contentW + TraitPadX * 2f + 2f);   // +2 是防取整把最后一行挤下去
        float plateH = Mathf.Max(TraitHMin, contentH + TraitPadY * 2f);
        row.sizeDelta = new Vector2(plateW, plateH);
        t.rectTransform.sizeDelta = new Vector2(Mathf.Max(24f, plateW - TraitPadX * 2f),
                                                Mathf.Max(24f, plateH - TraitPadY * 2f));
        return y + plateH + RowGap;
    }

    // ── 文案 ───────────────────────────────────────────────────────────────

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

    static string PrefixLabel(CardData d)
    {
        return string.IsNullOrEmpty(d.prefix) ? "无" : d.prefix;
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

    // ── 清理 ───────────────────────────────────────────────────────────────

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
    // 小工具（与卡牌总览同口径的建件）
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
        t.font = font;
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
    /// 不依赖布局刷新 —— Game 里的 HoverTagLabel 也是这么量的。</summary>
    static Vector2 Measure(TMP_Text t, string s)
    {
        t.enableWordWrapping = false;
        return t.GetPreferredValues(s, float.MaxValue, float.MaxValue);
    }

    static Texture2D LoadTex(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#else
        return null;
#endif
    }

    static Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }
}
