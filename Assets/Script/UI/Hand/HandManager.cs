using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Mirror;

public class HandManager : MonoBehaviour
{
    [Header("弧形布局")]
    public float radius = 500f;
    public float totalArcAngle = 30f;
    public float maxWidth = 480f;
    public float cardWidth = 83.333f;
    public float maxOverlapRatio = 0.92f;
    public float hoverOffsetY = 30f;
    public float hoverScale = 1.15f;
    public float pushRatio = 0.7f;
    public float animationSpeed = 15f;

    private List<CardView> handCards = new List<CardView>();
    private CardView draggingCard;
    private int draggingIndex = -1;

    void Start() { }

    public void RegisterCard(CardView cv)
    {
        if (!handCards.Contains(cv))
            handCards.Add(cv);
        RefreshLayout(true);
    }

    public void RemoveCard(CardView cv)
    {
        if (cv == null) return;

        string removedTemplateID = "";
        CardInstance ciRemove = cv.GetComponent<CardInstance>();
        if (ciRemove != null) removedTemplateID = ciRemove.templateID;

        if (handCards.Contains(cv))
            handCards.Remove(cv);
        if (draggingCard == cv)
            draggingCard = null;
        handCards.RemoveAll(c => c == null);
        if (cv.gameObject != null)
        {
            Destroy(cv.gameObject);
        }

        // 手牌为0时强制刷新按钮交互
        if (handCards.Count == 0)
        {
            EndTurnButton endBtn = FindObjectOfType<EndTurnButton>();
            if (endBtn != null)
            {
                CanvasGroup endCG = endBtn.GetComponent<CanvasGroup>();
                if (endCG == null) endCG = endBtn.gameObject.AddComponent<CanvasGroup>();
                endCG.interactable = true;
                endCG.blocksRaycasts = true;
            }
            DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
            if (drawUI != null)
            {
                CanvasGroup drawCG = drawUI.GetComponent<CanvasGroup>();
                if (drawCG == null) drawCG = drawUI.gameObject.AddComponent<CanvasGroup>();
                drawCG.interactable = true;
                drawCG.blocksRaycasts = true;
            }
        }

        // Sync to remote client after spell/counter cast is fully processed
        if (NetworkClient.isConnected && !string.IsNullOrEmpty(removedTemplateID))
        {
            NetworkPlayer.Local?.CmdPlayCard(removedTemplateID, -1);
            BoardSyncManager.Instance?.SyncHostBoard();
        }

        RefreshLayout(true);
    }
    public void HideOtherCards(GameObject dragging)
    {
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            if (handCards[i] == null)
            {
                handCards.RemoveAt(i);
                continue;
            }
            if (handCards[i].gameObject != dragging)
                handCards[i].gameObject.SetActive(false);
        }
    }

    public void ShowAllCards()
    {
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            if (handCards[i] == null)
            {
                handCards.RemoveAt(i);
                continue;
            }
            handCards[i].gameObject.SetActive(true);
        }
    }

    public bool IsPlayArea(Vector2 screenPos)
    {
        return screenPos.y > Screen.height * 0.6f;
    }

    public void OnDragStart(CardView cv)
    {
        draggingCard = cv;
        draggingIndex = handCards.IndexOf(cv);
    }

    public void OnDragUpdate(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, screenPos, null, out Vector2 local);
        draggingIndex = GetInsertIndex(local.x);
        RefreshLayout(false);
    }

    public void OnDragEnd(Vector2 screenPos)
    {
        if (screenPos.y > Screen.height * 0.6f)
            RemoveCard(draggingCard);
        else
        {
            if (draggingCard != null && handCards.Count > 0)
            {
                if (draggingIndex < 0) draggingIndex = handCards.Count;
                draggingIndex = Mathf.Clamp(draggingIndex, 0, handCards.Count - 1);
                handCards.Remove(draggingCard);
                handCards.Insert(draggingIndex, draggingCard);
            }
            draggingCard = null;
            RefreshLayout(true);
        }
    }

    public void RefreshLayout(bool instant)
    {
        if (handCards.Count == 0)
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.interactable = true;
            return;
        }
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            if (handCards[i] == null || handCards[i].gameObject == null)
                handCards.RemoveAt(i);
        }

        int count = handCards.Count;
        if (count == 0)
{
    CanvasGroup cg = GetComponent<CanvasGroup>();
    if (cg != null) cg.interactable = true;
    return;
}

        float overlap = Mathf.Lerp(0f, maxOverlapRatio, (float)(count - 1) / 19f);
        float step = cardWidth * (1f - overlap);
        float totalW = step * (count - 1) + cardWidth;

        if (totalW > maxWidth && count > 1)
        {
            step = (maxWidth - cardWidth) / (count - 1);
            totalW = maxWidth;
        }

        float startX = -totalW / 2f + cardWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            CardView cv = handCards[i];
            float x = startX + i * step;

            if (draggingCard != null && draggingIndex >= 0 && i >= draggingIndex && cv != draggingCard)
                x += step * pushRatio;

            float normalizedX = x / (maxWidth / 2f);
            float arcY = -Mathf.Abs(normalizedX) * radius * 0.02f;
            Vector3 target = new Vector3(x, arcY, 0);
            float angle = -normalizedX * totalArcAngle * 0.5f;
            Quaternion targetRot = Quaternion.Euler(0, 0, angle);

            cv.targetPos = target;
            cv.targetRotation = targetRot;

            if (instant)
            {
                cv.rectTransform.localPosition = target;
                cv.rectTransform.localRotation = targetRot;
                cv.rectTransform.localScale = Vector3.one;
            }
        }

        for (int i = 0; i < count; i++)
            handCards[i].transform.SetSiblingIndex(i);
    }

    int GetInsertIndex(float localX)
    {
        for (int i = 0; i < handCards.Count; i++)
            if (handCards[i] != draggingCard && localX < handCards[i].targetPos.x + cardWidth / 2f)
                return i;
        return handCards.Count;
    }

    // HandManager.PlaceCardToSlot 完整方法
    public void PlaceCardToSlot(BoardSlot slot, GameObject cardObject)
    {
        Debug.Log($"PlaceCardToSlot: cardObject={cardObject?.name}, active={cardObject?.activeSelf}");
        CardInstance sourceInstance = cardObject.GetComponent<CardInstance>();
        if (sourceInstance == null) return;

        CardData template = CardDatabase.Instance?.GetTemplate(sourceInstance.templateID);
        if (template?.prefab3D == null) return;
        if (slot == null && !sourceInstance.canAttach) return;

        // ========== 附着牌打出处理 ==========
        if (sourceInstance.canAttach)
        {
            bool hasAllyTarget = false;
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                for (int i = 6; i <= 11; i++)
                {
                    if (bm.GetSlot(i)?.currentCard3D != null) { hasAllyTarget = true; break; }
                }
            }
            Debug.Log($"附着牌打出检查: baseHealth={template.baseHealth}, hasAllyTarget={hasAllyTarget}");
            if (template.baseHealth == 0 && !hasAllyTarget)
            {
                Debug.Log("附着牌生命值为0且场上没有己方召唤物，无法打出");
                NetworkPlayer.Local.AddEnergy(sourceInstance.currentCost);
                CardView cvFail = cardObject.GetComponent<CardView>();
                if (cvFail != null) { handCards.Remove(cvFail); Destroy(cardObject); RefreshLayout(true); }
                return;
            }

            GameObject cardObj = cardObject;
            bool canBeIndependent = sourceInstance.baseHealth > 0;

            BoardSlot.StartAttachSelect(canBeIndependent, (selectedSlot) =>
            {
                if (selectedSlot.hasCard)
                {
                    CardInstance cardInst = cardObj.GetComponent<CardInstance>();
                    if (IsBoardFull() && cardInst != null && cardInst.canAttach && canBeIndependent)
                    {
                        BoardSlot.isReplaceMode = true;
                        ReplaceOrAttachModal.Instance.Show(
                            onReplace: () =>
                            {
                                GameObject oldCard = selectedSlot.currentCard3D;
                                PlaceIndependentCard(selectedSlot, sourceInstance, template, cardObj);
                                if (oldCard != null)
                                {
                                    Card3DInstance oldInst = oldCard.GetComponent<Card3DInstance>();
                                    if (oldInst?.cardInstance != null)
                                    {
                                        oldInst.cardInstance.isActiveExit = false;
                                        oldInst.cardInstance.hasRevenge = false;
                                        if (oldInst.cardInstance.templateID == "01106") NetworkPlayer.Local.AddEnergy(1);
                                    }
                                    BoardManager bm2 = FindObjectOfType<BoardManager>();
                                    if (bm2 != null)
                                        for (int i = bm2.attachedModels.Count - 1; i >= 0; i--)
                                        {
                                            GameObject obj = bm2.attachedModels[i];
                                            if (obj == null) continue;
                                            Card3DInstance c3d = obj.GetComponent<Card3DInstance>();
                                            if (c3d?.cardInstance != null && c3d.cardInstance.hostSlotID == selectedSlot.slotID)
                                            { bm2.attachedModels.RemoveAt(i); Destroy(obj); }
                                        }
                                    Destroy(oldCard);
                                }
                                BoardSlot.isReplaceMode = false;
                                BoardSlot.CleanupAttachSelect();
                                CleanupAfterSelection();
                            },
                            onAttach: () =>
                            {
                                PlaceAttachedCard(slot, sourceInstance, template, selectedSlot, cardObj);
                                BoardSlot.isReplaceMode = false;
                                BoardSlot.CleanupAttachSelect();
                                if (template.hasOnEnter)
                                {
                                    CardInstance hostInst = selectedSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                                    selectedSlot.StartOnEnterEffect(template, hostInst ?? sourceInstance);
                                }
                                CardView cvAttach = cardObj.GetComponent<CardView>();
                                if (cvAttach != null) RemoveCard(cvAttach);
                                else { handCards.RemoveAll(c => c == null); RefreshLayout(true); }
                                CleanupAfterSelection();
                            }
                        );
                        return;
                    }
                }

                if (selectedSlot.hasCard)
                {
                    PlaceAttachedCard(slot, sourceInstance, template, selectedSlot, cardObj);
                    BoardSlot.CleanupAttachSelect();
                    if (template.hasOnEnter)
                    {
                        CardInstance hostInst = selectedSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                        selectedSlot.StartOnEnterEffect(template, hostInst ?? sourceInstance);
                    }
                    CardView cvAttach2 = cardObj.GetComponent<CardView>();
                    if (cvAttach2 != null) RemoveCard(cvAttach2);
                    else { handCards.RemoveAll(c => c == null); RefreshLayout(true); }
                }
                else
                {
                    PlaceIndependentCard(selectedSlot, sourceInstance, template, cardObj);
                    BoardSlot.CleanupAttachSelect();
                    if (template.hasOnEnter)
                    {
                        CardInstance indInst = selectedSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                        selectedSlot.StartOnEnterEffect(template, indInst ?? sourceInstance);
                    }
                    CardView cvInd = cardObj.GetComponent<CardView>();
                    if (cvInd != null) RemoveCard(cvInd);
                    else { handCards.RemoveAll(c => c == null); RefreshLayout(true); }
                }
            });
            return;
        }

        Vector3 worldPos = GetSlotWorldPosition(slot.slotID);
        GameObject model = Instantiate(template.prefab3D, worldPos, Quaternion.Euler(0, 180, 0));
        model.name = sourceInstance.instanceID;
        model.transform.localScale = template.prefab3D.transform.localScale;
        Card3DInstance instance3D = model.GetComponent<Card3DInstance>();
        if (instance3D != null)
        {
            CardInstance cardInst = model.AddComponent<CardInstance>();
            cardInst.CopyFrom(sourceInstance);
            cardInst.handledReturnToHand = false;
            instance3D.cardInstance = cardInst;
        }

        slot.SetCard(model);
        if (instance3D != null) instance3D.UpdateValues();
        // 阴阳独立打出检查
        if (sourceInstance.isXValue && sourceInstance.templateID == "03012")
        {
            bool hasEnemyMinion = false;
            BoardManager bmCheck = FindObjectOfType<BoardManager>();
            if (bmCheck != null)
            {
                for (int i = 0; i <= 5; i++)
                {
                    if (bmCheck.GetSlot(i)?.currentCard3D != null)
                    {
                        hasEnemyMinion = true;
                        break;
                    }
                }
            }
            if (!hasEnemyMinion)
            {
                Debug.Log("对方场上没有召唤物，阴阳无法打出");
                NetworkPlayer.Local.AddEnergy(sourceInstance.currentCost);
                Destroy(model);
                slot.SetCard(null);
                CardData templateReturn = CardDatabase.Instance?.GetTemplate(sourceInstance.templateID);
                if (templateReturn != null)
                    NetworkPlayer.Local.AddCardToHandFromInstance(templateReturn, sourceInstance);
                CardView cvFail = cardObject.GetComponent<CardView>();
                if (cvFail != null) RemoveCard(cvFail);
                return;
            }
        }
        // ===== 阴/阳合成检测 + 召唤限制 =====
        if (sourceInstance.isXValue && (sourceInstance.templateID == "01306" || sourceInstance.templateID == "01307"))
        {
           
            string otherID = sourceInstance.templateID == "01306" ? "01307" : "01306";
            BoardManager bmMerge = FindObjectOfType<BoardManager>();
            BoardSlot otherSlot = null;
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot s = bmMerge?.GetSlot(i);
                CardInstance ci = s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == otherID)
                {
                    otherSlot = s;
                    break;
                }
            }
    // HandManager.PlaceCardToSlot 完整方法
            if (otherSlot != null)
            {
                // 转移附着物到后进场的槽位
                TransferAttachments(otherSlot, slot);

                // 销毁阴和阳
                Destroy(otherSlot.currentCard3D);
                otherSlot.SetCard(null);
                Destroy(model);
                slot.SetCard(null);

                CardData yinYangData = CardDatabase.Instance?.GetTemplate("03012");
                if (yinYangData?.prefab3D != null)
                {
                    Vector3 mergePos = GetSlotWorldPosition(slot.slotID);
                    GameObject mergeModel = Instantiate(yinYangData.prefab3D, mergePos, Quaternion.Euler(0, 180, 0));
                    mergeModel.name = "03012_merged";
                    Card3DInstance mergeInst = mergeModel.GetComponent<Card3DInstance>();
                    if (mergeInst != null)
                    {
                        CardInstance mergeCard = mergeModel.AddComponent<CardInstance>();
                        mergeCard.templateID = "03012";
                        mergeCard.instanceID = "03012_merged";
                        mergeCard.isXValue = true;
                        mergeCard.xAttackReadsHighest = true;
                        mergeCard.xHealthReadsHighest = true;
                        mergeCard.currentCost = yinYangData.baseCost;
                        mergeCard.currentTier = yinYangData.baseTier;
                        mergeCard.summonType = SummonType.Special;
                        mergeCard.hasFirstStrike = true;
                        mergeCard.isYinYang = true;
                        mergeInst.cardInstance = mergeCard;
                        mergeInst.UpdateValues();
                        mergeCard.xInitialHealth = mergeCard.currentHealth;
                    }
                    slot.SetCard(mergeModel);
                    UpdateXValues(mergeInst.cardInstance);
                }
                CardView cvMerge = cardObject.GetComponent<CardView>();
                if (cvMerge != null) RemoveCard(cvMerge);
                return;
            }

            UpdateXValues(sourceInstance);
        }

        // ===== 杂耍大师强制同步 =====
        if (sourceInstance != null && sourceInstance.templateID == "01135")
        {
            sourceInstance.hasDiscard = template.hasDiscard;
        }
        if (sourceInstance.isXValue && instance3D?.cardInstance != null)
            UpdateXValues(instance3D.cardInstance);
        // 删除手牌
        CardView cv = cardObject?.GetComponent<CardView>();
        if (cv != null)
        {
            handCards.Remove(cv);
            Destroy(cardObject);
            handCards.RemoveAll(c => c == null);
            RefreshLayout(true);
        }
        else
        {
            handCards.RemoveAll(c => c == null);
            RefreshLayout(true);
        }
        ProcessAuras(slot, sourceInstance);

       
    }
   
    void TransferAttachments(BoardSlot oldSlot, BoardSlot newSlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        foreach (GameObject obj in bm.attachedModels)
        {
            CardInstance ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached && ci.hostSlotID == oldSlot.slotID)
            {
                ci.hostSlotID = newSlot.slotID;
            }
        }

        BoardManager.SyncAttachedModels(newSlot);
    }
    // 检测缄默神官是否在场
    private bool IsSuppressorOnField()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "03501")
                return true;
        }
        return false;
    }

    // BoardSlot.HandleDeath 完整方法

    void ApplySageAura(CardInstance card, int slotID)
    {
        if (card == null || slotID < 6 || slotID > 11 || card.summonType != SummonType.Hero)
            return;
        if (card.buffedBySage)
            return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        bool sageOnField = false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance inst = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (inst != null && inst.templateID == "03503")
            {
                sageOnField = true;
                break;
            }
        }
        if (!sageOnField) return;

        if (!card.cannotHealOrGainMaxHP)
        {
            card.currentHealth += 2;
            card.currentMaxHealth += 2;
        }
        card.currentAttack += 1;
        card.buffedBySage = true;
        BoardSlot heroSlot = bm.GetSlot(slotID);
        if (heroSlot?.currentCard3D != null)
        {
            Card3DInstance hero3D = heroSlot.currentCard3D.GetComponent<Card3DInstance>();
            hero3D?.UpdateValues();
        }

        CardDisplay2D display2D = card.GetComponent<CardDisplay2D>();
        if (display2D != null) display2D.Refresh();
    }

    Card3DInstance FindCard3DBySlot(int slotID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardSlot slot = bm?.GetSlot(slotID);
        if (slot?.currentCard3D == null) return null;
        return slot.currentCard3D.GetComponent<Card3DInstance>();
    }

    public Vector3 GetSlotWorldPosition(int slotID)
    {
        float x = 0f, y = 0f, z = -5.5f;
        switch (slotID)
        {
            case 0: x = 3f; y = 2.3f; break;
            case 1: x = 0f; y = 2.3f; break;
            case 2: x = -3f; y = 2.3f; break;
            case 3: x = 3f; y = 4.6f; break;
            case 4: x = 0f; y = 4.6f; break;
            case 5: x = -3f; y = 4.6f; break;
            case 6: x = 3f; y = -0.27f; break;
            case 7: x = 0f; y = -0.27f; break;
            case 8: x = -3f; y = -0.27f; break;
            case 9: x = 3f; y = -2.57f; break;
            case 10: x = 0f; y = -2.57f; break;
            case 11: x = -3f; y = -2.57f; break;
        }
        return new Vector3(x, y, z);
    }

    public void HideAllCards()
    {
        foreach (CardView cv in handCards)
            if (cv != null) cv.gameObject.SetActive(false);
    }

    public void SetHandAreaRaycast(bool enabled)
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.interactable = enabled;
        cg.blocksRaycasts = enabled;
    }
    private void PlaceIndependentCard(BoardSlot slot, CardInstance sourceInstance, CardData template, GameObject cardObject)
    {
        Vector3 worldPos = GetSlotWorldPosition(slot.slotID);
        GameObject model = Instantiate(template.prefab3D, worldPos, Quaternion.Euler(0, 180, 0));
        model.name = sourceInstance.instanceID;
        Card3DInstance instance3D = model.GetComponent<Card3DInstance>();

        if (instance3D != null)
        {
            CardInstance cardInst = model.AddComponent<CardInstance>();
            CopyCardInstance(cardInst, sourceInstance);
            instance3D.cardInstance = cardInst;
        }

        slot.SetCard(model);
        if (instance3D != null) instance3D.UpdateValues();

       
        ProcessAuras(slot, sourceInstance);

     
        if (sourceInstance.isXValue && instance3D?.cardInstance != null)
            UpdateXValues(instance3D.cardInstance);

        // 删除手牌
        CardView cv = cardObject?.GetComponent<CardView>();
        if (cv != null)
        {
            handCards.Remove(cv);
            Destroy(cardObject);
            handCards.RemoveAll(c => c == null);
            RefreshLayout(true);
        }
        else
        {
            handCards.RemoveAll(c => c == null);
            RefreshLayout(true);
        }
       ;
    }
    void CopyCardInstance(CardInstance dest, CardInstance src)
    {
        dest.CopyFrom(src);
    }
    private void PlaceAttachedCard(BoardSlot slot, CardInstance sourceInstance, CardData template, BoardSlot hostSlot, GameObject cardObject)
    {
        // 计算附着偏移
        int attachOrder = 0;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            foreach (GameObject obj in bm.attachedModels)
            {
                CardInstance ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.hostSlotID == hostSlot.slotID)
                    attachOrder++;
            }
        }

        Vector3 hostPos = hostSlot.currentCard3D.transform.position;
        Vector3 attachPos = new Vector3(hostPos.x - 0.5f - attachOrder * 0.5f, hostPos.y, hostPos.z + 0.1f + attachOrder * 0.1f);

        GameObject model = Instantiate(template.prefab3D, attachPos, Quaternion.Euler(0, 180, 0));
        model.name = sourceInstance.instanceID + "_attach_" + attachOrder;
        Card3DInstance instance3D = model.GetComponent<Card3DInstance>();

        if (instance3D != null)
        {
            CardInstance cardInst = model.AddComponent<CardInstance>();
            cardInst.CopyFrom(sourceInstance);
            cardInst.handledReturnToHand = false;
            cardInst.isAttached = true;
            cardInst.hostSlotID = hostSlot.slotID;
            cardInst.attachOrder = attachOrder;
            instance3D.cardInstance = cardInst;
        }
        // 隐藏附着物的所有文字
        CardDisplay3D display = model.GetComponent<CardDisplay3D>();
        if (display != null)
        {
            if (display.nameText != null) display.nameText.gameObject.SetActive(false);
            if (display.prefixText != null) display.prefixText.gameObject.SetActive(false);
            if (display.attackText != null) display.attackText.gameObject.SetActive(false);
            if (display.healthText != null) display.healthText.gameObject.SetActive(false);
            if (display.costText != null) display.costText.gameObject.SetActive(false);
        }
        // 解析附着特性文本，给宿主加增益
        if (!string.IsNullOrEmpty(template.traits))
        {
            // 凝聚体：+2+1
            if (template.templateID == "01126")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 2;
                        hostCard.currentMaxHealth += 2;
                    }
                    hostCard.currentAttack += 1;
                }
            }
            // 超数故障：附着时+2+0
            else if (template.templateID == "01127")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 1;
                        hostCard.currentMaxHealth += 1;
                    }
                    hostCard.currentAttack += 1;
                }
            }
            else if (template.templateID == "01129")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    hostCard.currentHealth += 1;
                    hostCard.currentMaxHealth += 1;
                    if (hostCard.prefixes.Contains("灵能"))
                    {
                        hostCard.currentHealth += 3;
                        hostCard.currentMaxHealth += 3;
                    }
                    hostCard._nourisherHost = true;
                    hostCard._nourisherInstanceID = sourceInstance.instanceID;
                }
                sourceInstance._nourisherAttached = true;
            }
            else if (template.templateID == "01131")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    hostCard.currentAttack += 1;
                }
            }
            // 脆弱精灵：+1+1，阶位+1
            else if (template.templateID == "01112")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 1;
                        hostCard.currentMaxHealth += 1;
                    }
                    hostCard.currentAttack += 1;
                    hostCard.currentTier += 1;
                }
            }
            // 超数故障：附着时+2+0
            else if (template.templateID == "03001")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    hostCard.currentAttack += 0;
                }
            }
            // 认同指令：附着时添加机械前缀，每另有机械单位+1+1（单次最多+2+2，不含宿主）
            else if (template.templateID == "01119")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    // 添加机械前缀（不重复）
                    if (!hostCard.prefixes.Contains("灵能"))
                    {
                        if (string.IsNullOrEmpty(hostCard.prefixes) || hostCard.prefixes == "无")
                            hostCard.prefixes = "灵能";
                        else
                            hostCard.prefixes += " 灵能";
                    }

                    // 统计其他机械单位数量（不含宿主自己）
                    int mechCount = 0;
                    BoardManager bm2 = FindObjectOfType<BoardManager>();
                    if (bm2 != null)
                    {
                        for (int i = 6; i <= 11; i++)
                        {
                            BoardSlot mechSlot = bm2.GetSlot(i);
                            if (mechSlot?.currentCard3D == null) continue;
                            if (mechSlot == hostSlot) continue;
                            CardInstance ci = mechSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ci != null && ci.prefixes.Contains("机械"))
                                mechCount++;
                        }
                    }
                    int bonus = Mathf.Min(mechCount, 2);
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += bonus;
                        hostCard.currentMaxHealth += bonus;
                    }
                    hostCard.currentAttack += bonus;
                }
            }
            else if (template.templateID == "01327")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    hostCard.currentHealth += 4;
                    hostCard.currentMaxHealth += 4;
                    hostCard.currentAttack += 3;
                }
            }
            // 超数故障：附着时+2+0
            else if (template.templateID == "01333")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {

                        hostCard.currentHealth += 4;
                        hostCard.currentMaxHealth += 4;
                    }
                    hostCard.currentAttack += 3;
                }
            }
            else if (template.templateID == "01334")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (hostCard.prefixes.Contains("灵能"))
                    {
                        // 己方全体+2+1
                        BoardManager bmCluster = FindObjectOfType<BoardManager>();
                        for (int i = 6; i <= 11; i++)
                        {
                            BoardSlot clusterSlot = bmCluster?.GetSlot(i);
                            if (clusterSlot?.currentCard3D != null)
                            {
                                CardInstance ci = clusterSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                if (ci != null)
                                {
                                    if (!hostCard.cannotHealOrGainMaxHP)
                                    {
                                        ci.currentHealth += 2;
                                        ci.currentMaxHealth += 2;
                                    }
                                    ci.currentAttack += 1;
                                    clusterSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                }
                            }
                        }
                    }
                    else
                    {
                        // 仅附着者+2+1if
                        if (!hostCard.cannotHealOrGainMaxHP)
                        {
                            hostCard.currentHealth += 2;
                            hostCard.currentMaxHealth += 2;
                        }
                        hostCard.currentAttack += 1;
                    }
                }
            }
            else if (template.templateID == "01335")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 2;
                        hostCard.currentMaxHealth += 2;
                    }
                    hostCard.currentAttack += 2;
                }
            }
            else if (template.templateID == "01336")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (hostCard.prefixes.Contains("灵能"))
                    {
                        hostCard.GrantTrait("先手：对对方前排召唤物造成2伤害，对后排造成1伤害");
                    }
                    else
                    {
                        hostCard.GrantTrait("先手：对对方前排召唤物造成1伤害");
                    }
                    hostCard.hasFirstStrike = true;
                }
            }
            else if (template.templateID == "01510")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 5;
                        hostCard.currentMaxHealth += 5;
                    }
                }
            }
            // 消逝之影：附着时+6+5，+1能量，附加灵能前缀
            else if (template.templateID == "01527")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 6;
                        hostCard.currentMaxHealth += 6;
                    }
                    hostCard.currentAttack += 5;

                    if (!hostCard.prefixes.Contains("灵能"))
                    {
                        if (string.IsNullOrEmpty(hostCard.prefixes) || hostCard.prefixes == "无")
                            hostCard.prefixes = "灵能";
                        else
                            hostCard.prefixes += " 灵能";
                    }
                }
                NetworkPlayer.Local.AddEnergy(1);
            }
            else if (template.templateID == "01528")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 1;
                        hostCard.currentMaxHealth += 1;
                    }
                    hostCard.currentAttack += 3;
                }
            }
            // 超数故障：附着时+2+0
            else if (template.templateID == "01128")
            {
                CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (hostCard != null)
                {
                    if (!hostCard.cannotHealOrGainMaxHP)
                    {
                        hostCard.currentHealth += 2;
                        hostCard.currentMaxHealth += 2;
                    }
                }
            }
            // 统一刷新宿主显示
            hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
        }
        bm.attachedModels.Add(model);

        // 删除手牌
        CardView cv = cardObject?.GetComponent<CardView>();
        if (cv != null)
        {
            handCards.Remove(cv);
            Destroy(cardObject);
            handCards.RemoveAll(c => c == null);
            RefreshLayout(true);
        }
        else
        {
            handCards.RemoveAll(c => c == null);
            RefreshLayout(true);
        }
    }
    private void ProcessAuras(BoardSlot slot, CardInstance sourceInstance)
    {
        // 智者自身进场光环
        if (sourceInstance != null && sourceInstance.templateID == "03503")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot allySlot = bm?.GetSlot(i);
                if (allySlot?.currentCard3D != null)
                {
                    Card3DInstance allyInst = allySlot.currentCard3D.GetComponent<Card3DInstance>();
                    CardInstance allyCard = allyInst?.cardInstance;
                    if (allyCard != null && !allyCard.buffedBySage && allyCard.templateID != "03503" && allyCard.summonType == SummonType.Hero)
                    {
                        if (!allyCard.cannotHealOrGainMaxHP)
                        {
                            allyCard.currentHealth += 2;
                            allyCard.currentMaxHealth += 2;
                        }
                        allyCard.currentAttack += 1;
                        allyCard.buffedBySage = true;
                        allyInst.UpdateValues();
                    }
                }
            }
        }

        // 新英雄进场：如果是渊前缀且皇帝在场，+1+1
        if (sourceInstance != null && sourceInstance.summonType == SummonType.Hero)
        {
            CardInstance placedCI = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (placedCI != null)
                ApplySageAura(placedCI, slot.slotID);
        }
        // 无赖：进场获得护盾（攻击回合开始消失）
        if (sourceInstance != null && sourceInstance.summonType == SummonType.Hero)
        {
            if (IsSuppressorOnField())
            {
                Card3DInstance hero3D = slot.currentCard3D?.GetComponent<Card3DInstance>();
                if (hero3D?.cardInstance != null)
                {
                    hero3D.cardInstance.currentTier += 1;
                    hero3D.UpdateValues();
                }
            }
        }
        // 无赖：进场获得护盾（攻击回合开始消失）
        if (sourceInstance != null && sourceInstance.templateID == "03027")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasValid = false;
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot coreSlot = bm?.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && !ci.prefixes.Contains("灵能"))
                {
                        if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                            ci.prefixes = "灵能";
                    else
                            ci.prefixes += " 灵能";
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
            // 附加灵能前缀：手牌中召唤物
            foreach (GameObject handCard in NetworkPlayer.Local.handCards)
            {
                if (handCard == null) continue;
                CardInstance ci = handCard.GetComponent<CardInstance>();
                if (ci != null)
                {
                    CardData cd = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (cd != null && cd.cardType == CardType.Summon && !ci.prefixes.Contains("灵能"))
                    {
                        if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                            ci.prefixes = "灵能";
                        else
                            ci.prefixes += " 灵能";
                        CardDisplay2D d2d = handCard.GetComponent<CardDisplay2D>();
                        d2d?.Refresh();
                    }
                }
            }
        }
        // 无赖：进场获得护盾（攻击回合开始消失）
        if (sourceInstance != null && sourceInstance.templateID == "01501")
        {
            if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(sourceInstance))
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s?.currentCard3D != null)
                    {
                        CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.prefixes.Contains("Ԩ") && !ci.buffedByEmperor && ci != sourceInstance)
                        {
                            if (!ci.cannotHealOrGainMaxHP)
                            {
                                ci.currentHealth += 1;
                                ci.currentMaxHealth += 1;
                            }
                            ci.currentAttack += 1;
                            ci.buffedByEmperor = true;
                            s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                        }
                    }
                }
            }
        }

        // 新英雄进场：如果是渊前缀且皇帝在场，+1+1
        if (sourceInstance != null && sourceInstance.prefixes.Contains("Ԩ"))
        {
            CardInstance placedCI = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (placedCI != null && !placedCI.buffedByEmperor)
            {
                bool emperorActive = false;
                BoardManager bm = FindObjectOfType<BoardManager>();
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s?.currentCard3D != null)
                    {
                        CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.templateID == "01501" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(ci)))
                        {
                            emperorActive = true;
                            break;
                        }
                    }
                }
                if (emperorActive)
                {
                    if (!placedCI.cannotHealOrGainMaxHP)
                    {
                        placedCI.currentHealth += 1;
                        placedCI.currentMaxHealth += 1;
                    }
                    placedCI.currentAttack += 1;
                    placedCI.buffedByEmperor = true;
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
        }
        // 无赖：进场获得护盾（攻击回合开始消失）
        if (sourceInstance != null && sourceInstance.prefixes.Contains("机械") && sourceInstance.templateID != "01513")
        {
            CardInstance reborn = FindRebornOnField();
            if (reborn != null && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(reborn)))
            {
                Debug.Log($"复生造物增幅前: health={reborn.currentHealth}, maxHealth={reborn.currentMaxHealth}");
                reborn.currentHealth += 1;
                reborn.currentMaxHealth += 1;
                Debug.Log($"复生造物增幅前: health={reborn.currentHealth}, maxHealth={reborn.currentMaxHealth}");
                UpdateRebornDisplay(reborn);
            }
        }
        // 无赖：进场获得护盾（攻击回合开始消失）
        if (sourceInstance != null && sourceInstance.templateID == "01309")
        {
            CardInstance placedCI = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (placedCI != null)
            {
                placedCI.GrantShield(false, true, false);
            }
        }
        // X数值同步
        BoardManager bmSync = FindObjectOfType<BoardManager>();
        if (bmSync != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slotSync = bmSync.GetSlot(i);
                if (slotSync?.currentCard3D == null) continue;
                CardInstance ciSync = slotSync.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ciSync != null && ciSync.isXValue)
                    UpdateXValues(ciSync);
            }
        }
    }
    private void CleanupAfterSelection()
    {
        BoardSlot.CleanupAttachSelect();
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        SetHandAreaRaycast(true);
        ShowAllCards();
        BoardSyncManager.Instance?.SyncHostBoard();
    }
    private bool IsBoardFull()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot == null) continue;
            if (!slot.isBlocked && !slot.hasCard)
                return false;
        }
        return true;
    }
    public void UpdateXValues(CardInstance ci)
    {
        if (ci == null || !ci.isXValue) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        bool isYinYang = ci.templateID == "03012";
        int searchStart = isYinYang ? 0 : 0;
        int searchEnd = isYinYang ? 12 : 6;

        int highestAttack = 0;
        int lowestAttack = int.MaxValue;
        int highestHealth = 0;
        int lowestHealth = int.MaxValue;
        bool anyMinion = false;

        BoardSlot mySlot = FindMySlot(ci);

        for (int i = searchStart; i < searchEnd; i++)
        {
            if (mySlot != null && i == mySlot.slotID) continue;
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance == null) continue;

            anyMinion = true;
            int atk = c3d.cardInstance.currentAttack;
            int hp = c3d.cardInstance.currentHealth;

            if (atk > highestAttack) highestAttack = atk;
            if (atk < lowestAttack) lowestAttack = atk;
            if (hp > highestHealth) highestHealth = hp;
            if (hp < lowestHealth) lowestHealth = hp;
        }

        if (!anyMinion)
        {
            ci.currentAttack = 0;
            ci.currentHealth = 0;
            ci.currentMaxHealth = 0;
            ci.xInitialHealth = 0;
            if (mySlot != null)
            {
                mySlot.HandleDeath(mySlot.currentCard3D);
            }
            return;
        }

        if (ci.xAttackReadsHighest)
            ci.currentAttack = highestAttack;
        else
            ci.currentAttack = lowestAttack == int.MaxValue ? 0 : lowestAttack;

        if (ci.xHealthReadsHighest)
        {
            ci.currentHealth = highestHealth;
            ci.currentMaxHealth = highestHealth;
        }
        else
        {
            ci.currentHealth = lowestHealth == int.MaxValue ? 0 : lowestHealth;
            ci.currentMaxHealth = lowestHealth == int.MaxValue ? 0 : lowestHealth;
        }
        ci.xInitialHealth = ci.currentHealth;

        if (ci.gameObject != null)
        {
            Card3DInstance c3d = ci.gameObject.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
        }
    }
    private BoardSlot FindMySlot(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        BoardSlot[] slots = bm.GetAllSlots();
        for (int i = 0; i < 12; i++)
        {
            if (slots[i]?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return slots[i];
        }
        return null;
    }
    /// <summary>
    /// 临时显示指定条件的手牌，隐藏其余，点击一张后回调
    /// </summary>
    public void ShowFilteredHand(System.Predicate<CardInstance> filter, System.Action<CardInstance> onSelected, System.Action onCancel)
    {
        handCards.RemoveAll(c => c == null);

        List<GameObject> validCards = new List<GameObject>();

        foreach (CardView cv in handCards)
        {
            if (cv == null || cv.gameObject == null) continue;
            CardInstance ci = cv.GetComponent<CardInstance>();
            if (ci != null && filter(ci))
            {
                validCards.Add(cv.gameObject);
                cv.gameObject.SetActive(true);
            }
            else
            {
                cv.gameObject.SetActive(false);
            }
        }

        ArrangeTempHand(validCards);

        foreach (GameObject card in validCards)
        {
            CardView cv = card.GetComponent<CardView>();
            cv.OnCardClicked = (ci) =>
            {
                onSelected?.Invoke(ci);
                ShowAllCards();
                RefreshLayout(true);
            };
        }

        StartCoroutine(WaitForCancel(onCancel));
    }

    void ArrangeTempHand(List<GameObject> cards)
    {
        int count = cards.Count;
        float startX = -((count - 1) * (cardWidth + 10f)) / 2f;
        for (int i = 0; i < count; i++)
        {
            RectTransform rt = cards[i].GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(startX + i * (cardWidth + 10f), 0);
        }
    }

    IEnumerator WaitForCancel(System.Action onCancel)
    {
        yield return new WaitForSeconds(0.5f);
        // 检测ESC或右键取消
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                onCancel?.Invoke();
                ShowAllCards();
                RefreshLayout(true);
                yield break;
            }
            yield return null;
        }
    }
    public IEnumerator ReformFormationEffect(CardDrag cardDrag)
    {
        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(false);
        }
        SetHandAreaRaycast(false);
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
        Card3DHover.allowDiscard = false;

        BoardSlot firstSlot = null;
        bool confirmed = false;
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);

        BoardSlot.onTargetSelected = (selected) =>
        {
            if (firstSlot == null) { firstSlot = selected; }
            else
            {
                BoardSlot secondSlot = selected;
                GameObject c1 = firstSlot.currentCard3D;
                GameObject c2 = secondSlot.currentCard3D;
                Vector3 p1 = GetSlotWorldPosition(firstSlot.slotID);
                Vector3 p2 = GetSlotWorldPosition(secondSlot.slotID);
                firstSlot.SetCard(null); secondSlot.SetCard(null);
                if (c2 != null)
                {
                    if (!firstSlot.CanPlaceCard(c2.GetComponent<Card3DInstance>()?.cardInstance)) { firstSlot = null; return; }
                    c2.transform.position = p1;
                    firstSlot.SetCard(c2);
                }
                if (c1 != null)
                {
                    if (!secondSlot.CanPlaceCard(c1.GetComponent<Card3DInstance>()?.cardInstance)) { firstSlot = null; return; }
                    c1.transform.position = p2;
                    secondSlot.SetCard(c1);
                }
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                    foreach (GameObject obj in bm.attachedModels)
                    {
                        CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isAttached)
                        {
                            if (ci.hostSlotID == firstSlot.slotID) ci.hostSlotID = secondSlot.slotID;
                            else if (ci.hostSlotID == secondSlot.slotID) ci.hostSlotID = firstSlot.slotID;
                        }
                    }
                BoardManager.SyncAttachedModels(firstSlot);
                BoardManager.SyncAttachedModels(secondSlot);
                firstSlot = null;
            }
        };

        yield return new WaitUntil(() => confirmed);
        BoardSlot.isStrengtheningSlot = false;
        SelectionManager.Instance.ForceEndAll();
        ConfirmSelectionButton.Instance.Hide();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(true);
        }
        RefreshLayout(true);
        CardDrag.CleanupSpellResources();
    }
    public IEnumerator HandCleanseEffect()
    {
        NetworkPlayer player = NetworkPlayer.Local;
        player.handCards.RemoveAll(c => c == null);
        if (player.handCards.Count == 0) { CardDrag.CleanupSpellResources(); yield break; }

        SelectionManager.Instance.BeginOpenSelection(TargetType.None, null);
        int maxKeep = 4;
        List<GameObject> kept = new List<GameObject>();
        Dictionary<GameObject, Vector3> orig = new Dictionary<GameObject, Vector3>();
        var valid = ConfirmQueueManager.FilterHandCards(ci => true);
        foreach (GameObject card in valid)
        {
            CardView cv = card.GetComponent<CardView>();
            if (cv != null) orig[card] = cv.targetPos;
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h == null) h = card.AddComponent<CardClickHandler>();
            h.onClick = () =>
            {
                if (kept.Contains(card)) { kept.Remove(card); if (cv != null && orig.ContainsKey(card)) cv.targetPos = orig[card]; }
                else if (kept.Count < maxKeep) { kept.Add(card); if (cv != null && orig.ContainsKey(card)) cv.targetPos = orig[card] + new Vector3(0, 30, 0); }
            };
        }

        bool confirmed = false;
        ConfirmSelectionButton.Instance.transform.SetAsLastSibling();
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);
        yield return new WaitUntil(() => confirmed);

        List<GameObject> toRemove = new List<GameObject>();
        foreach (GameObject card in player.handCards) { if (card != null && !kept.Contains(card)) toRemove.Add(card); }
        int discard = 0;
        foreach (GameObject card in toRemove) { player.handCards.Remove(card); Destroy(card); discard++; }
        foreach (GameObject card in valid) { if (card == null) continue; CardClickHandler h = card.GetComponent<CardClickHandler>(); if (h != null) Destroy(h); }

        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();
        ConfirmSelectionButton.Instance.Hide();
        SelectionManager.Instance.ForceEndAll();

        for (int i = 0; i < discard; i++) player.DrawCard();
        RefreshLayout(true);
        CardDrag.CleanupSpellResources();
    }
    public IEnumerator SummonTwoMinions()
    {
        CardData template = CardDatabase.Instance?.GetTemplate("03004");
        if (template?.prefab3D == null) { CardDrag.CleanupSpellResources(); yield break; }

        for (int round = 0; round < 2; round++)
        {
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            BoardSlot.cardToPlace = null;

            // 创建临时手牌走正常召唤流程
            GameObject temp = new GameObject("TempSpawn");
            CardInstance ti = temp.AddComponent<CardInstance>();
            ti.InitFromTemplate(template, round);
            BoardSlot.cardToPlace = temp;

            // 等玩家点槽位，OnPointerClick 的放置分支会处理
            yield return new WaitWhile(() => BoardSlot.isPlacingCard);

            Destroy(temp);
        }

        CardDrag.CleanupSpellResources();
    }
    public IEnumerator ManyCardsEffect()
    {
        NetworkPlayer player = NetworkPlayer.Local;

        // 1. 抽7张牌
        for (int i = 0; i < 7; i++)
        {
            player.DrawCardWithoutLimit();
        }
        player.handCards.RemoveAll(c => c == null);

        if (player.handCards.Count == 0)
        {
            CardDrag.CleanupSpellResources();
            yield break;
        }

        bool done = false;
        SelectionManager.Instance.BeginOpenSelection(TargetType.None, null);
        int maxDiscard = 4;
        List<GameObject> selectedCards = new List<GameObject>();
        Dictionary<GameObject, Vector3> origPos = new Dictionary<GameObject, Vector3>();

        var validCards = ConfirmQueueManager.FilterHandCards(ci => true);
        foreach (GameObject card in validCards)
        {
            CardView cv = card.GetComponent<CardView>();
            if (cv != null) origPos[card] = cv.targetPos;

            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h == null) h = card.AddComponent<CardClickHandler>();
            h.onClick = () =>
            {
                if (selectedCards.Contains(card))
                {
                    selectedCards.Remove(card);
                    if (cv != null && origPos.ContainsKey(card)) cv.targetPos = origPos[card];
                }
                else if (selectedCards.Count < maxDiscard)
                {
                    selectedCards.Add(card);
                    if (cv != null && origPos.ContainsKey(card)) cv.targetPos = origPos[card] + new Vector3(0, 30, 0);
                }
            };
        }

        bool confirmed = false;
        ConfirmSelectionButton.Instance.transform.SetAsLastSibling();
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);
        yield return new WaitUntil(() => confirmed);

        // 3. 弃掉选中的牌，计算能量
        int energyGain = 0;
        foreach (GameObject card in selectedCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null)
            {
                CardData data = CardDatabase.Instance?.GetTemplate(ci.templateID);
                if (data != null && data.baseCost == 5)
                    energyGain++;
            }
            player.handCards.Remove(card);
            Destroy(card);
        }
        player.AddEnergy(energyGain);

        // 4. 清理
        foreach (GameObject card in validCards)
        {
            if (card == null) continue;
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();
        ConfirmSelectionButton.Instance.Hide();
        SelectionManager.Instance.ForceEndAll();
        RefreshLayout(true);
        CardDrag.CleanupSpellResources();
    }
    public IEnumerator SwapTwoAllies()
    {
        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

        BoardSlot firstSlot = null;
        bool swapDone = false;

        BoardSlot.onTargetSelected = (selected) =>
        {
            if (selected == null) return;

            // 忽略抛置穿透的点击
            if (selected.slotID == Card3DHover.ignoreSlotID)
            {
                Card3DHover.ignoreSlotID = -1;
                return;
            }
            Card3DHover.ignoreSlotID = -1;

            if (firstSlot == null)
            {
                firstSlot = selected;
            }
            else if (selected != firstSlot)
            {
                BoardSlot secondSlot = selected;

                GameObject card1 = firstSlot.currentCard3D;
                GameObject card2 = secondSlot.currentCard3D;

                Vector3 pos1 = GetSlotWorldPosition(firstSlot.slotID);
                Vector3 pos2 = GetSlotWorldPosition(secondSlot.slotID);

                firstSlot.SetCard(null);
                secondSlot.SetCard(null);

                if (card2 != null)
                {
                    if (!firstSlot.CanPlaceCard(card2.GetComponent<Card3DInstance>()?.cardInstance)) { firstSlot = null; return; }
                    card2.transform.position = pos1;
                    firstSlot.SetCard(card2);
                }
                if (card1 != null)
                {
                    if (!secondSlot.CanPlaceCard(card1.GetComponent<Card3DInstance>()?.cardInstance)) { firstSlot = null; return; }
                    card1.transform.position = pos2;
                    secondSlot.SetCard(card1);
                }

                BoardManager bmSwap = FindObjectOfType<BoardManager>();
                if (bmSwap != null)
                {
                    foreach (GameObject obj in bmSwap.attachedModels)
                    {
                        CardInstance ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isAttached)
                        {
                            if (ci.hostSlotID == firstSlot.slotID)
                                ci.hostSlotID = secondSlot.slotID;
                            else if (ci.hostSlotID == secondSlot.slotID)
                                ci.hostSlotID = firstSlot.slotID;
                        }
                    }
                }

                BoardManager.SyncAttachedModels(firstSlot);
                BoardManager.SyncAttachedModels(secondSlot);

                SelectionManager.Instance.ForceEndAll();
                swapDone = true;
            }
        };

        yield return new WaitUntil(() => swapDone);
        BoardSlot.isStrengtheningSlot = false;
    }
    public IEnumerator SpotlightEffect()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasAvailableSlot = false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.isBlocked && !s.hasSpotlight) { hasAvailableSlot = true; break; }
        }
        if (!hasAvailableSlot) { CardDrag.CleanupSpellResources(); yield break; }

        BoardSlot.isStrengtheningSlot = true;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (selectedSlot) =>
        {
            if (selectedSlot != null && !selectedSlot.isBlocked && !selectedSlot.hasSpotlight)
            {
                selectedSlot.hasSpotlight = true;
                selectedSlot.spotlightTierBoost = 2;
                if (selectedSlot.currentCard3D != null)
                {
                    CardInstance ci = selectedSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null)
                    {
                        ci.currentTier += 2;
                        selectedSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
            done = true;
        });

        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(false);
        }
        SetHandAreaRaycast(false);
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
        Card3DHover.allowDiscard = false;

        yield return new WaitUntil(() => done);
        BoardSlot.isStrengtheningSlot = false;
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(true);
        }
        RefreshLayout(true);
        CardDrag.CleanupSpellResources();
    }
    public IEnumerator GreatEvolutionEffect()
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasFieldTarget = false;
        for (int i = 6; i <= 11; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasFieldTarget = true; break; }
        }
        bool hasHandTarget = false;
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null && CardDatabase.Instance?.GetTemplate(card.GetComponent<CardInstance>()?.templateID)?.cardType == CardType.Summon)
            { hasHandTarget = true; break; }
        }
        if (!hasFieldTarget && !hasHandTarget)
        {
            CardDrag.CleanupSpellResources();
            yield break;
        }

        string layerId = SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Spell)
            {
                card.SetActive(false);
                spellCards.Add(card);
            }
        }

        List<GameObject> handSummons = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Summon)
            {
                handSummons.Add(card);
                CardClickHandler handler = card.GetComponent<CardClickHandler>();
                if (handler == null) handler = card.AddComponent<CardClickHandler>();
                handler.onClick = () =>
                {
                    SelectionManager.Instance.EndSelection(layerId);
                    CleanupEvolutionUI(spellCards, handSummons);
                    ApplyEvolutionEffect(card);
                    CardDrag.CleanupSpellResources();
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.EndSelection(layerId);
                CleanupEvolutionUI(spellCards, handSummons);
                ApplyEvolutionEffect(targetSlot.currentCard3D);
                CardDrag.CleanupSpellResources();
            }
        };

        yield return new WaitUntil(() => !SelectionManager.Instance.IsSelecting);
    }
    void CleanupEvolutionUI(List<GameObject> hiddenSpells, List<GameObject> handSummons)
    {
        foreach (GameObject card in hiddenSpells)
            if (card != null) card.SetActive(true);
        foreach (GameObject card in handSummons)
        {
            if (card == null) continue;
            CardClickHandler handler = card.GetComponent<CardClickHandler>();
            if (handler != null) Destroy(handler);
        }
    }

    void ApplyEvolutionEffect(GameObject target)
    {
        if (target == null) return;
        CardInstance ci = target.GetComponent<CardInstance>();
        if (ci == null)
        {
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            if (c3d != null) ci = c3d.cardInstance;
        }
        if (ci != null)
        {
            ci.currentTier += 3;
            ci.baseTier += 3;
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();
            Debug.Log($"伟大进化：{ci.instanceID} 阶位永久+3");
        }
    }

    public IEnumerator SummonCoreEffect()
    {
        CardData template = CardDatabase.Instance?.GetTemplate("03027");
        if (template?.prefab3D == null) { CardDrag.CleanupSpellResources(); yield break; }

        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasEmpty = false;
        for (int i = 6; i <= 11; i++)
            if (bm.GetSlot(i) != null && !bm.GetSlot(i).isBlocked && !bm.GetSlot(i).hasCard) { hasEmpty = true; break; }
        if (!hasEmpty) { CardDrag.CleanupSpellResources(); yield break; }

        HandManager hm = FindObjectOfType<HandManager>();
        BoardSlot.isPlacingCard = true;
        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(false);
        }
        SetHandAreaRaycast(false);
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
        Card3DHover.allowDiscard = false;

        bool placed = false;
        BoardSlot.onTargetSelected = (selectedSlot) =>
        {
            if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.hasCard || selectedSlot.slotID < 6) return;
            GameObject temp = new GameObject("TempCore");
            CardInstance ti = temp.AddComponent<CardInstance>();
            ti.InitFromTemplate(template, 0);
            hm.PlaceCardToSlot(selectedSlot, temp);
            Destroy(temp);
            placed = true;
            SelectionManager.Instance.ForceEndAll();
            BoardSlot.isPlacingCard = false;
            BoardSlot.isStrengtheningSlot = false;

        CardInstance watcher = null;
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && !ci.prefixes.Contains("灵能"))
                {
                        if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                            ci.prefixes = "灵能";
                    else
                            ci.prefixes += " 灵能";
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
            // 附加灵能前缀：手牌中召唤物
            foreach (GameObject handCard in NetworkPlayer.Local.handCards)
            {
                if (handCard == null) continue;
                CardInstance ci = handCard.GetComponent<CardInstance>();
                if (ci != null)
                {
                    CardData cd = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (cd != null && cd.cardType == CardType.Summon && !ci.prefixes.Contains("灵能"))
                    {
                        if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                            ci.prefixes = "灵能";
                        else
                            ci.prefixes += " 灵能";
                        CardDisplay2D d2d = handCard.GetComponent<CardDisplay2D>();
                        d2d?.Refresh();
                    }
                }
            }
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                if (card != null) card.SetActive(true);
            }
            RefreshLayout(true);
            CardDrag.CleanupSpellResources();
        };
        yield return new WaitUntil(() => placed);
    }
    public IEnumerator CollectorEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        if (NetworkPlayer.Local.handCards.Count == 0)
        {
                Debug.Log("对方场上没有召唤物，阴阳无法打出");
            yield break;
        }

        List<CardInstance> displayList = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null) displayList.Add(ci);
        }

        CardDisplayPanel.Instance.multiSelect = true;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(displayList, ci => true, () =>
        {
            confirmed = true;
        }, "召唤");

        yield return new WaitUntil(() => confirmed);

        List<CardInstance> selectedList = CardDisplayPanel.Instance.GetSelectedCards();

        if (selectedList.Count == 0)
        {
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
            foreach (GameObject c in NetworkPlayer.Local.handCards) { if (c != null) c.SetActive(true); }
            SetHandAreaRaycast(true);
            RefreshLayout(true);
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
            Card3DHover.allowDiscard = true;
            yield break;
        }

        int consumed = 0;
        foreach (CardInstance ci in selectedList)
        {
            if (ci == null) continue;
            GameObject toRemove = null;
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                CardInstance handCI = card?.GetComponent<CardInstance>();
                if (handCI != null && handCI.instanceID == ci.instanceID)
                {
                    toRemove = card;
                    break;
                }
            }
            if (toRemove != null)
            {
                NetworkPlayer.Local.handCards.Remove(toRemove);
                Destroy(toRemove);
                consumed++;
            }
        }

        if (consumed > 0)
        {
            giver.currentAttack += consumed;
            giver.baseAttack += consumed;
            NetworkPlayer.Local.AddEnergy(consumed);

            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D != null)
                    {
                        Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                        if (c3d?.cardInstance == giver)
                        {
                            c3d.UpdateValues();
                            break;
                        }
                    }
                }
            }
        }

        CardDisplayPanel.Instance.Hide();
        CardDisplayPanel.Instance.multiSelect = false;

        foreach (GameObject c in NetworkPlayer.Local.handCards) { if (c != null) c.SetActive(true); }
        SetHandAreaRaycast(true);
        RefreshLayout(true);
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        Card3DHover.allowDiscard = true;
    }
    public void RemnantFinalize(CardInstance first, CardInstance second, bool returnFirst)
    {
        CardInstance returnTarget = returnFirst ? first : second;
        CardInstance refundTarget = returnFirst ? second : first;

        CardData returnTemplate = CardDatabase.Instance?.GetTemplate(returnTarget.templateID);

        NetworkPlayer.Local.AddEnergy(refundTarget.currentCost);

        returnTarget.isActiveExit = true;
        refundTarget.isActiveExit = true;

        BoardSlot returnSlot = FindSlotOf(returnTarget);
        BoardSlot refundSlot = FindSlotOf(refundTarget);

        if (returnSlot != null)
            returnSlot.HandleDeath(returnSlot.currentCard3D);
        if (refundSlot != null)
            refundSlot.HandleDeath(refundSlot.currentCard3D);

        returnTarget.handledReturnToHand = true;
        if (returnTemplate != null)
            NetworkPlayer.Local.AddCardToHandFromInstance(returnTemplate, returnTarget);

       
    }
    BoardSlot FindSlotOf(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return slot;
        }
        return null;
    }
    public IEnumerator SummonSmallEvilEffect()
    {
        CardData template = CardDatabase.Instance?.GetTemplate("03010");
        if (template?.prefab3D == null) { CardDrag.CleanupSpellResources(); yield break; }

        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasEmpty = false;
        for (int i = 6; i <= 11; i++)
            if (bm.GetSlot(i) != null && !bm.GetSlot(i).isBlocked && !bm.GetSlot(i).hasCard) { hasEmpty = true; break; }
        if (!hasEmpty) { CardDrag.CleanupSpellResources(); yield break; }

        BoardSlot.isPlacingCard = true;
        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

        bool placed = false;
        BoardSlot.onTargetSelected = (selectedSlot) =>
        {
            if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.hasCard || selectedSlot.slotID < 6) return;
            GameObject temp = new GameObject("TempSmallEvil");
            CardInstance ti = temp.AddComponent<CardInstance>();
            ti.InitFromTemplate(template, 0);
            PlaceCardToSlot(selectedSlot, temp);
            Destroy(temp);
            placed = true;
            SelectionManager.Instance.ForceEndAll();
            BoardSlot.isPlacingCard = false;
            BoardSlot.isStrengtheningSlot = false;
            CardDrag.CleanupSpellResources();
        };
        yield return new WaitUntil(() => placed);
    }
    public IEnumerator SpawnTwoHorrors(int baseHP, int baseAtk)
    {
        CardData horrorTemplate = CardDatabase.Instance?.GetTemplate("03029");
        if (horrorTemplate?.prefab3D == null) yield break;

        for (int k = 0; k < 2; k++)
        {
            HandManager hm = this;
            hm.HideAllCards();
            hm.SetHandAreaRaycast(false);
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
            Card3DHover.allowDiscard = false;

            GameObject temp = new GameObject("TempHorror");
            CardInstance ti = temp.AddComponent<CardInstance>();
            ti.InitFromTemplate(horrorTemplate, k);
            ti.baseHealth = baseHP;
            ti.baseMaxHealth = baseHP;
            ti.currentHealth = baseHP;
            ti.currentMaxHealth = baseHP;
            ti.baseAttack = baseAtk;
            ti.currentAttack = baseAtk;

            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            BoardSlot.cardToPlace = temp;
            if (k == 0) BoardSlot.ignoreNextClickSlot = Card3DHover.ignoreSlotID;

            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
            Destroy(temp);
        }

        Card3DHover.ignoreSlotID = -1;
        SetHandAreaRaycast(true);
        ShowAllCards();
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        Card3DHover.allowDiscard = true;
        BoardSyncManager.Instance?.SyncHostBoard();
    }

    void PlaceHorror(BoardSlot slot, CardData template, int baseHP, int baseAtk, int index)
    {
        GameObject temp = new GameObject("TempHorror");
        CardInstance ti = temp.AddComponent<CardInstance>();
        ti.InitFromTemplate(template, index);
        ti.baseHealth = baseHP;
        ti.baseMaxHealth = baseHP;
        ti.currentHealth = baseHP;
        ti.currentMaxHealth = baseHP;
        ti.baseAttack = baseAtk;
        ti.currentAttack = baseAtk;
        PlaceCardToSlot(slot, temp);
        Destroy(temp);
    }
    CardInstance FindRebornOnField()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01513") return ci;
            }
        }
        return null;
    }

    bool IsFullySilenced(CardInstance ci)
    {
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci);
    }

    void UpdateRebornDisplay(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            {
                s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                return;
            }
        }
    }
   public IEnumerator DoorEffect()
    {
        NetworkPlayer.Local.TakeDamage(1);
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<CardInstance> summonList = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Summon)
                summonList.Add(ci);
        }

        if (summonList.Count == 0)
        {
                Debug.Log("对方场上没有召唤物，阴阳无法打出");
            CardDrag.CleanupSpellResources();
            yield break;
        }

        CardDisplayPanel.Instance.enableCostCheck = true;
        CardDisplayPanel.Instance.maxTotalCost = 8;
        CardDisplayPanel.Instance.multiSelect = true;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(summonList, ci => true, () =>
        {
            confirmed = true;
        }, "召唤");

        Debug.Log($"门 multiSelect={CardDisplayPanel.Instance.multiSelect}");

        yield return new WaitUntil(() => confirmed);

        Debug.Log($"门 confirmed, selected.Count={CardDisplayPanel.Instance.GetSelectedCards().Count}");

        List<CardInstance> selected = CardDisplayPanel.Instance.GetSelectedCards();

        if (selected.Count == 0)
        {
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
            CardDrag.CleanupSpellResources();
            yield break;
        }

        int totalCost = 0;
        foreach (CardInstance ci in selected)
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null) totalCost += td.baseCost;
        }

        if (totalCost > 8)
        {
            Debug.Log($"门：召唤费用和={totalCost}，超过8");
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
            CardDrag.CleanupSpellResources();
            yield break;
        }

        foreach (CardInstance ci in selected)
        {
            GameObject cardObj = null;
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                CardInstance handCI = card?.GetComponent<CardInstance>();
                if (handCI != null && handCI.instanceID == ci.instanceID)
                {
                    cardObj = card;
                    break;
                }
            }
            if (cardObj == null) continue;

            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td == null) continue;

            NetworkPlayer.Local.handCards.Remove(cardObj);

            HandManager hm = FindObjectOfType<HandManager>();
            hm.HideAllCards();
            hm.SetHandAreaRaycast(false);
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);

            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            BoardSlot.cardToPlace = cardObj;

            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
        }

        CardDisplayPanel.Instance.Hide();
        CardDisplayPanel.Instance.multiSelect = false;
        CardDrag.CleanupSpellResources();
    }
    public IEnumerator PlagueEffect()
    {
        // 第一次：隐藏手牌
        BoardSlot first = null;
        bool firstDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.isBlocked) { first = s; firstDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => firstDone);
        if (first == null) { CardDrag.CleanupSpellResources(); yield break; }

        // 第二次：隐藏手牌
        BoardSlot second = null;
        bool secondDone = false;
        BoardSlot.extraTargetFilter = (s) => s != first;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.isBlocked && s != first) { second = s; secondDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => secondDone);
        BoardSlot.extraTargetFilter = null;
        if (second == null) { CardDrag.CleanupSpellResources(); yield break; }

        first.hasPlague = true;
        first.plagueRoundCount = 1;
        second.hasPlague = true;
        second.plagueRoundCount = 1;

        CardDrag.CleanupSpellResources();
    }
    public IEnumerator ChargeHornEffect()
    {
        yield return null;

        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasValid = false;
        for (int i = 6; i <= 11; i++)
            if (bm.GetSlot(i)?.currentCard3D != null) { hasValid = true; break; }
        if (!hasValid) { CardDrag.CleanupSpellResources(); yield break; }

        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.AllyAnyRow, (selectedSlot) =>
        {
            int rowStart = selectedSlot.slotID < 9 ? 6 : 9;
            int enemyRowStart = selectedSlot.slotID < 9 ? 0 : 3;

            for (int col = 0; col < 3; col++)
            {
                BoardSlot mySlot = bm.GetSlot(rowStart + col);
                if (mySlot?.currentCard3D == null) continue;

                CardInstance myInst = mySlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (myInst == null) continue;

                int atk = myInst.currentAttack;
                if (atk <= 0)
                {
                    myInst.currentAttack = Mathf.Max(0, myInst.currentAttack - 1);
                    mySlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    continue;
                }

                BoardSlot enemySlot = bm.GetSlot(enemyRowStart + col);
                if (enemySlot?.currentCard3D != null)
                {
                    CardInstance enemyInst = enemySlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (enemyInst != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(enemyInst, atk, mySlot.currentCard3D);
                        enemySlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }

                myInst.currentAttack = Mathf.Max(0, myInst.currentAttack - 1);
                mySlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }

            BoardSlot.CheckAndHandleDeaths();
            done = true;
        });

        yield return new WaitUntil(() => done);
        CardDrag.CleanupSpellResources();
    }
    public IEnumerator CounterKillerEffect()
    {
        List<CounterCard> enemyCounters = CounterManager.Instance?.enemyCounters;
        if (enemyCounters == null || enemyCounters.Count == 0)
        {
                Debug.Log("对方场上没有召唤物，阴阳无法打出");
            CardDrag.CleanupSpellResources();
            yield break;
        }

        // 清理按钮
        foreach (var cc in enemyCounters)
        {
            if (cc.model != null)
            {
                Button btn = cc.model.GetComponent<Button>() ?? cc.model.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                var captured = cc;
                btn.onClick.AddListener(() => OnCKSelected(captured));
            }
        }

        selectedCK = null;
        yield return new WaitUntil(() => selectedCK != null);

        // 清理按钮
        foreach (var cc in enemyCounters)
        {
            if (cc.model != null)
            {
                Button btn = cc.model.GetComponent<Button>();
                if (btn != null) Destroy(btn);
            }
        }

        if (selectedCK != null)
        {
            CounterCard cc = selectedCK;
            CardData template = cc.template;

            // 无效果触发对方反制牌（正常扣费）
            CounterManager.Instance.TriggerEnemyCounterNoEffect(cc);

            // 己方打出复制品，触发时扣1能量
            CounterManager.Instance.PlayCounterWithReducedCost(template, 1);
        }

        CardDrag.CleanupSpellResources();
    }

    CounterCard selectedCK;

    void OnCKSelected(CounterCard cc)
    {
        selectedCK = cc;
    }
    public IEnumerator WatcherDelayedCheck()
    {
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        yield return null;
        WatcherCheckAndTrigger();
    }

    public void WatcherImmediateCheck()
    {
        WatcherCheckAndTrigger();
    }

    void WatcherCheckAndTrigger()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasEnemy = false;
        for (int i = 0; i <= 5; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }

        if (!hasEnemy) return;

        CardInstance watcher = null;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isWatcher) { watcher = ci; break; }
            }
        }

        if (watcher == null) return;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(watcher)) return;

        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (target) =>
        {
            if (target?.currentCard3D != null)
            {
                Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                if (t3d?.cardInstance != null)
                {
                    BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 1, null);
                    t3d.UpdateValues();
                }
            }
            BoardSlot.CheckAndHandleDeaths();
        });
    }
  public  IEnumerator BetrayalEffect()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        // 检查对方区域是否有空位
        bool hasEmpty = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s != null && !s.isBlocked && !s.hasCard && !s.prisonBlocked)
            { hasEmpty = true; break; }
        }

        if (!hasEmpty)
        {
                Debug.Log("对方场上没有召唤物，阴阳无法打出");
            CardDrag.CleanupSpellResources();
            yield break;
        }

        CardData traitorTemplate = CardDatabase.Instance?.GetTemplate("03025");
        if (traitorTemplate?.prefab3D == null) { CardDrag.CleanupSpellResources(); yield break; }

        // 选择对方空位
        BoardSlot.isPlacingCard = true;
        BoardSlot.isStrengtheningSlot = true;
        bool placed = false;

        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (selectedSlot) =>
        {
            if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.hasCard || selectedSlot.slotID > 5) return;

            GameObject temp = new GameObject("TempTraitor");
            CardInstance ti = temp.AddComponent<CardInstance>();
            ti.InitFromTemplate(traitorTemplate, 0);
            HandManager hm = FindObjectOfType<HandManager>();
            hm.PlaceCardToSlot(selectedSlot, temp);
            Destroy(temp);

            // 敌方单位朝向
            selectedSlot.currentCard3D.transform.rotation = Quaternion.Euler(0, 180, 0);

            placed = true;
            BoardSlot.isPlacingCard = false;
            BoardSlot.isStrengtheningSlot = false;
        });

        yield return new WaitUntil(() => placed);
        CardDrag.CleanupSpellResources();
    }
}