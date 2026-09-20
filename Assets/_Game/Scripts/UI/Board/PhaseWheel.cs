using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

/// <summary>
/// 阶段轮换（横向）：5 个环在一条水平线上滚动，中间 3 个为可见区。
///
/// PhaseStart 与随后行动阶段（MyTurn/EnemyTurn）合二为一：PhaseStart 不触发滚动，
/// 初始直接显示第一行动阶段，每轮只滚动 3 次（首行动→次行动→Battle→下轮首行动）。
/// 内容模型（同一数据源 tm.currentPhase + tm.isMyTurnFirst）：
///   - Prev      ：刚过去的单元（L 位，左侧，缩小变淡）
///   - Cur       ：当前单元（C 位，正中，最大最亮）
///   - Next      ：下一单元（R 位，右侧，缩小变淡）
///   - NextNext  ：下下单元（H2 位预载，滚动后进 R 位显示"切换后的下一单元"）
///
/// 布局（画布局域单位，行中心 y=0）：
///   H1 = -2×slotStep（屏外，alpha 0）／L = -1×slotStep／C = 0／R = +1×slotStep／H2 = +2×slotStep（屏外）
///
/// 完整循环（严格"预载→滚动→清空→再预载"，滚动方向 = 从右向左）：
///   ① 预载：隐藏环(H2) = NextNext（滚动后进 R 位显示新 Next）
///   ② 瞬移：H1 位的环（原 Prev，此刻已在屏外且 alpha=0）直接挪到 H2 位 —— 两端都不可见，传送无痕
///   ③ 滚动：其余 4 个环一起左移一位（位置 / 大小 / 淡出按连续角色位同步插值）
///   ④ 结果：原 NextNext → R；原 Next → C；原 Cur → L；原 Prev → H1（转出显示区）
///   ⑤ 清空：H1（原 Prev）清空图案，为再下一轮准备（下次滚动前作为 H2 被预载）
///   显示环（L/C/R）图案永不更新——只靠物理环带内容移动。
///
/// 先后手交换（关键）：
///   先手每轮互换一次——TurnManager.EndCurrentTurn 在设 BattlePhase 前调 SwapFirstPlayer()
///   翻转 isMyTurnFirst。因此轮内各阶段生效期间 isMyTurnFirst 不变，且预载任何未来阶段
///   （Next/NextNext）时 tm.isMyTurnFirst 已反映该阶段所在轮的先后手，直接读取即可
///   （见 IsFirstMineForPhase）。
///
/// 滚动校正：滚动动画期间阶段又变化时（回合边界 Battle→PhaseStart→MyTurn/EnemyTurn
///   常在滚动窗口内连跳，但 PhaseStart 被合并不滚动），滚动结束后按最新阶段 + 最新先手
///   补滚一次，避免五环内容滞后。被 _rotating 挡下的请求由 UpdateWheelContents 兜底预载。
///
/// 收起 / 展开：点金色隐藏按钮 → 只把「阶段底衬」（承载圆环的一条）上移 hideContentShift 藏出屏幕，
///   木框与其它顶栏元素不动；吊在底衬下沿的按钮自己上移 hideButtonShift，收起后停在木框下沿，指标三角顺时针转 180°（向上 → 向下）；再点恢复。
///
/// 头像显示（行动者视角，AI 对战与联机一致）：
///   - MyTurn（己方行动）→ 己方头像；EnemyTurn（对方行动）→ 对方头像（AI 无头像 → 空白环）。
///   - PhaseStart（准备阶段）→ 本回合先手头像（按 isMyTurnFirst）。
///   头像不可用（AI 先手/AI 回合/未加载）→ 空白环（绝不 SetAvatar(null) 残留）。
/// </summary>
public class PhaseWheel : MonoBehaviour
{
    public static PhaseWheel Instance { get; private set; }

    [Header("引用")]
    public RectTransform wheelContainer;   // 环容器（MaskArea 下的窗框，仅作结构引用）
    public RingSlot[] slots;               // 5 个环，物理 index [0=Hidden1, 1=Left, 2=Center, 3=Right, 4=Hidden2]
    [Tooltip("阶段行根（本组件所在 RectTransform）；空 = 自身")]
    public RectTransform rowRoot;
    [Tooltip("点隐藏时上移的内容 = 阶段底衬（圆环挂在它下面）")]
    public RectTransform hideContent;
    [Tooltip("金色隐藏按钮")]
    public RectTransform hideButton;
    [Tooltip("按钮上的三角指标（绕中心旋转）")]
    public RectTransform hideArrow;

    [Header("配置")]
    [Tooltip("横向滚动动画时长（秒）")]
    public float rotateDuration = 0.4f;
    [Tooltip("攻击回合图标（两剑交叉）")]
    public Sprite battleIcon;

    [Header("横向布局（Canvas 局域单位）")]
    [Tooltip("相邻两环的中心间距")]
    public float slotStep = 210f;
    [Tooltip("中间环（当前阶段）倍率")]
    public float centerScale = 1f;
    [Tooltip("两侧环倍率（比中间小）")]
    public float sideScale = 0.7f;
    [Tooltip("两侧环不透明度（比中间淡）")]
    public float sideAlpha = 0.6f;
    [Tooltip("出屏环淡出到 0 的额外距离（角色位单位，1 = 一个 slotStep）")]
    public float fadeSpan = 0.7f;

    [Header("收起 / 展开")]
    [Tooltip("底衬（承载圆环的那条）上移距离，= 底衬下沿距屏顶的距离，恰好完全藏出屏幕")]
    public float hideContentShift = 57f;
    [Tooltip("隐藏按钮上移距离（收起后按钮升到木框下沿）")]
    public float hideButtonShift = 30f;
    [Tooltip("收起 / 展开动画时长（秒）")]
    public float hideDuration = 0.28f;
    [Tooltip("指标旋转时长（秒）")]
    public float arrowSpinDuration = 0.28f;

    static readonly TurnManager.TurnPhase[] ORDER = { TurnManager.TurnPhase.PhaseStart, TurnManager.TurnPhase.MyTurn, TurnManager.TurnPhase.EnemyTurn, TurnManager.TurnPhase.BattlePhase };

    /// <summary>角色 → 物理环 index。[H1, L, C, R, H2]。</summary>
    int[] _roleSlot = { 0, 1, 2, 3, 4 };
    bool _rotating;
    TurnManager.TurnPhase? _lastPhase;
    /// <summary>物理环 → 内容描述（跟随物理环，滚动时内容不变）。</summary>
    string[] _slotDesc = new string[5];
    /// <summary>物理环 → CanvasGroup（淡出用；Awake 自动补挂）。</summary>
    CanvasGroup[] _groups = new CanvasGroup[5];

    // 收起 / 展开状态
    bool _hidden;
    Coroutine _hideAnim, _buttonAnim, _arrowAnim;
    Vector2 _contentRestPos, _buttonRestPos;

    void LogWheel(string tag)
    {
        if (slots == null || slots.Length != 5) return;
        bool mf = TurnManager.Instance != null && TurnManager.Instance.isMyTurnFirst;
        int round = TurnManager.Instance != null ? TurnManager.Instance.phaseCount : 0;
        Debug.Log($"[PhaseWheel] {tag} (myFirst={mf}, round={round}) → H1={_slotDesc[_roleSlot[0]]}, " +
                  $"L={_slotDesc[_roleSlot[1]]}, C={_slotDesc[_roleSlot[2]]}, R={_slotDesc[_roleSlot[3]]}, H2={_slotDesc[_roleSlot[4]]}");
    }

    string AvatarDesc(bool firstMine, Texture2D avatar)
    {
        if (avatar == null) return SimpleAI.IsAIMatch ? "AI空白" : "空白";
        return firstMine ? "玩家1" : "玩家2";
    }

    void Awake()
    {
        Instance = this;
        if (slots == null || slots.Length != 5) { Debug.LogError("[PhaseWheel] 需要 5 个 RingSlot 引用"); return; }
        if (rowRoot == null) rowRoot = GetComponent<RectTransform>();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            CanvasGroup g = slots[i].GetComponent<CanvasGroup>();
            if (g == null) g = slots[i].gameObject.AddComponent<CanvasGroup>();
            g.interactable = false;
            g.blocksRaycasts = false;
            _groups[i] = g;
        }
        if (hideContent != null) _contentRestPos = hideContent.anchoredPosition;
        if (hideButton != null)
        {
            _buttonRestPos = hideButton.anchoredPosition;
            Button btn = hideButton.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(ToggleHidden);
            else Debug.LogWarning("[PhaseWheel] 隐藏按钮上没有 Button 组件，点击无效");
        }
    }

    void Start()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;
        bool myFirst = tm.isMyTurnFirst;
        // PhaseStart 与随后行动阶段合二为一：初始直接显示第一行动阶段（MyTurn/EnemyTurn），
        // 不把 PhaseStart 当独立节点。_lastPhase 记为 initial，使随后 PhaseStart→首行动 不触发滚动。
        TurnManager.TurnPhase initial = tm.currentPhase;
        if (initial == TurnManager.TurnPhase.PhaseStart)
            initial = myFirst ? TurnManager.TurnPhase.MyTurn : TurnManager.TurnPhase.EnemyTurn;
        _lastPhase = initial;
        // 游戏开始第一回合：L（上一阶段）= 空白（尚无上一阶段）；C=第一行动阶段；R=下一单元；隐藏位留空。
        ApplyContent(_roleSlot[1], null, false); // L = 空白
        ApplyContent(_roleSlot[2], initial, false);                       // C = Cur
        ApplyContent(_roleSlot[3], NextOfPhase(initial, myFirst), false); // R = Next
        slots[_roleSlot[0]].SetEmpty();
        slots[_roleSlot[4]].SetEmpty();
        ApplyAllVisuals();
        SetHiddenImmediate(false);
        LogWheel("[Start] 初始五环");
    }

    void Update()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;
        TurnManager.TurnPhase cur = tm.currentPhase;
        if (_lastPhase == null) { _lastPhase = cur; return; }
        if (_lastPhase.Value == cur) return;

        // PhaseStart 与随后行动阶段（MyTurn/EnemyTurn）合二为一：PhaseStart 不触发旋转，
        // 也不推进 _lastPhase——Battle→PhaseStart 直接过渡到下一轮首行动阶段时只旋转一次
        // （该旋转在首行动阶段（PhaseStart→MyTurn/EnemyTurn）那一刻触发，_lastPhase 仍是 Battle）。
        if (cur == TurnManager.TurnPhase.PhaseStart) return;

        LogWheel($"[Update] 阶段变化 {_lastPhase.Value} → {cur}，切换前五环");
        // 上一旋转单元（PhaseStart 已被跳过，故 _lastPhase 必为真实单元，可作旋转的 prev）。
        TurnManager.TurnPhase prev = _lastPhase.Value;
        // 立即记录最新阶段（无论是否旋转中）——防止旋转结束误判阶段又变，触发第二次旋转（连转两次）。
        _lastPhase = cur;
        if (_rotating) return; // 旋转动画播放期间忽略新的旋转请求
        bool myFirst = tm.isMyTurnFirst;
        RotateToPhase(prev, cur, NextOfPhase(cur, myFirst));
    }

    /// <summary>下一单元（考虑先手方：MyTurn/EnemyTurn 顺序因先手而异）。
    /// 旋转节点：MyTurn → EnemyTurn → Battle → 下一轮首行动（MyTurn/EnemyTurn），PhaseStart 已合并。
    /// 玩家先手轮：MyTurn→EnemyTurn→Battle→MyTurn(下轮)；AI 先手轮：EnemyTurn→MyTurn→Battle→EnemyTurn(下轮)。</summary>
    static TurnManager.TurnPhase NextOfPhase(TurnManager.TurnPhase p, bool myFirst)
    {
        switch (p)
        {
            case TurnManager.TurnPhase.PhaseStart: return myFirst ? TurnManager.TurnPhase.MyTurn : TurnManager.TurnPhase.EnemyTurn;
            case TurnManager.TurnPhase.MyTurn:     return myFirst ? TurnManager.TurnPhase.EnemyTurn : TurnManager.TurnPhase.BattlePhase;
            case TurnManager.TurnPhase.EnemyTurn:  return myFirst ? TurnManager.TurnPhase.BattlePhase : TurnManager.TurnPhase.MyTurn;
            // Battle 的下一单元 = 下一轮首行动阶段（PhaseStart 已合并），按翻转后的先手取 MyTurn/EnemyTurn
            case TurnManager.TurnPhase.BattlePhase: return myFirst ? TurnManager.TurnPhase.MyTurn : TurnManager.TurnPhase.EnemyTurn;
        }
        return TurnManager.TurnPhase.PhaseStart;
    }

    /// <summary>滚动一个环位：预载 NextNext → 横向左移动画 → 角色轮转 → 清空刚转出的隐藏环。
    /// L 位是显示环（上一阶段），永不清空——只在 Start 初始态为空白。</summary>
    public void RotateToPhase(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (_rotating || slots == null || slots.Length != 5) { UpdateWheelContents(previous, current, next); return; }
        StartCoroutine(RotateRoutine(previous, current, next));
    }

    // ══════════════════════════════════════════════════════════════════
    // 横向摆位：把"角色位"（连续值，0=H1 1=L 2=C 3=R 4=H2）映射成
    // 位置 / 大小 / 淡出。滚动动画就是对角色位做 1→0 的连续插值，
    // 于是位移、缩放、透明度由同一根曲线带出来，不会各自为政。
    // ══════════════════════════════════════════════════════════════════

    /// <summary>按角色位摆一个环（位置 + 大小 + 淡出）。rolePos 允许小数（动画中间态）。</summary>
    void ApplyRoleVisual(int physIndex, float rolePos)
    {
        if (physIndex < 0 || physIndex >= slots.Length || slots[physIndex] == null) return;
        RectTransform rt = slots[physIndex].GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2((rolePos - 2f) * slotStep, 0f);
            float sc = Mathf.Lerp(centerScale, sideScale, Mathf.Clamp01(Mathf.Abs(rolePos - 2f)));
            rt.localScale = new Vector3(sc, sc, 1f);
        }
        CanvasGroup g = physIndex < _groups.Length ? _groups[physIndex] : null;
        if (g != null)
        {
            float d = Mathf.Abs(rolePos - 2f);          // 离正中的距离（角色位单位）
            g.alpha = d <= 1f
                ? Mathf.Lerp(1f, sideAlpha, d)                                            // 正中 → 两侧：变淡
                : Mathf.Lerp(sideAlpha, 0f, Mathf.Clamp01((d - 1f) / Mathf.Max(0.01f, fadeSpan))); // 两侧 → 出屏：淡没
        }
    }

    /// <summary>5 个环全部按各自角色位重摆一遍（初始 / 兜底）。</summary>
    void ApplyAllVisuals()
    {
        if (slots == null || slots.Length != 5) return;
        for (int role = 0; role < 5; role++) ApplyRoleVisual(_roleSlot[role], role);
    }

    IEnumerator RotateRoutine(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        _rotating = true;
        LogWheel($"[滚动] {DescribePhase(previous)} → {DescribePhase(current)} → {DescribePhase(next)}");

        // ① 预载：隐藏环(H2) = next（滚动后的"下一阶段" = 滚动前的 NextNext）。
        ApplyContent(_roleSlot[4], next, true);
        LogWheel($"[预载] 预载 H2={DescribePhase(next)}，五环");

        // ② 瞬移：H1 位的环（原 Prev）直接挪到 H2 位。此刻 H1 与 H2 都在屏幕外且 alpha=0，
        //    传送没有任何可见痕迹；不这么做它会横穿整条可见带滑到右边。
        ApplyRoleVisual(_roleSlot[0], 4f);

        // ③ 滚动动画：其余 4 个环一起左移一位（role → role-1），位置/大小/淡出同步插值。
        float t = 0f;
        while (t < rotateDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / rotateDuration);
            float e = 1f - (1f - p) * (1f - p); // ease-out
            for (int role = 1; role < 5; role++)
                ApplyRoleVisual(_roleSlot[role], Mathf.Lerp(role, role - 1f, e));
            yield return null;
        }
        for (int role = 1; role < 5; role++) ApplyRoleVisual(_roleSlot[role], role - 1f);

        // ④ 角色轮转：H1←L, L←C, C←R, R←H2, H2←H1
        RotateRoles();

        // ⑤ 清空"刚滚出显示区"的隐藏环（当前 H1 位 = 原 Prev 环）。
        slots[_roleSlot[0]].SetEmpty();
        _slotDesc[_roleSlot[0]] = "空白";
        ApplyRoleVisual(_roleSlot[0], 0f);

        LogWheel("[滚动后] 五环");
        _rotating = false;

        // ⑤ 旋转校正：旋转动画期间阶段又变化（回合边界 Battle→PhaseStart→MyTurn/EnemyTurn
        //    常在 0.4s 旋转窗口内连跳，PhaseStart 被合并跳过），本次旋转是按旧阶段预载的，
        //    补转一次落到最新行动阶段。补转时用最新 isMyTurnFirst 计算 prev/next，
        //    确保跨轮预载拿到翻转后的先后手。
        var tm = TurnManager.Instance;
        if (tm != null && tm.currentPhase != current && tm.currentPhase != TurnManager.TurnPhase.PhaseStart)
        {
            LogWheel($"[旋转校正] 旋转期间阶段 {current} → {tm.currentPhase}，补转校正");
            bool myFirst = tm.isMyTurnFirst;
            // prev = 本次旋转的 current（刚显示的真实单元）
            RotateToPhase(current, tm.currentPhase, NextOfPhase(tm.currentPhase, myFirst));
        }
    }

    static string DescribePhase(TurnManager.TurnPhase? phase)
    {
        if (phase == null) return "空白";
        switch (phase.Value)
        {
            case TurnManager.TurnPhase.BattlePhase: return "攻击图标";
            case TurnManager.TurnPhase.PhaseStart: return "准备阶段";
            case TurnManager.TurnPhase.MyTurn: return "己方回合";
            case TurnManager.TurnPhase.EnemyTurn: return "对方回合";
            default: return "未知";
        }
    }

    /// <summary>角色顺时针移动一位：H1←L, L←C, C←R, R←H2, H2←H1。</summary>
    void RotateRoles()
    {
        int h1 = _roleSlot[0], l = _roleSlot[1], c = _roleSlot[2], r = _roleSlot[3], h2 = _roleSlot[4];
        _roleSlot[0] = l; _roleSlot[1] = c; _roleSlot[2] = r; _roleSlot[3] = h2; _roleSlot[4] = h1;
    }

    /// <summary>兜底预载：H2 = next（isNext=true）。显示环永不在此更新。</summary>
    public void UpdateWheelContents(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (slots == null || slots.Length != 5) return;
        ApplyContent(_roleSlot[4], next, true);
    }

    // ══════════════════════════════════════════════════════════════════
    // 收起 / 展开：整条顶栏上移藏出屏幕，按钮自己上移贴到屏幕顶端，
    // 指标三角顺时针旋转 180°（向上 → 向下）。再点一次原路回来。
    // ══════════════════════════════════════════════════════════════════

    /// <summary>切换收起 / 展开（隐藏按钮的点击回调）。</summary>
    public void ToggleHidden() { SetHidden(!_hidden); }

    /// <summary>是否已收起（只露隐藏按钮）。</summary>
    public bool IsHidden => _hidden;

    /// <summary>收起 / 展开。</summary>
    public void SetHidden(bool hidden)
    {
        _hidden = hidden;
        if (hideContent != null)
        {
            if (_hideAnim != null) StopCoroutine(_hideAnim);
            _hideAnim = StartCoroutine(MoveAnchoredY(hideContent,
                _contentRestPos.y + (hidden ? hideContentShift : 0f), hideDuration));
        }
        if (hideButton != null)
        {
            if (_buttonAnim != null) StopCoroutine(_buttonAnim);
            _buttonAnim = StartCoroutine(MoveAnchoredY(hideButton,
                _buttonRestPos.y + (hidden ? hideButtonShift : 0f), hideDuration));
        }
        if (hideArrow != null)
        {
            if (_arrowAnim != null) StopCoroutine(_arrowAnim);
            _arrowAnim = StartCoroutine(SpinArrowRoutine(hidden));
        }
    }

    /// <summary>不做动画直接落到某一态（初始用）。</summary>
    void SetHiddenImmediate(bool hidden)
    {
        _hidden = hidden;
        if (hideContent != null)
            hideContent.anchoredPosition = new Vector2(hideContent.anchoredPosition.x,
                _contentRestPos.y + (hidden ? hideContentShift : 0f));
        if (hideButton != null)
            hideButton.anchoredPosition = new Vector2(hideButton.anchoredPosition.x,
                _buttonRestPos.y + (hidden ? hideButtonShift : 0f));
        if (hideArrow != null) hideArrow.localRotation = Quaternion.Euler(0f, 0f, hidden ? 180f : 0f);
    }

    IEnumerator MoveAnchoredY(RectTransform rt, float toY, float dur)
    {
        float fromY = rt.anchoredPosition.y;
        if (dur <= 0f) { rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, toY); yield break; }
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float e = p * p * (3f - 2f * p); // smoothstep
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(fromY, toY, e));
            yield return null;
        }
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, toY);
    }

    /// <summary>指标旋转：始终顺时针 —— 收起 0→180，展开 180→360（视觉上回到正位）。</summary>
    IEnumerator SpinArrowRoutine(bool down)
    {
        float from = hideArrow.localEulerAngles.z;
        if (!down && from < 1f) { hideArrow.localRotation = Quaternion.identity; yield break; }
        float to = down ? 180f : 360f;
        float dur = Mathf.Max(0.01f, arrowSpinDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float e = p * p * (3f - 2f * p);
            hideArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, to, e));
            yield return null;
        }
        hideArrow.localRotation = Quaternion.Euler(0f, 0f, down ? 180f : 0f);
    }

    /// <summary>填充环内容。isNext=true 表示"未来阶段"（Next/NextNext 预载），需跨轮翻转先手。</summary>
    void ApplyContent(int physIndex, TurnManager.TurnPhase? phase, bool isNext)
    {
        if (physIndex < 0 || physIndex >= slots.Length || slots[physIndex] == null) return;
        // AI 对战：AI 回合（EnemyTurn）永远空白——入口统一判断，任何路径不得绕过
        if (SimpleAI.IsAIMatch && phase == TurnManager.TurnPhase.EnemyTurn)
        {
            slots[physIndex].SetEmpty();
            _slotDesc[physIndex] = "AI空白";
            return;
        }
        if (phase == null)
        {
            slots[physIndex].SetEmpty();
            _slotDesc[physIndex] = "空白";
            return;
        }
        switch (phase.Value)
        {
            case TurnManager.TurnPhase.BattlePhase:
                slots[physIndex].SetBattle(battleIcon); // 整个环替换为攻击图片，不显示头像
                _slotDesc[physIndex] = "攻击图标";
                break;
            case TurnManager.TurnPhase.PhaseStart:
                // 准备阶段：显示先手（按 isMyTurnFirst）
                {
                    bool fMine = IsFirstMineForPhase(phase.Value, isNext);
                    Texture2D fAvatar = fMine ? MyAvatar() : OppAvatar();
                    if (fAvatar != null) slots[physIndex].SetAvatar(fAvatar);
                    else slots[physIndex].SetEmpty();
                    _slotDesc[physIndex] = AvatarDesc(fMine, fAvatar);
                }
                break;
            case TurnManager.TurnPhase.MyTurn:
                // 己方行动回合：显示己方头像（行动者视角，AI 对战与联机一致）
                {
                    Texture2D my = MyAvatar();
                    if (my != null) slots[physIndex].SetAvatar(my);
                    else slots[physIndex].SetEmpty();
                    _slotDesc[physIndex] = AvatarDesc(true, my);
                }
                break;
            case TurnManager.TurnPhase.EnemyTurn:
                // 对方行动回合：显示对方头像（AI 对战 OppAvatar=null → 空白；联机显示对手头像）
                {
                    Texture2D opp = OppAvatar();
                    if (opp != null) slots[physIndex].SetAvatar(opp);
                    else slots[physIndex].SetEmpty();
                    _slotDesc[physIndex] = AvatarDesc(false, opp);
                }
                break;
        }
    }

    // ============ 先手判断（同一数据源 tm.isMyTurnFirst）============

    /// <summary>该阶段生效时先手是否己方。
    /// 已验证翻转时机：TurnManager.EndCurrentTurn 在设 BattlePhase 前调 SwapFirstPlayer()
    /// 翻转 isMyTurnFirst（每轮一次）。因此轮内各阶段生效期间 isMyTurnFirst 不变，且轮末 Battle
    /// 前已翻转为下一轮的值——预载任何未来阶段（Next/NextNext，最多跨一轮边界）时，
    /// tm.isMyTurnFirst 已反映该阶段所在轮的先后手，直接返回即可，无需在此翻转。
    /// 若未来翻转时机变化，可改为按 phase 跨过的轮边界数翻转。</summary>
    bool IsFirstMineForPhase(TurnManager.TurnPhase phase, bool isNext)
    {
        var tm = TurnManager.Instance;
        if (tm == null) return true;
        return tm.isMyTurnFirst;
    }

    Texture2D MyAvatar()
    {
        // 己方头像 = LocalSteamID（统一管理器缓存；兜底 SteamDataManager.localAvatar）
        Texture2D tex = SteamAvatarManager.GetAvatarTexture(LobbyConfig.LocalSteamID);
        return tex != null ? tex : (SteamDataManager.Instance != null ? SteamDataManager.Instance.localAvatar : null);
    }

    static ulong _lastLoggedOppSteamID = ulong.MaxValue; // 诊断：记录上次日志的 SteamID

    Texture2D OppAvatar()
    {
        // AI 对战：AI(Remote, server-only) 无 SteamID，AI 头像为空白
        if (SimpleAI.IsAIMatch) return null;
        // 对方头像 = RemoteSteamID（大厅捕获 + 网络 SyncVar 双路填充，统一管理器缓存）
        ulong sid = LobbyConfig.RemoteSteamID;
        Texture2D tex = SteamAvatarManager.GetAvatarTexture(sid);
        // 诊断：SteamID 变化或结果为 null 时都打印——抓 RemoteSteamID 被改成 0 的时刻
        if (sid != _lastLoggedOppSteamID || tex == null)
        {
            _lastLoggedOppSteamID = sid;
            Debug.Log($"[PhaseWheel] OppAvatar: RemoteSteamID={sid}, 返回={(tex != null ? $"有纹理({tex.width}x{tex.height})" : "null")}");
        }
        return tex;
    }
}
