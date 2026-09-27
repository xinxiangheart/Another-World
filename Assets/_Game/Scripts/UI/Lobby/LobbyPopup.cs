using UnityEngine;
using TMPro;

/// <summary>大厅占位弹窗：只做「显示 / 隐藏 + 换标题 / 换提示行」，内容等真实面板接进来。</summary>
/// <remarks>2026-09-26：五个「压墙」图标点开的是**同一个**占位弹窗，只换标题（好友 / 商城 / 活动 / 教程 / 邮件）。
/// 根节点由 Assets/_Game/Editor/LobbyUIBuilder.cs 生成并存成 inactive；遮罩与关闭按钮的 onClick
/// 是编辑器里加的持久监听（都指向 Hide）。
/// 2026-09-27：加**提示行** <see cref="hintText"/> —— 好友图标拿它显示「我的 ID：P00-…」。
/// 调用方不传 hint（或传空）时还原成编辑器里那份默认提示，所以几个图标共用同一个弹窗也不会串味。</remarks>
public class LobbyPopup : MonoBehaviour
{
    [Header("标题")] public TextMeshProUGUI titleText;
    [Header("提示行（可空）")] public TextMeshProUGUI hintText;

    string _defaultHint;
    bool _defaultHintCaptured;

    public void Show(string title) { Show(title, null); }

    /// <summary>title 空 = 不改标题；hint 空 = 还原默认提示行。</summary>
    public void Show(string title, string hint)
    {
        CaptureDefaultHint();
        if (titleText != null && !string.IsNullOrEmpty(title)) titleText.text = title;
        if (hintText != null) hintText.text = string.IsNullOrEmpty(hint) ? _defaultHint : hint;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>第一帧就把编辑器里的提示行原文记下来 —— 之后每次 Show 都能还原。
    /// （弹窗在场景里是 inactive，Awake 要等第一次 SetActive 才跑，所以不能放 Awake。）</summary>
    void CaptureDefaultHint()
    {
        if (_defaultHintCaptured) return;
        _defaultHintCaptured = true;
        if (hintText != null) _defaultHint = hintText.text;
    }
}
