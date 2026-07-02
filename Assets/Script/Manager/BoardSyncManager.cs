using Mirror;
using UnityEngine;

/// <summary>
/// Keeps enemy 3D models visually in sync. NEVER touches CardInstance stats.
/// Stats are computed locally by each client's own BattleManager.
/// </summary>
public class BoardSyncManager : MonoBehaviour
{
    public static BoardSyncManager Instance { get; private set; }
    bool _dirty;

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }
    public static void MarkDirty() { if (Instance != null) Instance._dirty = true; }

    void LateUpdate() { if (_dirty && NetworkServer.active) { _dirty = false; SyncNow(); } }

    void SyncNow()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        // 12 slots: templateID or empty
        string[] tids = new string[12];
        for (int i = 0; i < 12; i++)
            tids[i] = Tid(bm.GetSlot(i)?.currentCard3D);

        // attachments
        bm.attachedModels.RemoveAll(a => a == null);
        var al = new System.Collections.Generic.List<string>();
        foreach (var o in bm.attachedModels)
        {
            var ci = o.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null) al.Add($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}");
        }
        string ab = al.Count > 0 ? string.Join("||", al) : "";

        foreach (var kv in NetworkServer.connections)
            if (kv.Value != NetworkPlayer.Local?.connectionToClient)
            { NetworkPlayer.Local?.RpcSyncBoard(kv.Value, tids, ab); return; }
    }

    static string Tid(GameObject o)
    {
        if (o == null) return "";
        return o.GetComponent<Card3DInstance>()?.cardInstance?.templateID ?? "";
    }

    // ============= Client =============

    public void ApplySync(string[] tids, string attachBlock)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        HandManager hm = FindObjectOfType<HandManager>();
        if (bm == null || tids == null || tids.Length < 12) return;

        for (int i = 0; i < 6; i++)
        {
            EnsureSlot(i + 6, tids[i], bm, hm);     // server 0-5 → client 6-11
            EnsureSlot(i, tids[i + 6], bm, hm);     // server 6-11 → client 0-5
        }

        // attachments
        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
        { if (bm.attachedModels[i] != null) SafeDestroy(bm.attachedModels[i]); bm.attachedModels.RemoveAt(i); }

        if (string.IsNullOrEmpty(attachBlock)) return;
        foreach (var item in attachBlock.Split(new[] { "||" }, System.StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(item)) continue;
            var p = item.Split('|');
            int hs = 0, o = 0;
            if (p.Length > 1 && int.TryParse(p[1], out int h)) hs = h;
            if (p.Length > 2 && int.TryParse(p[2], out int od)) o = od;
            var t = CardDatabase.Instance?.GetTemplate(p[0]);
            if (t?.prefab3D == null || hm == null) continue;
            int cs = (hs >= 6 && hs <= 11) ? hs - 6 : hs;
            var m = Instantiate(t.prefab3D, hm.GetSlotWorldPosition(cs)
                + new Vector3(-0.5f - o * 0.5f, 0, 0.1f + o * 0.1f), Quaternion.Euler(0, 180, 0));
            var c = m.GetComponent<Card3DInstance>();
            if (c != null)
            {
                var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0);
                n.isAttached = true; n.hostSlotID = cs; n.attachOrder = o;
                c.cardInstance = n; c.UpdateValues();
            }
            var d = m.GetComponent<CardDisplay3D>();
            if (d != null) { if (d.nameText) d.nameText.gameObject.SetActive(false); if (d.prefixText) d.prefixText.gameObject.SetActive(false); if (d.attackText) d.attackText.gameObject.SetActive(false); if (d.healthText) d.healthText.gameObject.SetActive(false); if (d.costText) d.costText.gameObject.SetActive(false); }
            bm.attachedModels.Add(m);
        }
    }

    void EnsureSlot(int idx, string wantTid, BoardManager bm, HandManager hm)
    {
        BoardSlot slot = bm.GetSlot(idx);
        if (slot == null) return;
        string curTid = Tid(slot.currentCard3D);
        if (curTid == wantTid) return;
        if (slot.currentCard3D != null) { SafeDestroy(slot.currentCard3D); slot.SetCard(null); }
        if (!string.IsNullOrEmpty(wantTid) && hm != null)
        {
            var t = CardDatabase.Instance?.GetTemplate(wantTid);
            if (t?.prefab3D != null)
            {
                var m = Instantiate(t.prefab3D, hm.GetSlotWorldPosition(idx), Quaternion.Euler(0, 180, 0));
                var c = m.GetComponent<Card3DInstance>();
                if (c != null) { var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0); c.cardInstance = n; c.UpdateValues(); }
                slot.SetCard(m);
            }
        }
    }

    static void SafeDestroy(GameObject o) { var ni = o.GetComponent<NetworkIdentity>(); if (ni != null) Object.Destroy(ni); Object.Destroy(o); }
}
