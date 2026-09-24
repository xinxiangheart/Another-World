using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ============================================================================
// DamageFloater — 3D 场景浮动数字（伤害红 / 治疗绿 / 抵挡蓝 / 增幅黄 / 减益紫）
// ============================================================================
//
// 弹出：**随机方向**（出生点散开 + 方向偏角 + 速度 / 大小 / 倾斜抖动），
//       走抛物线下坠（不是一条直线向上），落位前从小冲过头再收回。
// 材质：与顶栏数值同源（NotoSerifCJKsc-Black SDF + Distance Field shader），
//       在字体自带材质上复制一份，打开 OUTLINE_ON / UNDERLAY_ON：
//         字面 = 类型色提亮、描边 = 类型色压深、再加一层柔投影 —— 压在花哨卡面上也读得清。
//       顶点色恒为白：颜色全交给材质，免得 TMP 把描边颜色也乘一遍。
//
// 配置: Project 窗口右键 → Another World → Floater Config，
//       放在 Resources/Config/FloaterConfig.asset（本类按该路径加载）。
// 未配置时全部走代码里的默认值。
// ============================================================================

public enum FloaterType { Damage, Heal, Blocked, Buff, Debuff }

public class DamageFloater : MonoBehaviour
{
    [Tooltip("浮动数字配置（ScriptableObject），Inspector 实时调参")]
    public FloaterConfig config;

    // Resources 下的实际路径（旧路径写错了一个层级 → 配置整份失效，这里两个都试）
    const string CfgPath = "Config/FloaterConfig";
    const string CfgPathLegacy = "FloaterConfig";
    const string NumFontName = "NotoSerifCJKsc-Black SDF";

    static FloaterConfig _cfg;
    static Canvas _sharedCanvas;
    static Transform _poolRoot;
    static GameObject _template;
    static TMP_FontAsset _font;
    static Material[] _typeMats;      // 一类一份：描边色随类型
    static TMP_FontAsset _matFont;    // 上面那批材质是按哪个字体建的（换场景重载字体即作废重建）
    static bool _cfgLoaded;

    TextMeshProUGUI _tmp;
    RectTransform _rt;
    CanvasGroup _cg;
    float _age;
    float _duration;
    float _fadeStart;
    Vector3 _worldPos;
    Vector3 _velocity;      // 世界单位/秒（初速带随机偏角）
    float _gravity;         // 下坠
    float _drift;           // 横向收束
    float _popTime;
    float _popFrom;
    float _baseScale;       // 类型基准缩放 × 随机抖动

    static readonly Queue<DamageFloater> _pool = new Queue<DamageFloater>();

    // ═══════════════════════════════════════════════════════════════════
    // 对外接口
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>在世界坐标处弹出浮动数字（随机方向 + 随机大小，同类同时弹会自然错开）。</summary>
    public static void Show(Vector3 worldPos, int value, FloaterType type)
    {
        if (type == FloaterType.Heal && value <= 0) return;

        var cfg = Config;
        var df = GetFromPool();
        df.gameObject.SetActive(true);
        df.EnsureRefs();

        df._age = 0f;
        df._duration = Num(cfg != null ? cfg.duration : (float?)null, 1.2f);
        df._fadeStart = Num(cfg != null ? cfg.fadeStart : (float?)null, 0.45f);
        df._popTime = Num(cfg != null ? cfg.popTime : (float?)null, 0.16f);
        df._popFrom = Num(cfg != null ? cfg.popFrom : (float?)null, 0.55f);
        df._gravity = Num(cfg != null ? cfg.gravity : (float?)null, 2.2f);
        df._drift = Num(cfg != null ? cfg.horizontalDrift : (float?)null, 1.6f);

        // ── 随机弹出：出生点散开 + 方向偏转 + 速度抖动 ──
        float spread = Num(cfg != null ? cfg.angleSpread : (float?)null, 50f);
        float speed = Num(cfg != null ? cfg.floatSpeed : (float?)null, 1.3f)
                      * (1f + Random.Range(-Num(cfg != null ? cfg.speedJitter : (float?)null, 0.22f),
                                             Num(cfg != null ? cfg.speedJitter : (float?)null, 0.22f)));
        float spawn = Num(cfg != null ? cfg.spawnJitter : (float?)null, 0.24f);
        float ang = Random.Range(-spread, spread) * Mathf.Deg2Rad;
        df._velocity = new Vector3(Mathf.Sin(ang), Mathf.Cos(ang), 0f) * speed;
        df._worldPos = worldPos + new Vector3(Random.Range(-spawn, spawn),
                                             Random.Range(-spawn * 0.6f, spawn * 0.6f), 0f);

        // ── 文案 / 配色 / 缩放 ──
        string text;
        Color fill;
        float scale;
        switch (type)
        {
            case FloaterType.Heal:
                text = "+" + value;
                scale = Num(cfg != null ? cfg.healScale : (float?)null, 0.92f);
                break;
            case FloaterType.Blocked:
                text = cfg != null ? cfg.blockedText : "抵挡!";
                scale = Num(cfg != null ? cfg.blockedScale : (float?)null, 1f);
                break;
            case FloaterType.Buff:
                text = "+" + value;
                scale = Num(cfg != null ? cfg.buffScale : (float?)null, 0.85f);
                break;
            case FloaterType.Debuff:
                text = "-" + value;
                scale = Num(cfg != null ? cfg.debuffScale : (float?)null, 0.85f);
                break;
            default:
                text = "-" + value;
                scale = Num(cfg != null ? cfg.damageScale : (float?)null, 1.05f);
                break;
        }
        fill = TypeFill(type);

        var tmp = df._tmp;
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = Color.white;                 // 顶点色留白，颜色全部由材质决定
            var font = Font;
            if (font != null && tmp.font != font) tmp.font = font;   // 先换字体（会重置材质）
            var mat = TypeMaterial(type, fill);
            if (mat != null) tmp.fontSharedMaterial = mat;
        }

        float sizeJ = Num(cfg != null ? cfg.sizeJitter : (float?)null, 0.1f);
        float spinJ = Num(cfg != null ? cfg.spinJitter : (float?)null, 6f);
        df._baseScale = scale * (1f + Random.Range(-sizeJ, sizeJ));
        if (df._rt != null)
        {
            df._rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-spinJ, spinJ));
            df._rt.localScale = Vector3.one * (df._baseScale * df._popFrom);   // 从「小」弹出
        }
        if (df._cg != null) df._cg.alpha = 1f;
        df.UpdatePosition();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════════════════════

    void Awake() { EnsureRefs(); }

    /// <summary>取齐组件引用。Awake 只在「激活」时才跑（池里的实例是实例化后 SetActive(true)
    /// 才醒的），所以建实例的地方要能主动调一次，别拿 null 去判断配置。</summary>
    void EnsureRefs()
    {
        if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (_cg == null)
        {
            _cg = gameObject.GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Update()
    {
        _age += Time.deltaTime;

        // 抛物线：竖直方向匀减速再下坠，横向慢慢收束 —— 起点方向随机但落点不飘走
        _velocity.y -= _gravity * Time.deltaTime;
        _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, _drift * Time.deltaTime);
        _worldPos += _velocity * Time.deltaTime;
        UpdatePosition();

        // 弹入：从小冲过头再收回（EaseOutBack），之后保持基准大小
        float sc = _baseScale;
        if (_age < _popTime && _popTime > 0f)
            sc = _baseScale * Mathf.Lerp(_popFrom, 1f, EaseOutBack(_age / _popTime));
        if (_rt != null) _rt.localScale = Vector3.one * sc;

        float t = _duration > 0f ? _age / _duration : 1f;
        float alpha = 1f - Mathf.InverseLerp(_fadeStart, 1f, t);
        if (_cg != null) _cg.alpha = alpha;

        if (_age >= _duration)
        {
            gameObject.SetActive(false);
            _pool.Enqueue(this);
        }
    }

    /// <summary>缩放过冲缓动：p=0→0，中途冲过 1，p=1→1。</summary>
    static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float q = Mathf.Clamp01(p) - 1f;
        return 1f + c3 * q * q * q + c1 * q * q;
    }

    void UpdatePosition()
    {
        if (_rt == null || _sharedCanvas == null) return;

        // 相机自检重绑（这段别删）：画布挂在 DontDestroyOnLoad 的池上、全局只建一次，
        // EnsureInstance 里记下的那台 Camera.main 会随场景切换被销毁 —— 相机一死，Canvas 的
        // 坐标空间就不再由它驱动（退回屏幕像素空间），而 update 里仍拿存活的 Camera.main 去
        // 反算局部坐标，结果会整体塌到画布左下角：表现就是**飘字全堆在屏幕最左下角**，
        // 而不是弹在卡槽上方。每帧查一次，是死引用就重绑当前主相机，Canvas 随即恢复。
        Camera cam = _sharedCanvas.worldCamera;
        bool reBound = false;
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
            _sharedCanvas.worldCamera = cam;
            reBound = true;
        }
        if (!Finite(_worldPos)) return;   // 坏坐标（NaN/Inf）→ 保持上一帧位置，别让它塌到原点

        SyncAnchorToCanvas();

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, _worldPos);
        // 刚重绑的这一帧，画布还停在失效的坐标系里，正常反算给出的正是「左下角」那个错误值 ——
        // 该帧改用屏幕像素换算兜底，等下一帧画布按新相机排布好，再走正常反算。
        _rt.anchoredPosition = reBound ? PixelToCanvas(screen) : ScreenToCanvas(screen, cam);
    }

    /// <summary>锚点与画布 pivot 同步：anchoredPosition 的参考点是「parentRect.min + anchor × 画布尺寸」，
    /// 只有 anchor == 画布 pivot 时它才正好是画布中心，从屏幕像素反算出来的局部坐标才对得上；
    /// 锚点若是 (0,0) 那种默认值而画布 pivot 不是，数字会整体偏出半个屏幕。</summary>
    void SyncAnchorToCanvas()
    {
        Vector2 anchor = ((RectTransform)_sharedCanvas.transform).pivot;
        if (_rt.anchorMin != anchor || _rt.anchorMax != anchor)
            _rt.anchorMin = _rt.anchorMax = anchor;
    }

    /// <summary>屏幕像素 → 画布局部坐标（即 anchoredPosition）。反算失败或结果非法时走
    /// PixelToCanvas 兜底 —— 宁可偏一点，也不能写进 NaN / 让数字塌到画布角上。</summary>
    Vector2 ScreenToCanvas(Vector2 screen, Camera cam)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_sharedCanvas.transform, screen, cam, out Vector2 local))
            if (Finite(local)) return local;
        return PixelToCanvas(screen);
    }

    /// <summary>兜底换算：画布的局部原点（相机模式 / 叠加模式都一样）就在屏幕中心，
    /// 故 局部 = (屏幕像素 − 屏幕中心) ÷ 画布缩放。</summary>
    Vector2 PixelToCanvas(Vector2 screen)
    {
        float s = _sharedCanvas.scaleFactor;
        if (s <= 0f) s = 1f;
        return new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f) / s;
    }

    static bool Finite(float f)   => !float.IsNaN(f) && !float.IsInfinity(f);
    static bool Finite(Vector2 v) => Finite(v.x) && Finite(v.y);
    static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

    // ═══════════════════════════════════════════════════════════════════
    // 字体 / 材质
    // ═══════════════════════════════════════════════════════════════════

    static FloaterConfig Config
    {
        get
        {
            if (!_cfgLoaded)
            {
                _cfgLoaded = true;
                _cfg = Resources.Load<FloaterConfig>(CfgPath);
                if (_cfg == null) _cfg = Resources.Load<FloaterConfig>(CfgPathLegacy);
                if (_cfg == null)
                    Debug.LogWarning("[Floater] 没找到 Resources/" + CfgPath + ".asset，浮动数字走代码默认值");
            }
            return _cfg;
        }
    }

    /// <summary>项目统一数字字体（与顶栏数值同源的 Serif CJK Black）。
    /// 字体资产不在 Resources 下，只能从「已加载资产」里取（顶栏正在用，正常情况下一定在）。</summary>
    static TMP_FontAsset Font
    {
        get
        {
            if (_font != null) return _font;
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == NumFontName) { _font = all[i]; break; }
            if (_font == null)
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && all[i].name.Contains("NotoSerifCJKsc") && all[i].name.Contains("Black")) { _font = all[i]; break; }
            if (_font == null)
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && all[i].name.Contains("NotoSerifCJKsc")) { _font = all[i]; break; }
            return _font;   // 都没有 → null，TMP 会用默认字体（数字仍能显示）
        }
    }

    /// <summary>一类一份材质：复制字体自带材质（保住 SDF 图集与距离场参数），再打开
    /// OUTLINE_ON / UNDERLAY_ON —— 字面=类型色提亮、描边=类型色压深、柔投影垫底。</summary>
    static Material TypeMaterial(FloaterType type, Color fill)
    {
        int i = (int)type;
        var font = Font;
        if (font == null || font.material == null) return null;

        // 池与静态引用是 DontDestroyOnLoad 的：换场景后字体 atlas 可能被卸载重载，
        // 旧材质会指向已销毁的图集（数字糊掉），所以字体换了就整批重来。
        if (_typeMats == null || _matFont != font)
        {
            _matFont = font;
            _typeMats = new Material[5];
        }
        if (_typeMats[i] != null) return _typeMats[i];

        var cfg = Config;
        float brighten = Num(cfg != null ? cfg.fillBrighten : (float?)null, 0.22f);
        float darken = Num(cfg != null ? cfg.rimDarken : (float?)null, 0.30f);
        float rimW = Num(cfg != null ? cfg.outlineWidth : (float?)null, 0.22f);
        bool rimFromType = cfg == null || cfg.useRimColor;
        Color rim = rimFromType
            ? new Color(fill.r * darken, fill.g * darken, fill.b * darken, 1f)
            : (cfg != null ? cfg.outlineColor : new Color(0.05f, 0.04f, 0.04f, 1f));

        var m = new Material(font.material);
        m.name = "FloaterMat_" + type;

        m.SetColor("_FaceColor", Color.Lerp(fill, Color.white, Mathf.Clamp01(brighten)));
        SetIf(m, "_FaceDilate", Num(cfg != null ? cfg.faceDilate : (float?)null, 0.28f));
        m.EnableKeyword("OUTLINE_ON");
        SetIf(m, "_OutlineWidth", rimW);
        SetIf(m, "_OutlineSoftness", Num(cfg != null ? cfg.outlineSoftness : (float?)null, 0.06f));
        m.SetColor("_OutlineColor", rim);

        // 柔投影：压在花哨卡面上也能读清（shader 有 underlay 才开）
        if ((cfg == null || cfg.useShadow) && m.HasProperty("_UnderlayColor"))
        {
            float off = Num(cfg != null ? cfg.shadowOffset : (float?)null, 0.9f);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", cfg != null ? cfg.shadowColor : new Color(0f, 0f, 0f, 0.62f));
            SetIf(m, "_UnderlayOffsetX", off);
            SetIf(m, "_UnderlayOffsetY", -off);
            SetIf(m, "_UnderlayDilate", 0.12f);
            SetIf(m, "_UnderlaySoftness", 0.35f);
        }

        _typeMats[i] = m;
        return m;
    }

    static void SetIf(Material m, string prop, float v)
    {
        if (m.HasProperty(prop)) m.SetFloat(prop, v);
    }

    static float Num(float? v, float fallback) { return v.HasValue ? v.Value : fallback; }

    /// <summary>各类型的字面主色（默认值与 Show 里那份是同一套；配置存在时以配置为准）。
    /// 单独提出来给「面板 / 血条弹字」复用，免得颜色在两处各写一份后走样。</summary>
    public static Color TypeFill(FloaterType type)
    {
        var cfg = Config;
        switch (type)
        {
            case FloaterType.Heal:    return cfg != null ? cfg.healColor    : new Color(0.25f, 1f, 0.35f, 1f);
            case FloaterType.Blocked: return cfg != null ? cfg.blockedColor : new Color(0.35f, 0.6f, 1f, 1f);
            case FloaterType.Buff:    return cfg != null ? cfg.buffColor    : new Color(1f, 0.85f, 0.12f, 1f);
            case FloaterType.Debuff:  return cfg != null ? cfg.debuffColor  : new Color(0.72f, 0.35f, 1f, 1f);
            default:                  return cfg != null ? cfg.damageColor  : new Color(1f, 0.22f, 0.2f, 1f);
        }
    }

    /// <summary>把飘字那套「字体 + 字面 / 描边 / 柔投影」材质套到任意 TMP 上
    /// （攻击回合面板的飞行数字与血条弹字复用，样式与场上飘字完全一致）。</summary>
    public static void ApplyStyle(TMP_Text tmp, FloaterType type)
    {
        if (tmp == null) return;
        tmp.color = Color.white;                 // 顶点色留白，颜色全交给材质
        var font = Font;
        if (font != null && tmp.font != font) tmp.font = font;   // 先换字体（会重置材质）
        var mat = TypeMaterial(type, TypeFill(type));
        if (mat != null) tmp.fontSharedMaterial = mat;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 对象池
    // ═══════════════════════════════════════════════════════════════════

    static DamageFloater GetFromPool()
    {
        EnsureInstance();
        while (_pool.Count > 0)
        {
            var p = _pool.Dequeue();
            if (p != null) return p;
        }
        return CreateNew();
    }

    static void EnsureInstance()
    {
        if (_poolRoot != null) return;

        _poolRoot = new GameObject("DamageFloaterPool").transform;
        DontDestroyOnLoad(_poolRoot.gameObject);

        var canvasGo = new GameObject("FloaterCanvas");
        canvasGo.transform.SetParent(_poolRoot, false);
        _sharedCanvas = canvasGo.AddComponent<Canvas>();
        _sharedCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        // 注意：这只是「建池那一刻」的主相机，换场景会被销毁 → 变成死引用。
        // 每帧由 UpdatePosition 自检重绑（见那里的说明），不要删掉那段逻辑。
        _sharedCanvas.worldCamera = Camera.main;

        var cfg = Config;
        _sharedCanvas.sortingOrder = cfg != null ? cfg.sortingOrder : 100;
        _sharedCanvas.planeDistance = cfg != null ? cfg.planeDistance : 5f;

        // 模板：只做骨架，字体 / 材质每次 Show 时再套（那时顶栏字体一定已经加载好了）
        var tmpl = CreateTemplate(canvasGo.transform);
        tmpl.gameObject.SetActive(false);
        _template = tmpl.gameObject;

        // 与全项目统一口径：1920×1080 + 按宽高比切换 match（GameSettings.ApplyScalerTo 内部即 SafeMatch）。
        // 不这么做的话飘字画布是「恒定像素」尺寸，同一处伤害数字的视觉大小会随分辨率变、和其它 UI 不同步；
        // 位置换算走 ScreenPointToLocalPointInRectangle，本来就按缩放系数处理，不受影响。
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        GameSettings.ApplyScalerTo(scaler);
    }

    static DamageFloater CreateTemplate(Transform parent)
    {
        var go = new GameObject("Floater");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        // 中心 pivot：数字以「锚点位置」为中心显示。锚点本身不在这里写死，
        // 由 UpdatePosition 每次落位前同步成画布的 pivot（见 ScreenToCanvas 的说明）——
        // 锚点若和画布 pivot 不一致，anchoredPosition 的参考点就不是画布中心，数字会整体偏掉，
        // (0,0) 这种默认值更是直接塌到画布左下角。
        rt.pivot = new Vector2(0.5f, 0.5f);
        var cfg = Config;
        rt.sizeDelta = new Vector2(cfg != null ? cfg.boxWidth : 160f, cfg != null ? cfg.boxHeight : 60f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Num(cfg != null ? cfg.fontSize : (float?)null, 58f);
        tmp.fontStyle = cfg == null || cfg.bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        return go.AddComponent<DamageFloater>();
    }

    static DamageFloater CreateNew()
    {
        var go = Instantiate(_template, _sharedCanvas.transform);
        go.name = "Floater";

        var df = go.GetComponent<DamageFloater>();
        df.config = Config;
        df.EnsureRefs();     // 克隆体还没激活，Awake 没跑过，先手动取齐引用

        var cfg = Config;
        if (df._tmp != null && cfg != null)
        {
            df._tmp.fontSize = cfg.fontSize;
            df._tmp.fontStyle = cfg.bold ? FontStyles.Bold : FontStyles.Normal;
            if (df._rt != null) df._rt.sizeDelta = new Vector2(cfg.boxWidth, cfg.boxHeight);
        }
        return df;
    }
}
