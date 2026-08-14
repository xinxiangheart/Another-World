using Mirror;
using UnityEngine;

// ============================================================================
// OfflineAIHost — 离线单机模式：Host 就绪后创建 AI 的 NetworkPlayer 并赋 Remote
// ============================================================================
//
// 离线模式走 Host（AutoConnect.Start 调 StartHost），Mirror 自动创建人类 Local。
// 本组件在 Host 就绪后手动 NetworkServer.Spawn 第二个 NetworkPlayer（AI），赋给
// NetworkPlayer.Remote。AI 是 server-only 对象（connectionToClient == null），因此
// AI 的所有操作必须走 server 本地方法，禁止调用 [Command]/[TargetRpc]。
// ============================================================================

public class OfflineAIHost : MonoBehaviour
{
    private NetworkManager _nm;
    private bool _aiCreated;

    void Awake()
    {
        _nm = FindObjectOfType<NetworkManager>();
    }

    void Update()
    {
        if (_aiCreated) return;
        if (!NetworkServer.active) return;       // 只有 Host 才创建
        if (LobbyConfig.FromLobby) return;       // 在线模式不创建（真实对手会连接）
        if (NetworkPlayer.Local == null) return; // 等人类 Local 就绪
        if (NetworkPlayer.Remote != null) return;

        CreateAIPlayer();
    }

    void CreateAIPlayer()
    {
        if (_nm == null || _nm.playerPrefab == null)
        {
            Debug.LogError("[OfflineAIHost] NetworkManager 或 playerPrefab 缺失，无法创建 AI");
            return;
        }

        GameObject aiGo = Instantiate(_nm.playerPrefab);
        NetworkServer.Spawn(aiGo); // server-only spawn，connectionToClient == null

        NetworkPlayer ai = aiGo.GetComponent<NetworkPlayer>();
        NetworkPlayer.Remote = ai;
        _aiCreated = true;

        Debug.Log($"[OfflineAIHost] AI player 已创建: netId={ai?.netId}, conn={ai?.connectionToClient == null}, health={ai?.currentHealth}, energy={ai?.currentEnergy}");
    }
}
