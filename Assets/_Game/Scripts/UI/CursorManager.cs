using UnityEngine;

/// <summary>
/// CursorManager — 用自制贴图替换系统鼠标指针（2026-09-18）。
///
/// 贴图：Resources/UI/Cursor/CursorNormal（常态）、CursorPressed（按住鼠标时压暗一档）。
/// 两张都是 32×32，细黑描边 + 上下两块硬边平涂面（轻微立体感），热点取箭头尖。
///
/// 自动创建：场景里不用挂任何东西 —— RuntimeInitializeOnLoadMethod 后自建 DontDestroyOnLoad 单例，
/// 与 AudioManager / GPUOptimizer 同一套路。
///
/// 注：Cursor.SetCursor 只吃 RGBA32 / ARGB32 / RGB24 / Alpha8，编辑器外贴图可能被压成 DXT，
/// 所以这里统一复制一份 RGBA32 贴图再交给系统；源贴图需开 Read/Write（四张 .meta 已设 isReadable: 1）。
/// 觉得指针偏小就把下面两条路径换成 "…CursorNormal_64" / "…CursorPressed_64"（热点同时改成 (5,5)）。
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    /// <summary>true = 64×64（默认，三块面的细节留得住）；false = 32×32（与原版系统指针同尺寸）。</summary>
    const bool UseLargeCursor = true;

    const string NormalPath  = UseLargeCursor ? "UI/Cursor/CursorNormal_64"  : "UI/Cursor/CursorNormal";
    const string PressedPath = UseLargeCursor ? "UI/Cursor/CursorPressed_64" : "UI/Cursor/CursorPressed";

    /// <summary>热点 = 箭头尖在贴图里的像素坐标（原点在左上角）：64px 版是 (5,5)，32px 版是 (3,3)。</summary>
    static readonly Vector2 Hotspot = new Vector2(UseLargeCursor ? 5f : 3f, UseLargeCursor ? 5f : 3f);

    Texture2D _normal;
    Texture2D _pressed;
    bool _isPressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("CursorManager");
            go.AddComponent<CursorManager>();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _normal = LoadCursorTexture(NormalPath);
        _pressed = LoadCursorTexture(PressedPath);
        if (_pressed == null) _pressed = _normal;

        if (_normal == null)
        {
            Debug.LogWarning($"[CursorManager] 未找到指针贴图：Resources/{NormalPath}");
            return;
        }
        Apply(false);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);   // 还原系统默认指针
    }

    void Update()
    {
        if (_normal == null) return;
        bool pressed = Input.GetMouseButton(0) || Input.GetMouseButton(1);
        if (pressed != _isPressed) Apply(pressed);
    }

    /// <summary>切到别的窗口再切回来时按住状态会残留，强制回到常态。</summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _isPressed) Apply(false);
    }

    void Apply(bool pressed)
    {
        var tex = pressed && _pressed != null ? _pressed : _normal;
        if (tex == null) return;
        _isPressed = pressed;
        Cursor.SetCursor(tex, Hotspot, CursorMode.Auto);
    }

    /// <summary>加载 Resources 里的指针贴图，并统一复制成 RGBA32（见类注释）。</summary>
    static Texture2D LoadCursorTexture(string path)
    {
        Texture2D src = Resources.Load<Texture2D>(path);
        if (src == null)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) src = sprite.texture;
        }
        if (src == null) return null;

        var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        copy.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            copy.SetPixels(src.GetPixels());
            copy.Apply();
        }
        catch (System.Exception)
        {
            Destroy(copy);      // 源贴图没开 Read/Write 时退回原贴图
            return src;
        }
        return copy;
    }
}
