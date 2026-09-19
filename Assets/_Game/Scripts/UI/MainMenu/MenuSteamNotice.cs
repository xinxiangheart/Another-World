using UnityEngine;
using UnityEngine.UI;
using Steamworks;

/// <summary>
/// MenuSteamNotice — 开始界面的「未连接到 Steam」提示窗，2026-09-19。
///
/// 联机走 Steam：检测不到 Steam 客户端就弹一次小窗（文案在场景里的 Label 上，
/// 「未连接到Steam！/仅能体验离线模式」），位置在版本提示窗 MenuUpdateNotice 的正下方，
/// 滑入 / 停留 / 淡出的节奏和它完全一致 —— 也是同一个物体结构复制出来的。
/// 它没有链接，所以不吃点击、也没有悬停高亮。
///
/// 检测（SteamReady）有两级，关键是**不要**踩 SteamManager.Initialized 那个坑：
///   ① 场景里已经有 SteamManager（Game 场景带的那只，DontDestroyOnLoad 一路跟过来）
///      → 直接看它初始化成没成；
///   ② 还没有 SteamManager（开始界面就是这种）→ 只问一句「Steam 客户端在跑吗」
///      （SteamAPI.IsSteamRunning），不初始化、不碰 DLL 之外的任何东西。
/// 之所以不能直接读 SteamManager.Initialized：那个属性的 getter 在没有实例时会**顺手 new 一个
/// SteamManager**，而它的 Awake 会去 Init Steam，`RestartAppIfNecessary` 还可能直接把游戏重启掉。
/// 直连模式（LobbyConfig.IsDirectIP）本来就不需要 Steam，不弹。
/// </summary>
[DisallowMultipleComponent]
public class MenuSteamNotice : MonoBehaviour
{
    [Header("时间轴")]
    [Tooltip("入场时间轴，等它走完才开始倒计时；留空就当已经走完")]
    public SceneIntro intro;
    [Tooltip("时间轴结束到弹出之间隔多久（秒）")]
    public float postIntroDelay = 0.5f;

    [Header("面板（三层）")]
    public CanvasGroup group;
    [Tooltip("会滑动的那一层（父节点）")]
    public RectTransform panel;
    public Image border;
    public Image fill;
    [Tooltip("圆角半径（像素）")]
    public float cornerRadius = 20f;

    [Header("滑动 / 停留")]
    public float slideTime = 0.35f;
    [Tooltip("起点：在落点右边多远（像素）")]
    public float slideFrom = 360f;
    [Tooltip("停多久后淡出（秒）")]
    public float holdTime = 2.5f;
    public float fadeTime = 0.35f;

    [Header("颜色")]
    public Color borderColor = new Color32(0xB9, 0x90, 0x5A, 0xFF);
    public Color fillColor = new Color32(0x0E, 0x14, 0x20, 0xE6);

    [Header("检测")]
    [Tooltip("编辑器里也检测。开发时 Steam 多半没开，会一直弹；嫌吵就关掉")]
    public bool checkInEditor = true;

    enum State { Hidden, Sliding, Holding, Fading }

    State _state = State.Hidden;
    bool _wanted;
    bool _gateDone;        // 入场时间轴已走完
    float _gate;
    float _t;
    float _restX;
    Sprite _sprite;

    // ── Steam 检测 ─────────────────────────────────────────────────────────

    /// <summary>Steam 可用吗（客户端在跑 且 SteamAPI 初始化成功）。</summary>
    public static bool SteamReady()
    {
        if (LobbyConfig.IsDirectIP) return true;        // 直连模式不走 Steam，别误报

        if (FindObjectOfType<SteamManager>() != null)
            return SteamManager.Initialized;

        try { return SteamAPI.IsSteamRunning(); }        // 只问「Steam 客户端在跑吗」，不初始化
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MenuSteamNotice] 检测 Steam 失败：{e.Message}");
            return false;
        }
    }

    /// <summary>这一趟要不要弹。</summary>
    public bool ShouldShow()
    {
        if (Application.isEditor && !checkInEditor) return false;
        return !SteamReady();
    }

    // ── 生命周期 ───────────────────────────────────────────────────────────

    void Awake()
    {
        _gate = postIntroDelay;

        _sprite = MenuUpdateNotice.MakeRoundedSprite(cornerRadius);   // 与版本提示窗同款圆角
        if (border != null) { border.sprite = _sprite; border.type = Image.Type.Sliced; border.color = borderColor; }
        if (fill != null) { fill.sprite = _sprite; fill.type = Image.Type.Sliced; fill.color = fillColor; }

        if (panel != null) _restX = panel.anchoredPosition.x;

        _wanted = ShouldShow();
        if (_wanted) Debug.Log("[MenuSteamNotice] 没检测到 Steam（客户端没开 / 没初始化）→ 提示仅能离线");

        Hide();
    }

    void OnDestroy()
    {
        if (_sprite != null)
        {
            if (_sprite.texture != null) Destroy(_sprite.texture);
            Destroy(_sprite);
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;             // 暂停也照走

        // ① 先等入场时间轴走完，再等 postIntroDelay
        if (!_gateDone)
        {
            if (intro == null || intro.Finished) { _gateDone = true; _gate = postIntroDelay; }
            else return;
        }
        else if (_gate > 0f)
        {
            _gate -= dt;
        }

        // ② 状态机
        switch (_state)
        {
            case State.Hidden:
                if (_wanted && _gate <= 0f)
                {
                    _wanted = false;
                    _state = State.Sliding;
                    _t = 0f;
                    SetVisible(true);
                    Apply(0f);
                }
                break;

            case State.Sliding:
                _t += dt;
                Apply(EaseOut(Mathf.Clamp01(_t / Mathf.Max(0.0001f, slideTime))));
                if (_t >= slideTime) { _state = State.Holding; _t = 0f; Apply(1f); }
                break;

            case State.Holding:
                _t += dt;
                if (_t >= holdTime) { _state = State.Fading; _t = 0f; }
                break;

            case State.Fading:
                _t += dt;
                float q = Mathf.Clamp01(_t / Mathf.Max(0.0001f, fadeTime));
                SetAlpha(1f - q);                      // 只淡出，位置不动
                if (q >= 1f) { _state = State.Hidden; Hide(); }
                break;
        }
    }

    // ── 表现 ───────────────────────────────────────────────────────

    /// <summary>p = 0 在右侧生成位置（透明），p = 1 到落点（不透明）。</summary>
    void Apply(float p)
    {
        float e = Mathf.Clamp01(p);
        SetAlpha(e);
        if (panel != null)
            panel.anchoredPosition = new Vector2(_restX + slideFrom * (1f - e), panel.anchoredPosition.y);
        SetVisible(e > 0.25f);
    }

    void Hide()
    {
        SetAlpha(0f);
        SetVisible(false);
        if (panel != null) panel.anchoredPosition = new Vector2(_restX + slideFrom, panel.anchoredPosition.y);
    }

    void SetAlpha(float a)
    {
        if (group != null) group.alpha = a;
    }

    void SetVisible(bool on)
    {
        if (group != null) { group.blocksRaycasts = on; group.interactable = on; }
    }

    static float EaseOut(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }
}
