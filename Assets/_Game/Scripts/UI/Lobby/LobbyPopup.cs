using UnityEngine;
using TMPro;

/// <summary>大厅占位弹窗：只做「显示 / 隐藏 + 换标题」，内容等真实面板接进来。</summary>
/// <remarks>2026-09-26：四张「压墙」图标点开的是**同一个**占位弹窗，只换标题（好友 / 商城 / 活动 / 教程）。
/// 根节点由 Assets/_Game/Editor/LobbyUIBuilder.cs 生成并存成 inactive；遮罩与关闭按钮的 onClick
/// 是编辑器里加的持久监听（都指向 Hide）。</remarks>
public class LobbyPopup : MonoBehaviour
{
    [Header("标题")] public TextMeshProUGUI titleText;

    public void Show(string title)
    {
        if (titleText != null) titleText.text = title;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}