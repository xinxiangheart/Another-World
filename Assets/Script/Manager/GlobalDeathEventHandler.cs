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

        // ===== 3. 水墨(01523)：己方召唤物退场+1能量 =====
        if (isAlly && dyingCI.templateID != "01523")
        {
            CardInstance ink = FindByTemplateID(bm, "01523", isAlly);
            if (ink != null && !IsSilenced(ink))
            {
                NetworkPlayer.Local.AddEnergy(1);
            }
        }

        // ===== 4. 深渊皇帝(01501)：渊前缀伤害来源+1+1 =====
        if (!isAlly) // 对方退场
        {
            CardInstance emperor = FindByTemplateID(bm, "01501", true);
            if (emperor != null && !IsSilenced(emperor))
            {
                foreach (string sourceID in damageSourceInstanceIDs)
                {
                    CardInstance sourceCI = FindByInstanceID(bm, sourceID);
                    if (sourceCI != null && sourceCI.prefixes.Contains("Ԩ") && IsAlly(bm, sourceCI))
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

        // ===== 5. 能量收割者(01528)：导致对方退场+3/+2能量 =====
        if (!isAlly)
        {
            foreach (string sourceID in damageSourceInstanceIDs)
            {
                CardInstance sourceCI = FindByInstanceID(bm, sourceID);
                if (sourceCI != null && sourceCI.templateID == "01528")
                {
                    if (sourceCI.isAttached) NetworkPlayer.Local.AddEnergy(2);
                    else NetworkPlayer.Local.AddEnergy(3);
                }
                else
                {
                    // 伤害来源是宿主，检查宿主身上的能量收割者附着物
                    int hostSlotID = GetSlotOf(bm, sourceID);
                    if (hostSlotID >= 0)
                    {
                        foreach (GameObject obj in bm.attachedModels)
                        {
                            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
                            if (c3d?.cardInstance?.templateID == "01528" && c3d.cardInstance.hostSlotID == hostSlotID)
                            {
                                NetworkPlayer.Local.AddEnergy(2);
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
                    if (NetworkPlayer.Remote != null && NetworkPlayer.Remote.handCards.Count > 0)
                    {
                        int randomIndex = Random.Range(0, NetworkPlayer.Remote.handCards.Count);
                        GameObject card = NetworkPlayer.Remote.handCards[randomIndex];
                        NetworkPlayer.Remote.handCards.RemoveAt(randomIndex);
                        Object.Destroy(card);
                    }
                }
            }
        }
        // 活化母巢(01534)：对方退场+0+1
        if (!isAlly)
        {
            CardInstance nest = FindByTemplateID(bm, "01534", true);
            if (nest != null && !IsSilenced(nest))
            {
                nest.currentAttack += 1;
                nest.baseAttack += 1;
                UpdateDisplay(bm, nest);
            }
        }
        // 复生造物(01513)：标记需要召唤杂兵
        dyingCI._rebornSummon = false;
        if (isAlly && dyingCI != null && dyingCI.templateID != "03004")
        {
            if (dyingCI.enemyDamageSourceIDs.Count > 0)
            {
                CardInstance reborn = FindByTemplateID(bm, "01513", true);
                if (reborn != null && !IsSilenced(reborn))
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
        return -1;
    }

    static bool IsAlly(BoardManager bm, CardInstance ci)
    {
        for (int i = 6; i <= 11; i++)
        {
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return true;
        }
        return false;
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