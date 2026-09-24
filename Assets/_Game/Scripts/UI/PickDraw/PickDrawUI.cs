using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 择牌（Pick Draw）面板 —— 屏幕中央的 2D 三选一，纯运行时构建，不依赖场景与预制体。
///
/// 两个视角：
///   · 选择者（ShowChooser）：牌库顶三张的正面依次亮出，点一张加入手牌；
///     其余两张打「明弃」角标后淡出。
///   · 旁观者（ShowSpectator）：同一位置只看到三张牌背；待对方选定后，
///     被明弃的那两张翻为正面对其展示（SpectatorReveal），停留片刻后淡出。
///
/// 挂在与手牌同一块主 Canvas 下（PlayRevealManager.FindMainCanvas），永远压在最上层。
/// 选择者模式下整块面板吃射线（挡掉手牌 / 结束回合等交互）；旁观者模式不吃射线，不打断对方操作。
/// </summary>
public class PickDrawUI : MonoBehaviour
{
    public static PickDrawUI Instance { get; private set; }

    [Header("节奏（秒）")]
    public float dimAlpha = 0.68f;      // 压暗底透明度
    public float cardScale = 3f;        // 与 Player.Scale2DCard 同一倍率
    public float gapRatio = 0.26f;      // 卡与卡的间隙 = 卡宽 × 该比例
    public float appearGap = 0.06f;     // 依次展示的间隔（左→右）
    public float appearTime = 0.10f;    // 单张亮起时长
    public float riseRatio = 0.22f;     // 亮起动效：从下方滑入的距离 = 卡高 × 该比例（起点更靠近终点）
    public float popScale = 1.14f;      // 选定那一张的高亮过冲倍率
    public float popLift = 64f;         // 选定那一张上抬的距离
    public float punchTime = 0.20f;     // 选定那一张「冲一下」的时长（放大过冲 + 选定框亮起）
    public float dimOthers = 0.30f;     // 未被选中那两张的压暗透明度
    public float haloAlpha = 1f;        // 选定卡身后「费用色框」的透明度（素材自带配色，默认原样）
    public float haloPad = 26f;         // 选定框比卡每边宽出的像素
    public float hoverLift = 30f;       // 悬停抬起距离
    public float hoverScale = 1.07f;    // 悬停放大倍率
    public float hoverOthersDim = 0.72f;// 悬停时其余两张的压暗
    public float hoverTime = 0.10f;     // 悬停 / 移开的推移时长
    public float resolveTime = 0.24f;   // 选定后停留（让「加入手牌 / 明弃」读得出来）
    public float flipHalf = 0.13f;      // 旁观者翻牌半程
    public float discardHold = 1.25f;   // 旁观者看清「明弃」正面后的停留

    [Header("文案")]
    public string chooserTitle = "择　牌";
    public string chooserHint = "选择一张加入手牌　其余两张将对对方明弃";
    public string spectatorTitle = "对方择牌";
    public string spectatorHint = "对方正在从牌库顶三张中挑选一张";
    public string spectatorRevealHint = "对方已选定　未选中的对其明弃";
    const string discardBadge = "明弃";

    // ── 运行时构建的引用 ──
    RectTransform _row;
    Image _dim;
    CanvasGroup _root;
    TextMeshProUGUI _title;
    TextMeshProUGUI _hint;

    readonly List<Slot> _slots = new List<Slot>();
    bool _built;
    bool _visible;
    bool _chooser;
    bool _resolved;
    Action<int> _onPick;
    int _seq;
    int _hoverIndex = -1;
    float _cardW = 83.33f * 3f;   // 卡宽（像素，含运行时缩放）
    float _cardH = 146.33f * 3f;  // 卡高（像素，含运行时缩放）

    class Slot
    {
        public RectTransform holder;
        public GameObject card;
        public CanvasGroup group;
        public GameObject badge;
        public Vector2 basePos;
        public Image halo;
        public int cost;             // 该槽位的费用（0-5）：决定选定框颜色
        public int glideToken;       // 同一槽位后发起的推移顶掉前一个
        public bool revealing;       // 亮起动画进行中：不接受悬停推移
    }

    // ══════════════════════════════════════════════════════════════════
    // 对外接口
    // ══════════════════════════════════════════════════════════════════

    /// <summary>本端是否正开着择牌面板。</summary>
    public static bool IsOpen => Instance != null && Instance._visible;

    /// <summary>选择者视角：亮出牌库顶若干张的正面，点哪张回调哪个下标。</summary>
    public static void ShowChooser(string[] templateIDs, Action<int> onPick)
    {
        Ensure();
        if (Instance == null) return;
        Instance.StopAllCoroutines();
        Instance.BeginShow(chooser: true);
        Instance.StartCoroutine(Instance.ChooserRoutine(templateIDs, onPick));
    }

    /// <summary>旁观者视角：只亮牌背。</summary>
    public static void ShowSpectator(int count)
    {
        Ensure();
        if (Instance == null) return;
        Instance.StopAllCoroutines();
        Instance.BeginShow(chooser: false);
        Instance.StartCoroutine(Instance.SpectatorRoutine(count));
    }

    /// <summary>旁观者：把被明弃的那两张翻成正面对其展示（indices 是它们在牌堆三张里的位置）。
    /// 对方侧不打「明弃」红条 —— 红条只给抽牌方自己看。</summary>
    public static void SpectatorReveal(int[] indices, string[] templateIDs)
    {
        if (Instance == null || !Instance._visible || Instance._chooser) return;
        Instance.StartCoroutine(Instance.SpectatorRevealRoutine(indices, templateIDs));
    }

    /// <summary>选择者：服务端回执（正常路径下面板已自行退场；此处只兜底关掉）。</summary>
    public static void ChooserConfirm(int index)
    {
        // 正常路径下点击时已自行进入退场动画（_resolved=true），此处只兜底
        if (Instance == null || !Instance._visible || !Instance._chooser || Instance._resolved) return;
        Instance.StartCoroutine(Instance.ChooserResolveRoutine(index));
    }

    /// <summary>服务端中止（对方断线 / 回合已过）：直接收起面板。</summary>
    public static void Abort()
    {
        if (Instance == null) return;
        Instance.StopAllCoroutines();
        Instance.Hide();
    }

    // ══════════════════════════════════════════════════════════════════
    // 构建
    // ══════════════════════════════════════════════════════════════════

    static void Ensure()
    {
        if (Instance != null) return;
        var canvasGo = PlayRevealManager.FindMainCanvas();
        if (canvasGo == null) { Debug.LogWarning("[PickDraw] 找不到主 Canvas，跳过择牌面板"); return; }
        var go = new GameObject("PickDrawUI", typeof(RectTransform), typeof(PickDrawUI));
        go.transform.SetParent(canvasGo.transform, false);
        go.transform.SetAsLastSibling();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        Hide();
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);

        _root = gameObject.AddComponent<CanvasGroup>();

        var dim = NewRect("Dim", root);
        Stretch(dim);
        _dim = dim.gameObject.AddComponent<Image>();
        _dim.color = new Color(0.02f, 0.02f, 0.03f, dimAlpha);
        _dim.raycastTarget = false;   // 吃射线由根 CanvasGroup 统一控制

        _title = NewText("Title", root, 46f);
        _title.color = new Color(0.984f, 0.965f, 0.925f, 1f);

        _hint = NewText("Hint", root, 23f);
        _hint.color = new Color(0.847f, 0.788f, 0.667f, 0.95f);

        _row = NewRect("Row", root);
        _row.anchorMin = _row.anchorMax = new Vector2(0.5f, 0.5f);
        _row.pivot = new Vector2(0.5f, 0.5f);
        _row.anchoredPosition = Vector2.zero;
        _row.sizeDelta = new Vector2(1400f, 620f);
    }

    void BeginShow(bool chooser)
    {
        _chooser = chooser;
        _resolved = false;
        _onPick = null;
        ResetSlots();

        _root.alpha = 1f;
        _root.interactable = chooser;
        _root.blocksRaycasts = chooser;   // 旁观者不吃射线，不打断对方操作
        _dim.raycastTarget = chooser;     // 选择者：整屏挡掉手牌 / 结束回合等交互
        _dim.enabled = true;
        _title.gameObject.SetActive(true);
        _hint.gameObject.SetActive(true);
        _row.gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _visible = true;
    }

    void Hide()
    {
        ResetSlots();
        _visible = false;
        _chooser = false;
        _onPick = null;
        if (_root != null)
        {
            _root.alpha = 0f;
            _root.interactable = false;
            _root.blocksRaycasts = false;
        }
        if (_dim != null) _dim.enabled = false;
        if (_title != null) _title.gameObject.SetActive(false);
        if (_hint != null) _hint.gameObject.SetActive(false);
        if (_row != null) _row.gameObject.SetActive(false);
    }

    void ResetSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].holder != null) Destroy(_slots[i].holder.gameObject);
        _slots.Clear();
    }

    // ══════════════════════════════════════════════════════════════════
    // 选择者
    // ══════════════════════════════════════════════════════════════════

    IEnumerator ChooserRoutine(string[] templateIDs, Action<int> onPick)
    {
        _onPick = onPick;
        _title.text = chooserTitle;
        _hint.text = chooserHint;

        int n = templateIDs != null ? templateIDs.Length : 0;

        // 先全部建出来（顺带量出卡宽/卡高），再统一排布，最后依次亮起。
        // 注意：下标必须与服务端 _pickDrawHeld 一一对应 —— 模板取不到也要占一个槽，不能跳过。
        if (n == 0) { Hide(); yield break; }

        for (int i = 0; i < n; i++)
        {
            var td = CardDatabase.Instance?.GetTemplate(templateIDs[i]);
            var s = CreateSlot();
            s.card = BuildCard(td, back: td == null, parent: s.holder);
            s.cost = td != null ? Mathf.Clamp(td.baseCost, 0, 5) : 0;   // 选定框颜色跟费用走
            s.group.alpha = 0f;
            s.group.blocksRaycasts = false;
        }
        LayoutSlots();

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            s.revealing = true;
            yield return RevealSlot(s);
            s.revealing = false;
            int idx = i;
            if (s.card != null)
            {
                var click = s.card.GetComponent<CardClickHandler>();
                if (click == null) click = s.card.AddComponent<CardClickHandler>();
                click.onClick = () => OnCardPicked(idx);

                var hover = s.card.GetComponent<PickHoverGlow>();
                if (hover == null) hover = s.card.AddComponent<PickHoverGlow>();
                hover.ui = this;
                hover.index = idx;
            }
            s.group.blocksRaycasts = true;
            if (i < _slots.Count - 1)
                yield return new WaitForSeconds(appearGap);
        }
    }

    void OnCardPicked(int index)
    {
        if (!_visible || !_chooser || _resolved) return;
        _resolved = true;
        var cb = _onPick;
        _onPick = null;
        cb?.Invoke(index);                       // 立刻上报服务端，动画在本端继续放
        StartCoroutine(ChooserResolveRoutine(index));
    }

    IEnumerator ChooserResolveRoutine(int index)
    {
        _resolved = true;
        _root.interactable = false;
        _root.blocksRaycasts = false;
        _hoverIndex = -1;

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s == null) continue;
            if (i == index) continue;
            s.group.alpha = dimOthers;      // 未选中的压得更暗
            ShowBadge(s);                   // 「明弃」红条
            StartCoroutine(GlideSlot(s, s.basePos, 0.94f, dimOthers, punchTime));
        }

        if (index >= 0 && index < _slots.Count && _slots[index] != null)
        {
            ShowHalo(_slots[index]);
            yield return PunchSlot(_slots[index]);
        }

        if (resolveTime > 0f)
            yield return new WaitForSeconds(resolveTime);

        yield return FadeRootOut(0.18f);
        Hide();
    }

    /// <summary>选定那一张：抬起 + 放大过冲 + 费用色选定框亮起，再回落到高亮位。</summary>
    IEnumerator PunchSlot(Slot s)
    {
        if (s == null || s.holder == null) yield break;
        Vector2 from = s.holder.anchoredPosition;
        float fromScale = s.holder.localScale.x;
        Vector2 to = s.basePos + new Vector2(0f, popLift);
        float punch = popScale + 0.20f;      // 冲过头
        float settle = popScale + 0.06f;     // 落点
        float half = Mathf.Max(0.02f, punchTime * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / half));
            s.holder.anchoredPosition = Vector2.Lerp(from, to, p);
            s.holder.localScale = Vector3.one * Mathf.Lerp(fromScale, punch, p);
            s.group.alpha = 1f;
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / half));
            s.holder.localScale = Vector3.one * Mathf.Lerp(punch, settle, p);
            yield return null;
        }

        s.holder.anchoredPosition = to;
        s.holder.localScale = Vector3.one * settle;
        s.group.alpha = 1f;
    }

    /// <summary>选定卡身后的框：九宫格素材 UI/PickFrame_{费用}，颜色就是这张卡自己的费用色。</summary>
    void ShowHalo(Slot s)
    {
        if (s == null || s.holder == null) return;
        if (s.halo == null)
        {
            var rt = NewRect("Halo", s.holder);
            rt.SetSiblingIndex(0);       // 压在卡下面
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(_cardW + haloPad * 2f, _cardH + haloPad * 2f);
            rt.anchoredPosition = Vector2.zero;

            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            s.halo = img;

            var spr = LoadFrameSprite(s.cost);
            if (spr != null)
            {
                img.sprite = spr;
                img.type = Image.Type.Sliced;   // 角部不缩放，四边拉伸
                img.fillCenter = false;         // 中心本就透明，别让底板把卡糊掉
                img.color = new Color(1f, 1f, 1f, 0f);
                StartCoroutine(FadeImage(img, 0f, haloAlpha, punchTime));
            }
            else
            {
                // 素材缺失时退回「一块比卡略大的底板」，否则空精灵会被画成一个大白块
                Debug.LogWarning("[PickDraw] 找不到 UI/PickFrame_" + Mathf.Clamp(s.cost, 0, 5) + "，选定框退回底板");
                img.color = new Color(1f, 0.84f, 0.46f, 0f);
                StartCoroutine(FadeImage(img, 0f, 0.34f, punchTime));
            }
        }
        else
        {
            s.halo.gameObject.SetActive(true);
        }
    }

    static readonly Sprite[] _frameCache = new Sprite[6];

    static Sprite LoadFrameSprite(int cost)
    {
        int i = Mathf.Clamp(cost, 0, 5);
        if (_frameCache[i] == null)
            _frameCache[i] = Resources.Load<Sprite>("UI/PickFrame_" + i);
        return _frameCache[i];
    }

    IEnumerator FadeImage(Image img, float from, float to, float dur)
    {
        if (img == null) yield break;
        Color c = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, p));
            yield return null;
        }
        img.color = new Color(c.r, c.g, c.b, to);
    }

    // ══════════════════════════════════════════════════════════════════
    // 悬停反馈（选择者视角）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>鼠标压在哪一张上：抬起放大那一张，其余压暗。纯表现，不改规则。</summary>
    public void OnSlotHover(int index, bool on)
    {
        if (!_visible || !_chooser || _resolved) return;
        if (on) _hoverIndex = index;
        else if (_hoverIndex == index) _hoverIndex = -1;
        RefreshHoverTargets();
    }

    void RefreshHoverTargets()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s == null || s.holder == null || s.revealing) continue;
            bool hot = (i == _hoverIndex);
            Vector2 pos = s.basePos + new Vector2(0f, hot ? hoverLift : 0f);
            float sc = hot ? hoverScale : 1f;
            float a = (_hoverIndex >= 0 && !hot) ? hoverOthersDim : 1f;
            StartCoroutine(GlideSlot(s, pos, sc, a, hoverTime));
        }
    }

    /// <summary>把槽位平滑推到目标位（同一槽位后发起的推移顶掉前一个）。</summary>
    IEnumerator GlideSlot(Slot s, Vector2 to, float scale, float alpha, float dur)
    {
        if (s == null || s.holder == null) yield break;
        int token = ++s.glideToken;
        Vector2 from = s.holder.anchoredPosition;
        float fromScale = s.holder.localScale.x;
        float fromAlpha = s.group != null ? s.group.alpha : 1f;
        float t = 0f;
        while (t < dur)
        {
            if (s.glideToken != token) yield break;
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            s.holder.anchoredPosition = Vector2.Lerp(from, to, p);
            s.holder.localScale = Vector3.one * Mathf.Lerp(fromScale, scale, p);
            if (s.group != null) s.group.alpha = Mathf.Lerp(fromAlpha, alpha, p);
            yield return null;
        }
        if (s.glideToken != token) yield break;
        s.holder.anchoredPosition = to;
        s.holder.localScale = Vector3.one * scale;
        if (s.group != null) s.group.alpha = alpha;
    }

    // ══════════════════════════════════════════════════════════════════
    // 旁观者
    // ══════════════════════════════════════════════════════════════════

    IEnumerator SpectatorRoutine(int count)
    {
        _title.text = spectatorTitle;
        _hint.text = spectatorHint;

        count = Mathf.Clamp(count, 1, 5);
        for (int i = 0; i < count; i++)
        {
            var s = CreateSlot();
            s.card = BuildCard(null, back: true, parent: s.holder);
            s.group.alpha = 0f;
        }
        LayoutSlots();

        for (int i = 0; i < _slots.Count; i++)
        {
            yield return RevealSlot(_slots[i]);
            if (i < _slots.Count - 1)
                yield return new WaitForSeconds(appearGap);
        }
    }

    IEnumerator SpectatorRevealRoutine(int[] indices, string[] templateIDs)
    {
        _hint.text = spectatorRevealHint;

        // 翻面：被明弃的那几张翻成正面对旁观者展示
        for (int k = 0; k < indices.Length; k++)
        {
            int idx = indices[k];
            if (idx < 0 || idx >= _slots.Count) continue;
            string tid = (templateIDs != null && k < templateIDs.Length) ? templateIDs[k] : null;
            var td = CardDatabase.Instance?.GetTemplate(tid);
            if (td == null) continue;
            StartCoroutine(FlipSlotToFront(_slots[idx], td));
        }

        if (discardHold > 0f)
            yield return new WaitForSeconds(discardHold + flipHalf);

        yield return FadeRootOut(0.3f);
        Hide();
    }

    IEnumerator FlipSlotToFront(Slot s, CardData td)
    {
        yield return ScaleX(s.holder, 1f, 0.03f, flipHalf);
        if (s.card != null) Destroy(s.card);
        s.card = BuildCard(td, back: false, parent: s.holder);
        yield return ScaleX(s.holder, 0.03f, 1f, flipHalf);
    }

    IEnumerator ScaleX(RectTransform rt, float from, float to, float dur)
    {
        if (rt == null) yield break;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            rt.localScale = new Vector3(Mathf.Lerp(from, to, p), 1f, 1f);
            yield return null;
        }
        rt.localScale = new Vector3(to, 1f, 1f);
    }

    IEnumerator FadeRootOut(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            _root.alpha = 1f - Mathf.Clamp01(t / dur);
            yield return null;
        }
        _root.alpha = 0f;
    }

    IEnumerator RevealSlot(Slot s)
    {
        if (s == null || s.holder == null) yield break;
        Vector2 basePos = s.basePos;
        float rise = _cardH * riseRatio;
        s.holder.localScale = Vector3.one;
        s.group.alpha = 0f;
        s.holder.anchoredPosition = basePos + new Vector2(0f, -rise);
        float t = 0f;
        while (t < appearTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / appearTime);
            float fade = 1f - Mathf.Pow(1f - p, 3f);   // 透明度先亮起来
            float riseP = Mathf.SmoothStep(0f, 1f, p); // 位移匀速滑升，避免「还没看清就到位」
            s.group.alpha = fade;
            s.holder.anchoredPosition = basePos + new Vector2(0f, -rise * (1f - riseP));
            yield return null;
        }
        s.group.alpha = 1f;
        s.holder.anchoredPosition = basePos;
    }

    // ══════════════════════════════════════════════════════════════════
    // 布局 / 构建卡
    // ══════════════════════════════════════════════════════════════════

    Slot CreateSlot()
    {
        var holder = NewRect("Slot" + _slots.Count, _row);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        holder.pivot = new Vector2(0.5f, 0.5f);
        holder.sizeDelta = new Vector2(_cardW, _cardH);
        var s = new Slot { holder = holder, group = holder.gameObject.AddComponent<CanvasGroup>() };
        _slots.Add(s);
        return s;
    }

    void LayoutSlots()
    {
        int n = _slots.Count;
        float spacing = _cardW * (1f + gapRatio);
        for (int i = 0; i < n; i++)
        {
            _slots[i].basePos = new Vector2((i - (n - 1) * 0.5f) * spacing, 0f);
            _slots[i].holder.anchoredPosition = _slots[i].basePos;
        }

        float top = _cardH * 0.5f;
        if (_title != null) _title.rectTransform.anchoredPosition = new Vector2(0f, top + 108f);
        if (_hint != null) _hint.rectTransform.anchoredPosition = new Vector2(0f, top + 52f);
    }

    /// <summary>造一张展示用 2D 卡。back=true 只出卡背（旁观者视角）。</summary>
    GameObject BuildCard(CardData td, bool back, Transform parent)
    {
        var player = Player.Instance;
        GameObject prefab = null;
        if (player != null)
        {
            if (td != null && td.cardType == CardType.Spell) prefab = player.spellCardPrefab2D;
            if (prefab == null) prefab = player.cardPrefab2D;
        }
        if (prefab == null)
        {
            Debug.LogWarning("[PickDraw] 没有 2D 卡预制体，跳过择牌展示");
            return null;
        }

        var go = Instantiate(prefab, parent);
        go.name = back ? "PickBack" : ("Pick_" + (td != null ? td.templateID : "?"));
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            var size = rt.sizeDelta;
            if (size.x > 1f && size.y > 1f)
            {
                _cardW = size.x * cardScale;
                _cardH = size.y * cardScale;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * cardScale;
        }

        // 展示卡不参与任何手牌 / 拖拽逻辑
        var cv = go.GetComponent<CardView>();
        if (cv != null) { cv.enabled = false; cv.handManager = null; }
        var drag = go.GetComponent<CardDrag>();
        if (drag != null) drag.enabled = false;

        var di = go.GetComponent<CardInstance>();
        if (di == null) di = go.AddComponent<CardInstance>();
        if (td != null) di.InitFromTemplate(td, 0, null);
        di.instanceID = "_pick_" + (++_seq);

        if (back)
        {
            ApplyBackFace(go, td);
            StartCoroutine(DelayedBackFace(go, td));   // CardDisplay2DSpell.Start() 会无条件切正面，下一帧补一次
        }
        else
        {
            go.GetComponent<CardDisplay2D>()?.RefreshWithInstance(di);
            ApplyFrontFace(go);
        }
        return go;
    }

    IEnumerator DelayedBackFace(GameObject go, CardData td)
    {
        yield return null;
        if (go != null) ApplyBackFace(go, td);
    }

    void ShowBadge(Slot s)
    {
        if (s == null) return;
        if (s.badge != null) { s.badge.SetActive(true); return; }

        var rt = NewRect("Badge", s.holder);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(_cardW * 0.92f, 66f);
        rt.anchoredPosition = new Vector2(0f, -_cardH * 0.20f);

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.62f, 0.16f, 0.15f, 0.94f);
        bg.raycastTarget = false;

        var label = NewText("Text", rt, 32f);
        label.text = discardBadge;
        label.color = new Color(1f, 0.96f, 0.92f, 1f);
        label.rectTransform.sizeDelta = rt.sizeDelta;

        s.badge = rt.gameObject;
    }

    // ══════════════════════════════════════════════════════════════════
    // 正反面切换（与 PlayRevealManager 同一套分流）
    // ══════════════════════════════════════════════════════════════════

    static void ApplyBackFace(GameObject go, CardData td)
    {
        if (go == null) return;
        var dNew = go.GetComponent<CardDisplay2DNew>();
        if (dNew != null) { dNew.ShowBack(); return; }
        var dSpell = go.GetComponent<CardDisplay2DSpell>();
        if (dSpell != null) { dSpell.ShowBack(td, discardBadge); return; }
        go.GetComponent<CardDisplay2D>()?.ShowBack(td, discardBadge);
    }

    static void ApplyFrontFace(GameObject go)
    {
        if (go == null) return;
        var dNew = go.GetComponent<CardDisplay2DNew>();
        if (dNew != null) { dNew.Refresh(); dNew.ShowFront(); return; }
        var dSpell = go.GetComponent<CardDisplay2DSpell>();
        if (dSpell != null) { dSpell.ShowFront(); dSpell.Refresh(); return; }
    }

    // ══════════════════════════════════════════════════════════════════
    // 小工具
    // ══════════════════════════════════════════════════════════════════

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI NewText(string name, Transform parent, float size)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1400f, 72f);

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = "";
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.984f, 0.965f, 0.925f, 1f);
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(t);
        return t;
    }

    /// <summary>取一个「认中文」的字体：优先顶栏数字（Serif Black + TMP_Orb_Black_Outline），
    /// 其次场景里任何带该字的字体，最后 TMP 默认。</summary>
    static void ApplyFont(TMP_Text t)
    {
        TMP_FontAsset font = null;
        Material mat = null;

        var p = Player.Instance;
        TMP_Text hud = p != null ? p.energyText : null;
        if (hud == null && p != null) hud = p.healthText;
        if (hud != null && hud.font != null && hud.font.HasCharacter('择'))
        {
            font = hud.font;
            mat = hud.fontSharedMaterial;
        }

        if (font == null)
        {
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < all.Length; i++)
            {
                var f = all[i];
                if (f != null && f.HasCharacter('择')) { font = f; break; }
            }
        }

        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font != null) t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
    }
}

/// <summary>择牌卡上的悬停探针：只把进出事件转发给 PickDrawUI，表现全在那边做。</summary>
public class PickHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public PickDrawUI ui;
    public int index;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ui != null) ui.OnSlotHover(index, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ui != null) ui.OnSlotHover(index, false);
    }
}
