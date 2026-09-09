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
    [Tooltip("悬停卡牌时相邻卡牌的额外水平让位偏移（0=关闭）")]
    public float hoverSpacingOffset = 24f;

    [Header("非己方回合手牌压暗（对手回合提示）")]
    [Tooltip("非己方回合且不在选择阶段时，整手缩小倍率")] public float dimScale = 0.7f;
    [Tooltip("压暗时整手向下偏移（局部单位，负=下移，让手牌部分移出视野）")] public float dimOffsetY = -160f;
    bool _handDimmed;

    private List<CardView> handCards = new List<CardView>();
    private CardView draggingCard;
    private int draggingIndex = -1;

    // 动态射线阻挡
    private bool _handAreaVisible = true;
    private bool _boundsDirty = true;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    // 抽牌动画
    private bool _isDrawAnimating = false;
    public bool IsDrawAnimating => _isDrawAnimating;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void RegisterCard(CardView cv, bool refreshLayout = true)
    {
        if (!handCards.Contains(cv))
            handCards.Add(cv);
        if (refreshLayout)
        {
            RefreshLayout(true);
            MarkBoundsDirty();
        }
        // 新入卡若正值"非己方回合压暗"，立即应用同态（缩小/下移/淡灰遮罩）
        if (_handDimmed && cv != null)
            cv.SetGroupDim(true, dimScale, dimOffsetY);
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

        // Sync to remote client after spell/counter cast is fully processed.
        // Skip attach-only cards (canAttach && baseHealth==0) — their models live in attachedModels,
        // and CmdPlayCard rejects them, triggering unnecessary MarkDirty that races with attach sync.
        if (NetworkClient.isConnected && !string.IsNullOrEmpty(removedTemplateID))
        {
            CardData removedTD = CardDatabase.Instance?.GetTemplate(removedTemplateID);
            bool isAttachOnly = removedTD != null && removedTD.canAttach && removedTD.baseHealth == 0;
            if (!isAttachOnly)
            {
                NetworkPlayer.Local?.CmdPlayCard(removedTemplateID, -1, -1, -1, -1, -1, "");
                BoardSyncManager.MarkDirty();
            }
        }

        RefreshLayout(true);
        MarkBoundsDirty();
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
        MarkBoundsDirty();
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
        // 仅当手牌是被真 HideAllCards 隐藏(标志置位)才恢复按钮；预览式/鼠标通道的 ShowAllCards 不清标志、不碰按钮
        if (_handCardsHidden)
        {
            _handCardsHidden = false;
            SetTurnButtonsVisible(true);
        }
        MarkBoundsDirty();
    }

    public bool IsPlayArea(Vector2 screenPos)
    {
        return screenPos.y > Screen.height * 0.6f;
    }

    public void OnDragStart(CardView cv)
    {
        draggingCard = cv;
        draggingIndex = handCards.IndexOf(cv);
        MarkBoundsDirty();
    }

    public void OnDragUpdate(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, screenPos, null, out Vector2 local);
        draggingIndex = GetInsertIndex(local.x);
        RefreshLayout(false);
        MarkBoundsDirty();
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
            MarkBoundsDirty();
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
            MarkBoundsDirty();
            return;
        }

        int hovIdx = GetHoveredIndex();

        // 压暗态(dimScale<1)：整手卡面已缩小，间距/让位/弧线须按同一倍率缩放，避免"小卡大间距"。
        float lay = _handDimmed ? dimScale : 1f;
        float cardW = cardWidth * lay;
        float maxW = maxWidth * lay;
        float hovSpace = hoverSpacingOffset * lay;
        float arcRadius = radius * lay;

        float overlap = Mathf.Lerp(0f, maxOverlapRatio, (float)(count - 1) / 19f);
        float step = cardW * (1f - overlap);
        float totalW = step * (count - 1) + cardW;

        if (totalW > maxW && count > 1)
        {
            step = (maxW - cardW) / (count - 1);
            totalW = maxW;
        }

        float startX = -totalW / 2f + cardW / 2f;

        for (int i = 0; i < count; i++)
        {
            CardView cv = handCards[i];
            float x = startX + i * step;

            if (draggingCard != null && draggingIndex >= 0 && i >= draggingIndex && cv != draggingCard)
                x += step * pushRatio;

            // 悬停让位：相邻卡牌向外偏移，给放大卡牌腾空间（越近偏移越大，d=1 全量、d=2 减半）；程度随 lay 缩放
            if (hovIdx >= 0 && hovIdx != i)
            {
                int d = Mathf.Abs(i - hovIdx);
                float falloff = Mathf.Max(0f, 1f - (d - 1) * 0.5f);
                x += hovSpace * falloff * (i < hovIdx ? -1f : 1f);
            }

            float normalizedX = x / (maxW / 2f);
            float arcY = -Mathf.Abs(normalizedX) * arcRadius * 0.02f;
            Vector3 target = new Vector3(x, arcY, 0);
            float angle = -normalizedX * totalArcAngle * 0.5f;
            Quaternion targetRot = Quaternion.Euler(0, 0, angle);

            cv.targetPos = target;
            cv.targetRotation = targetRot;

            // 飞行中的牌只更新目标，不 snap——交给 FlyInFromDeck 动画落位
            if (instant && !cv.IsFlying)
            {
                cv.rectTransform.localPosition = target;
                cv.rectTransform.localRotation = targetRot;
            }
        }

        for (int i = 0; i < count; i++)
            handCards[i].transform.SetSiblingIndex(i);
        // 悬停卡牌置顶（不被兄弟排序拉下去）
        if (hovIdx >= 0) handCards[hovIdx].transform.SetAsLastSibling();

        MarkBoundsDirty();
    }

    /// <summary>返回当前悬停的卡牌索引（-1=无悬停）。</summary>
    int GetHoveredIndex()
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            var cv = handCards[i]?.GetComponent<CardView>();
            if (cv != null && cv.IsHovered) return i;
        }
        return -1;
    }

    int GetInsertIndex(float localX)
    {
        float lay = _handDimmed ? dimScale : 1f; // 压暗态插入阈值随卡面缩放
        for (int i = 0; i < handCards.Count; i++)
            if (handCards[i] != draggingCard && localX < handCards[i].targetPos.x + (cardWidth * lay) / 2f)
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

        // [打出展示] 己方真实手牌召唤物落地：仅带 CardView 的真手牌（效果直接生成的 Token 无 CardView，不捕获）→ 正面展示
        if (cardObject.GetComponent<CardView>() != null
            && template.cardType == CardType.Summon
            && !sourceInstance.canAttach)
        {
            PlayRevealManager.Show(template, false);
        }

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
                                            { bm2.attachedModels.RemoveAt(i); BoardManager.RecordAndRemoveAttach(obj); }
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
                                TurnManager.SyncMyBoardToOpponent();
                                if (template.hasOnEnter)
                                {
                                    selectedSlot.StartCoroutine(selectedSlot.StartOnEnterEffect(template, sourceInstance));
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
                    TurnManager.SyncMyBoardToOpponent();
                    if (template.hasOnEnter)
                    {
                        selectedSlot.StartCoroutine(selectedSlot.StartOnEnterEffect(template, sourceInstance));
                    }
                    CardView cvAttach2 = cardObj.GetComponent<CardView>();
                    if (cvAttach2 != null) RemoveCard(cvAttach2);
                    else { handCards.RemoveAll(c => c == null); RefreshLayout(true); }
                    // 修复：附着类无进场效果时也恢复手牌射线/按钮（否则抽牌/结束按钮被永久隐藏禁用）
                    ShowAllCards();
                    SetHandAreaRaycast(true);
                    FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
                }
                else
                {
                    PlaceIndependentCard(selectedSlot, sourceInstance, template, cardObj);
                    BoardSlot.CleanupAttachSelect();
                    if (template.hasOnEnter)
                    {
                        CardInstance indInst = selectedSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                        selectedSlot.StartCoroutine(selectedSlot.StartOnEnterEffect(template, indInst ?? sourceInstance));
                    }
                    CardView cvInd = cardObj.GetComponent<CardView>();
                    if (cvInd != null) RemoveCard(cvInd);
                    else { handCards.RemoveAll(c => c == null); RefreshLayout(true); }
                    // 修复：hasOnEnter==0 的 canAttach 卡独立落位不会触发进场 → 无 CleanupAfterPlacement，
                    // 这里主动恢复手牌射线/抽牌/结束按钮，防被永久隐藏禁用
                    ShowAllCards();
                    SetHandAreaRaycast(true);
                    FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
                }
            });
            return;
        }

        Vector3 worldPos = GetSlotWorldPosition(slot.slotID);
        GameObject model = Instantiate(template.prefab3D, worldPos, Quaternion.Euler(0, 180, 0));
        Card3DInstance.PlaySummonOn(model); // 召唤动画（模型层 0→1.5→1.4→1.0）
        model.name = sourceInstance.instanceID;
        model.transform.localScale = template.prefab3D.transform.localScale;
        Player.Scale3DModel(model);
        Card3DInstance instance3D = model.GetComponent<Card3DInstance>();
        if (instance3D != null)
        {
            CardInstance cardInst = model.AddComponent<CardInstance>();
            cardInst.CopyFrom(sourceInstance);
            cardInst.handledReturnToHand = false;
            cardInst.merchantDiscounted = false;
            cardInst.energyReaperDiscounted = false;
            if (cardInst.templateID == "01534")
            {
                NetworkPlayer owner = BoardManager.GetOwnerPlayer(slot.slotID);
                if (owner != null && owner.outlawNestTotalDamage > 0)
                    cardInst.totalDamageTaken = owner.outlawNestTotalDamage;
            }
            instance3D.cardInstance = cardInst;
            // 商人/收割者"召唤费用-1"场上固化（仅权威侧：host/离线/server 落板一次，随 activeStatuses 同步）
            if (NetworkServer.active) CardInstance.ApplyCostDiscountStatus(cardInst, slot);
        }

        slot.SetCard(model);
        // 同步到服务器——纯客户端 MarkDirty 是空操作，必须显式通知服务器
        if (NetworkClient.isConnected && !string.IsNullOrEmpty(sourceInstance.templateID))
        {
            string iid = sourceInstance.instanceID ?? CardZoneManager.GenerateInstanceID(sourceInstance.templateID);
            int cost = instance3D?.cardInstance?.currentCost ?? sourceInstance.currentCost;
            int atk = sourceInstance.currentAttack;
            int hp = sourceInstance.currentHealth;
            int maxHp = sourceInstance.currentMaxHealth;
            Debug.Log($"[PLACE-NORMAL] CmdPlayCard: tid={sourceInstance.templateID} slot={slot.slotID} handCost={sourceInstance.currentCost} boardCost={instance3D?.cardInstance?.currentCost} finalCost={cost} iid={iid} atk={atk} hp={hp}");
            NetworkPlayer.Local?.CmdPlayCard(sourceInstance.templateID, slot.slotID, atk, hp, maxHp, cost, iid);
        }
        BoardSyncManager.MarkDirty();
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
                    Card3DInstance.PlaySummonOn(mergeModel); // 召唤动画
                    Player.Scale3DModel(mergeModel);
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

                // Sync merged 阴阳 + cleared slot to opponent
                TurnManager.SyncMyBoardToOpponent();
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

    /// <summary>中枢(03027)是否在同半场？用于持续生效的灵能前缀光环。</summary>
    bool IsCoreOnField(int slotID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        BoardManager.GetSideRange(slotID, out int s, out int e);
        for (int i = s; i <= e; i++)
        {
            var ci = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "03027") return true;
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
        float x = 0f, y = 0f, z = -5.7f;
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

    /// <summary>附着物在指定槽位 + 附着序号下的世界坐标。</summary>
    public static Vector3 GetAttachWorldPos(int slotID, int attachOrder)
    {
        var bm = FindObjectOfType<BoardManager>();
        var hm = FindObjectOfType<HandManager>();
        Vector3 basePos;
        var card3D = bm?.GetSlot(slotID)?.currentCard3D;
        if (card3D != null && card3D.transform != null)
            basePos = card3D.transform.position;
        else if (hm != null)
            basePos = hm.GetSlotWorldPosition(slotID);
        else
            basePos = Vector3.zero;
        return new Vector3(basePos.x - 0.25f - attachOrder * 0.25f, basePos.y, basePos.z + 0.1f + attachOrder * 0.05f); // 附着-宿主及附着-附着 X 间隔均0.25
    }

    bool _handCardsHidden; // 真手牌隐藏标志：置位才隐藏/恢复抽牌与结束回合按钮（预览/射线通道不置位）

    public void HideAllCards()
    {
        foreach (CardView cv in handCards)
            if (cv != null) cv.gameObject.SetActive(false);
        // 隐藏手牌 → 同步隐藏抽牌/结束回合按钮，防误触（幂等）
        if (!_handCardsHidden)
        {
            _handCardsHidden = true;
            SetTurnButtonsVisible(false);
        }
        MarkBoundsDirty();
    }

    EndTurnButton _endBtnCache;
    DrawCardUI _drawUiCache;

    /// <summary>抽牌与结束回合按钮显隐（手牌隐藏期间防误触）。只切 activeSelf；各自 interactable/回合门逻辑不受影响。
    /// 必须缓存引用：FindObjectOfType 不命中已隐藏(inactive)的对象——首次调用(隐藏)时对象仍激活即可缓存，此后直接 SetActive 恢复。</summary>
    void SetTurnButtonsVisible(bool visible)
    {
        if (_endBtnCache == null) _endBtnCache = FindObjectOfType<EndTurnButton>();
        if (_drawUiCache == null) _drawUiCache = FindObjectOfType<DrawCardUI>();
        if (_endBtnCache != null) _endBtnCache.gameObject.SetActive(visible);
        if (_drawUiCache != null) _drawUiCache.gameObject.SetActive(visible);
    }

    public void SetHandAreaRaycast(bool enabled)
    {
        _handAreaVisible = enabled;
        _canvasGroup.interactable = enabled;
        // ⚠ 不要在此切按钮显隐：本方法被悬停/鼠标离开(OnMouseExit)等"预览/交互锁定"通道高频调用，
        // 会错误地在鼠标离开卡牌时恢复按钮。按钮显隐只由真手牌隐藏标志(HideAllCards/ShowAllCards)驱动。
        if (!enabled)
            _canvasGroup.blocksRaycasts = false;
        else
            MarkBoundsDirty();
    }

    /// <summary>标记包围盒脏，下一帧 LateUpdate 重新计算。</summary>
    public void MarkBoundsDirty()
    {
        _boundsDirty = true;
    }

    // ══════════════════════════════════════════════════════════════
    // 非己方回合手牌压暗：!IsMyTurn 且 不在选择阶段 → 整手 0.7× + 下移 + 淡灰遮罩
    // （选择阶段 = SelectionManager.IsSelecting：对手回合弹给我的选择目标面板也恢复）
    // ══════════════════════════════════════════════════════════════
    bool ShouldDimHand()
    {
        if (TurnManager.Instance == null) return false;
        if (TurnManager.Instance.IsMyTurn()) return false;
        if (SelectionManager.Instance != null && SelectionManager.Instance.IsSelecting) return false;
        return true;
    }

    void ApplyHandDimToAll(bool dim)
    {
        if (handCards == null) return;
        // 先按新 dim 重算整手间距/位置（RefreshLayout 读到 _handDimmed 会按 dimScale 缩放水平排布），
        // 再对每张卡做缩放 + 下移 + 遮罩动画，确保缩小时间距同步收紧。
        RefreshLayout(false);
        for (int i = 0; i < handCards.Count; i++)
        {
            var cv = handCards[i];
            if (cv == null) continue;
            cv.SetGroupDim(dim, dim ? dimScale : 1f, dim ? dimOffsetY : 0f);
        }
        MarkBoundsDirty();
    }

    void ReconcileHandDimState()
    {
        bool dim = ShouldDimHand();
        if (dim == _handDimmed) return; // 仅状态变化时生效，避免逐帧干扰卡片动画
        _handDimmed = dim;
        ApplyHandDimToAll(dim);
    }

    /// <summary>手牌重排兜底：任何"裸删"（外部 Remove+Destroy 未走 RemoveCard，如弃置/偷牌/换洗/效果消耗手牌）都会在
    /// handCards 里留下 null → 每帧探测到即清空并 RefreshLayout，杜绝移除后手牌留洞不缩拢。正规 RemoveCard 已即时
    /// 重排并自清 null，此处不触发。仅对已登记到本列表的可见手牌生效（服务端/AI 追踪列表无 CardView 自然无影响）。</summary>
    void Update()
    {
        ReconcileHandDimState(); // 回合/选择状态变化 → 整手压暗或还原（边缘检测）
        if (handCards == null || handCards.Count == 0) return;
        bool hasNull = false;
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i] == null) { hasNull = true; break; }
        }
        if (hasNull)
        {
            handCards.RemoveAll(c => c == null);
            if (draggingCard != null && draggingCard.gameObject == null) draggingCard = null;
            RefreshLayout(true);
            MarkBoundsDirty();
        }
    }

    void LateUpdate()
    {
        if (_boundsDirty)
        {
            _boundsDirty = false;
            if (_handAreaVisible)
            {
                Rect bounds = CalculateCardsBounds();
                ApplyBoundsToRectTransform(bounds);
            }
        }
    }

    /// <summary>计算所有可见、非拖拽中的手牌在屏幕空间的包围盒。</summary>
    public Rect CalculateCardsBounds()
    {
        if (handCards == null || handCards.Count == 0)
            return Rect.zero;

        Rect bounds = Rect.zero;
        bool first = true;

        foreach (var card in handCards)
        {
            if (card == null || !card.gameObject.activeSelf) continue;
            if (card == draggingCard) continue; // 拖拽中的牌已脱离手牌区，不参与
            if (card.IsFlying) continue; // 飞行中的牌位置不稳定，不参与

            Rect cardRect = card.GetWorldRect();
            if (first) { bounds = cardRect; first = false; }
            else bounds = RectUnion(bounds, cardRect);
        }

        return bounds;
    }

    static Rect RectUnion(Rect a, Rect b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(a.xMin, b.xMin),
            Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax),
            Mathf.Max(a.yMax, b.yMax)
        );
    }

    /// <summary>将屏幕空间包围盒应用到 HandArea 的 RectTransform，动态调整射线阻挡区域。</summary>
    void ApplyBoundsToRectTransform(Rect bounds)
    {
        if (bounds.width <= 0 || bounds.height <= 0)
        {
            _canvasGroup.blocksRaycasts = false;
            return;
        }

        // 保持原始 anchor (0.5, 0) 和 anchoredPosition (0, 210)，只修改 sizeDelta。
        // 否则改变 anchor/pivot 会导致 HandArea 整体偏移，连带所有手牌位置错位。
        _rectTransform.sizeDelta = new Vector2(
            Mathf.Max(bounds.width, 200f),
            Mathf.Max(bounds.height, 100f));

        _canvasGroup.blocksRaycasts = true;
    }

    /// <summary>获取抽牌起点世界坐标（屏幕右边界外，完全不可见）。</summary>
    public Vector3 GetDeckWorldPosition()
    {
        Camera cam = GetComponentInParent<Canvas>()?.worldCamera ?? Camera.main;
        if (cam == null)
            return transform.position + new Vector3(300, 0, 0);

        // Y/Z 用 HandArea 中心世界坐标（transform.position 即 RectTransform 中心，pivot 0.5,0.5）
        Vector3 center = transform.position;

        // 屏幕右边界外 200 像素 → 世界坐标 X。
        // ScreenToWorldPoint 的 z 参数是「相机前方深度」，正交时无关、透视时必须传正确深度，
        // 否则透视投影下 X 映射错误（曾导致抽牌从手牌区中央飞出而非屏幕右外）。
        float depth = Vector3.Dot(center - cam.transform.position, cam.transform.forward);
        Vector3 rightEdgeWorld = cam.ScreenToWorldPoint(new Vector3(Screen.width + 200, 0, depth));
        return new Vector3(rightEdgeWorld.x, center.y, center.z);
    }

    /// <summary>抽牌入场动画：收集新增的 CardView，依次从牌库位置飞入，延迟让位。</summary>
    /// <param name="newCards">本次抽牌新增的卡牌（targetPos 待算，尚未落位，alpha=0）</param>
    public System.Collections.IEnumerator AnimateCardDraw(List<CardView> newCards)
    {
        if (newCards == null || newCards.Count == 0) yield break;

        // 快速连续抽牌：动画进行中时新牌直接落位显示，不播动画（但仍要播音效）
        if (_isDrawAnimating)
        {
            RefreshLayout(false);
            foreach (var cv in newCards)
            {
                if (cv == null) continue;
                cv.IsFlying = false;
                cv.rectTransform.localPosition = cv.targetPos;
                cv.rectTransform.localRotation = cv.targetRotation;
                cv.SetAlpha(1f);
                // 每张牌都触发一次抽牌音效（即使动画未播完，音效不能被吞掉）
                AudioManager.Instance?.Play(SoundEffectType.DrawCard);
            }
            yield break;
        }

        AnimationConfig cfg = AnimationConfig.Load();
        float duration = cfg != null ? cfg.cardDrawDuration : 0.4f;
        float stagger = cfg != null ? cfg.cardDrawStaggerDelay : 0.12f;
        float trigger = cfg != null ? cfg.deferredLayoutTrigger : 0.5f;
        float randomness = cfg != null ? cfg.flyDurationRandomness : 0.1f;

        // 发放顺序：按 handCards 索引升序（索引0在最左）→ 从左到右依次飞入
        newCards.RemoveAll(c => c == null);
        newCards.Sort((a, b) => handCards.IndexOf(a).CompareTo(handCards.IndexOf(b)));

        _isDrawAnimating = true;
        SetHandAreaRaycast(false);

        // 1. 保存现有牌（非新牌）的旧目标位置 —— 让位前它们应保持原地
        var oldTargets = new List<(CardView cv, Vector3 pos, Quaternion rot)>();
        foreach (var c in handCards)
        {
            if (c == null || newCards.Contains(c) || c.IsFlying) continue;
            oldTargets.Add((c, c.targetPos, c.targetRotation));
        }

        // 2. 计算最终布局（拿到新牌 targetPos，现有牌 targetPos 也更新），再恢复现有牌旧目标 → 现有牌原地不动
        RefreshLayout(false);
        foreach (var (cv, pos, rot) in oldTargets)
        {
            cv.targetPos = pos;
            cv.targetRotation = rot;
        }

        Vector3 deckWorldPos = GetDeckWorldPosition();
        bool layoutTriggered = false;
        float maxDur = duration;

        for (int i = 0; i < newCards.Count; i++)
        {
            CardView cv = newCards[i];
            if (cv == null) continue;

            // 目标世界坐标 = HandArea 局部坐标 targetPos 转世界（RefreshLayout 已算好 targetPos/targetRotation）
            Vector3 targetWorldPos = cv.transform.parent.TransformPoint(cv.targetPos);
            Quaternion targetRot = cv.targetRotation;

            // 多张牌时逐张延迟，形成连续飞出效果
            if (i > 0)
                yield return new WaitForSeconds(stagger);

            // 每张牌飞行时长做微小随机化（±randomness），避免机械一致
            float dur = duration * Random.Range(1f - randomness, 1f + randomness);
            if (dur > maxDur) maxDur = dur;

            // 每张牌飞入时播放一次抽牌音效（多张牌时随 stagger 间隔依次播放）
            AudioManager.Instance?.Play(SoundEffectType.DrawCard);

            // 启动飞入（不等待，下一张可在当前飞行期间开始延迟计时）。
            // 飞到 trigger 进度时回调一次 RefreshLayout(false)，让现有手牌开始 lerp 滑动让位。
            StartCoroutine(cv.FlyInFromDeck(deckWorldPos, targetWorldPos, targetRot, dur, cfg, trigger, () =>
            {
                if (!layoutTriggered)
                {
                    layoutTriggered = true;
                    RefreshLayout(false);
                }
            }));
        }

        // 等待最后一张牌飞完（取最长的随机时长）
        yield return new WaitForSeconds(maxDur);

        _isDrawAnimating = false;
        SetHandAreaRaycast(true);
    }

    private void PlaceIndependentCard(BoardSlot slot, CardInstance sourceInstance, CardData template, GameObject cardObject)
    {
        Vector3 worldPos = GetSlotWorldPosition(slot.slotID);
        GameObject model = Instantiate(template.prefab3D, worldPos, Quaternion.Euler(0, 180, 0));
        Card3DInstance.PlaySummonOn(model); // 召唤动画
        Player.Scale3DModel(model);
        model.name = sourceInstance.instanceID;
        Card3DInstance instance3D = model.GetComponent<Card3DInstance>();

        if (instance3D != null)
        {
            CardInstance cardInst = model.AddComponent<CardInstance>();
            CopyCardInstance(cardInst, sourceInstance);
            cardInst.isAttached = false;
            cardInst.hostSlotID = -1;
            cardInst.merchantDiscounted = false;
            cardInst.energyReaperDiscounted = false;
            instance3D.cardInstance = cardInst;
        }

        slot.SetCard(model);
        if (instance3D != null) instance3D.UpdateValues();

        // 附着专用卡（baseHealth==0）永远不放独立槽位模型到服务器
        if (sourceInstance.canAttach && sourceInstance.baseHealth == 0)
        {
            Debug.LogWarning($"[PlaceIndependentCard] 附着专用卡 {sourceInstance.templateID} 被错误放置为独立卡！已拦截 CmdPlayCard，销毁本地模型。");
            Destroy(model);
            slot.SetCard(null);
            return;
        }

        if (NetworkClient.isConnected)
        {
            string iid = instance3D?.cardInstance?.instanceID ?? sourceInstance.instanceID ?? CardZoneManager.GenerateInstanceID(sourceInstance.templateID);
            int cost = instance3D?.cardInstance?.currentCost ?? sourceInstance.currentCost;
            Debug.Log($"[PLACE-IND] CmdPlayCard: tid={sourceInstance.templateID} slot={slot.slotID} cost={cost} iid={iid}");
            NetworkPlayer.Local?.CmdPlayCard(sourceInstance.templateID, slot.slotID, -1, -1, -1, cost, iid);
        }
        BoardSyncManager.MarkDirty();

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

        Vector3 attachPos = GetAttachWorldPos(hostSlot.slotID, attachOrder);

        GameObject model = Instantiate(template.prefab3D, attachPos, Quaternion.Euler(0, 180, 0));
        Player.Scale3DModel(model);
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
            cardInst._placedAtTime = Time.time;
            cardInst.placementGeneration = BoardSlot.NextPlacementGeneration();
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
        // 修复：附着物自身显示刷新——触发 ApplyArtFromCard，加载前缀背景(PrefixArtBG)/卡图(CardArt)
        if (instance3D != null) instance3D.UpdateValues();

        // 附着动画：从下一个附着牌理论位置滑入自己理论位置（约0.5s，仅表现，不影响附着逻辑）
        instance3D?.PlayAttachSlideIn(GetAttachWorldPos(hostSlot.slotID, attachOrder + 1), attachPos);

        // 附着瞬间效果沉默门（5.x/B1）：附着体被完全沉默 → 跳过一次性增益（附着动作不受影响）。
        // 旧实现对新拷贝判 IsFullySilenced，但拷贝在 bm.attachedModels.Add(下方) 前不可定位 → 恒 false 死门。
        // 改为几何判定：目标宿主槽被能量骇客(01335)对位压制（新附着体落该槽即全沉默），或源实例已在场且被沉默
        // （保留"已在场可独立附着物被沉默后再附"语义）。双方各自本地 PlaceAttachedCard → 天然对称。
        bool attachEffectSilenced = hostSlot != null && GlobalEventManager.Instance != null
            && GlobalEventManager.Instance.IsSlotHackedByEnergyHacker(hostSlot.slotID);
        if (!attachEffectSilenced && sourceInstance != null && GlobalEventManager.Instance != null
            && GlobalEventManager.Instance.IsFullySilenced(sourceInstance))
            attachEffectSilenced = true;

        // 解析附着特性文本，给宿主加增益
        if (!string.IsNullOrEmpty(template.traits) && !attachEffectSilenced)
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
                    if (!hostCard.prefixes.Contains("机械"))
                    {
                        hostCard.GivePrefix("机械", "01119");
                    }

                    // 统计其他机械单位数量（不含宿主自己）；范围按宿主半场 owner 动态化（AI 0-5 / 玩家 6-11）
                    int mechCount = 0;
                    BoardManager bm2 = FindObjectOfType<BoardManager>();
                    if (bm2 != null)
                    {
                        int mStart = hostSlot.slotID >= 6 ? 6 : 0;
                        int mEnd = mStart + 5;
                        for (int i = mStart; i <= mEnd; i++)
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
                        BoardManager.GetSideRange(hostSlot.slotID, out int clS, out int clE);
                        for (int i = clS; i <= clE; i++)
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
                        hostCard.GrantTrait("先手：对对方前排召唤物造成2伤害，对后排造成1伤害", null, "01336");
                    }
                    else
                    {
                        hostCard.GrantTrait("先手：对对方前排召唤物造成1伤害", null, "01336");
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
                        hostCard.GivePrefix("灵能");
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
        BoardSyncManager.MarkDirty();

        // Registry: 附着卡入板面追踪
        var attachedCI = model.GetComponent<Card3DInstance>()?.cardInstance;
        if (attachedCI != null)
            RegistrySyncManager.Instance?.UpdateCard(attachedCI, hostSlot.slotID >= 6 ? 0 : 1, CardZone.Board, hostSlot.slotID);

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

        // Sync attachment + host stat changes to opponent.
        // SyncMyBoardToOpponent → CmdReportMyBoard handles everything (slots + attachBlock).
        // Do NOT also call CmdReportAttach — it creates a separate server-side attachment
        // that may get broadcast before CmdReportMyBoard clears and rebuilds, causing double model.
        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            TurnManager.SyncMyBoardToOpponent();
        }
    }
    /// <summary>AI 直接附着（无 UI 手牌卡对象）：主机/服务器侧把 source 附着到 hostSlot（AI 侧 0-5）。
    /// 复用 PlaceAttachedCard 完整流程（建附着模型+附着效果如 01112/01119），再同步。</summary>
    public void AI_PlaceAttach(CardInstance sourceInst, BoardSlot hostSlot)
    {
        if (sourceInst == null || hostSlot == null || hostSlot.currentCard3D == null) return;
        CardData td = CardDatabase.Instance?.GetTemplate(sourceInst.templateID);
        if (td == null) return;
        PlaceAttachedCard(null, sourceInst, td, hostSlot, null);
        BoardSyncManager.MarkDirty();
        TurnManager.SyncMyBoardToOpponent();
        if (td.hasOnEnter)
            hostSlot.StartCoroutine(hostSlot.StartOnEnterEffect(td, sourceInst));
    }

    private void ProcessAuras(BoardSlot slot, CardInstance sourceInstance)
    {
        // 智者自身进场光环
        bool sageBuffed = false;
        if (sourceInstance != null && sourceInstance.templateID == "03503")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            BoardManager.GetSideRange(slot.slotID, out int sageS, out int sageE);
            for (int i = sageS; i <= sageE; i++)
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
                        sageBuffed = true;
                    }
                }
            }
            if (sageBuffed)
                TurnManager.SyncMyBoardToOpponent();
        }

        // 新英雄进场：如果智者/皇帝在场，应用对应的光环加成
        if (sourceInstance != null && sourceInstance.summonType == SummonType.Hero)
        {
            CardInstance placedCI = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (placedCI != null)
            {
                ApplySageAura(placedCI, slot.slotID);
                // Sync buffed hero stats to opponent
                if (placedCI.buffedBySage)
                    TurnManager.SyncMyBoardToOpponent();
            }
        }
        // 无赖：进场获得护盾（攻击回合开始消失）/ 压制者(03501)：英雄阶位+1
        if (sourceInstance != null && sourceInstance.summonType == SummonType.Hero)
        {
            if (IsSuppressorOnField())
            {
                Card3DInstance hero3D = slot.currentCard3D?.GetComponent<Card3DInstance>();
                if (hero3D?.cardInstance != null)
                {
                    hero3D.cardInstance.currentTier += 1;
                    // 4.4 神官阶位+1：后置 Hero 进场时记来源（source=缄默神官，string 模板ID）
                    hero3D.cardInstance.AddStatus(false, "阶位临时+1", "03501");
                    hero3D.UpdateValues();
                    // Sync the tier buff to opponent
                    if (NetworkClient.isConnected)
                        TurnManager.SyncMyBoardToOpponent();
                }
            }
        }
        // 压制者(03501)进场：给己方所有在场英雄阶位+1（retro-buff，退场时在 Handle03501 减回）
        if (sourceInstance != null && sourceInstance.templateID == "03501")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            BoardManager.GetSideRange(slot.slotID, out int supS, out int supE);
            for (int i = supS; i <= supE; i++)
            {
                BoardSlot s = bm?.GetSlot(i);
                CardInstance ci = s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.summonType == SummonType.Hero)
                {
                    ci.currentTier += 1;
                    // 4.4 神官阶位+1：目标 Hero 记来源（神官退场还原处 RemoveStatusBySource 清除）
                    ci.AddStatus(false, "阶位临时+1", sourceInstance);
                    s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
            if (NetworkClient.isConnected)
                TurnManager.SyncMyBoardToOpponent();
        }
        // 中枢：附加灵能前缀
        if (sourceInstance != null && sourceInstance.templateID == "03027")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            BoardManager.GetSideRange(slot.slotID, out int coreS, out int coreE);
            for (int i = coreS; i <= coreE; i++)
            {
                BoardSlot coreSlot = bm?.GetSlot(i);
                if (coreSlot?.currentCard3D == null) continue;
                CardInstance ci = coreSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && !ci.prefixes.Contains("灵能"))
                {
                        ci.GivePrefix("灵能", "03027");
                    coreSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
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
                        ci.GivePrefix("灵能", "03027");
                        CardDisplay2D d2d = handCard.GetComponent<CardDisplay2D>();
                        d2d?.Refresh();
                        // 同步手牌前缀到服务器（打出时 ConsumeHandPrefixOverride 注入）
                        if (NetworkClient.isConnected)
                            NetworkPlayer.Local?.CmdSetHandCardPrefix(ci.instanceID, "灵能");
                    }
                }
            }
        }
        // 中枢(03027)在场时，新进场的随从自动获得灵能前缀（持续生效）
        if (sourceInstance != null && sourceInstance.templateID != "03027")
        {
            if (IsCoreOnField(slot.slotID))
            {
                CardInstance placedCI = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (placedCI != null && !placedCI.prefixes.Contains("灵能"))
                {
                    placedCI.GivePrefix("灵能", "03027");
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    // 场上随从前缀通过 SyncNow 同步
                }
            }
        }
        // 皇帝：渊前缀+1+1
        if (sourceInstance != null && sourceInstance.templateID == "01501")
        {
            if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(sourceInstance))
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                BoardManager.GetSideRange(slot.slotID, out int empS, out int empE);
                for (int i = empS; i <= empE; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s?.currentCard3D != null)
                    {
                        CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.prefixes.Contains("渊") && !ci.buffedByEmperor && ci != sourceInstance)
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
        if (sourceInstance != null && sourceInstance.prefixes.Contains("渊"))
        {
            CardInstance placedCI = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (placedCI != null && !placedCI.buffedByEmperor)
            {
                bool emperorActive = false;
                BoardManager bm = FindObjectOfType<BoardManager>();
                BoardManager.GetSideRange(slot.slotID, out int empPS, out int empPE);
                for (int i = empPS; i <= empPE; i++)
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
            CardInstance reborn = FindRebornOnField(slot.slotID);
            if (reborn != null && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(reborn)))
            {
                reborn.currentHealth += 1;
                reborn.currentMaxHealth += 1;
                UpdateRebornDisplay(reborn);
                TurnManager.SyncMyBoardToOpponent();
            }
        }
        // 无赖：进场获得护盾（攻击回合开始消失）
        if (sourceInstance != null && sourceInstance.templateID == "01309")
        {
            CardInstance placedCI = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (placedCI != null)
            {
                placedCI.GrantShield(false, true, false, "01309");
            }
        }
        // X数值同步
        BoardManager bmSync = FindObjectOfType<BoardManager>();
        if (bmSync != null)
        {
            BoardManager.GetSideRange(slot.slotID, out int xvS, out int xvE);
            for (int i = xvS; i <= xvE; i++)
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
        BoardSyncManager.MarkDirty();
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
        BoardSlot mySlot = FindMySlot(ci);
        int mySlotID = mySlot?.slotID ?? 6;

        // 阴/阳(01306/01307)：读取对方半场。阴阳(03012)：读取全场。
        int searchStart, searchEnd;
        if (isYinYang)
        {
            searchStart = 0;
            searchEnd = 12;
        }
        else
        {
            BoardManager.GetEnemySideRange(mySlotID, out searchStart, out searchEnd);
            searchEnd++; // GetEnemySideRange returns end inclusive, loop needs exclusive
        }

        int highestAttack = 0;
        int lowestAttack = int.MaxValue;
        int highestHealth = 0;
        int lowestHealth = int.MaxValue;
        bool anyMinion = false;

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
        // [AI] 02106：AI 确认即不变位置（不弹重排面板）
        if (SimpleAI.IsAIEvaluating)
        {
            BoardSlot.isPlacingCard = false;
            BoardSlot.isStrengtheningSlot = false;
            CardDrag.CleanupSpellResources();
            yield break;
        }
        BoardSlot.isStrengtheningSlot = true;
        var swapTracker = new System.Collections.Generic.List<(int, int)>();
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
                BoardManager.SwapCards(firstSlot.slotID, selected.slotID);
                swapTracker.Add((firstSlot.slotID, selected.slotID));
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
        // 发送每次交换——Host 本地已换位，改为同步到对方客户端(TargetSwapCards)；纯客户端上报服务器
        if (NetworkServer.active)
            foreach (var (a, b) in swapTracker)
                NetworkPlayer.SendSwapToRemote(a, b);
        else if (NetworkClient.isConnected)
            foreach (var (a, b) in swapTracker)
                NetworkPlayer.Local?.CmdSwapCards(a, b);
        TurnManager.SyncMyBoardToOpponent();
    }
    public IEnumerator HandCleanseEffect()
    {
        NetworkPlayer player = NetworkPlayer.Local;
        player.handCards.RemoveAll(c => c == null);
        if (player.handCards.Count == 0) { CardDrag.CleanupSpellResources(); yield break; }

        // 反选语义：多选“保留”的（≤4），其余弃掉并摸等量（CardDisplayPanel 收藏家式多选）
        List<CardInstance> candidates = BuildHandCardList(ci => true);
        if (candidates.Count == 0) { CardDrag.CleanupSpellResources(); yield break; }

        bool confirmed = false;
        CardDisplayPanel.Instance.multiSelect = true;
        CardDisplayPanel.Instance.maxSelect = 4;           // 最多保留 4
        CardDisplayPanel.Instance.confirmWhenEmpty = true; // 允许保留 0（全弃重摸）
        CardDisplayPanel.Instance.ShowWithCallback(candidates, ci => true, () => confirmed = true, "保留最多4张");
        float cleanseDeadline = Time.time + 30f;
        while (!confirmed && Time.time < cleanseDeadline) yield return null;
        List<CardInstance> chosen = CardDisplayPanel.Instance.GetSelectedCards();
        EndHandSelectionCleanup();
        if (!confirmed) { CardDrag.CleanupSpellResources(); yield break; }

        // 选中 = 保留清单；其余手牌弃掉并摸等量（iid 回找真身）
        HashSet<string> keepIids = new HashSet<string>();
        foreach (var c in chosen) if (c != null && !string.IsNullOrEmpty(c.instanceID)) keepIids.Add(c.instanceID);

        List<GameObject> toRemove = new List<GameObject>();
        foreach (GameObject card in player.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && keepIids.Contains(ci.instanceID)) continue; // 保留
            toRemove.Add(card);
        }
        int discard = 0;
        foreach (GameObject card in toRemove)
        {
            var ci = card?.GetComponent<CardInstance>();
            if (ci != null && NetworkClient.isConnected) NetworkPlayer.Local?.CmdDiscardCard(ci.instanceID);
            player.handCards.Remove(card); Destroy(card); discard++;
        }
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

            // 创建临时手牌走正常召唤流程 — 每名 03004 有全局唯一 instanceID
            GameObject temp = new GameObject("TempSpawn");
            CardInstance ti = temp.AddComponent<CardInstance>();
            ti.InitFromTemplate(template, 0, CardZoneManager.GenerateInstanceID("03004"));
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

        // 反选语义：多选“要弃的”（≤4），每弃一张 baseCost=5 的牌 +1 能量（CardDisplayPanel 收藏家式多选）
        List<CardInstance> candidates = BuildHandCardList(ci => true);
        if (candidates.Count == 0) { CardDrag.CleanupSpellResources(); yield break; }

        bool confirmed = false;
        CardDisplayPanel.Instance.multiSelect = true;
        // 弃牌数 = min(4, 手牌数)：手牌充足时须弃满 4 张才能确认（confirmWhenFull）；
        // 手牌不足 4 张时不足全弃（不卡死面板）
        CardDisplayPanel.Instance.maxSelect = Mathf.Min(4, candidates.Count);
        CardDisplayPanel.Instance.confirmWhenFull = true;
        CardDisplayPanel.Instance.ShowWithCallback(candidates, ci => true, () => confirmed = true, "弃4张");
        float manyDeadline = Time.time + 30f;
        while (!confirmed && Time.time < manyDeadline) yield return null;
        List<CardInstance> chosen = CardDisplayPanel.Instance.GetSelectedCards();
        EndHandSelectionCleanup();
        if (!confirmed) { CardDrag.CleanupSpellResources(); yield break; }

        // 选中 = 弃牌清单（iid 回找真身）
        HashSet<string> discardIids = new HashSet<string>();
        foreach (var c in chosen) if (c != null && !string.IsNullOrEmpty(c.instanceID)) discardIids.Add(c.instanceID);

        List<GameObject> toRemove = new List<GameObject>();
        foreach (GameObject card in player.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null || !discardIids.Contains(ci.instanceID)) continue;
            toRemove.Add(card);
        }

        // 3. 弃掉选中的牌，计算能量
        int energyGain = 0;
        foreach (GameObject card in toRemove)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null)
            {
                CardData data = CardDatabase.Instance?.GetTemplate(ci.templateID);
                if (data != null && data.baseCost == 5)
                    energyGain++;
                if (NetworkClient.isConnected) NetworkPlayer.Local?.CmdDiscardCard(ci.instanceID);
            }
            player.handCards.Remove(card);
            Destroy(card);
        }
        player.AddEnergy(energyGain);

        // 4. 清理
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
                Debug.Log($"[SwapTwoAllies] 忽略抛置槽位 {selected.slotID}");
                Card3DHover.ignoreSlotID = -1;
                return;
            }
            Card3DHover.ignoreSlotID = -1;

            if (firstSlot == null)
            {
                firstSlot = selected;
                Debug.Log($"[SwapTwoAllies] 第一次选择: slot={firstSlot.slotID}, card3D={(firstSlot.currentCard3D != null)}");
            }
            else if (selected != firstSlot)
            {
                BoardSlot secondSlot = selected;
                Debug.Log($"[SwapTwoAllies] 第二次选择: slot={secondSlot.slotID}, card3D={(secondSlot.currentCard3D != null)}，执行换位 {firstSlot.slotID}<->{secondSlot.slotID}");

                // SwapCardsSafe：纯客户端本地移动 + 服务端权威；Host 只走 CmdSwapCards，避免双换位撤销
                NetworkPlayer.SwapCardsSafe(firstSlot.slotID, secondSlot.slotID);

                SelectionManager.Instance.ForceEndAll();
                TurnManager.SyncMyBoardToOpponent();
                swapDone = true;
            }
        };

        yield return new WaitUntil(() => swapDone);
        BoardSlot.isStrengtheningSlot = false;
        SetHandAreaRaycast(true);
        ShowAllCards();
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        BoardSyncManager.MarkDirty();
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

        // [AI] 02310：AI 施法 → 直选 AI(0-5) 首个可聚光灯格
        if (SimpleAI.IsAIEvaluating)
        {
            BoardManager bmAI2310 = FindObjectOfType<BoardManager>();
            BoardSlot pick2310 = null;
            for (int s = 0; s <= 5; s++)
            {
                BoardSlot sl = bmAI2310?.GetSlot(s);
                if (sl != null && !sl.isBlocked && !sl.hasSpotlight) { pick2310 = sl; break; }
            }
            if (pick2310 != null)
            {
                pick2310.hasSpotlight = true;
                pick2310.spotlightTierBoost = 2;
                pick2310.spotlightSourceTemplateID = "02310";
                if (pick2310.currentCard3D != null)
                {
                    CardInstance ci = pick2310.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null)
                    {
                        ci.currentTier += 2;
                        ci.AddStatus(false, "阶位+2；每阶段开始恢复2生命值", "02310");
                        pick2310.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
            TurnManager.SyncMyBoardToOpponent();
            CardDrag.CleanupSpellResources();
            yield break;
        }

        BoardSlot.isStrengtheningSlot = true;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (selectedSlot) =>
        {
            if (selectedSlot != null && !selectedSlot.isBlocked && !selectedSlot.hasSpotlight)
            {
                selectedSlot.hasSpotlight = true;
                selectedSlot.spotlightTierBoost = 2;
                selectedSlot.spotlightSourceTemplateID = "02310"; // 4.3 聚光灯来源（法术离场记模板ID）
                if (selectedSlot.currentCard3D != null)
                {
                    CardInstance ci = selectedSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null)
                    {
                        ci.currentTier += 2;
                        // 4.2 聚光灯：给已占位卡记状态（换卡时随 setter 转移）
                        ci.AddStatus(false, "阶位+2；每阶段开始恢复2生命值", "02310");
                        selectedSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
            done = true;
            TurnManager.SyncMyBoardToOpponent();
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

        // 手牌+场上混合弹窗（01349 收藏家模式）：候选=己方召唤物(手牌 + 场上6-11)
        HandManager hmx = FindObjectOfType<HandManager>();
        List<CardInstance> candidates = hmx != null
            ? hmx.BuildHandPlusFieldCardList(
                ci => CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Summon)
            : new List<CardInstance>();
        if (candidates.Count == 0) { CardDrag.CleanupSpellResources(); yield break; }

        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(candidates, ci => true, () => confirmed = true, "进化");
        float evoDeadline = Time.time + 30f;
        while (!confirmed && Time.time < evoDeadline) yield return null;
        if (!confirmed) { hmx?.EndHandSelectionCleanup(); CardDrag.CleanupSpellResources(); yield break; }

        CardInstance chosen = CardDisplayPanel.Instance.GetSelectedCard();
        string iid = chosen != null ? chosen.instanceID : null;
        hmx?.EndHandSelectionCleanup();
        if (!string.IsNullOrEmpty(iid)) ApplyEvolutionEffectById(iid);
        CardDrag.CleanupSpellResources();
    }

    /// <summary>伟大进化结算（弹窗克隆→按 iid 回扫真身）：场/手牌阶位+3 并只同步对应端。</summary>
    void ApplyEvolutionEffectById(string iid)
    {
        HandManager hmh = FindObjectOfType<HandManager>();
        Card3DInstance c3d = hmh?.ResolveFieldCardByInstanceID(iid);
        CardInstance ci = c3d?.cardInstance;
        GameObject handCard = null;
        if (ci == null) { handCard = hmh?.ResolveHandCardByInstanceID(iid); ci = handCard?.GetComponent<CardInstance>(); }
        if (ci == null) return;

        ci.currentTier += 3;
        ci.baseTier += 3;
        if (c3d != null)
        {
            c3d.UpdateValues();
            TurnManager.SyncMyBoardToOpponent(); // 场上阶位走板面同步
        }
        else if (handCard != null)
        {
            handCard.GetComponent<CardDisplay2D>()?.Refresh();
            if (NetworkClient.isConnected) NetworkPlayer.Local?.CmdSetHandCardTier(ci.instanceID, ci.currentTier, ci.baseTier);
        }
        Debug.Log($"伟大进化：{ci.instanceID} 阶位永久+3");
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
            // 同步阶位变更
            if (c3d != null)
            {
                // 场上随从：板面同步携带当前 tier
                TurnManager.SyncMyBoardToOpponent();
            }
            else if (d2d != null && NetworkClient.isConnected)
            {
                // 手牌：通知服务器记录 tier 覆盖，放牌时 CmdPlayCard 自动应用
                NetworkPlayer.Local?.CmdSetHandCardTier(ci.instanceID, ci.currentTier, ci.baseTier);
            }
            Debug.Log($"伟大进化：{ci.instanceID} 阶位永久+3");
        }
    }

    public IEnumerator SummonCoreEffect()
    {
        CardData template = CardDatabase.Instance?.GetTemplate("03027");
        if (template?.prefab3D == null) { CardDrag.CleanupSpellResources(); yield break; }

        // [AI] 02212：直放核心(03027)到 AI(0-5) 首个空槽，并给 AI 场/手牌召唤物加"灵能"
        if (SimpleAI.IsAIEvaluating)
        {
            BoardManager bmAI2212 = FindObjectOfType<BoardManager>();
            if (bmAI2212 != null)
            {
                for (int s = 0; s <= 5; s++)
                {
                    BoardSlot sl = bmAI2212.GetSlot(s);
                    if (sl == null || sl.hasCard || sl.isBlocked || sl.prisonBlocked || sl.permaBlocked) continue;
                    GameObject tempAI = new GameObject("TempCoreAI");
                    CardInstance tiAI = tempAI.AddComponent<CardInstance>();
                    tiAI.InitFromTemplate(template, 0);
                    PlaceCardToSlot(sl, tempAI);
                    Destroy(tempAI);
                    if (NetworkClient.isConnected)
                        NetworkPlayer.Local?.CmdPlayCard("03027", sl.slotID,
                            tiAI.baseAttack, tiAI.baseHealth, tiAI.baseMaxHealth, tiAI.currentCost,
                            tiAI.instanceID ?? CardZoneManager.GenerateInstanceID("03027"));
                    break;
                }
                // 灵能：AI 场上(0-5)
                for (int s2 = 0; s2 <= 5; s2++)
                {
                    BoardSlot sl2 = bmAI2212.GetSlot(s2);
                    CardInstance ci2 = sl2?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci2 != null && (ci2.prefixes == null || !ci2.prefixes.Contains("灵能")))
                    { ci2.GivePrefix("灵能", "03027"); sl2.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues(); }
                }
            }
            // 灵能：AI 手牌
            NetworkPlayer aiH2212 = NetworkPlayer.Remote;
            if (aiH2212 != null)
                foreach (GameObject hc in aiH2212.handCards)
                {
                    if (hc == null) continue;
                    CardInstance hci = hc.GetComponent<CardInstance>();
                    if (hci != null && (hci.prefixes == null || !hci.prefixes.Contains("灵能")))
                        hci.GivePrefix("灵能", "03027");
                }
            CardDrag.CleanupSpellResources();
            yield break;
        }

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
            // 同步新召唤的核心到服务器/对端（PlaceCardToSlot 不调 CmdPlayCard）
            if (NetworkClient.isConnected)
                NetworkPlayer.Local?.CmdPlayCard("03027", selectedSlot.slotID,
                    ti.baseAttack, ti.baseHealth, ti.baseMaxHealth, ti.currentCost,
                    ti.instanceID ?? CardZoneManager.GenerateInstanceID("03027"));
            placed = true;
            SelectionManager.Instance.ForceEndAll();
            BoardSlot.isPlacingCard = false;
            BoardSlot.isStrengtheningSlot = false;

            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && !ci.prefixes.Contains("灵能"))
                {
                        ci.GivePrefix("灵能", "03027");
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
                        ci.GivePrefix("灵能", "03027");
                        CardDisplay2D d2d = handCard.GetComponent<CardDisplay2D>();
                        d2d?.Refresh();
                        if (NetworkClient.isConnected)
                            NetworkPlayer.Local?.CmdSetHandCardPrefix(ci.instanceID, "灵能");
                    }
                }
            }
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                if (card != null) card.SetActive(true);
            }
            RefreshLayout(true);
            CardDrag.CleanupSpellResources();

            // Sync core model + ally prefix changes to opponent
            TurnManager.SyncMyBoardToOpponent();
        };
        yield return new WaitUntil(() => placed);
    }
    // ═══════════════════ 手牌/手牌+场上 弹窗选择公共助手（01349 收藏家模式复用）═══════════════════

    /// <summary>组装"仅手牌"候选 CardInstance 列表（本地手牌；可选过滤）。供 CardDisplayPanel 弹窗。</summary>
    public List<CardInstance> BuildHandCardList(System.Func<CardInstance, bool> filter = null)
    {
        var list = new List<CardInstance>();
        if (NetworkPlayer.Local?.handCards == null) return list;
        foreach (var card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            var ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            if (filter != null && !filter(ci)) continue;
            list.Add(ci);
        }
        return list;
    }

    /// <summary>组装"手牌 + 场上"混合候选列表（side=己方半场起点，默认6；可选过滤作用于两源）。
    /// 场上以 Card3DInstance.cardInstance 入列，弹窗按模板重建 2D 卡展示。</summary>
    public List<CardInstance> BuildHandPlusFieldCardList(System.Func<CardInstance, bool> filter = null, int side = 6)
    {
        var list = BuildHandCardList(filter);
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) return list;
        for (int i = side; i < side + 6; i++)
        {
            var ci = bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null) continue;
            if (filter != null && !filter(ci)) continue;
            list.Add(ci);
        }
        return list;
    }

    /// <summary>按 instanceID 回找本地手牌里的真身 GameObject（弹窗返回的是克隆）。找不到 → null。</summary>
    public GameObject ResolveHandCardByInstanceID(string iid)
    {
        if (string.IsNullOrEmpty(iid) || NetworkPlayer.Local?.handCards == null) return null;
        foreach (var card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            if (card.GetComponent<CardInstance>()?.instanceID == iid) return card;
        }
        return null;
    }

    /// <summary>按 instanceID 回找 12 槽/附着模型里的场上真身 Card3DInstance。找不到 → null。</summary>
    public Card3DInstance ResolveFieldCardByInstanceID(string iid)
    {
        if (string.IsNullOrEmpty(iid)) return null;
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        for (int i = 0; i < 12; i++)
        {
            var c3d = bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance != null && c3d.cardInstance.instanceID == iid) return c3d;
        }
        if (bm.attachedModels != null)
            foreach (var obj in bm.attachedModels)
            {
                var c3d = obj?.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance != null && c3d.cardInstance.instanceID == iid) return c3d;
            }
        return null;
    }

    /// <summary>弹窗选择结束的通用收尾（01349 同款 7 步：Hide/复位 multiSelect/恢复手牌/射线/重排/按钮/allowDiscard）。</summary>
    public void EndHandSelectionCleanup()
    {
        if (CardDisplayPanel.Instance != null)
        {
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
        }
        if (NetworkPlayer.Local?.handCards != null)
            foreach (var c in NetworkPlayer.Local.handCards) { if (c != null) c.SetActive(true); }
        SetHandAreaRaycast(true);
        RefreshLayout(true);
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        Card3DHover.allowDiscard = true;
    }

    /// <summary>01349 收藏家 AI：从 AI 手牌挑 1 张(1/3/5 优先)消耗 → giver +1攻/+1能量（沿用消耗单张语义）。</summary>
    IEnumerator CollectorConsumeAI(CardInstance giver)
    {
        NetworkPlayer ai1349 = NetworkPlayer.Remote;
        if (ai1349 == null || giver == null) yield break;

        CardInstance pick1349 = null;
        int bestRank1349 = int.MaxValue;
        int[] pref1349 = { 1, 3, 5 };
        foreach (GameObject h in ai1349.handCards)
        {
            if (h == null) continue;
            CardInstance c1349 = h.GetComponent<CardInstance>();
            if (c1349 == null) continue;
            int r1349 = System.Array.IndexOf(pref1349, c1349.currentCost);
            if (r1349 < 0) r1349 = pref1349.Length;
            if (r1349 < bestRank1349) { bestRank1349 = r1349; pick1349 = c1349; }
        }
        if (pick1349 == null) yield break;

        ai1349.handCards.Remove(pick1349.gameObject);
        giver.currentAttack += 1;
        giver.baseAttack += 1;
        ai1349.AddEnergy(1);

        BoardManager bm1349 = FindObjectOfType<BoardManager>();
        if (bm1349 != null)
            for (int i = 0; i < 12; i++)
            {
                BoardSlot s1349 = bm1349.GetSlot(i);
                if (s1349?.currentCard3D != null && s1349.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance == giver)
                { s1349.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues(); break; }
            }
        TurnManager.SyncMyBoardToOpponent();
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

        // AI 放收集者（AI 半场 0-5）：从 AI 手牌挑 1 张(1/3/5)消耗 → +1 攻 +1 能量（不弹窗/不用玩家手牌）
        if (SimpleAI.IsAIMatch)
        {
            BoardManager cbmAI = FindObjectOfType<BoardManager>();
            bool isAiCollector = false;
            for (int i = 0; i < 6; i++)
                if (cbmAI?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == giver)
                { isAiCollector = true; break; }
            if (isAiCollector)
            {
                yield return CollectorConsumeAI(giver);
                yield break;
            }
        }

        CardDisplayPanel.Instance.multiSelect = true;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(displayList, ci => true, () =>
        {
            confirmed = true;
        }, "召唤");
        float collectorDeadline = Time.time + 30f;
        while (!confirmed && Time.time < collectorDeadline)
            yield return null;
        if (!confirmed)
        {
            confirmed = true;
            Debug.LogWarning("[Effect] 01349 收集者确认超时，AI兜底确认");
        }

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

        // [AI/侧向] 返费给该卡 owner（AI 01322 → Remote）
        NetworkPlayer refundOwner1322 = null;
        BoardSlot rfSlot1322 = FindSlotOf(refundTarget) ?? FindSlotOf(returnTarget);
        if (rfSlot1322 != null) refundOwner1322 = BoardManager.GetOwnerPlayer(rfSlot1322.slotID);
        if (refundOwner1322 != null) refundOwner1322.AddEnergy(refundTarget.currentCost);
        else NetworkPlayer.Local?.AddEnergy(refundTarget.currentCost);

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
            NetworkPlayer.ReturnCardToOwner(returnTemplate, returnTarget); // 回手按 owner 分流（AI→AI手牌）

        // 01322 进场完成，恢复界面状态
        BoardSlot anySlot = FindSlotOf(returnTarget) ?? FindSlotOf(refundTarget) ?? returnSlot ?? refundSlot;
        if (anySlot != null) anySlot.CleanupAfterPlacement();
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

        // [AI] 02403：AI 施法 → 直放 03010 到 AI(0-5) 首个空槽，不弹玩家
        if (SimpleAI.IsAIEvaluating)
        {
            BoardManager bmAI2403 = FindObjectOfType<BoardManager>();
            for (int s = 0; s <= 5; s++)
            {
                BoardSlot sl = bmAI2403?.GetSlot(s);
                if (sl == null || sl.hasCard || sl.isBlocked || sl.prisonBlocked || sl.permaBlocked) continue;
                GameObject tempAI = new GameObject("TempSmallEvilAI");
                CardInstance tiAI = tempAI.AddComponent<CardInstance>();
                tiAI.InitFromTemplate(template, 0);
                PlaceCardToSlot(sl, tempAI);
                Destroy(tempAI);
                if (NetworkClient.isConnected)
                    NetworkPlayer.Local?.CmdPlayCard("03010", sl.slotID,
                        tiAI.baseAttack, tiAI.baseHealth, tiAI.baseMaxHealth, tiAI.currentCost,
                        tiAI.instanceID ?? CardZoneManager.GenerateInstanceID("03010"));
                break;
            }
            CardDrag.CleanupSpellResources();
            yield break;
        }

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
            // 同步新召唤物到服务器/对端（PlaceCardToSlot 不调 CmdPlayCard）
            if (NetworkClient.isConnected)
                NetworkPlayer.Local?.CmdPlayCard("03010", selectedSlot.slotID,
                    ti.baseAttack, ti.baseHealth, ti.baseMaxHealth, ti.currentCost,
                    ti.instanceID ?? CardZoneManager.GenerateInstanceID("03010"));
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

        // [AI] 01534：抛置者属 AI 侧(0-5) → 直放两只可怖之物到 AI 空槽，不弹玩家放置
        bool aiH1534 = SimpleAI.IsAIMatch && Card3DHover.ignoreSlotID < 6;
        if (aiH1534)
        {
            BoardManager bmH1534 = FindObjectOfType<BoardManager>();
            int placedH1534 = 0;
            for (int s = 0; s <= 5 && placedH1534 < 2; s++)
            {
                BoardSlot sl = bmH1534?.GetSlot(s);
                if (sl == null || sl.hasCard || sl.isBlocked || sl.prisonBlocked || sl.permaBlocked) continue;
                PlaceHorror(sl, horrorTemplate, baseHP, baseAtk, placedH1534);
                placedH1534++;
                yield return null;
            }
            BoardSyncManager.MarkDirty();
            yield break;
        }

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
        BoardSyncManager.MarkDirty();
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
    CardInstance FindRebornOnField(int soldierSlotID = -1)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (soldierSlotID >= 0)
        {
            // 只在杂兵同侧搜索 01513——避免服务端远端半场视角不一致
            BoardManager.GetSideRange(soldierSlotID, out int s, out int e);
            for (int i = s; i <= e; i++)
            {
                BoardSlot slot = bm?.GetSlot(i);
                if (slot?.currentCard3D != null)
                {
                    CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.templateID == "01513") return ci;
                }
            }
            return null;
        }
        // 兼容旧调用：默认搜 6-11
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
        // [AI] 02501：AI 施法 → 自伤AI + Remote 手牌 Summon 贪心"和≤8(高费优先)"子集 → 依次放到 AI(0-5) 空槽
        if (SimpleAI.IsAIEvaluating)
        {
            NetworkPlayer.Remote?.TakeDamage(1, "02501", null, null);
            DamageFX.Request(DamageFX.GetPlayerWorldPos(false), 1, FloaterType.Damage, DamageFxSource.Self, 0, -1, true);
            NetworkPlayer ai2501 = NetworkPlayer.Remote;
            if (ai2501 != null)
            {
                var pool2501 = new List<(CardInstance c, CardData td)>();
                foreach (GameObject h in ai2501.handCards)
                {
                    if (h == null) continue;
                    CardInstance c = h.GetComponent<CardInstance>();
                    if (c == null) continue;
                    CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
                    if (td != null && td.cardType == CardType.Summon) pool2501.Add((c, td));
                }
                pool2501.Sort((a, b) => b.td.baseCost.CompareTo(a.td.baseCost));
                var chosen2501 = new List<(CardInstance c, CardData td)>();
                int sum2501 = 0;
                foreach (var it in pool2501)
                {
                    if (sum2501 + it.td.baseCost <= 8) { sum2501 += it.td.baseCost; chosen2501.Add(it); }
                }
                BoardManager bm2501 = FindObjectOfType<BoardManager>();
                foreach (var it in chosen2501)
                {
                    CardInstance ci2501 = it.c;
                    // 放到 AI 第一个空槽
                    BoardSlot emp2501 = null;
                    for (int s = 0; s <= 5; s++)
                    {
                        BoardSlot sl = bm2501?.GetSlot(s);
                        if (sl != null && !sl.hasCard && !sl.isBlocked && !sl.prisonBlocked && !sl.permaBlocked) { emp2501 = sl; break; }
                    }
                    if (emp2501 == null) break;
                    for (int i = ai2501.handCards.Count - 1; i >= 0; i--)
                        if (ai2501.handCards[i] != null && ai2501.handCards[i].GetComponent<CardInstance>() == ci2501)
                        { ai2501.handCards.RemoveAt(i); break; }
                    CardDisplayPanel.Instance.multiSelect = false;
                    PlaceCardToSlot(emp2501, ci2501.gameObject);
                    BoardSyncManager.MarkDirty();
                    yield return null;
                }
            }
            CardDisplayPanel.Instance.multiSelect = false;
            CardDrag.CleanupSpellResources();
            yield break;
        }

        NetworkPlayer.Local.TakeDamage(1, "02501"); // 传送门自伤，来源=法术02501
        DamageFX.Request(DamageFX.GetPlayerWorldPos(false), 1, FloaterType.Damage, DamageFxSource.Self, 0, -1, true); // 英雄自伤特殊轨迹
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
    /// 瘟疫(02408)选敌守卫：排除敌方免疫卡(征服者01508)（第二步关卡3）。true=允许。
    static bool PlagueExcludeImmuneFilter(BoardSlot s)
    {
        if (s?.currentCard3D == null) return true;
        var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
        return ci == null || !ci.ImmuneToEnemySpells;
    }

    public IEnumerator PlagueEffect()
    {
        // [AI] 02408：AI 施法 → 直选 玩家方(6-11) 两个合法空格（排除免疫），不弹玩家
        if (SimpleAI.IsAIEvaluating)
        {
            BoardManager bmAI2408 = FindObjectOfType<BoardManager>();
            List<BoardSlot> pick2408 = new List<BoardSlot>();
            for (int s = 6; s <= 11 && pick2408.Count < 2; s++)
            {
                BoardSlot sl = bmAI2408?.GetSlot(s);
                if (sl == null || sl.isBlocked || !PlagueExcludeImmuneFilter(sl)) continue;
                pick2408.Add(sl);
            }
            foreach (BoardSlot p in pick2408)
            {
                p.hasPlague = true;
                p.plagueRoundCount = 1;
                p.plagueSourceTemplateID = "02408";
            }
            TurnManager.SyncMyBoardToOpponent();
            CardDrag.CleanupSpellResources();
            yield break;
        }

        // 第一次：隐藏手牌（征服者免疫：瘟疫不可选中敌方免疫卡01508）
        BoardSlot.extraTargetFilter = PlagueExcludeImmuneFilter;
        BoardSlot first = null;
        bool firstDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.isBlocked && PlagueExcludeImmuneFilter(s)) { first = s; firstDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => firstDone);
        if (first == null) { BoardSlot.extraTargetFilter = null; CardDrag.CleanupSpellResources(); yield break; }

        // 第二次：隐藏手牌（同样排除免疫卡 + 不重复选同一格）
        BoardSlot second = null;
        bool secondDone = false;
        BoardSlot.extraTargetFilter = (s) => s != first && PlagueExcludeImmuneFilter(s);
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.isBlocked && s != first && PlagueExcludeImmuneFilter(s)) { second = s; secondDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => secondDone);
        BoardSlot.extraTargetFilter = null;
        if (second == null) { CardDrag.CleanupSpellResources(); yield break; }

        first.hasPlague = true;
        first.plagueRoundCount = 1;
        first.plagueSourceTemplateID = "02408"; // 来源=瘟疫法术（离场，记模板ID）
        second.hasPlague = true;
        second.plagueRoundCount = 1;
        second.plagueSourceTemplateID = "02408";
        // hasPlague/plagueRoundCount 已在 BoardSyncManager 同步管道中，只需触发上报
        TurnManager.SyncMyBoardToOpponent();

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

                myInst.currentAttack += 1;
                mySlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }

            BoardSlot.CheckAndHandleDeaths();
            TurnManager.SyncMyBoardToOpponent();
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

        // [AI] 02302：AI 施法 → 自动选"玩家最后放置"的反制（enemyCounters 末位），不弹玩家点选
        if (SimpleAI.IsAIEvaluating)
        {
            CounterCard aiCK2302 = enemyCounters[enemyCounters.Count - 1];
            if (aiCK2302 != null)
            {
                CounterManager.Instance.TriggerEnemyCounterNoEffect(aiCK2302);
                CounterManager.Instance.PlayCounterWithReducedCost(aiCK2302.template, 1);
            }
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

        // 找守望者(01339)：0-5(AI) 优先（玩家打出时触发 AI 守望者），否则 6-11(Host)
        CardInstance watcher = null;
        int watcherSlot = -1;
        for (int i = 0; i <= 11; i++)
        {
            if (watcherSlot >= 0 && i > 5 && watcherSlot < 6) break; // 已在 0-5 找到就不扫 6-11
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isWatcher) { watcher = ci; watcherSlot = i; break; }
            }
        }
        if (watcher == null) return;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(watcher)) return;

        // 目标 = watcher 的对侧（AI watcher(0-5)→玩家6-11；Host watcher→0-5）
        int tStart = watcherSlot < 6 ? 6 : 0;

        // [AI] watcher 属 AI 侧且当前由玩家打出触发 → 自动选（否则会弹给玩家）
        if (watcherSlot < 6)
        {
            BoardSlot bestW = null;
            for (int i = tStart; i < tStart + 6; i++)
            {
                BoardSlot s = bm?.GetSlot(i);
                if (s?.currentCard3D != null) { bestW = s; break; }
            }
            if (bestW?.currentCard3D != null)
            {
                Card3DInstance t3d = bestW.currentCard3D.GetComponent<Card3DInstance>();
                if (t3d?.cardInstance != null)
                {
                    BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 1, null);
                    t3d.UpdateValues();
                    BoardSlot.CheckAndHandleDeaths();
                }
            }
            return;
        }

        bool hasEnemy = false;
        for (int i = tStart; i < tStart + 6; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }
        if (!hasEnemy) return;

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

        // [AI] 02010：AI 施法 → 直放叛徒到 玩家方(6-11) 首个空槽（不弹玩家；人类侧敌方为 0-5）
        if (SimpleAI.IsAIEvaluating)
        {
            BoardManager bmAI2010 = FindObjectOfType<BoardManager>();
            for (int s = 6; s <= 11; s++)
            {
                BoardSlot sl = bmAI2010?.GetSlot(s);
                if (sl == null || sl.hasCard || sl.isBlocked || sl.prisonBlocked || sl.permaBlocked) continue;
                GameObject tempAI = new GameObject("TempTraitorAI");
                CardInstance tiAI = tempAI.AddComponent<CardInstance>();
                tiAI.InitFromTemplate(traitorTemplate, 0);
                PlaceCardToSlot(sl, tempAI);
                Destroy(tempAI);
                sl.currentCard3D.transform.rotation = Quaternion.Euler(0, 180, 0);
                if (NetworkClient.isConnected)
                    NetworkPlayer.Local?.CmdPlayCard("03025", sl.slotID,
                        tiAI.baseAttack, tiAI.baseHealth, tiAI.baseMaxHealth, tiAI.currentCost,
                        tiAI.instanceID ?? CardZoneManager.GenerateInstanceID("03025"));
                break;
            }
            CardDrag.CleanupSpellResources();
            yield break;
        }

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

            // 同步至服务器/对端——PlaceCardToSlot 不调 CmdPlayCard，需手动发出
            if (NetworkClient.isConnected)
            {
                int atk = ti.baseAttack;
                int hp = ti.baseHealth;
                int maxHp = ti.baseMaxHealth;
                int cost = ti.currentCost;
                string iid = ti.instanceID ?? CardZoneManager.GenerateInstanceID("03025");
                NetworkPlayer.Local?.CmdPlayCard("03025", selectedSlot.slotID, atk, hp, maxHp, cost, iid);
            }

            placed = true;
            BoardSlot.isPlacingCard = false;
            BoardSlot.isStrengtheningSlot = false;
        });

        yield return new WaitUntil(() => placed);
        CardDrag.CleanupSpellResources();
    }
}