using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>好友表的一条过滤规则。</summary>
public interface IFriendFilter
{
    string Name { get; }
    bool Enabled { get; }
    bool Keep(FriendEntry e);
}

/// <summary>
/// 「他是否拥有 / 玩过本游戏」的证据层。
///
/// **改造位就在这一条**：拿到 Steam Web API Key 之后，把 `IPlayerService/GetOwnedGames` 的查询
/// 装到 <see cref="OwnedLookup"/>（键 = SteamID64，值 = 是否拥有本游戏）——
///   ① 正式 AppID 下来（现在 steam_appid.txt 还是 480 = Spacewar 测试号，"拥有本游戏"这件事在 Steam 眼里还是 Spacewar）；
///   ② 好友的「游戏详情」对公众可见（否则查不到，只能落回运行时证据）；
///   ③ 结果本地缓存（一天刷一次就够，Web API 有配额）。
/// 装上之前 <see cref="OwnedLookup"/> 是 null，<see cref="OwnsGame"/> 恒 false —— 规则退化成下面三条运行时证据。
/// </summary>
public static class FriendEvidence
{
    /// <summary>键 = SteamID64；值 = 是否拥有本游戏。null = 这条路没装（现役）。</summary>
    public static Func<ulong, bool> OwnedLookup;

    public static bool HasOwnedLookup { get { return OwnedLookup != null; } }

    public static bool OwnsGame(ulong steamId)
    {
        if (OwnedLookup == null || steamId == 0UL) return false;
        try { return OwnedLookup(steamId); }
        catch (Exception e)
        {
            Debug.LogWarning("[FriendEvidence] OwnedLookup 抛异常，本次当「没有」处理：" + e.Message);
            return false;
        }
    }

    /// <summary>日志用：现役证据链的说明（改造位装上后那句会变）。</summary>
    public static string Describe()
    {
        return HasOwnedLookup
            ? "Steam 好友 且（正在玩 / 一起玩过 / 我们见过 / 拥有本游戏[Web API]）"
            : "Steam 好友 且（正在玩本游戏 / 一起玩过本游戏 / 我们见过他）";
    }
}

/// <summary>
/// **现役规则**（用户 2026-09-27 定）：「双方是 Steam 好友 且 对方玩过本游戏」→ 默认就加。
///
/// 没有 Web API key 时，「对方玩过本游戏」只有三条**运行时**证据（见 <see cref="SteamFriendSource"/>）：
///   ① 他此刻正在玩本游戏 —— SteamFriends.GetFriendGamePlayed()
///   ② Steam 记着「我俩一起玩过本游戏」—— SteamFriends.GetFriendCoplayGame()
///   ③ 我们自己见过他（本地名单 local-seen，例如匹配到过、进过同一局）
/// 拿到 key 之后 <see cref="FriendEvidence.OwnsGame"/> 变成第 ④ 条「拥有即加」，**本类不用改**。
///
/// 反例（要提前接受）：一个 Steam 好友自己买了、自己玩，但从没和你一起开过局、你也没在大厅见过他
/// —— 客户端 SDK 看不见他，不会自动加。这种人只能等 Web API。
/// </summary>
public class SteamFriendAndPlayedFilter : IFriendFilter
{
    public string Name { get { return "Steam 好友 + 玩过本游戏"; } }
    public bool Enabled { get { return true; } }

    public bool Keep(FriendEntry e)
    {
        if (e == null) return false;
        if (!e.steamFriend) return false;              // ① 双方必须互为 Steam 好友
        if (e.playedOurGame) return true;              // ② 三条运行时证据之一
        return FriendEvidence.OwnsGame(e.SteamId);     // ③ 改造位：拥有即加（没装 key 时恒 false）
    }
}

/// <summary>过滤器链：按顺序跑，任何一个「启用中」的过滤器说不留，就不留。</summary>
public class FriendFilterChain
{
    readonly List<IFriendFilter> _filters = new List<IFriendFilter>();

    public void Add(IFriendFilter f) { if (f != null) _filters.Add(f); }
    public IList<IFriendFilter> Filters { get { return _filters; } }

    public bool Keep(FriendEntry e)
    {
        for (int i = 0; i < _filters.Count; i++)
        {
            IFriendFilter f = _filters[i];
            if (f != null && f.Enabled && !f.Keep(e)) return false;
        }
        return true;
    }

    public string Describe()
    {
        var sb = new StringBuilder(FriendEvidence.Describe());
        for (int i = 0; i < _filters.Count; i++)
        {
            if (_filters[i] == null) continue;
            sb.Append(" ｜ ").Append(_filters[i].Name);
            if (!_filters[i].Enabled) sb.Append("(未启用)");
        }
        return sb.ToString();
    }
}
