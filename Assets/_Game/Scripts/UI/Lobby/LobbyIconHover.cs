using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>大厅占位图标：悬停换「悬停贴图」、移开换回「常态贴图」，点击弹占位弹窗。</summary>
/// <remarks>2026-09-26：服务 LobbyUI_v1 里那五个「压墙」图标（好友 / 商城 / 活动 / 教程 / 邮件）。
/// 两张贴图由 Tools/cardframe/LobbyUIv1.ps1 出，**只差色调**（石面提亮 + 金线 GOLD→GOLD_L），
/// 形体 / 尺寸完全一致，所以切换时不会跳位。没有 Button —— 直接用 IPointer* 接口，
/// 图标自己的 RawImage 就是 raycast 目标。悬停只有在 Play 模式下才触发（EventSystem 不进编辑模式）。
/// 2026-09-27：加**弹窗提示行**。好友图标曾勾 <see cref="showMyId"/> —— 点击时把提示行换成
/// 「我的 ID：P00-…」（<c>SteamDataManager.PlayerIdLabel</c>），那是玩家看到并抄下自己号的地方。
/// 2026-09-27 同日再改：好友图标改挂 <see cref="friendsPanel"/> —— 点击不再弹占位窗，改成开 / 关
/// 左侧的好友侧边栏（<see cref="LobbyFriendPanel"/>）；「我的 ID」那行随占位窗一起退出这个入口。</remarks>
public class LobbyIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("图标")] public RawImage icon;
    [Header("常态 / 悬停贴图")] public Texture normalTexture;
    public Texture hoverTexture;

    [Header("点击")] public LobbyPopup popup;
    public string title = "占位";
    [Tooltip("弹窗提示行；留空 = 用弹窗自己那份默认提示")]
    public string hint;

    [Header("好友图标勾它：点击时提示行换成「我的 ID：…」")]
    public bool showMyId;

    [Header("好友图标勾它：点击改成开 / 关左侧的好友侧边栏（勾了就不再弹占位窗）")]
    public LobbyFriendPanel friendsPanel;

    [Header("房间面板右上角那个加入图标勾它：点击改成开 / 关右侧的「加入房间」侧边栏")]
    public LobbyJoinSidebar joinSidebar;

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
        // 好友图标：不弹占位窗，改成开 / 关左侧的好友侧边栏（2026-09-27 用户：点好友从屏幕左侧滑出侧边栏）
        if (friendsPanel != null) { friendsPanel.Toggle(); return; }
        if (joinSidebar != null) { joinSidebar.Toggle(); return; }     // 「加入房间」：同上，改成开 / 关右侧侧边栏

        // 别的图标弹占位窗之前，把好友侧边栏收回去 —— 两块抢同一块屏幕
        if (LobbyFriendPanel.Instance != null) LobbyFriendPanel.Instance.Close();

        if (popup == null) return;
        popup.Show(title, showMyId ? MyIdLabel() : hint);
    }

    /// <summary>「我的 ID：P00-20260927-482-6　·　创建时赛季 00 · 2026-09-27」——
    /// SteamDataManager 还没起来时给一句占位，不显示空白行。</summary>
    public static string MyIdLabel()
    {
        var sd = SteamDataManager.Instance;
        string label = sd != null ? sd.PlayerIdLabel : "";
        return string.IsNullOrEmpty(label) ? "我的 ID：读取中…" : label;
    }
}
