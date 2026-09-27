using System;
using UnityEngine;

/// <summary>好友在列表里的状态（决定那行小字 + 颜色，也决定排序）。
/// 2026-09-27 用户定：只这四种 —— 在线（绿）/ 匹配中（金）/ 对局中（金）/ 离线（灰）。</summary>
public enum FriendPresence
{
    Offline = 0,    // 离线（灰）
    Online = 1,     // 在线（绿）—— 没在匹配、也没在对局
    Matching = 2,   // 匹配中（金）—— 在搜索 / 等对方确认
    InGame = 3,     // 对局中（金，比「匹配中」再亮一档）
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
    /// <summary>用户点过「删除」：**留一条墓碑**，Steam 那条路每 20 秒重扫时不再把他收回来（2026-09-27）。</summary>
    public bool removed;
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

    /// <summary>列表里那行小字（用户 2026-09-27 定的四种）。</summary>
    public string StatusLabel
    {
        get
        {
            switch (Presence)
            {
                case FriendPresence.InGame:   return "对局中";
                case FriendPresence.Matching: return "匹配中";
                case FriendPresence.Online:   return "在线";
                default:                      return "离线";
            }
        }
    }

    /// <summary>状态色：对局中 = 亮金 #E4CB84，匹配中 = 金 #C8A44A，在线 = 绿 #74B08A，离线 = 钢灰 #8EA2B4（更暗）。</summary>
    public Color StatusColor
    {
        get
        {
            switch (Presence)
            {
                case FriendPresence.InGame:   return new Color32(228, 203, 132, 255);
                case FriendPresence.Matching: return new Color32(200, 164, 74, 235);
                case FriendPresence.Online:   return new Color32(116, 176, 138, 255);
                default:                      return new Color32(142, 162, 180, 140);
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

    /// <summary>排序权重：对局中 → 匹配中 → 在线 → 离线。</summary>
    public int SortWeight
    {
        get
        {
            switch (Presence)
            {
                case FriendPresence.InGame:   return 0;
                case FriendPresence.Matching: return 1;
                case FriendPresence.Online:   return 2;
                default:                      return 3;
            }
        }
    }
}
