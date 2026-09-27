using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>好友详情「好友列表」一行右边那三格动作（拉黑 / 删除 / 邀请）。</summary>
public enum FriendRowActionKind
{
    Block = 0,     // 拉黑
    Delete = 1,    // 删除
    Invite = 2,    // 邀请（仅「空闲在线」那一档出现）
}

/// <summary>行右端一个小图标动作：悬停换贴图、点击转给 <see cref="FriendDetailRowUI"/>。</summary>
/// <remarks>2026-09-27：用户「右边分别是当前状态……拉黑，删除，（仅在在线状态下）邀请，拉黑删除和邀请都是小ui图案代替文字」。
///
/// **不挂 Button** —— 同物体上再挂一个 Button 就会有两个 IPointerClickHandler 各触发一次
/// （与好友表头那颗「+」、左侧四个 tab 同一条坑）。raycast 目标就是这张 RawImage。
/// 悬停只有在 Play 模式下才触发（EventSystem 不进编辑模式）。
/// </remarks>
public class FriendRowAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("这一格是哪个动作")] public FriendRowActionKind kind;

    [Header("图标（256 贴图 = 屏幕 44x44）")] public RawImage icon;
    public Texture normalTexture;
    public Texture hoverTexture;

    [Header("点下去交给哪一行")] public FriendDetailRowUI row;

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
        if (row != null) row.Action(kind);
    }
}