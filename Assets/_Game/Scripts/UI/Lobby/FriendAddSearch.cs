using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>「添加好友」搜出来的一行，该给哪个动作（决定右端那颗「+」出不出来、状态列写什么）。</summary>
public enum FriendAddState
{
    Addable = 0,      // 解出了异界号、且还不是好友 → 右端那颗「+」可点
    Existing = 1,     // 已经在好友表里 → 不出「+」，状态列写「已是好友」
    SteamFriend = 2,  // 只是 Steam 好友、我们没他的异界号 → 不出「+」，状态列写「Steam 好友」
}

/// <summary>搜索结果的一行（**纯数据**，不进场景）。</summary>
public class FriendSearchHit
{
    public string displayName = "";
    public string playerId = "";                  // 本游戏「异界号」（知道才有）
    public ulong  steamId;                        // SteamID64（0 = 没有 Steam 身份）
    public FriendAddState state = FriendAddState.Addable;
    public FriendPresence presence = FriendPresence.Offline;

    /// <summary>去重用的键：异界号优先，其次 SteamID64，最后退到名字。</summary>
    public string Key
    {
        get
        {
            if (!string.IsNullOrEmpty(playerId)) return "p:" + playerId;
            if (steamId != 0UL) return "s:" + steamId;
            return "n:" + displayName;
        }
    }
}

/// <summary>
/// 「添加好友」那一格的搜索：把用户敲进去的一串东西，变成右侧大格子里那份结果名单。
/// </summary>
/// <remarks>
/// 2026-09-27 用户：「输入框可以之间输入id或者昵称……然后会在下方列出所有相关玩家（一般是输入昵称时可以有多个玩家）」。
///
/// **结果从哪来（两条路，都不需要后端）**：
///   ① **异界号** —— PlayerId 是 A 方案：Steam 账号 7 个 Base32 符号**直接编进了 ID**，
///      所以在本地就能解出对方的 SteamID64（PlayerId.ResolveSteamId）。解析成功 = **精确命中一个人**，
///      而且**一定可加**（FriendStore.AddManual 只认异界号）。用户给过口径：「加好友只需游戏内的即可，
///      离线玩家不考虑联机功能」。
///   ② **昵称 / 号片段** —— 在「已知玩家」里模糊匹配（好友表 FriendListService.Entries
///      ∪ Steam 好友 SteamFriendSource.Collect）。这一条**天然只能搜到我们已经认识的人** ——
///      按昵称全服搜需要一份玩家目录（Steam Web API / 自建后端），现在没有；分界线就留在
///      FriendAddSearch.Run 这一层，以后接上只改这里。
///
/// 因此状态只有三档：**可添加**（解出了异界号且还不是好友）/ **已是好友**（好友表里的人）/
/// **Steam 好友**（只是 Steam 好友、没异界号 —— 记不进游戏内好友表，所以不给「+」）。
/// </remarks>
public static class FriendAddSearch
{
    public const int MaxResults = 50;

    /// <summary>一个「已知玩家」 + 他在不在好友表里（决定是「已是好友」还是「Steam 好友」）。</summary>
    class Known
    {
        public FriendEntry entry;
        public bool listed;
    }

    /// <summary>搜一次。message 只在「这一串不用搜」时给话（比如输入的是自己的号）。</summary>
    public static List<FriendSearchHit> Run(string query, out string message)
    {
        message = "";
        var hits = new List<FriendSearchHit>();
        string q = query == null ? "" : query.Trim();
        if (q.Length == 0) return hits;

        string selfId = SteamDataManager.Instance != null ? SteamDataManager.Instance.PlayerIdText : "";
        ulong selfSteam = LobbyConfig.LocalSteamID;
        List<FriendEntry> blockStore = FriendStore.Load();   // 拉黑名单读一次，下面按行判（别每行读一遍文件）

        var takenKeys = new HashSet<string>();
        var takenSteam = new HashSet<ulong>();
        List<Known> known = KnownPlayers();

        // ① 异界号：本地就能解出 SteamID64 ⇒ 精确命中一个人
        int season; DateTime created; ulong sid;
        if (PlayerId.TryParse(q, out season, out created, out sid))
        {
            if ((selfSteam != 0UL && sid == selfSteam)
                || (!string.IsNullOrEmpty(selfId) && PlayerId.Normalize(selfId) == PlayerId.Normalize(q)))
            {
                message = "这是你自己的异界号";
                return hits;
            }

            // 拉黑（任一方向）⇒ 这个人当不存在。理由**不吐给用户**：被拉黑的一方不该从 UI 上读出来
            if (FriendBlock.IsHidden(blockStore, PlayerId.Pretty(q), sid))
            {
                message = "搜不到这个异界号";
                return hits;
            }

            var idHit = new FriendSearchHit();
            idHit.playerId = PlayerId.Pretty(q);
            idHit.steamId = sid;
            Known k = FindKnown(known, sid, idHit.playerId);
            idHit.displayName = k != null ? k.entry.DisplayName : PersonaName(sid, idHit.playerId);
            idHit.state = (k != null && k.listed) ? FriendAddState.Existing : FriendAddState.Addable;
            if (k != null) idHit.presence = k.entry.Presence;

            hits.Add(idHit);
            takenKeys.Add(idHit.Key);
            takenSteam.Add(sid);
        }

        // ② 昵称 / 号片段：在已知玩家（好友表 ∪ Steam 好友）里模糊匹配 —— 昵称一般会命中好几个
        string qn = PlayerId.Normalize(q);
        var nick = new List<FriendSearchHit>();
        for (int i = 0; i < known.Count; i++)
        {
            FriendEntry e = known[i].entry;
            if (e == null) continue;
            if (e.SteamId != 0UL && takenSteam.Contains(e.SteamId)) continue;
            if (selfSteam != 0UL && e.SteamId == selfSteam) continue;

            bool byName = !string.IsNullOrEmpty(e.name) && e.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            bool byId = qn.Length >= 4 && !string.IsNullOrEmpty(e.playerId)
                        && PlayerId.Normalize(e.playerId).IndexOf(qn, StringComparison.Ordinal) >= 0;
            if (!byName && !byId) continue;
            if (FriendBlock.IsHidden(blockStore, e.playerId, e.SteamId)) continue;   // 拉黑（任一方向）⇒ 搜不出来

            var h = new FriendSearchHit();
            h.displayName = e.DisplayName;
            h.playerId = e.playerId;
            h.steamId = e.SteamId;
            h.presence = e.Presence;
            h.state = known[i].listed
                ? FriendAddState.Existing
                : (string.IsNullOrEmpty(h.playerId) ? FriendAddState.SteamFriend : FriendAddState.Addable);
            if (!takenKeys.Add(h.Key)) continue;
            if (e.SteamId != 0UL) takenSteam.Add(e.SteamId);
            nick.Add(h);
        }
        nick.Sort((a, b) => Rank(a, q).CompareTo(Rank(b, q)));
        hits.AddRange(nick);

        if (hits.Count > MaxResults) hits.RemoveRange(MaxResults, hits.Count - MaxResults);
        return hits;
    }

    /// <summary>加这个人为「游戏内好友」。返回 false 时 message 里是原因（直接拿去弹提示）。</summary>
    public static bool TryAdd(FriendSearchHit hit, out string message)
    {
        if (hit == null) { message = "没有这一行"; return false; }

        // 拉黑（任一方向）⇒ 加不了。**不写「你被拉黑了」**：只报「加不了这个人」（见 FriendBlock 口径②）
        if (FriendBlock.IsHidden(FriendStore.Load(), hit.playerId, hit.steamId))
        {
            message = "加不了这个人";
            return false;
        }

        if (hit.state == FriendAddState.Existing) { message = "「" + hit.displayName + "」已经是好友了"; return false; }
        if (hit.state == FriendAddState.SteamFriend || string.IsNullOrEmpty(hit.playerId))
        {
            message = "「" + hit.displayName + "」还没有异界号，加不了";
            return false;
        }

        List<FriendEntry> store = FriendStore.Load();
        if (!FriendStore.AddManual(store, hit.playerId, hit.displayName, hit.steamId))
        {
            message = "「" + hit.displayName + "」已经在好友表里了";
            return false;
        }

        FriendStore.Save(store);
        if (FriendListService.Instance != null) FriendListService.Instance.Refresh();
        message = "已添加「" + hit.displayName + "」";
        return true;
    }

    // ── 内部 ────────────────────────────────────────────────────────────────

    static int Rank(FriendSearchHit h, string q)
    {
        if (string.Equals(h.displayName, q, StringComparison.OrdinalIgnoreCase)) return 0;
        if (!string.IsNullOrEmpty(h.displayName) && h.displayName.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    static Known FindKnown(List<Known> known, ulong steamId, string playerId)
    {
        for (int i = 0; i < known.Count; i++)
        {
            FriendEntry e = known[i].entry;
            if (e == null) continue;
            if (steamId != 0UL && e.SteamId == steamId) return known[i];
            if (!string.IsNullOrEmpty(playerId) && e.playerId == playerId) return known[i];
        }
        return null;
    }

    /// <summary>「已知玩家」= 好友表里的人 ∪ Steam 好友（还没进好友表的那些）。</summary>
    static List<Known> KnownPlayers()
    {
        var list = new List<Known>();
        var seenSteam = new HashSet<ulong>();
        var seenIds = new HashSet<string>();

        FriendListService svc = FriendListService.Instance;
        if (svc != null && svc.Entries != null)
        {
            for (int i = 0; i < svc.Entries.Count; i++)
            {
                FriendEntry e = svc.Entries[i];
                if (e == null) continue;
                Add(list, seenSteam, seenIds, e, true);
            }
        }

        // Steam 好友里那些还没进好友表的（没玩过本游戏 / 我们没见过他玩）也搜得到 —— 只是加不了（没异界号）
        if (SteamManager.Initialized)
        {
            List<FriendEntry> steam = SteamFriendSource.Collect();
            List<FriendEntry> store = FriendStore.Load();
            for (int i = 0; i < steam.Count; i++)
            {
                FriendEntry e = steam[i];
                if (e == null) continue;
                FriendEntry local = FriendStore.FindBySteam(store, e.SteamId);
                if (local != null && local.removed) continue;    // 删过的人：搜也别再冒出来
                Add(list, seenSteam, seenIds, e, false);
            }
        }
        return list;
    }

    static bool Add(List<Known> list, HashSet<ulong> seenSteam, HashSet<string> seenIds, FriendEntry e, bool listed)
    {
        if (e.SteamId != 0UL && !seenSteam.Add(e.SteamId)) return false;
        if (!string.IsNullOrEmpty(e.playerId) && !seenIds.Add(e.playerId)) return false;
        var k = new Known();
        k.entry = e;
        k.listed = listed;
        list.Add(k);
        return true;
    }

    /// <summary>从 Steam 问昵称；问不到（不是好友 / 没缓存 / 没 Steam）就显示「未知玩家」—— 名字那格不重复异界号。</summary>
    static string PersonaName(ulong steamId, string fallback)
    {
        if (steamId != 0UL && SteamManager.Initialized)
        {
            try
            {
                string n = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
                if (!string.IsNullOrEmpty(n) && !n.StartsWith("[unknown]", StringComparison.Ordinal)) return n;
            }
            catch { }
        }
        return "未知玩家";
    }
}
