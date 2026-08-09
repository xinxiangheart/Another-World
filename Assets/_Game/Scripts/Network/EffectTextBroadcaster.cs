using Mirror;
using UnityEngine;

/// <summary>让客户端也能看到特效调试文字。服务端 Dispatch 时广播。</summary>
public static class EffectTextBroadcaster
{
    public static void Show(string templateID, string cardName, string traitCN)
    {
        if (!NetworkServer.active) return;

        // 广播给远程客户端（主机端由本地 Dispatch 直接写 debugText）
        foreach (var conn in NetworkServer.connections)
        {
            if (conn.Value?.identity == null) continue;
            if (conn.Value.identity.isOwned) continue; // skip host's own identity
            var np = conn.Value.identity.GetComponent<NetworkPlayer>();
            if (np != null)
                np.TargetShowEffectText(conn.Value, templateID, cardName, traitCN);
        }
    }

    public static void Hide()
    {
        if (!NetworkServer.active) return;
        foreach (var conn in NetworkServer.connections)
        {
            if (conn.Value?.identity == null) continue;
            if (conn.Value.identity.isOwned) continue;
            var np = conn.Value.identity.GetComponent<NetworkPlayer>();
            if (np != null)
                np.TargetHideEffectText(conn.Value);
        }
    }
}
