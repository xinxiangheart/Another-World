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

        // 12 slots: "tid|data"
        string[] s = new string[12];
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            string card = Tid(slot?.currentCard3D);
            string flags = slot == null ? "" :
                $"{(slot.isBlocked?1:0)}{(slot.prisonBlocked?1:0)}{(slot.hasPlague?1:0)}" +
                $"{(slot.hasSpotlight?1:0)}|{slot.plagueRoundCount}|{slot.spotlightTierBoost}|{slot.slotTempAttackBoost}";
            s[i] = $"{card}|{flags}";
        }

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
            { NetworkPlayer.Local?.RpcSyncBoard(kv.Value, s, ab); return; }
    }

    static string Tid(GameObject o)
    {
        if (o == null) return "";
        var ci = o.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci == null) return "";
        return $"{ci.templateID}|{ci.currentHealth}|{ci.currentAttack}|{ci.currentMaxHealth}|{ci.currentCost}|{ci.currentTier}|{(ci.hasShield?1:0)}|{(ci.silencedThisPhase?1:0)}|{(ci.isAttached?1:0)}|{(ci.poisoned?1:0)}|{ci.prefixes??""}";
    }

    // ============= Client =============

    public void ApplySync(string[] s, string attachBlock)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        HandManager hm = FindObjectOfType<HandManager>();
        if (bm == null || s == null || s.Length < 12) return;

        for (int i = 0; i < 6; i++)
        {
            ApplySlot(i + 6, s[i], bm, hm);     // server 0-5 → client 6-11
            ApplySlot(i, s[i + 6], bm, hm);     // server 6-11 → client 0-5
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

    void ApplySlot(int idx, string raw, BoardManager bm, HandManager hm)
    {
        BoardSlot slot = bm.GetSlot(idx);
        if (slot == null) return;

        // Parse: "cardPart|flagsPart" where cardPart = everything before last "|slotTempAttackBoost|...?"
        // Actually: "templateID|hp|atk|maxHp|cost|tier|shield|silenced|attached|poisoned|prefixes|isBlocked.isPrison.isPlague.isSpotlight|slotTempAttackBoost"
        // Split carefully: the card part has 11 fields, then flags have 3 fields
        string[] parts = raw.Split('|');
        if (parts.Length == 0) { EnsureEmpty(idx, slot, bm); return; }

        // Card part: first 11 tokens (or fewer if empty)
        string tid = parts[0];
        if (string.IsNullOrEmpty(tid)) { EnsureEmpty(idx, slot, bm); return; }

        EnsureCard(idx, parts, slot, bm, hm);

        // Slot flags: last 4 tokens = "BBBB" | plagueRoundCount | spotlightTierBoost | slotTempAttackBoost
        if (parts.Length >= 14)
        {
            string f = parts[parts.Length - 4];
            if (f.Length >= 4)
            {
                slot.isBlocked = f[0] == '1';
                slot.prisonBlocked = f[1] == '1';
                slot.hasPlague = f[2] == '1';
                slot.hasSpotlight = f[3] == '1';
            }
            if (int.TryParse(parts[parts.Length - 3], out int prc)) slot.plagueRoundCount = prc;
            if (int.TryParse(parts[parts.Length - 2], out int stb)) slot.spotlightTierBoost = stb;
            if (int.TryParse(parts[parts.Length - 1], out int boost)) slot.slotTempAttackBoost = boost;
        }
    }

    void EnsureEmpty(int idx, BoardSlot slot, BoardManager bm)
    {
        if (slot.currentCard3D != null) { SafeDestroy(slot.currentCard3D); slot.SetCard(null); }
        slot.isBlocked = false; slot.prisonBlocked = false; slot.hasPlague = false; slot.hasSpotlight = false;
        slot.plagueRoundCount = 0; slot.spotlightTierBoost = 0; slot.slotTempAttackBoost = 0;
    }

    void EnsureCard(int idx, string[] parts, BoardSlot slot, BoardManager bm, HandManager hm)
    {
        string tid = parts[0];
        var cur = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (cur != null && cur.templateID != tid) { SafeDestroy(slot.currentCard3D); slot.SetCard(null); cur = null; }
        if (cur == null && hm != null)
        {
            var t = CardDatabase.Instance?.GetTemplate(tid);
            if (t?.prefab3D != null)
            {
                var m = Instantiate(t.prefab3D, hm.GetSlotWorldPosition(idx), Quaternion.Euler(0, 180, 0));
                var c = m.GetComponent<Card3DInstance>();
                if (c != null) { var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0); c.cardInstance = n; c.UpdateValues(); }
                slot.SetCard(m);
                cur = c?.cardInstance;
            }
        }
        if (cur != null && cur.templateID == tid && parts.Length >= 11)
        {
            var p = parts; int v;
            if (int.TryParse(p[1], out v)) cur.currentHealth = v;
            if (int.TryParse(p[2], out v)) cur.currentAttack = v;
            if (int.TryParse(p[3], out v)) cur.currentMaxHealth = v;
            if (int.TryParse(p[4], out v)) cur.currentCost = v;
            if (int.TryParse(p[5], out v)) cur.currentTier = v;
            cur.hasShield = (p[6] == "1");
            cur.silencedThisPhase = (p[7] == "1");
            cur.isAttached = (p[8] == "1");
            cur.poisoned = (p[9] == "1");
            cur.prefixes = p[10];
            slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
        }
    }

    static void SafeDestroy(GameObject o) { var ni = o.GetComponent<NetworkIdentity>(); if (ni != null) Object.Destroy(ni); Object.Destroy(o); }
}
