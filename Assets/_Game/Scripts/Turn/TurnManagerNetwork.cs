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
            // Send updated stats to server so other client sees phase-start effects.
            // Only pure client reports — host IS the server.
            if (NetworkClient.isConnected && !NetworkServer.active)
                ReportMyBoard();
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
    /// Public resync entry point — call after any local board mutation
    /// (prefix add, transform, attach) so the opponent's view updates.
    /// Only does anything when running as a PURE client (NOT host-as-server).
    /// Host's board IS the server board — no client→server report needed.
    /// </summary>
    public static void SyncMyBoardToOpponent()
    {
        // Only pure clients report. Host=server, its board is the authority.
        if (NetworkClient.isConnected && !NetworkServer.active)
            ReportMyBoard();
    }

    /// <summary>
    /// Call after TriggerMyTurnStartEffects on any client.
    /// Packs slots 6-11 and sends to server for relay.
    /// </summary>
    static void ReportMyBoard()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        string[] my = new string[6];
        for (int i = 0; i < 6; i++)
        {
            var c3d = bm.GetSlot(i + 6)?.currentCard3D?.GetComponent<Card3DInstance>();
            var ci = c3d?.cardInstance;
            if (ci == null) { my[i] = ""; continue; }
            my[i] = string.Join("|",
                ci.templateID ?? "",
                ci.currentHealth, ci.currentAttack, ci.currentMaxHealth,
                ci.currentCost, ci.currentTier,
                ci.hasShield ? "1" : "0",
                ci.silencedThisPhase ? "1" : "0",
                ci.isAttached ? "1" : "0",
                ci.poisoned ? "1" : "0",
                ci.prefixes ?? "");
        }

        // Also serialize attachments whose host is in our ally slots (6-11)
        // Same format as BoardSyncManager.SyncNow attachBlock
        bm.attachedModels.RemoveAll(a => a == null);
        var attachParts = new System.Collections.Generic.List<string>();
        foreach (var o in bm.attachedModels)
        {
            var ci = o.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached && ci.hostSlotID >= 6)
                attachParts.Add($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}");
        }
        string attachBlock = attachParts.Count > 0 ? string.Join("||", attachParts) : "";

        NetworkPlayer.Local?.CmdReportMyBoard(my, attachBlock);
    }
}
