using UnityEngine;
using System.Collections;

/// <summary>
/// 战斗场景入场时，「匹配」「排位」两张卡从屏幕最右侧滑入到停位。
/// </summary>
/// <remarks>2026-09-27：用户「进入战斗场景时会从最右边滑入到最左边，匹配在最左边，排位在其次，
/// 二者之间以及和边框之间有一定距离，位置在屏幕中心线上，大小较大」。
///
/// 停位由本组件算，不靠手拖 —— 改 <see cref="gap"/> / <see cref="anchorX"/> / <see cref="edgeMargin"/>
/// / <see cref="offsetX"/> / <see cref="offsetY"/> 就能整体挪，卡片各自改宽高也不用重新摆。
/// 贴图是 battle-mode-v1 的 BattleModeCard_{Match,Ranked}.png（900x1260 = 屏幕 300x420）。
/// 文字是卡上的 TMP 子物体，不进贴图（与大厅入口板同一口径）。
///
/// 层级：本物体挂在大厅**战斗子弹窗**（LobbySubPanel 的 Panel_Battle）下、排在 Bg 之后 ——
/// 同一父物体里后面的画在上面，所以标题 / 提示 / 关闭叉仍压在卡之上。
/// **播放时机 = 面板每次打开**：Panel_Battle 存成 active、由 closeOnStart 在 Start 里关掉，
/// 之后每次 Open 都是一次 SetActive(false)->(true)，正好触发本组件的 OnEnable。
/// </remarks>
public class BattleModeCards : MonoBehaviour
{
    [Header("卡（左 → 右）")]
    public RectTransform[] cards;

    [Header("版式 · 参考分辨率 1920x1080 的屏幕 px")]
    [Tooltip("两张卡之间的净距。")]
    public float gap = 60f;
    [Tooltip("整组贴边时，卡与屏幕边缘保留的距离（anchorX 为 0 或 1 时生效）。")]
    public float edgeMargin = 120f;
    [Tooltip("0 = 整组贴左（留 edgeMargin）；0.5 = 整组居中；1 = 整组贴右。")]
    [Range(0f, 1f)] public float anchorX = 0f;   // 用户 2026-09-27：默认贴左
    [Tooltip("整组再叠一个水平偏移（+ 向右）。")]
    public float offsetX = 0f;
    [Tooltip("垂直落点：0 = 屏幕中心线上。")]
    public float offsetY = 0f;

    [Header("入场")]
    [Tooltip("开场延迟（秒）。")]
    public float delay = 0.25f;
    [Tooltip("从屏幕右侧多远处滑入（屏幕 px，自屏幕右沿往外量）。")]
    public float slideFrom = 400f;
    [Tooltip("单张卡的滑入时长（秒）。")]
    public float duration = 0.85f;
    [Tooltip("后一张比前一张晚多久起步（秒）。")]
    public float stagger = 0.14f;
    [Tooltip("缓动曲线。")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("开关")]
    [Tooltip("勾上 = 本物体每次被启用（= 战斗子弹窗每次打开）都播一次滑入。")]
    public bool playOnEnable = true;
    [Tooltip("编辑器里改参数时，把卡摆在停位而不是起始位（方便对位）。")]
    public bool previewAtRest = true;

    RectTransform _rt;
    float _canvasW = 1920f;
    Vector2[] _rest;

    void Awake()
    {
        _rt = transform as RectTransform;
        _canvasW = MeasureCanvasWidth();
        CacheRestPositions();
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
        else if (previewAtRest) ApplyPositions(1f);
        else ApplyPositions(0f);
    }

    /// <summary>重算停位并立刻摆好（不动画）。</summary>
    [ContextMenu("摆到停位")]
    public void SnapToRest()
    {
        _canvasW = MeasureCanvasWidth();
        CacheRestPositions();
        ApplyPositions(1f);   // 1 = 停位（0 是屏幕右外的起始位）
    }

    /// <summary>从屏幕右侧播一次滑入。</summary>
    [ContextMenu("重新播放滑入")]
    public void Play()
    {
        if (!isActiveAndEnabled) { ApplyPositions(1f); return; }
        StopAllCoroutines();
        _canvasW = MeasureCanvasWidth();
        CacheRestPositions();
        ApplyPositions(0f);
        StartCoroutine(SlideIn());
    }

    IEnumerator SlideIn()
    {
        float start = Time.time + Mathf.Max(0f, delay);
        float total = 0f;
        if (cards != null && cards.Length > 0)
            total = Mathf.Max(0f, duration) + Mathf.Max(0f, stagger) * (cards.Length - 1);

        while (true)
        {
            float t = Time.time - start;
            for (int i = 0; i < cards.Length; i++)
            {
                float local = Mathf.Max(0f, duration) <= 0f
                    ? 1f
                    : Mathf.Clamp01((t - Mathf.Max(0f, stagger) * i) / duration);
                Apply(i, ease != null ? ease.Evaluate(local) : local);
            }
            if (t >= total) break;
            yield return null;
        }

        ApplyPositions(1f);
    }

    void CacheRestPositions()
    {
        if (cards == null) { _rest = null; return; }
        _rest = new Vector2[cards.Length];

        int n = 0;
        float totalW = 0f;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            totalW += cards[i].rect.width;
            n++;
        }
        if (n == 0) return;
        totalW += Mathf.Max(0f, gap) * (n - 1);

        float half = _canvasW * 0.5f;
        float margin = Mathf.Min(Mathf.Max(0f, edgeMargin), Mathf.Max(0f, half - totalW * 0.5f));
        float leftCenter = -half + margin + totalW * 0.5f;
        float rightCenter = half - margin - totalW * 0.5f;
        float groupCenter = Mathf.Lerp(leftCenter, rightCenter, Mathf.Clamp01(anchorX)) + offsetX;

        float cursor = groupCenter - totalW * 0.5f;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            float w = cards[i].rect.width;
            _rest[i] = new Vector2(cursor + w * 0.5f, offsetY);
            cursor += w + Mathf.Max(0f, gap);
        }
    }

    /// <summary>整组沿 X 平移：$k = 0 起始位（屏幕右外）、1 停位。</summary>
    void ApplyPositions(float k)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++) Apply(i, k);
    }

    void Apply(int i, float k)
    {
        if (cards == null || i < 0 || i >= cards.Length) return;
        var card = cards[i];
        if (card == null) return;
        if (_rest == null || i >= _rest.Length) return;

        float startX = _canvasW * 0.5f + Mathf.Max(0f, slideFrom) + MaxHalfWidth();
        float dx = startX - _rest[i].x;
        card.anchoredPosition = new Vector2(_rest[i].x + dx * (1f - Mathf.Clamp01(k)), _rest[i].y);
    }

    /// <summary>最宽那张卡的一半宽度 —— 起始位要整张都在屏幕右沿之外。</summary>
    float MaxHalfWidth()
    {
        float max = 0f;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            float h = cards[i].rect.width * 0.5f;
            if (h > max) max = h;
        }
        return max;
    }

    float MeasureCanvasWidth()
    {
        Canvas c = GetComponentInParent<Canvas>();
        if (c != null)
        {
            var crt = c.transform as RectTransform;
            if (crt != null && crt.rect.width > 1f) return crt.rect.width;
        }
        var rt = transform as RectTransform;
        if (rt != null && rt.rect.width > 1f) return rt.rect.width;
        return 1920f;
    }
}