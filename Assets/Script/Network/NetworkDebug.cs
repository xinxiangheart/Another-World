using UnityEngine;
using Mirror;

public class NetworkDebug : MonoBehaviour
{
    void OnEnable()
    {
        NetworkServer.OnConnectedEvent += (conn) => Debug.Log($"Server: {conn} connected");
        NetworkServer.OnDisconnectedEvent += (conn) => Debug.Log($"Server: {conn} disconnected");
        NetworkClient.OnConnectedEvent += () => Debug.Log("Client: connected to server");
        NetworkClient.OnDisconnectedEvent += () => Debug.Log("Client: disconnected");
    }
    void Start()
    {
        NetworkServer.OnConnectedEvent += (conn) =>
        {
            Debug.Log($"有客户端连接: {conn.connectionId}");
        };

        NetworkServer.OnDisconnectedEvent += (conn) =>
        {
            Debug.Log($"客户端断开: {conn.connectionId}");
        };
    }

    void OnGUI()
    {
        if (NetworkClient.active)
            GUI.Label(new Rect(10, 10, 300, 30), $"Client connected: {NetworkClient.isConnected}");
    }
}