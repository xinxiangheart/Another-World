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
        Debug.Log("=== Game Start ===");
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
            StartCoroutine(BattleManager.Instance.BattleCoroutine());
        }
        else if (phase == TurnPhase.EnemyTurn)
        {
            currentPhase = TurnPhase.EnemyTurn;
            SetEndButton(false);
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

            if (NetworkServer.active)
            {
                NetworkTurnSync nts = FindObjectOfType<NetworkTurnSync>();
                if (nts != null)
                {
                    nts.currentPhaseId = (int)currentPhase;
                    nts.RpcPhaseChange((int)currentPhase);
                }
            }
        }
    }
}
