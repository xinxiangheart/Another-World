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

        // ---- 服务器端去重（按 instanceID + 放置时间戳） ----
        // 同一半场同一 instanceID 出现在两个槽位 = 竞态幻影。
        // 销毁放置时间较旧的那个，保留最新的。
        for (int half = 0; half <= 6; half += 6)
        {
            var seen = new System.Collections.Generic.Dictionary<string, (int slotID, float placedAt)>(); // instanceID → (槽号, 放置时间)
            for (int i = half; i < half + 6; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                var ci = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                string iid = ci?.instanceID;
                if (string.IsNullOrEmpty(iid)) continue;

                if (seen.TryGetValue(iid, out var prev))
                {
                    // 两个槽位有同一 instanceID → 保留放置时间较新的
                    float curTime = ci._placedAtTime;
                    if (curTime > prev.placedAt)
                    {
                        // 当前槽位的卡更新 → 销毁旧槽位的
                        var oldSlot = bm.GetSlot(prev.slotID);
                        if (oldSlot?.currentCard3D != null) { SafeDestroy(oldSlot.currentCard3D); oldSlot.SetCard(null); }
                        seen[iid] = (i, curTime);
                    }
                    else
                    {
                        // 当前槽位的卡更旧 → 销毁当前
                        SafeDestroy(slot.currentCard3D); slot.SetCard(null);
                    }
                }
                else
                {
                    seen[iid] = (i, ci._placedAtTime);
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
                $"{(slot.hasSpotlight?1:0)}{(slot.deepSeaMarked?1:0)}{(slot.deepSeaHealthDebuff?1:0)}|{slot.plagueRoundCount}|{slot.spotlightTierBoost}|{slot.slotTempAttackBoost}~{slot.deepSeaAttackDebuff}";
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
        string gtt = ci.grantedTraitTexts != null && ci.grantedTraitTexts.Count > 0
            ? string.Join(";;", ci.grantedTraitTexts) : "";
        return $"{ci.templateID}|{ci.currentHealth}|{ci.currentAttack}|{ci.currentMaxHealth}|{ci.baseAttack}|{ci.baseHealth}|{ci.baseMaxHealth}|{ci.currentCost}|{ci.currentTier}|{ci.baseTier}|{(ci.hasShield?1:0)}|{(ci.silencedThisPhase?1:0)}|{(ci.isAttached?1:0)}|{(ci.poisoned?1:0)}|{ci.prefixes??""}|{gtt}";
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

        // attachments — 不再盲目清空重建，做 diff
        SyncAttachmentsFromBlock(bm, hm, attachBlock, mistHiderActive);
    }

    static void SyncAttachmentsFromBlock(BoardManager bm, HandManager hm, string attachBlock, bool mistHiderActive)
    {
        if (string.IsNullOrEmpty(attachBlock)) return;

        // 解析附着块为列表
        var incoming = new System.Collections.Generic.List<(string tid, int hs, int order)>();
        foreach (var item in attachBlock.Split(new[] { "||" }, System.StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(item)) continue;
            var p = item.Split('|');
            if (p.Length < 3) continue;
            if (!int.TryParse(p[1], out int hs) || !int.TryParse(p[2], out int o)) continue;
            incoming.Add((p[0], hs, o));
        }

        // 去重 slot 侧已有的模板（独立放置过的牌 → 不是附着物，不重复造）
        var slotTids = new System.Collections.Generic.HashSet<string>();
        for (int si = 0; si < 12; si++)
        {
            var sci = bm.GetSlot(si)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (sci != null && !string.IsNullOrEmpty(sci.templateID)) slotTids.Add(sci.templateID);
        }

        // 移除多余的附着物（incoming 中没有的 → 销毁）
        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
        {
            var obj = bm.attachedModels[i];
            if (obj == null) { bm.attachedModels.RemoveAt(i); continue; }
            var ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || !ci.isAttached) { bm.attachedModels.RemoveAt(i); continue; }
            // incoming hs is SERVER coordinate. ci.hostSlotID is CLIENT coordinate.
            // Map client hostSlotID back to server space for exact comparison.
            int ciServerHS = ci.hostSlotID >= 6 ? ci.hostSlotID - 6 : ci.hostSlotID + 6;
            bool stillExists = incoming.Exists(x => x.tid == ci.templateID
                && x.hs == ciServerHS && x.order == ci.attachOrder);
            if (!stillExists) { SafeDestroy(obj); bm.attachedModels.RemoveAt(i); }
        }

        // 添加/更新 incoming 中的附着物
        foreach (var (tid, hs, o) in incoming)
        {
            if (slotTids.Contains(tid)) continue;

            int cs = hs >= 6 ? hs - 6 : hs + 6;

            // 检查是否已有同模板同附着序号的模型
            GameObject existing = null;
            foreach (var obj in bm.attachedModels)
            {
                var ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == tid && ci.attachOrder == o && ci.hostSlotID == cs)
                { existing = obj; break; }
            }

            if (existing != null)
            {
                // 已有 — 仅更新坐标和宿主
                var eci = existing.GetComponent<Card3DInstance>()?.cardInstance;
                if (eci != null) eci.hostSlotID = cs;
                existing.transform.position = HandManager.GetAttachWorldPos(cs, o);
                continue;
            }

            // 没有 — 新建
            var t = CardDatabase.Instance?.GetTemplate(tid);
            if (t?.prefab3D == null || hm == null) continue;
            var m = Instantiate(t.prefab3D, HandManager.GetAttachWorldPos(cs, o), Quaternion.Euler(0, 180, 0));
            var c = m.GetComponent<Card3DInstance>();
            if (c != null)
            {
                var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0);
                n.isAttached = true; n.hostSlotID = cs; n.attachOrder = o;
                c.cardInstance = n; c.UpdateValues();
            }
            Card3DHover.SetHidden(m, mistHiderActive, true);
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

        string[] parts = raw.Split('|');
        if (parts.Length == 0) { EnsureEmpty(idx, slot, bm); slot.SyncVisual(); return; }

        // Slot flags: last 4 fields = "BBBB" | plagueRoundCount | spotlightTierBoost | slotTempAttackBoost
        // ❗ 必须在 tid 检查之前应用 flag——空槽位的 prisonBlocked 等标记也需要同步
        // ❗ 条件为 >= 5（非 >= 6）——空槽位只产生 5 段（空字符串 + 4 个 flag 段）
        if (parts.Length >= 5)
        {
            string f = parts[parts.Length - 4];
            if (f.Length >= 4)
            {
                slot.isBlocked = f[0] == '1';
                slot.prisonBlocked = f[1] == '1';
                slot.hasPlague = f[2] == '1';
                slot.hasSpotlight = f[3] == '1';
                slot.deepSeaMarked = f.Length >= 5 && f[4] == '1';
                slot.deepSeaHealthDebuff = f.Length >= 6 && f[5] == '1';
            }
            if (int.TryParse(parts[parts.Length - 3], out int prc)) slot.plagueRoundCount = prc;
            if (int.TryParse(parts[parts.Length - 2], out int stb)) slot.spotlightTierBoost = stb;
            // 最后一段 "sTAB~dSAD"（~deepSeaAttackDebuff 为可选兼容）
            string lastField = parts[parts.Length - 1];
            string[] sub = lastField.Split('~');
            if (sub.Length > 0 && int.TryParse(sub[0], out int boost)) slot.slotTempAttackBoost = boost;
            if (sub.Length > 1 && int.TryParse(sub[1], out int dsa)) slot.deepSeaAttackDebuff = dsa;
        }
        slot.SyncVisual();

        // Card part: templateID|hp|atk|maxHp|baseAtk|baseHp|baseMaxHp|cost|tier|baseTier|shield|silenced|attached|poisoned|prefixes (15 fields)
        string tid = parts[0];
        if (string.IsNullOrEmpty(tid)) { EnsureEmpty(idx, slot, bm); return; }

        EnsureCard(idx, parts, slot, bm, hm);
    }

    void EnsureEmpty(int idx, BoardSlot slot, BoardManager bm)
    {
        if (slot.currentCard3D != null)
        {
            var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;

            // 保护：己方半场(6-11) 0.5s 内刚放置的卡 → 防网络竞态
            // 敌方半场(0-5)不保护——服务端权威，过期数据必须清理
            if (idx >= 6 && ci != null && Time.time - ci._placedAtTime < 0.5f) return;

            // 清理附着在此槽位的附着模型
            for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
            {
                var am = bm.attachedModels[i];
                if (am == null) { bm.attachedModels.RemoveAt(i); continue; }
                var aci = am.GetComponent<Card3DInstance>()?.cardInstance;
                if (aci != null && aci.isAttached && aci.hostSlotID == idx)
                {
                    SafeDestroy(am);
                    bm.attachedModels.RemoveAt(i);
                }
            }

            SafeDestroy(slot.currentCard3D); slot.SetCard(null);
        }

        // 槽位标记由 sync data 提供，此处不重置——prisonBlocked 等空槽标记需持久化
    }

    void EnsureCard(int idx, string[] parts, BoardSlot slot, BoardManager bm, HandManager hm)
    {
        string tid = parts[0];
        var cur = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;

        // templateID 不匹配 → 当前模型已过时（换位后、死亡替换后等），销毁后按同步数据重建
        if (cur != null && cur.templateID != tid)
        {
            // 保护：0.3s 内刚放置 → 可能是换位 RPC 尚未到达，暂不销毁
            if (Time.time - cur._placedAtTime < 0.3f) return;

            // 销毁旧模型 + 其附着物，清空槽位以便重建
            for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
            {
                var am = bm.attachedModels[i];
                if (am == null) { bm.attachedModels.RemoveAt(i); continue; }
                var aci = am.GetComponent<Card3DInstance>()?.cardInstance;
                if (aci != null && aci.isAttached && aci.hostSlotID == idx)
                {
                    SafeDestroy(am);
                    bm.attachedModels.RemoveAt(i);
                }
            }
            SafeDestroy(slot.currentCard3D); slot.SetCard(null); cur = null;
        }
        if (cur == null && hm != null)
        {
            // 保护：本槽位刚刚被 HandleDeath 清空 → 不重建（等 DeathPipeline 的网络同步完成后自然一致）
            if (slot.lastHandleDeathTime > 0 && Time.time - slot.lastHandleDeathTime < 2f)
                return;

            var t = CardDatabase.Instance?.GetTemplate(tid);
            // 附着专用卡不放槽位模型
            if (t != null && t.canAttach && t.baseHealth == 0) return;
            if (t?.prefab3D != null)
            {
                var m = Instantiate(t.prefab3D, hm.GetSlotWorldPosition(idx), Quaternion.Euler(0, 180, 0));
                var c = m.GetComponent<Card3DInstance>();
                if (c != null) { var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0); n._placedAtTime = Time.time; c.cardInstance = n; c.UpdateValues(); }
                slot.SetCard(m);
                cur = c?.cardInstance;
            }
        }
        if (cur != null && cur.templateID == tid && parts.Length >= 15)
        {
            var p = parts; int v;
            if (int.TryParse(p[1], out v)) cur.currentHealth = v;
            if (int.TryParse(p[2], out v)) cur.currentAttack = v;
            if (int.TryParse(p[3], out v)) cur.currentMaxHealth = v;
            if (int.TryParse(p[4], out v)) cur.baseAttack = v;
            if (int.TryParse(p[5], out v)) cur.baseHealth = v;
            if (int.TryParse(p[6], out v)) cur.baseMaxHealth = v;
            if (int.TryParse(p[7], out v)) cur.currentCost = v;
            if (int.TryParse(p[8], out v)) cur.currentTier = v;
            if (int.TryParse(p[9], out v)) cur.baseTier = v;
            cur.hasShield = (p[10] == "1");
            cur.silencedThisPhase = (p[11] == "1");
            cur.isAttached = (p[12] == "1");
            cur.poisoned = (p[13] == "1");
            cur.prefixes = p[14];
            // granted trait texts (16th field, ";;" separated)
            if (p.Length > 15)
            {
                var newList = new System.Collections.Generic.List<string>(
                    p[15].Split(new[] { ";;" }, System.StringSplitOptions.None));
                newList.RemoveAll(t => string.IsNullOrEmpty(t));
                if (cur.grantedTraitTexts == null) cur.grantedTraitTexts = new System.Collections.Generic.List<string>();
                var oldCopy = new System.Collections.Generic.List<string>(cur.grantedTraitTexts);
                foreach (var t in oldCopy)
                    if (!newList.Contains(t)) cur.RemoveGrantedTrait(t);
                foreach (var t in newList)
                    if (!oldCopy.Contains(t)) cur.GrantTrait(t);
            }
            // 服务端 FinalDamage 已将临时字段清零；远端本地始终信任服务端同步的 currentAttack
            cur.tempAttackBoost = 0;
            cur.originalAttackBeforeDebuff = 0;
            slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
        }
        Test1Panel.Instance?.RefreshIfOpen();
    }

    static void SafeDestroy(GameObject o) { var ni = o.GetComponent<NetworkIdentity>(); if (ni != null) Object.Destroy(ni); Object.Destroy(o); }
}
