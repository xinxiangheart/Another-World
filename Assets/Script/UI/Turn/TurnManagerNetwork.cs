using UnityEngine;
using Mirror;

/// <summary>
/// Network extension for TurnManager (partial class).
/// All network-related TurnManager methods live here to avoid
/// modifying the GB2312-encoded TurnManager.cs.
/// </summary>
public partial class TurnManager
{
    /// <summary>Check if game has started (for network clients)</summary>
    public bool HasGameStarted()
    {
        return _gameStarted;
    }

    /// <summary>Called by NetworkTurnSync when the host signals game start</summary>
    public void StartGameForClient()
    {
        if (_gameStarted) return;
        Debug.Log("[TurnManager] StartGameForClient: starting game");
        _gameStarted = true;
        StartCoroutine(InitialDraw());
    }

    /// <summary>Called by RPC to sync current turn phase from server</summary>
    public void SetPhaseFromNetwork(TurnPhase phase)
    {
        Debug.Log($"[TurnManager] SetPhaseFromNetwork: {phase}, current={currentPhase}");

        if (phase == TurnPhase.MyTurn && currentPhase != TurnPhase.MyTurn)
        {
            currentPhase = TurnPhase.MyTurn;
            SetEndButton(true);
            NetworkPlayer.Local?.AddEnergy(6);
            FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
            TriggerMyTurnStartEffects();
            Debug.Log("[TurnManager] My turn started (from network)");
        }
        else if (phase == TurnPhase.BattlePhase)
        {
            currentPhase = TurnPhase.BattlePhase;
            StartCoroutine(BattleManager.Instance.BattleCoroutine());
            Debug.Log("[TurnManager] Battle phase started (from network)");
        }
        else if (phase == TurnPhase.EnemyTurn)
        {
            currentPhase = TurnPhase.EnemyTurn;
            SetEndButton(false);
            Debug.Log("[TurnManager] Enemy turn - waiting (from network)");
        }
    }

    /// <summary>
    /// Server-authoritative end turn. Validates the correct player is ending their turn,
    /// then processes the turn end and broadcasts the result to all clients.
    /// </summary>
    public void ServerEndTurn(NetworkPlayer player)
    {
        bool isHostTurn = isMyTurnFirst && (player == NetworkPlayer.Local);
        bool isRemoteTurn = !isMyTurnFirst && (player == NetworkPlayer.Remote);

        if (!isHostTurn && !isRemoteTurn)
        {
            Debug.LogWarning("[TurnManager] ServerEndTurn rejected: wrong player");
            return;
        }

        if (currentPhase == TurnPhase.MyTurn)
        {
            Debug.Log("[TurnManager] ServerEndTurn: ending turn");

            // Cap energy before ending turn
            if (player != null)
            {
                player._energyCanExceedLimit = false;
                if (player.currentEnergy > player.maxEnergy)
                    player.currentEnergy = player.maxEnergy;
                player.UpdateUI();
            }

            EndCurrentTurn();

            if (NetworkServer.active)
            {
                NetworkTurnSync nts = FindObjectOfType<NetworkTurnSync>();
                if (nts != null)
                {
                    nts.currentPhaseId = (int)currentPhase;
                    nts.RpcPhaseChange((int)currentPhase);
                    Debug.Log("[TurnManager] Phase change broadcast sent");
                }
            }
        }
    }
}
