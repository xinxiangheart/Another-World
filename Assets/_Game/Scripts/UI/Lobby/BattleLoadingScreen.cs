using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 双方确认之后的**真正加载界面**（全遮挡）—— 2026-09-27。
/// </summary>
/// <remarks>
/// 用户原话：「现在做真正的双方确认后的加载界面，这个是真正的全遮挡，包括遮挡左上角和右上角，
/// 上半区域背景从右边滑动到最左边，下半区域背景从左边滑动到最右边，上半区域从右向左展示对方头像
/// （下面是名称和未来做的称号什么的），再左边是预留的段位，再左边是总场次，其下面是胜率，其再下面是
/// 连胜场次，己方的则是从左到右展示在左下角，双方这些信息只占半区，另外黄色区域是一个动态旋转的
/// 装饰性不对称图案，下面是双方加载的进程，只用 xx% 显示即可，加载完成后上半区域很快速从右边滑出，
/// 下半从左边滑出，另外加载过程中上半区域和下半区域不是完全静态，是有一个类似于惯性的缓慢移动
/// （缓慢能看出来动态即可）」。
///
/// 版式（屏幕 px · 1920×1080，与 Tools/cardframe/BattleLoadingV1.ps1 同源）：
///   两条带各 2400×560（**比屏幕宽 480**，每侧 240 是单向惯性"走到底也不露边"的余量），上带中心 (0, +280)、下带中心 (0, -280) —— 上带顶边落到屏外 30px（它那条
///   金线也就落到屏幕外），下带顶边正好落在 y = 0，于是**中央分隔金线只有一条**（上下共用一张贴图）。
///   带内局部坐标（相对带中心，+y 向上）：饰纹 x = ∓560 / 总场次·胜率·连胜列 x = ±185 / 段位 x = ±430 /
///   头像·名字·称号 x = ±740；上带取正、下带全部镜像（左右与上下同时翻）。进度 xx% 挂在根上 (0, -440)，
///   不随带滑动。
///
/// 时序：In（0.5s 缓出滑入）→ Hold（**单向惯性滑行**（上带向左 / 下带向右，见 Drift/SlideLength）+ 饰纹自转 + 进度爬升）→ Out（0.35s 缓入加速甩出）→
///   甩完才 Preloader.LoadGameScene()（之后由 LoadingScreen 的黑幕接着盖到战斗开始）。
///
/// 进度只取 Preloader.Progress（资源预加载那一半；场景那 60% 还没开始），并且被 minSeconds 的爬升速度
/// 封顶 —— 双方确认那一刻 Preloader 就已经跑起来了，进这屏时资源往往早就加载完，不封顶这屏会一闪而过。
///
/// 层级：挂在 Canvas/Layer_Hud_v1 下的 Panel_BattleLoading（常驻 active），视觉全在子物体 window
/// （存 inactive，用 Inspector 右键菜单「预览（显示）」看效果）—— 与 MatchConfirmPanel 同一种写法。
/// 贴图出图脚本：Tools/cardframe/BattleLoadingV1.ps1。
/// </remarks>
public class BattleLoadingScreen : MonoBehaviour
{
    public static BattleLoadingScreen Instance { get; private set; }

    [Header("视觉根（Open / Hide 切这个 —— 整块全遮挡）")]
    public GameObject window;

    [Header("上下两个半区（2400x560 的背景带 —— 比屏幕宽 480，留给单向惯性）")]
    public RectTransform bandTop;      // 对方：从右滑入 / 向右滑出
    public RectTransform bandBottom;   // 己方：从左滑入 / 向左滑出

    [Header("饰纹（动态旋转的不对称图案）")]
    public RectTransform ornTop;
    public RectTransform ornBottom;

    [Header("对方（上带 · 从右向左）")]
    public RawImage oppAvatar;
    public RawImage oppFrame;
    public TMP_Text oppNameText;
    public TMP_Text oppTitleText;
    [Tooltip("三行战绩：标签一列（钢灰）+ 数值一列（奶白）—— 拆两列各自对齐，数字才成列。")]
    public TMP_Text oppTotalLabel, oppTotalValue;
    public TMP_Text oppWinRateLabel, oppWinRateValue;
    public TMP_Text oppStreakLabel, oppStreakValue;

    [Header("己方（下带 · 从左向右）")]
    public RawImage localAvatar;
    public RawImage localFrame;
    public TMP_Text localNameText;
    public TMP_Text localTitleText;
    public TMP_Text localTotalLabel, localTotalValue;
    public TMP_Text localWinRateLabel, localWinRateValue;
    public TMP_Text localStreakLabel, localStreakValue;

    [Header("进度（只显示 xx%）")]
    public TextMeshProUGUI progressText;

    [Header("时长")]
    [Tooltip("进场：上带从右侧滑入 / 下带从左侧滑入（缓出）。")]
    public float openSeconds = 0.5f;
    [Tooltip("收工：上带向右 / 下带向左迅速甩出（缓入加速）。")]
    public float outSeconds = 0.35f;
    [Tooltip("至少显示这么久 —— 进度按 1/minSeconds 的速度爬升封顶，免得这屏一闪而过。")]
    public float minSeconds = 1.4f;

    [Header("惯性漂移（缓慢的匀速滑行 —— 「缓慢能看出来动态即可」）")]
    [Tooltip("单向滑行速度（屏幕 px/s）—— 上带向左、下带向右（用户 2026-09-27）。")]
    public float driftSpeed = 10f;
    [Tooltip("起步的加速段（秒）—— 从静止匀加速到 driftSpeed，像惯性滑出去。")]
    public float driftRamp = 0.8f;
    [Tooltip("最多滑这么多屏幕 px（= 条带每侧多画出来的宽度）—— 走到头就停住，永远不露边。")]
    public float driftMax = 240f;
    [Tooltip("下带相对上带的时间滞后（秒）—— 有滞后才像「惯性」；完全同步会像整体平移。")]
    public float driftLag = 0.35f;

    [Header("饰纹")]
    public float ornamentDegPerSec = 12f;

    [Header("滑进滑出的距离（屏幕 px）")]
    [Tooltip("960 + 带宽一半 = 2160：条带比屏幕宽 480，仍取 1920 会让它开局就露出一条边。")]
    public float slideDistance = 2160f;

    [Header("占位文案（称号 / 段位都还没做）")]
    public string titlePlaceholder = "称号·待定";
    public string emptyNumber = "--";

    /// <summary>调试用：只播动画、不真的切战斗场景（冒烟测试脚本在运行期置位，不进 Inspector）。</summary>
    public static bool DebugSkipSceneLoad;

    enum State { Idle, In, Hold, Out }
    State _st = State.Idle;
    float _t;          // In / Out 计时
    float _holdT;      // Hold 计时（漂移与进度爬升共用）
    float _ornAngle;
    float _dx, _dx2;              // 漂移量（只有横向 —— 用户 2026-09-27：两条带只沿左右移动）
    Vector2 _restTop, _restBottom;
    bool _restCached;
    bool _finished;

    public bool IsOpen { get { return window != null && window.activeSelf; } }

    /// <summary>是否正处在「停位惯性滑行」这一段 —— 冒烟脚本按状态判定单向窗口，
    /// 不用 x 阈值猜：滑出段起步 x 也会往回走，拿阈值卡会把甩出误判成反向帧。</summary>
    public bool IsDrifting { get { return _st == State.Hold; } }

    void Awake()
    {
        Instance = this;
        CacheRest();
        if (window != null && window != gameObject) window.SetActive(false);
    }

    // ===================== 开关 =====================

    /// <summary>双方确认后由 QuickMatchPanel 调 —— 顶替原来的 JoinGamePanel。</summary>
    public void Open()
    {
        if (IsOpen) return;
        CacheRest();
        EnsurePreloader();
        if (Preloader.Instance != null) Preloader.Instance.StartPreload();   // 幂等：MatchConfirmPanel 已经起过就不再起

        FillContent();

        _st = State.In; _t = 0f; _holdT = 0f; _finished = false;
        _dx = _dx2 = 0f;
        LayoutBands(slideDistance, -slideDistance);   // 先摆到屏外
        SetProgress(0f);
        if (window != null) window.SetActive(true);
    }

    /// <summary>收工 / 取消：直接收掉整块（不走滑出动画）。</summary>
    public void Hide()
    {
        _st = State.Idle;
        if (window != null && window != gameObject) window.SetActive(false);
    }

    // ===================== 逐帧 =====================

    void Update()
    {
        if (_st == State.Idle || !IsOpen) return;
        float dt = Time.unscaledDeltaTime;

        switch (_st)
        {
            case State.In:
            {
                _t += dt;
                float k = Mathf.Clamp01(_t / Mathf.Max(0.01f, openSeconds));
                float off = Mathf.Lerp(slideDistance, 0f, EaseOutCubic(k));
                LayoutBands(off, -off);
                SpinOrnament(dt);
                if (k >= 1f) { _st = State.Hold; _holdT = 0f; }
                break;
            }

            case State.Hold:
            {
                _holdT += dt;
                Drift(_holdT);
                LayoutBands(0f, 0f);
                SpinOrnament(dt);
                SetProgress(ShownProgress());
                if (Ready()) { _st = State.Out; _t = 0f; }
                break;
            }

            case State.Out:
            {
                _t += dt;
                float k = Mathf.Clamp01(_t / Mathf.Max(0.01f, outSeconds));
                float off = Mathf.Lerp(0f, slideDistance, k * k);   // 缓入：越走越快，甩出去
                LayoutBands(off, -off);
                SpinOrnament(dt);
                if (k >= 1f) Finish();
                break;
            }
        }
    }

    // ===================== 带位 / 漂移 / 饰纹 =====================

    void CacheRest()
    {
        if (_restCached) return;
        if (bandTop != null) _restTop = bandTop.anchoredPosition;
        if (bandBottom != null) _restBottom = bandBottom.anchoredPosition;
        _restCached = true;
    }

    /// <summary>带上带 / 下带的最终位置再叠一层漂移。**只改 x** —— 用户 2026-09-27：
    /// 「上下两个背景只会沿着左右移动，惯性也一样」，纵向自本条起锁死在 rest 位上，
    /// 滑进 / 停位 / 滑出 / 惯性所有阶段都不许有纵向位移。
    /// offTop / offBottom = 滑进滑出的额外横移。</summary>
    void LayoutBands(float offTop, float offBottom)
    {
        if (bandTop != null)
            bandTop.anchoredPosition = new Vector2(_restTop.x + offTop + _dx, _restTop.y);
        if (bandBottom != null)
            bandBottom.anchoredPosition = new Vector2(_restBottom.x + offBottom + _dx2, _restBottom.y);
    }

    /// <summary>缓慢惯性漂移：**只有横向**（纵向已按用户 2026-09-27 的要求锁死）；
    void Drift(float t)
    {
        _dx = -SlideLength(t);
        _dx2 = SlideLength(t - driftLag);
    }

    /// <summary>单向滑行位移 = ∫v dt：前 driftRamp 秒从静止匀加速到 driftSpeed，之后匀速；
    /// 封顶 driftMax —— 条带每侧就多画了这么多（Tools/cardframe/BattleLoadingV1.ps1 的 $BAND_PAD），
    /// 走到头也只是贴住屏幕边缘，永远不露底。</summary>
    float SlideLength(float t)
    {
        if (t <= 0f) return 0f;
        float ramp = Mathf.Max(0.01f, driftRamp);
        float d = t < ramp ? driftSpeed * t * t / (2f * ramp)
                           : driftSpeed * (t - ramp * 0.5f);
        return Mathf.Min(d, Mathf.Max(0f, driftMax));
    }

    void SpinOrnament(float dt)
    {
        _ornAngle += ornamentDegPerSec * dt;
        if (_ornAngle > 360f) _ornAngle -= 360f;
        if (ornTop != null) ornTop.localRotation = Quaternion.Euler(0f, 0f, _ornAngle);
        if (ornBottom != null) ornBottom.localRotation = Quaternion.Euler(0f, 0f, -_ornAngle);   // 镜像反向，和带的镜像一致
    }

    static float EaseOutCubic(float k) { float u = 1f - k; return 1f - u * u * u; }

    // ===================== 进度 =====================

    /// <summary>显示进度 = 真实进度，但爬升速度不超过 1/minSeconds（防止一闪而过）。</summary>
    float ShownProgress()
    {
        float real = RealProgress();
        float ramp = _holdT / Mathf.Max(0.05f, minSeconds);
        return Mathf.Clamp01(Mathf.Min(real, ramp));
    }

    /// <summary>真实进度只取资源预加载那一半（Preloader.Progress；场景那 60% 还没开始）。</summary>
    float RealProgress()
    {
        var p = Preloader.Instance;
        if (p == null) return 1f;
        if (p.TimedOut) return 1f;
        return Mathf.Clamp01(p.Progress);
    }

    bool Ready()
    {
        return RealProgress() >= 0.999f && _holdT >= Mathf.Max(0f, minSeconds);
    }

    void SetProgress(float v)
    {
        if (progressText == null) return;
        progressText.text = Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        _st = State.Idle;
        if (window != null && window != gameObject) window.SetActive(false);
        EnsurePreloader();
        if (Preloader.Instance != null && !DebugSkipSceneLoad) Preloader.Instance.LoadGameScene();
        Debug.Log("[BattleLoading] 加载界面收工 → 切战斗场景");
    }

    // ===================== 内容 =====================

    void FillContent()
    {
        var sd = SteamDataManager.Instance;
        var qm = QuickMatchPanel.Instance;

        // 己方（左下）
        SetText(localNameText, (sd != null && !string.IsNullOrEmpty(sd.localPlayerName)) ? sd.localPlayerName : "我");
        SetText(localTitleText, titlePlaceholder);
        ApplyAvatar(localAvatar, sd != null ? sd.localAvatar : null);
        SetRow(localTotalLabel, localTotalValue, "总场次", sd != null ? sd.playerData.totalMatches.ToString() : emptyNumber);
        SetRow(localWinRateLabel, localWinRateValue, "胜率", sd != null ? sd.WinRate.ToString("F1") + "%" : emptyNumber);
        SetRow(localStreakLabel, localStreakValue, "连胜", sd != null ? sd.playerData.winStreak.ToString() : emptyNumber);

        // 对方（右上）
        SetText(oppNameText, (qm != null && !string.IsNullOrEmpty(qm.opponentName)) ? qm.opponentName : "对手");
        SetText(oppTitleText, titlePlaceholder);
        ApplyAvatar(oppAvatar, qm != null ? qm.opponentTexture as Texture2D : null);
        bool ok = qm != null && qm.opponentStatsKnown;
        SetRow(oppTotalLabel, oppTotalValue, "总场次", ok ? qm.opponentTotalMatches.ToString() : emptyNumber);
        SetRow(oppWinRateLabel, oppWinRateValue, "胜率", ok ? qm.opponentWinRate.ToString("F1") + "%" : emptyNumber);
        SetRow(oppStreakLabel, oppStreakValue, "连胜", ok ? qm.opponentWinStreak.ToString() : emptyNumber);
    }

    static void SetText(TMP_Text t, string s) { if (t != null) t.text = s; }

    /// <summary>一行战绩 = 标签（钢灰）+ 数值（奶白）两个 TMP —— 两列各自对齐，数字才成列。</summary>
    static void SetRow(TMP_Text label, TMP_Text value, string labelText, string valueText)
    {
        SetText(label, labelText);
        SetText(value, valueText);
    }

    /// <summary>铺头像（圆形裁切复用 PlayerProfilePanel 那套；源图不可读时退回占位圆盘）。</summary>
    static void ApplyAvatar(RawImage img, Texture2D src)
    {
        if (img == null) return;
        if (src == null) { img.texture = PlayerProfilePanel.Placeholder(); return; }
        var tex = PlayerProfilePanel.CircleCrop(src);
        img.texture = tex != null ? tex : src;
    }

    static void EnsurePreloader()
    {
        if (Preloader.Instance == null)
        {
            var go = new GameObject("Preloader");
            go.AddComponent<Preloader>();
        }
    }

    // ===================== Inspector 预览 =====================

    [ContextMenu("预览（显示）")]
    void PreviewShow()
    {
        CacheRest();
        if (bandTop != null) bandTop.anchoredPosition = _restTop;
        if (bandBottom != null) bandBottom.anchoredPosition = _restBottom;
        if (window != null) window.SetActive(true);
    }

    [ContextMenu("预览（隐藏）")]
    void PreviewHide()
    {
        if (window != null && window != gameObject) window.SetActive(false);
    }
}
