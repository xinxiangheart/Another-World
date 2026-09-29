using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>大厅「卡牌详情」右栏那层「拖空白也能滚」的透明面专用：**单击**时问面板「这点压在底板上没有」，
/// 没压在 ⇒ 当点空白处理，走幕那条关闭路径。</summary>
/// <remarks>2026-09-29 用户：「右侧空白区域点击取消的范围应和文字背景占地对应，现在右边若文本较少
/// 点击右边下方空白不会退出详情」。
///
/// 病灶：右栏那层透明面（<c>ScrollRect</c> 的视野）是**铺满整条右栏**的 raycast target，为了「拖空白也能滚」。
/// 它在幕之上，于是那一大片压根到不了底下幕上的 <c>Button</c> —— 文本一短，右边 / 下面空着一大块，
/// 点了没反应。现在把这一层从「只吸拖拽」改成「拖 = 滚，点 = 判落点」：
///   · 落点在某一栏底板的矩形里（底板 = 文字背景，见 <c>LobbyCardDetailPanel.AddRow</c> / <c>AddTraitRow</c>）⇒ 什么也不做；
///   · 落点不在 ⇒ 当点空白，关详情。
///
/// **拖完抬手不会误关**：拖动一越过阈值，EventSystem 就会 `eligibleForClick = false`
/// （`PointerInputModule.ProcessDrag`，条件 `pointerPress != pointerDrag` —— `ScrollRect` 不实现
/// `IPointerDownHandler`，所以这两个必然不等），这条与卡牌总览里 `LobbyCardTile` 依赖的是同一个机制。
/// </remarks>
[DisallowMultipleComponent]
public class LobbyColumnClick : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("由 LobbyCardDetailPanel.Build 填：右栏视图口那件")]
    public LobbyCardDetailPanel panel;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (panel == null) return;
        if (panel.PointOverAnyRow(eventData.position)) return;   // 压在某栏底板上 ⇒ 不算点空白
        panel.Close();
    }
}
