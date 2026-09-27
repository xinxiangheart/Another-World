using UnityEngine;
using TMPro;

/// <summary>
/// 大厅「子全屏弹窗」：铺满全屏的一层，用 common-bg-v1 的通用背景 + 标题 + 右上角那个通用关闭叉。
/// </summary>
/// <remarks>2026-09-27：用户「现在做战斗的子全屏弹窗，通用背景，先生成一个通用的叉ui图标，用于关闭弹窗」+
/// 「左上角和右上角的显示是在那些全屏显示的弹窗界面中仍显示在屏幕上（除非明确说明隐藏这些）的」。
///
/// **HUD 常驻**这条不靠本组件实现，靠场景层级：Canvas 下最后一个子物体是 Layer_Hud_v1
/// （里面是 Plate_Profile + Plate_TopBand），Layer_Sub_v1 在它前面 —— 同层同级，后面的画在上面，
/// 所以全屏面板永远压在 HUD 之下、HUD 永远可见。本组件只负责「开 / 关 / 抬到本层最上面」。
/// 例外那半句走 <see cref="hideHudOnOpen"/>：勾上就把 HUD 层整个 SetActive(false)，关窗时还原。
///
/// 关闭叉 = Icon_Close.png（Tools/cardframe/LobbyUIv1.ps1 出的通用件）；本组件不持有它，
/// 叉子上挂的是 Button → Close()（编辑器里连的持久监听），悬停换贴图由同一物体上的 LobbyIconHover 做。
/// 根节点在场景里**存成 active**（方便在编辑器里直接看版式 / 拖里面的东西），运行时由 Start 的
/// <see cref="closeOnStart"/> 自己关掉，再由入口板（LobbyPlateHover.subPanel）Open。
/// </remarks>
public class LobbySubPanel : MonoBehaviour
{
    [Header("标题")]
    public TextMeshProUGUI titleText;

    [Header("HUD 例外（默认不勾 = HUD 压在面板之上、一直可见）")]
    [Tooltip("勾上 = 这个面板打开时把整个 HUD 层藏掉，关窗时还原。")]
    public bool hideHudOnOpen = false;
    [Tooltip("要藏的 HUD 根节点（一般就是 Layer_Hud_v1）。")]
    public GameObject hudLayer;

    [Header("场景里默认可见（只在编辑器里看和调）")]
    [Tooltip("勾上 = 面板在场景里是**开着**的，运行时 Start 自己关掉。这样在编辑器里就能直接看到版式、拖里面的东西，不用进 Play。")]
    public bool closeOnStart = true;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    void Start()
    {
        if (closeOnStart) gameObject.SetActive(false);
    }

    /// <summary>开窗。可带一个新标题（不传就保持场景里那个）。</summary>
    public void Open(string title = null)
    {
        if (titleText != null && !string.IsNullOrEmpty(title)) titleText.text = title;

        if (hideHudOnOpen && hudLayer != null) hudLayer.SetActive(false);

        gameObject.SetActive(true);
        // 同一层里后开的压在上面 —— HUD 层是 Canvas 的最后一个子物体，这里抬不出这一层，动不到 HUD
        transform.SetAsLastSibling();
    }

    /// <summary>无参版本：给 Button 的持久监听用（UnityEvent 绑不了带默认值的方法）。</summary>
    public void OpenSelf() { Open(null); }

    public void Close()
    {
        gameObject.SetActive(false);
        if (hideHudOnOpen && hudLayer != null) hudLayer.SetActive(true);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open(null);
    }
}