using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MenuLightCurtain — 开局场景（Welcome）的「光幕」动态背景，2026-09-19。
///
/// 不是粒子系统：全部由 UI Image + 运行时生成的柔边贴图组成。整层插在画布的
/// 「背景图之后、其余 UI 之前」，所以标题 / 按钮照旧压在光幕上面；整层挂
/// CanvasGroup.blocksRaycasts = false，绝不挡点击。
///
/// 两层（光幕 + 光斑；深蓝暗底由 WelcomeBG.png 直接提供）：
///   ① 光幕：两张径向渐变（核心亮 + 外圈弱），缓慢呼吸 + 上下微漂
///   ② 光斑：bokeh 光点，三层景深，自下往上飘 + 横向微摆，出屏后回到底部
///
/// 视差：鼠标位置（相对屏幕中心的偏移）驱动整层做小幅度反偏，越近的光斑偏得越多，
/// 光幕偏得最少 —— 观感像手机陀螺仪那种「视角一动，前后景错开」的层次感。
///
/// 位置 / 尺寸 / 速度一律用画布尺寸的比例表示，换分辨率观感一致；
/// 可调参数集中在下面的常量区。只对 TargetScenes 里列出的场景生效。
/// </summary>
public class MenuLightCurtain : MonoBehaviour
{
    // ── 可调参数 ───────────────────────────────────────────────────────────
    static readonly string[] TargetScenes = { "Welcome" };

    const int   MoteCount    = 80;      // 光斑总数（三层轮流分配）
    const float BreathPeriod = 9f;      // 光幕呼吸周期（秒）
    const float CoreCenterY  = 0.62f;   // 光幕核心的高度（0=底 1=顶）
    const float HaloCenterY  = 0.55f;   // 外圈光晕的高度

    const float ParallaxMax    = 0.030f;  // 最近一层光斑的最大位移（画布宽度比例）
    const float ParallaxGlow   = 0.35f;   // 光幕层的位移倍率（相对最近一层光斑）
    const float ParallaxSmooth = 6f;      // 跟随速度：越大越跟手（指数平滑，帧率无关）
    static readonly float[] ParallaxDepth = { 0.30f, 0.65f, 1.00f };  // 三层景深各自的位移倍率

    static readonly Color CoreColor  = new Color(0.93f, 0.97f, 1.00f, 0.40f);
    static readonly Color HaloColor  = new Color(0.55f, 0.74f, 0.98f, 0.18f);
    static readonly Color MoteTint   = new Color(0.88f, 0.94f, 1.00f, 1f);

    // 三层景深：远（小、暗、慢）→ 近（大、亮、快）。尺寸 = 画布高度的比例，速度 = 画布高度/秒
    static readonly float[] SizeMin  = { 0.006f, 0.016f, 0.034f };
    static readonly float[] SizeMax  = { 0.016f, 0.038f, 0.100f };
    static readonly float[] AlphaMin = { 0.05f,  0.08f,  0.06f  };
    static readonly float[] AlphaMax = { 0.11f,  0.17f,  0.17f  };
    static readonly float[] SpeedMin = { 0.006f, 0.012f, 0.020f };
    static readonly float[] SpeedMax = { 0.012f, 0.022f, 0.034f };
    // ──────────────────────────────────────────────────────────────────────

    class Mote
    {
        public RectTransform rt; public Image img;
        public float x, y, size, alpha, speed, swayAmp, swayFreq, phase;
        public float parallax;      // 这一颗的视差倍率（远小近大）
    }

    readonly List<Mote> _motes = new List<Mote>();

    RectTransform _self, _core, _halo;
    Image _coreImg, _haloImg;
    Sprite _radialSprite, _moteSprite;
    Vector2 _look;      // 当前视差偏移（-1..1，已平滑）
    float _t;
    bool _built;

    // ── 自动挂载（场景里不用放任何东西）────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryBuild(SceneManager.GetActiveScene().name);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { TryBuild(scene.name); }

    static void TryBuild(string sceneName)
    {
        if (System.Array.IndexOf(TargetScenes, sceneName) < 0) return;
        if (FindObjectOfType<MenuLightCurtain>() != null) return;      // 已经建过

        Canvas canvas = FindMenuCanvas();
        if (canvas == null) { Debug.LogWarning("[MenuLightCurtain] 没找到画布，光幕未创建"); return; }

        var go = new GameObject("MenuLightCurtain", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetSiblingIndex(InsertIndex(canvas.transform));   // 背景图之后、其余 UI 之前
        go.AddComponent<MenuLightCurtain>().Build();
    }

    /// <summary>主菜单画布 = 面积最大的根 Overlay 画布。</summary>
    static Canvas FindMenuCanvas()
    {
        Canvas best = null; float bestArea = 0f;
        foreach (var c in FindObjectsOfType<Canvas>())
        {
            if (c == null || !c.isActiveAndEnabled || !c.isRootCanvas) continue;
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            var rt = (RectTransform)c.transform;
            float area = rt.rect.width * rt.rect.height;
            if (area >= bestArea) { bestArea = area; best = c; }
        }
        return best;
    }

    /// <summary>
    /// 插到第一张「铺满全屏的 Image」（背景图）后面。
    ///
    /// 画布子物体的顺序就是绘制顺序，最底下那张全屏图就是背景，
    /// 光幕要压在它上面、其余 UI 下面。切勿改成「最后一张」：
    /// 渐入黑幕（SceneIntro 的全屏遮罩）也是全屏 Image 且永远在最上层，
    /// 取最后一张会把光幕垫到黑幕之上、标题与菜单之上。
    /// </summary>
    static int InsertIndex(Transform canvasTr)
    {
        for (int i = 0; i < canvasTr.childCount; i++)
        {
            var rt = canvasTr.GetChild(i) as RectTransform;
            if (rt == null || rt.GetComponent<Image>() == null) continue;
            if (Mathf.Approximately(rt.anchorMin.x, 0f) && Mathf.Approximately(rt.anchorMin.y, 0f) &&
                Mathf.Approximately(rt.anchorMax.x, 1f) && Mathf.Approximately(rt.anchorMax.y, 1f)) return i + 1;
        }
        return 0;
    }

    // ── 搭建 ───────────────────────────────────────────────────────────────

    void Build()
    {
        _self = (RectTransform)transform;
        _self.anchorMin = Vector2.zero;
        _self.anchorMax = Vector2.one;
        _self.offsetMin = Vector2.zero;
        _self.offsetMax = Vector2.zero;

        // 嵌套 Canvas：每帧的重建只发生在这一层，不带着主菜单的文字 / 按钮一起重绘
        var nested = gameObject.AddComponent<Canvas>();
        nested.overrideSorting = false;

        var group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        _radialSprite = MakeRadialSprite(256, 2.0f);
        _moteSprite   = MakeRadialSprite(64, 1.7f);

        _halo = NewImage("GlowHalo", HaloColor, _radialSprite, _self);
        _haloImg = _halo.GetComponent<Image>();
        _core = NewImage("GlowCore", CoreColor, _radialSprite, _self);
        _coreImg = _core.GetComponent<Image>();

        for (int i = 0; i < MoteCount; i++)
        {
            int tier = i % 3;
            var m = new Mote();
            m.size    = Mathf.Lerp(SizeMin[tier],  SizeMax[tier],  Random.value);
            m.alpha   = Mathf.Lerp(AlphaMin[tier], AlphaMax[tier], Random.value);
            m.speed   = Mathf.Lerp(SpeedMin[tier], SpeedMax[tier], Random.value);
            m.swayAmp = Random.Range(0.003f, 0.012f);
            m.swayFreq = Random.Range(0.15f, 0.45f);
            m.phase   = Random.Range(0f, Mathf.PI * 2f);
            m.parallax = ParallaxDepth[tier];
            m.x = Random.value;
            m.y = Random.value;

            var c = MoteTint;
            c.a = m.alpha;
            m.rt = NewImage("Mote" + i, c, _moteSprite, _self);
            m.img = m.rt.GetComponent<Image>();
            _motes.Add(m);
        }

        _built = true;
    }

    // ── 视差输入 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 把鼠标位置换算成 -1..1 的视差目标值（屏幕中心 = 0），再指数平滑。
    /// 用 1 - e^(-k*dt) 而不是固定 Lerp 系数：帧率变化时跟随速度一致。
    /// </summary>
    void UpdateLook(float dt)
    {
        Vector3 mp = Input.mousePosition;
        float sw = Mathf.Max(1f, Screen.width);
        float sh = Mathf.Max(1f, Screen.height);
        Vector2 target = new Vector2(
            Mathf.Clamp(mp.x / sw * 2f - 1f, -1f, 1f),
            Mathf.Clamp(mp.y / sh * 2f - 1f, -1f, 1f));
        _look = Vector2.Lerp(_look, target, 1f - Mathf.Exp(-ParallaxSmooth * dt));
    }

    // ── 每帧动画 ───────────────────────────────────────────────────────────

    void Update()
    {
        if (!_built) return;
        float dt = Time.unscaledDeltaTime;      // 主菜单即使暂停，光幕照走
        _t += dt;
        UpdateLook(dt);

        Vector2 sz = _self.rect.size;
        float w = sz.x, h = sz.y;
        if (w < 2f || h < 2f) return;

        // ① 光幕：呼吸（缩放 + 透明度）+ 上下微漂
        float breath = Mathf.Sin(_t * Mathf.PI * 2f / BreathPeriod);
        float gx = _look.x * ParallaxMax * ParallaxGlow * w;   // 光幕也跟着偏，但幅度最小
        float gy = _look.y * ParallaxMax * ParallaxGlow * h;
        Place(_core, w * 0.5f + gx, h * (CoreCenterY + 0.006f * breath) + gy);
        _core.sizeDelta = new Vector2(w * (0.62f + 0.03f * breath), h * (0.80f + 0.03f * breath));
        Alpha(_coreImg, CoreColor.a * (1f + 0.14f * breath));

        Place(_halo, w * 0.5f + gx, h * HaloCenterY + gy);
        _halo.sizeDelta = new Vector2(w * (0.95f - 0.02f * breath), h * 1.35f);
        Alpha(_haloImg, HaloColor.a * (1f + 0.10f * breath));

        // ② 光斑：上飘 + 横向微摆，飘出上沿后从底部重来
        foreach (var m in _motes)
        {
            m.y += m.speed * dt;
            if (m.y > 1.08f) { m.y = -0.06f; m.x = Random.value; }

            float sway = Mathf.Sin(_t * m.swayFreq + m.phase) * m.swayAmp;
            float par = ParallaxMax * m.parallax;
            Place(m.rt, Mathf.Clamp01(m.x + sway) * w + _look.x * par * w,
                              m.y * h + _look.y * par * h);

            float d = m.size * h;
            m.rt.sizeDelta = new Vector2(d, d);
            Alpha(m.img, m.alpha * (0.85f + 0.15f * Mathf.Sin(_t * 0.6f + m.phase)));
        }
    }

    void OnDestroy()
    {
        DestroySprite(_radialSprite);
        DestroySprite(_moteSprite);
    }

    static void DestroySprite(Sprite s)
    {
        if (s == null) return;
        if (s.texture != null) Destroy(s.texture);
        Destroy(s);
    }

    // ── 工具 ───────────────────────────────────────────────────────────────

    /// <summary>锚点固定在中心，位置传「画布左下角为原点的比例坐标」。</summary>
    void Place(RectTransform rt, float x, float y)
    {
        rt.anchoredPosition = new Vector2(x - _self.rect.width * 0.5f, y - _self.rect.height * 0.5f);
    }

    static void Alpha(Image img, float a)
    {
        var c = img.color;
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }

    /// <summary>建一个不吃点击的 Image（用默认 UI 材质：不 new Material，也不依赖被剥离的着色器）。</summary>
    static RectTransform NewImage(string name, Color color, Sprite sprite, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>柔边圆：a = (1 - d)^power，d = 到圆心距离 / 半径。白色 RGB，靠 Image 的颜色染色。</summary>
    static Sprite MakeRadialSprite(int size, float power)
    {
        var tex = NewTexture(size, size);
        float c = (size - 1) * 0.5f, r = size * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r);
                float a = Mathf.Pow(1f - d, power);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        return Finish(tex, px, size, size);
    }

    static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;    // 默认 Repeat，边缘会出血
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    static Sprite Finish(Texture2D tex, Color32[] px, int w, int h)
    {
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
