using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// HoverTagLabel — 3D 召唤物悬停标签（单个文本标签）。
// 结构（TagLabel.prefab）：TagLabel(BG Image Sliced) → Text(TMP)，
//   根上挂 ContentSizeFitter（布局由本组件手动驱动，故运行时禁用 CSF）。
// SetText 负责：
//   - 单行自然宽 ≤ tagMaxWidth → 单行显示，BG 紧贴文字
//   - 超过 tagMaxWidth → 开启自动换行并按 maxWidth 撑高，BG 高度自适应
//   - tagPadding 控制文字相对 BG 的内边距
// 测量用 TMP.GetPreferredValues（跨版本稳定），不依赖 CSF 布局。
// ============================================================================

public class HoverTagLabel : MonoBehaviour
{
    [Tooltip("BG 背景 Image（Sliced 九宫格）")]
    public Image bgImage;
    [Tooltip("正文 TextMeshProUGUI")]
    public TMP_Text labelText;

    [Header("换行/内边距（SetText 运行时覆盖）")]
    [Tooltip("文字超过该宽自动换行（0=不换行）")]
    public float tagMaxWidth = 260f;
    [Tooltip("文字相对 BG 的内边距")]
    public Vector2 tagPadding = new Vector2(10f, 6f);

    RectTransform _rt;
    RectTransform _textRT;

    void Awake()
    {
        _rt = (RectTransform)transform;
        if (bgImage == null) bgImage = GetComponentInChildren<Image>(true);
        if (labelText == null) labelText = GetComponentInChildren<TMP_Text>(true);
        if (labelText != null) _textRT = (RectTransform)labelText.transform;

        // BG 撑满根（文字四周留 tagPadding）：文字铺满根、偏移 = pad。
        // 手动驱动尺寸，避免 ContentSizeFitter 与 textBounds 双重计算打架。
        var csf = GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;
        if (bgImage != null) bgImage.raycastTarget = false;
        if (labelText != null) labelText.raycastTarget = false; // 标签不拦截点击/悬停
    }

    /// <summary>设置文本并按文字内容调整尺寸。text 为 null/空 → 返回 false（调用方不显示本标签）。</summary>
    public bool SetText(string text)
    {
        if (labelText == null) return false;
        if (string.IsNullOrEmpty(text)) { labelText.text = ""; return false; }

        float padX = Mathf.Max(0f, tagPadding.x * 2f);
        float padY = Mathf.Max(0f, tagPadding.y * 2f);
        float maxContentW = tagMaxWidth > padX ? tagMaxWidth - padX : tagMaxWidth; // 内容可用宽

        // ① 测自然宽（禁 wrap）：GetPreferredValues(宽∞) = 单行宽 + 单行高。
        labelText.enableWordWrapping = false;
        labelText.text = text;
        Vector2 natural = labelText.GetPreferredValues(text, float.MaxValue, float.MaxValue);
        bool wrap = maxContentW > 0f && natural.x > maxContentW;
        float contentW, contentH;
        if (wrap)
        {
            // ② 超宽 → 换行：按 maxContentW 约束测换行后高度。
            labelText.enableWordWrapping = true;
            Vector2 wrapped = labelText.GetPreferredValues(text, maxContentW, float.MaxValue);
            contentW = maxContentW;
            contentH = wrapped.y;
        }
        else
        {
            contentW = natural.x;
            contentH = natural.y;
        }

        // ③ 根尺寸 = 内容 + pad；文字铺满根、四周留 pad（文字区域自动 = 内容宽，换行一致）。
        float totalW = contentW + padX;
        float totalH = contentH + padY;
        if (_rt != null) _rt.sizeDelta = new Vector2(totalW, totalH);
        if (_textRT != null)
        {
            _textRT.anchorMin = Vector2.zero;
            _textRT.anchorMax = Vector2.one;
            _textRT.pivot = new Vector2(0.5f, 0.5f);
            _textRT.offsetMin = new Vector2(tagPadding.x, tagPadding.y);
            _textRT.offsetMax = new Vector2(-tagPadding.x, -tagPadding.y);
        }
        return true;
    }

    /// <summary>当前渲染宽高（根 RectTransform 尺寸）。</summary>
    public Vector2 GetSize()
        => _rt != null ? _rt.sizeDelta : Vector2.zero;
}
