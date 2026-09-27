using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 好友行右端那个「邀请加号」的悬停 / 点击（2026-09-27）。
/// </summary>
/// <remarks>
/// 用户 2026-09-27：「好友栏中处于在线（非战斗状态和匹配状态）时其名字右边会出现一个加号，点击后发送邀请
/// 并且加号变成 10 秒倒计时」。
///
/// 为什么不复用 <see cref="LobbyIconHover"/>：那个的 OnPointerClick 里带着「把好友侧边栏收回去 / 弹占位窗」
/// 两个副作用（它服务的是压墙图标）—— 挂在加号上会把玩家刚点开的好友栏关掉。这里只要「悬停换图 + 点击转交」。
///
/// 贴图不由本组件持有：三张（加号 / 悬停 / 倒计时那块空板）都在 <see cref="owner"/> 上，
/// 由 <see cref="FriendRowUI.ApplyInviteVisual"/> 按「是不是在冷却 + 是不是悬停」选一张 —— 一处说了算，
/// 免得两边各自记状态、在冷却中途悬停一次就把加号又画回来。
///
/// 点击落点：本物体是那行 Button 的子物体，且兄弟序在最后 —— EventSystem 从命中物体往上找第一个
/// IPointerClickHandler，找到的就是这里，所以点加号**不会**顺带触发整行的点击（那行现在没有监听，留着备用）。
/// </remarks>
public class FriendInviteButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("加号贴图（这张 RawImage 自己就是 raycast 目标）")] public RawImage icon;
    [Header("归属（点了之后由它去发邀请）")] public FriendRowUI owner;

    bool _hover;

    /// <summary>鼠标是否停在这块上（冷却期间不画悬停态，但状态照记）。</summary>
    public bool Hovering { get { return _hover; } }

    void Awake() { if (icon == null) icon = GetComponent<RawImage>(); }

    // 别在这里回填贴图：本物体就是那一格本身，OnDisable 一响说明它刚被藏起来 ——
    // 而 owner.ApplyInviteVisual() 看到 _canInvite 为真会立刻把它 SetActive(true)，藏不住（递归）。
    // 悬停状态在 OnPointerExit 里就已经清了，回填交给 FriendRowUI 每帧那次。
    void OnDisable() { _hover = false; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hover = true;
        if (owner != null) owner.ApplyInviteVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hover = false;
        if (owner != null) owner.ApplyInviteVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null) owner.InviteClicked();
    }
}
