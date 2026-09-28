using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>卡牌总览左栏那一格筛选格：常态 / 悬停 / 选中三态换板贴图。</summary>
/// <remarks>2026-09-28：用户「左边一栏默认选中全部（这些选择格子都是一个 ui + 文字，有子背景以及选中变化）」。
///
/// 与好友详情那四格（<see cref="LobbyFriendTab"/>）**同一套**：三张板只差色调、形体尺寸完全一致，切图不跳位。
/// 差别只有两处：
///   1 板是 LobbyChip_Filter*（屏幕 108x52，Tools/cardframe/LobbyFilterChipV1.ps1 出）；
///   2 徽记这格没有 —— 筛选格只有 ui + 文字，所以本组件比 LobbyFriendTab 少一个 emblem。
///
/// 点击**不挂 Button**：本组件自己实现 IPointerClickHandler（同物体上再挂 Button 会两个处理器各触发一次，
/// 见 LobbyFriendTab 的备注）。raycast 目标是子物体 Chip 那张 RawImage，文字关掉 raycastTarget 后
/// 落在字上也会冒泡到本组件。
/// 悬停只有在 Play 模式下才触发（EventSystem 不进编辑模式）。
/// </remarks>
public class LobbyFilterChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("引用")]
    public RawImage plate;
    public TextMeshProUGUI label;

    [Header("三态板贴图（由 LobbyCardCollectionPanel 填）")]
    public Texture normalTexture;
    public Texture hoverTexture;
    public Texture onTexture;

    [Header("配色 —— 常态色就是板上文字的实际初值口径")]
    public Color normalColor = new Color32(240, 232, 210, 236);      // 奶油 #F0E8D2
    public Color goldColor = new Color32(228, 203, 132, 255);        // 本套亮金 #E4CB84

    [Header("点击回调（由面板填：带自己的标签）")]
    public System.Action onClick;

    bool _hover;

    /// <summary>是不是选中态（由面板 RefreshChips 置）。</summary>
    public bool IsOn { get; private set; }

    void Awake()
    {
        if (plate == null) plate = GetComponentInChildren<RawImage>(true);
    }

    void OnDisable() { _hover = false; }

    public void SetOn(bool on) { IsOn = on; Apply(); }

    public void SetHover(bool hover) { _hover = hover; Apply(); }

    void Apply()
    {
        if (plate != null)
        {
            Texture t = IsOn ? onTexture : (_hover && hoverTexture != null ? hoverTexture : normalTexture);
            if (t != null) plate.texture = t;
        }
        if (label != null) label.color = (IsOn || _hover) ? goldColor : normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData) { SetHover(true); }
    public void OnPointerExit(PointerEventData eventData) { SetHover(false); }
    public void OnPointerClick(PointerEventData eventData) { if (onClick != null) onClick(); }
}
