using Steamworks;
using UnityEngine;

/// <summary>
/// 自己的在线状态（Steam rich presence）—— 好友列表里那行「匹配中 / 对局中」读的就是它
/// （对方侧见 <see cref="SteamFriendSource"/> 里的 GetFriendRichPresence）。
///
/// 键固定 <c>status</c>，三档取值：""（在大厅 ⇒ 好友看到「在线」）/ "matching" / "ingame"。
/// Steam 没初始化（非 Steam 启动 / Editor 没登 Steam / 回调分发器没起来）时全部空跑，不抛异常。
///
/// 调用的四处：MatchWaitPanel.Show/Hide（搜索中）、MatchConfirmPanel.Open/Hide（等待对方确认那段）、
/// BattleLoadingScreen 开加载界面（对局中）、LobbyManager.Start（回大厅 = 清掉）。
/// </summary>
public static class SteamPresence
{
    public const string StatusKey = "status";
    public const string StatusMatching = "matching";
    public const string StatusInGame = "ingame";

    /// <summary>搜索中 / 等对方确认 —— 好友看到「匹配中」（金）。</summary>
    public static void Matching() { Set(StatusMatching); }

    /// <summary>已经进战斗 —— 好友看到「对局中」（金，更亮一档）。</summary>
    public static void InGame() { Set(StatusInGame); }

    /// <summary>在大厅，既不在匹配也不在对局 —— 好友看到「在线」（绿）。</summary>
    public static void Idle() { Set(""); }

    /// <summary>读好友自己写的 status（他没在跑本游戏时 Steam 返回空串，按「对局中」兜底）。</summary>
    public static string ReadFriend(CSteamID id)
    {
        if (!SteamManager.Initialized) return "";
        try { return SteamFriends.GetFriendRichPresence(id, StatusKey); }
        catch { return ""; }
    }

    static void Set(string value)
    {
        if (!SteamManager.Initialized) return;
        try { SteamFriends.SetRichPresence(StatusKey, value); }
        catch (System.Exception e) { Debug.LogWarning("[SteamPresence] 写 rich presence 失败：" + e.Message); }
    }
}
