using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MenuUpdateNotice — 开始界面右上角的「有新版本」提示窗，2026-09-19。
///
/// 流程：UpdateManager 检测到版本过期（SetOutdated(true)）后，
/// 等入场时间轴走完 postIntroDelay 秒，从右侧滑入小窗口，
/// 停留 holdTime 秒后淡出；点它打开下载页。
/// 点被版本门槛灰掉的「开始游戏」也会把它叫出来
/// （TextMenuButton ⇒ FlashIfOutdated；已经弹着就只刷新住留时间）。
///
/// 面板是场景里的真实物体（边框 + 底色 + 文字三层），
/// 位置 / 大小直接在 Inspector 里调；圆角贴图由脚本运行时生成，
/// 不依赖任何美术资源。回到开始界面会重新走一遍流程（场景重载）。
/// </summary>
[DisallowMultipleComponent]
public class MenuUpdateNotice : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
    public Color borderHoverColor = new Color32(0xFF, 0xD8, 0x88, 0xFF);
    public Color fillColor = new Color32(0x0E, 0x14, 0x20, 0xE6);
    [Tooltip("鼠标移上去的变色速度")]
    public float colorSpeed = 14f;

    [Header("链接")]
    public string url = "https://github.com/xinxiangheart/Another-World/releases";

    [Header("音效")]
    [Tooltip("鼠标移上来一响（和菜单项同一个音）")]
    public float hoverVolume = 0.45f;
    [Tooltip("点击音量")]
    public float clickVolume = 0.8f;

    enum State { Hidden, Sliding, Holding, Fading }

    static MenuUpdateNotice _instance;

    State _state = State.Hidden;
    bool _outdated;
    bool _wanted;          // 想弹（还在等时间轴 / 延迟）
    bool _gateDone;        // 入场时间轴已走完
    bool _hover;
    float _gate;
    float _t;
    float _restX;
    Sprite _sprite;

    // ── 对外 ──────────────────────────────────────────────────────────────

    /// <summary>版本过期了就自动弹；已是最新就收起来。</summary>
    public void SetOutdated(bool outdated)
    {
        _outdated = outdated;
        if (outdated) Flash();
    }

    /// <summary>再弹一次：隐着就重新滑入，已经弹着就只刷新住留时间。</summary>
    public void Flash()
    {
        if (!_outdated) return;
        switch (_state)
        {
            case State.Hidden:
                _wanted = true;
                break;
            case State.Sliding:
                break;
            case State.Holding:
                _t = 0f;                                  // 刷新存在时间
                break;
            case State.Fading:
                _state = State.Holding;                   // 淡出到一半又点了 ⇒ 回到停留
                _t = 0f;
                SetVisible(true);
                SetAlpha(1f);
                break;
        }
    }

    /// <summary>灰掉的「开始游戏」被点时调这个（没这个组件就什么都不做）。</summary>
    public static void FlashIfOutdated()
    {
        if (_instance != null) _instance.Flash();
    }

    // ── 生命周期 ───────────────────────────────────────────────────────────

    void Awake()
    {
        _instance = this;
        _gate = postIntroDelay;

        _sprite = MakeRoundedSprite(cornerRadius);
        if (border != null) { border.sprite = _sprite; border.type = Image.Type.Sliced; border.color = borderColor; }
        if (fill != null) { fill.sprite = _sprite; fill.type = Image.Type.Sliced; fill.color = fillColor; }

        if (panel != null) _restX = panel.anchoredPosition.x;
        Hide();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_sprite != null)
        {
            if (_sprite.texture != null) Destroy(_sprite.texture);
            Destroy(_sprite);
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;      // 主菜单即使暂停也照走

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
                SetAlpha(1f - q);                  // 只淡出，位置不动
                if (q >= 1f) { _state = State.Hidden; Hide(); }
                break;
        }

        // ③ 鼠标悬停：边框亮一点表示可点
        if (border != null)
        {
            Color c = _hover ? borderHoverColor : borderColor;
            float tc = 1f - Mathf.Exp(-colorSpeed * dt);
            border.color = Color.Lerp(border.color, c, tc);
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

    // ── 交互 ────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(url)) return;
        AudioManager.Instance?.Play(SoundEffectType.ButtonClick, clickVolume, Random.Range(0.97f, 1.03f));
        Application.OpenURL(url);
        Debug.Log($"[MenuUpdateNotice] 打开下载页: {url}");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_hover) AudioManager.Instance?.Play(SoundEffectType.ButtonHover, hoverVolume, Random.Range(0.97f, 1.03f));
        _hover = true;
    }
    public void OnPointerExit(PointerEventData eventData) { _hover = false; }

    // ── 圆角贴图（运行时生成，九宫格）───────────────────────────────

    /// <summary>运行时生成圆角九宫格贴图。MenuSteamNotice 也用它，两窗同款圆角。</summary>
    public static Sprite MakeRoundedSprite(float radius)
    {
        float r = Mathf.Clamp(radius, 2f, 60f);
        int size = Mathf.CeilToInt(r) * 2 + 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;      // 默认 Repeat，边缘会出血
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, size, r);
                float a = Mathf.Clamp01(0.5f - d);      // 1 ?????
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();

        int b = Mathf.CeilToInt(r) + 2;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>到圆角矩形边界的有符号距离（< 0 在内部）。</summary>
    static float RoundedDistance(float x, float y, int size, float r)
    {
        float cx = Mathf.Clamp(x, r, size - r);
        float cy = Mathf.Clamp(y, r, size - r);
        return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    }
}
