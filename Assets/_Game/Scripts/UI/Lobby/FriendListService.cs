using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 好友表服务：把「Steam 好友 + 证据」和「本地攒下来的名单」合成大厅要的那一份，排序后交给 UI。
///
/// 规则（用户 2026-09-27）：「双方是 Steam 好友 且 对方注册 / 玩过本游戏」→ 默认就加。
/// 没有 Web API key 时「玩过」只认三条**运行时**证据：正在玩本游戏 / 我俩一起玩过本游戏 / 我们见过他；
/// 拿到 key 后 <see cref="FriendEvidence.OwnedLookup"/> 一装，就自动多一条「拥有即加」——本类不用改。
///
/// 另外：手动用异界号加的「游戏内好友」（<see cref="FriendEntry.manual"/>）不受 Steam 好友条件约束，
/// 永远留在表里（用户口径：加好友只需游戏内的）。
///
/// 挂在大厅 Canvas 上，20 秒重扫一次（Steam 在线状态会变，但不必每帧问）。没 Steam 时表就是本地名单，不报错。
/// </summary>
public class FriendListService : MonoBehaviour
{
    public static FriendListService Instance { get; private set; }

    [Header("重扫间隔（秒）")]
    public float rescanInterval = 20f;

    /// <summary>名单变了就发一次（UI 订阅它重排）。</summary>
    public event Action Refreshed;

    readonly List<FriendEntry> _entries = new List<FriendEntry>();
    public IList<FriendEntry> Entries { get { return _entries; } }

    FriendFilterChain _chain;
    float _nextScan;
    string _lastLog = "";

    public bool SteamReady { get { return SteamManager.Initialized; } }

    /// <summary>现役规则链的说明（日志 / 调试报告用）。</summary>
    public string RuleDescription { get { return _chain != null ? _chain.Describe() : ""; } }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _chain = new FriendFilterChain();
        _chain.Add(new SteamFriendAndPlayedFilter());     // ← 现役规则；改造位在 FriendEvidence.OwnedLookup
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        if (Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + Mathf.Max(5f, rescanInterval);
        Refresh();
    }

    /// <summary>重扫一遍：Steam 好友 + 本地名单 → 过滤 → 排序 → 广播。</summary>
    public void Refresh()
    {
        List<FriendEntry> store = FriendStore.Load();
        List<FriendEntry> steam = SteamFriendSource.Collect();
        bool dirty = false;

        // ① 把「正在玩 / 一起玩过」记进本地表 —— 以后 Steam 不再给证据时，也还记得他是本游戏玩家
        for (int i = 0; i < steam.Count; i++)
        {
            FriendEntry s = steam[i];
            if (s == null || !s.playedOurGame) continue;
            if (FriendStore.MarkPlayed(store, s.SteamId, s.name, s.evidence, s.coplayUnix, true)) dirty = true;
        }

        // ② Steam 好友：用本地记录补齐 → 过过滤链
        var result = new List<FriendEntry>();
        var taken = new HashSet<ulong>();
        for (int i = 0; i < steam.Count; i++)
        {
            FriendEntry e = steam[i];
            if (e == null) continue;

            FriendEntry local = FriendStore.FindBySteam(store, e.SteamId);
            if (local != null)
            {
                if (string.IsNullOrEmpty(e.playerId)) e.playerId = local.playerId;
                if (local.lastSeenUnix > 0) e.lastSeenUnix = local.lastSeenUnix;
                if (e.coplayUnix == 0 && local.coplayUnix != 0) e.coplayUnix = local.coplayUnix;
                if (!e.playedOurGame && local.playedOurGame) { e.playedOurGame = true; e.evidence = "local-seen"; }
            }

            if (!_chain.Keep(e)) continue;
            if (taken.Add(e.SteamId)) result.Add(e);
        }

        // ③ 手动加的「游戏内好友」：不受 Steam 好友条件约束
        for (int i = 0; i < store.Count; i++)
        {
            FriendEntry s = store[i];
            if (s == null || !s.manual) continue;
            ulong sid = s.SteamId;
            if (sid != 0UL && !taken.Add(sid)) continue;      // 已经在 Steam 那条路里出现过

            var e = new FriendEntry();
            e.steamIdText = s.steamIdText;
            e.playerId = s.playerId;
            e.name = s.name;
            e.manual = true;
            e.playedOurGame = s.playedOurGame;
            e.lastSeenUnix = s.lastSeenUnix;
            e.Presence = FriendPresence.InGameOnly;
            if (sid != 0UL) taken.Add(sid);
            result.Add(e);
        }

        result.Sort(CompareEntries);
        _entries.Clear();
        _entries.AddRange(result);

        if (dirty) FriendStore.Save(store);

        string sig = _entries.Count + "/" + store.Count + "/" + FriendEvidence.HasOwnedLookup;
        if (sig != _lastLog)
        {
            _lastLog = sig;
            Debug.Log("[FriendList] 好友表 " + _entries.Count + " 人（本地存了 " + store.Count + " 条，Steam " +
                      (SteamManager.Initialized ? "已就绪" : "未初始化") + "）｜规则：" + _chain.Describe());
        }

        if (Refreshed != null) Refreshed();
    }

    /// <summary>
    /// 记下「我们遇到过这个玩家」（匹配到对手 / 一起开局时调一次）—— 这是第 ③ 条证据 local-seen：
    /// 从此这个人只要是我的 Steam 好友，就会出现在好友表里。
    /// 现在还没人调它：等匹配流程接进来（「已找到对手」那一步）调一次即可。
    /// </summary>
    public static void RecordMetOpponent(ulong steamId, string name)
    {
        if (steamId == 0UL) return;
        List<FriendEntry> store = FriendStore.Load();
        if (FriendStore.MarkPlayed(store, steamId, name, "local-seen", 0, true))
        {
            FriendStore.Save(store);
            Debug.Log("[FriendList] 记下遇到过的玩家 SteamID=" + steamId + "（local-seen）");
        }
        if (Instance != null) Instance.Refresh();
    }

    static int CompareEntries(FriendEntry a, FriendEntry b)
    {
        int w = a.SortWeight.CompareTo(b.SortWeight);
        if (w != 0) return w;
        return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
