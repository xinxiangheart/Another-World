using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 纯文字菜单项：常态白色，鼠标悬停 / 手柄选中时变金色并轻微放大，
/// 两侧指示标从「生成位置」迅速贴近文字，末段速度锐减（缓出）。
///
/// 结构约定（scene 里就是这么搭的）：
///   Item            ← 本脚本 + Button + 全透明 Image（当点击热区）
///   ├─ Label        ← TMP 文字，只有它被缩放
///   ├─ HintLeft     ← 指示标（左）
///   └─ HintRight    ← 指示标（右）
/// 指示标是 Label 的兄弟节点，所以不会跟着文字一起放大。
/// 文字的黑描边来自材质 TMP_Menu_Bold_Outline，不在这里改。
/// </summary>
[DisallowMultipleComponent]
public class TextMenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("文字")]
    [Tooltip("会被缩放的文字节点（留空则用 label 自己）")]
    public RectTransform labelRoot;
    public TMP_Text label;

    [Tooltip("未选中：白色")]
    public Color normalColor = new Color32(0xFA, 0xFA, 0xF6, 0xFF);
    [Tooltip("选中：金色")]
    public Color highlightColor = new Color32(0xFF, 0xD8, 0x88, 0xFF);

    [Tooltip("悬停时的放大倍率（只轻微放大）")]
    public float highlightScale = 1.06f;

    [Header("指示标")]
    public RectTransform hintLeft;
    public RectTransform hintRight;
    [Tooltip("按文字实际宽度把指示标贴到文字两侧（关掉就用场景里摆的位置）")]
    public bool autoPlaceHints = true;
    [Tooltip("落点：文字边缘到指示标内边缘的距离")]
    public float hintGap = 14f;
    [Tooltip("指示标从这里「生成」，再迅速贴回落点")]
    public float hintOffset = 52f;
    [Tooltip("贴回落点用时（越短越急）")]
    public float hintInTime = 0.22f;
    [Tooltip("退出（退回 + 淡出）用时")]
    public float hintOutTime = 0.14f;

    [Header("速度")]
    [Tooltip("变色 / 缩放速度，越大越快")]
    public float colorSpeed = 16f;

    bool _highlighted;
    float _p;                       // 0 = 隐藏在生成位置, 1 = 已贴回落点
    float _hintWidth = 56f;

    Vector3 _labelBaseScale = Vector3.one;
    Vector2 _hintLeftBase;
    Vector2 _hintRightBase;
    Image _hintLeftImage;
    Image _hintRightImage;

    void Awake()
    {
        if (labelRoot == null && label != null) labelRoot = label.rectTransform;
        if (labelRoot != null) _labelBaseScale = labelRoot.localScale;

        // 引用丢了也能自愈：按名字找子节点
        if (hintLeft == null) hintLeft = FindHint("HintLeft");
        if (hintRight == null) hintRight = FindHint("HintRight");

        if (hintLeft != null)
        {
            _hintLeftBase = hintLeft.anchoredPosition;
            _hintWidth = hintLeft.rect.width > 1f ? hintLeft.rect.width : 56f;
            _hintLeftImage = hintLeft.GetComponent<Image>();
        }
        if (hintRight != null)
        {
            _hintRightBase = hintRight.anchoredPosition;
            _hintRightImage = hintRight.GetComponent<Image>();
        }

        if (autoPlaceHints && label != null)
        {
            label.ForceMeshUpdate();
            float half = label.preferredWidth * 0.5f + hintGap + _hintWidth * 0.5f;
            _hintLeftBase = new Vector2(-half, _hintLeftBase.y);
            _hintRightBase = new Vector2(half, _hintRightBase.y);
        }

        ApplyInstant(false);
    }

    RectTransform FindHint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t as RectTransform : null;
    }

    void OnDisable()
    {
        _highlighted = false;
        ApplyInstant(false);
    }

    public void OnPointerEnter(PointerEventData eventData) { _highlighted = true; }
    public void OnPointerExit(PointerEventData eventData)  { _highlighted = false; }
    public void OnSelect(BaseEventData eventData)          { _highlighted = true; }
    public void OnDeselect(BaseEventData eventData)        { _highlighted = false; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float tc = 1f - Mathf.Exp(-colorSpeed * dt);

        if (label != null)
            label.color = Color.Lerp(label.color, _highlighted ? highlightColor : normalColor, tc);

        if (labelRoot != null)
            labelRoot.localScale = Vector3.Lerp(labelRoot.localScale,
                _labelBaseScale * (_highlighted ? highlightScale : 1f), tc);

        float target = _highlighted ? 1f : 0f;
        float dur = _highlighted ? hintInTime : hintOutTime;
        _p = dur > 0.0001f ? Mathf.MoveTowards(_p, target, dt / dur) : target;

        // 缓出：起步就冲出去，越接近落点越慢
        float e = EaseOutCubic(_p);
        ApplyHint(e, e);
    }

    /// <summary>1 - (1-t)^3：t=0.33 时已经走完 70% 的路程。</summary>
    static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }

    /// <summary>不做插值，直接落到某个状态（Awake / OnDisable 用）。</summary>
    void ApplyInstant(bool highlighted)
    {
        _p = highlighted ? 1f : 0f;
        float e = EaseOutCubic(_p);

        if (label != null) label.color = highlighted ? highlightColor : normalColor;
        if (labelRoot != null) labelRoot.localScale = _labelBaseScale * (highlighted ? highlightScale : 1f);
        ApplyHint(e, e);
    }

    void ApplyHint(float alpha, float shown)
    {
        if (_hintLeftImage != null)
        {
            Color c = _hintLeftImage.color; c.a = alpha; _hintLeftImage.color = c;
        }
        if (_hintRightImage != null)
        {
            Color c = _hintRightImage.color; c.a = alpha; _hintRightImage.color = c;
        }

        float off = hintOffset * (1f - shown);
        if (hintLeft != null)
            hintLeft.anchoredPosition = new Vector2(_hintLeftBase.x - off, _hintLeftBase.y);
        if (hintRight != null)
            hintRight.anchoredPosition = new Vector2(_hintRightBase.x + off, _hintRightBase.y);
    }
}
