using System.Collections.Generic;
using UnityEngine;

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
/// **⚠ 别直接写 rect**：那三块同时是 <see cref="LobbyBgParallax"/> 的层（反向陀螺仪 depth −0.30），
/// 那个组件**每帧都会把 anchoredPosition 整个写掉**。所以本件走 <see cref="LobbyBgParallax.Layer.extra"/>
/// （视差之上再叠一份偏移）—— 一个 rect 只有一个写的人。找不到对应层时才退回直接写 rect。
/// ※ 走 extra 这条路时，**rect 的值要等视差下一次 Update 才刷出来** —— 自证时别在同一帧读。
///
/// **自动挂载**：场景里不用预放节点（同 <c>LobbyBgMotes</c> 的光点、卡牌详情面板都是运行时生成）。
/// <c>AfterSceneLoad</c> 时按名字找 `Entry_Battle`，找到就挂到它父节点（`LobbyUI_v1`）上；
/// 找不到 = 不是大厅场景，什么都不做。挂载发生在**本帧渲染之前** ⇒ 不会先闪一帧静止位再跳走。
///
/// 跑完自己 <c>enabled = false</c>（本件不接任何 EventSystem 事件，停用是安全的）。
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
    [Tooltip("场景刚出来先等多久（秒）")]
    public float startAt = 0.10f;

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

        _t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (!_calibrated)
        {
            float a = ComputeAutoSlide();
            if (a > 0f) { _autoSlide = a; _slide = a; _calibrated = true; }
            else if (_t > startAt) { _slide = _autoSlide; _calibrated = true; }   // 几何一直没就绪：用兜底值，别再等
        }

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

    /// <summary>大厅场景一加载就把本件挂上（<c>Entry_Battle</c> 是这场景独有的名字）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        GameObject anchor = GameObject.Find("Entry_Battle");
        if (anchor == null) return;                       // 不是大厅场景
        Transform parent = anchor.transform.parent;
        if (parent == null || parent.GetComponent<LobbyEntryIntro>() != null) return;
        parent.gameObject.AddComponent<LobbyEntryIntro>();
    }
}
