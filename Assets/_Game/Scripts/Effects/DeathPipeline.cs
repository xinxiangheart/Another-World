using System.Collections.Generic;
using Mirror;
using UnityEngine;

// ============================================================================
// DeathPipeline — 死亡通用后处理管线（Step 2a）
// ============================================================================
//
// 从 BoardSlot.HandleDeath (原 lines 1592-1740) 提取所有死亡共用的后处理流程。
// 无论哪张卡退场，以下步骤始终一致：
//
//   1. 清除临时加成 + 数值重置为 base
//   2. 墓地记录 (GraveyardManager)
//   3. 指挥官双倍死亡 (_conductorDoubleDeath)
//   4. 古老精灵重附着 / 非妖精附着物销毁
//   5. SetCard(null)
//   6. _rebornSummon → 召唤杂兵(03004)
//   7. 回手逻辑 (03504/01117/03009)
//   8. Destroy(dyingCard)
//   9. X 值刷新
//  10. 清除宿主附着物 + SyncMistHiderDisplay
//  11. 网络同步 (pure client → SyncMyBoardToOpponent)
//
// 当前阶段（2a）: HandleDeath 直接同步调用 ExecuteCommon。
// 后续（2c）: 每一步拆为独立 SyncAction，通过 ActionQueueManager 入队执行。
// ============================================================================

/// <summary>
/// 死亡后处理参数包。HandleDeath 在 per-templateID 分支执行完毕后填充此结构。
/// </summary>
public struct DeathPipelineParams
{
    public GameObject dyingCard;
    public Card3DInstance c3d;
    /// <summary>死亡发生的槽位（用于 SetCard、StartCoroutine 等）。</summary>
    public BoardSlot slot;
    public bool shouldReturn03504;
    public CardData template03504;
    public bool shouldReturn01117;
    public CardData template01117;
    public bool shouldReturn03009;
    public CardData template03009;
}

public static class DeathPipeline
{
    /// <summary>
    /// 执行所有卡牌死亡共用的后处理管线。
    /// 调用时机：HandleDeath 中 per-templateID 分支全部执行完毕后。
    /// </summary>
    public static void ExecuteCommon(DeathPipelineParams p)
    {
        var c3d = p.c3d;
        var ci = c3d?.cardInstance;
        var slot = p.slot;
        if (ci == null || slot == null) return;
        int slotID = slot.slotID;

        // ── 1. 清除临时加成 + 重置为 base ──────────────────────────────
        if (ci.tempHealthBoost > 0)
            ci.currentHealth -= ci.tempHealthBoost;
        ci.currentAttack -= ci.tempAttackBoost;
        ci.tempAttackBoost = 0;
        ci.tempHealthBoost = 0;

        // 二次清理（原代码有重复，保留以防 temp boost 多次叠加）
        if (ci.tempHealthBoost > 0)
            ci.currentHealth -= ci.tempHealthBoost;
        ci.currentAttack -= ci.tempAttackBoost;
        ci.tempAttackBoost = 0;
        ci.tempHealthBoost = 0;

        ci.currentAttack = ci.baseAttack;
        ci.currentHealth = ci.baseHealth;
        ci.currentMaxHealth = ci.baseMaxHealth;
        ci.currentTier = ci.baseTier;

        // ── 2. 墓地记录 ────────────────────────────────────────────────
        GraveEntry entry = new GraveEntry();
        entry.templateID = ci.templateID;
        entry.instanceID = ci.instanceID;
        entry.currentCost = ci.currentCost;
        entry.currentAttack = ci.currentAttack;
        entry.baseAttack = ci.baseAttack;
        entry.currentHealth = ci.currentHealth;
        entry.baseHealth = ci.baseHealth;
        entry.baseMaxHealth = ci.baseMaxHealth;
        entry.currentMaxHealth = ci.currentMaxHealth;
        entry.currentTier = ci.currentTier;
        entry.baseTier = ci.baseTier;
        entry.prefixes = ci.prefixes;
        entry.handledReturnToHand = false;
        entry.deathPhase = TurnManager.Instance.phaseCount;
        GraveyardManager.Instance.AddToGraveyard(entry);

        // ── 3. 指挥官双倍死亡 ──────────────────────────────────────────
        if (ci._conductorDoubleDeath)
        {
            ci._conductorDoubleDeath = false;
            BoardSlot.DeathEffectData data = BoardSlot.ExtractDeathData(ci);
            data.slotID = slotID;
            slot.StartCoroutine(slot.ConductorDoubleDeathEffect(data));
        }

        // ── 4. 处理附着物（古老精灵重附着 + 非妖精销毁） ──────────────
        // 纯客户端：保留妖精在 attachedModels 中——TargetRpc 按 hostSlotID 查找
        if (ci.isAttached == false)
        {
            BoardManager bm = Object.FindObjectOfType<BoardManager>();
            if (bm == null) return;
            List<GameObject> fairies = new List<GameObject>();
            foreach (GameObject obj in bm.attachedModels)
            {
                Card3DInstance c3dAtt = obj?.GetComponent<Card3DInstance>();
                if (c3dAtt?.cardInstance != null
                    && c3dAtt.cardInstance.isAncientFairy
                    && c3dAtt.cardInstance.hostSlotID == slotID)
                {
                    fairies.Add(obj);
                }
            }

            foreach (GameObject fairy in fairies)
            {
                CardInstance fairyCIg = fairy.GetComponent<Card3DInstance>()?.cardInstance;
                // 5.x 01510 转移沉默门：古老精灵(01510)自身被完全沉默 → 不转移再附，随宿主退场销毁。
                // 复用下方"无他友销毁"语义（两端一致：服务端销毁不发重附 RPC；纯客户端本地同步销毁）。
                if (fairyCIg != null && GlobalEventManager.Instance != null
                    && GlobalEventManager.Instance.IsFullySilenced(fairyCIg))
                {
                    bm.attachedModels.Remove(fairy);
                    fairyCIg.isActiveExit = true;
                    Object.Destroy(fairy);
                    continue;
                }

                bool isPureClient = NetworkClient.isConnected && !NetworkServer.active;

                bool hasOtherAlly = false;
                BoardManager.GetSideRange(p.slot.slotID, out int afS, out int afE);
                for (int i = afS; i <= afE; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s != null && s.hasCard && s.slotID != p.slot.slotID)
                    {
                        hasOtherAlly = true;
                        break;
                    }
                }

                if (hasOtherAlly)
                {
                    if (isPureClient)
                    {
                        // 纯客户端移除但不销毁——模型清除，TargetRpc 在 _fairyPending 中查找
                        bm.attachedModels.Remove(fairy);
                        BoardSlot._fairyPending.Add(fairy);
                    }
                    else
                    {
                        bm.attachedModels.Remove(fairy);
                        BoardSlot.isPlacingCard = true;
                        slot.StartCoroutine(slot.AncientFairyReattach(fairy, p.slot.slotID));
                    }
                }
                else
                {
                    bm.attachedModels.Remove(fairy);
                    CardInstance fairyCI = fairy.GetComponent<Card3DInstance>()?.cardInstance;
                    if (fairyCI != null) fairyCI.isActiveExit = true;
                    Object.Destroy(fairy);
                }
            }
        }

        // ── 5. SetCard(null) ───────────────────────────────────────────
        slot.SetCard(null);
        // 通知远端客户端销毁此槽位模型——纯客户端不根据 SyncNow 销毁模型。
        // AI 对手（connectionToClient == null）无客户端，跳过 RPC。
        if (Mirror.NetworkServer.active && !slot.prisonBlocked && NetworkPlayer.Remote != null
            && NetworkPlayer.Remote.connectionToClient != null)
            NetworkPlayer.Remote.TargetDestroyCard(NetworkPlayer.Remote.connectionToClient, slot.slotID);

        // ── 6. _rebornSummon → 召唤杂兵(03004) ─────────────────────────
        if (ci._rebornSummon)
        {
            CardData soldierTemplate = CardDatabase.Instance?.GetTemplate("03004");
            if (soldierTemplate?.prefab3D != null && !slot.isBlocked)
            {
                GameObject temp = new GameObject("TempSoldier");
                CardInstance ti = temp.AddComponent<CardInstance>();
                ti.InitFromTemplate(soldierTemplate, 0, CardZoneManager.GenerateInstanceID("03004"));
                HandManager hm = Object.FindObjectOfType<HandManager>();
                hm?.PlaceCardToSlot(slot, temp);
                Object.Destroy(temp);
                // Token 同步到服务器/对方——纯客户端不依赖 SyncNow 创建模型
                if (NetworkClient.isConnected)
                    NetworkPlayer.Local?.CmdPlayCard("03004", slot.slotID, -1, -1, -1, -1, ti.instanceID);
            }
        }

        // ── 7. 回手逻辑（仅服务端/离线执行——客户端由板面同步驱动，重复执行会导致重复加到双方手牌）──
        if (NetworkServer.active || !NetworkClient.isConnected)
        {
            if (p.shouldReturn03504 && p.template03504 != null)
            {
                NetworkPlayer owner = BoardManager.GetOwnerPlayer(p.slot.slotID);
                if (owner != null) owner.AddCardToHandFromInstance(p.template03504, ci);
            }
            if (p.shouldReturn01117 && p.template01117 != null)
            {
                NetworkPlayer owner = BoardManager.GetOwnerPlayer(p.slot.slotID);
                if (owner != null) owner.AddCardToHandFromInstance(p.template01117, ci);
            }
            if (p.shouldReturn03009 && p.template03009 != null)
            {
                NetworkPlayer owner = BoardManager.GetOwnerPlayer(p.slot.slotID);
                if (owner != null) owner.AddCardToHandFromInstance(p.template03009, ci);
            }
        }

        // ── 8. Destroy ─────────────────────────────────────────────────
        Object.Destroy(p.dyingCard);

        // ── 9. X 值刷新 ────────────────────────────────────────────────
        HandManager hmDeath = Object.FindObjectOfType<HandManager>();
        if (hmDeath != null)
        {
            BoardManager bmDeath = Object.FindObjectOfType<BoardManager>();
            if (bmDeath != null)
            {
                BoardManager.GetSideRange(p.slot.slotID, out int xvS, out int xvE);
                for (int i = xvS; i <= xvE; i++)
                {
                    BoardSlot sd = bmDeath.GetSlot(i);
                    if (sd?.currentCard3D == null) continue;
                    CardInstance ciX = sd.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ciX != null && ciX.isXValue) hmDeath.UpdateXValues(ciX);
                }
            }
        }

        // ── 10. 清除宿主的非妖精附着物 + SyncMistHiderDisplay ─────────
        BoardManager bmAtt = Object.FindObjectOfType<BoardManager>();
        if (bmAtt != null)
            for (int iB = bmAtt.attachedModels.Count - 1; iB >= 0; iB--)
            {
                GameObject obj = bmAtt.attachedModels[iB];
                if (obj == null) continue;
                Card3DInstance ca = obj.GetComponent<Card3DInstance>();
                if (ca?.cardInstance != null && ca.cardInstance.hostSlotID == slotID)
                {
                    if (ca.cardInstance.isAncientFairy) continue;
                    // 01534 活化母巢：附着物退场也+0+1
                    GlobalDeathEventHandler.GiveNestBuff(ca.cardInstance.hostSlotID);
                    bmAtt.attachedModels.RemoveAt(iB);
                    BoardManager.RecordAndRemoveAttach(obj);
                }
            }
        BoardSlot.SyncMistHiderDisplay();

        // ── 11. 网络同步已移至 CheckAndHandleDeaths 的 onAllDeathsResolved 回调 ——
        //     死亡链全部排空后统一同步，避免每个死亡中途上报中间态板面。
    }
}
