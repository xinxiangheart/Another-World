using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始界面入场：整屏先纯黑停留一下，再由黑渐入；渐入过程中标题先浮现，
/// 随后三项文字从上到下依次「从下方浮上来」。
/// 挂在全屏黑幕那个物体上（overlay 就是它自己那层 Image）。
///
/// 时间轴（秒，均可在 Inspector 调；后面几段都从黑幕停留结束后开始算）：
///   0.00                        全黑静止（blackHold）
///   blackHold                   黑幕开始褪去（overlayFade，缓入缓出）
///   blackHold + titleDelay      标题淡入 + 微微上浮
///   blackHold + itemDelay       第一项淡入，之后每项 +itemStagger
///
/// 初始状态在 Awake 里压成「全黑 + 标题/文字全透明」，
/// 所以场景里那些 alpha 保持 1 就行 —— 脚本没挂上时画面是正常的，不会黑屏。
/// </summary>
[DisallowMultipleComponent]
public class SceneIntro : MonoBehaviour
{
    [Header("黑幕")]
    public Graphic overlay;
    [Tooltip("开场纯黑停留时长（秒）：留 0 就是一进场就开始褪黑")]
    public float blackHold = 0.12f;
    public float overlayFade = 0.33f;

    [Header("标题")]
    public CanvasGroup titleGroup;
    public RectTransform titleRect;
    public float titleDelay = 0.03f;
    public float titleFade = 0.85f;
    [Tooltip("标题从下方浮上来的距离")]
    public float titleRise = 14f;

    [Header("菜单文字（数组顺序 = 从上到下）")]
    public CanvasGroup[] itemGroups;
    public float itemDelay = 0.55f;
    [Tooltip("相邻两项的间隔")]
    public float itemStagger = 0.12f;
    public float itemFade = 0.45f;
    [Tooltip("每一项从下方浮上来的距离")]
    public float itemRise = 30f;

    [Header("其它")]
    [Tooltip("按任意键 / 点鼠标直接跳到结束")]
    public bool skipOnInput = true;
    [Tooltip("这么久之前的输入不算（刚进场的杂散输入不能把入场跳掉）")]
    public float skipGrace = 0.25f;

    /// <summary>入场时间轴是否已走完（其他 UI 靠这个判断什么时候能接管画面）。</summary>
    public bool Finished { get; private set; }

    float _t;
    float _total;
    Vector2 _titleBase;
    Vector2[] _itemBase;

    void Awake()
    {
        if (overlay != null) SetAlpha(overlay, 1f);

        if (titleRect != null) _titleBase = titleRect.anchoredPosition;
        if (titleGroup != null) titleGroup.alpha = 0f;

        int n = itemGroups != null ? itemGroups.Length : 0;
        _itemBase = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            CanvasGroup g = itemGroups[i];
            if (g == null) continue;
            RectTransform rt = g.transform as RectTransform;
            if (rt != null) _itemBase[i] = rt.anchoredPosition;
            g.alpha = 0f;
        }

        _total = blackHold + Mathf.Max(overlayFade, titleDelay + titleFade);
        if (n > 0) _total = Mathf.Max(_total, blackHold + itemDelay + itemStagger * (n - 1) + itemFade);
        _t = 0f;
    }

    void Update()
    {
        bool skip = skipOnInput && _t > skipGrace && (Input.anyKeyDown || Input.GetMouseButtonDown(0));
        // 单帧增量封顶：编辑器进 Play 的第一帧往往很慢，不封顶的话黑幕会一帧就褪完。
        _t = skip ? _total : _t + Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // 黑幕：先纯黑静止 blackHold，再用缓入缓出褪去。
        // 起步不能快：编辑器刚进 Play 的头几帧本身很慢，
        // 用急冲式的 Ease 会让黑幕在看见之前就褪完了。
        if (overlay != null)
            SetAlpha(overlay, 1f - EaseInOut(Mathf.Clamp01((_t - blackHold) / Mathf.Max(0.0001f, overlayFade))));

        Apply(titleGroup, titleRect, _titleBase, blackHold + titleDelay, titleFade, titleRise);

        for (int i = 0; i < _itemBase.Length; i++)
        {
            CanvasGroup g = itemGroups[i];
            if (g == null) continue;
            Apply(g, g.transform as RectTransform, _itemBase[i],
                  blackHold + itemDelay + itemStagger * i, itemFade, itemRise);
        }

        if (_t > _total + 0.05f) { Finished = true; enabled = false; }
    }

    void Apply(CanvasGroup g, RectTransform rt, Vector2 basePos, float delay, float dur, float rise)
    {
        if (g == null) return;
        float p = Mathf.Clamp01((_t - delay) / Mathf.Max(0.0001f, dur));
        float e = Ease(p);
        g.alpha = e;
        if (rt != null) rt.anchoredPosition = new Vector2(basePos.x, basePos.y - rise * (1f - e));
    }

    /// <summary>1 - (1-t)^3：起步快、收尾慢（标题与文字用）。</summary>
    static float Ease(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }

    /// <summary>缓入缓出：黑幕褪去用，两头都不突兀。</summary>
    static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
