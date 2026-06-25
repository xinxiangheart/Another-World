using kcp2k;
using Mirror;
using UnityEngine;

public class NetworkSelfCheck : MonoBehaviour
{
    void Start()
    {
        Debug.Log("===== 网络组件自检 =====");

        // 查找 NetworkManager
        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null)
        {
            Debug.LogError("[自检] NetworkManager 不存在！");
            return;
        }
        Debug.Log($"[自检] NetworkManager: {nm.name}");

        // 检查 Transport
        if (nm.transport == null)
        {
            Debug.LogError("[自检] Transport 未绑定！");
        }
        else
        {
            Debug.Log($"[自检] Transport: {nm.transport.GetType().Name}");

            // 检查端口
            if (nm.transport is PortTransport portTransport)
            {
                Debug.Log($"[自检] Port: {portTransport.Port}");
            }
        }

        // 检查 Network Address
        Debug.Log($"[自检] Network Address: {nm.networkAddress}");

        // 检查 Player Prefab
        if (nm.playerPrefab == null)
        {
            Debug.LogError("[自检] Player Prefab 未设置！");
        }
        else
        {
            Debug.Log($"[自检] Player Prefab: {nm.playerPrefab.name}");
            NetworkIdentity ni = nm.playerPrefab.GetComponent<NetworkIdentity>();
            if (ni == null)
                Debug.LogError("[自检] Player Prefab 上缺少 NetworkIdentity！");
            else
                Debug.Log("[自检] Player Prefab 上 NetworkIdentity 存在");
        }

        // 检查 KcpTransport
        KcpTransport kcp = FindObjectOfType<KcpTransport>();
        if (kcp != null)
        {
            Debug.Log($"[自检] KcpTransport Port: {kcp.Port}");
        }
        else
        {
            Debug.LogWarning("[自检] KcpTransport 未找到");
        }

        // 检查网络模式
        Debug.Log($"[自检] Application.internetReachability = {Application.internetReachability}");
        try
        {
            Debug.Log($"[自检] System.Net.Dns.GetHostName() = {System.Net.Dns.GetHostName()}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[自检] GetHostName() 失败 (系统编码问题，不影响联网): {e.Message}");
        }

        Debug.Log("===== 自检结束 =====");
    }
}
