using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 择牌（Pick Draw）—— NetworkPlayer 的网络与服务端权威部分。
///
/// 规则：
///   · 己方回合的**第一次主动抽牌**（点抽牌按钮）被替换为择牌：不随机抽一张，
///     改为亮出牌库顶三张，选一张加入手牌。
///   · 另外两张对对方翻正面展示，视为「明弃」：进弃牌堆，但**永不再洗回牌库**。
///   · 每回合一次，**不跨回合保留**：本回合不用即作废（用 phaseCount 做键，天然自清）。
///
/// 只有「点抽牌按钮」这条路径会触发择牌；卡牌效果造成的抽牌走 DrawCard()/DrawCardWithoutLimit()，
/// 一律不受影响。服务端权威：牌库、待选三张、明弃与入账都在服务端决定。
/// </summary>
public partial class NetworkPlayer
{
    /// <summary>择牌亮出的张数（牌库不足则按实际张数）。</summary>
    public const int PickDrawCount = 3;

    // ── 服务端状态（只有服务端读写）──
    int _pickDrawUsedPhase = -1;                                        // 本回合是否已用过择牌（= TurnManager.phaseCount）
    int _pickDrawPhase = -1;                                            // 当前这一局择牌发生在哪个 phaseCount
    bool _pickDrawPending;                                              // 是否有一局择牌正在等选择
    readonly List<(string tid, string iid)> _pickDrawHeld = new();      // 已从牌库取出、等选择的牌

    public bool IsPickDrawPending => _pickDrawPending;

    // ══════════════════════════════════════════════════════════════════
    // 服务端：发起
    // ══════════════════════════════════════════════════════════════════

    /// <summary>本回合的择牌机会是否还在（服务端权威）。每个阶段每人一次，
    /// 回合过去键就变了 —— 不使用即作废，不跨回合保留。</summary>
    public bool ServerCanPickDraw()
    {
        if (!NetworkServer.active) return false;
        TurnManager tm = TurnManager.Instance;
        if (tm == null) return false;
        if (_pickDrawPending) return false;
        return _pickDrawUsedPhase != tm.phaseCount;
    }

    /// <summary>「主动抽牌」入口：能择牌就转为择牌并返回 true（调用方不要再抽牌）；
    /// 返回 false 表示按老规矩随机抽一张。</summary>
    public bool ServerTryStartPickDraw()
    {
        if (!ServerCanPickDraw()) return false;

        TurnManager tm = TurnManager.Instance;
        if (tm == null) return false;
        // 严格限定在「该玩家的行动回合」：PhaseStart / BattlePhase 不算
        if (tm.currentPhase != TurnManager.TurnPhase.MyTurn &&
            tm.currentPhase != TurnManager.TurnPhase.EnemyTurn) return false;
        if (!IsMyTurnOnServer(tm)) return false;

        CardZoneManager czm = CardZoneManager.Instance;
        if (czm == null || czm.DeckCount <= 0) return false;

        // 取出牌库顶若干张（移出牌库，等选择）
        List<DeckCard> top = czm.PeekTopCards(PickDrawCount);
        _pickDrawHeld.Clear();
        for (int i = 0; i < top.Count; i++)
        {
            if (string.IsNullOrEmpty(top[i].instanceID)) continue;
            if (!czm.RemoveFromDeck(top[i].instanceID)) continue;
            _pickDrawHeld.Add((top[i].templateID, top[i].instanceID));
        }
        if (_pickDrawHeld.Count == 0) return false;

        _pickDrawUsedPhase = tm.phaseCount;
        _pickDrawPhase = tm.phaseCount;
        _pickDrawPending = true;

        string[] tids = new string[_pickDrawHeld.Count];
        for (int i = 0; i < tids.Length; i++) tids[i] = _pickDrawHeld[i].tid;

        if (connectionToClient != null)
            TargetBeginPickDraw(connectionToClient, tids);
        else
            Debug.LogWarning($"[PickDraw] netId={netId} 无客户端连接，择牌无法展示");

        // 对手：同一时刻只看到同样数量的牌背
        NetworkPlayer opp = ServerPickDrawOpponent();
        if (opp != null && opp.connectionToClient != null)
            opp.TargetBeginPickSpectate(opp.connectionToClient, _pickDrawHeld.Count);

        Debug.Log($"[PickDraw] netId={netId} 择牌开始：{string.Join(",", tids)}");
        return true;
    }

    /// <summary>「主动抽牌」的服务端总入口（主机 / 离线直调路径）：
    /// 择牌面板正开着 → 本次点击作废（返回 false，调用方退能量与次数）；
    /// 本回合还没用过择牌 → 转择牌；否则照常随机抽一张。</summary>
    public bool ServerHandleActiveDraw()
    {
        if (_pickDrawPending) return false;
        // 择牌依赖服务端牌库；非服务端（纯单机）走原来的随机抽牌，行为不变
        if (NetworkServer.active && ServerTryStartPickDraw()) return true;
        DrawCard();
        return true;
    }

    // ══════════════════════════════════════════════════════════════════
    // 服务端：结算
    // ══════════════════════════════════════════════════════════════════

    /// <summary>本端提交选择（主机走服务端直调，远程客户端走 Cmd）。</summary>
    public void SubmitPickDrawChoice(int index)
    {
        if (NetworkServer.active) ServerApplyPickDrawChoice(index);
        else CmdPickDrawChoice(index);
    }

    [Command]
    public void CmdPickDrawChoice(int index)
    {
        ServerApplyPickDrawChoice(index);
    }

    void ServerApplyPickDrawChoice(int index)
    {
        if (!NetworkServer.active || !_pickDrawPending) return;
        if (index < 0 || index >= _pickDrawHeld.Count) index = 0;

        var held = new List<(string tid, string iid)>(_pickDrawHeld);
        _pickDrawHeld.Clear();
        _pickDrawPending = false;

        // ── 选中的那张 → 加入手牌 ──
        var chosen = held[index];
        CardData tpl = CardDatabase.Instance?.GetTemplate(chosen.tid);
        if (tpl != null)
        {
            if (connectionToClient != null)
                TargetReceiveCard(connectionToClient, chosen.tid, chosen.iid);
            AddServerSideCard(tpl, chosen.iid);
        }

        // ── 其余 → 明弃：进弃牌堆，但永不洗回牌库 ──
        var discIdx = new List<int>();
        var discTids = new List<string>();
        for (int i = 0; i < held.Count; i++)
        {
            if (i == index) continue;
            discIdx.Add(i);
            discTids.Add(held[i].tid);
            CardZoneManager.Instance?.AddToGraveyard(new GraveEntry
            {
                templateID = held[i].tid,
                instanceID = held[i].iid,
                deathPhase = TurnManager.Instance != null ? TurnManager.Instance.phaseCount : 0,
            }, openDiscard: true);
        }

        // ── 两端收尾 ──
        if (connectionToClient != null)
            TargetPickDrawResolved(connectionToClient, index);

        NetworkPlayer opp = ServerPickDrawOpponent();
        if (opp != null && opp.connectionToClient != null && discIdx.Count > 0)
            opp.TargetPickDrawDiscardReveal(opp.connectionToClient, discIdx.ToArray(), discTids.ToArray());

        Debug.Log($"[PickDraw] netId={netId} 选中 #{index} {chosen.tid}，明弃 {discTids.Count} 张（不再回牌库）");
    }

    /// <summary>服务端中止未完成的择牌：待选牌洗回牌库，两端面板收起。用于阶段推进 / 断线。</summary>
    public void ServerAbortPickDraw(string reason)
    {
        if (!NetworkServer.active || !_pickDrawPending) return;
        _pickDrawPending = false;

        if (_pickDrawHeld.Count > 0)
        {
            var back = new List<(string templateID, string instanceID)>();
            for (int i = 0; i < _pickDrawHeld.Count; i++)
                back.Add((_pickDrawHeld[i].tid, _pickDrawHeld[i].iid));
            CardZoneManager.Instance?.ShuffleIntoDeck(back);
        }
        _pickDrawHeld.Clear();

        if (connectionToClient != null) TargetPickDrawAbort(connectionToClient);
        NetworkPlayer opp = ServerPickDrawOpponent();
        if (opp != null && opp.connectionToClient != null) opp.TargetPickDrawAbort(opp.connectionToClient);

        Debug.Log($"[PickDraw] netId={netId} 中止：{reason}");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (_pickDrawPending) ServerAbortPickDraw("连接断开");
    }

    /// <summary>服务端每帧兜底：择牌期间回合推进 → 立刻中止（面板挡得住点击，挡不住阶段被别处推进）。</summary>
    void ServerTickPickDraw()
    {
        if (!_pickDrawPending) return;

        TurnManager tm = TurnManager.Instance;
        if (tm == null) { ServerAbortPickDraw("TurnManager 缺失"); return; }
        if (tm.phaseCount != _pickDrawPhase) { ServerAbortPickDraw("阶段已推进"); return; }

        if (tm.currentPhase == TurnManager.TurnPhase.MyTurn && this != NetworkPlayer.LocalHalfPlayer)
            ServerAbortPickDraw("已不是该玩家的回合");
        else if (tm.currentPhase == TurnManager.TurnPhase.EnemyTurn && this != NetworkPlayer.RemoteHalfPlayer)
            ServerAbortPickDraw("已不是该玩家的回合");
    }

    /// <summary>这一局择牌的对手方（离线的 AI 对手 connectionToClient == null，不会收到 RPC）。</summary>
    NetworkPlayer ServerPickDrawOpponent()
    {
        if (this == NetworkPlayer.LocalHalfPlayer) return NetworkPlayer.RemoteHalfPlayer;
        if (this == NetworkPlayer.RemoteHalfPlayer) return NetworkPlayer.LocalHalfPlayer;

        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            var p = conn != null && conn.identity != null ? conn.identity.GetComponent<NetworkPlayer>() : null;
            if (p != null && p != this) return p;
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════════
    // 网络回调
    // ══════════════════════════════════════════════════════════════════

    /// <summary>服务端 → 选择者：亮出牌库顶若干张的正面，等着点。</summary>
    [TargetRpc]
    public void TargetBeginPickDraw(NetworkConnectionToClient target, string[] templateIDs)
    {
        PickDrawUI.ShowChooser(templateIDs, SubmitPickDrawChoice);
    }

    /// <summary>服务端 → 对手：同一位置只亮牌背。</summary>
    [TargetRpc]
    public void TargetBeginPickSpectate(NetworkConnectionToClient target, int count)
    {
        PickDrawUI.ShowSpectator(count);
    }

    /// <summary>服务端 → 选择者：已结算（面板已在点击时自行退场，这里只兜底）。</summary>
    [TargetRpc]
    public void TargetPickDrawResolved(NetworkConnectionToClient target, int chosenIndex)
    {
        PickDrawUI.ChooserConfirm(chosenIndex);
    }

    /// <summary>服务端 → 对手：被明弃的那两张翻正面对其展示。</summary>
    [TargetRpc]
    public void TargetPickDrawDiscardReveal(NetworkConnectionToClient target, int[] indices, string[] templateIDs)
    {
        PickDrawUI.SpectatorReveal(indices, templateIDs);
    }

    /// <summary>服务端 → 两端：收起择牌面板。</summary>
    [TargetRpc]
    public void TargetPickDrawAbort(NetworkConnectionToClient target)
    {
        PickDrawUI.Abort();
    }
}
