using System.Collections.Generic;
using UnityEngine;

public static class GlobalDeathEventHandler
{
    public static void Trigger(CardInstance dyingCI, int slotID, List<string> damageSourceInstanceIDs, bool isActiveExit)
    {
        if (dyingCI == null) return;

        BoardManager bm = GameObject.FindObjectOfType<BoardManager>();
        if (bm == null) return;

        bool isAlly = slotID >= 6;
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        NetworkPlayer opponent = BoardManager.GetOpponentPlayer(slotID);

        // ===== 1. 守墓人(01330)：他导致的对方退场，禁止退场效果 =====
        foreach (string sourceID in damageSourceInstanceIDs)
        {
            CardInstance sourceCI = FindByInstanceID(bm, sourceID);
            if (sourceCI != null && sourceCI.templateID == "01330" && !IsSilenced(sourceCI))
            {
                dyingCI.hasOnDeath = false;
                dyingCI.hasActiveExit = false;
                break;
            }
        }

        // ===== 2. 群狼之王(01504)：己方狼退场，群狼之王+1+1 =====
        if (isAlly && dyingCI.templateID == "03006" && !string.IsNullOrEmpty(dyingCI.wolfKingInstanceID))
        {
            CardInstance king = FindByInstanceID(bm, dyingCI.wolfKingInstanceID);
            if (king != null && !IsSilenced(king))
            {
                king.currentHealth += 1;
                king.currentMaxHealth += 1;
                king.currentAttack += 1;
                UpdateDisplay(bm, king);
            }
        }

        // ===== 3. 水墨(01523)：己方召唤物退场（除水墨自身），场上有未沉默的水墨→+1能量 =====
        if (dyingCI.templateID != "01523")
        {
            CardInstance ink = FindByTemplateID_AnySide(bm, "01523");
            if (ink != null && !IsSilenced(ink))
            {
                int inkSlot = GetSlotOf(bm, ink.instanceID);
                bool inkOnAllySide = inkSlot >= 6;
                // 水墨只对同半场的退场做出反应
                if (inkOnAllySide == isAlly)
                {
                    NetworkPlayer inkOwner = BoardManager.GetOwnerPlayer(inkSlot);
                    inkOwner?.AddEnergy(1);
                }
            }
        }

        // ===== 4. 深渊皇帝(01501)：渊前缀伤害来源+1+1（对方退场时触发）=====
        if (!isAlly)
        {
            CardInstance emperor = FindByTemplateID_AnySide(bm, "01501");
            if (emperor != null && !IsSilenced(emperor))
            {
                int emperorSlot = GetSlotOf(bm, emperor.instanceID);
                if (emperorSlot >= 6)
                {
                    foreach (string sourceID in damageSourceInstanceIDs)
                    {
                        CardInstance sourceCI = FindByInstanceID(bm, sourceID);
                        if (sourceCI != null && sourceCI.prefixes.Contains("渊") && IsOnSide(bm, sourceCI, emperorSlot))
                        {
                            if (!sourceCI.cannotHealOrGainMaxHP)
                            {
                                sourceCI.currentHealth += 1;
                                sourceCI.currentMaxHealth += 1;
                            }
                            sourceCI.currentAttack += 1;
                            UpdateDisplay(bm, sourceCI);
                        }
                    }
                }
            }
        }

        // ===== 5. 能量收割者(01528)：导致对方退场+3/+2能量 =====
        if (!isAlly)
        {
            foreach (string sourceID in damageSourceInstanceIDs)
            {
                CardInstance sourceCI = FindByInstanceID(bm, sourceID);
                if (sourceCI != null && sourceCI.templateID == "01528" && !IsSilenced(sourceCI))
                {
                    int reaperSlot = GetSlotOf(bm, sourceCI.instanceID);
                    NetworkPlayer reaperOwner = BoardManager.GetOwnerPlayer(reaperSlot);
                    if (sourceCI.isAttached) reaperOwner?.AddEnergy(2);
                    else reaperOwner?.AddEnergy(3);
                }
                else
                {
                    // 伤害来源是宿主，检查宿主身上的能量收割者附着物
                    int hostSlotID = GetSlotOfByInstanceID(bm, sourceID);
                    if (hostSlotID >= 0)
                    {
                        foreach (GameObject obj in bm.attachedModels)
                        {
                            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
                            if (c3d?.cardInstance?.templateID == "01528" && c3d.cardInstance.hostSlotID == hostSlotID)
                            {
                                NetworkPlayer hostOwner = BoardManager.GetOwnerPlayer(hostSlotID);
                                hostOwner?.AddEnergy(2);
                            }
                        }
                    }
                }
            }
        }

        // ===== 6. 恐惧之龙(01530)：导致对方退场，弃对方一张牌 =====
        if (!isAlly)
        {
            foreach (string sourceID in damageSourceInstanceIDs)
            {
                CardInstance sourceCI = FindByInstanceID(bm, sourceID);
                if (sourceCI != null && sourceCI.templateID == "01530" && !IsSilenced(sourceCI))
                {
                    NetworkPlayer dragonOwner = BoardManager.GetOwnerPlayer(GetSlotOf(bm, sourceCI.instanceID));
                    NetworkPlayer dragonOpponent = BoardManager.GetOpponentPlayer(GetSlotOf(bm, sourceCI.instanceID));
                    if (dragonOpponent != null && dragonOpponent.handCards.Count > 0)
                    {
                        int randomIndex = Random.Range(0, dragonOpponent.handCards.Count);
                        GameObject card = dragonOpponent.handCards[randomIndex];
                        dragonOpponent.handCards.RemoveAt(randomIndex);
                        Object.Destroy(card);
                    }
                }
            }
        }

        // ===== 7. 活化母巢(01534)：对方退场+0+1 =====
        if (!isAlly)
        {
            CardInstance nest = FindByTemplateID_AnySide(bm, "01534");
            if (nest != null && !IsSilenced(nest) && IsOnSameSide(bm, nest, slotID))
            {
                nest.currentAttack += 1;
                nest.baseAttack += 1;
                UpdateDisplay(bm, nest);
            }
        }

        // ===== 8. 复生造物(01513)：标记需要召唤杂兵 =====
        dyingCI._rebornSummon = false;
        if (isAlly && dyingCI != null && dyingCI.templateID != "03004")
        {
            if (dyingCI.enemyDamageSourceIDs.Count > 0)
            {
                CardInstance reborn = FindByTemplateID_AnySide(bm, "01513");
                if (reborn != null && !IsSilenced(reborn) && IsOnSameSide(bm, reborn, slotID))
                {
                    dyingCI._rebornSummon = true;
                }
            }
        }
    }

    // ========== 辅助方法 ==========

    static bool IsSilenced(CardInstance ci)
    {
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci);
    }

    /// <summary>在指定半场搜索 templateID。</summary>
    static CardInstance FindByTemplateID(BoardManager bm, string templateID, bool searchAlly)
    {
        int start = searchAlly ? 6 : 0;
        int end = searchAlly ? 11 : 5;
        for (int i = start; i <= end; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == templateID) return ci;
            }
        }
        return null;
    }

    /// <summary>遍历全部 12 槽位搜索 templateID。</summary>
    static CardInstance FindByTemplateID_AnySide(BoardManager bm, string templateID)
    {
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == templateID) return ci;
            }
        }
        foreach (GameObject obj in bm.attachedModels)
        {
            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance?.templateID == templateID) return c3d.cardInstance;
        }
        return null;
    }

    static CardInstance FindByInstanceID(BoardManager bm, string instanceID)
    {
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.instanceID == instanceID) return ci;
            }
        }
        foreach (GameObject obj in bm.attachedModels)
        {
            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance?.instanceID == instanceID) return c3d.cardInstance;
        }
        return null;
    }

    static int GetSlotOf(BoardManager bm, string instanceID)
    {
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance?.instanceID == instanceID) return i;
        }
        foreach (GameObject obj in bm.attachedModels)
        {
            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance?.instanceID == instanceID) return c3d.cardInstance.hostSlotID;
        }
        return -1;
    }

    /// <summary>根据 instanceID 查找宿主槽位（用于附着物查找）。</summary>
    static int GetSlotOfByInstanceID(BoardManager bm, string instanceID)
    {
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance?.instanceID == instanceID) return i;
        }
        return -1;
    }

    /// <summary>card 和 referenceSlot 是否在同半场。</summary>
    static bool IsOnSameSide(BoardManager bm, CardInstance card, int referenceSlotID)
    {
        int cardSlot = GetSlotOf(bm, card.instanceID);
        return (cardSlot >= 6) == (referenceSlotID >= 6);
    }

    /// <summary>card 是否和 referenceSlot 在同半场。</summary>
    static bool IsOnSide(BoardManager bm, CardInstance card, int referenceSlotID)
    {
        int cardSlot = GetSlotOfByInstanceID(bm, card.instanceID);
        return (cardSlot >= 6) == (referenceSlotID >= 6);
    }

    static void UpdateDisplay(BoardManager bm, CardInstance ci)
    {
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            {
                s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                return;
            }
        }
    }
}