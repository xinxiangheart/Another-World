using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>
/// 从 Steam **客户端 SDK** 拉「互为 Steam 好友」的人 + 三条「玩过本游戏」的运行时证据。
///
/// ⚠️ 客户端 SDK **没有**「好友是否拥有本游戏」的接口（好友的库 / 成就一律读不到），
/// 所以「拥有」这条路只能靠 Web API —— 改造位见 <see cref="FriendEvidence.OwnedLookup"/>。
/// 本类只答三个问题：他此刻在玩什么（GetFriendGamePlayed）、我俩一起玩过什么（GetFriendCoplayGame）、
/// 他自己写的是什么状态（GetFriendRichPresence → 见 <see cref="SteamPresence"/>）。
/// 四态由这两个拼出来 —— 判据见 <see cref="PresenceFor"/>。
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

    /// <summary>
    /// 四态映射（**纯函数** —— 自证直接喂参数，不用真 Steam 好友）。
    /// </summary>
    /// <remarks>
    /// **2026-09-27 修（用户报的 bug）**：原来判据反了 ——
    ///   · 「Steam 在线、但**没在跑本游戏**」被算成**在线**（应该**离线**：这四种状态说的都是「他**在本游戏里**的位置」，
    ///     人根本不在本游戏里，对我们就是离线）；
    ///   · 「在跑本游戏、但**没进对局**」（rich presence 是空串 = 在大厅）被算成**对局中**（应该**在线**）。
    /// 现在判据的根换成 **传进来的 `playingOurGame`**（= GetFriendGamePlayed 打的正是本机 appid），
    /// 再看他自己写的那条 rich presence（<see cref="SteamPresence"/> 的三个值）。
    ///
    /// **顺带修好的**：邀请那枚「+」的开关是 `Presence == Online`
    /// （<see cref="FriendRowUI"/> / <see cref="FriendDetailRowUI"/>）——
    /// 以前「在线」= 只是 Steam 在线（**根本没法邀请**），现在「在线」= **在本游戏大厅里**（正好就是能邀请的状态）。
    ///
    /// 拿不到 rich presence（还没同步过来 / 老客户端没写过）时按**在线**兜底 —— 总比误报「对局中」好。
    /// </remarks>
    public static FriendPresence PresenceFor(bool playingOurGame, string richStatus)
    {
        if (!playingOurGame) return FriendPresence.Offline;          // 不在本游戏里 = 离线（灰）
        if (richStatus == SteamPresence.StatusMatching) return FriendPresence.Matching;   // 搜索 / 等确认（金）
        if (richStatus == SteamPresence.StatusInGame)   return FriendPresence.InGame;     // 已经进对局（金，更亮）
        return FriendPresence.Online;                                // "" = 在大厅（绿），拿不到也按这个兜底
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

            // ③ 状态（用户 2026-09-27 定：只有 在线 / 匹配中 / 对局中 / 离线 这四种）—— 判据见 PresenceFor
            e.Presence = PresenceFor(playingOurGame, playingOurGame ? SteamPresence.ReadFriend(id) : "");

            list.Add(e);
        }

        Debug.Log("[SteamFriend] 互为 Steam 好友 " + list.Count + " 人（本机 appid=" + appId + "）");
        return list;
    }
}
