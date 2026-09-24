using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 攻击回合伤害面板 —— 屏幕左侧上下两块「圆形头像 + 跑道型底 + 实时累计伤害数」。
///
/// 位置：上=对方，下=己方。数字 = **本次攻击回合该玩家将要受到的伤害累计**
/// （召唤物打空位的 tier、亡命之徒的加伤、存活差），不含 TakeDamage 直接扣的那部分。
///
/// 一整轮的演出：
///   ① 每个「打英雄」的伤害数字从攻击者头顶跳出 → 到最高点停住 → 加速飞进对应面板，
///      落地时该面板的数字跳一下并加上这个数（ShowIncoming）。
///   ② 结算时两个数字相减（受击方从大数递减到净伤害，另一方递减到 0）→ 另一方整块淡出
///      （Settle）。
///   ③ 从受击方的面板飞出一颗粒子，往上飞向顶栏（对方）/ 往下飞向左下（己方）的生命值，
///      到达的那一刻才真正扣血并弹出伤害数字。
///
/// 数据来源全是 BattleManager 的 pendingDamageToMe/Enemy（仅服务端有），所以：
///   · 服务端（含离线 AI）由 BattleManager 直接驱动；
///   · 客户端靠 NetworkPlayer.TargetAttackTurnSettle 把「结算 + 两个累计值」推过来对齐，
///     飞行数字则由 PlayAttackLocally 用同一套 ShowIncoming 播（槽位按客户端视角翻转）。
///   面板的显隐由本组件自己按当前阶段（BattlePhase）开关，两端一致。
///
/// 纯运行时构建，不依赖场景与预制体；挂在与手牌同一块主 Canvas 下（PlayRevealManager.FindMainCanvas）。
/// </summary>
public class AttackTurnDamagePanel : MonoBehaviour
{
    public static AttackTurnDamagePanel Instance { get; private set; }

    public const int SideOpponent = 0;   // 上：对方
    public const int SideSelf = 1;       // 下：己方

    // Resources 路径（相对任一 Resources 文件夹）
    const string PlatePath = "UI/AttackTurn/TurnDmg_Plate";
    const string DiscPath  = "UI/AttackTurn/TurnDmg_AvatarDisc";
    const string RingPath  = "UI/AttackTurn/TurnDmg_AvatarRing";
    const string OrbPath   = "UI/AttackTurn/TurnDmg_Orb";

    [Header("布局（1920×1080 参考分辨率下的像素）")]
    [Tooltip("面板宽（跑道型底的长度）")]
    public float panelWidth = 292f;
    [Tooltip("面板高（= 左端头像圆的直径）")]
    public float panelHeight = 108f;
    [Tooltip("上方面板（对方）：距屏幕左边缘 / 相对屏幕竖直中线")]
    public Vector2 opponentPos = new Vector2(168f, 260f);
    [Tooltip("下方面板（己方）：距屏幕左边缘 / 相对屏幕竖直中线")]
    public Vector2 selfPos = new Vector2(168f, 140f);

    [Header("节奏（秒）")]
    [Tooltip("伤害数字弹出时的过冲时长")]
    public float popTime = 0.16f;
    [Tooltip("弹出重力（像素/秒²），配合 apexHeight 决定初速")]
    public float apexGravity = 2600f;
    [Tooltip("弹出最高点相对出点的高度（像素）")]
    public float apexHeight = 140f;
    [Tooltip("最高点停住多久才开始飞向面板")]
    public float apexHold = 0.16f;
    [Tooltip("飞进面板的时长（加速吸入）")]
    public float flyInTime = 0.40f;
    [Tooltip("面板数字落地的跳动时长")]
    public float punchTime = 0.20f;
    [Tooltip("结算相减的时长")]
    public float subTime = 0.55f;
    [Tooltip("单块面板淡出时长")]
    public float fadeTime = 0.32f;
    [Tooltip("粒子从面板飞向生命值的时长")]
    public float orbTime = 0.55f;
    [Tooltip("扣血弹字之后停留多久收面板")]
    public float impactHold = 0.45f;
    [Tooltip("阶段结束收面板的淡出时长")]
    public float phaseFade = 0.22f;

    [Header("字号")]
    public float flyFontSize = 54f;
    public float valueFontSize = 56f;
    public float popFontSize = 62f;

    // ── 运行时构建 ──
    Canvas _canvas;
    RectTransform _canvasRect;
    Camera _cam;                 // 仅 ScreenSpaceCamera / World 画布需要
    RectTransform _root;
    RectTransform _fxLayer;      // 飞行中的伤害数字（跟着面板一起淡出）
    RectTransform _popLayer;     // 血条上的伤害弹字（不跟面板淡出）
    CanvasGroup _cg;

    readonly Side[] _sides = new Side[2];
    readonly List<FlyNum> _fly = new List<FlyNum>();
    readonly List<Image> _orbs = new List<Image>();

    bool _built;
    bool _visible;
    bool _settling;
    bool _consumed;          // 本轮已结算过：阶段还没翻走之前别再亮回来
    int _pendingFlights;     // 还在飞的伤害数字（结算要等它们落完再相减）
    bool _avatarTick;

    class Side
    {
        public RectTransform root;
        public Image plate;
        public Image avatar;
        public TextMeshProUGUI value;
        public CanvasGroup cg;
        public int shown;            // 当前显示的数字
        public float punchT = 1f;    // 0..1，1 = 不在跳
    }

    class FlyNum
    {
        public GameObject go;
        public RectTransform rt;
        public CanvasGroup cg;
        public TextMeshProUGUI tmp;
        public bool busy;
    }

    // ══════════════════════════════════════════════════════════════════
    // 对外接口
    // ══════════════════════════════════════════════════════════════════

    /// <summary>创建面板（幂等）。找不到主 Canvas 就什么都不做。</summary>
    public static AttackTurnDamagePanel Ensure()
    {
        if (Instance != null) return Instance;
        var canvasGo = PlayRevealManager.FindMainCanvas();
        if (canvasGo == null) return null;

        var go = new GameObject("AttackTurnDamagePanel", typeof(RectTransform), typeof(AttackTurnDamagePanel));
        go.transform.SetParent(canvasGo.transform, false);
        go.transform.SetAsLastSibling();

        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGo.GetComponentInParent<Canvas>();
        Instance.Attach(canvas);
        return Instance;
    }

    /// <summary>面板此刻是否亮着（攻击回合内为 true）。BattleManager 用它判断
    /// 该走「面板演出 + 到达才扣血」还是直接结算。</summary>
    public bool IsShowing => _built && _visible;

    /// <summary>攻击回合开场：两块面板亮出来，数字归零。</summary>
    public static void BeginPhase()
    {
        var p = Ensure();
        if (p != null) p.Begin();
    }

    /// <summary>一个伤害数字：从世界坐标（攻击者头顶）跳出 → 顶点停顿 → 飞进对应面板并加上去。
    /// 面板不可用时退回原来的场上飘字，绝不吞掉数字。</summary>
    public static void ShowIncoming(Vector3 worldPos, int value, int side)
    {
        if (value <= 0) return;
        var p = Instance;
        if (p == null || !p._visible)
        {
            DamageFloater.Show(worldPos, value, FloaterType.Damage);
            return;
        }
        p.StartCoroutine(p.FlyRoutine(worldPos, value, side));
    }

    /// <summary>客户端镜像：服务端把「结算 + 两个累计值」推过来，这边播同一段演出。
    /// 面板侧别已按接收方视角翻好（0=上/对方，1=下/己方）。</summary>
    public static void PlaySettledForClient(int finalDamage, int loserSide, int topValue, int bottomValue)
    {
        var p = Ensure();
        if (p == null) return;
        if (!p._built) return;
        if (!p._visible) p.Begin();
        p.StartCoroutine(p.SettleRoutine(finalDamage, loserSide, topValue, bottomValue, null));
    }

    /// <summary>结算：两数相减 → 只留受击方 → 粒子飞向该玩家的生命值 → 到达时调 onImpact（真正扣血）。
    /// onImpact 保证恰好被调用一次；面板不可用由调用方兜底。</summary>
    public IEnumerator Settle(int finalDamage, int loserSide, Action onImpact)
        => SettleRoutine(finalDamage, loserSide, -1, -1, onImpact);

    // ══════════════════════════════════════════════════════════════════
    // 生命周期
    // ══════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Attach(Canvas canvas)
    {
        _canvas = canvas;
        _canvasRect = canvas != null ? (RectTransform)canvas.transform : (RectTransform)transform.parent;
        _cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
             ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
             : null;
        Build();
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        _root = (RectTransform)transform;
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;
        _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = Vector2.zero;

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        _sides[SideOpponent] = BuildSide("Opponent", SideOpponent);
        _sides[SideSelf] = BuildSide("Self", SideSelf);

        _fxLayer = NewRect("FlyLayer", _root);
        Stretch(_fxLayer);

        // 血条弹字单独一层（挂在画布下、面板之上，不随面板淡出）
        var popParent = _root.parent != null ? _root.parent : _root;
        _popLayer = NewRect("AttackTurnPopLayer", popParent);
        Stretch(_popLayer);
        _popLayer.SetAsLastSibling();

        ResolveAvatarsOnce();
        RefreshAvatars();
    }

    void Update()
    {
        if (!_built) return;

        // 头像 1 秒一刷（Steam 头像是异步到的，进来时可能还没有）
        if (!_avatarTick)
        {
            _avatarTick = true;
            RefreshAvatars();
        }
        else if (Time.frameCount % 60 == 0)
        {
            RefreshAvatars();
        }

        bool inBattle = InBattlePhase();
        if (!inBattle) _consumed = false;      // 离开攻击回合 → 解除「本轮已结算」
        if (!_settling)
        {
            bool want = inBattle && !_consumed;
            if (want && !_visible) Begin();
            else if (!want && _visible) EndPhase();
        }

        // 数字落地的跳动
        for (int i = 0; i < 2; i++)
        {
            var s = _sides[i];
            if (s == null || s.value == null || s.punchT >= 1f) continue;
            s.punchT = Mathf.Clamp01(s.punchT + Time.deltaTime / Mathf.Max(0.01f, punchTime));
            float k = Mathf.Sin(Mathf.PI * s.punchT);
            s.value.rectTransform.localScale = Vector3.one * (1f + 0.22f * k);
            if (s.plate != null) s.plate.rectTransform.localScale = Vector3.one * (1f + 0.030f * k);
            if (s.punchT >= 1f)
            {
                s.value.rectTransform.localScale = Vector3.one;
                if (s.plate != null) s.plate.rectTransform.localScale = Vector3.one;
            }
        }
    }

    static bool InBattlePhase()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return false;
        if (tm.currentPhase != TurnManager.TurnPhase.BattlePhase) return false;
        var bm = BattleManager.Instance;
        if (bm != null && bm.skipBattle) return false;
        return true;
    }

    void Begin()
    {
        if (!_built) return;
        _settling = false;
        _consumed = false;
        _visible = true;
        _cg.alpha = 1f;
        for (int i = 0; i < 2; i++)
        {
            var s = _sides[i];
            if (s == null) continue;
            s.shown = 0;
            s.punchT = 1f;
            s.cg.alpha = 1f;
            s.root.localScale = Vector3.one;
            if (s.value != null) { s.value.text = "0"; s.value.rectTransform.localScale = Vector3.one; }
            if (s.plate != null) s.plate.rectTransform.localScale = Vector3.one;
        }
        RefreshAvatars();
    }

    void EndPhase()
    {
        if (!_visible) return;
        _visible = false;
        StartCoroutine(FadeRoot(phaseFade, false));
    }

    IEnumerator FadeRoot(float dur, bool toVisible)
    {
        float a0 = _cg.alpha, a1 = toVisible ? 1f : 0f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            _cg.alpha = Mathf.Lerp(a0, a1, Mathf.Clamp01(t / Mathf.Max(0.01f, dur)));
            yield return null;
        }
        _cg.alpha = a1;
    }

    // ══════════════════════════════════════════════════════════════════
    // ① 飞行数字
    // ══════════════════════════════════════════════════════════════════

    IEnumerator FlyRoutine(Vector3 worldPos, int value, int side)
    {
        var f = GetFly(_fxLayer);
        var rt = f.rt;
        _pendingFlights++;
        Vector2 start = WorldToCanvas(worldPos);
        rt.anchoredPosition = start;
        rt.localScale = Vector3.one * 0.5f;
        f.tmp.text = "-" + value;
        f.tmp.fontSize = flyFontSize;
        DamageFloater.ApplyStyle(f.tmp, FloaterType.Damage);
        f.cg.alpha = 1f;

        // ── 跳出：抛出后到最高点 ──
        float h = apexHeight * UnityEngine.Random.Range(0.85f, 1.15f);
        float v0 = Mathf.Sqrt(2f * apexGravity * h);
        Vector2 v = new Vector2(UnityEngine.Random.Range(-0.45f, 0.45f) * v0, v0);
        Vector2 p = start;
        float t = 0f;
        while (v.y > 0f && t < 1.5f)
        {
            float dt = Time.deltaTime;
            t += dt;
            v.y -= apexGravity * dt;
            p += v * dt;
            rt.anchoredPosition = p;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, popTime));
            rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, EaseOutBack(k));
            yield return null;
        }
        rt.localScale = Vector3.one;

        // ── 最高点停住 ──
        yield return new WaitForSeconds(apexHold);

        // ── 加速飞进面板 ──
        Vector2 to = FlyTarget(side);
        Vector2 from = rt.anchoredPosition;
        for (float d = 0f; d < flyInTime; d += Time.deltaTime)
        {
            float k = Mathf.Clamp01(d / Mathf.Max(0.01f, flyInTime));
            float e = k * k;
            rt.anchoredPosition = Vector2.Lerp(from, to, e);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, k);
            f.cg.alpha = 1f - 0.4f * k;
            yield return null;
        }
        Release(f);
        AddValue(side, value);
        _pendingFlights--;
    }

    void AddValue(int side, int delta)
    {
        var s = _sides[side];
        if (s == null) return;
        s.shown += delta;
        if (s.shown < 0) s.shown = 0;
        if (s.value != null) s.value.text = s.shown.ToString();
        s.punchT = 0f;
    }

    // ══════════════════════════════════════════════════════════════════
    // ② 结算
    // ══════════════════════════════════════════════════════════════════

    IEnumerator SettleRoutine(int finalDamage, int loserSide, int topValue, int bottomValue, Action onImpact)
    {
        if (!_built) { onImpact?.Invoke(); yield break; }
        _settling = true;
        _visible = true;
        _cg.alpha = 1f;

        // 等还在飞的数字落完再相减（存活差那笔就在结算前一瞬起飞，落点必须算进这一轮）
        for (float w = 0f; _pendingFlights > 0 && w < 2f; w += Time.deltaTime)
            yield return null;

        // 权威值对齐（客户端靠服务端推来的两个数；-1 = 用自己现在的显示值）
        if (topValue >= 0) _sides[SideOpponent].shown = topValue;
        if (bottomValue >= 0) _sides[SideSelf].shown = bottomValue;
        for (int i = 0; i < 2; i++)
            if (_sides[i].value != null) _sides[i].value.text = _sides[i].shown.ToString();

        int keep = Mathf.Clamp(loserSide, 0, 1);
        int drop = 1 - keep;

        if (finalDamage <= 0)
        {
            // 没有任何一方掉血：两个数一起归零，然后收场
            yield return CountDown(keep, 0, drop, 0, subTime);
            yield return FadeSide(drop, fadeTime);
            yield return FadeSide(keep, fadeTime);
            onImpact?.Invoke();
            _consumed = true;
            _settling = false;
            HideRoot();
            yield break;
        }

        // ── 相减：受击方从大数递减到净伤害，另一方递减到 0 ──
        int keepTo = finalDamage;                          // 以服务端净伤害为准
        yield return CountDown(keep, keepTo, drop, 0, subTime);

        // ── 只保留受击方，另一方淡出 ──
        yield return FadeSide(drop, fadeTime);

        // ── 粒子飞向该玩家的生命值 ──
        Vector2 from = FlyTarget(keep);
        var hp = HpRect(keep);
        Vector2 to = hp != null ? CanvasLocal(hp) : DefaultHpAnchor(keep);
        yield return OrbFly(from, to, orbTime);

        onImpact?.Invoke();
        PopNumber(to, finalDamage);

        yield return new WaitForSeconds(impactHold);
        _consumed = true;
        _settling = false;
        HideRoot();
    }

    void HideRoot()
    {
        _visible = false;
        StartCoroutine(FadeRoot(phaseFade, false));
    }

    IEnumerator CountDown(int sideA, int toA, int sideB, int toB, float dur)
    {
        int fromA = _sides[sideA].shown, fromB = _sides[sideB].shown;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, dur));
            float e = Mathf.SmoothStep(0f, 1f, k);
            _sides[sideA].shown = Mathf.RoundToInt(Mathf.Lerp(fromA, toA, e));
            _sides[sideB].shown = Mathf.RoundToInt(Mathf.Lerp(fromB, toB, e));
            WriteValue(sideA);
            WriteValue(sideB);
            yield return null;
        }
        _sides[sideA].shown = toA;
        _sides[sideB].shown = toB;
        WriteValue(sideA);
        WriteValue(sideB);
        _sides[sideA].punchT = 0f;
    }

    void WriteValue(int side)
    {
        var s = _sides[side];
        if (s != null && s.value != null) s.value.text = s.shown.ToString();
    }

    IEnumerator FadeSide(int side, float dur)
    {
        var s = _sides[side];
        if (s == null || s.cg == null) yield break;
        float a0 = s.cg.alpha;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            s.cg.alpha = Mathf.Lerp(a0, 0f, Mathf.Clamp01(t / Mathf.Max(0.01f, dur)));
            yield return null;
        }
        s.cg.alpha = 0f;
    }

    // ══════════════════════════════════════════════════════════════════
    // ③ 粒子 / 弹字
    // ══════════════════════════════════════════════════════════════════

    IEnumerator OrbFly(Vector2 from, Vector2 to, float dur)
    {
        var img = GetOrb();
        var rt = img.rectTransform;
        Vector2 dir = to - from;
        Vector2 nrm = dir.sqrMagnitude > 0.001f ? new Vector2(-dir.y, dir.x).normalized : Vector2.right;
        // 往上飞就往右拱，往下飞就往左拱，别糊在自己身上
        if (dir.y < 0f) nrm = -nrm;
        Vector2 mid = (from + to) * 0.5f;
        Vector2 ctrl = mid + nrm * Mathf.Min(120f, dir.magnitude * 0.20f);

        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, dur));
            float e = k * k * (3f - 2f * k);
            rt.anchoredPosition = Bezier(from, ctrl, to, e);
            rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.5f, k);
            yield return null;
        }
        img.gameObject.SetActive(false);
    }

    void PopNumber(Vector2 at, int value)
    {
        if (value <= 0) return;
        var f = GetFly(_popLayer);
        f.rt.anchoredPosition = at;
        f.rt.localScale = Vector3.one * 0.5f;
        f.tmp.text = "-" + value;
        f.tmp.fontSize = popFontSize;
        DamageFloater.ApplyStyle(f.tmp, FloaterType.Damage);
        f.cg.alpha = 1f;
        StartCoroutine(PopRoutine(f));
    }

    IEnumerator PopRoutine(FlyNum f)
    {
        const float dur = 1.0f;
        Vector2 p0 = f.rt.anchoredPosition;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / dur);
            f.rt.anchoredPosition = p0 + new Vector2(0f, 76f * Mathf.Sqrt(k));
            float sc = k < 0.18f ? Mathf.Lerp(0.5f, 1.18f, k / 0.18f)
                                 : Mathf.Lerp(1.18f, 1f, (k - 0.18f) / 0.82f);
            f.rt.localScale = Vector3.one * sc;
            f.cg.alpha = 1f - Mathf.InverseLerp(0.55f, 1f, k);
            yield return null;
        }
        Release(f);
    }

    // ══════════════════════════════════════════════════════════════════
    // 构建
    // ══════════════════════════════════════════════════════════════════

    Side BuildSide(string name, int side)
    {
        var s = new Side();
        s.root = NewRect(name, _root);
        s.root.anchorMin = s.root.anchorMax = new Vector2(0f, 0.5f);
        s.root.pivot = new Vector2(0.5f, 0.5f);
        s.root.sizeDelta = new Vector2(panelWidth, panelHeight);
        s.root.anchoredPosition = side == SideOpponent ? opponentPos : selfPos;
        s.cg = s.root.gameObject.AddComponent<CanvasGroup>();

        // 跑道型底
        var plateRt = NewRect("Plate", s.root);
        Stretch(plateRt);
        s.plate = plateRt.gameObject.AddComponent<Image>();
        s.plate.sprite = LoadSprite(PlatePath);
        s.plate.type = Image.Type.Sliced;
        s.plate.raycastTarget = false;

        // 头像圆（底盘当 Mask 图元，头像是它被裁成圆的子节点）
        var discRt = NewRect("AvatarDisc", s.root);
        discRt.anchorMin = discRt.anchorMax = new Vector2(0f, 0.5f);
        discRt.pivot = new Vector2(0.5f, 0.5f);
        discRt.sizeDelta = new Vector2(panelHeight, panelHeight);
        discRt.anchoredPosition = new Vector2(panelHeight * 0.5f, 0f);
        var disc = discRt.gameObject.AddComponent<Image>();
        disc.sprite = LoadSprite(DiscPath);
        disc.raycastTarget = false;
        var mask = discRt.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        var avRt = NewRect("Avatar", discRt);
        avRt.anchorMin = avRt.anchorMax = new Vector2(0.5f, 0.5f);
        avRt.pivot = new Vector2(0.5f, 0.5f);
        avRt.sizeDelta = new Vector2(panelHeight * 0.86f, panelHeight * 0.86f);
        avRt.anchoredPosition = Vector2.zero;
        s.avatar = avRt.gameObject.AddComponent<Image>();
        s.avatar.raycastTarget = false;
        s.avatar.enabled = false;

        // 金环压在头像上
        var ringRt = NewRect("AvatarRing", s.root);
        ringRt.anchorMin = ringRt.anchorMax = new Vector2(0f, 0.5f);
        ringRt.pivot = new Vector2(0.5f, 0.5f);
        ringRt.sizeDelta = new Vector2(panelHeight, panelHeight);
        ringRt.anchoredPosition = new Vector2(panelHeight * 0.5f, 0f);
        var ring = ringRt.gameObject.AddComponent<Image>();
        ring.sprite = LoadSprite(RingPath);
        ring.raycastTarget = false;

        // 数字（占头像右边整条跑道）
        var valRt = NewRect("Value", s.root);
        valRt.anchorMin = new Vector2(0f, 0f);
        valRt.anchorMax = new Vector2(1f, 1f);
        valRt.offsetMin = new Vector2(panelHeight * 1.02f, 0f);
        valRt.offsetMax = new Vector2(-panelHeight * 0.12f, 0f);
        s.value = valRt.gameObject.AddComponent<TextMeshProUGUI>();
        s.value.text = "0";
        s.value.fontSize = valueFontSize;
        s.value.alignment = TextAlignmentOptions.Center;
        s.value.enableWordWrapping = false;
        s.value.overflowMode = TextOverflowModes.Overflow;
        s.value.raycastTarget = false;
        ApplyValueStyle(s.value);
        return s;
    }

    /// <summary>面板上的累计数字：沿用顶栏数值那套字体与材质（与 2D 数字同一观感）。</summary>
    static void ApplyValueStyle(TMP_Text t)
    {
        var hud = Player.Instance != null ? Player.Instance.healthText : null;
        if (hud == null && EnemyPlayer.Instance != null) hud = EnemyPlayer.Instance.healthText;
        if (hud != null)
        {
            if (hud.font != null) t.font = hud.font;
            if (hud.fontSharedMaterial != null) t.fontSharedMaterial = hud.fontSharedMaterial;
        }
        t.color = new Color(1f, 0.972f, 0.916f, 1f);
    }

    /// <summary>数字的落脚点 = 面板上那个数字自己的位置（现取现算，别缓存 —— 面板刚建出来时布局还没落定）。</summary>
    Vector2 FlyTarget(int side)
    {
        var s = _sides[side];
        if (s == null) return Vector2.zero;
        var t = s.value != null ? s.value.rectTransform : s.root;
        return t != null ? CanvasLocal(t) : Vector2.zero;
    }

    // ══════════════════════════════════════════════════════════════════
    // 头像
    // ══════════════════════════════════════════════════════════════════

    void RefreshAvatars()
    {
        if (!_built) return;

        // 每秒只查缓存（PeekAvatar 不触发 Steam 请求、不刷日志）；
        // 完整解析（会请求 Steam）只在 Build 时做一次，结果当兜底。
        Texture2D mine = SteamAvatarManager.PeekAvatar(LobbyConfig.LocalSteamID);
        if (mine == null && SteamDataManager.Instance != null) mine = SteamDataManager.Instance.localAvatar;
        if (mine == null) mine = _myAvatarFull;
        ApplyAvatar(SideSelf, mine);

        Texture2D opp = SimpleAI.IsAIMatch ? null : SteamAvatarManager.PeekAvatar(LobbyConfig.RemoteSteamID);
        if (opp == null && !SimpleAI.IsAIMatch) opp = _oppAvatarFull;
        ApplyAvatar(SideOpponent, opp);
    }

    Texture2D _myAvatarFull;
    Texture2D _oppAvatarFull;

    /// <summary>完整解析一次（可能触发 Steam 异步加载 / 打印日志），只在建面板时调用。</summary>
    void ResolveAvatarsOnce()
    {
        _myAvatarFull = SteamAvatarManager.GetAvatarTexture(LobbyConfig.LocalSteamID);
        if (_myAvatarFull == null && SteamDataManager.Instance != null)
            _myAvatarFull = SteamDataManager.Instance.localAvatar;
        _oppAvatarFull = SimpleAI.IsAIMatch ? null : SteamAvatarManager.GetAvatarTexture(LobbyConfig.RemoteSteamID);
    }

    void ApplyAvatar(int side, Texture2D tex)
    {
        var s = _sides[side];
        if (s == null || s.avatar == null) return;
        if (tex == null) { s.avatar.enabled = false; return; }
        if (s.avatar.sprite == null || s.avatar.sprite.texture != tex)
            s.avatar.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        var c = s.avatar.color;
        c.a = 1f;
        s.avatar.color = c;
        s.avatar.enabled = true;
    }

    // ══════════════════════════════════════════════════════════════════
    // 坐标 / 取血条位置
    // ══════════════════════════════════════════════════════════════════

    Vector2 ToCanvas(Vector2 screenPoint)
    {
        if (_canvasRect == null) return screenPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, _cam, out var local);
        return local;
    }

    Vector2 WorldToCanvas(Vector3 world)
    {
        var cam = Camera.main;
        if (cam == null) return Vector2.zero;
        return ToCanvas(RectTransformUtility.WorldToScreenPoint(cam, world));
    }

    Vector2 CanvasLocal(RectTransform rt)
    {
        if (_canvasRect == null || rt == null) return Vector2.zero;
        return _canvasRect.InverseTransformPoint(rt.position);
    }

    static RectTransform HpRect(int side)
    {
        if (side == SideOpponent)
            return EnemyPlayer.Instance != null && EnemyPlayer.Instance.healthText != null
                 ? EnemyPlayer.Instance.healthText.rectTransform : null;
        return Player.Instance != null && Player.Instance.healthText != null
             ? Player.Instance.healthText.rectTransform : null;
    }

    Vector2 DefaultHpAnchor(int side)
    {
        float w = _canvasRect != null ? _canvasRect.rect.width : 1920f;
        float h = _canvasRect != null ? _canvasRect.rect.height : 1080f;
        // 兜底：对方 → 顶栏中部，己方 → 左下
        return side == SideOpponent ? new Vector2(0f, h * 0.42f) : new Vector2(-w * 0.30f, -h * 0.40f);
    }

    // ══════════════════════════════════════════════════════════════════
    // 小工具 / 池
    // ══════════════════════════════════════════════════════════════════

    FlyNum GetFly(RectTransform parent)
    {
        for (int i = 0; i < _fly.Count; i++)
            if (!_fly[i].busy && _fly[i].go.transform.parent == parent) return Take(_fly[i]);

        var rt = NewRect("FlyNum", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 90f);
        var f = new FlyNum
        {
            go = rt.gameObject,
            rt = rt,
            cg = rt.gameObject.AddComponent<CanvasGroup>(),
            tmp = rt.gameObject.AddComponent<TextMeshProUGUI>(),
        };
        f.tmp.alignment = TextAlignmentOptions.Center;
        f.tmp.enableWordWrapping = false;
        f.tmp.overflowMode = TextOverflowModes.Overflow;
        f.tmp.raycastTarget = false;
        _fly.Add(f);
        return Take(f);
    }

    static FlyNum Take(FlyNum f)
    {
        f.busy = true;
        f.go.SetActive(true);
        f.go.transform.SetAsLastSibling();
        return f;
    }

    static void Release(FlyNum f)
    {
        if (f == null) return;
        f.busy = false;
        f.go.SetActive(false);
    }

    Image GetOrb()
    {
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (!_orbs[i].gameObject.activeSelf)
            {
                _orbs[i].gameObject.SetActive(true);
                _orbs[i].transform.SetAsLastSibling();
                return _orbs[i];
            }
        }
        var rt = NewRect("Orb", _fxLayer);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(46f, 46f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = LoadSprite(OrbPath);
        img.raycastTarget = false;
        _orbs.Add(img);
        return img;
    }

    static Sprite LoadSprite(string path)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogWarning("[AttackTurnPanel] 找不到 Resources/" + path + ".png");
        return sp;
    }

    static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float q = Mathf.Clamp01(p) - 1f;
        return 1f + c3 * q * q * q + c1 * q * q;
    }

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
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
