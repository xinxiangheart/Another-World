using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LobbyBgParallax —— 大厅背景（Bg_v2）的鼠标视差，2026-09-26。
///
/// 口径照 Assets/_Game/Scripts/UI/MainMenu/MenuLightCurtain.cs：
///   鼠标相对屏幕中心的偏移（-1..1）× parallaxMax（画布尺寸的比例，默认 0.030 ≈ 1920 下的 58px）
///   → 再乘每层自己的 depth（0 = 钉死，1 = 最近、位移最大）→ 指数平滑跟随（6 / 秒，帧率无关）。
///
/// 只改 anchoredPosition，不动尺寸。所以**每层贴图必须自带溢出**：
/// Bg_Far / Bg_Near 是按屏的 1.08 倍出的图（2074×1166），四边各留 4% ≈ 77px，
/// 而 depth 1.0 的层最多走 57.6px —— 够，不会露出画布边缘。
///
/// 只在 Play 模式动；场景里静止的摆位就是设计稿的位置（设计师照 LobbyBgV2.ps1 的屏坐标摆即可）。
/// 挂载：LobbyUI_v1/Bg_v2（由 Assets/_Game/Editor/LobbyBgV2Builder.cs 生成）。
/// 层与 depth（2026-09-26 七次定：用户「整体背景幅度较小、圆环较大，做一个区分」）：
///   Far 0.10（背景底，几乎不跟手）/ Near 0.30（背景浮尘，轻微）/ Ring 0.85（星环，最明显）。
///   按 parallaxMax = 0.030 折算到 1920×1080：Far ≈ 5.8px、Near ≈ 17.3px、Ring ≈ 49.0px（横），
///   环与背景底的位移比 8.4 : 1 —— 环右边缘 956 + 49 = 1005 仍在 UI 热点最左 1099 之外。
/// </summary>
public class LobbyBgParallax : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public RectTransform rect;
        [Range(0f, 2f)] public float depth = 0.25f;      // 相对最近层的位移倍率
    }

    [Tooltip("depth = 1 的层最大位移（画布尺寸的比例）。照 MenuLightCurtain 的 0.030")]
    public float parallaxMax = 0.030f;

    [Tooltip("跟随速度：越大越跟手（指数平滑，帧率无关）")]
    public float smooth = 6f;

    public List<Layer> layers = new List<Layer>();

    Vector2 _look;          // 当前视差（-1..1，已平滑）
    Vector2[] _base;        // 每层的基准 anchoredPosition
    bool _ready;

    void OnEnable()
    {
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