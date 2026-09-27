using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 好友表落盘：`Application.persistentDataPath/friends.json`（和 player_data.json 同目录 —— 更新不丢）。
/// 只存**我们攒下来的东西**：手动用异界号加的游戏内好友（manual）、我们确认过他玩过本游戏的人、
/// 以及以后接 Steam Web API 拿到的缓存。Steam 实时能给的（昵称 / 头像 / 在线 / 正在玩）不以此为准，
/// 每次扫描都会盖上去。
///
/// 用户口径（2026-09-27）：「默认会添加双方是 Steam 好友且对方注册该游戏的玩家好友」。
/// </summary>
public static class FriendStore
{
    [Serializable]
    class Container
    {
        public int version = 1;
        public List<FriendEntry> friends = new List<FriendEntry>();
    }

    static string FilePath { get { return Path.Combine(Application.persistentDataPath, "friends.json"); } }

    public static List<FriendEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<FriendEntry>();
            string json = File.ReadAllText(FilePath);
            var c = JsonUtility.FromJson<Container>(json);
            return c != null && c.friends != null ? c.friends : new List<FriendEntry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FriendStore] friends.json 读取失败：" + e.Message);
            return new List<FriendEntry>();
        }
    }

    public static void Save(List<FriendEntry> list)
    {
        try
        {
            var c = new Container();
            c.friends = list != null ? list : new List<FriendEntry>();
            File.WriteAllText(FilePath, JsonUtility.ToJson(c, true));
        }
        catch (Exception e)
        {
            Debug.LogError("[FriendStore] friends.json 保存失败：" + e.Message);
        }
    }

    public static FriendEntry FindBySteam(List<FriendEntry> store, ulong steamId)
    {
        if (store == null || steamId == 0UL) return null;
        for (int i = 0; i < store.Count; i++)
            if (store[i] != null && store[i].SteamId == steamId) return store[i];
        return null;
    }

    public static FriendEntry FindByPlayerId(List<FriendEntry> store, string playerId)
    {
        if (store == null || string.IsNullOrEmpty(playerId)) return null;
        for (int i = 0; i < store.Count; i++)
            if (store[i] != null && store[i].playerId == playerId) return store[i];
        return null;
    }

    /// <summary>
    /// 记下「他玩过本游戏」这条证据（有就更新、没有就新建）。**只改内存里的表**，落盘由调用方统一做。
    /// touchSeen = 顺便记「我们第一次见到他玩本游戏」的时间（只在还是 0 的时候写，避免每轮扫描都变脏）。
    /// </summary>
    public static bool MarkPlayed(List<FriendEntry> store, ulong steamId, string name, string evidence, int coplayUnix, bool touchSeen)
    {
        if (store == null || steamId == 0UL) return false;

        var e = FindBySteam(store, steamId);
        bool changed = false;
        if (e == null)
        {
            e = new FriendEntry();
            e.SteamId = steamId;
            e.steamFriend = true;
            store.Add(e);
            changed = true;
        }
        if (!e.playedOurGame) { e.playedOurGame = true; changed = true; }
        if (!string.IsNullOrEmpty(name) && e.name != name) { e.name = name; changed = true; }
        if (!string.IsNullOrEmpty(evidence) && string.IsNullOrEmpty(e.evidence)) { e.evidence = evidence; changed = true; }
        if (coplayUnix != 0 && e.coplayUnix != coplayUnix) { e.coplayUnix = coplayUnix; changed = true; }
        if (touchSeen && e.lastSeenUnix == 0) { e.lastSeenUnix = NowUnix(); changed = true; }
        return changed;
    }

    /// <summary>
    /// 手动加一个「游戏内好友」（只有异界号，Steam 那边不一定是好友）。
    /// 返回 false = ID 空 / 已经在表里。**只改内存**。
    /// </summary>
    public static bool AddManual(List<FriendEntry> store, string playerId, string name, ulong steamId)
    {
        if (store == null || string.IsNullOrEmpty(playerId)) return false;
        if (FindByPlayerId(store, playerId) != null) return false;

        var e = new FriendEntry();
        e.playerId = playerId;
        e.name = name;
        e.manual = true;
        if (steamId != 0UL) { e.SteamId = steamId; e.steamFriend = true; }
        store.Add(e);
        return true;
    }

    /// <summary>删好友（留给「删好友」UI；按 SteamID 或异界号找）。**只改内存**。</summary>
    public static bool Remove(List<FriendEntry> store, string key)
    {
        if (store == null || string.IsNullOrEmpty(key)) return false;
        for (int i = 0; i < store.Count; i++)
        {
            if (store[i] == null) continue;
            if (store[i].playerId == key || store[i].steamIdText == key) { store.RemoveAt(i); return true; }
        }
        return false;
    }

    static long NowUnix() { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
}
