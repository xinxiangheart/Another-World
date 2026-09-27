using System;
using UnityEngine;

/// <summary>好友在列表里的状态（决定那行小字 + 颜色，也决定排序）。</summary>
public enum FriendPresence
{
    Offline = 0,        // 离线 / 未知
    Online = 1,         // 在线（没在玩游戏）
    PlayingOther = 2,   // 正在玩别的游戏
    PlayingOurGame = 3, // 正在玩本游戏（排最前）
    InGameOnly = 4,     // 只有异界号、没有 Steam 身份的「游戏内好友」
}

/// <summary>
/// 好友表的一行（**纯数据**，不进场景）。
/// 持久化走 JsonUtility —— Unity 的序列化器对 ulong 不可靠，所以 SteamID 存文本
/// （<see cref="steamIdText"/>），要 ulong 用 <see cref="SteamId"/>（与 player_data.json 里
/// playerIdSteam 同一套写法）。
/// </summary>
[Serializable]
public class FriendEntry
{
    public string steamIdText = "";   // SteamID64 文本；空 = 没有 Steam 身份（离线玩家 / 只有异界号）
    public string name = "";          // 显示名（Steam 昵称优先，拿不到才退回异界号）
    public string playerId = "";      // 本游戏「异界号」（知道才有；以后按 ID 加好友用）
    public bool steamFriend;          // 与该 SteamID **互为** Steam 好友
    public bool playedOurGame;        // 有「他玩过本游戏」的证据（见 evidence）
    public bool manual;               // 手动用异界号加的「游戏内好友」（不受 Steam 好友条件约束）
    public int presence;              // FriendPresence
    public long lastSeenUnix;         // 我们最后一次「见过他」的 Unix 秒（0 = 没见过）
    public int coplayUnix;            // Steam 记的「我俩一起玩过本游戏」时间（0 = 没记）
    public string evidence = "";      // 证据来源：steam-playing / steam-coplay / local-seen（日志与调试用）

    public ulong SteamId
    {
        get { ulong v; return ulong.TryParse(steamIdText, out v) ? v : 0UL; }
        set { steamIdText = value == 0UL ? "" : value.ToString(); }
    }

    public FriendPresence Presence
    {
        get { return (FriendPresence)presence; }
        set { presence = (int)value; }
    }

    /// <summary>列表里那行小字。</summary>
    public string StatusLabel
    {
        get
        {
            switch (Presence)
            {
                case FriendPresence.PlayingOurGame: return "正在玩本游戏";
                case FriendPresence.PlayingOther:   return "游戏中";
                case FriendPresence.Online:         return "在线";
                case FriendPresence.InGameOnly:     return "游戏内好友";
                default:                            return "离线";
            }
        }
    }

    /// <summary>状态色：正在玩本游戏 = 金，其余 = 钢（离线更暗）—— 本套金 #C8A44A / 钢 #8EA2B4。</summary>
    public Color StatusColor
    {
        get
        {
            switch (Presence)
            {
                case FriendPresence.PlayingOurGame: return new Color32(200, 164, 74, 255);
                case FriendPresence.Online:         return new Color32(142, 162, 180, 225);
                case FriendPresence.PlayingOther:   return new Color32(142, 162, 180, 185);
                default:                            return new Color32(142, 162, 180, 125);
            }
        }
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(name)) return name;
            if (!string.IsNullOrEmpty(playerId)) return playerId;
            return SteamId != 0UL ? steamIdText : "未知玩家";
        }
    }

    /// <summary>排序权重：正在玩本游戏 → 在线 → 游戏中 → 游戏内好友 → 离线。</summary>
    public int SortWeight
    {
        get
        {
            switch (Presence)
            {
                case FriendPresence.PlayingOurGame: return 0;
                case FriendPresence.Online:         return 1;
                case FriendPresence.PlayingOther:   return 2;
                case FriendPresence.InGameOnly:     return 3;
                default:                            return 4;
            }
        }
    }
}
