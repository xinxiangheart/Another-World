using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 「添加好友」那口井**右键 = 粘贴**（2026-09-27：用户对房间号那口井提过「加入一个在输入栏右键自动粘贴复制的
/// 房间号功能」，这次「添加好友」同样要 ——「输入框可以之间输入id或者昵称（加入右键粘贴id功能）」）。
/// </summary>
/// <remarks>
/// TMP_InputField 自己**只认左键** —— 它的 OnPointerClick 第一行就把非左键 return 掉（实测
/// com.unity.textmeshpro@3.0.7），键盘那套 Ctrl+V / Shift+Insert 是有的事后，右键一直是空的。
///
/// 与房间号那件（LobbyJoinInputPaste）是**同一套写法**，只是 owner 换成
/// FriendAddSearchUI（那边硬绑 LobbyJoinSidebar）—— 两件各自独立，改一边不会动到另一边。
///
/// 本件**只转交、不碰查询规则** —— 清洗 / 去空白 / 立刻开搜全在 FriendAddSearchUI 里，
/// 与手打进去走同一条 onValueChanged 通路。
/// eventData 为 null（脚本直接调）也照贴：自证执行器就是直接调这一下。
/// </remarks>
public class FriendAddInputPaste : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("真正干活的那一格（留空 = 往上层找）")]
    public FriendAddSearchUI owner;

    void Awake()
    {
        if (owner == null) owner = GetComponentInParent<FriendAddSearchUI>(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Right) return;   // 左键照旧留给 TMP 定位光标
        if (owner != null) owner.PasteFromClipboard();
        else Debug.LogWarning("[FriendAddInputPaste] 没找到 FriendAddSearchUI —— 右键粘贴这一下没接上");
    }
}
