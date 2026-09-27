using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 文字按钮的**子背景**悬停换贴图（踢出 / 开始游戏）：移进去换悬停底、移开换回常态底。
/// </summary>
/// <remarks>2026-09-27：用户「踢出和开始游戏是有个子背景的」—— 两张底由 Tools/cardframe/LobbyRoomChipV1.ps1 出，
/// **只差色调**（石面提亮 + 金线 GOLD→GOLD_L），形体尺寸完全一致，所以切换不跳位。
///
/// 为什么不复用 <see cref="LobbyIconHover"/>：那个的 OnPointerClick 会顺手把好友侧边栏收回去（它服务的是压墙图标），
/// 挂在按钮上会多出一个不该有的副作用 —— 这里只要悬停换底，点击完全归 Button。
/// </remarks>
public class LobbyChipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("子背景（那块底的 RawImage）")] public RawImage chip;
    [Header("常态 / 悬停贴图")] public Texture normalTexture;
    public Texture hoverTexture;

    void Awake() { if (chip == null) chip = GetComponentInChildren<RawImage>(true); }

    void OnDisable() { if (chip != null && normalTexture != null) chip.texture = normalTexture; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (chip != null && hoverTexture != null) chip.texture = hoverTexture;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (chip != null && normalTexture != null) chip.texture = normalTexture;
    }
}
