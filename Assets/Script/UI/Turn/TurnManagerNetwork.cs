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
        }
        else if (phase == TurnPhase.BattlePhase && currentPhase != TurnPhase.BattlePhase)
        {
            Debug.Log("[TurnManager] SetPhaseFromNetwork: ENTER BattlePhase");
            currentPhase = TurnPhase.BattlePhase;
            SetPlayerActionsEnabled(false);
            StartCoroutine(BattleManager.Instance.BattleCoroutine());
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

        Debug.Log($"[TurnManager] BroadcastTurnPhase: hostPhase={hostPhase}, Local={NetworkPlayer.Local?.netId}, Remote={NetworkPlayer.Remote?.netId}");

        if (NetworkPlayer.Local == null)
        {
            Debug.LogError("[TurnManager] BroadcastTurnPhase: NetworkPlayer.Local is NULL! Cannot broadcast.");
            return;
        }

        if (hostPhase == TurnPhase.BattlePhase || hostPhase == TurnPhase.PhaseStart)
        {
            // Same for both players
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)hostPhase);
            if (NetworkPlayer.Remote != null)
                NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)hostPhase);
            else
                Debug.LogWarning("[TurnManager] BroadcastTurnPhase: Remote is null, only Local received phase");
        }
        else if (hostPhase == TurnPhase.MyTurn)
        {
            // Host is active: host sees MyTurn, remote sees EnemyTurn
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.MyTurn);
            if (NetworkPlayer.Remote != null)
                NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)TurnPhase.EnemyTurn);
            else
                Debug.LogWarning("[TurnManager] BroadcastTurnPhase: Remote is null, remote missed EnemyTurn notification");
        }
        else // EnemyTurn (host perspective) = Remote is active
        {
            // Remote is active: host sees EnemyTurn, remote sees MyTurn
            NetworkPlayer.Local.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.EnemyTurn);
            if (NetworkPlayer.Remote != null)
                NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)TurnPhase.MyTurn);
            else
                Debug.LogWarning("[TurnManager] BroadcastTurnPhase: Remote is null, remote missed MyTurn notification");
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
}
