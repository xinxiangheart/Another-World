using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ============================================================================
// DamageFloater — 3D 场景浮动数字（伤害红 / 治疗绿 / 抵挡蓝）
// ============================================================================
//
// 从世界坐标上方浮出，向上飘移 + 渐隐，到时回收。
// 配置: Project 窗口右键 → Another World → Floater Config，拖入 config 字段。
// 未配置时使用默认值。
// ============================================================================

public enum FloaterType { Damage, Heal, Blocked, Buff, Debuff }

public class DamageFloater : MonoBehaviour
{
    [Tooltip("浮动数字配置（ScriptableObject），Inspector 实时调参")]
    public FloaterConfig config;

    static FloaterConfig _cfg;
    static Canvas _sharedCanvas;
    static Transform _poolRoot;

    TextMeshProUGUI _tmp;
    RectTransform _rt;
    CanvasGroup _cg;
    float _age;
    float _duration;
    Vector3 _worldPos;
    Vector3 _velocity;

    static Queue<DamageFloater> _pool = new Queue<DamageFloater>();

    // ═══════════════════════════════════════════════════════════════════

    /// <summary>在世界坐标处弹出浮动数字</summary>
    public static void Show(Vector3 worldPos, int value, FloaterType type)
    {
        if (type == FloaterType.Heal && value <= 0) return;

        var df = GetFromPool();
        df.gameObject.SetActive(true);
        df._age = 0f;
        df._duration = _cfg != null ? _cfg.duration : 1.5f;
        df._worldPos = worldPos;
        float speed = _cfg != null ? _cfg.floatSpeed : 1.2f;
        df._velocity = Vector3.up * speed;

        switch (type)
        {
            case FloaterType.Damage:
                df._tmp.text = $"-{value}";
                df._tmp.color = _cfg != null ? _cfg.damageColor : new Color(1f, 0.2f, 0.2f, 1f);
                df.transform.localScale = Vector3.one * (_cfg != null ? _cfg.damageScale : 1.1f);
                break;
            case FloaterType.Heal:
                df._tmp.text = $"+{value}";
                df._tmp.color = _cfg != null ? _cfg.healColor : new Color(0.2f, 1f, 0.3f, 1f);
                df.transform.localScale = Vector3.one * (_cfg != null ? _cfg.healScale : 0.9f);
                break;
            case FloaterType.Blocked:
                df._tmp.text = _cfg != null ? _cfg.blockedText : "抵挡!";
                df._tmp.color = _cfg != null ? _cfg.blockedColor : new Color(0.3f, 0.5f, 1f, 1f);
                df.transform.localScale = Vector3.one * (_cfg != null ? _cfg.blockedScale : 1f);
                break;
            case FloaterType.Buff:
                df._tmp.text = $"+{value}";
                df._tmp.color = _cfg != null ? _cfg.buffColor : new Color(1f, 0.85f, 0.1f, 1f);   // 黄
                df.transform.localScale = Vector3.one * (_cfg != null ? _cfg.buffScale : 0.85f);
                break;
            case FloaterType.Debuff:
                df._tmp.text = $"-{value}";
                df._tmp.color = _cfg != null ? _cfg.debuffColor : new Color(0.7f, 0.3f, 1f, 1f);   // 紫
                df.transform.localScale = Vector3.one * (_cfg != null ? _cfg.debuffScale : 0.85f);
                break;
        }
        if (df._cg != null) df._cg.alpha = 1f;
        df.UpdatePosition();
    }

    // ═══════════════════════════════════════════════════════════════════

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        _rt = GetComponent<RectTransform>();
        _cg = gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        _age += Time.deltaTime;

        _worldPos += _velocity * Time.deltaTime;
        UpdatePosition();

        float t = _age / _duration;
        float fadeStart = _cfg != null ? _cfg.fadeStart : 0f;
        float alpha = Mathf.InverseLerp(fadeStart, 1f, t);
        alpha = Mathf.Lerp(1f, 0f, alpha);
        if (_cg != null) _cg.alpha = alpha;

        if (_age >= _duration)
        {
            gameObject.SetActive(false);
            _pool.Enqueue(this);
        }
    }

    void UpdatePosition()
    {
        if (_sharedCanvas == null) return;
        Camera cam = _sharedCanvas.worldCamera ?? Camera.main;
        if (cam == null) return;
        var screen = RectTransformUtility.WorldToScreenPoint(cam, _worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_sharedCanvas.transform, screen, cam, out var local);
        _rt.anchoredPosition = local;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 对象池
    // ═══════════════════════════════════════════════════════════════════

    static DamageFloater GetFromPool()
    {
        EnsureInstance();
        if (_pool.Count > 0) return _pool.Dequeue();
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
        _sharedCanvas.worldCamera = Camera.main;

        // 先建模板（会触发 Awake，此时 _cfg 还是 null）
        var tmpl = CreateTemplate(canvasGo.transform);
        tmpl.gameObject.SetActive(false);

        // 从模板取 config 引用
        _cfg = tmpl.config;
        if (_cfg == null)
            _cfg = Resources.Load<FloaterConfig>("FloaterConfig");

        // 应用 config 到 canvas
        if (_cfg != null)
        {
            _sharedCanvas.sortingOrder = _cfg.sortingOrder;
            _sharedCanvas.planeDistance = _cfg.planeDistance;
        }
        else
        {
            _sharedCanvas.sortingOrder = 100;
            _sharedCanvas.planeDistance = 5f;
        }

        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
    }

    static DamageFloater CreateTemplate(Transform parent)
    {
        var go = new GameObject("Floater");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120, 40);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 180);

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSerifCJKsc-Bold SDF");
        if (font != null) { tmp.font = font; tmp.fontMaterial = font.material; }

        return go.AddComponent<DamageFloater>();
    }

    static DamageFloater CreateNew()
    {
        var go = Instantiate(_poolRoot.GetChild(0).GetChild(0).gameObject, _poolRoot.GetChild(0));
        go.name = "Floater";
        // 复制 config 到新实例
        var df = go.GetComponent<DamageFloater>();
        df.config = _cfg;

        // 应用字体样式
        var tmp = df._tmp;
        if (_cfg != null && tmp != null)
        {
            tmp.fontSize = _cfg.fontSize;
            tmp.fontStyle = _cfg.bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.outlineWidth = _cfg.outlineWidth;
            tmp.outlineColor = _cfg.outlineColor;
            var rt = df._rt;
            if (rt != null) rt.sizeDelta = new Vector2(_cfg.boxWidth, _cfg.boxHeight);
        }
        return df;
    }
}
