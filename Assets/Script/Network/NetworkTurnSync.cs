using Mirror;
using UnityEngine;

/// <summary>
/// Server-authoritative network bridge for turn coordination.
/// Must be placed on a GameObject with NetworkIdentity (e.g. as child of NetworkManager).
/// Host assigns first player, syncs game start, routes turn requests.
/// </summary>
public class NetworkTurnSync : NetworkBehaviour
{
    public static NetworkTurnSync Instance { get; private set; }

    [SyncVar(hook = nameof(OnHostFirstChanged))]
    public bool isHostFirst = true;

    [SyncVar(hook = nameof(OnGameStartedChanged))]
    public bool gameStarted;

    [SyncVar(hook = nameof(OnCurrentPhaseChanged))]
    public int currentPhaseId;

    TurnManager turnManager;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        turnManager = FindObjectOfType<TurnManager>();
    }

    public override void OnStartServer()
    {
        isHostFirst = Random.value > 0.5f;
        Debug.Log($"[NetworkTurnSync] Server started. isHostFirst = {isHostFirst}");
    }

    void Update()
    {
        // Host: check if both players are ready and start the game
        if (NetworkServer.active && !gameStarted && turnManager != null)
        {
            if (NetworkPlayer.Local != null && NetworkPlayer.Remote != null)
            {
                // Both players connected - start the game!
                gameStarted = true;
                Debug.Log("[NetworkTurnSync] Both players ready, starting game!");

                // Host starts the game
                turnManager.enabled = true;
                if (!turnManager.HasGameStarted())
                {
                    turnManager.StartGameForClient();
                }

                // Notify remote client
                RpcStartGame();
            }
        }
    }

    public override void OnStartClient()
    {
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();
        ApplyFirstPlayer();
        Debug.Log($"[NetworkTurnSync] Client started. isLocalPlayer={isLocalPlayer}, isHostFirst={isHostFirst}");
    }

    void ApplyFirstPlayer()
    {
        if (turnManager == null) return;
        if (isLocalPlayer)
            turnManager.isMyTurnFirst = isHostFirst;
        else
            turnManager.isMyTurnFirst = !isHostFirst;
        Debug.Log($"[NetworkTurnSync] isMyTurnFirst = {turnManager.isMyTurnFirst}");
    }

    void OnHostFirstChanged(bool oldValue, bool newValue) { ApplyFirstPlayer(); }

    void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        if (newValue && turnManager != null && !turnManager.HasGameStarted())
        {
            Debug.Log("[NetworkTurnSync] Game start signal received!");
            turnManager.enabled = true;
            turnManager.StartGameForClient();
        }
    }

    void OnCurrentPhaseChanged(int oldValue, int newValue)
    {
        if (turnManager != null)
        {
            turnManager.SetPhaseFromNetwork((TurnManager.TurnPhase)newValue);
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdRequestEndTurn()
    {
        NetworkPlayer player = connectionToClient?.identity?.GetComponent<NetworkPlayer>();
        if (player != null && turnManager != null)
        {
            turnManager.ServerEndTurn(player);
        }
    }

    [ClientRpc]
    public void RpcStartGame()
    {
        Debug.Log("[NetworkTurnSync] RpcStartGame received");
        if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();
        gameStarted = true;
        if (turnManager != null)
        {
            turnManager.enabled = true;
            if (!turnManager.HasGameStarted())
                turnManager.StartGameForClient();
        }
    }

    [ClientRpc]
    public void RpcPhaseChange(int phaseId)
    {
        Debug.Log($"[NetworkTurnSync] RpcPhaseChange: phase={phaseId}");
        currentPhaseId = phaseId;
    }
}
