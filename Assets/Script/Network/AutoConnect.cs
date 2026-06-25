using UnityEngine;
using Mirror;

/// <summary>
/// Manages TurnManager lifecycle for networked games.
/// Connection is now handled by LobbyManager and Mirror scene management.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AutoConnect : MonoBehaviour
{
    public static bool IsClientMode { get; private set; }
    public static bool ClientConnected { get; private set; }

    private TurnManager _turnManager;

    void Awake()
    {
        if (NetworkServer.active)
        {
            // Host mode: TurnManager starts normally
            IsClientMode = false;
            Debug.Log("[AutoConnect] Host mode");
        }
        else if (NetworkClient.isConnected)
        {
            // Client mode (came from Lobby)
            IsClientMode = true;
            Debug.Log("[AutoConnect] Client mode (connected via Lobby)");
            NetworkClient.OnConnectedEvent += OnConnectedToServer;
            NetworkClient.OnDisconnectedEvent += OnDisconnected;
        }
        else
        {
            // Standalone / Editor test mode
            Debug.Log("[AutoConnect] Standalone mode - no networking");
        }
    }

    void Start()
    {
        _turnManager = FindObjectOfType<TurnManager>();

        if (IsClientMode && _turnManager != null)
        {
            _turnManager.enabled = false;
            Debug.Log("[AutoConnect] TurnManager disabled (client mode)");
        }
    }

    void OnDestroy()
    {
        NetworkClient.OnConnectedEvent -= OnConnectedToServer;
        NetworkClient.OnDisconnectedEvent -= OnDisconnected;
    }

    void OnConnectedToServer()
    {
        ClientConnected = true;
        Debug.Log("[AutoConnect] Client connected");
        if (_turnManager != null)
            _turnManager.enabled = true;
    }

    void OnDisconnected()
    {
        ClientConnected = false;
        Debug.LogError("[AutoConnect] Client disconnected");
    }
}
