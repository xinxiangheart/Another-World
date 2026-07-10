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

        // ---- 服务器端去重 ----
        // 同一半场（0-5 或 6-11）不应有两个不同槽位持有相同 templateID 的非空牌。
        // 若发生，说明某张牌被错误写入两个槽——清理幻影，防止主机和客户端都看到重复模型。
        for (int half = 0; half <= 6; half += 6)
        {
            var seen = new System.Collections.Generic.Dictionary<string, int>(); // tid → 槽号
            for (int i = half; i < half + 6; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                var ci = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                string tid = ci?.templateID;
                if (string.IsNullOrEmpty(tid)) continue;
                if (seen.TryGetValue(tid, out int firstSlot))
                {
                    var dupSlot = bm.GetSlot(i);
                    if (dupSlot?.currentCard3D != null) { SafeDestroy(dupSlot.currentCard3D); dupSlot.SetCard(null); }
                }
                else
                {
                    seen[tid] = i;
                }
            }
        }

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
        // 收集同半场 slot 已有的 templateID（slot 去重后再收集，确保 slot 侧已是权威）
        var slotTids = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 12; i++)
        {
            var ci2 = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci2 != null && !string.IsNullOrEmpty(ci2.templateID)) slotTids.Add(ci2.templateID);
        }
        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
        {
            var o = bm.attachedModels[i];
            var ci = o?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null) { bm.attachedModels.RemoveAt(i); continue; }
            // attachedModels 里的模型如果 templateID 已存在于 slot（=独立放置过的牌），
            // 则为同步竞态产生的重复附着幻影，清理掉。
            if (slotTids.Contains(ci.templateID))
            {
                SafeDestroy(o);
                bm.attachedModels.RemoveAt(i);
                continue;
            }
            al.Add($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}");
        }
        string ab = al.Count > 0 ? string.Join("||", al) : "";

        // Signal whether the server-side has an active MistHider so the client hides the correct side
        bool mistHiderActive = IsMistHiderActive();
        string header = mistHiderActive ? "1|" : "0|";

        foreach (var kv in NetworkServer.connections)
            if (kv.Value != NetworkPlayer.Local?.connectionToClient)
            { NetworkPlayer.Local?.RpcSyncBoard(kv.Value, s, header + ab); return; }
    }

    static bool IsMistHiderActive()
    {
        var all = GlobalEventManager.Instance?.GetAllAuras();
        if (all == null) return false;
        foreach (var a in all)
            if (a is MistHiderAura && a.IsActive()) return true;
        return false;
    }

    static string Tid(GameObject o)
    {
        if (o == null) return "";
        var ci = o.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci == null) return "";
        return $"{ci.templateID}|{ci.currentHealth}|{ci.currentAttack}|{ci.currentMaxHealth}|{ci.currentCost}|{ci.currentTier}|{(ci.hasShield?1:0)}|{(ci.silencedThisPhase?1:0)}|{(ci.isAttached?1:0)}|{(ci.poisoned?1:0)}|{ci.prefixes??""}";
    }

    // ============= Client =============

    public void ApplySync(string[] s, string attachBlockExt)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        HandManager hm = FindObjectOfType<HandManager>();
        if (bm == null || s == null || s.Length < 12) return;

        // Parse header: "1|rest" → mistHiderActive, "0|rest" → not active
        bool mistHiderActive = false;
        string attachBlock = attachBlockExt;
        if (!string.IsNullOrEmpty(attachBlockExt))
        {
            int sepIdx = attachBlockExt.IndexOf('|');
            if (sepIdx >= 0)
            {
                mistHiderActive = attachBlockExt[0] == '1';
                attachBlock = attachBlockExt.Substring(sepIdx + 1);
            }
        }

        for (int i = 0; i < 6; i++)
        {
            ApplySlot(i + 6, s[i], bm, hm);     // server 0-5 → client 6-11
            ApplySlot(i, s[i + 6], bm, hm);     // server 6-11 → client 0-5
        }

        // Server's 6-11 maps to this client's 0-5. If server has MistHider, enemy cards are hidden.
        // Apply in both directions: hide when active, unhide when aura expires.
        Card3DHover.EnemyCardsAreHidden = mistHiderActive;
        for (int i = 0; i <= 5; i++)
        {
            GameObject card = bm.GetSlot(i)?.currentCard3D;
            if (card != null) Card3DHover.SetHidden(card, mistHiderActive, false);
        }

        // attachments
        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
        { if (bm.attachedModels[i] != null) SafeDestroy(bm.attachedModels[i]); bm.attachedModels.RemoveAt(i); }

        if (string.IsNullOrEmpty(attachBlock)) return;
        // 客户端附着物去重：收集当前所有 slot 的 templateID，防附着块重建同模板幻影
        var clientSlotTids = new System.Collections.Generic.HashSet<string>();
        for (int si = 0; si < 12; si++)
        {
            var sci = bm.GetSlot(si)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (sci != null && !string.IsNullOrEmpty(sci.templateID)) clientSlotTids.Add(sci.templateID);
        }
        foreach (var item in attachBlock.Split(new[] { "||" }, System.StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(item)) continue;
            var p = item.Split('|');
            int hs = 0, o = 0;
            if (p.Length > 1 && int.TryParse(p[1], out int h)) hs = h;
            if (p.Length > 2 && int.TryParse(p[2], out int od)) o = od;
            // 去重：同模板已存在于 slot 则跳过
            if (clientSlotTids.Contains(p[0])) continue;
            var t = CardDatabase.Instance?.GetTemplate(p[0]);
            if (t?.prefab3D == null || hm == null) continue;
            int cs = hs >= 6 ? hs - 6 : hs + 6;  // mirror 6-11↔0-5
            var m = Instantiate(t.prefab3D, hm.GetSlotWorldPosition(cs)
                + new Vector3(-0.5f - o * 0.5f, 0, 0.1f + o * 0.1f), Quaternion.Euler(0, 180, 0));
            var c = m.GetComponent<Card3DInstance>();
            if (c != null)
            {
                var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0);
                n.isAttached = true; n.hostSlotID = cs; n.attachOrder = o;
                c.cardInstance = n; c.UpdateValues();
            }
            // Attachments: always text-hidden. Use same pattern as PlaceAttachedCard.
            // If MistHider active, also flip + disable hover.
            Card3DHover.SetHidden(m, mistHiderActive, true);
            // Duplicate the manual SetActive(false) calls — SetHidden's HideAllInfo
            // silently no-ops if CardDisplay3D text fields are null on this prefab.
            CardDisplay3D d2 = m.GetComponent<CardDisplay3D>();
            if (d2 != null)
            {
                if (d2.nameText != null) d2.nameText.gameObject.SetActive(false);
                if (d2.attackText != null) d2.attackText.gameObject.SetActive(false);
                if (d2.healthText != null) d2.healthText.gameObject.SetActive(false);
                if (d2.costText != null) d2.costText.gameObject.SetActive(false);
                if (d2.prefixText != null) d2.prefixText.gameObject.SetActive(false);
                if (d2.effectText != null) d2.effectText.gameObject.SetActive(false);
            }
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
        slot.SyncVisual();
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
            // 去重：同步竞态可能把同一张牌的数据写进多个槽位。
            // 若同侧(6-11)已有同 templateID 的牌（=刚才正常放置的，如 PlaceIndependentCard），
            // 且本方当前不在该目标槽，则跳过创建，防止"一个放置变两个模型"。
            int otherSlot = -1;
            for (int check = 6; check <= 11; check++)
            {
                if (check == idx) continue;
                var checkCard = bm.GetSlot(check)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (checkCard != null && checkCard.templateID == tid)
                    { otherSlot = check; break; }
            }
            if (otherSlot >= 0) return;

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
