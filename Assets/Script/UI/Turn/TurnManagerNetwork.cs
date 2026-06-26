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
        Debug.Log(string.Format("[TurnManager] SetPhaseFromNetwork: {0}, current={1}", phase, currentPhase));

        if (phase == TurnPhase.MyTurn && currentPhase != TurnPhase.MyTurn)
        {
            currentPhase = TurnPhase.MyTurn;
            SetEndButton(true);
            if (NetworkPlayer.Local != null) NetworkPlayer.Local.AddEnergy(6);
            FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
            TriggerMyTurnStartEffects();
        }
        else if (phase == TurnPhase.BattlePhase)
        {
            currentPhase = TurnPhase.BattlePhase;
            SetEndButton(false);
            StartCoroutine(BattleManager.Instance.BattleCoroutine());
        }
        else if (phase == TurnPhase.EnemyTurn)
        {
            currentPhase = TurnPhase.EnemyTurn;
            SetEndButton(false);
        }
        else if (phase == TurnPhase.PhaseStart)
        {
            currentPhase = TurnPhase.PhaseStart;
            SetEndButton(false);
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

        if (hostPhase == TurnPhase.BattlePhase || hostPhase == TurnPhase.PhaseStart)
        {
            // Same for both players
            NetworkPlayer.Local?.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)hostPhase);
            if (NetworkPlayer.Remote != null)
                NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)hostPhase);
        }
        else if (hostPhase == TurnPhase.MyTurn)
        {
            // Host is active: host sees MyTurn, remote sees EnemyTurn
            NetworkPlayer.Local?.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.MyTurn);
            if (NetworkPlayer.Remote != null)
                NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)TurnPhase.EnemyTurn);
        }
        else // EnemyTurn (host perspective) = Remote is active
        {
            // Remote is active: host sees EnemyTurn, remote sees MyTurn
            NetworkPlayer.Local?.TargetSetPhase(NetworkPlayer.Local.connectionToClient, (int)TurnPhase.EnemyTurn);
            if (NetworkPlayer.Remote != null)
                NetworkPlayer.Remote.TargetSetPhase(NetworkPlayer.Remote.connectionToClient, (int)TurnPhase.MyTurn);
        }
    }

    /// <summary>
    /// Server-authoritative end turn.
    /// </summary>
    public void ServerEndTurn(NetworkPlayer player)
    {
        bool isHostTurn = isMyTurnFirst && (player == NetworkPlayer.Local);
        bool isRemoteTurn = !isMyTurnFirst && (player == NetworkPlayer.Remote);

        if (!isHostTurn && !isRemoteTurn)
        {
            Debug.LogWarning("[TurnManager] ServerEndTurn rejected: not player's turn");
            return;
        }

        if (currentPhase == TurnPhase.MyTurn)
        {
            Debug.Log("[TurnManager] ServerEndTurn: ending turn");

            if (player != null)
            {
                player._energyCanExceedLimit = false;
                if (player.currentEnergy > player.maxEnergy)
                    player.currentEnergy = player.maxEnergy;
                player.UpdateUI();
            }

            EndCurrentTurn();
        }
    }
}
