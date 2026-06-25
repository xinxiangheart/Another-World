using UnityEngine;
using Mirror;

public class NetworkDebug : MonoBehaviour
{
    void Awake()
    {
        // 延迟绑定 transport 事件，因为 Transport.active 可能在 Awake 时尚未设置
        if (Transport.active != null)
        {
            Transport.active.OnClientConnected += OnTransportClientConnected;
            Transport.active.OnClientDisconnected += OnTransportClientDisconnected;
        }
        else
        {
            Debug.LogWarning("[NetworkDebug] Awake 时 Transport.active 为 null，将在 Start 中重试绑定");
        }
    }

    void Start()
    {
        // 如果 Awake 时 Transport.active 为 null，在这里重试
        if (Transport.active != null)
        {
            Transport.active.OnClientConnected -= OnTransportClientConnected; // 防止重复绑定
            Transport.active.OnClientDisconnected -= OnTransportClientDisconnected;
            Transport.active.OnClientConnected += OnTransportClientConnected;
            Transport.active.OnClientDisconnected += OnTransportClientDisconnected;
        }
        else
        {
            Debug.LogError("[NetworkDebug] Transport.active 仍为 null，无法绑定传输层事件");
        }
    }

    void OnTransportClientConnected()
    {
        Debug.Log("[Transport] 客户端传输层连接成功");
    }

    void OnTransportClientDisconnected()
    {
        Debug.Log("[Transport] 客户端传输层断开");
    }

    void OnEnable()
    {
        NetworkServer.OnConnectedEvent += OnServerConnected;
        NetworkServer.OnDisconnectedEvent += OnServerDisconnected;
        NetworkClient.OnConnectedEvent += OnClientConnected;
        NetworkClient.OnDisconnectedEvent += OnClientDisconnected;
    }

    void OnDisable()
    {
        NetworkServer.OnConnectedEvent -= OnServerConnected;
        NetworkServer.OnDisconnectedEvent -= OnServerDisconnected;
        NetworkClient.OnConnectedEvent -= OnClientConnected;
        NetworkClient.OnDisconnectedEvent -= OnClientDisconnected;

        if (Transport.active != null)
        {
            Transport.active.OnClientConnected -= OnTransportClientConnected;
            Transport.active.OnClientDisconnected -= OnTransportClientDisconnected;
        }
    }

    void OnServerConnected(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Server] 客户端连接成功: connectionId={conn.connectionId}");
    }

    void OnServerDisconnected(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Server] 客户端断开: connectionId={conn.connectionId}");
    }

    void OnClientConnected()
    {
        Debug.Log($"[Client] 连接到服务器成功");
    }

    void OnClientDisconnected()
    {
        Debug.Log($"[Client] 与服务器断开连接");
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.Label($"NetworkServer.active = {NetworkServer.active}");
        GUILayout.Label($"NetworkServer.connections.Count = {NetworkServer.connections.Count}");
        GUILayout.Label($"NetworkClient.active = {NetworkClient.active}");
        GUILayout.Label($"NetworkClient.isConnected = {NetworkClient.isConnected}");
        GUILayout.Label($"NetworkServer.isLoadingScene = {NetworkServer.isLoadingScene}");
        GUILayout.Label($"NetworkClient.isLoadingScene = {NetworkClient.isLoadingScene}");
        GUILayout.EndArea();
    }
}
