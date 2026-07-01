using Mirror;
using UnityEngine;

/// <summary>
/// Syncs server's 12-slot board state to the non-host client.
/// Server: 0-5=remote/host's-enemy, 6-11=host/host's-own
/// Client: 0-5=client's-enemy(sync from server's 6-11), 6-11=client's-own(sync from server's 0-5)
/// Full stats synced after battle and at phase start.
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
    /// Server: pack only host-side cards (6-11) for the client's enemy view (0-5).
    /// Fast sync used after individual card plays.
    /// </summary>
    public void SyncHostBoard()
    {
        if (!NetworkServer.active) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        string[] hostData = PackSix(6);

        foreach (var kv in NetworkServer.connections)
        {
            if (kv.Value != NetworkPlayer.Local?.connectionToClient)
            {
                NetworkPlayer.Local?.TargetSyncHostBoard(kv.Value, hostData);
                return;
            }
        }
    }

    /// <summary>
    /// Server: pack ALL 12 slots and send to the other client.
    /// Full sync used after battle and at phase start.
    /// </summary>
    public void SyncFullBoard()
    {
        if (!NetworkServer.active) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        string[] hostSide = PackSix(6);
        string[] enemySide = PackSix(0);

        foreach (var kv in NetworkServer.connections)
        {
            if (kv.Value != NetworkPlayer.Local?.connectionToClient)
            {
                NetworkPlayer.Local?.TargetSyncFullBoard(kv.Value, hostSide, enemySide);
                return;
            }
        }
    }

    string[] PackSix(int startSlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        string[] d = new string[6];
        for (int i = 0; i < 6; i++)
        {
            BoardSlot slot = bm?.GetSlot(startSlot + i);
            d[i] = Pack(slot?.currentCard3D);
        }
        return d;
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
            ci.prefixes ?? "");
    }

    void Apply(CardInstance ci, string raw)
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

    void SyncSlots(string[] data, int startSlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        HandManager hm = FindObjectOfType<HandManager>();

        for (int i = 0; i < 6; i++)
        {
            int slotIdx = startSlot + i;
            BoardSlot slot = bm?.GetSlot(slotIdx);
            if (slot == null) continue;

            string raw = (i < data.Length) ? data[i] : "";
            string wantTID = "";
            if (!string.IsNullOrEmpty(raw)) wantTID = raw.Split('|')[0];

            CardInstance existing = null;
            if (slot.currentCard3D != null)
                existing = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;

            if (string.IsNullOrEmpty(wantTID))
            {
                if (existing != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }
                continue;
            }

            if (existing == null || existing.templateID != wantTID)
            {
                if (existing != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }
                CardData tpl = CardDatabase.Instance?.GetTemplate(wantTID);
                if (tpl?.prefab3D != null && hm != null)
                {
                    GameObject m = Instantiate(tpl.prefab3D, hm.GetSlotWorldPosition(slotIdx), Quaternion.Euler(0, 180, 0));
                    var c3d = m.GetComponent<Card3DInstance>();
                    if (c3d != null) { var ci = m.AddComponent<CardInstance>(); ci.InitFromTemplate(tpl, 0); c3d.cardInstance = ci; }
                    slot.SetCard(m);
                    existing = c3d?.cardInstance;
                }
            }

            if (existing != null && existing.templateID == wantTID)
            {
                Apply(existing, raw);
                var u = slot.currentCard3D?.GetComponent<Card3DInstance>();
                if (u != null) u.UpdateValues();
            }
        }
    }

    /// <summary>
    /// Client: host-side sync. Server 6-11 → client enemy 0-5.
    /// </summary>
    public void ApplyHostBoard(string[] hostData)
    {
        SyncSlots(hostData, 0);
    }

    /// <summary>
    /// Client: full sync with perspective remapping.
    /// server 0-5 (remote cards) → client 6-11 (my cards)
    /// server 6-11 (host cards) → client 0-5 (enemy cards)
    /// </summary>
    public void ApplyFullBoard(string[] serverEnemy, string[] serverHost)
    {
        // server 0-5 → client 6-11 (my own cards)
        SyncSlots(serverEnemy, 6);
        // server 6-11 → client 0-5 (enemy cards)
        SyncSlots(serverHost, 0);
    }
}
