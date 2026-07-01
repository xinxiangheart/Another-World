using Mirror;
using UnityEngine;

/// <summary>
/// Syncs host-side slots 6-11 (with full stats) to the remote client's enemy view (0-5).
/// Remote reports its own 6-11 back to server via CmdReportMyBoard.
/// Client's own 6-11 are NEVER touched by sync.
/// </summary>
public class BoardSyncManager : MonoBehaviour
{
    public static BoardSyncManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Server: pack slots 6-11 with full stats, send to remote client.
    /// </summary>
    public void SyncAll()
    {
        if (!NetworkServer.active) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        string[] data = new string[6];
        for (int i = 0; i < 6; i++)
            data[i] = Pack(bm.GetSlot(i + 6)?.currentCard3D);

        foreach (var kv in NetworkServer.connections)
        {
            if (kv.Value != NetworkPlayer.Local?.connectionToClient)
            {
                NetworkPlayer.Local?.TargetSyncHostBoard(kv.Value, data);
                return;
            }
        }
    }

    public void SyncHostBoard() => SyncAll();

    static string Pack(GameObject obj)
    {
        if (obj == null) return "";
        var ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci == null) return "";
        return string.Join("|",
            ci.templateID ?? "",
            ci.currentHealth, ci.currentAttack, ci.currentMaxHealth,
            ci.currentCost, ci.currentTier,
            ci.hasShield ? "1" : "0", ci.silencedThisPhase ? "1" : "0",
            ci.isAttached ? "1" : "0", ci.poisoned ? "1" : "0",
            ci.prefixes ?? "");
    }

    void UnpackTo(CardInstance ci, string raw)
    {
        if (ci == null) return;
        string[] p = raw.Split('|');
        int hp, atk, mh, cost, tier;
        if (p.Length > 1 && int.TryParse(p[1], out hp)) ci.currentHealth = hp;
        if (p.Length > 2 && int.TryParse(p[2], out atk)) ci.currentAttack = atk;
        if (p.Length > 3 && int.TryParse(p[3], out mh)) ci.currentMaxHealth = mh;
        if (p.Length > 4 && int.TryParse(p[4], out cost)) ci.currentCost = cost;
        if (p.Length > 5 && int.TryParse(p[5], out tier)) ci.currentTier = tier;
        if (p.Length > 6) ci.hasShield = (p[6] == "1");
        if (p.Length > 7) ci.silencedThisPhase = (p[7] == "1");
        if (p.Length > 8) ci.isAttached = (p[8] == "1");
        if (p.Length > 9) ci.poisoned = (p[9] == "1");
        if (p.Length > 10) ci.prefixes = p[10];
    }

    /// <summary>
    /// Client: server 6-11 → client enemy slots 0-5.
    /// Only touches 0-5. Client's own 6-11 are NEVER modified.
    /// </summary>
    /// <summary>
    /// Client: server 6-11 → client enemy slots 0-5.
    /// Handles model create/destroy + stats.
    /// </summary>
    public void ApplyHostBoard(string[] data)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        HandManager hm = FindObjectOfType<HandManager>();
        if (bm == null) return;

        for (int i = 0; i < 6; i++)
        {
            int sid = i;
            BoardSlot slot = bm.GetSlot(sid);
            if (slot == null) continue;

            string raw = (i < data.Length) ? data[i] : "";
            string tid = string.IsNullOrEmpty(raw) ? "" : raw.Split('|')[0];

            CardInstance cur = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;

            if (string.IsNullOrEmpty(tid))
            {
                if (cur != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }
                continue;
            }

            if (cur != null && cur.templateID != tid)
            {
                Destroy(slot.currentCard3D);
                slot.SetCard(null);
                cur = null;
            }

            if (cur == null)
            {
                CardData tpl = CardDatabase.Instance?.GetTemplate(tid);
                if (tpl?.prefab3D != null && hm != null)
                {
                    var m = Instantiate(tpl.prefab3D, hm.GetSlotWorldPosition(sid), Quaternion.Euler(0, 180, 0));
                    var c3d = m.GetComponent<Card3DInstance>();
                    if (c3d != null) { var ci = m.AddComponent<CardInstance>(); ci.InitFromTemplate(tpl, 0); c3d.cardInstance = ci; }
                    slot.SetCard(m);
                    cur = c3d?.cardInstance;
                }
            }

            if (cur != null && cur.templateID == tid)
            {
                UnpackTo(cur, raw);
                slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }
    }

    /// <summary>
    /// Client: full bidir sync after battle.
    /// server upper (6-11=host cards) → client enemy 0-5 (model+stats)
    /// server lower (0-5=remote cards) → client own 6-11 (STATS ONLY, no model create/destroy)
    /// </summary>
    public void ApplyBattleSync(string[] hostData, string[] remoteData)
    {
        ApplyHostBoard(hostData); // host cards → client enemy

        // Stats-only update for client's own cards from server's remote-card data
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 6; i++)
        {
            int ownSlot = i + 6;
            BoardSlot slot = bm.GetSlot(ownSlot);
            string raw = (i < remoteData.Length) ? remoteData[i] : "";
            string tid = string.IsNullOrEmpty(raw) ? "" : raw.Split('|')[0];
            var cur = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (cur != null && cur.templateID == tid)
            {
                UnpackTo(cur, raw);
                slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }
    }

    // No-ops for old callers
    public void RpcApply(string[] _, string[] __) { }
}
