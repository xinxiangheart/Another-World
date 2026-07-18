using UnityEngine;
using Mirror;

/// <summary>
/// Network extension for TurnManager (partial class).
/// All network-related TurnManager methods live here.
/// </summary>
public partial class TurnManager
{
    /// <summary>Check if game has started (delegates to NetworkTurnSync.Instance.gameStarted)</summary>
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
            Debug.Log("[TurnManager] SetPhaseFromNetwork: ENTER MyTurn — enabling actions, +6 energy");
            currentPhase = TurnPhase.MyTurn;
            SetPlayerActionsEnabled(true);
            if (NetworkPlayer.Local != null) NetworkPlayer.Local.AddEnergy(6);
            else Debug.LogError("[TurnManager] SetPhaseFromNetwork: NetworkPlayer.Local is NULL!");
            DrawCardUI dc = FindObjectOfType<DrawCardUI>();
            if (dc != null) dc.ResetForNewPhase();
            else Debug.LogWarning("[TurnManager] SetPhaseFromNetwork: DrawCardUI not found!");
            TriggerMyTurnStartEffects();
            // Each client processes OWN phase-start triggers (01525/01535/01526).
            // Host handles this in EndCurrentTurn/StartNewPhase directly.
            if (!NetworkServer.active)
                ProcessPhaseStartTriggers();
            // Client processes "下阶段退场" deaths (host does it in StartNewPhase)
            if (!NetworkServer.active)
                ProcessPhaseStartDeaths();
            // Send updated stats to server so other client sees phase-start effects.
            // Only pure client reports — host IS the server.
            if (NetworkClient.isConnected && !NetworkServer.active)
                ReportAllSlots();
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
        else if (phase == TurnPhase.PhaseStart)
        {
            currentPhase = TurnPhase.PhaseStart;
            SetPlayerActionsEnabled(false);
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

        if (hostPhase == TurnPhase.BattlePhase || hostPhase == TurnPhase.PhaseStart)
        {
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)hostPhase);
            NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)hostPhase);
        }
        else if (hostPhase == TurnPhase.MyTurn)
        {
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.MyTurn);
            NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)TurnPhase.EnemyTurn);
        }
        else // EnemyTurn (host perspective) = Remote is active
        {
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.EnemyTurn);
            NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)TurnPhase.MyTurn);
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
                    ci.hasShield ? "1" : "0",
                    ci.silencedThisPhase ? "1" : "0",
                    ci.isAttached ? "1" : "0",
                    ci.poisoned ? "1" : "0",
                    ci.prefixes ?? "",
                    ci.grantedTraitTexts != null && ci.grantedTraitTexts.Count > 0
                        ? string.Join(";;", ci.grantedTraitTexts) : "");
            string flagPart = slot == null ? "0000|0|0|0" :
                $"{(slot.isBlocked?1:0)}{(slot.prisonBlocked?1:0)}{(slot.hasPlague?1:0)}{(slot.hasSpotlight?1:0)}|{slot.plagueRoundCount}|{slot.spotlightTierBoost}|{slot.slotTempAttackBoost}";
            all[i] = $"{cardPart}|{flagPart}";
        }

        bm.attachedModels.RemoveAll(a => a == null);
        var attachParts = new System.Collections.Generic.List<string>();
        foreach (var o in bm.attachedModels)
        {
            var ci = o.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached)
                attachParts.Add($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}");
        }
        string attachBlock = attachParts.Count > 0 ? string.Join("||", attachParts) : "";

        NetworkPlayer.Local?.CmdReportAllSlots(all, attachBlock);
    }

}
