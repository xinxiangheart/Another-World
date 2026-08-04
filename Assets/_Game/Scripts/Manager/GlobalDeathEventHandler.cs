using System.Collections.Generic;
using UnityEngine;

public static class GlobalDeathEventHandler
{
    public static void Trigger(CardInstance dyingCI, int slotID, List<string> damageSourceInstanceIDs, bool isActiveExit)
    {
        if (dyingCI == null) return;
        // 附着物随宿主退场——退场来源是宿主，和导致宿主退场的来源无关。
        // 任何全局退场监听（01501/01528/01530等）不应对附着物的分离退场做出反应。
        if (dyingCI.isAttached) return;

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
        if (isAlly && dyingCI.templateID == "03006")
        {
            // 优先用 instanceID 精确查找，服务器侧狼无此字段时回退到同半场 templateID 查找
            CardInstance king = null;
            if (!string.IsNullOrEmpty(dyingCI.wolfKingInstanceID))
                king = FindByInstanceID(bm, dyingCI.wolfKingInstanceID);
            if (king == null)
                king = FindByTemplateID(bm, "01504", isAlly);
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
        // 不依赖 isAlly——01528可能在服务器0-5导致6-11退场，也可能在6-11导致0-5退场。
        // 每层独立检查01528与死亡卡是否在对侧。
        {
            string ctx = $"dyingSlot={slotID} dyingTid={dyingCI.templateID} src#={damageSourceInstanceIDs.Count} srv={Mirror.NetworkServer.active} ally={isAlly}";
            Debug.Log($"[01528] 进入: {ctx}");
            bool foundReaper = false;

            // ① 遍历 damageSourceInstanceIDs —— 只取在对侧的01528
            foreach (string sourceID in damageSourceInstanceIDs)
            {
                CardInstance sourceCI = FindByInstanceID(bm, sourceID);
                if (sourceCI != null)
                    Debug.Log($"[01528] ①src={sourceID} tid={sourceCI.templateID} attached={sourceCI.isAttached} slot={GetSlotOf(bm,sourceCI.instanceID)} oppSide={(GetSlotOf(bm,sourceCI.instanceID)>=6)!=isAlly}");
                else Debug.Log($"[01528] ①src={sourceID} find=null");
                if (sourceCI != null && sourceCI.templateID == "01528" && !IsSilenced(sourceCI))
                {
                    int reaperSlot = GetSlotOf(bm, sourceCI.instanceID);
                    if ((reaperSlot >= 6) != isAlly)
                    {
                        NetworkPlayer reaperOwner = BoardManager.GetOwnerPlayer(reaperSlot);
                        int add = sourceCI.isAttached ? 2 : 3;
                        reaperOwner?.AddEnergy(add);
                        foundReaper = true;
                        Debug.Log($"[01528] ①命中 slot={reaperSlot} +{add}");
                    }
                }
            }

            // ② 遍历宿主身上的 01528 附着物——宿主与死亡卡对侧
            if (!foundReaper)
            {
                foreach (string sourceID in damageSourceInstanceIDs)
                {
                    int hostSlotID = GetSlotOfByInstanceID(bm, sourceID);
                    if (hostSlotID >= 0 && (hostSlotID >= 6) != isAlly)
                    {
                        foreach (GameObject obj in bm.attachedModels)
                        {
                            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
                            if (c3d?.cardInstance?.templateID == "01528" && c3d.cardInstance.hostSlotID == hostSlotID)
                            {
                                NetworkPlayer hostOwner = BoardManager.GetOwnerPlayer(hostSlotID);
                                hostOwner?.AddEnergy(2);
                                foundReaper = true;
                                Debug.Log($"[01528] ②命中 hostSlot={hostSlotID}");
                            }
                        }
                    }
                }
            }

            // ③ 全板扫描回退——对侧
            if (!foundReaper)
            {
                Debug.Log($"[01528] ③全板扫描");
                for (int i = 0; i < 12; i++)
                {
                    if ((i >= 6) == isAlly) continue; // 同侧跳过
                    var s = bm.GetSlot(i);
                    var sci = s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                    if (sci != null && sci.templateID == "01528" && !IsSilenced(sci))
                    {
                        NetworkPlayer reaperOwner = BoardManager.GetOwnerPlayer(sci.isAttached ? sci.hostSlotID : i);
                        int add = sci.isAttached ? 2 : 3;
                        reaperOwner?.AddEnergy(add);
                        foundReaper = true;
                        Debug.Log($"[01528] ③命中独立 slot={i} attached={sci.isAttached} +{add}");
                        break;
                    }
                }
                if (!foundReaper)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        if ((i >= 6) == isAlly) continue;
                        foreach (GameObject obj in bm.attachedModels)
                        {
                            var aci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                            if (aci?.templateID == "01528" && aci.hostSlotID == i && !IsSilenced(aci))
                            {
                                NetworkPlayer hostOwner = BoardManager.GetOwnerPlayer(i);
                                hostOwner?.AddEnergy(2);
                                foundReaper = true;
                                Debug.Log($"[01528] ③命中附着 hostSlot={i}");
                                break;
                            }
                        }
                        if (foundReaper) break;
                    }
                }
            }

            if (!foundReaper) Debug.Log($"[01528] 未找到——不加能量");
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
        // 不限定 isAlly——01513 可在任一方，IsOnSameSide 确保同侧触发
        if (dyingCI != null && dyingCI.templateID != "03004")
        {
            bool hasEnemySource = dyingCI.enemyDamageSourceIDs.Count > 0;
            // 纯客户端：回退到 damageSourceInstanceIDs 判断敌方来源
            if (!hasEnemySource)
            {
                foreach (string srcID in damageSourceInstanceIDs)
                {
                    int srcSlot = GetSlotOfByInstanceID(bm, srcID);
                    if (srcSlot >= 0 && BoardManager.IsAllySide(srcSlot) != BoardManager.IsAllySide(slotID))
                    {
                        hasEnemySource = true;
                        break;
                    }
                }
            }
            Debug.Log($"[01513-DEBUG] hasEnemySource={hasEnemySource}");
            if (hasEnemySource)
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