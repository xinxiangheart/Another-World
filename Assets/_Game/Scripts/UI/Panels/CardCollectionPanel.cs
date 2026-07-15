using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================================
// CardCollectionPanel — 卡牌总览浏览器（树状筛选 + 同级多选）
// ============================================================================
//
// 多级按钮树: 召唤物 → 英雄 → 1费/3费/5费/带进场/带退场/…
// 同级按钮可**多选** (toggle)，filter 为 AND(父级) + OR(同子级选中)。
// 选中按钮变黄色。点到不同父级时重置子级选中。
// ============================================================================

public class CardCollectionPanel : MonoBehaviour
{
    public static CardCollectionPanel Instance;

    [Header("Root")]
    public GameObject panelRoot;

    [Header("Button Bars (级联按钮行)")]
    public Transform[] buttonBars;
    public GameObject filterButtonPrefab;
    public Vector2 btnCellSize = new Vector2(160, 30);
    public Vector2 btnSpacing = new Vector2(10, 8);
    public int buttonsPerRow = 10;

    [Header("Card Grid")]
    public ScrollRect scrollRect;
    public Transform cardContainer;
    public GameObject summonCardPrefab;
    public GameObject spellCardPrefab;
    public TMPro.TMP_FontAsset detailFont;
    public int cardsPerRow = 4;
    public float cardScale = 1f;
    public float cardWidth = 83f, cardHeight = 146f, hSpacing = 25f, vSpacing = 25f;
    public float startX = -140f, startY = 282f;

    [Header("Return Button")]
    public Button returnButton;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    // ── 内部状态 ──────────────────────────────────────────────────────
    List<CardData> _allCards;
    List<GameObject> _spawned = new List<GameObject>();
    Func<CardData, bool> _activeFilter;

    // ── 按钮树数据结构 ───────────────────────────────────────────────
    class BtnDef { public string label; public Func<CardData, bool> filter; public List<BtnDef> children; }
    BtnDef[] _level0;

    // 多选状态：每级一个 HashSet<int>，记录哪些 index 被选中
    HashSet<int>[] _selected = new HashSet<int>[8];
    // 当前激活的深度（最深有内容的 bar）
    int _currentDepth;

    static BtnDef B(string label, Func<CardData, bool> filter, params BtnDef[] kids)
        => new BtnDef { label = label, filter = filter, children = kids?.ToList() };

    static bool IsHero(CardData d) => d.cardType == CardType.Summon
        && (d.templateID.StartsWith("011") || d.templateID.StartsWith("013") || d.templateID.StartsWith("015"));
    static bool Is1(CardData d) => d.templateID.StartsWith("011");
    static bool Is3(CardData d) => d.templateID.StartsWith("013");
    static bool Is5(CardData d) => d.templateID.StartsWith("015");

    void BuildTree()
    {
        _level0 = new BtnDef[]
        {
            B("全部", null,
                B("召唤物", d => d.cardType == CardType.Summon,
                    B("英雄", IsHero,
                        B("1费", Is1),
                        B("3费", Is3),
                        B("5费", Is5),
                        B("带进场", d => IsHero(d) && d.hasOnEnter),
                        B("带退场", d => IsHero(d) && d.hasOnDeath),
                        B("带主动退场", d => IsHero(d) && d.hasActiveExit),
                        B("带先手", d => IsHero(d) && d.hasFirstStrike),
                        B("带反击", d => IsHero(d) && d.hasRevenge),
                        B("带抛置", d => IsHero(d) && d.hasDiscard),
                        B("带附着", d => IsHero(d) && d.canAttach)),
                    B("神选者", d => d.templateID.StartsWith("035")),
                    B("特殊/衍生", d => d.templateID.StartsWith("030"))),

                B("法术", d => d.cardType == CardType.Spell,
                    B("0费/特殊", d => d.templateID.StartsWith("020")),
                    B("1费", d => d.templateID.StartsWith("021")),
                    B("2费", d => d.templateID.StartsWith("022")),
                    B("3费", d => d.templateID.StartsWith("023")),
                    B("4费", d => d.templateID.StartsWith("024")),
                    B("5费", d => d.templateID.StartsWith("025")),
                    B("反制牌", d => d.spellType == SpellType.Counter),
                    B("邪恶法术", d => (d.spellType & SpellType.Evil) != 0))),
        };
    }

    // ═══════════════════════════════════════════════════════════════════

    void Awake()
    {
        // Lobby 场景没有 CardDatabase / Test1Panel / Player 单例，动态补上
        if (CardDatabase.Instance == null)
        {
            var dbGo = new GameObject("CardDatabase");
            dbGo.AddComponent<CardDatabase>();
        }

        if (Test1Panel.Instance == null)
        {
            var tpGo = new GameObject("Test1Panel");
            tpGo.transform.SetParent(transform.parent, false); // 同级 Canvas
            var tprt = tpGo.AddComponent<RectTransform>();
            tprt.anchorMin = tprt.anchorMax = tprt.pivot = new Vector2(1, 1);
            tprt.anchoredPosition = new Vector2(-20, -20);
            tprt.sizeDelta = new Vector2(300, 450);
            var tpImg = tpGo.AddComponent<UnityEngine.UI.Image>();
            tpImg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            var txtGo = new GameObject("InfoText");
            txtGo.transform.SetParent(tpGo.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(12, 12); txtRt.offsetMax = new Vector2(-12, -12);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            var font = detailFont ?? Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/NotoSerifCJKsc-Bold SDF");
            if (font != null)
            {
                txt.font = font;
                txt.fontMaterial = font.material;
            }
            txt.fontSize = 15;
            txt.color = Color.white;
            txt.alignment = TMPro.TextAlignmentOptions.TopLeft;

            var tp = tpGo.AddComponent<Test1Panel>();
            tp.panelRoot = tpGo;
            tp.infoText = txt;
        }

        if (cardContainer == null && scrollRect != null && scrollRect.content != null)
            cardContainer = scrollRect.content;

        _allCards = new List<CardData>();
        foreach (var td in Resources.LoadAll<CardData>("CardData"))
            if (td != null) _allCards.Add(td);
        foreach (var td in Resources.LoadAll<CardData>("ChosenOneData"))
            if (td != null) _allCards.Add(td);
        _allCards.Sort((a, b) => a.templateID.CompareTo(b.templateID));

        Instance = this;
        if (returnButton) returnButton.onClick.AddListener(Hide);
        for (int i = 0; i < _selected.Length; i++) _selected[i] = new HashSet<int>();
        BuildTree();

        panelRoot?.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 公开 API
    // ═══════════════════════════════════════════════════════════════════

    public void Show()
    {
        panelRoot?.SetActive(true);
        panelRoot?.transform.SetAsLastSibling();
        foreach (var s in _selected) s.Clear();
        ClearAllBars();

        // 默认选中"全部"，展开其子级（召唤物/法术）
        _selected[0].Add(0);
        PopulateBar(0, _level0);
        if (_level0[0].children != null && _level0[0].children.Count > 0)
        {
            PopulateBar(1, _level0[0].children.ToArray());
            _currentDepth = 1;
        }
        else _currentDepth = 0;

        _activeFilter = null;
        RebuildGrid();
        RefreshAllBarHighlights();
    }

    public void Hide()
    {
        panelRoot?.SetActive(false);
        Test1Panel.Instance?.Hide();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 多选 toggle 逻辑
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// - 同级中**有子节点**的按钮互斥（radio），选中一个自动取消其他有子节点的按钮
    /// - 同级中**叶节点**可多选（toggle，button变黄）
    /// - 选中任意有子节点的按钮 → 展开 depth+1
    /// - 取消选中 → 折叠子级
    /// </summary>
    void OnBtnClicked(int depth, int index, BtnDef btn)
    {
        bool already = _selected[depth].Contains(index);
        bool isBranch = btn.children != null && btn.children.Count > 0;

        if (already)
        {
            _selected[depth].Remove(index);
            if (isBranch)
                CollapseFrom(depth + 1);
        }
        else
        {
            if (isBranch)
            {
                // Branch 互斥：取消同级所有其他 branch 选中，折叠它们的子级
                var toRemove = new List<int>();
                foreach (int i in _selected[depth])
                    if (i < buttonBars[depth].childCount)
                    {
                        // 找到对应的 BtnDef 判断是否为 branch
                        var otherBtn = FindBtnAt(depth, i);
                        if (otherBtn != null && otherBtn.children != null && otherBtn.children.Count > 0)
                            toRemove.Add(i);
                    }
                foreach (int i in toRemove)
                {
                    _selected[depth].Remove(i);
                    // 折叠被取消的 branch 的更深级
                    CollapseFrom(depth + 1);
                }
            }

            _selected[depth].Add(index);

            if (isBranch)
            {
                // 清空更深级选中
                for (int d = depth + 1; d < _selected.Length; d++) _selected[d].Clear();
                ClearBar(depth + 1);
                for (int d = depth + 2; d < buttonBars.Length; d++) ClearBar(d);
                PopulateBar(depth + 1, btn.children.ToArray());
                _currentDepth = Math.Max(_currentDepth, depth + 1);
            }
        }

        _activeFilter = BuildFilter();
        RebuildGrid();
        RefreshAllBarHighlights();
    }

    BtnDef FindBtnAt(int depth, int idx)
    {
        if (idx < 0) return null;
        BtnDef[] list = _level0;
        for (int d = 1; d <= depth; d++)
        {
            // 顺着第一个选中的 branch 往下走
            BtnDef[] next = null;
            foreach (int si in _selected[d - 1])
            {
                if (si < list.Length && list[si].children != null)
                { next = list[si].children.ToArray(); break; }
            }
            if (next == null) return null;
            list = next;
        }
        if (list == null || idx >= list.Length) return null;
        return list[idx];
    }

    void CollapseFrom(int depth)
    {
        for (int d = depth; d < _selected.Length; d++) _selected[d].Clear();
        for (int d = depth; d < buttonBars.Length; d++) ClearBar(d);
        UpdateCurrentDepth();
    }

    /// <summary>
    /// 递归构建 filter：选中节点的 filter AND (选中子节点的 filter 的 OR)。
    /// 同级多选之间用 OR，层级之间用 AND。叶子节点无选中 = pass。
    /// </summary>
    Func<CardData, bool> BuildFilter()
    {
        return BuildFilterRecursive(0, _level0);
    }

    Func<CardData, bool> BuildFilterRecursive(int depth, BtnDef[] btns)
    {
        if (btns == null || _selected[depth].Count == 0) return null;

        // 同级选中 OR
        var orParts = new List<Func<CardData, bool>>();
        foreach (int i in _selected[depth])
        {
            if (i >= btns.Length) continue;
            var btn = btns[i];
            Func<CardData, bool> childFilter = null;
            if (btn.children != null && btn.children.Count > 0)
                childFilter = BuildFilterRecursive(depth + 1, btn.children.ToArray());

            Func<CardData, bool> combined;
            if (btn.filter != null && childFilter != null)
                combined = d => btn.filter(d) && childFilter(d);
            else if (btn.filter != null)
                combined = btn.filter;
            else if (childFilter != null)
                combined = childFilter;
            else
                combined = null;

            if (combined != null) orParts.Add(combined);
        }

        if (orParts.Count == 0) return null;
        if (orParts.Count == 1) return orParts[0];
        return d => { foreach (var f in orParts) if (f(d)) return true; return false; };
    }

    void UpdateCurrentDepth()
    {
        _currentDepth = 0;
        for (int d = 0; d < _selected.Length; d++)
            if (d < buttonBars.Length && buttonBars[d].childCount > 0)
                _currentDepth = d;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 按钮栏操作
    // ═══════════════════════════════════════════════════════════════════

    void PopulateBar(int depth, BtnDef[] btns)
    {
        ClearBar(depth);
        if (depth >= buttonBars.Length || btns == null) return;
        var bar = buttonBars[depth];
        if (bar == null || filterButtonPrefab == null) return;

        bar.gameObject.SetActive(true);

        // 移除旧布局组件，换上 GridLayoutGroup 自动换行
        var oldHlg = bar.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (oldHlg) DestroyImmediate(oldHlg);
        var oldVlg = bar.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (oldVlg) DestroyImmediate(oldVlg);
        var oldCsf = bar.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (oldCsf) DestroyImmediate(oldCsf);

        var grid = bar.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (!grid) grid = bar.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = btnCellSize;
        grid.spacing = btnSpacing;
        grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = buttonsPerRow;

        for (int i = 0; i < btns.Length; i++)
        {
            var go = Instantiate(filterButtonPrefab, bar);
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = btns[i].label;
            var b = go.GetComponent<Button>();
            if (b)
            {
                int d = depth, idx = i;
                var btn = btns[i];
                b.onClick.AddListener(() => OnBtnClicked(d, idx, btn));
            }
        }
    }

    void ClearBar(int depth)
    {
        if (depth >= buttonBars.Length || buttonBars[depth] == null) return;
        foreach (Transform t in buttonBars[depth]) Destroy(t.gameObject);
        buttonBars[depth].gameObject.SetActive(false);
    }

    void ClearAllBars()
    {
        for (int d = 0; d < buttonBars.Length; d++) ClearBar(d);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 高亮
    // ═══════════════════════════════════════════════════════════════════

    Color normalColor = Color.white;
    Color selectedColor = Color.yellow;

    void RefreshAllBarHighlights()
    {
        for (int d = 0; d < buttonBars.Length; d++)
        {
            if (buttonBars[d] == null) continue;
            var sel = _selected[d];
            for (int i = 0; i < buttonBars[d].childCount; i++)
            {
                var img = buttonBars[d].GetChild(i).GetComponent<Image>();
                if (img) img.color = sel.Contains(i) ? selectedColor : normalColor;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 卡片网格（不变）
    // ═══════════════════════════════════════════════════════════════════

    void RebuildGrid()
    {
        if (cardContainer == null && scrollRect != null)
            cardContainer = scrollRect.content;
        if (cardContainer == null) return;
        _spawned.Clear();
        foreach (Transform t in cardContainer) Destroy(t.gameObject);

        var filtered = _activeFilter != null
            ? _allCards.Where(d => _activeFilter(d)).ToList()
            : _allCards;

        if (statusText) statusText.text = $"显示 {filtered.Count}/{_allCards.Count} 张";

        for (int i = 0; i < filtered.Count; i++)
        {
            var td = filtered[i];
            var prefab = td.cardType == CardType.Summon ? summonCardPrefab : spellCardPrefab;
            if (prefab == null) continue;

            var go = Instantiate(prefab, cardContainer);
            // 确保卡片可以接收悬停事件（IPointerEnterHandler 需要 RaycastTarget）
            var gfx = go.GetComponent<UnityEngine.UI.Graphic>();
            if (gfx != null) gfx.raycastTarget = true;
            var di = go.GetComponent<CardInstance>() ?? go.AddComponent<CardInstance>();
            di.InitFromTemplate(td, 0);
            go.GetComponent<CardDisplay2D>()?.RefreshWithInstance(di);

            var cv = go.GetComponent<CardView>(); if (cv) { cv.enabled = false; cv.handManager = null; }
            var drag = go.GetComponent<CardDrag>(); if (drag) drag.enabled = false;
            go.AddComponent<CardHover>();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.localScale = Vector3.one * cardScale;
            rt.sizeDelta = new Vector2(cardWidth, cardHeight);
            float sw = (cardWidth + hSpacing) * cardScale;
            float sh = (cardHeight + vSpacing) * cardScale;
            int row = i / cardsPerRow, col = i % cardsPerRow;
            rt.anchoredPosition = new Vector2(startX + col * sw, startY - row * sh);
            _spawned.Add(go);
        }

        int rows = Mathf.Max(1, Mathf.CeilToInt((float)filtered.Count / cardsPerRow));
        var crt = cardContainer.GetComponent<RectTransform>();
        if (crt && scrollRect)
        {
            float vh = scrollRect.viewport.rect.height;
            crt.sizeDelta = new Vector2(crt.sizeDelta.x,
                Mathf.Max(vh, rows * (cardHeight + vSpacing) * cardScale + Mathf.Abs(startY)));
        }
    }
}
