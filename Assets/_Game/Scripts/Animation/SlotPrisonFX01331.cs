using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// SlotPrisonFX01331 — 01331 囚牢「雾牢」槽位特效（纯表现层）
// ============================================================================
//
// 和 SlotLockFX01505 同一套槽位 overlay 口径：
//   · 根节点铺满槽位，z 略高于槽位底色、低于 3D 卡牌与选择括号；
//   · 所有 Image raycastTarget = false，不参与点选；
//   · 状态先结算，这里只负责雾、雾栅、封印、涡眼与解除。
//
// 动态：
//   · 雾 A/B 两层左右弥漫，周期 8s / 4s，反向；
//   · 雾栅底端固定，顶端按相位随风摆；用 PrisonBarGraphic 生成条带网格，
//     不用多段 Image 拼接，避免接缝。
// ============================================================================

public static class SlotPrisonFX01331
{
    const string RootName = "SlotPrison01331";
    const string ResRoot = "UI/SlotPrison01331/";

    internal static Sprite FogBedSprite;
    internal static Sprite MistASprite;
    internal static Sprite MistBSprite;
    internal static Sprite SigilSprite;
    internal static Sprite MoteSprite;
    internal static Sprite VortexSprite;
    internal static Texture2D BarTexture;

    static bool _loadTried;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        FogBedSprite = null;
        MistASprite = null;
        MistBSprite = null;
        SigilSprite = null;
        MoteSprite = null;
        VortexSprite = null;
        BarTexture = null;
        _loadTried = false;
    }

    /// <summary>同步状态：blocked=true 创建/保留 overlay；false 播放解除并销毁。</summary>
    public static void Sync(BoardSlot slot, bool blocked, bool allowYuan)
    {
        if (slot == null) return;
        SlotPrisonOverlay overlay = Find(slot);
        if (blocked)
        {
            bool created = false;
            if (overlay == null)
            {
                overlay = Create(slot);
                created = overlay != null;
            }
            if (overlay == null) return;
            overlay.SetYuan(allowYuan);
            // 远端同步首次看到囚牢时也给一次入场；重连/场景恢复最多播一次，不打断已在播的入场。
            if (created) overlay.PlayEntrance();
            else if (overlay.IsReleasing) overlay.SetFinal(allowYuan);
        }
        else if (overlay != null)
        {
            overlay.Release();
        }
    }

    /// <summary>囚牢进场：两格各播一次 0.62s 入场。</summary>
    public static void Play(BoardSlot slot)
    {
        if (slot == null) return;
        SlotPrisonOverlay overlay = Find(slot);
        if (overlay == null) overlay = Create(slot);
        if (overlay == null) return;
        overlay.SetYuan(slot.prisonAllowYuan);
        overlay.PlayEntrance();
    }

    /// <summary>囚牢退场：0.34s 雾散、栅沉、印淡出后销毁。</summary>
    public static void Release(BoardSlot slot)
    {
        if (slot == null) return;
        SlotPrisonOverlay overlay = Find(slot);
        if (overlay != null) overlay.Release();
    }

    /// <summary>立即清掉（场景重建 / 强制重置用）。</summary>
    public static void Clear(BoardSlot slot)
    {
        if (slot == null) return;
        SlotPrisonOverlay overlay = Find(slot);
        if (overlay != null) Object.Destroy(overlay.gameObject);
    }

    static SlotPrisonOverlay Find(BoardSlot slot)
    {
        Transform t = slot.transform.Find(RootName);
        return t != null ? t.GetComponent<SlotPrisonOverlay>() : null;
    }

    static SlotPrisonOverlay Create(BoardSlot slot)
    {
        LoadAssets();
        if (FogBedSprite == null || MistASprite == null || MistBSprite == null ||
            SigilSprite == null || MoteSprite == null || VortexSprite == null || BarTexture == null)
            return null;

        Transform existing = slot.transform.Find(RootName);
        if (existing != null)
        {
            SlotPrisonOverlay bound = existing.GetComponent<SlotPrisonOverlay>();
            if (bound != null) return bound;
            Object.Destroy(existing.gameObject);
        }

        RectTransform slotRt = slot.transform as RectTransform;
        Vector2 slotSize = slotRt != null && slotRt.rect.width > 0.0001f
            ? slotRt.rect.size
            : new Vector2(1.25f, 2.22f) * HandManager.BoardLayoutScale;

        GameObject root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(slot.transform, false);
        SlotPrisonOverlay overlay = root.AddComponent<SlotPrisonOverlay>();
        overlay.Build(slot, slotSize);
        return overlay;
    }

    static void LoadAssets()
    {
        if (_loadTried) return;
        _loadTried = true;

        Texture2D fog = LoadTexture("SlotPrison_FogBed_01331");
        Texture2D mistA = LoadTexture("SlotPrison_MistA_01331");
        Texture2D mistB = LoadTexture("SlotPrison_MistB_01331");
        Texture2D sigil = LoadTexture("SlotPrison_Sigil_01331");
        Texture2D mote = LoadTexture("SlotPrison_Mote_01331");
        Texture2D vortex = LoadTexture("SlotPrison_Vortex_01331");
        BarTexture = LoadTexture("SlotPrison_Bar_01331");

        FogBedSprite = MakeSprite(fog);
        MistASprite = MakeSprite(mistA);
        MistBSprite = MakeSprite(mistB);
        SigilSprite = MakeSprite(sigil);
        MoteSprite = MakeSprite(mote);
        VortexSprite = MakeSprite(vortex);

        if (FogBedSprite == null || MistASprite == null || MistBSprite == null ||
            SigilSprite == null || MoteSprite == null || VortexSprite == null || BarTexture == null)
            Debug.LogWarning("[01331] SlotPrison sprites missing under Resources/" + ResRoot);
    }

    static Texture2D LoadTexture(string name)
    {
        Texture2D tex = Resources.Load<Texture2D>(ResRoot + name);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    static Sprite MakeSprite(Texture2D tex)
    {
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}

// ============================================================================

public class SlotPrisonOverlay : MonoBehaviour
{
    enum Mode { Idle, Entrance, Release }

    BoardSlot _slot;
    RectTransform _rt;
    CanvasGroup _group;

    Image _fog;
    Image _mistA;
    Image _mistB;
    Image _sigil;
    Image _vortex;
    Image _flash;
    Image[] _motes;
    PrisonBarGraphic _bars;

    float _w, _h;
    float _mistABaseY;
    float _mistBBaseX;
    float _mistBBaseY;
    float _clock;
    float _stateT;
    float _releaseP;
    Mode _mode;
    bool _allowYuan;

    float _fogAlpha;
    float _mistAlpha;
    float _sigilAlpha;
    float _vortexAlpha;
    float _moteAlpha;
    float _flashAlpha;
    float _sigilScale = 1f;
    float _sigilRot;
    readonly float[] _barReveal = { 0f, 0f, 0f, 0f };

    // 与 SlotPrisonMotionPreview 的 seed=5 粒子表一致：位置 / 上升偏移 / 尺寸 / 横向相位。
    static readonly float[] MoteFx = { 0.7074f, 0.4207f, 0.6036f, 0.7341f, 0.5378f, 0.7517f, 0.6411f, 0.3810f, 0.4650f, 0.5048f };
    static readonly float[] MoteOff = { 0.8079f, 0.4085f, 0.2345f, 0.3924f, 0.2715f, 0.2273f, 0.0012f, 0.8062f, 0.7989f, 0.5064f };
    static readonly float[] MoteSize = { 0.0220f, 0.0187f, 0.0316f, 0.0275f, 0.0189f, 0.0302f, 0.0241f, 0.0201f, 0.0225f, 0.0182f };
    static readonly float[] MotePhase = { 0.3387f, 6.2748f, 5.6374f, 0.3818f, 4.2653f, 0.1163f, 1.2764f, 4.3867f, 5.0232f, 5.8606f };

    public bool IsReleasing => _mode == Mode.Release;

    public void Build(BoardSlot slot, Vector2 slotSize)
    {
        _slot = slot;
        _w = Mathf.Max(0.0001f, slotSize.x);
        _h = Mathf.Max(0.0001f, slotSize.y);
        _mistABaseY = _h * 0.06f;
        _mistBBaseX = _w * 0.04f;
        _mistBBaseY = -_h * 0.12f;

        _rt = (RectTransform)transform;
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.one;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.anchoredPosition = Vector2.zero;
        _rt.localPosition = new Vector3(0f, 0f, -0.035f);
        _rt.localRotation = Quaternion.identity;
        _rt.localScale = Vector3.one;

        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        _mistA = CreateImage("MistA", SlotPrisonFX01331.MistASprite,
            new Vector2(0f, _mistABaseY), new Vector2(_w * 1.36f, _w * 1.36f), false);
        _mistB = CreateImage("MistB", SlotPrisonFX01331.MistBSprite,
            new Vector2(0f, _mistBBaseY), new Vector2(_w * 1.20f, _w * 1.20f), false);
        // 预览里底雾压在雾层之上，用来统一色相并柔化雾团；这里保持同一顺序。
        _fog = CreateImage("FogBed", SlotPrisonFX01331.FogBedSprite, Vector2.zero, new Vector2(_w, _h), true);

        GameObject barsObj = new GameObject("Bars", typeof(RectTransform), typeof(CanvasRenderer));
        barsObj.transform.SetParent(transform, false);
        RectTransform barsRt = (RectTransform)barsObj.transform;
        barsRt.anchorMin = Vector2.zero;
        barsRt.anchorMax = Vector2.one;
        barsRt.offsetMin = Vector2.zero;
        barsRt.offsetMax = Vector2.zero;
        _bars = barsObj.AddComponent<PrisonBarGraphic>();
        _bars.barTexture = SlotPrisonFX01331.BarTexture;
        _bars.raycastTarget = false;
        _bars.color = Color.white;

        _sigil = CreateImage("Sigil", SlotPrisonFX01331.SigilSprite,
            Vector2.zero, new Vector2(_w * 0.36f, _w * 0.36f), false);
        _flash = CreateImage("SealFlash", SlotPrisonFX01331.MoteSprite,
            Vector2.zero, new Vector2(_w * 0.92f, _w * 0.92f), false);
        _flash.enabled = false;

        _vortex = CreateImage("Vortex", SlotPrisonFX01331.VortexSprite,
            Vector2.zero, new Vector2(_w * 0.24f, _w * 0.24f), false);
        _vortex.enabled = false;

        _motes = new Image[MoteFx.Length];
        for (int i = 0; i < _motes.Length; i++)
        {
            _motes[i] = CreateImage("Mote_" + i, SlotPrisonFX01331.MoteSprite,
                Vector2.zero, new Vector2(_w * MoteSize[i], _w * MoteSize[i]), false);
            _motes[i].enabled = false;
        }

        // 选择括号在 z 上更靠前；这里仍把 sibling 顺序提回去，避免 Canvas 排序覆盖。
        transform.SetAsLastSibling();
        Transform selection = _slot != null ? _slot.transform.Find("SelectionIndicator") : null;
        if (selection != null) selection.SetAsLastSibling();

        SetFinal(false);
    }

    public void SetYuan(bool allow)
    {
        _allowYuan = allow;
        if (_mode == Mode.Idle) _vortexAlpha = allow ? 1f : 0f;
    }

    public void SetFinal(bool allow)
    {
        _allowYuan = allow;
        _mode = Mode.Idle;
        _stateT = 0f;
        _releaseP = 0f;
        _fogAlpha = 1f;
        _mistAlpha = 1f;
        _sigilAlpha = 1f;
        _vortexAlpha = allow ? 1f : 0f;
        _moteAlpha = 1f;
        _flashAlpha = 0f;
        _sigilScale = 1f;
        _sigilRot = 0f;
        for (int i = 0; i < _barReveal.Length; i++) _barReveal[i] = 1f;
        if (_bars != null) _bars.SetAllReveal(1f);
    }

    public void PlayEntrance()
    {
        if (_mode == Mode.Entrance && _stateT < 0.10f) return;
        _mode = Mode.Entrance;
        _stateT = 0f;
        _clock = 0f;
        _releaseP = 0f;
        _fogAlpha = 0f;
        _mistAlpha = 0f;
        _sigilAlpha = 0f;
        _vortexAlpha = 0f;
        _moteAlpha = 0f;
        _flashAlpha = 0f;
        _sigilScale = 1.35f;
        _sigilRot = -18f;
        for (int i = 0; i < _barReveal.Length; i++)
        {
            _barReveal[i] = 0f;
            if (_bars != null) _bars.SetReveal(i, 0f);
        }
    }

    public void Release()
    {
        if (_mode == Mode.Release) return;
        _mode = Mode.Release;
        _stateT = 0f;
        _releaseP = 0f;
    }

    void Update()
    {
        if (_slot == null)
        {
            Destroy(gameObject);
            return;
        }
        // 兜底：死亡 / 退场 / 远端清标记都可能只改字段不调 Sync，这里统一收口。
        if (!_slot.prisonBlocked && _mode != Mode.Release) Release();
        if (_mode == Mode.Idle && _slot.prisonAllowYuan != _allowYuan) SetYuan(_slot.prisonAllowYuan);

        float dt = Time.deltaTime;
        _clock += dt;

        if (_mode == Mode.Entrance)
        {
            _stateT += dt;
            if (_stateT >= 0.62f) SetFinal(_allowYuan);
            else ComputeEntrance(_stateT);
        }
        else if (_mode == Mode.Release)
        {
            _stateT += dt;
            _releaseP = Mathf.Clamp01(_stateT / 0.34f);
            if (_releaseP >= 1f)
            {
                Destroy(gameObject);
                return;
            }
        }

        ApplyMotion();
    }

    void ComputeEntrance(float t)
    {
        _fogAlpha = EaseOutCubic(Mathf.Clamp01(t / 0.10f));
        _mistAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.04f) / 0.24f));

        float sig = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.28f) / 0.18f));
        _sigilAlpha = sig;
        _sigilScale = Mathf.Lerp(1.35f, 1f, sig);
        _sigilRot = Mathf.Lerp(-18f, 0f, sig);

        _vortexAlpha = _allowYuan
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.44f) / 0.16f))
            : 0f;
        _moteAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.27f));

        for (int i = 0; i < _barReveal.Length; i++)
        {
            float u = Mathf.Clamp01((t - (0.12f + 0.04f * i)) / 0.16f);
            float reveal = u >= 1f ? 1f : EaseOutCubic(u);
            _barReveal[i] = reveal;
            if (_bars != null) _bars.SetReveal(i, reveal);
        }

        float flash = Mathf.Clamp01(1f - Mathf.Abs(t - 0.46f) / 0.10f);
        _flashAlpha = flash * 0.18f;
    }

    void ApplyMotion()
    {
        float fade = _mode == Mode.Release ? 1f - _releaseP : 1f;
        // 8s 循环相位：与 SlotPrisonMotionPreview 的 a 对齐。
        float a = _clock * Mathf.PI * 0.25f;

        if (_fog != null)
            _fog.color = new Color(1f, 1f, 1f,
                _fogAlpha * fade * (0.90f + 0.06f * Mathf.Sin(a * 2f)));

        // 雾 A：8s，左右 ±10% 槽宽
        if (_mistA != null)
        {
            float ax = Mathf.Sin(a) * _w * 0.10f;
            float ay = Mathf.Sin(a + 0.9f) * _h * 0.012f;
            RectTransform rt = _mistA.rectTransform;
            rt.anchoredPosition = new Vector2(ax, _mistABaseY + ay);
            rt.localRotation = Quaternion.Euler(0f, 0f, -7f + Mathf.Sin(a + 0.4f) * 2.2f);
            float ms = 1f + 0.12f * _releaseP;
            rt.localScale = new Vector3(ms, ms, 1f);
            _mistA.color = new Color(1f, 1f, 1f,
                0.80f * _mistAlpha * fade * (0.92f + 0.08f * Mathf.Sin(a * 2f + 0.3f)));
        }

        // 雾 B：4s，反向 ±7.8% 槽宽
        if (_mistB != null)
        {
            float bx = Mathf.Sin(a * 2f + 1.3f) * _w * 0.078f;
            float by = Mathf.Cos(a * 2f + 0.4f) * _h * 0.015f;
            RectTransform rt = _mistB.rectTransform;
            rt.anchoredPosition = new Vector2(_mistBBaseX + bx, _mistBBaseY + by);
            rt.localRotation = Quaternion.Euler(0f, 0f, 12f + Mathf.Sin(a * 2f + 1.1f) * 2.4f);
            float ms = 1f + 0.12f * _releaseP;
            rt.localScale = new Vector3(ms, ms, 1f);
            _mistB.color = new Color(1f, 1f, 1f,
                0.66f * _mistAlpha * fade * (0.90f + 0.10f * Mathf.Sin(a * 2f + 1.7f)));
        }

        // 雾栅：底端固定，顶端随风摆
        if (_bars != null)
        {
            _bars.SetWindTime(_clock);
            for (int i = 0; i < _barReveal.Length; i++)
                _bars.SetReveal(i, _barReveal[i] * fade);
        }

        // 中央封印
        if (_sigil != null)
        {
            float ss = _sigilScale
                       * (1f + 0.008f * Mathf.Sin(a * 2f + 0.5f))
                       * (1f + 0.15f * _releaseP);
            _sigil.rectTransform.localScale = new Vector3(ss, ss, 1f);
            _sigil.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _sigilRot + Mathf.Sin(a + 0.2f) * 1.5f);
            float sa = (0.92f + 0.05f * Mathf.Sin(a * 2f + 0.4f)) * _sigilAlpha * fade;
            _sigil.color = new Color(1f, 1f, 1f, Mathf.Clamp01(sa));
        }

        // 可放渊的涡眼
        if (_vortex != null)
        {
            float va = 0.55f * _vortexAlpha * fade;
            _vortex.enabled = va > 0.01f;
            _vortex.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_clock * 45f);
            _vortex.color = new Color(1f, 1f, 1f, va);
        }

        // 微光尘
        if (_motes != null)
        {
            for (int i = 0; i < _motes.Length; i++)
            {
                float prog = Mathf.Repeat(_clock / 3f + MoteOff[i], 1f);
                float fx = MoteFx[i] + 0.02f * Mathf.Sin(a + MotePhase[i]);
                float fy = 0.90f - 0.58f * prog;
                float x = (fx - 0.5f) * _w;
                float y = (0.5f - fy) * _h;
                float al = Mathf.Pow(Mathf.Sin(prog * Mathf.PI), 1.2f) * 0.55f * _moteAlpha * fade;
                RectTransform rt = _motes[i].rectTransform;
                rt.anchoredPosition = new Vector2(x, y);
                _motes[i].enabled = al > 0.01f;
                _motes[i].color = new Color(1f, 1f, 1f, al);
            }
        }

        // 落定帧紫色闪光
        if (_flash != null)
        {
            float fa = _flashAlpha * fade;
            _flash.enabled = fa > 0.005f;
            if (_flash.enabled)
            {
                _flash.color = new Color(0.66f, 0.47f, 1f, fa);
                float fs = 1f + 0.15f * (1f - Mathf.Clamp01(_flashAlpha / 0.18f));
                _flash.rectTransform.localScale = new Vector3(fs, fs, 1f);
            }
        }
    }

    Image CreateImage(string name, Sprite sprite, Vector2 pos, Vector2 size, bool stretch)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)go.transform;
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = false;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}

// ============================================================================

/// <summary>四条雾栅的条带网格：底端顶点固定，顶端按相位摆动。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PrisonBarGraphic : MaskableGraphic
{
    public Texture2D barTexture;
    public int segments = 12;

    readonly float[] _reveal = { 1f, 1f, 1f, 1f };
    float _windTime;

    static readonly float[] X = { 0.17f, 0.38f, 0.62f, 0.83f };
    static readonly float[] H = { 0.74f, 0.84f, 0.80f, 0.70f };
    static readonly float[] Phase = { 0f, 0.9f, 1.8f, 2.7f };

    public override Texture mainTexture => barTexture != null ? (Texture)barTexture : base.mainTexture;

    public void SetWindTime(float t)
    {
        if (Mathf.Abs(t - _windTime) < 0.0001f) return;
        _windTime = t;
        SetVerticesDirty();
    }

    public void SetReveal(int index, float reveal)
    {
        if (index < 0 || index >= _reveal.Length) return;
        reveal = Mathf.Clamp01(reveal);
        if (Mathf.Abs(_reveal[index] - reveal) < 0.0005f) return;
        _reveal[index] = reveal;
        SetVerticesDirty();
    }

    public void SetAllReveal(float reveal)
    {
        reveal = Mathf.Clamp01(reveal);
        for (int i = 0; i < _reveal.Length; i++) _reveal[i] = reveal;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (barTexture == null) return;

        Rect r = rectTransform.rect;
        float w = r.width;
        float h = r.height;
        if (w <= 0f || h <= 0f) return;

        Color32 c = color;
        for (int i = 0; i < _reveal.Length; i++)
        {
            float reveal = _reveal[i];
            if (reveal <= 0.001f) continue;

            float barWidth = w * 0.115f;
            float barHeight = h * H[i] * reveal;
            float baseX = r.xMin + w * X[i];
            float baseY = r.yMin + h * 0.06f;
            float half = barWidth * 0.5f;
            int start = vh.currentVertCount;

            for (int s = 0; s <= segments; s++)
            {
                float t = (float)s / segments;
                float y = baseY + barHeight * t;
                float x = baseX + Offset(t, i, w);
                vh.AddVert(new Vector3(x - half, y, 0f), c, new Vector2(0f, t));
                vh.AddVert(new Vector3(x + half, y, 0f), c, new Vector2(1f, t));
            }

            for (int s = 0; s < segments; s++)
            {
                int v0 = start + s * 2;
                int v1 = v0 + 1;
                int v2 = v0 + 2;
                int v3 = v0 + 3;
                vh.AddTriangle(v0, v2, v1);
                vh.AddTriangle(v1, v2, v3);
            }
        }
    }

    float Offset(float t, int i, float w)
    {
        float phase = Phase[i];
        // 与 SlotPrisonMotionPreview 的 bend_rows 同式：
        // a = 8s 循环相位；主摆 4s、次摆 8s、颤动 2.667s，共用 0.80–1.00 阵风包络。
        float a = _windTime * Mathf.PI * 0.25f;
        float gust = 0.80f + 0.20f * Mathf.Sin(a + 0.6f);
        float top = Mathf.Sin(a * 2f + phase) * 0.055f * w
                  + Mathf.Sin(a + phase * 0.6f) * 0.018f * w;
        float wobbleAmp = Mathf.Sin(a * 3f + phase * 1.4f) * 0.008f * w;
        float bend = top * Mathf.Pow(t, 1.65f) * (0.75f + 0.25f * gust);
        float wobble = wobbleAmp * Mathf.Pow(t, 2f)
                       * Mathf.Sin(phase + a * 0.8f + (1f - t) * 3.4f);
        return bend + wobble;
    }
}
