using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Network extension for TurnManager (partial class).
/// All network-related TurnManager methods live here.
/// </summary>
public partial class TurnManager
{
    /// <summary>Check if game has started (delegates to NetworkTurnSync.Instance.gameStarted)</summary>
    [System.Obsolete]
    public bool HasGameStarted()
    {
        if (NetworkTurnSync.Instance != null)
            return NetworkTurnSync.Instance.gameStarted;
        return false;
    }

    /// <summary>Called by NetworkTurnSync when ready to start</summary>
    public void StartGameForClient()
    {
        Debug.Log("[TurnManager] StartGameForClient: starting game");
        StartCoroutine(InitialDraw());
    }

    /// <summary>Called by RPC to sync current turn phase from server</summary>
    public void SetPhaseFromNetwork(TurnPhase phase)
    {
        Debug.Log($"[TurnManager] SetPhaseFromNetwork: phase={phase}, currentPhase={currentPhase}, isServer={NetworkServer.active}");

        if (phase == TurnPhase.MyTurn && currentPhase != TurnPhase.MyTurn)
        {
            Debug.Log("[TurnManager] SetPhaseFromNetwork: ENTER MyTurn");
            currentPhase = TurnPhase.MyTurn;
            SetPlayerActionsEnabled(false);

            // 影舞者(01502)：若 PhaseStart 还没处理过影子（兜底），此时补上
            if (CardInstance.shadowMasterAlive && !_shadowsReenteredThisPhase)
            {
                BoardSlot bs = FindObjectOfType<BoardSlot>();
                if (bs != null)
                {
                    StartCoroutine(ShadowReentryThenEnableActions());
                    return;
                }
            }
            _shadowsReenteredThisPhase = false;
            EnableMyTurnActions();
        }
        else if (phase == TurnPhase.BattlePhase && currentPhase != TurnPhase.BattlePhase)
        {
            Debug.Log("[TurnManager] SetPhaseFromNetwork: ENTER BattlePhase");
            currentPhase = TurnPhase.BattlePhase;
            SetPlayerActionsEnabled(false);
            // Server runs battle — client waits for stat sync after battle
        }
        else if (phase == TurnPhase.EnemyTurn && currentPhase != TurnPhase.EnemyTurn)
        {
            Debug.Log("[TurnManager] SetPhaseFromNetwork: ENTER EnemyTurn — disabling actions");
            currentPhase = TurnPhase.EnemyTurn;
            SetPlayerActionsEnabled(false);
        }
        else if (phase == TurnPhase.PhaseStart && currentPhase != TurnPhase.PhaseStart)
        {
            Debug.Log("[TurnManager] SetPhaseFromNetwork: ENTER PhaseStart");
            currentPhase = TurnPhase.PhaseStart;
            SetPlayerActionsEnabled(false);

            if (!NetworkServer.active)
            {
                // 远程客户端：phaseCount 只在主机 StartNewPhase 递增，客户端需在收到 PhaseStart 时同步递增
                //（与主机 StartNewPhase 的 phaseCount++ 一一对应，用于字幕"第X阶段"/轮盘第一回合判断）
                phaseCount++;
                // 远程客户端：处理全部阶段开始效果（影子/铁匠/执行之剑/忤逆者等）
                if (CardInstance.shadowMasterAlive)
                {
                    BoardSlot bs = FindObjectOfType<BoardSlot>();
                    if (bs != null)
                    {
                        StartCoroutine(ShadowReentryThenPhaseStartTriggers(bs));
                        return;
                    }
                }
                RemotePhaseStartReady();
            }
        }
    }

    /// <summary>
    /// Server broadcasts a phase change. For MyTurn/EnemyTurn,
    /// each player receives a different phase based on perspective.
    /// BattlePhase and PhaseStart are broadcast equally to both.
    /// </summary>
    public void BroadcastTurnPhase(TurnPhase hostPhase)
    {
        if (!NetworkServer.active) return;
        if (NetworkPlayer.Local == null) return;
        // Remote disconnected — game is ending, skip broadcast
        if (NetworkPlayer.Remote == null) return;

        // AI 对手（server-only，无客户端连接）时，只广播给 Local，跳过 Remote RPC
        var remoteConn = NetworkPlayer.Remote.connectionToClient;

        if (hostPhase == TurnPhase.BattlePhase || hostPhase == TurnPhase.PhaseStart)
        {
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)hostPhase);
            if (remoteConn != null)
                NetworkPlayer.Remote.TargetSetPhase(remoteConn, (int)hostPhase);
        }
        else if (hostPhase == TurnPhase.MyTurn)
        {
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.MyTurn);
            if (remoteConn != null)
                NetworkPlayer.Remote.TargetSetPhase(remoteConn, (int)TurnPhase.EnemyTurn);
        }
        else // EnemyTurn (host perspective) = Remote is active
        {
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.EnemyTurn);
            if (remoteConn != null)
                NetworkPlayer.Remote.TargetSetPhase(remoteConn, (int)TurnPhase.MyTurn);
        }
    }

    /// <summary>
    /// Server-authoritative end turn. Validates the requesting player
    /// matches the current phase (MyTurn=Host, EnemyTurn=Remote from host perspective).
    /// </summary>
    public void ServerEndTurn(NetworkPlayer player)
    {
        bool isValid;
        if (currentPhase == TurnPhase.MyTurn)
            isValid = (player == NetworkPlayer.Local);
        else if (currentPhase == TurnPhase.EnemyTurn)
            isValid = (player == NetworkPlayer.Remote);
        else
            isValid = false;

        if (!isValid)
        {
            Debug.LogWarning($"[TurnManager] ServerEndTurn rejected: phase={currentPhase}, player netId={player?.netId}");
            return;
        }

        Debug.Log($"[TurnManager] ServerEndTurn accepted: netId={player?.netId}");

        // Clean up the requesting player's energy BEFORE EndCurrentTurn
        player._energyCanExceedLimit = false;
        if (player.currentEnergy > player.maxEnergy)
            player.currentEnergy = player.maxEnergy;
        player.UpdateUI();

        // Server-authoritative end turn. Energy cleanup already done for the correct player.
        EndCurrentTurn(skipEnergyCleanup: true);
    }

    /// <summary>
    /// Unified sync — call after any local board change (damage/transform/swap/death).
    /// Host: BoardSyncManager.MarkDirty() broadcasts to remote.
    /// Pure client: reports full 12-slot snapshot to server.
    /// Replaces old CmdSyncEnemyDamage / CmdPirateFinalize / CmdReportTransform.
    /// </summary>
    public static void SyncMyBoardToOpponent()
    {
        if (!NetworkClient.isConnected) return;
        if (NetworkServer.active)
        {
            // Host: server IS this client, board is already correct. Just broadcast.
            BoardSyncManager.MarkDirty();
            return;
        }
        // Pure client: report all 12 slots to server
        ReportAllSlots();
    }

    /// <summary>
    /// Packs all 12 slots + attachments and sends to server.
    /// Server handler (CmdReportAllSlots) applies the state, runs CheckAndHandleDeaths,
    /// then MarkDirty to broadcast to the other client.
    /// </summary>
    static void ReportAllSlots()
    {
        BoardManager bm = Object.FindObjectOfType<BoardManager>();
        if (bm == null) return;
        string[] all = new string[12];
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            var c3d = slot?.currentCard3D?.GetComponent<Card3DInstance>();
            var ci = c3d?.cardInstance;
            string cardPart;
            if (ci == null)
                cardPart = "";
            else
                cardPart = string.Join("|",
                    ci.templateID ?? "",
                    ci.currentHealth, ci.currentAttack, ci.currentMaxHealth,
                    ci.baseAttack, ci.baseHealth, ci.baseMaxHealth,
                    ci.currentCost, ci.currentTier, ci.baseTier,
                    ci.hasShield ? (1+(ci.shieldIsPermanent?2:0)+(ci.shieldEndAtBattleStart?4:0)+(ci.shieldEndAtBattleEnd?8:0)).ToString() : "0",
                    ci.silencedThisPhase ? "1" : "0",
                    ci.isAttached ? "1" : "0",
                    ci.poisoned ? "1" : "0",
                    ci.prefixes ?? "",
                    ci.grantedTraitTexts != null && ci.grantedTraitTexts.Count > 0
                        ? string.Join(";;", ci.grantedTraitTexts) : "",
                    ci.totalDamageTaken);
            string flagPart = slot == null ? "0000000|0|0|0|0" :
                $"{(slot.isBlocked?1:0)}{(slot.prisonBlocked?1:0)}{(slot.hasPlague?1:0)}{(slot.hasSpotlight?1:0)}{(slot.deepSeaMarked?1:0)}{(slot.deepSeaHealthDebuff?1:0)}{(slot.permaBlocked?1:0)}|{slot.plagueRoundCount}|{slot.spotlightTierBoost}|{slot.slotTempAttackBoost}~{slot.deepSeaAttackDebuff}";
            all[i] = $"{cardPart}|{flagPart}";
        }

        bm.attachedModels.RemoveAll(a => a == null);
        var attachParts = new System.Collections.Generic.List<string>();
        foreach (var o in bm.attachedModels)
        {
            var ci = o.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached)
                attachParts.Add($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}|{ci.instanceID ?? ""}");
        }
        string attachBlock = attachParts.Count > 0 ? string.Join("||", attachParts) : "";
        // 前缀：携带客户端已确认的 gen，服务端据此判断附件数据是否过期
        attachBlock = "G" + BoardManager.attachGen + "|" + attachBlock;

        NetworkPlayer.Local?.CmdReportAllSlots(all, attachBlock);
    }

    /// <summary>
    /// 影舞者(01502)辅助：先完成影子重新进场（玩家依次选格子），再处理阶段开始触发器，最后通知服务器。
    /// </summary>
    IEnumerator ShadowReentryThenPhaseStartTriggers(BoardSlot bs)
    {
        _shadowsReenteredThisPhase = true;
        yield return bs.SummonAllShadows();
        RemotePhaseStartReady();
    }

    /// <summary>远程客户端阶段开始处理完成，通知服务器。</summary>
    void RemotePhaseStartReady()
    {
        // 01524 画卷之核 — 阶段数增加 + 费用更新（客户端也需要）
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.templateID == "01524")
            {
                ci.scrollCorePhaseCount++;
                if (ci.scrollCorePhaseCount > 5) ci.scrollCorePhaseCount = 5;
                ci.currentCost = ci.scrollCorePhaseCount;
                card.GetComponent<CardDisplay2D>()?.Refresh();
            }
        }
        ProcessPhaseStartTriggers();
        ProcessPhaseStartDeaths();
        ReportAllSlots();
        NetworkPlayer.Local?.CmdPhaseStartReady();
    }

    /// <summary>
    /// 影舞者(01502)辅助：先完成影子重新进场，再启用回合行动。
    /// 用于 SetPhaseFromNetwork(MyTurn) 的兜底路径（PhaseStart 未触发时）。
    /// </summary>
    IEnumerator ShadowReentryThenEnableActions()
    {
        _shadowsReenteredThisPhase = true;
        BoardSlot bs = FindObjectOfType<BoardSlot>();
        if (bs != null)
            yield return bs.SummonAllShadows();
        EnableMyTurnActions();
    }

    /// <summary>
    /// 启用 MyTurn 行动：能量、抽牌、回合开始效果。
    /// 阶段开始触发器（01525/01535/01526）已在 PhaseStart 处理，此处不再重复。
    /// </summary>
    void EnableMyTurnActions()
    {
        Debug.Log("[TurnManager] SetPhaseFromNetwork: enabling actions");
        SetPlayerActionsEnabled(true);
        // 主机已在 StartNewPhase 中直接加能，此处仅远程客户端需要
        if (!NetworkServer.active)
        {
            if (NetworkPlayer.Local != null) NetworkPlayer.Local.AddEnergy(6);
            else Debug.LogError("[TurnManager] SetPhaseFromNetwork: NetworkPlayer.Local is NULL!");
        }
        DrawCardUI dc = FindObjectOfType<DrawCardUI>();
        if (dc != null) dc.ResetForNewPhase();
        else Debug.LogWarning("[TurnManager] SetPhaseFromNetwork: DrawCardUI not found!");
        TriggerMyTurnStartEffects();
        // Send updated stats to server so other client sees phase-start effects.
        if (NetworkClient.isConnected && !NetworkServer.active)
            ReportAllSlots();
    }

}
