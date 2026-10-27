using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDisplayPanel : MonoBehaviour
{
    public static CardDisplayPanel Instance { get; private set; }
    public GameObject panelRoot;
    public Transform cardContainer;
    public ScrollRect scrollRect;
    public int cardsPerRow = 5;
    public float cardSpacing = 25f, rowSpacing = 25f, startX = -172f, startY = 282f;

    [Header("选牌布局")]
    public float cardScale = 2.14f;      // 2.85 × 0.75；按可用宽度自动收，不会顶到左右安全区
    public float cardGapRatio = 0.26f;   // 卡间距 = 卡宽 × 该比例
    public float rowGapRatio = 0.18f;    // 行距 = 卡高 × 该比例
    public float leftPadding = 300f;     // 左侧留白（1920 参考分辨率下约 230 屏幕像素）
    public float rightPadding = 485f;    // 右侧让开牌库 / 结束回合栏（约 372 屏幕像素）
    public float verticalPadding = 220f; // 顶部让开 120 高的顶栏后再留出约 100 的间隔

    [Header("选牌动效")]
    public float appearGap = 0.055f;     // 逐张滑入的错峰
    public float appearTime = 0.24f;     // 单张滑入时长
    public float riseRatio = 0.24f;      // 从下方滑入的距离 = 卡高 × 该比例
    public float hoverLift = 28f;        // 悬停抬起
    public float hoverScale = 1.07f;     // 悬停放大
    public float hoverOthersDim = 0.74f; // 悬停时其余卡压暗
    public float hoverTime = 0.11f;      // 悬停过渡
    public float selectedScale = 1.14f;  // 已选卡持续放大
    public float selectedOtherDim = 0.90f;
    public float selectTime = 0.16f;     // 点选 / 反选的过渡
    public float disabledAlpha = 0.40f;  // 不满足选择条件的卡

    public bool multiSelect = false;
    public int maxSelect = 0;              // 多选上限（0=不限；达到上限后点选未选卡无效，02111/02307 弃/留 N 用）
    public bool confirmWhenEmpty = false;  // 多选且为 true：允许 0 张确认（确认键常显，02111 换洗保0全弃用）
    public bool confirmWhenFull = false;   // 多选且为 true：必须选满 maxSelect 才显示确认键（02307 很多牌弃4用）
    public bool showBack;        // 无畏者等：显示卡牌背面而非正面
    public string backLabel = "反制牌"; // 背面标题（可为召唤物自定义）
    public bool enableCostCheck;
    public int maxTotalCost;
    private List<CardInstance> cards;
    private Func<CardInstance, bool> filter;
    private CardInstance selected;
    private List<CardInstance> selectedCards = new List<CardInstance>();
    private Action onOk;
    private List<GameObject> createdCards = new List<GameObject>();
    private readonly List<CardEntry> entries = new List<CardEntry>();
    private int hoverIndex = -1;
    private bool confirmHomeCaptured;
    private Vector2 confirmHomePos;

    class CardEntry
    {
        public GameObject go;
        public RectTransform rt;
        public CanvasGroup group;
        public CardInstance inst;
        public Vector2 basePos;
        public float baseScale;
        public bool selectable;
        public int index;
        public int glideToken;
        public bool revealing;
    }

    void Awake() { Instance = this; panelRoot.SetActive(false); }

    /// <summary>面板是否正在展示（供 TurnButtonGate 判定"弹窗期间禁止结束回合/抽牌"）。
    /// 用 panelRoot 的实际激活状态派生，而不是另立一个布尔锁——Show/Hide 任一路径漏掉都不会残留。</summary>
    public bool IsShowing => panelRoot != null && panelRoot.activeInHierarchy;

    /// <summary>场上 3D 卡的鼠标消息不经过 EventSystem；面板开着时由 Card3DHover 查这里统一挡掉。</summary>
    public static bool BlocksWorldInput => Instance != null && Instance.IsShowing;

    public void Show(List<CardInstance> list, Func<CardInstance, bool> f, string txt = "确认")
    {
        cards = list; filter = f; onOk = null; selected = null;
        selectedCards.Clear();

        var player = Player.Instance;
        if (player != null)
        {
            player.handCards.RemoveAll(c => c == null);
            foreach (var c in player.handCards) if (c) c.SetActive(false);
        }

        // 弹窗期间的"结束回合/抽牌"禁用不再直接写按钮：按钮开关由 TurnButtonGate 按本面板的
        // IsShowing 派生（直接写会在"弹窗被顶掉、收尾没走到"时留下永久禁用 → 自己回合点不动按钮）。
        // 这里只关掉弃牌通道，真正的按钮重算放在 panelRoot 激活之后（见方法尾）。
        Card3DHover.allowDiscard = false;

        var hm = FindObjectOfType<HandManager>();
        if (hm) hm.SetHandAreaRaycast(false);

        // 弹窗期间禁止结束回合 — 任何需要玩家查看/选择的弹窗都应阻塞阶段推进（由 TurnButtonGate 统一判定）

        StopAllCoroutines();
        hoverIndex = -1;
        entries.Clear();
        foreach (Transform t in cardContainer)
        {
            t.gameObject.SetActive(false);
            Destroy(t.gameObject);
        }
        createdCards.Clear();

        ConfigurePanelFrame();
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        PositionConfirmButton();

        bool isGame = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game";
        const float rawCardW = 83.333333f;
        const float rawCardH = 146.333333f;

        var valid = new List<CardInstance>();
        var seenIDs = new HashSet<string>(StringComparer.Ordinal);
        var seenRefs = new HashSet<CardInstance>();
        if (cards != null)
        {
            foreach (var ci in cards)
            {
                if (ci == null) continue;
                if (CardDatabase.Instance?.GetTemplate(ci.templateID) == null) continue;

                // 防御重复候选：同一个实例可能同时留在手牌列表和场上快照里。
                // 有 instanceID 时按 ID 去重；没有 ID 的临时卡按对象引用去重。
                if (!string.IsNullOrEmpty(ci.instanceID))
                {
                    if (!seenIDs.Add(ci.instanceID)) continue;
                }
                else if (!seenRefs.Add(ci))
                {
                    continue;
                }
                valid.Add(ci);
            }
        }

        int total = valid.Count;
        int maxPerRow = Mathf.Max(1, cardsPerRow);
        int columns = Mathf.Clamp(total, 1, maxPerRow);
        float scale = isGame ? Mathf.Max(1f, cardScale) : 1f;

        float viewportW = Mathf.Max(320f, scrollRect.viewport.rect.width);
        float viewportH = Mathf.Max(240f, scrollRect.viewport.rect.height);
        float leftInset = Mathf.Clamp(leftPadding, 0f, viewportW * 0.22f);
        float rightInset = Mathf.Clamp(rightPadding, 0f, viewportW * 0.30f);
        float maxRowWidth = Mathf.Max(200f, viewportW - leftInset - rightInset);
        float wantedRowWidth = columns * rawCardW * scale
                             + (columns - 1) * rawCardW * scale * Mathf.Max(0f, cardGapRatio);
        if (wantedRowWidth > maxRowWidth)
            scale *= maxRowWidth / wantedRowWidth;

        float visualW = rawCardW * scale;
        float visualH = rawCardH * scale;
        float gapX = visualW * Mathf.Max(0f, cardGapRatio);
        float gapY = visualH * Mathf.Max(0f, rowGapRatio);
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)total / columns));
        float rowsHeight = rows * visualH + (rows - 1) * gapY;
        float contentH = Mathf.Max(viewportH, rowsHeight + Mathf.Max(0f, verticalPadding) * 2f);

        var crt = cardContainer.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, contentH);

        for (int i = 0; i < total; i++)
        {
            var ci = valid[i];
            if (!ci) continue;
            var td = CardDatabase.Instance.GetTemplate(ci.templateID);
            if (!td) continue;

            var prefab = td.cardType == CardType.Spell ? player.spellCardPrefab2D : player.cardPrefab2D;
            var go = Instantiate(prefab, cardContainer);
            if (isGame) Player.Scale2DCard(go);
            go.transform.localScale = Vector3.one * scale;

            var cv = go.GetComponent<CardView>();
            if (cv) { cv.enabled = false; cv.handManager = null; }
            var cd2 = go.GetComponent<CardDrag>();
            if (cd2) cd2.enabled = false;

            var di = go.GetComponent<CardInstance>() ?? go.AddComponent<CardInstance>();
            di.templateID = ci.templateID;
            // instanceID 可能为 null/空（临时 CardInstance），同名不同实例的卡会因 null==null 被误判为同一张卡。
            // 用 _temp_ + 索引 生成唯一临时 ID，确保 Click 中按 instanceID 的去重/反选逻辑正确。
            di.instanceID = string.IsNullOrEmpty(ci.instanceID) ? $"_temp_{i}" : ci.instanceID;
            di.currentCost = ci.currentCost;
            di.currentAttack = ci.currentAttack;
            di.currentHealth = ci.currentHealth;
            di.currentMaxHealth = ci.currentMaxHealth;
            di.currentTier = ci.currentTier;
            di.prefixes = ci.prefixes;
            // 赋予的特性文本也复制 — 详情面板需要展示
            di.grantedTraitTexts = ci.grantedTraitTexts != null ? new List<string>(ci.grantedTraitTexts) : null;
            di.hasShield = ci.hasShield;
            di.poisoned = ci.poisoned;
            // 背面模式：ShowBack（无畏者等隐藏卡），正常模式：RefreshWithInstance
            if (showBack)
                go.GetComponent<CardDisplay2D>()?.ShowBack(td, backLabel);
            else
                go.GetComponent<CardDisplay2D>()?.RefreshWithInstance(di);

            int row = i / columns;
            int col = i % columns;
            float x = leftInset + visualW * 0.5f + col * (visualW + gapX);
            float y = -(Mathf.Max(0f, verticalPadding) + visualH * 0.5f + row * (visualH + gapY));

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(rawCardW, rawCardH);
            rt.anchoredPosition = new Vector2(x, y);

            var entry = new CardEntry
            {
                go = go,
                rt = rt,
                group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>(),
                inst = di,
                basePos = new Vector2(x, y),
                baseScale = scale,
                selectable = f == null || f(ci),
                index = i
            };

            if (entry.selectable)
            {
                var btn = go.AddComponent<Button>();
                btn.onClick.AddListener(() => Click(entry.inst, entry.go));
            }

            if (go.GetComponent<CardHover>() == null) go.AddComponent<CardHover>();
            var hover = go.AddComponent<CardDisplayHover>();
            hover.panel = this;
            hover.index = i;

            entry.group.alpha = 0f;
            entry.group.blocksRaycasts = false;
            entry.revealing = true;
            entries.Add(entry);
            createdCards.Add(go);
            StartCoroutine(RevealEntry(entry, Mathf.Min(i, 8) * Mathf.Max(0f, appearGap)));
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
        TurnButtonGate.Refresh(); // panelRoot 已激活 → 此刻起结束回合/抽牌保持禁用

        // 多选 + confirmWhenEmpty：确认键常显，允许 0 张确认（onOk 在 ShowWithCallback 中随后赋值，
        // 闭包延迟读取字段，按钮回调时已就绪）。02111 换洗(全弃)/02307(弃0) 反选场景。
        if (multiSelect && confirmWhenEmpty)
        {
            var csb = ConfirmSelectionButton.Instance;
            if (csb) csb.Show(() => { onOk?.Invoke(); Hide(); });
        }
    }

    public void ShowWithCallback(List<CardInstance> list, Func<CardInstance, bool> f, Action ok, string txt = "确认")
    {
        // AI 环境：不弹面板，自动选第一个合法牌并触发回调
        if (SimpleAI.IsAIEvaluating)
        {
            cards = list;
            filter = f;
            selected = null;
            selectedCards.Clear();
            if (list != null)
            {
                foreach (var ci in list)
                {
                    if (ci == null) continue;
                    if (f == null || f(ci))
                    {
                        if (multiSelect) selectedCards.Add(ci);
                        else selected = ci;
                        break;
                    }
                }
            }
            onOk = ok;
            ok?.Invoke();
            return;
        }

        Show(list, f, txt);
        onOk = ok;
    }
    void Click(CardInstance ci, GameObject go)
    {
        Debug.Log($"Click进入: multiSelect={multiSelect}, ci={ci?.templateID}, iid={ci?.instanceID}");

        if (multiSelect)
        {
            bool alreadySelected = selectedCards.Exists(s => s.instanceID == ci.instanceID);
            if (alreadySelected)
            {
                selectedCards.RemoveAll(s => s.instanceID == ci.instanceID);
            }
            else
            {
                if (maxSelect > 0 && selectedCards.Count >= maxSelect) return; // 已达上限，忽略点选未选卡
                selectedCards.Add(ci);
            }
            RefreshCardVisuals(selectTime);

            bool showConfirm = true;
            if (enableCostCheck)
            {
                int totalCost = 0;
                foreach (CardInstance c in selectedCards)
                {
                    CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
                    if (td != null) totalCost += td.baseCost;
                }
                if (selectedCards.Count == 0 || totalCost > maxTotalCost)
                    showConfirm = false;
            }

            if (showConfirm && ShouldShowConfirmButton())
            {
                var csb = ConfirmSelectionButton.Instance;
                if (csb)
                {
                    csb.gameObject.SetActive(true);
                    csb.Show(() =>
                    {
                        onOk?.Invoke();
                        Hide();
                    });
                }
            }
            else
            {
                ConfirmSelectionButton.Instance?.Hide();
            }
        }
        else
        {
            if (selected != null && selected.instanceID == ci.instanceID)
            {
                selected = null;
                ConfirmSelectionButton.Instance?.Hide();
            }
            else
            {
                selected = ci;
                Test1Panel.Instance?.Show(ci);
                var csb = ConfirmSelectionButton.Instance;
                if (csb)
                {
                    csb.gameObject.SetActive(true);
                    csb.Show(() =>
                    {
                        onOk?.Invoke();
                        Hide();
                    });
                }
            }
            RefreshCardVisuals(selectTime);
        }
    }

    public void Hide()
    {
        StopAllCoroutines();
        hoverIndex = -1;
        showBack = false;
        enableCostCheck = false;
        maxSelect = 0;
        confirmWhenEmpty = false;
        confirmWhenFull = false;
        panelRoot.SetActive(false);
        var csb = ConfirmSelectionButton.Instance;
        if (csb) csb.Hide();
        RestoreConfirmButtonPosition();

        var player = Player.Instance;
        if (player != null)
        {
            player.handCards.RemoveAll(c => c == null);
            foreach (var c in player.handCards) if (c) c.SetActive(true);
        }

        // 关闭后重算按钮（由 TurnButtonGate 判定：非己方回合或仍有其它 UI 锁时保持禁用）
        TurnButtonGate.Refresh();
        Card3DHover.allowDiscard = true;

        var hm = FindObjectOfType<HandManager>();
        if (hm)
        {
            hm.SetHandAreaRaycast(true);
            hm.RefreshLayout(true);
        }
    }

    /// <summary>把场景里的旧竖条面板改成整屏选牌层：深色压暗 + 满屏滚动区 + 顶部对齐的内容容器。</summary>
    void ConfigurePanelFrame()
    {
        var root = panelRoot.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
        root.localScale = Vector3.one;

        var bg = panelRoot.transform.Find("PanelBackground")?.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color32(6, 9, 14, 204); // #06090E，约 80% 压暗；保留一点战场轮廓
            bg.raycastTarget = true;
        }

        if (scrollRect == null) return;
        var srt = scrollRect.transform as RectTransform;
        if (srt != null)
        {
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = Vector2.zero;
        }

        var svImage = scrollRect.GetComponent<Image>();
        if (svImage != null)
        {
            svImage.color = new Color(1f, 1f, 1f, 0f);
            svImage.raycastTarget = true;
        }

        var viewport = scrollRect.viewport;
        if (viewport != null)
        {
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = Vector2.zero;
        }

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.08f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 60f;
    }

    /// <summary>确认键挪到屏幕右上角；它被很多其它选择流程共用，关闭本面板时还原原位。</summary>
    void PositionConfirmButton()
    {
        var csb = ConfirmSelectionButton.Instance;
        var rt = csb != null ? csb.transform as RectTransform : null;
        if (rt == null || scrollRect == null || scrollRect.viewport == null) return;

        if (!confirmHomeCaptured)
        {
            confirmHomePos = rt.anchoredPosition;
            confirmHomeCaptured = true;
        }

        float vw = Mathf.Max(320f, scrollRect.viewport.rect.width);
        float vh = Mathf.Max(240f, scrollRect.viewport.rect.height);
        float halfW = rt.sizeDelta.x * 0.5f;
        float halfH = rt.sizeDelta.y * 0.5f;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(vw * 0.5f - halfW - 40f, vh * 0.5f - halfH - 40f);
    }

    void RestoreConfirmButtonPosition()
    {
        if (!confirmHomeCaptured) return;
        var csb = ConfirmSelectionButton.Instance;
        var rt = csb != null ? csb.transform as RectTransform : null;
        if (rt != null) rt.anchoredPosition = confirmHomePos;
    }

    /// <summary>卡片悬停探针的转发入口：只改表现，不参与选牌规则。</summary>
    public void OnCardHover(int index, bool on)
    {
        if (!IsShowing || index < 0 || index >= entries.Count) return;
        var entry = entries[index];
        if (entry == null || entry.rt == null) return;

        if (on)
        {
            if (!entry.selectable || entry.revealing) return;
            hoverIndex = index;
            entry.rt.SetAsLastSibling();
        }
        else
        {
            entry.rt.SetSiblingIndex(entry.index);
            if (hoverIndex != index) return;
            hoverIndex = -1;
        }
        RefreshCardVisuals(hoverTime);
    }

    bool IsSelected(CardInstance instance)
    {
        if (instance == null) return false;
        if (multiSelect) return selectedCards.Exists(s => s != null && s.instanceID == instance.instanceID);
        return selected != null && selected.instanceID == instance.instanceID;
    }

    float AlphaFor(CardEntry entry, bool anyHover)
    {
        if (entry == null) return 1f;
        if (!entry.selectable) return disabledAlpha;
        if (!anyHover) return 1f;
        if (entry.index == hoverIndex) return 1f;
        return IsSelected(entry.inst) ? selectedOtherDim : hoverOthersDim;
    }

    void RefreshCardVisuals(float duration)
    {
        bool anyHover = hoverIndex >= 0 && hoverIndex < entries.Count
                        && entries[hoverIndex] != null && entries[hoverIndex].selectable;
        if (anyHover && entries[hoverIndex].rt != null)
            entries[hoverIndex].rt.SetAsLastSibling();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.rt == null || entry.revealing) continue;

            bool hot = anyHover && i == hoverIndex;
            float targetMul = IsSelected(entry.inst) ? selectedScale : 1f;
            if (hot) targetMul = Mathf.Max(targetMul, hoverScale);

            Vector2 targetPos = entry.basePos + (hot ? new Vector2(0f, hoverLift) : Vector2.zero);
            float targetAlpha = AlphaFor(entry, anyHover);
            StartCoroutine(GlideCard(entry, targetPos, entry.baseScale * targetMul, targetAlpha, duration));
        }
    }

    IEnumerator RevealEntry(CardEntry entry, float delay)
    {
        if (entry == null || entry.rt == null || entry.group == null) yield break;
        entry.revealing = true;
        entry.group.blocksRaycasts = false;

        float rise = entry.rt.sizeDelta.y * entry.baseScale * Mathf.Max(0f, riseRatio);
        entry.rt.anchoredPosition = entry.basePos + new Vector2(0f, -rise);
        entry.rt.localScale = Vector3.one * (entry.baseScale * 0.97f);
        entry.group.alpha = 0f;

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float t = 0f;
        float dur = Mathf.Max(0.01f, appearTime);
        while (t < dur && entry.rt != null)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float move = Mathf.Sin(p * Mathf.PI * 0.5f); // 起步即走，落位最缓
            float fade = 1f - Mathf.Pow(1f - Mathf.Clamp01(p / 0.62f), 3f);
            bool anyHover = hoverIndex >= 0 && hoverIndex < entries.Count
                            && entries[hoverIndex] != null && entries[hoverIndex].selectable;
            entry.rt.anchoredPosition = Vector2.Lerp(entry.basePos + new Vector2(0f, -rise), entry.basePos, move);
            entry.rt.localScale = Vector3.one * Mathf.Lerp(entry.baseScale * 0.97f, entry.baseScale, move);
            entry.group.alpha = Mathf.Lerp(0f, AlphaFor(entry, anyHover), fade);
            yield return null;
        }

        if (entry.rt == null) yield break;
        bool hoverNow = hoverIndex >= 0 && hoverIndex < entries.Count
                        && entries[hoverIndex] != null && entries[hoverIndex].selectable;
        entry.rt.anchoredPosition = entry.basePos;
        entry.rt.localScale = Vector3.one * entry.baseScale;
        entry.group.alpha = AlphaFor(entry, hoverNow);
        entry.group.blocksRaycasts = true;
        entry.revealing = false;
    }

    IEnumerator GlideCard(CardEntry entry, Vector2 to, float scale, float alpha, float dur)
    {
        if (entry == null || entry.rt == null || entry.group == null) yield break;
        int token = ++entry.glideToken;
        Vector2 fromPos = entry.rt.anchoredPosition;
        float fromScale = entry.rt.localScale.x;
        float fromAlpha = entry.group.alpha;
        float t = 0f;
        dur = Mathf.Max(0.01f, dur);

        while (t < dur)
        {
            if (entry.rt == null || entry.glideToken != token) yield break;
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            entry.rt.anchoredPosition = Vector2.Lerp(fromPos, to, p);
            entry.rt.localScale = Vector3.one * Mathf.Lerp(fromScale, scale, p);
            entry.group.alpha = Mathf.Lerp(fromAlpha, alpha, p);
            yield return null;
        }

        if (entry.rt == null || entry.glideToken != token) yield break;
        entry.rt.anchoredPosition = to;
        entry.rt.localScale = Vector3.one * scale;
        entry.group.alpha = alpha;
    }

    public CardInstance GetSelectedCard() => selected;
    public List<CardInstance> GetSelectedCards() => selectedCards;

    /// <summary>多选确认键显隐策略：confirmWhenFull→选满 maxSelect；confirmWhenEmpty→0 张也显示；否则默认 ≥1 张。</summary>
    bool ShouldShowConfirmButton()
    {
        if (confirmWhenFull) return maxSelect > 0 && selectedCards.Count >= maxSelect;
        if (confirmWhenEmpty) return true;
        return selectedCards.Count > 0;
    }
}

/// <summary>选牌弹窗卡上的悬停探针：只把进出事件转发给面板，表现全在 CardDisplayPanel 里做。</summary>
public class CardDisplayHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CardDisplayPanel panel;
    public int index;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (panel != null) panel.OnCardHover(index, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (panel != null) panel.OnCardHover(index, false);
    }
}
