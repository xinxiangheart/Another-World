using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 输入栏**右键 = 粘贴**（2026-09-27：「加入一个在输入栏右键自动粘贴复制的房间号功能」）。
/// </summary>
/// <remarks>
/// TMP_InputField 自己**只认左键** —— `OnPointerClick` 第一行就把非左键 return 掉（实测
/// `com.unity.textmeshpro@3.0.7`），键盘那套 Ctrl+V / Shift+Insert 是有的事后，右键一直是空的。
/// 本件就补这一下：右键 ⇒ 交给 <see cref="owner"/>（<see cref="LobbyJoinSidebar.PasteFromClipboard"/>）
/// 去读剪贴板、清洗、贴进井里。
///
/// 本件**只转交、不碰房间号规则** —— 「只收大写字母 / 数字、≤6 位、贴完自动搜」那套全在
/// LobbyJoinSidebar 里，和手输走同一条 `onValueChanged` 通路。以后另一口井（比方说好友 ID）
/// 只要挂一个本件 + 指一个 owner 就行。
/// </remarks>
public class LobbyJoinInputPaste : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("真正干活的那个侧边栏（留空 = 往上层找）")]
    public LobbyJoinSidebar owner;

    void Awake()
    {
        if (owner == null) owner = GetComponentInParent<LobbyJoinSidebar>(true);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Right) return;   // 左键照旧留给 TMP 定位光标
        if (owner != null) owner.PasteFromClipboard();
        else Debug.LogWarning("[LobbyJoinInputPaste] 没找到 LobbyJoinSidebar —— 右键粘贴这一下没接上");
    }
}
