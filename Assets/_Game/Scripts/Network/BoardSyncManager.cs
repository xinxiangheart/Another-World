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

    /// <summary>防止刚放置/换位的卡牌被过期同步数据覆盖的时间窗口（秒）。</summary>
    public const float PLACE_PROTECT_WINDOW = 2.0f;

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
                    // 两个槽位有同一 instanceID → 保留放置时间较新的。
                    // 注意：跨半场比较时 _placedAtTime 来自不同客户端本地时钟，绝对值不可靠；
                    // 同 instanceID 跨端重复极罕见，一般情况下不会触发此路径。
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
                $"{(slot.hasSpotlight?1:0)}{(slot.deepSeaMarked?1:0)}{(slot.deepSeaHealthDebuff?1:0)}{(slot.permaBlocked?1:0)}|{slot.plagueRoundCount}|{slot.spotlightTierBoost}|{slot.slotTempAttackBoost}~{slot.deepSeaAttackDebuff}";
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
            al.Add($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}|{ci.instanceID ?? ""}");
        }
        string ab = al.Count > 0 ? string.Join("||", al) : "";

        // 清理已过期移除记录——当前仍在板面上的附件无需保护
        foreach (var o in bm.attachedModels)
        {
            var ci = o?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null)
            {
                if (!string.IsNullOrEmpty(ci.instanceID))
                    BoardManager.removedAttachIDs.Remove(ci.instanceID);
                BoardManager.removedAttachKeys.Remove($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}");
            }
        }

        // Signal whether the server-side has an active MistHider so the client hides the correct side
        // Also sync global shadow state (01502) for remote clients
        bool mistHiderActive = IsMistHiderActive();
        BoardManager.attachGen++;
        string header = $"{(mistHiderActive ? "1" : "0")}|{CardInstance.shadowLimit}|{CardInstance.shadowAtkBonus}|{CardInstance.shadowTierBonus}|{BoardManager.attachGen}|";

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
        // 管道字段索引: 0=tid  1=HP⛔  2=ATK⛔  3=maxHP⛔  4=baseATK⛔  5=baseHP⛔  6=baseMaxHP⛔
        //   7=cost⛔  8=tier⛔  9=baseTier⛔  10=shield⛔  11=silenced✓  12=attached⛔  13=poisoned✓
        //   14=prefixes⛔  15=grantedTraits⛔  16=totalDamageTaken⛔
        //   17=hasBuff⛔  18=buffText⛔  19=hasDebuff⛔  20=debuffText⛔  21=lastGivenPrefix⛔（卡名变色规则1）
        //   ✓=允许跨半场  ⛔=仅己方半场
        string gtt = ci.SerializeGrantedTraits();
        return $"{ci.templateID}|{ci.currentHealth}|{ci.currentAttack}|{ci.currentMaxHealth}|{ci.baseAttack}|{ci.baseHealth}|{ci.baseMaxHealth}|{ci.currentCost}|{ci.currentTier}|{ci.baseTier}|{(ci.hasShield?(1+(ci.shieldIsPermanent?2:0)+(ci.shieldEndAtBattleStart?4:0)+(ci.shieldEndAtBattleEnd?8:0)):0)}|{(ci.silencedThisPhase?1:0)}|{(ci.isAttached?1:0)}|{(ci.poisoned?1:0)}|{ci.prefixes??""}|{gtt}|{ci.totalDamageTaken}|{(ci.hasBuff?1:0)}|{ci.buffText??""}|{(ci.hasDebuff?1:0)}|{ci.debuffText??""}|{ci.lastGivenPrefix??""}";
    }

    // ============= Client =============

    public void ApplySync(string[] s, string attachBlockExt)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        HandManager hm = FindObjectOfType<HandManager>();
        if (bm == null || s == null || s.Length < 12) return;

        // Parse header: "mistHider|shadowLimit|shadowAtkBonus|shadowTierBonus|gen|attachBlock"
        bool mistHiderActive = false;
        int syncGen = 0;
        string attachBlock = attachBlockExt;
        if (!string.IsNullOrEmpty(attachBlockExt))
        {
            string[] hp = attachBlockExt.Split('|');
            if (hp.Length >= 1) mistHiderActive = hp[0] == "1";
            if (hp.Length >= 4)
            {
                if (int.TryParse(hp[1], out int sl))  CardInstance.shadowLimit = Mathf.Max(CardInstance.shadowLimit, sl);
                if (int.TryParse(hp[2], out int sab)) CardInstance.shadowAtkBonus = Mathf.Max(CardInstance.shadowAtkBonus, sab);
                if (int.TryParse(hp[3], out int stb)) CardInstance.shadowTierBonus = Mathf.Max(CardInstance.shadowTierBonus, stb);
                if (hp.Length >= 5 && int.TryParse(hp[4], out int g) && g < 10000) { syncGen = g; if (hp.Length > 5) attachBlock = string.Join("|", hp, 5, hp.Length - 5); else attachBlock = ""; }
                else { if (hp.Length > 4) attachBlock = string.Join("|", hp, 4, hp.Length - 4); else attachBlock = ""; }
            }
            else
            {
                int sepIdx = attachBlockExt.IndexOf('|');
                if (sepIdx >= 0) { mistHiderActive = attachBlockExt[0] == '1'; attachBlock = attachBlockExt.Substring(sepIdx + 1); }
            }
        }

        for (int i = 0; i < 6; i++)
        {
            ApplySlot(i + 6, s[i], bm, hm, syncGen);     // server 0-5 → client 6-11
            ApplySlot(i, s[i + 6], bm, hm, syncGen);     // server 6-11 → client 0-5
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
        var incoming = new System.Collections.Generic.List<(string tid, int hs, int order, string iid)>();
        if (!string.IsNullOrEmpty(attachBlock))
        {
            foreach (var item in attachBlock.Split(new[] { "||" }, System.StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(item)) continue;
                var p = item.Split('|');
                if (p.Length < 3) continue;
                if (!int.TryParse(p[1], out int hs) || !int.TryParse(p[2], out int o)) continue;
                string iid = p.Length > 3 ? p[3] : "";
                incoming.Add((p[0], hs, o, iid));
            }
        }

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
            int ciServerHS = ci.hostSlotID >= 6 ? ci.hostSlotID - 6 : ci.hostSlotID + 6;
            bool stillExists = incoming.Exists(x =>
                (!string.IsNullOrEmpty(x.iid) && x.iid == ci.instanceID)
                || (x.tid == ci.templateID && x.hs == ciServerHS && x.order == ci.attachOrder));
            if (!stillExists)
            {
                if (ci != null && ci.isAncientFairy)
                {
                    bm.attachedModels.RemoveAt(i);
                    BoardSlot._fairyPending.Add(obj);
                    continue;
                }
                bm.attachedModels.RemoveAt(i);
                BoardManager.RecordAndRemoveAttach(obj);
            }
        }

        // 添加/更新 incoming 中的附着物
        foreach (var (tid, hs, o, iid) in incoming)
        {
            if (slotTids.Contains(tid)) continue;
            int cs = hs >= 6 ? hs - 6 : hs + 6;

            // 用 instanceID 查找已有模型
            GameObject existing = null;
            foreach (var obj in bm.attachedModels)
            {
                var ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == tid)
                {
                    if (!string.IsNullOrEmpty(iid) && ci.instanceID == iid) { existing = obj; break; }
                    if (ci.attachOrder == o && ci.hostSlotID == cs) { existing = obj; break; }
                }
            }

            if (existing != null)
            {
                var eci = existing.GetComponent<Card3DInstance>()?.cardInstance;
                if (eci != null) eci.hostSlotID = cs;
                existing.transform.position = HandManager.GetAttachWorldPos(cs, o);
                continue;
            }

            // 没有 — 新建
            // 被明确移除过的 attachment 不重建
            if ((!string.IsNullOrEmpty(iid) && BoardManager.removedAttachIDs.Contains(iid))
                || BoardManager.removedAttachKeys.Contains($"{tid}|{cs}|{o}"))
                continue;

            var t = CardDatabase.Instance?.GetTemplate(tid);
            if (t?.prefab3D == null || hm == null) continue;
            var m = Instantiate(t.prefab3D, HandManager.GetAttachWorldPos(cs, o), Quaternion.Euler(0, 180, 0));
            var c = m.GetComponent<Card3DInstance>();
            if (c != null)
            {
                var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0);
                n.isAttached = true; n.hostSlotID = cs; n.attachOrder = o; n._placedAtTime = Time.time;
                n.placementGeneration = BoardSlot.NextPlacementGeneration();
                c.cardInstance = n; c.UpdateValues();
                c.PlayAttachSlideIn(HandManager.GetAttachWorldPos(cs, o + 1), HandManager.GetAttachWorldPos(cs, o)); // 附着滑入（仅表现）
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

    void ApplySlot(int idx, string raw, BoardManager bm, HandManager hm, int syncGen)
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
                slot.permaBlocked = f.Length >= 7 && f[6] == '1';
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

        EnsureCard(idx, parts, slot, bm, hm, syncGen);
    }

    void EnsureEmpty(int idx, BoardSlot slot, BoardManager bm)
    {
        if (slot.currentCard3D != null)
        {
            var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            // 刚放置/换位的卡不销毁（时间窗口，双端一致）——防止过期同步误删刚出的牌
            if (ci != null && Time.time - ci._placedAtTime < PLACE_PROTECT_WINDOW) return;
            // 纯客户端兜底守卫：从未被服务端同步确认（serverAckGen<0）的牌不销毁——
            // 可能是刚放置尚未被服务端确认，避免过期同步误删。主销毁路径仍是 TargetDestroyCard RPC。
            // 此兜底覆盖所有绕过 TargetDestroyCard 的移除路径（直接 Destroy+SetCard(null) 等）。
            if (NetworkClient.isConnected && !NetworkServer.active && ci != null && ci.serverAckGen < 0) return;

            for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
            {
                var am = bm.attachedModels[i];
                if (am == null) { bm.attachedModels.RemoveAt(i); continue; }
                var aci = am.GetComponent<Card3DInstance>()?.cardInstance;
                if (aci != null && aci.isAttached && aci.hostSlotID == idx)
                {
                    if (aci.isAncientFairy)
                    {
                        bm.attachedModels.RemoveAt(i);
                        BoardSlot._fairyPending.Add(am);
                    }
                    else
                    {
                        SafeDestroy(am);
                        bm.attachedModels.RemoveAt(i);
                    }
                }
            }

            slot.lastHandleDeathTime = Time.time;
            if (slot.currentCard3D != null)
            {
                var dyingCi = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (dyingCi != null) { dyingCi.isDead = true; dyingCi.deathGeneration = dyingCi.placementGeneration; }
            }
            SafeDestroy(slot.currentCard3D); slot.SetCard(null);
        }
    }

    void EnsureCard(int idx, string[] parts, BoardSlot slot, BoardManager bm, HandManager hm, int syncGen)
    {
        string tid = parts[0];
        var cur = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        bool justCreated = false;

        // templateID 不匹配 → 当前模型已过时。纯客户端：TargetSpawnCard3D 权威处理，SyncNow 不做替换
        if (cur != null && cur.templateID != tid)
        {
            if (NetworkClient.isConnected && !NetworkServer.active) return;
            if (cur._hadEnterEffect) return;
            if (cur._enterEffectRunning) return;
            if (tid == "03006" && cur.templateID != "03006") return;
            if (Time.time - cur._placedAtTime < PLACE_PROTECT_WINDOW) return;

            for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
            {
                var am = bm.attachedModels[i];
                if (am == null) { bm.attachedModels.RemoveAt(i); continue; }
                var aci = am.GetComponent<Card3DInstance>()?.cardInstance;
                if (aci != null && aci.isAttached && aci.hostSlotID == idx)
                {
                    if (aci.isAncientFairy)
                    {
                        bm.attachedModels.RemoveAt(i);
                        BoardSlot._fairyPending.Add(am);
                    }
                    else
                    {
                        SafeDestroy(am);
                        bm.attachedModels.RemoveAt(i);
                    }
                }
            }
            SafeDestroy(slot.currentCard3D); slot.SetCard(null); cur = null;
        }
        // 纯客户端：仅允许已存在模型的数值更新——不根据 SyncNow 创建新模型
        if (cur == null && hm != null)
        {
            if (NetworkClient.isConnected && !NetworkServer.active) return;
            if (slot.lastHandleDeathTime > 0 && Time.time - slot.lastHandleDeathTime < PLACE_PROTECT_WINDOW) return;

            var t = CardDatabase.Instance?.GetTemplate(tid);
            if (t != null && t.canAttach && t.baseHealth == 0) return;
            if (t?.prefab3D != null)
            {
                var m = Instantiate(t.prefab3D, hm.GetSlotWorldPosition(idx), Quaternion.Euler(0, 180, 0));
                Card3DInstance.PlaySummonOn(m); // 召唤动画
                var c = m.GetComponent<Card3DInstance>();
                if (c != null) { var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0); if (t.templateID == "03007") n.isShadow = true; if (t.templateID == "01502") CardInstance.shadowMasterAlive = true; n._placedAtTime = Time.time; n.placementGeneration = BoardSlot.NextPlacementGeneration(); n.serverAckGen = syncGen; c.cardInstance = n; c.UpdateValues(); }
                slot.SetCard(m);
                cur = c?.cardInstance; justCreated = true;
            }
        }
        if (cur != null && cur.templateID == tid && parts.Length >= 15)
        {
            // 服务端同步包含此卡 → 该卡已被服务端确认（EnsureEmpty 兜底销毁守卫依据）
            cur.serverAckGen = syncGen;
            var p = parts; int v;
            if (int.TryParse(p[1], out v)) cur.currentHealth = v;
            if (int.TryParse(p[2], out v)) cur.currentAttack = v;
            if (int.TryParse(p[3], out v)) cur.currentMaxHealth = v;
            if (int.TryParse(p[4], out v)) cur.baseAttack = v;
            if (int.TryParse(p[5], out v)) cur.baseHealth = v;
            if (int.TryParse(p[6], out v)) cur.baseMaxHealth = v;
            // 费用在场锁死——只设刚创建/重建的牌，已有牌不覆盖
            if (justCreated && int.TryParse(p[7], out v)) cur.currentCost = v;
            if (int.TryParse(p[8], out v)) cur.currentTier = v;
            if (int.TryParse(p[9], out v)) cur.baseTier = v;
              // 护盾类型编码: 0=无 bit0=hasShield bit1=永久 bit2=攻击开始消失 bit3=攻击结束消失
            if (int.TryParse(p[10], out int shieldEnc) && shieldEnc > 0)
            {
                cur.hasShield = true;
                cur.shieldIsPermanent = (shieldEnc & 2) != 0;
                cur.shieldEndAtBattleStart = (shieldEnc & 4) != 0;
                cur.shieldEndAtBattleEnd = (shieldEnc & 8) != 0;
            }
            else if (cur.hasShield && cur._placedAtTime > 0 && Time.time - cur._placedAtTime < PLACE_PROTECT_WINDOW
                && NetworkClient.isConnected && !NetworkServer.active)
            {
                // 纯客户端：进场2秒内保护护盾（进场效果异步设盾，
                // CmdPlayCard后首个SyncNow尚未包含盾信息）
            }
            else
            {
                cur.hasShield = false;
                cur.shieldIsPermanent = false;
                cur.shieldEndAtBattleStart = false;
                cur.shieldEndAtBattleEnd = false;
            }
            cur.silencedThisPhase = (p[11] == "1");
            cur.ApplySilenceToTraits(); // 对端板面同步应用：特性组派生 BlockAll/UnblockAll
            cur.isAttached = (p[12] == "1");
            cur.poisoned = (p[13] == "1");
            cur.prefixes = p[14];
            // granted trait texts (16th field, ";;" 分隔；结构化 text~attrs~source，兼容旧纯文本)
            if (p.Length > 15)
                cur.ApplySyncedGrantedTraits(p[15]);
            // totalDamageTaken (17th field, 01534 活化母巢需要)
            if (p.Length > 16 && int.TryParse(p[16], out int tdt))
                cur.totalDamageTaken = Mathf.Max(cur.totalDamageTaken, tdt);
            // Buff/Debuff 持续状态（18-21th 字段，向后兼容——旧数据缺省为 false/空）
            if (p.Length > 17) cur.hasBuff = p[17] == "1";
            if (p.Length > 18) cur.buffText = p[18];
            if (p.Length > 19) cur.hasDebuff = p[19] == "1";
            if (p.Length > 20) cur.debuffText = p[20];
            if (p.Length > 21) cur.lastGivenPrefix = p[21]; // 卡名变色：最后一次赋予的新前缀
            // 服务端 FinalDamage 已将临时字段清零；远端本地始终信任服务端同步的 currentAttack
            cur.tempAttackBoost = 0;
            cur.originalAttackBeforeDebuff = 0;
            if (cur.templateID == "03007") cur.isShadow = true;
            if (cur.templateID == "01502") CardInstance.shadowMasterAlive = true;
            slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
        }
        Test1Panel.Instance?.RefreshIfOpen();
    }

    static void SafeDestroy(GameObject o) { var ni = o.GetComponent<NetworkIdentity>(); if (ni != null) Object.Destroy(ni); Object.Destroy(o); }
}
