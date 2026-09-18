using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 槽位选择指示：选择模式下，在「允许被选择」的槽位四角画四个直角括号，并做呼吸式放大缩小，
/// 提示玩家该格可以点。判据唯一取自 <see cref="BoardSlot.CanBeSelected"/>——
/// 只问「这一格此时此刻允不允许被选择」，与格上是空格还是有卡牌无关。
///
/// 颜色随选择语义变（见 SelectionKindRules / SelectionManager.CurrentKind）：
/// 伤害红、治愈绿、减益紫、其余（放置 / 移动 / 加护盾 / 位置调整等）金。
///
/// 由 BoardSlot 运行时创建（不依赖美术资源：括号用 8 个无 sprite 的 Image 拼成硬边矩形），
/// 父级挂在槽位自身之下，因此随槽位一起移动 / 缩放。
/// 注意：指示器不能直接改 slotImage.color——槽位底色还背着封锁 / 囚牢 / 瘟疫 / 抛置等多套
/// 优先级状态，所以这里用独立子物体，绝不参与那套着色。
/// </summary>
[DisallowMultipleComponent]
public class SlotSelectionIndicator : MonoBehaviour
{
    /// <summary>兜底色（中性 / 非选择模式）：金色。</summary>
    public static readonly Color IndicatorColor = SelectionKindRules.NeutralColor;

    // 括号相对槽位矩形的尺寸（世界单位）。
    // 棋盘卡牌与槽位矩形几乎同尺寸（槽位 1.25×2.22，卡面 ≈1.26×2.24），所以括号必须画在槽位之外，
    // 否则会被格上的卡牌整块盖住；Gap 取小值，尽量只探出卡片一点点。
    const float Gap = 0.045f;  // 括号内侧与槽位边缘的间距
    const float Arm = 0.18f;   // 每条直角的臂长
    const float Bar = 0.018f;  // 描边粗细：细到接近一条线（卡面上的线条感），不再是一根粗条

    // 呼吸式脉动：周期 1.2s，幅度 0 → +0.16 倍
    const float PulsePeriod = 1.2f;
    const float PulseAmplitude = 0.16f;

    // 指示层必须比槽位更靠近相机，否则会被格上的 3D 卡牌挡住：
    // 相机在 z=-16.22 看向 +Z，越负越近；槽位 z=-5.6，卡牌 z=-5.7（比槽位近 0.1）。
    const float ForwardOffset = 0.35f;

    BoardSlot _slot;
    RectTransform _root;   // 常驻（本组件所在物体，不随显隐开关，否则 LateUpdate 会被停掉）
    RectTransform _bars;   // 8 根括号的容器，显隐 + 缩放都作用在它身上
    readonly List<Image> _barImages = new List<Image>();
    Color _appliedColor = SelectionKindRules.NeutralColor;
    bool _visible;
    float _pulseTime;

    /// <summary>在槽位下挂一个指示器。slotSize = 槽位矩形的世界尺寸（宽 × 高）。</summary>
    public static SlotSelectionIndicator AttachTo(BoardSlot slot, Vector2 slotSize)
    {
        if (slot == null) return null;
        GameObject go = new GameObject("SelectionIndicator", typeof(RectTransform));
        go.transform.SetParent(slot.transform, false);
        SlotSelectionIndicator indicator = go.AddComponent<SlotSelectionIndicator>();
        indicator.Build(go.GetComponent<RectTransform>(), slotSize);
        indicator._slot = slot;
        return indicator;
    }

    void Build(RectTransform root, Vector2 slotSize)
    {
        _root = root;
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = Vector2.zero;
        _root.localPosition = new Vector3(0f, 0f, -ForwardOffset);
        _root.localRotation = Quaternion.identity;
        _root.localScale = Vector3.one;

        GameObject barsObj = new GameObject("Bars", typeof(RectTransform));
        barsObj.transform.SetParent(_root, false);
        _bars = barsObj.GetComponent<RectTransform>();
        _bars.anchorMin = _bars.anchorMax = new Vector2(0.5f, 0.5f);
        _bars.pivot = new Vector2(0.5f, 0.5f);
        _bars.anchoredPosition = Vector2.zero;
        _bars.sizeDelta = slotSize;
        _bars.localRotation = Quaternion.identity;
        _bars.localScale = Vector3.one;

        float hx = slotSize.x * 0.5f + Gap;  // 括号中心线到槽位中心的水平距离
        float hy = slotSize.y * 0.5f + Gap;  // 竖直同理
        float a = Arm * 0.5f;

        // 四个直角括号：每个角一横一竖，构成 ⌐ ¬ L ⌐ 的直角
        AddBar(new Vector2(-hx + a, hy), new Vector2(Arm, Bar));  // 左上 · 横
        AddBar(new Vector2(-hx, hy - a), new Vector2(Bar, Arm));  // 左上 · 竖
        AddBar(new Vector2(hx - a, hy), new Vector2(Arm, Bar));   // 右上 · 横
        AddBar(new Vector2(hx, hy - a), new Vector2(Bar, Arm));   // 右上 · 竖
        AddBar(new Vector2(-hx + a, -hy), new Vector2(Arm, Bar)); // 左下 · 横
        AddBar(new Vector2(-hx, -hy + a), new Vector2(Bar, Arm)); // 左下 · 竖
        AddBar(new Vector2(hx - a, -hy), new Vector2(Arm, Bar));  // 右下 · 横
        AddBar(new Vector2(hx, -hy + a), new Vector2(Bar, Arm));  // 右下 · 竖

        _bars.gameObject.SetActive(false);
        _visible = false;
    }

    void AddBar(Vector2 anchoredPos, Vector2 size)
    {
        GameObject barObj = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = barObj.GetComponent<RectTransform>();
        rt.SetParent(_bars, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        Image img = barObj.GetComponent<Image>();
        img.sprite = null;          // 无 sprite → 纯色硬边矩形（与槽位高亮同一套平涂语言）
        img.color = IndicatorColor;
        img.raycastTarget = false;  // 绝不抢槽位自己的悬停 / 点击
        img.maskable = false;
        img.useSpriteMesh = false;
        _barImages.Add(img);
    }

    void LateUpdate()
    {
        if (_slot == null || _bars == null) return;

        // 只在「允许被选择 且 鼠标（或拖拽）正停在格上」时出现——即旧版高亮的悬停反馈，
        // 「哪些格能选」由 SelectionDim 的压暗表达（合法目标保持原色、其余压暗）。
        bool show = _slot.CanBeSelected() && _slot.IsSelectionHovered;
        if (show != _visible)
        {
            _visible = show;
            _bars.gameObject.SetActive(show);
            if (show) _pulseTime = 0f; // 每次重新出现都从最小尺寸起，形成一次明显的放大
        }
        if (!_visible) return;

        // 变色：伤害红 / 治愈绿 / 减益紫 / 中性金（选择类型由 SelectionManager 记录，见 SelectionKindRules）
        // 抛置悬停提示走绿色（与格子绿色高亮同一含义），其余按选择类型：伤害红 / 治愈绿 / 减益紫 / 中性金
        Color color = _slot.IsDiscardHinted
            ? SelectionKindRules.DiscardColor
            : SelectionKindRules.ColorOf(SelectionManager.CurrentKind);
        if (color != _appliedColor)
        {
            _appliedColor = color;
            for (int i = 0; i < _barImages.Count; i++) _barImages[i].color = color;
        }

        // 呼吸式放大缩小（0 → +PulseAmplitude），提示「这一格现在可以点」
        _pulseTime += Time.deltaTime;
        float phase = _pulseTime * (2f * Mathf.PI / PulsePeriod);
        float scale = 1f + PulseAmplitude * (0.5f - 0.5f * Mathf.Cos(phase));
        _bars.localScale = new Vector3(scale, scale, 1f);
    }
}
