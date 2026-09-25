using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LoadingScreen —— 进入战斗场景（Game）时的加载界面，2026-09-25。
///
/// 全黑底 + 右下角白字进度。取代原来 AutoConnect 自建的"半透明黑幕（alpha 0.85）+ 屏幕正中白字"。
///
/// 覆盖范围：从 Lobby 真正开始加载 Game 场景那一刻（Preloader.LoadGameScene）起，一直盖到战斗真正开始 ——
///   在线：NetworkTurnSync.gameStarted 时由 AutoConnect 请求撤幕；
///   离线单机：没有 gameStarted 信号，由 AutoConnect 在场景加载完成后请求撤幕（带兜底时限）。
///
/// 撤幕条件（RequestFadeOut）：场景已加载完（+ 至少再停 minShow 秒），然后淡出（FadeOutTime），从不硬切。
/// 撤幕与入场镜头的配合在 GameIntroCamera 那边：镜头会一直停在起始位姿（近景）等这块幕撤掉，
/// 幕一消失它才开始从近景飞向终态 —— 所以「黑幕撤下 → 视角开始从近景移动」，入场那段全程看得见。
///
/// 进度来自 Preloader（资源预加载 40% + 场景异步加载 60%，只增不减）。场景加载完成后进度行收掉，
/// 只留 AutoConnect 推上来的连接 / 等待对手文案（同样是右下角白字）。
///
/// 整层由脚本运行时创建（独立 Overlay 画布 + DontDestroyOnLoad），不依赖任何场景文件，也不需要在 Unity 里手动摆 UI。
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    const int   SortingOrder = 29000;   // 压住所有场景 UI；低于 SceneTransition 的黑条（30000）
    const float MarginX      = 48f;     // 右下角留白（参考分辨率 1920×1080 下的像素）
    const float MarginY      = 32f;
    const float FontSize     = 26f;
    const float FadeOutTime  = 0.45f;   // 淡出时长（秒，走 unscaledTime）

    static LoadingScreen _instance;

    GameObject _root;
    CanvasGroup _group;
    TextMeshProUGUI _text;
    string _status = "";
    float _progress;        // 0~1，只增不减
    bool _fading;           // 正在淡出
    float _fadeT;
    bool _pending;          // 已请求撤幕，等条件满足
    float _pendingAt;
    float _pendingMinShow, _pendingMaxWait;

    public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.activeSelf;

    /// <summary>开始一次新的加载：复位进度与文案并显示。由 Preloader.LoadGameScene 调用。</summary>
    public static LoadingScreen BeginLoad()
    {
        var s = Ensure();
        s.ResetToVisible();
        return s;
    }

    void ResetToVisible()
    {
        _status = "";
        _progress = 0f;
        _pending = false;
        _fading = false;
        _fadeT = 0f;
        if (_group != null) { _group.alpha = 1f; _group.blocksRaycasts = true; }
        if (_root != null) _root.SetActive(true);
        Refresh();
    }

    /// <summary>显示（已经显示着就什么都不做，保留当前进度与文案）。</summary>
    public static void Show()
    {
        var s = Ensure();
        if (s._root == null) return;
        if (s._root.activeSelf) return;   // 已经显示着：保留当前进度与文案，不重来一遍（避免进度从 100% 跳回 0%）
        s.ResetToVisible();
    }

    /// <summary>立刻隐藏（不做淡出；只用于切场景前的清场）。</summary>
    public static void Hide()
    {
        if (_instance == null || _instance._root == null) return;
        _instance._pending = false;
        _instance._fading = false;
        if (_instance._group != null) { _instance._group.alpha = 1f; _instance._group.blocksRaycasts = true; }
        _instance._root.SetActive(false);
    }

    /// <summary>
    /// 请求撤幕（战斗真正开始时调用）：等「场景加载完成 + 入场镜头就位」并且至少再停 minShow 秒，
    /// 然后淡出。maxWait 是兜底，避免任何情况下永远盖着。
    /// </summary>
    public static void RequestFadeOut(float minShow = 0f, float maxWait = 6f)
    {
        if (_instance == null) return;
        if (_instance._pending || _instance._fading) return;   // 已经在撤幕流程里：别把计时重置掉
        _instance._pending = true;
        _instance._pendingAt = Time.unscaledTime;
        _instance._pendingMinShow = minShow;
        _instance._pendingMaxWait = maxWait;
    }

    /// <summary>连接 / 等待对手等状态文案，显示在进度行上方（右下角，白字）。</summary>
    public static void SetStatus(string message)
    {
        if (_instance == null) return;
        _instance._status = message ?? "";
        _instance.Refresh();
    }

    static LoadingScreen Ensure()
    {
        if (_instance == null)
        {
            var go = new GameObject("LoadingScreen");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LoadingScreen>();
        }
        return _instance;
    }

    void Awake()
    {
        if (_instance == null) _instance = this;
        Build();
    }

    void Build()
    {
        if (_root != null) return;

        var canvasGo = new GameObject("LoadingScreenCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();
        _group = canvasGo.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = true;      // 加载期间挡住点击

        // 全黑底：不透明，加载期间顺便挡掉点击
        var black = new GameObject("Black", typeof(RectTransform));
        var blackRt = (RectTransform)black.transform;
        blackRt.SetParent(canvasGo.transform, false);
        blackRt.anchorMin = Vector2.zero;
        blackRt.anchorMax = Vector2.one;
        blackRt.offsetMin = Vector2.zero;
        blackRt.offsetMax = Vector2.zero;
        var img = black.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        // 右下角白字：状态行在上、进度行贴角
        var textGo = new GameObject("Text", typeof(RectTransform));
        var textRt = (RectTransform)textGo.transform;
        textRt.SetParent(canvasGo.transform, false);
        textRt.anchorMin = new Vector2(1f, 0f);
        textRt.anchorMax = new Vector2(1f, 0f);
        textRt.pivot = new Vector2(1f, 0f);
        textRt.anchoredPosition = new Vector2(-MarginX, MarginY);
        textRt.sizeDelta = new Vector2(1000f, 220f);

        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.fontSize = FontSize;
        _text.color = Color.white;
        _text.alignment = TextAlignmentOptions.BottomRight;
        _text.raycastTarget = false;
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSerifCJKsc-Bold SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font != null) _text.font = font;

        _root = canvasGo;
    }

    void Refresh()
    {
        if (_text == null) return;
        bool hasProgress = Preloader.Instance != null;
        float pct = Mathf.Clamp01(_progress);
        string text = _status ?? "";

        // 进度行贴右下角；场景加载完成后收掉（那时重点是连接 / 等待对手的状态文案）
        if (hasProgress && (pct < 0.999f || text.Length == 0))
        {
            string line = $"加载中 {Mathf.RoundToInt(pct * 100f)}%";
            text = text.Length == 0 ? line : text + "\n" + line;
        }
        _text.text = text;
    }

    void Update()
    {
        if (!IsVisible) return;

        // 淡出中：只推 alpha，推完了关掉整层
        if (_fading)
        {
            _fadeT += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(_fadeT / FadeOutTime);
            if (_group != null) _group.alpha = a;
            if (a <= 0f)
            {
                _fading = false;
                if (_group != null) { _group.alpha = 1f; _group.blocksRaycasts = true; }
                if (_root != null) _root.SetActive(false);
            }
            return;
        }

        if (Preloader.Instance != null)
        {
            float p = Preloader.Instance.TotalProgress;
            if (p > _progress + 0.0001f)
            {
                _progress = p;
                Refresh();
            }
        }

        if (!_pending) return;

        // 撤幕条件：场景加载完 + 至少再停 minShow 秒。
        // 入场镜头不用在这里等：GameIntroCamera 会等这块幕撤掉之后才起飞（见 GameIntroCamera.Play）。
        float waited = Time.unscaledTime - _pendingAt;
        bool loadDone = Preloader.Instance == null || Preloader.Instance.SceneReady;
        if ((loadDone && waited >= _pendingMinShow) || waited >= _pendingMaxWait)
        {
            Debug.Log($"[LoadingScreen] 开始淡出（请求后 {waited:F2}s，进度 {Mathf.RoundToInt(_progress * 100f)}%，" +
                      $"场景就绪={loadDone}）");
            _pending = false;
            _fading = true;
            _fadeT = 0f;
            if (_group != null) _group.blocksRaycasts = false;   // 淡出期间放行点击
        }
    }
}
