using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>大厅入口板的入场：进 Lobby 场景时，右边那三栏**从屏幕右边外面滑进来**
/// （`Entry_Battle` / `Entry_Cards` / `Entry_BottomRow` —— 最下面两个格子共用那个透明大框，天然算一栏）。</summary>
/// <remarks>2026-09-29 用户三改，最终口径（本节为准）：
///   ① 「加动态：进入 lobby 场景中时，右边中间那三栏也会从右边滑动进场（最下面两个格子算一栏）」
///   ② 「不需要完全从屏幕外滑入」—— 中间试过固定 slide 220，**已作废**
///   ③ 「算了仍是从屏幕外滑入，不过**加快滑动速度**，并且做**非恒定速度**，基础速度快，**即将到达终点时变慢**」⇒ 现版
/// 所以：slide 默认 0 = **自动** = 容器宽度 + <see cref="margin"/>（往右推整整一屏 ⇒ 任何本来在容器里的栏都必然出右界；
/// 1920 参考宽度下 = **1980px**），缓动 <c>1-(1-t)^4</c> —— 一半路程在**头 16% 时间**里走完，
/// 末端明显刹车，就是「基础速度快、快到终点变慢」。
///
/// **⚠ 自动档的距离不能在 <c>Awake</c> 里算**：那一刻 Canvas / RectTransform 的 rect 还没建立（实测拿到 0 宽）。
/// 现在改成：<c>Awake</c> 先套 <see cref="fallback"/>（保证在屏外），**第一帧 <c>Update</c> 再按真实几何定标**，
/// 定标发生在 <c>startAt</c> 之前 ⇒ 三栏都还没开始动，改了也看不出来。
///
/// **⚠ 也不能靠「量最左那一栏的左沿」**：2026-09-29 两次实测都栽在这上面（<c>GetWorldCorners</c> 读的是**上一帧**
/// 算好的变换矩阵，和当帧刚改的 <c>anchoredPosition</c> 不是一个时间点，量出来永远差一个「上一版偏移」；
/// 拿它算会得到 881 这种**根本没出屏**的小值）。所以改成**只读容器宽度**：
/// 「往右推整整一屏」对任何水平方向本来落在容器里的栏都必然出右界（左沿 + 容器宽 ≥ 容器右沿 恒成立），
/// 一个几何参数都不用猜。
///
/// **动作口径与全项目同源**（同 <c>LobbyCardsIntro</c> / 开始界面的 <c>SceneIntro</c>）：只多一档「四次方」收尾。
///   · 单帧增量封顶 <c>Mathf.Min(Time.unscaledDeltaTime, 0.05f)</c> —— 编辑器进 Play 头几帧很慢，不封顶会「看不见就播完」；
///   · 起手位 = 静止位 **+ slide（往右）**，随时间滑回 0；三栏起手依次错开 <see cref="step"/>。
///
/// **⚠ 必须等过场黑幕收完再开始**（2026-09-29 用户：「开始界面切换界面结束进入后这几个矩形已经到达位置了，
/// 应开始界面结束时开始播放动态」）。开始界面→大厅走的是 <see cref="SceneTransition"/>：
/// 新场景**在全黑里就被激活**，之后还有 <c>PostLoadWait 0.12s + FadeInTime 0.90s</c> 的渐入 ——
/// 入场要是在 <c>Awake</c> 开始计时，等屏幕亮起来时三栏早已经在位上了（实测就是这个现象）。
/// 所以本件**先把三栏压在屏外等着**，直到 <see cref="SceneTransition.IsRunning"/> 变 false
/// （= 渐入完、屏幕全亮）才 <c>_t = 0</c> 开始计时。没有过场（比如编辑器直接 Play 大厅）就当场开始。
///
/// **⚠ 别直接写 rect**：那三块同时是 <see cref="LobbyBgParallax"/> 的层（反向陀螺仪 depth −0.30），
/// 那个组件**每帧都会把 anchoredPosition 整个写掉**。所以本件走 <see cref="LobbyBgParallax.Layer.extra"/>
/// （视差之上再叠一份偏移）—— 一个 rect 只有一个写的人。找不到对应层时才退回直接写 rect。
/// ※ 走 extra 这条路时，**rect 的值要等视差下一次 Update 才刷出来** —— 自证时别在同一帧读。
///
/// **自动挂载**：场景里不用预放节点（同 <c>LobbyBgMotes</c> 的光点、卡片详情面板都是运行时生成）。
/// 按名字找 `Entry_Battle`（不依赖场景文件名），找到就挂到它父节点（`LobbyUI_v1`）上；
/// 找不到 = 不是大厅场景，什么都不做。挂载发生在**本帧渲染之前** ⇒ 不会先闪一帧静止位再跳走。
///
/// **⚠ `AfterSceneLoad` 只在「本次启动的第一个场景」之后跑一次**（2026-09-29 实测：真的从开始界面切进大厅时，
/// 这个回调**根本没再跑** ⇒ 入口板压根没挂上，三栏就那么躺在原位一动不动 ——
/// 用户看到的「已经到达位置了」就是它）。所以这里同时**订阅 `SceneManager.sceneLoaded`**，之后每次切场景都过一遍。
/// </remarks>
[DisallowMultipleComponent]
public class LobbyEntryIntro : MonoBehaviour
{
    [Header("入场的三栏（按名字在本节点下找；下排两格共用 Entry_BottomRow 这一个大框 ⇒ 天然是一栏）")]
    public string[] rows = { "Entry_Battle", "Entry_Cards", "Entry_BottomRow" };

    [Header("动作参数")]
    [Tooltip("从右边起手的距离（px · 1920x1080 参考分辨率）。0 = 自动：把最靠左那一栏整栏推到屏幕右沿之外（+ margin），三栏同距")]
    public float slide = 0f;
    [Tooltip("slide = 0（自动）时，在屏幕右沿外再多留出来的余量（px）")]
    public float margin = 60f;
    [Tooltip("自动档的兜底距离（px）：定标拿不到容器宽度时用它，保证起手就在屏幕外")]
    public float fallback = 2200f;
    [Tooltip("单栏时长（秒）—— 用户 2026-09-29：「加快滑动速度」；全程 ≈1670px（从屏幕外）")]
    public float dur = 0.45f;
    [Tooltip("相邻两栏的起手间隔（秒），从上到下依次")]
    public float step = 0.09f;
    [Tooltip("场景刚出来先等多久（秒；从「过场黑幕收完」那一刻算起）—— 用户 2026-09-29 定：0.03")]
    public float startAt = 0.01f;
    [Tooltip("等过场（SceneTransition）黑幕收完再开始——开始界面切进来时不得已经跑完")]
    public bool waitForTransition = true;
    [Tooltip("最多等过场多久（秒）—— 黑幕那边万一出盆子，别让三栏永远停在屏外")]
    public float curtainMaxWait = 8f;

    class Row
    {
        public RectTransform rt;
        public Vector2 basePos;                 // Awake 那一刻的静止位（那时视差的 extra 还是 0）
        public LobbyBgParallax.Layer layer;     // 有就写 layer.extra（别跟视差抢 rect）
        public float at;                        // 起手时刻（Play 时按 step 重排）
    }

    readonly List<Row> _rows = new List<Row>();
    float _autoSlide;                          // 自动档距离（Awake 先给 fallback，第一帧 Update 定标）
    float _slide;                              // 本次实际用的距离
    float _t;
    bool _run;
    bool _calibrated;                          // 自动档是否已按真实几何定标
    bool _released;                            // 过场放行了没（没有过场就当场放行）
    float _heldT;                              // 已经在屏外等了多久

    void Awake()
    {
        var parallax = GetComponentInChildren<LobbyBgParallax>(true);
        for (int i = 0; i < rows.Length; i++)
        {
            RectTransform rt = transform.Find(rows[i]) as RectTransform;
            if (rt == null) continue;
            _rows.Add(new Row
            {
                rt = rt,
                basePos = rt.anchoredPosition,                          // 此刻 = 静止位（视差的 extra 还是 0）
                layer = parallax != null ? parallax.FindLayer(rt) : null,
            });
        }
        _autoSlide = fallback;
        _calibrated = slide > 0f;                  // 手动给了距离就不用定标
        Play();
    }

    /// <summary>把三栏压回起手位再滑回来。<c>Awake</c> 会调一次；也可手动重放（调试 / 自证）。
    /// **只摆参数、不当场套偏移** —— 偏移留给第一帧 <c>Update</c>（要在定标之后），见类注释。</summary>
    public void Play()
    {
        if (_rows.Count == 0) return;
        _calibrated = slide > 0f;                  // 自动档：下一帧 Update 里按真实几何定标
        _slide = slide > 0f ? slide : _autoSlide;  // 自动档先给兜底值，定标成功就被覆盖
        _released = !waitForTransition || !SceneTransition.IsRunning;   // 没有过场就当场开始
        _heldT = 0f;
        _t = 0f;
        _run = true;
        enabled = true;
        for (int i = 0; i < _rows.Count; i++) _rows[i].at = startAt + step * i;
    }

    /// <summary>自动档的统一距离 = **容器宽度 + <see cref="margin"/>**：往右推整整一屏。
    /// 对任何水平方向本来就落在容器里的栏，左沿 + 容器宽 ≥ 容器右沿恒成立 ⇒ 必然整栏出右界。
    /// 返回 &lt; 0 = 容器几何还没就绪（早于 Canvas 建立 rect），下一帧再试。
    /// ⚠ 见类注释：**不能在 Awake 里算**（那时 rect 是 0 宽）；也**不要**去量世界角点（帧延迟）。
    /// </summary>
    float ComputeAutoSlide()
    {
        var parent = transform as RectTransform;
        if (parent == null) return -1f;
        float w = parent.rect.width;
        if (w < 2f) return -1f;                                        // Canvas 还没把 rect 建出来
        return w + margin;
    }

    void Update()
    {
        if (!_run) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (!_calibrated)                       // 定标（等过场的时候也要把距离备好）
        {
            float a = ComputeAutoSlide();
            if (a > 0f) { _autoSlide = a; _slide = a; _calibrated = true; }
            else if (_heldT + _t > startAt) { _slide = _autoSlide; _calibrated = true; }
        }

        if (!_released)                         // 等过场黑幕收完：三栏压在屏外一动不动，计时不走
        {
            _heldT += dt;
            if (SceneTransition.IsRunning && _heldT < curtainMaxWait) { ApplyAll(_slide); return; }
            _released = true;
            _t = 0f;                            // 从「屏幕全亮」这一刻开始计时
        }

        _t += dt;

        bool all = true;
        for (int i = 0; i < _rows.Count; i++)
        {
            Row r = _rows[i];
            if (r.rt == null) continue;
            float p = Mathf.Clamp01((_t - r.at) / Mathf.Max(0.0001f, dur));
            Apply(r, _slide * (1f - Ease(p)));
            if (p < 1f) all = false;
        }
        if (all) { SnapHome(); _run = false; enabled = false; }
    }

    void OnDisable()
    {
        if (_run) { SnapHome(); _run = false; }     // 中途被停（换场景等）：把偏移清掉，别留残影
    }

    void SnapHome()
    {
        for (int i = 0; i < _rows.Count; i++) Apply(_rows[i], 0f);
    }

    void ApplyAll(float off)
    {
        for (int i = 0; i < _rows.Count; i++) Apply(_rows[i], off);
    }

    static void Apply(Row r, float off)
    {
        Vector2 d = new Vector2(off, 0f);
        if (r.layer != null) r.layer.extra = d;                  // 交给视差那层去加（它每帧会写 rect）
        else if (r.rt != null) r.rt.anchoredPosition = r.basePos + d;
    }

    /// <summary><c>1 - (1-t)^4</c>：起步快、**末端明显刹车**（用户：「基础速度快，即将到达终点时变慢」）。
    /// 数字化：**一半路程只花头 15.9% 的时间**，93.75% 的路程花一半时间 —— 最后 6.25% 用掉另一半时间。</summary>
    static float Ease(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        float inv2 = inv * inv;
        return 1f - inv2 * inv2;
    }

    /// <summary>按名字找 `Entry_Battle`，找到就把本件挂到它父节点上。
    /// ① 启动那一下跑一次；② 之后**每次切场景都跑**（见类注释：只靠 <c>AfterSceneLoad</c> 会漏掉大厅）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;        // 先卸再挂，避免重复订阅
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // 启动那个场景手动过一遍
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameObject anchor = GameObject.Find("Entry_Battle");
        if (anchor == null) return;                       // 不是大厅场景
        Transform parent = anchor.transform.parent;
        if (parent == null || parent.GetComponent<LobbyEntryIntro>() != null) return;
        parent.gameObject.AddComponent<LobbyEntryIntro>();
    }
}
