using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LobbyBgParallax —— 大厅的鼠标视差，2026-09-26。
///
/// 口径照 Assets/_Game/Scripts/UI/MainMenu/MenuLightCurtain.cs：
///   鼠标相对屏幕中心的偏移（-1..1）× parallaxMax（画布尺寸的比例，默认 0.030 ≈ 1920 下的 58px）
///   → 再乘每层自己的 depth → 指数平滑跟随（6 / 秒，帧率无关）。只改 anchoredPosition，不动尺寸。
///
/// **depth 的正负就是方向**：
///   · **正** = 跟着鼠标**同向**（背景 / 星环）；
///   · **负** = **反向**（鼠标往右，这一层往左）—— 四块入口板走的就是这条，做出「反向陀螺仪」。
///
/// 层与 depth（2026-09-26 九次定）：
///   Far 0.10（背景底，几乎不跟手）/ Ring 0.85（星环）/ Near 0.30（背景浮尘）；
///   Entry_Battle / Entry_Cards / Entry_BottomRow **-0.30** —— 反向，幅度与 Near 背景一层相当。
///   **2026-09-26 十次定**：parallaxMax 0.030 → **0.012**（「只能看到动一点点即可」），折算到 1920×1080 变成
///   Far ≈ 2.3px / Near ≈ 6.9px / Ring ≈ 19.6px / 入口板 ≈ 6.9px（反向）—— 比例关系没动，只是整体收细。
///
/// 挂载：LobbyUI_v1/Bg_v2（由 Assets/_Game/Editor/LobbyBgV2Builder.cs 生成 + 接入口板）。
/// 入口板那三层存的是 **resolveName**（名字）而不是硬引用 —— 重新跑「生成大厅 UI v1」把节点重建之后，
/// OnEnable 会按名字找回来，不会变成一串空引用。
///
/// 只在 Play 模式动；场景里静止的摆位就是设计稿的位置。
/// </summary>
public class LobbyBgParallax : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        [Tooltip("要驱动的 RectTransform")]
        public RectTransform rect;

        [Tooltip("rect 掉了（比如重新跑了「生成大厅 UI v1」）就按这个名字在场景里找回来")]
        public string resolveName;

        [Tooltip("位移倍率：正 = 跟鼠标同向，负 = 反向（陀螺仪）")]
        [Range(-2f, 2f)] public float depth = 0.25f;
    }

    [Tooltip("depth = 1 的层最大位移（画布尺寸的比例）。开始界面那套光幕是 0.030，大厅 2026-09-26 十次定收到 0.012 —— 用户「幅度更低，只能看到动一点点即可」")]
    public float parallaxMax = 0.012f;

    [Tooltip("跟随速度：越大越跟手（指数平滑，帧率无关）")]
    public float smooth = 6f;

    public List<Layer> layers = new List<Layer>();

    Vector2 _look;          // 当前视差（-1..1，已平滑）
    Vector2[] _base;        // 每层的基准 anchoredPosition
    bool _ready;

    void OnEnable()
    {
        ResolveLayers();
        _base = new Vector2[layers.Count];
        for (int i = 0; i < layers.Count; i++)
            if (layers[i] != null && layers[i].rect != null)
                _base[i] = layers[i].rect.anchoredPosition;
        _ready = true;
    }

    void OnDisable()
    {
        if (!_ready || _base == null) return;
        for (int i = 0; i < layers.Count; i++)
            if (layers[i] != null && layers[i].rect != null)
                layers[i].rect.anchoredPosition = _base[i];
        _look = Vector2.zero;
    }

    /// <summary>rect 为空的层，按 resolveName 在场景里找回自己（UI 节点被重建之后用）。</summary>
    void ResolveLayers()
    {
        if (layers == null) return;
        for (int i = 0; i < layers.Count; i++)
        {
            Layer L = layers[i];
            if (L == null || L.rect != null || string.IsNullOrEmpty(L.resolveName)) continue;
            GameObject go = GameObject.Find(L.resolveName);
            if (go != null) L.rect = go.transform as RectTransform;
        }
    }

    void Update()
    {
        if (!_ready || _base == null) return;
        var self = (RectTransform)transform;
        Vector2 size = self.rect.size;
        if (size.x < 2f || size.y < 2f) return;

        Vector3 mp = Input.mousePosition;
        var target = new Vector2(
            Mathf.Clamp(mp.x / Mathf.Max(1f, Screen.width) * 2f - 1f, -1f, 1f),
            Mathf.Clamp(mp.y / Mathf.Max(1f, Screen.height) * 2f - 1f, -1f, 1f));
        float k = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
        _look = Vector2.Lerp(_look, target, k);

        for (int i = 0; i < layers.Count; i++)
        {
            Layer L = layers[i];
            if (L == null || L.rect == null) continue;
            L.rect.anchoredPosition = _base[i] + new Vector2(
                _look.x * parallaxMax * L.depth * size.x,
                _look.y * parallaxMax * L.depth * size.y);
        }
    }
}