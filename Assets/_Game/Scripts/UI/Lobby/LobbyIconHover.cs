using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>大厅占位图标：悬停换「悬停贴图」、移开换回「常态贴图」，点击弹占位弹窗。</summary>
/// <remarks>2026-09-26：只服务 LobbyUI_v1 里那四个「压墙」图标（好友 / 商城 / 活动 / 教程）。
/// 两张贴图由 Tools/cardframe/LobbyUIv1.ps1 出，**只差色调**（石面提亮 + 金线 GOLD→GOLD_L），
/// 形体 / 尺寸完全一致，所以切换时不会跳位。没有 Button —— 直接用 IPointer* 接口，
/// 图标自己的 RawImage 就是 raycast 目标。悬停只有在 Play 模式下才触发（EventSystem 不进编辑模式）。</remarks>
public class LobbyIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("图标")] public RawImage icon;
    [Header("常态 / 悬停贴图")] public Texture normalTexture;
    public Texture hoverTexture;
    [Header("点击")] public LobbyPopup popup;
    public string title = "占位";

    void Awake()
    {
        if (icon == null) icon = GetComponent<RawImage>();
    }

    void OnDisable()
    {
        if (icon != null && normalTexture != null) icon.texture = normalTexture;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (icon != null && hoverTexture != null) icon.texture = hoverTexture;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (icon != null && normalTexture != null) icon.texture = normalTexture;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (popup != null) popup.Show(title);
    }
}