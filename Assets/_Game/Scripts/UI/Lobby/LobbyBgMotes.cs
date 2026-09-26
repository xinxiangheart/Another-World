using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LobbyBgMotes —— 大厅背景的光点，2026-09-26（八次定：用户「将光点分为前景和后景，
/// 前景的光点会缓慢地上浮，后景的光点会缓慢地闪烁」）。
///
/// 两种模式，挂在**不同的视差层**下，自己就跟着那一层走（不用额外接线）：
///   · Twinkle（后景，挂 Bg_v2/Far）：**钉在原地缓慢明暗** —— 每颗自己的周期 / 相位都随机，走正弦，
///     所以是连续的呼吸，不会「亮一下停一会儿」，也不会整片一起眨眼；
///   · Rise（前景，挂 Bg_v2/Near）：**缓慢上浮**（带一点横向摆动），飘出上沿后从下沿外重生 ——
///     重生点在可视区之外，所以不会看到「冒出来」。
///
/// 只在 Play 生效：OnEnable 用代码建点，OnDisable 全部销毁 —— **一个点都不存进场景**，Hierarchy 永远干净。
/// 贴图也是代码里的柔边圆（做法照 MenuLightCurtain），所以这个组件不需要任何资源引用。
///
/// 口径：容器是「全拉伸 + localScale 1.08」的那两层，局部坐标 = 设计屏 1920x1080，y 向上。
/// 可视区在局部坐标里只有中间 1778x1000（因为容器比画布大 8%），所以 Twinkle 撒点带外扩、
/// Rise 的上下重生点都落在可视区外（见 WrapY / SpawnY）。
/// </summary>
public class LobbyBgMotes : MonoBehaviour
{
    public enum Mode { Twinkle, Rise }

    [Tooltip("Twinkle = 后景原地闪烁；Rise = 前景缓慢上浮")]
    public Mode mode = Mode.Twinkle;

    [Tooltip("点数量")]
    public int count = 100;

    [Tooltip("随机种子（改一下就是换一批点）")]
    public int seed = 20260926;

    [Header("外观")]
    [Tooltip("直径（设计屏 px；容器有 1.08 缩放，实际会再大 8%）")]
    public Vector2 sizeRange = new Vector2(2.0f, 4.6f);

    [Tooltip("暗档 / 亮档的透明度倍率（每颗自己还有一个 0.5~1 的基准亮度）。默认按烘焙星点校准：远景亮档星芯 92/255、最亮档 128/255，所以峰值压在 0.7 上下，别比星星亮出一个数量级")]
    public Vector2 alphaRange = new Vector2(0.15f, 0.70f);

    public Color tintCold = new Color(0.86f, 0.92f, 1.00f, 1f);
    public Color tintGold = new Color(0.91f, 0.82f, 0.54f, 1f);

    [Range(0f, 1f), Tooltip("其中多大比例是金点（其余冷白）")]
    public float goldShare = 0.25f;

    [Header("Twinkle —— 后景：原地缓慢明暗")]
    [Tooltip("一颗自己「暗到底再亮回来」要几秒（每颗在区间里随机，避免同步眨眼）")]
    public Vector2 periodRange = new Vector2(3.5f, 8f);

    [Header("Rise —— 前景：缓慢上浮")]
    [Tooltip("上浮速度（画布高 / 秒）；0.02 约 54 秒穿过一屏")]
    public Vector2 speedRange = new Vector2(0.010f, 0.028f);

    [Tooltip("横向摆动幅度（画布宽的比例）")]
    public float swayAmp = 0.006f;

    [Tooltip("横向摆动的周期（秒）")]
    public Vector2 swayPeriod = new Vector2(6f, 14f);

    [Tooltip("重生点（画布高的比例，负数 = 在可视区下沿之外）")]
    public float spawnY = -0.05f;

    [Tooltip("出屏线（画布高的比例，>1 = 在可视区上沿之外）")]
    public float wrapY = 1.04f;

    class Mote
    {
        public RectTransform rt; public Image img;
        public float x, y, size, baseA, period, phase, speed, swayA, swayF, swayP;
    }

    readonly List<Mote> _motes = new List<Mote>();
    Sprite _dot;
    System.Random _rng;
    float _t;

    void OnEnable() { Build(); }
    void OnDisable() { Clear(); }

    void Clear()
    {
        for (int i = 0; i < _motes.Count; i++)
            if (_motes[i].rt != null) Destroy(_motes[i].rt.gameObject);
        _motes.Clear();
        if (_dot != null)
        {
            if (_dot.texture != null) Destroy(_dot.texture);
            Destroy(_dot);
            _dot = null;
        }
    }

    void Build()
    {
        Clear();
        if (count <= 0) return;
        _rng = new System.Random(seed);
        _dot = MakeDotSprite(32);
        _t = 0f;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("mote", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.sprite = _dot;
            img.raycastTarget = false;

            var m = new Mote { rt = rt, img = img };
            m.size = Rand(sizeRange.x, sizeRange.y);
            m.baseA = F(0.5f, 1.0f);
            // Twinkle 撒点带外扩（容器比画布大 8%，可视区只占中间一段）
            m.x = F(-0.04f, 1.04f);
            m.y = F(-0.05f, 1.05f);
            m.period = Rand(periodRange.x, periodRange.y);
            m.phase = F(0f, 1f);
            m.speed = Rand(speedRange.x, speedRange.y);
            m.swayA = swayAmp * F(0.4f, 1.2f);
            m.swayF = 2f * Mathf.PI / Rand(swayPeriod.x, swayPeriod.y);
            m.swayP = F(0f, 6.2831853f);
            if (mode == Mode.Rise) m.y = F(spawnY, wrapY);

            Color c = (_rng.NextDouble() < goldShare) ? tintGold : tintCold;
            rt.sizeDelta = new Vector2(m.size, m.size);
            img.color = new Color(c.r, c.g, c.b, 0f);
            _motes.Add(m);
        }
        Apply();     // 第一帧先摆好，别从原点闪一下
    }

    /// <summary>按当前 _t 摆一遍位置 + 刷一遍颜色（Twinkle 只改颜色，Rise 只改位置）。</summary>
    void Apply()
    {
        var self = (RectTransform)transform;
        float W = self.rect.width, H = self.rect.height;
        if (W < 2f || H < 2f) return;

        for (int i = 0; i < _motes.Count; i++)
        {
            var m = _motes[i];
            if (mode == Mode.Twinkle)
            {
                float w = 0.5f - 0.5f * Mathf.Cos((_t / Mathf.Max(0.01f, m.period) + m.phase) * Mathf.PI * 2f);
                m.rt.anchoredPosition = new Vector2(m.x * W, m.y * H);
                SetAlpha(m, m.baseA * Mathf.Lerp(alphaRange.x, alphaRange.y, w));
            }
            else
            {
                float sway = Mathf.Sin(_t * m.swayF + m.swayP) * m.swayA;
                m.rt.anchoredPosition = new Vector2(Mathf.Clamp01(m.x + sway) * W, m.y * H);
                SetAlpha(m, m.baseA);
            }
        }
    }

    void Update()
    {
        if (_motes.Count == 0) return;
        var self = (RectTransform)transform;
        float W = self.rect.width, H = self.rect.height;
        if (W < 2f || H < 2f) return;

        float dt = Time.unscaledDeltaTime;
        _t += dt;

        for (int i = 0; i < _motes.Count; i++)
        {
            var m = _motes[i];
            if (mode == Mode.Twinkle)
            {
                float w = 0.5f - 0.5f * Mathf.Cos((_t / Mathf.Max(0.01f, m.period) + m.phase) * Mathf.PI * 2f);
                SetAlpha(m, m.baseA * Mathf.Lerp(alphaRange.x, alphaRange.y, w));
            }
            else
            {
                m.y += m.speed * dt;
                if (m.y > wrapY) { m.y = spawnY; m.x = F(-0.04f, 1.04f); }   // 重生点在可视区之外，看不到冒出来
                float sway = Mathf.Sin(_t * m.swayF + m.swayP) * m.swayA;
                m.rt.anchoredPosition = new Vector2(Mathf.Clamp01(m.x + sway) * W, m.y * H);
            }
        }
    }

    void SetAlpha(Mote m, float a)
    {
        Color c = m.img.color;
        c.a = Mathf.Clamp01(a);
        m.img.color = c;
    }

    float Rand(float lo, float hi) { return lo + (float)_rng.NextDouble() * (hi - lo); }
    float F(float lo, float hi) { return Rand(lo, hi); }

    /// <summary>柔边圆：a = (1 - d)^power，白色 RGB，染色靠 Image.color（做法同 MenuLightCurtain）。</summary>
    static Sprite MakeDotSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f, r = size * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r);
                float a = Mathf.Pow(1f - d, 2.2f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}