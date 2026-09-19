using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SceneTransition —— 黑条扫屏切场景，2026-09-19。
///
/// 点「开始游戏」后：等宽黑条从屏幕右侧依次滑进来，自左向右一格格把整屏盖黑 →
/// 全黑这段时间不白等：把目标场景一进去就可能用到的重资源提前读进来（见 SceneResources）→
/// 再多压一小段全黑（免得闪一下）→ 由黑渐入，和 Welcome 开场同款。
///
/// 用法：用 SceneTransition.LoadScene("Lobby") 取代 SceneManager.LoadScene("Lobby")。
/// 整层是脚本运行时创建的（独立 Overlay 画布 + DontDestroyOnLoad），永远在所有场景 UI 之上，
/// 切场景时不会被销毁，渐入结束后自己销毁。可调参数都在下面的常量区。
/// </summary>
public class SceneTransition : MonoBehaviour
{
    // ── 可调参数 ───────────────────────────────────────────────────────────
    const int   BarCount     = 9;       // 黑条数量
    const float BarTime      = 0.55f;   // 单条从右侧扫到落点的时长
    const float BarStagger   = 0.09f;   // 条与条之间的错峰（第 i 条晚 i × 这么久出发）
    const float MinBlackHold = 0.35f;   // 全黑至少停这么久
    const float PostLoadWait = 0.12f;   // 新场景激活后再等一小下，避开第一帧卡顿
    const float FadeInTime   = 0.90f;   // 由黑渐入的时长
    const int   SortingOrder = 30000;   // 压在所有场景 UI 之上
    // ──────────────────────────────────────────────────────────────────────

    // ── 过场预加载 ─────────────────────────────────────────────────────────
    // 扫屏 + 全黑这段时间不白等：把目标场景一进去就可能会用到的重资源先读进来。
    // 路径是 Resources 下的相对路径，**结尾带 / 表示整个目录**（Resources.LoadAll，目录没有异步枚举 API），
    // 不带则是单个资源（Resources.LoadAsync）。一个条目占一帧，条目之间可以超时喊停。
    static readonly Dictionary<string, string[]> SceneResources = new Dictionary<string, string[]>
    {
        // Lobby：卡牌总览一打开就会 Instantiate 全部 ~250 张卡、每张现读自己的卡面，
        //        外加 CardCollectionPanel.Awake 里那一次同步的 Resources.LoadAll<CardData>。
        { "Lobby", new[]
            {
                "CardData/",                // 179 张卡牌模板（Lobby 一进场就会被同步读一遍）
                "ChosenOneData/",           // 7 张神选者模板
                "Cards/Back And Front/",    // 卡框 + 卡背（小，必用）
                "Cards/PrefixArtBG/",       // 前缀底图（小，必用）
                "Cards/Summon/Hero/1/",     // ↓ 卡面，最重的一坨
                "Cards/Summon/Hero/3/",
                "Cards/Summon/Hero/5/",
                "Cards/Summon/ChosenOne/",
                "Cards/Summon/Special/",
            }
        },
    };

    const float PreloadTimeout = 6f;    // 预加载最多占这么久，超时就先进场景，剩下的交回场景按需加载
    // ──────────────────────────────────────────────────────────────────────

    static SceneTransition _instance;
    static bool _busy;

    RectTransform _canvasRect;
    CanvasGroup _group;

    // 预加载出来的资源要一直拎着：场景激活时 Unity 会跑一次 UnloadUnusedAssets，
    // 没人引用的资源会被当场收走，等于白读。
    readonly List<Object> _preloaded = new List<Object>();
    bool _preloadDone;

    readonly List<RectTransform> _bars = new List<RectTransform>();
    readonly List<float> _from = new List<float>();     // 生成位置（屏幕右侧外）
    readonly List<float> _to = new List<float>();       // 落点（左边缘）
    readonly List<float> _delay = new List<float>();    // 这一条晚多久出发

    /// <summary>带着黑条扫屏切到某个场景；正在切就直接忽略。</summary>
    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || _busy) return;

        if (_instance == null)
        {
            var go = new GameObject("SceneTransition");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneTransition>();
        }
        _busy = true;
        _instance.StartCoroutine(_instance.Run(sceneName));
    }

    void OnDestroy()
    {
        if (_instance == this) { _instance = null; _busy = false; }
    }

    // ── 流程 ───────────────────────────────────────────────────────────────

    IEnumerator Run(string sceneName)
    {
        Build();
        yield return null;              // 等画布完成一次布局，拿到屏幕尺寸
        BuildBars();

        yield return BarsIn();          // ① 黑条扫屏，直到整屏全黑

        // ② 全黑期间异步加载新场景
        float blackStart = Time.realtimeSinceStartup;
        AsyncOperation op = null;
        try { op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
        catch (System.Exception e) { Debug.LogError($"[SceneTransition] 场景 {sceneName} 打不开: {e.Message}"); }

        if (op == null)
        {
            Debug.LogError($"[SceneTransition] 加载场景失败: {sceneName}");
            Destroy(gameObject);
            yield break;
        }

        op.allowSceneActivation = false;

        _preloadDone = false;
        StartCoroutine(PreloadRoutine(sceneName));                                 // 与场景加载并行跑

        while (op.progress < 0.9f) yield return null;                              // 场景资源加载完成
        while (!_preloadDone) yield return null;                                   // 预加载也读完再掀幕
        while (Time.realtimeSinceStartup - blackStart < MinBlackHold) yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return new WaitForSecondsRealtime(PostLoadWait);

        // ③ 由黑渐入：黑条留在原地，整层一起淡出
        float t = 0f;
        while (t < FadeInTime)
        {
            t += Time.unscaledDeltaTime;                                            // 暂停也照走
            SetAlpha(1f - EaseInOut(Mathf.Clamp01(t / FadeInTime)));
            yield return null;
        }
        SetAlpha(0f);

        Destroy(gameObject);
    }

    // ── 搭建 ───────────────────────────────────────────────────────────────

    void Build()
    {
        var canvasGo = new GameObject("TransitionCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;         // 比任何场景 UI 都靠上
        canvasGo.AddComponent<GraphicRaycaster>();

        _group = canvasGo.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.interactable = false;
        _group.blocksRaycasts = true;               // 切换期间把所有点击吃掉

        _canvasRect = (RectTransform)canvasGo.transform;

        // 挡板：全透明但吃点击 —— 黑条还没扫到的地方也点不动
        RectTransform block = NewImage("Blocker", new Color(0f, 0f, 0f, 0f), _canvasRect);
        block.anchorMin = Vector2.zero;
        block.anchorMax = Vector2.one;
        block.offsetMin = Vector2.zero;
        block.offsetMax = Vector2.zero;
        block.GetComponent<Image>().raycastTarget = true;
    }

    void BuildBars()
    {
        float w = _canvasRect.rect.width;
        float h = _canvasRect.rect.height;
        if (w < 2f || h < 2f) { w = Screen.width; h = Screen.height; }   // 画布还没布局好时的兜底

        float baseW = w / BarCount;

        // 等宽黑条，自左向右依次出发：第 0 条先走、最右一条最后走，
        // 于是整屏是从左往右一格一格追黑的，而不是整排一起平移。
        for (int i = 0; i < BarCount; i++)
        {
            RectTransform rt = NewImage("Bar" + i, Color.black, _canvasRect);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);    // 以屏幕左边缘为原点
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(w, 0f);               // 生成在屏幕右侧外

            // 等宽，每条正好占一格；相邻严丝合缝、绝不互相搭接 —— 渐入时两层黑会叠成
            // 一条更深的竖缝，所以宁可相接也不许重叠。最右一条往屏外多探 1 像素兜底。
            rt.sizeDelta = new Vector2(baseW + (i == BarCount - 1 ? 1f : 0f), h);

            _bars.Add(rt);
            _from.Add(w);
            _to.Add(i * baseW);                                     // 落点 = 第 i 格（等宽相接，落满即全黑）
            _delay.Add(i * BarStagger);                             // 从左到右依次出发
        }
    }

    IEnumerator BarsIn()
    {
        float total = BarTime + BarStagger * (BarCount - 1);
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            ApplyBars(t);
            yield return null;
        }
        ApplyBars(total);           // 收到精确终态
    }

    void ApplyBars(float t)
    {
        for (int i = 0; i < _bars.Count; i++)
        {
            float p = Mathf.Clamp01((t - _delay[i]) / BarTime);
            float x = Mathf.Lerp(_from[i], _to[i], EaseInOut(p));
            _bars[i].anchoredPosition = new Vector2(x, 0f);
        }
    }

    // ── 过场预加载 ─────────────────────────────────────────────────────────

    IEnumerator PreloadRoutine(string sceneName)
    {
        if (!SceneResources.TryGetValue(sceneName, out string[] entries) || entries == null || entries.Length == 0)
        {
            _preloadDone = true;
            yield break;
        }

        float start = Time.realtimeSinceStartup;
        int count = 0, steps = 0;

        foreach (string raw in entries)
        {
            if (raw.EndsWith("/"))
            {
                string dir = raw.Substring(0, raw.Length - 1);
                Object[] objs = Resources.LoadAll(dir);     // 目录只能同步枚举——反正全黑，这一下看不见
                foreach (Object o in objs) if (o != null) { _preloaded.Add(o); count++; }
            }
            else
            {
                ResourceRequest req = Resources.LoadAsync(raw);
                while (!req.isDone) yield return null;
                if (req.asset != null) { _preloaded.Add(req.asset); count++; }
            }

            steps++;
            yield return null;                              // 一个条目一帧

            if (Time.realtimeSinceStartup - start > PreloadTimeout)
            {
                Debug.LogWarning($"[SceneTransition] {sceneName} 预加载超时（完成 {steps}/{entries.Length} 项），其余交回场景按需加载");
                break;
            }
        }

        _preloadDone = true;
        Debug.Log($"[SceneTransition] {sceneName} 预加载完成：{count} 个资源，用了 {Time.realtimeSinceStartup - start:F2}s，持有 {_preloaded.Count} 个引用");
    }

    // ── 工具 ───────────────────────────────────────────────────────────────

    void SetAlpha(float a)
    {
        if (_group != null) _group.alpha = a;
    }

    RectTransform NewImage(string name, Color color, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>缓入缓出：两头都不突兀（和入场黑幕同款）。</summary>
    static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }
}
