using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 「申请列表 / 黑名单」两格的**调试数据**入口（2026-09-27）。
/// </summary>
/// <remarks>
/// 用户 2026-09-27：「申请列表和黑名单一起做……」
///
/// 这两格的正式数据来源都还没有 —— 申请要等后端收包（<see cref="FriendRequestStore.Add(string,string,ulong)"/>），
/// 黑名单要等真人点「拉黑」。没有后端的现在，光靠界面是**看不到版式**的（两格都是空的），
/// 所以留这几个菜单塞假数据，看完清掉即可：
///   · 好友申请落在 `friend_requests.json`（与好友表同目录，跟真数据一个文件 —— 别拿它当正式入口）
///   · 黑名单就是好友表里 blocked 的那些（<see cref="FriendBlock.SetBlocked"/>），所以塞进去的会**真的进好友表**
///     （带 blocked 旗子），清的时候只把旗子放下来、**不删记录** —— 与真人点拉黑 / 取消拉黑完全同一条路径。
///
/// 在 Play 模式下顺手把场景里那两格重画一遍（<see cref="FriendPanelListUI.Rebuild"/>）；
/// 编辑模式**不重画** —— 那会往场景里克隆行、把 Lobby.unity 弄脏。
/// </remarks>
public static class FriendDataDebugMenu
{
    static readonly string[] FakeNames = { "夜行人", "雾岛听风", "星野", "无名客", "离线丁" };

    [MenuItem("Tools/异界/调试：塞一条好友申请（看版式用）")]
    public static void AddFakeRequest()
    {
        int n = FriendRequestStore.Count();
        int i = n % FakeNames.Length;
        ulong sid = PlayerId.SteamIdBase + (ulong)(7000 + n);
        string pid = PlayerId.Create(System.DateTime.Today, sid);
        FriendRequestStore.Add(pid, FakeNames[i], sid);
        Debug.Log("[FriendDataDebug] 已塞一条好友申请：" + FakeNames[i] + " / " + pid
                  + "（现在共 " + FriendRequestStore.Count() + " 条）" + RefreshHint());
        RefreshPanels();
    }

    [MenuItem("Tools/异界/调试：清空好友申请")]
    public static void ClearRequests()
    {
        FriendRequestStore.Save(new List<FriendEntry>());
        Debug.Log("[FriendDataDebug] 好友申请已清空。" + RefreshHint());
        RefreshPanels();
    }

    [MenuItem("Tools/异界/调试：塞一个陌生人进黑名单")]
    public static void AddFakeBlock()
    {
        int n = FriendBlock.List().Count;
        ulong sid = PlayerId.SteamIdBase + (ulong)(7100 + n);
        string pid = PlayerId.Create(System.DateTime.Today, sid);
        FriendBlock.SetBlocked(pid, sid, FakeNames[n % FakeNames.Length], true);
        Debug.Log("[FriendDataDebug] 已塞一个陌生人进黑名单：" + FakeNames[n % FakeNames.Length] + " / " + pid
                  + "（现在共 " + FriendBlock.List().Count + " 个）" + RefreshHint());
        RefreshPanels();
    }

    [MenuItem("Tools/异界/调试：清空黑名单（只放旗子，不删记录）")]
    public static void ClearBlocks()
    {
        List<FriendEntry> blocked = FriendBlock.List();
        for (int i = 0; i < blocked.Count; i++)
            FriendBlock.SetBlocked(blocked[i].playerId, blocked[i].SteamId, blocked[i].DisplayName, false);
        Debug.Log("[FriendDataDebug] 黑名单已清空（放掉 " + blocked.Count + " 面旗子）。" + RefreshHint());
        RefreshPanels();
    }

    static string RefreshHint()
    {
        return Application.isPlaying ? "" : " 进 Play 才看得到（编辑模式不往场景里克隆行）。";
    }

    static void RefreshPanels()
    {
        if (!Application.isPlaying) return;

        if (FriendListService.Instance != null) FriendListService.Instance.Refresh();
        var lists = Resources.FindObjectsOfTypeAll<FriendPanelListUI>();
        for (int i = 0; i < lists.Length; i++)
            if (lists[i] != null) lists[i].Rebuild();
    }
}
