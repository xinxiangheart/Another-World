using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「拉黑」这一件事只在这一个文件里 —— 界面 / 搜索 / 申请都调这里，别各写一份。
/// </summary>
/// <remarks>
/// **用户 2026-09-27 定的口径**：「拉黑的玩家无法搜索到拉黑他的玩家，也无法对其发送好友申请，匹配倒是能正常匹配到」。
///
/// 三句话拆成三条规则：
///   ① **搜不到**（双向）：搜索（<see cref="FriendAddSearch"/>）里任何一行，只要两个方向有一个拉黑，
///      就当这个人不存在。
///   ② **加不了**（双向）：加好友 / 发申请被拒，理由只报「加不了这个人」，**不告诉对方「你被拉黑了」**
///      —— 被拉黑的一方不该从 UI 上读出自己被人拉黑了。
///   ③ **匹配照常**：匹配池子完全不看这张名单（匹配那边没有任何一处调本类）。
///
/// ⚠ **方向**：现役只有本地那份名单（<see cref="IsBlockedByMe"/>）。
/// 「他拉黑了我」这件事只有**服务端 / Steam Web API 知道**（对方的名单不在我们手上），
/// 所以 <see cref="BlockedByLookup"/> 是个**改造位**（与 <see cref="FriendEvidence.OwnedLookup"/> 同一套写法）：
/// 接上之后 <see cref="BlocksMe"/> 才有值，① ② 两条的双向性才真的成立。**没接之前它恒 false。**
/// </remarks>
public static class FriendBlock
{
    /// <summary>键 = (异界号, SteamID64)，值 = 「他拉黑了我」。null = 这条路没装（现役）⇒ <see cref="BlocksMe"/> 恒 false。</summary>
    public static Func<string, ulong, bool> BlockedByLookup;

    /// <summary>我们拉黑了他（查本地那张名单）。</summary>
    public static bool IsBlockedByMe(string playerId, ulong steamId)
    {
        List<FriendEntry> store = FriendStore.Load();
        return IsBlockedByMe(store, playerId, steamId);
    }

    /// <summary>同上，但复用已经读进来的那份表（搜索要按行调很多次，别每行读一遍文件）。</summary>
    public static bool IsBlockedByMe(List<FriendEntry> store, string playerId, ulong steamId)
    {
        if (store == null) return false;
        for (int i = 0; i < store.Count; i++)
        {
            FriendEntry e = store[i];
            if (e == null || !e.blocked) continue;
            if (steamId != 0UL && e.SteamId == steamId) return true;
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(e.playerId)
                && PlayerId.Normalize(e.playerId) == PlayerId.Normalize(playerId)) return true;
        }
        return false;
    }

    /// <summary>他拉黑了我（服务端才知道；现役恒 false）。</summary>
    public static bool BlocksMe(string playerId, ulong steamId)
    {
        if (BlockedByLookup == null) return false;
        try { return BlockedByLookup(playerId, steamId); }
        catch (Exception e)
        {
            Debug.LogWarning("[FriendBlock] BlockedByLookup 抛异常，本次当「没拉黑」处理：" + e.Message);
            return false;
        }
    }

    /// <summary>两个方向任一成立 = 互相看不见（搜索 / 加好友两条路都拿这个判）。</summary>
    public static bool IsHidden(List<FriendEntry> store, string playerId, ulong steamId)
    {
        return IsBlockedByMe(store, playerId, steamId) || BlocksMe(playerId, steamId);
    }

    /// <summary>黑名单那格的数据源（好友表里所有 flagged 的，按「最近见到的在前」排）。</summary>
    public static List<FriendEntry> List()
    {
        var outList = new List<FriendEntry>();
        List<FriendEntry> store = FriendStore.Load();
        for (int i = 0; i < store.Count; i++)
        {
            FriendEntry e = store[i];
            if (e != null && e.blocked) outList.Add(e);
        }
        outList.Sort((a, b) => b.lastSeenUnix.CompareTo(a.lastSeenUnix));
        return outList;
    }

    /// <summary>
    /// 拉黑 / 取消拉黑（写盘）。人不在表里就**建一条**（拉黑一个陌生人不该顺手把他加成好友）。
    /// 「拉黑」**不删好友**：取消拉黑只是把旗子放下来，所以不能靠删好友来表达拉黑 —— 见 ⑤ 排错记录。
    /// 反过来，取消拉黑时**只删「为拉黑而建」的那条**（既非手动好友、也非 Steam 好友、又没玩过本游戏）——
    /// 那种记录旗子一落就没有存在理由，留着会挡住以后正常加好友。
    /// </summary>
    public static bool SetBlocked(string playerId, ulong steamId, string name, bool on)
    {
        if (string.IsNullOrEmpty(playerId) && steamId == 0UL) return false;

        List<FriendEntry> store = FriendStore.Load();
        FriendEntry rec = steamId != 0UL ? FriendStore.FindBySteam(store, steamId) : null;
        if (rec == null && !string.IsNullOrEmpty(playerId)) rec = FriendStore.FindByPlayerId(store, playerId);
        if (rec == null)
        {
            rec = new FriendEntry();
            if (steamId != 0UL) rec.SteamId = steamId;
            rec.playerId = playerId;
            rec.name = name;
            store.Add(rec);
        }
        if (rec.blocked == on) return false;

        rec.blocked = on;
        if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(rec.name)) rec.name = name;

        // 取消拉黑时：如果这条记录**只是为了记住「我拉黑过他」才建的** —— 他既不是手动好友、
        // 也没有「玩过本游戏」的证据、更不是 Steam 好友 —— 那旗子一放下来它就什么也不是了。
        // 留着会变成一条**幽灵记录**：好友表和黑名单两边都不显示它，可加好友时会被当成
        // 「已经在表里」，于是点勾报一句莫名其妙的「已经是好友了」（见 Stage70b ③ 反例）。
        // 真好友不删 —— 取消拉黑就是当场回到好友表（FriendListService ④：旗子留在表里）。
        if (!on && !rec.manual && !rec.playedOurGame && !rec.steamFriend)
            store.Remove(rec);

        FriendStore.Save(store);

        // 拉黑顺手把他的申请一起清掉。不清会出事：他现在**不在好友表里**，可申请列表里还躺着一条，
        // 一点勾就会走到 FriendStore.AddManual —— 而它按异界号找得到的正是这条 blocked 记录，
        // 于是返回 false，界面还会报一句莫名其妙的「已经是好友了」（他明明不在好友列表里）。
        if (on) FriendRequestStore.RemoveByPlayer(rec.playerId, steamId);
        return true;
    }

    /// <summary>日志 / 自证用。</summary>
    public static string Describe()
    {
        return BlockedByLookup != null
            ? "我拉黑的人 或 拉黑了我的人（服务端）"
            : "我拉黑的人（服务端那半边还没接：BlockedByLookup 为 null）";
    }
}
