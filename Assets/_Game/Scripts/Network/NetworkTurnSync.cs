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

    /// <summary>Server-authoritative phase ID. No hook — clients receive correct perspective via BroadcastTurnPhase TargetRpc.</summary>
    [SyncVar]
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

                // Phase sync is handled by StartNewPhase → BroadcastTurnPhase TargetRpc.
                // currentPhaseId and RpcPhaseChange are NOT called here because at this
                // point the coroutine hasn't run yet and turnManager.currentPhase is still PhaseStart.

                Debug.Log($"[NetworkTurnSync] Game started, isMyTurnFirst={isMyTurnFirst}");
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
        // NetworkTurnSync is a scene object (NOT a player object), so isLocalPlayer is always false.
        // Use isServer instead: on the host/server, the SyncVar is the ground truth;
        // on a pure client, negate it to get the local player's perspective.
        // isMyTurnFirst (SyncVar) = isHostFirst = "does the host go first?"
        // Host's TurnManager: isMyTurnFirst = isHostFirst
        // Remote's TurnManager: isMyTurnFirst = !isHostFirst (remote goes first when host goes second)
        turnManager.isMyTurnFirst = isServer ? isMyTurnFirst : !isMyTurnFirst;
        Debug.Log($"[NetworkTurnSync] ApplyFirstPlayer: isMyTurnFirst={turnManager.isMyTurnFirst} (isServer={isServer}, syncVar={isMyTurnFirst})");
    }

    void OnHostFirstChanged(bool oldValue, bool newValue) { ApplyFirstPlayer(); }
    void OnIsMyTurnFirstChanged(bool oldValue, bool newValue) { ApplyFirstPlayer(); }

    void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        if (newValue && turnManager != null)
        {
            Debug.Log("[NetworkTurnSync] Game start signal received!");
            turnManager.enabled = true;
            // Host already started via InitialDraw. Phase arrives via BroadcastTurnPhase TargetRpc.

            // 服务端立即广播一次全量板面快照，确保新连入的客户端看到正确初始状态
            if (NetworkServer.active)
                BoardSyncManager.MarkDirty();
        }
    }

    /// <summary>
    /// 客户端重连时服务端强制广播全量状态：12 槽 + 附着物 + 血量 + 能量 + 手牌数。
    /// SyncVar 自动同步血量/能量/手牌数，板面通过 MarkDirty 触发。
    /// </summary>
    [Server]
    public static void ForceFullSync()
    {
        BoardSyncManager.MarkDirty();
        // SyncVars (currentHealth/currentEnergy/handCardCount) auto-replicate on next NetworkServer.SendToAll
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
