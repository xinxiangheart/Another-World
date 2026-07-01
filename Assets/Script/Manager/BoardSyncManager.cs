using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 集中管理所有3D模型同步。服务器每帧末或每次板面变化后，
/// 把自己的12槽位完整状态广播给纯客户端。
/// 客户端收到后用视角映射重建板面。
///
/// 调用方式（服务器端任意位置）:
///   BoardSyncManager.MarkDirty();
///
/// 原理:
///   - 服务器是所有游戏逻辑的权威来源（战斗、特性等）
///   - MarkDirty() 标记本帧需要同步
///   - LateUpdate 中自动执行一次 SyncNow()，避免同帧重复发送
/// </summary>
public class BoardSyncManager : MonoBehaviour
{
    public static BoardSyncManager Instance { get; private set; }

    bool _dirty;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>标记需要同步。同一帧内多次调用只触发一次网络发送。</summary>
    public static void MarkDirty()
    {
        if (Instance != null) Instance._dirty = true;
    }

    void LateUpdate()
    {
        if (_dirty && NetworkServer.active)
        {
            _dirty = false;
            SyncNow();
        }
    }

    // ============================================================
    // 服务器 → 客户端
    // ============================================================

    void SyncNow()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        // 打包全部12个槽位
        string[] all = new string[12];
        for (int i = 0; i < 12; i++)
            all[i] = Pack(bm.GetSlot(i)?.currentCard3D);

        // 发给非主机客户端
        foreach (var kv in NetworkServer.connections)
        {
            if (kv.Value != NetworkPlayer.Local?.connectionToClient)
            {
                NetworkPlayer.Local?.RpcSyncBoard(kv.Value, all);
                return;
            }
        }
    }

    static string Pack(GameObject obj)
    {
        if (obj == null) return "";
        var ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci == null) return "";
        return string.Join("|",
            ci.templateID ?? "",
            ci.currentHealth, ci.currentAttack, ci.currentMaxHealth,
            ci.currentCost, ci.currentTier,
            ci.hasShield ? "1" : "0",
            ci.silencedThisPhase ? "1" : "0",
            ci.isAttached ? "1" : "0",
            ci.poisoned ? "1" : "0",
            ci.prefixes ?? "",
            // 附着信息
            ci.hostSlotID,
            // 位置标记
            ci.templateID == "01345" ? "1" : "0"  // 传导者
        );
    }

    // ============================================================
    // 客户端应用
    // ============================================================

    /// <summary>
    /// Client receives full 12-slot server board.
    /// Map: server[0..5]→client enemy[0..5], server[6..11]→client enemy[0..5] reversed.
    /// Actually: map to preserve the tactical relationship — tank in server's 6→client's 0.
    /// </summary>
    public void ApplySync(string[] serverData)
    {
        if (serverData == null || serverData.Length < 12) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        HandManager hm = FindObjectOfType<HandManager>();
        if (bm == null) return;

        // Only sync enemy side (0-5). Client's own (6-11) is managed locally.
        // Server 6-11 (host's cards) → client 0-5 (enemy view)
        for (int i = 0; i < 6; i++)
            ApplySlot(i, serverData[i + 6], bm, hm);

        // Do NOT touch client's own slots 6-11 — those are managed locally by the client's own code.
    }

    void ApplySlot(int slotIdx, string raw, BoardManager bm, HandManager hm)
    {
        BoardSlot slot = bm.GetSlot(slotIdx);
        if (slot == null) return;

        string tid = "";
        if (!string.IsNullOrEmpty(raw))
            tid = raw.Split('|')[0];

        CardInstance cur = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;

        // 该空 → 清空
        if (string.IsNullOrEmpty(tid))
        {
            if (cur != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }
            return;
        }

        // 模板不匹配 → 替换
        if (cur != null && cur.templateID != tid)
        {
            Destroy(slot.currentCard3D);
            slot.SetCard(null);
            cur = null;
        }

        // 缺失 → 生成
        if (cur == null && hm != null)
        {
            CardData tpl = CardDatabase.Instance?.GetTemplate(tid);
            if (tpl?.prefab3D != null)
            {
                var m = Instantiate(tpl.prefab3D, hm.GetSlotWorldPosition(slotIdx), Quaternion.Euler(0, 180, 0));
                var c3d = m.GetComponent<Card3DInstance>();
                if (c3d != null)
                {
                    var nc = m.AddComponent<CardInstance>();
                    nc.InitFromTemplate(tpl, 0);
                    c3d.cardInstance = nc;
                }
                slot.SetCard(m);
                cur = c3d?.cardInstance;
            }
        }

        // 应用数值
        if (cur != null && cur.templateID == tid)
            UnpackTo(cur, raw);

        slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
    }

    void UnpackTo(CardInstance ci, string raw)
    {
        string[] p = raw.Split('|');
        int v;
        if (p.Length > 1 && int.TryParse(p[1], out v)) ci.currentHealth = v;
        if (p.Length > 2 && int.TryParse(p[2], out v)) ci.currentAttack = v;
        if (p.Length > 3 && int.TryParse(p[3], out v)) ci.currentMaxHealth = v;
        if (p.Length > 4 && int.TryParse(p[4], out v)) ci.currentCost = v;
        if (p.Length > 5 && int.TryParse(p[5], out v)) ci.currentTier = v;
        if (p.Length > 6) ci.hasShield = (p[6] == "1");
        if (p.Length > 7) ci.silencedThisPhase = (p[7] == "1");
        if (p.Length > 8) ci.isAttached = (p[8] == "1");
        if (p.Length > 9) ci.poisoned = (p[9] == "1");
        if (p.Length > 10) ci.prefixes = p[10];
        if (p.Length > 11 && int.TryParse(p[11], out v)) ci.hostSlotID = v;
    }
}
