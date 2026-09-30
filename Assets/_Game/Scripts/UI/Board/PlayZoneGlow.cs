using UnityEngine;
using UnityEngine.UI;

/// <summary>打出区提示 —— 拖牌进「距屏幕底部 1/3 以上」时，屏幕四边浮起一圈极淡的荧光：
/// **绿 = 这张牌此刻能打出去，红 = 打不出去**（判据 = CardDrag.CanPlayNow，与松手时那道闸门同源）。
///
/// 纯运行时构建（与 PickDrawUI 同做法）：挂在与手牌同一块主 Canvas 下，需要时才建。
/// **全程不吃射线**（每张图 raycastTarget=false、CanvasGroup.blocksRaycasts=false）—— 它只是提示，
/// 不能把正在拖的那张牌的拖拽事件吃掉。
///
/// 层级：显示时把自己放到「倒数第二」个兄弟。CardDrag.OnBeginDrag 会把被拖的牌
/// SetAsLastSibling（永远最后一个），所以光晕压住除它以外的一切 —— 牌不会被自己的提示盖住。
///
/// 画法：四条边带（顶 / 底 / 左 / 右各一张按边缘衰减的渐变图，拼成一圈环），
/// 只有一圈呼吸（明暗缓动），**没有任何滚动的光斑 / 游走的圆**。
/// 色相取场上红 / 绿提示那一族，但把亮度与饱和度压下来 —— 与「深蓝黑石面 + 金细线」那套同调，
/// 是「屏幕边缘泛了一层色」而不是「贴了一圈霓虹」。
/// </summary>
public class PlayZoneGlow : MonoBehaviour
{
    public static PlayZoneGlow Instance { get; private set; }

    [Header("颜色（场上红/绿提示的色相，压过饱和度与亮度）")]
    public Color canPlayColor = new Color(0.38f, 0.66f, 0.44f, 1f);    // 灰玉绿
    public Color cannotPlayColor = new Color(0.72f, 0.30f, 0.30f, 1f); // 生命红 #B64848 同值

    [Header("边带")]
    [Tooltip("边带厚度 = 画布高 × 该比例（只做一圈窄边，不往画面里糊）")] public float bandHeightRatio = 0.105f;
    [Tooltip("呼吸的下限 / 上限（整圈的透明度区间 —— 要「淡」，宁可再往下调）")]
    public float bandAlphaMin = 0.08f;
    public float bandAlphaMax = 0.18f;
    [Tooltip("呼吸周期（秒）")] public float pulsePeriod = 2.0f;

    [Header("淡入淡出")]
    [Tooltip("进出打出区的淡入淡出时长（秒）—— 要「浮现」，不是「弹出」")] public float fadeTime = 0.22f;

    // ── 运行时 ────────────────────────────────────────────────────────────
    RectTransform _root;
    CanvasGroup _group;
    readonly Image[] _bands = new Image[4];      // 0=上 1=下 2=左 3=右
    readonly Texture2D[] _owned = new Texture2D[4];

    bool _built;
    bool _shown;
    float _alpha;
    Color _color = Color.white;
    float _laidOutForH;

    // ══════════════════════════════════════════════════════════════════════
    // 对外入口
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>拖拽中「进 / 出打出区」时调；canPlay = 这张牌此刻能不能打出去。
    /// visible=false 时 canPlay 忽略。找不到主 Canvas 时静默跳过（绝不打断拖拽）。</summary>
    public static void SetHint(bool visible, bool canPlay)
    {
        if (visible && Instance == null) Ensure();
        if (Instance == null) return;
        Instance.Apply(visible, canPlay);
    }

    /// <summary>拖拽结束时收尾（回手 / 打出 / 取消都要调，别把光晕留在屏幕上）。</summary>
    public static void ForceHide()
    {
        if (Instance != null) Instance.Apply(false, false);
    }

    static void Ensure()
    {
        var canvasGo = PlayRevealManager.FindMainCanvas();
        if (canvasGo == null) return;
        var go = new GameObject("PlayZoneGlow", typeof(RectTransform), typeof(PlayZoneGlow));
        go.transform.SetParent(canvasGo.transform, false);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        _alpha = 0f;
        _group.alpha = 0f;
        enabled = false;   // 没显示时不跑 Update
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        for (int i = 0; i < _owned.Length; i++)
        {
            if (_owned[i] != null) Destroy(_owned[i]);
            _owned[i] = null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 构建
    // ══════════════════════════════════════════════════════════════════════

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

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        // 四条边带：贴屏幕四边，只沿「厚度」方向做衰减
        _bands[0] = MakeBand("BandTop",    MakeBandSprite(vertical: true,  flip: true),  new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        _bands[1] = MakeBand("BandBottom", MakeBandSprite(vertical: true,  flip: false), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        _bands[2] = MakeBand("BandLeft",   MakeBandSprite(vertical: false, flip: false), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        _bands[3] = MakeBand("BandRight",  MakeBandSprite(vertical: false, flip: true),  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
    }

    Image MakeBand(string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 显示 / 隐藏
    // ══════════════════════════════════════════════════════════════════════

    void Apply(bool visible, bool canPlay)
    {
        if (visible)
        {
            if (!_shown)
            {
                _shown = true;
                // 换色直接落到目标色（不在两色之间慢慢染 —— 红绿之间必然会穿过一段脏黄）
                _color = canPlay ? canPlayColor : cannotPlayColor;
                LiftAboveDraggedCard();
            }
            enabled = true;
        }
        else
        {
            _shown = false;
        }
    }

    /// <summary>显示时插到倒数第二个兄弟：被拖的牌是最后一个，光晕压住除它以外的一切。</summary>
    void LiftAboveDraggedCard()
    {
        var p = transform.parent;
        if (p == null) return;
        int want = Mathf.Max(0, p.childCount - 2);
        if (transform.GetSiblingIndex() != want) transform.SetSiblingIndex(want);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 每帧
    // ══════════════════════════════════════════════════════════════════════

    void Update()
    {
        if (!_built) Build();

        float target = _shown ? 1f : 0f;
        float step = fadeTime > 0.001f ? Time.unscaledDeltaTime / fadeTime : 1f;
        _alpha = Mathf.MoveTowards(_alpha, target, step);

        if (_alpha <= 0f && !_shown)
        {
            _group.alpha = 0f;
            enabled = false;   // 收完了就停 —— 不占每帧
            return;
        }
        _group.alpha = _alpha;

        LayoutIfResized();

        // 整圈呼吸：只在 bandAlphaMin / bandAlphaMax 之间缓动，再淡也不会忽明忽暗
        float pulse = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / Mathf.Max(0.05f, pulsePeriod)));
        Color col = new Color(_color.r, _color.g, _color.b, Mathf.Lerp(bandAlphaMin, bandAlphaMax, pulse));
        for (int i = 0; i < _bands.Length; i++)
            if (_bands[i] != null) _bands[i].color = col;
    }

    void LayoutIfResized()
    {
        float h = _root.rect.height;
        if (Mathf.Approximately(h, _laidOutForH)) return;
        _laidOutForH = h;

        float thick = Mathf.Max(8f, h * bandHeightRatio);
        _bands[0].rectTransform.sizeDelta = new Vector2(0f, thick);
        _bands[1].rectTransform.sizeDelta = new Vector2(0f, thick);
        _bands[2].rectTransform.sizeDelta = new Vector2(thick, 0f);
        _bands[3].rectTransform.sizeDelta = new Vector2(thick, 0f);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 贴图（运行时烘的小图，跟组件一起销毁）
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>一条从「亮边」向内衰减到 0 的渐变带。vertical=true 时沿 Y 衰减。</summary>
    Sprite MakeBandSprite(bool vertical, bool flip)
    {
        const int N = 64;
        var tex = NewTex(N, N);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float t = (vertical ? y : x) / (float)(N - 1);   // 0 = 亮边，1 = 内侧
                if (flip) t = 1f - t;
                // 幂次偏大 + 幂底缓：亮边也不是一条硬线，往里很快收掉 —— 一圈「泛色」而不是「描边」
                float a = Mathf.Pow(1f - t, 2.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return ToSprite(tex);
    }

    Texture2D NewTex(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int i = 0; i < _owned.Length; i++)
        {
            if (_owned[i] == null) { _owned[i] = tex; break; }
        }
        return tex;
    }

    Sprite ToSprite(Texture2D tex)
        => Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
}