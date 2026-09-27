using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>
/// 从 Steam **客户端 SDK** 拉「互为 Steam 好友」的人 + 三条「玩过本游戏」的运行时证据。
///
/// ⚠️ 客户端 SDK **没有**「好友是否拥有本游戏」的接口（好友的库 / 成就一律读不到），
/// 所以「拥有」这条路只能靠 Web API —— 改造位见 <see cref="FriendEvidence.OwnedLookup"/>。
/// 本类只答四个问题：他此刻在玩什么（GetFriendGamePlayed）、我俩一起玩过什么（GetFriendCoplayGame）、
/// 他在不在线（GetFriendPersonaState）、他自己写的是什么状态（GetFriendRichPresence → 见 <see cref="SteamPresence"/>）。
///
/// Steam 没初始化（非 Steam 启动 / Direct IP / 回调分发器没起来）时返回空表，不抛异常。
/// </summary>
public static class SteamFriendSource
{
    /// <summary>本机正在跑的 appid（现在 steam_appid.txt 是 480 = Spacewar 测试号）。</summary>
    public static uint OurAppId
    {
        get
        {
            if (!SteamManager.Initialized) return 0u;
            try { return SteamUtils.GetAppID().m_AppId; }
            catch { return 0u; }
        }
    }

    /// <summary>互为 Steam 好友的人（含证据与在线状态）。没 Steam 时是空表。</summary>
    public static List<FriendEntry> Collect()
    {
        var list = new List<FriendEntry>();
        if (!SteamManager.Initialized)
        {
            Debug.Log("[SteamFriend] Steam 未初始化 —— 好友表当空处理");
            return list;
        }

        uint appId = OurAppId;
        int total = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        for (int i = 0; i < total; i++)
        {
            CSteamID id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            if (SteamFriends.GetFriendRelationship(id) != EFriendRelationship.k_EFriendRelationshipFriend) continue;

            var e = new FriendEntry();
            e.SteamId = id.m_SteamID;
            e.steamFriend = true;
            e.name = SteamFriends.GetFriendPersonaName(id);
            SteamFriends.RequestUserInformation(id, true);   // 拉昵称 / 头像

            // ① 他此刻正在玩什么
            FriendGameInfo_t info;
            bool playing = SteamFriends.GetFriendGamePlayed(id, out info);
            bool playingOurGame = playing && appId != 0u && info.m_gameID.AppID().m_AppId == appId;
            if (playingOurGame)
            {
                e.playedOurGame = true;
                e.evidence = "steam-playing";
            }

            // ② Steam 记的「我俩一起玩过本游戏」
            if (!e.playedOurGame)
            {
                uint coplayApp = SteamFriends.GetFriendCoplayGame(id).m_AppId;
                if (appId != 0u && coplayApp == appId)
                {
                    e.playedOurGame = true;
                    e.evidence = "steam-coplay";
                    e.coplayUnix = SteamFriends.GetFriendCoplayTime(id);
                }
            }

            // ③ 状态（用户 2026-09-27 定：只有 在线 / 匹配中 / 对局中 / 离线 这四种）
            //    离线 → 灰；正在玩本游戏 → 问他自己写的那条 rich presence（matching = 匹配中，
            //    其余包括没写 = 对局中）；剩下的（在线但没在玩本游戏）→ 在线（绿）。
            if (SteamFriends.GetFriendPersonaState(id) == EPersonaState.k_EPersonaStateOffline)
                e.Presence = FriendPresence.Offline;
            else if (playingOurGame)
                e.Presence = SteamPresence.ReadFriend(id) == SteamPresence.StatusMatching
                    ? FriendPresence.Matching
                    : FriendPresence.InGame;
            else
                e.Presence = FriendPresence.Online;

            list.Add(e);
        }

        Debug.Log("[SteamFriend] 互为 Steam 好友 " + list.Count + " 人（本机 appid=" + appId + "）");
        return list;
    }
}
