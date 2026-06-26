using Mirror;
using UnityEngine;

/// <summary>
/// Server-authoritative network bridge for turn coordination.
/// Must be on a GameObject with NetworkIdentity.
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

    [SyncVar(hook = nameof(OnIsMyTurnFirstChanged))]
    public bool isMyTurnFirst;

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
        isMyTurnFirst = isHostFirst;
        Debug.Log($"[NetworkTurnSync] Server started. isHostFirst={isHostFirst}");
    }

    void Update()
    {
        if (NetworkServer.active && !gameStarted && turnManager != null)
        {
            if (NetworkPlayer.Local != null && NetworkPlayer.Remote != null)
            {
                Debug.Log("[NetworkTurnSync] Both players ready, starting game!");
                turnManager.enabled = true;

                // Server runs initial draw (handles both players)
                turnManager.StartGameForClient();

                // After initial draw completes, broadcast game start
                gameStarted = true;

                // Sync initial phase to all clients
                currentPhaseId = (int)turnManager.currentPhase;
                RpcPhaseChange((int)turnManager.currentPhase);

                Debug.Log($"[NetworkTurnSync] Game started, phase={turnManager.currentPhase}, isMyTurnFirst={isMyTurnFirst}");
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
        turnManager.isMyTurnFirst = isLocalPlayer ? isMyTurnFirst : !isMyTurnFirst;
        Debug.Log($"[NetworkTurnSync] isMyTurnFirst={turnManager.isMyTurnFirst} (local)");
    }

    void OnHostFirstChanged(bool oldValue, bool newValue) { ApplyFirstPlayer(); }
    void OnIsMyTurnFirstChanged(bool oldValue, bool newValue) { ApplyFirstPlayer(); }

    void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        if (newValue && turnManager != null)
        {
            Debug.Log("[NetworkTurnSync] Game start signal received!");
            turnManager.enabled = true;
            // Server already started via InitialDraw. Client waits for RpcPhaseChange.
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
            // Initial draw already happened on server and cards were sent via TargetRpc.
            // Phase broadcast follows via RpcPhaseChange.
        }
    }

    [ClientRpc]
    public void RpcPhaseChange(int phaseId)
    {
        Debug.Log($"[NetworkTurnSync] RpcPhaseChange: phase={phaseId}");
        currentPhaseId = phaseId;
    }

    /// <summary>
    /// Server call: flip first player after battle for next phase pair.
    /// SyncVar auto-propagates to all clients.
    /// </summary>
    [Server]
    public void SwapFirstPlayer()
    {
        isMyTurnFirst = !isMyTurnFirst;
        Debug.Log($"[NetworkTurnSync] Swapped first player: isMyTurnFirst={isMyTurnFirst}");
    }
}
