using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Game场景资源预加载器——在Lobby场景中提前异步加载重资源，
/// 缓存到DontDestroyOnLoad单例中，Game场景启动时优先读缓存。
///
/// 触发时机: JoinGamePanel.Open() → StartPreload()
/// 进度查询: Preloader.Instance.Progress (0~1) / IsDone
/// 场景加载: Preloader.Instance.LoadGameScene() 替代 SceneManager.LoadScene("Game")
/// </summary>
public class Preloader : MonoBehaviour
{
    public static Preloader Instance { get; private set; }

    // 资源缓存
    readonly Dictionary<string, Object> _cache = new();
    readonly List<ResourceRequest> _loadOps = new();
    int _completedOps;
    int _totalOps => _loadOps.Count;

    public float Progress => _totalOps > 0 ? (float)_completedOps / _totalOps : 0f;
    public bool IsDone => _completedOps >= _totalOps && _totalOps > 0;

    AsyncOperation _sceneLoadOp;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>从缓存读取资源，未命中时fallback到同步Resources.Load。</summary>
    public T Get<T>(string path) where T : Object
    {
        if (_cache.TryGetValue(path, out var obj)) return obj as T;
        var loaded = Resources.Load<T>(path);
        if (loaded != null) _cache[path] = loaded;
        return loaded;
    }

    /// <summary>启动预加载——异步加载Game场景的重资源，可在Lobby场景提前调用。</summary>
    public void StartPreload()
    {
        if (_loadOps.Count > 0) return; // already started

        // ── 卡牌数据 ──────────────────────────────────────────
        // ChosenOneData ScriptableObjects（03501~03513 等）
        PreloadDir<CardData>("ChosenOneData");

        // ── 通用 UI Prefab ────────────────────────────────────
        Preload<GameObject>("UI/SpellCard2D");
        Preload<GameObject>("UI/Card2D");
        Preload<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF");

        // ── 卡牌 3D Prefab（按卡牌模板列表预加载）───
        // 启动时不需要全部 ~230 张牌，只预加载最常用的 Token 和英雄 Prefab
        PreloadTokenPrefabs();

        Debug.Log($"[Preloader] 启动预加载 {_totalOps} 项资源...");
        StartCoroutine(WaitForPreload());
    }

    /// <summary>预加载单个资源路径。</summary>
    void Preload<T>(string path) where T : Object
    {
        var op = Resources.LoadAsync<T>(path);
        op.completed += _ => _completedOps++;
        _loadOps.Add(op);
    }

    /// <summary>预加载整个目录下的 ScriptableObject。</summary>
    void PreloadDir<T>(string dir) where T : Object
    {
        var all = Resources.LoadAll<T>(dir);
        foreach (var asset in all)
        {
            string key = $"{dir}/{asset.name}";
            _cache[key] = asset;
        }
        _loadOps.Add(new CompletedOp());
        _completedOps++;
    }

    /// <summary>预加载常用 Token/召唤物 Prefab。</summary>
    void PreloadTokenPrefabs()
    {
        string[] commonTokens = {
            "Prefabs/Cards/03001",  // 杂兵
            "Prefabs/Cards/03002",  // 幽灵
            "Prefabs/Cards/03004",  // 士兵
            "Prefabs/Cards/03007",  // 影子
            "Prefabs/Cards/03010",  // 小恶
            "Prefabs/Cards/03027",  // 中枢
        };
        foreach (var t in commonTokens)
        {
            var op = Resources.LoadAsync<GameObject>(t);
            op.completed += req =>
            {
                if (req.asset != null) _cache[t] = req.asset;
                _completedOps++;
            };
            _loadOps.Add(op);
        }
    }

    IEnumerator WaitForPreload()
    {
        while (!IsDone) yield return null;
        Debug.Log("[Preloader] 资源预加载完成");
    }

    /// <summary>异步加载Game场景——先异步加载场景（不激活），资源就绪后激活。</summary>
    public void LoadGameScene()
    {
        StartCoroutine(LoadGameSceneRoutine());
    }

    IEnumerator LoadGameSceneRoutine()
    {
        // 1. 异步加载场景（不激活）——场景内Awake/Start不会执行
        _sceneLoadOp = SceneManager.LoadSceneAsync("Game");
        if (_sceneLoadOp != null)
            _sceneLoadOp.allowSceneActivation = false;

        // 2. 等待资源预加载完成
        while (!IsDone)
        {
            yield return null;
        }

        // 3. 等待场景加载到 90%（此时所有资源已加载，只差 activation）
        if (_sceneLoadOp != null)
        {
            while (_sceneLoadOp.progress < 0.9f)
                yield return null;
        }

        // 4. 激活场景
        if (_sceneLoadOp != null)
            _sceneLoadOp.allowSceneActivation = true;
    }

    /// <summary>预加载完成 + 场景已激活的总进度（供UI显示）。</summary>
    public float TotalProgress
    {
        get
        {
            float resProg = Progress;
            float sceProg = _sceneLoadOp != null ? Mathf.Clamp01(_sceneLoadOp.progress / 0.9f) : 0f;
            // 资源占 40%，场景占 60%
            return resProg * 0.4f + sceProg * 0.6f;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>已完成的假请求——用于同步加载的目录型预加载占位。</summary>
    class CompletedOp : AsyncOperation
    {
        public override bool isDone => true;
        public override float progress => 1f;
        public override int priority { get; set; }
        public override bool allowSceneActivation { get; set; }
    }
}
