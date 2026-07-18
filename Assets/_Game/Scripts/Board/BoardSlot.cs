using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CardData;

public class BoardSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int slotID;
    public int opponentSlotID;
    public bool isBlocked = false;
    public bool hasCard = false;
    public static System.Func<BoardSlot, bool> extraTargetFilter;
    public int spotlightTierBoost;   // 聚光灯阶位增幅
    public bool hasSpotlight;        // 是否有聚光灯效果
    private static float lastClickTime = 0f;
    public int plagueRoundCount;
    public bool hasPlague;
    public static bool isTargetingMode
    {
        get => SelectionManager.Instance != null && SelectionManager.Instance.IsSelecting;
        set { }
    }

    private static bool _isPlacingCard = false;
    private static bool _isReplaceMode = false;
    private static bool _isAttachSelectMode = false;

    public static bool isPlacingCard
    {
        get => _isPlacingCard;
        set => _isPlacingCard = value;
    }
    public static bool isReplaceMode
    {
        get => _isReplaceMode;
        set => _isReplaceMode = value;
    }
    public static bool isAttachSelectMode
    {
        get => _isAttachSelectMode;
        set => _isAttachSelectMode = value;
    }

    public static GameObject cardToPlace = null;
    public static TargetType currentTargetType = TargetType.None;
    public static Action<BoardSlot> onTargetSelected;

    private Vector3 originalScale;
    public Image slotImage;
    public Color normalColor;
    public Color highlightColor = Color.yellow;

    public static bool attachCanBeIndependent = false;
    public int slotTempAttackBoost;
    private GameObject _currentCard;
    public static bool isStrengtheningSlot = false;

    /// <summary>退场后待处理的反击队列（同时窗口分界线）。
    /// 存储(死卡槽位ID, 反击效果文本, 伤害来源实例ID列表)。</summary>
    public static List<(int deadSlotID, string revengeEffect, List<string> sourceInstanceIDs)> pendingRevenges
        = new List<(int, string, List<string>)>();
    public bool prisonBlocked;      // 囚牢封锁
    public bool prisonAllowYuan;    // 允许放置渊前缀召唤物（仅己方封锁格子）
    public int deepSeaAttackDebuff; // 格子攻击力减益
    public bool deepSeaHealthDebuff; // 格子每阶段扣血标记
    public static int ignoreNextClickSlot = -1;
    void Start()
    {
        currentCard3D = null;
        slotImage = GetComponent<Image>();
        originalScale = transform.localScale;
        normalColor = slotImage.color;
    }
    // 从CardInstance提取数据包
    public class DeathEffectData
    {
      
        public int slotID;
        public string templateID;
        public string instanceID;
        public bool isActiveExit;
        public bool hasOnDeath;
        public bool hasActiveExit;
        public bool hasRevenge;
        public bool hasFirstStrike;
        public bool hasOnEnter;
        public bool hasDiscard;
        public string revengeEffect;
        public List<string> giveableDeathTraits;
        public List<string> grantedTraitTexts;
        public List<string> damageSourceInstanceIDs;
        public bool handledReturnToHand;
        public bool silencedThisPhase;
        public bool isFullySilenced;
        public bool isDeathBlocked;
        
        public int currentCost;
        public int currentAttack;
        public int currentHealth;
        public int currentMaxHealth;
        public int currentTier;
        public string prefixes;
        public SummonType summonType;
   
        public bool poisoned;
        public bool isXValue;
        public bool xAttackReadsHighest;
        public bool xHealthReadsHighest;
        public int xAccumulatedDamage;
        public int xInitialHealth;
        public int tempAttackBoost;
        public int tempHealthBoost;
        public bool hasShield;
        public bool shieldIsPermanent;
        public bool shieldEndAtBattleStart;
        public bool shieldEndAtBattleEnd;
        public bool isAttached;
        public int hostSlotID;
        public int attachOrder;
        public bool canAttach;
        public bool attacksFrontRow;
        public bool attacksBackRow;
        public bool isYinYang;
        public bool buffedBySage;
        public bool buffedByEmperor;
        public bool overclocked;
        public bool cannotHeal;
        public string braveTemplateID;
        public int greedySnakeEnterCount;
        public bool merchantDiscounted;
        public bool energyReaperDiscounted;
        public bool _justTransformed;
        public int prisonMySlot;
        public int prisonEnemySlot;
        public int ironSmithTotalConsumedCount;
        public int ironSmithOneCostConsumedCount;
        public bool _conductorDoubleDeath;
        public int scrollCorePhaseCount;
  
    }

    // 从CardInstance提取数据包
    public static DeathEffectData ExtractDeathData(CardInstance ci)
    {
        if (ci == null) return null;
        return new DeathEffectData
        {
            hasActiveExit = ci.hasActiveExit,
            hasRevenge = ci.hasRevenge,
            templateID = ci.templateID,
            isActiveExit = ci.isActiveExit,
            hasOnDeath = ci.hasOnDeath,
            revengeEffect = ci.revengeEffect,
            giveableDeathTraits = ci.giveableDeathTraits != null ? new List<string>(ci.giveableDeathTraits) : null,
            grantedTraitTexts = ci.grantedTraitTexts != null ? new List<string>(ci.grantedTraitTexts) : null,
            hasFirstStrike = ci.hasFirstStrike,
            hasOnEnter = ci.hasOnEnter,
            hasDiscard = ci.hasDiscard,
            currentCost = ci.currentCost,
            currentAttack = ci.currentAttack,
            currentHealth = ci.currentHealth,
            currentMaxHealth = ci.currentMaxHealth,
            currentTier = ci.currentTier,
            prefixes = ci.prefixes,
            summonType = ci.summonType,
            handledReturnToHand = ci.handledReturnToHand,
            silencedThisPhase = ci.silencedThisPhase,
            poisoned = ci.poisoned,
            isXValue = ci.isXValue,
            xAttackReadsHighest = ci.xAttackReadsHighest,
            xHealthReadsHighest = ci.xHealthReadsHighest,
            xAccumulatedDamage = ci.xAccumulatedDamage,
            xInitialHealth = ci.xInitialHealth,
            tempAttackBoost = ci.tempAttackBoost,
            tempHealthBoost = ci.tempHealthBoost,
            hasShield = ci.hasShield,
            shieldIsPermanent = ci.shieldIsPermanent,
            shieldEndAtBattleStart = ci.shieldEndAtBattleStart,
            shieldEndAtBattleEnd = ci.shieldEndAtBattleEnd,
            isAttached = ci.isAttached,
            hostSlotID = ci.hostSlotID,
            attachOrder = ci.attachOrder,
            canAttach = ci.canAttach,
            attacksFrontRow = ci.attacksFrontRow,
            attacksBackRow = ci.attacksBackRow,
            isYinYang = ci.isYinYang,
            buffedBySage = ci.buffedBySage,
            buffedByEmperor = ci.buffedByEmperor,
            overclocked = ci.overclocked,
            cannotHeal = ci.cannotHeal,
            braveTemplateID = ci.braveTemplateID,
            greedySnakeEnterCount = ci.greedySnakeEnterCount,
            merchantDiscounted = ci.merchantDiscounted,
            energyReaperDiscounted = ci.energyReaperDiscounted,
            _justTransformed = ci._justTransformed,
            prisonMySlot = ci.prisonMySlot,
            prisonEnemySlot = ci.prisonEnemySlot,
            ironSmithTotalConsumedCount = ci.ironSmithTotalConsumedCount,
            ironSmithOneCostConsumedCount = ci.ironSmithOneCostConsumedCount,
            _conductorDoubleDeath = ci._conductorDoubleDeath,
            scrollCorePhaseCount = ci.scrollCorePhaseCount,
            instanceID = ci.instanceID,
            damageSourceInstanceIDs = ci.damageSourceInstanceIDs != null ? new List<string>(ci.damageSourceInstanceIDs) : null,
            isFullySilenced = GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci),
            isDeathBlocked = GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(ci, "退场"),
        };
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isBlocked) return;

        if (prisonBlocked)
        {
            if (isPlacingCard && slotID >= 6 && prisonAllowYuan)
            {
                CardInstance ci = cardToPlace?.GetComponent<CardInstance>();
                if (ci != null && ci.prefixes.Contains("渊"))
                {
                    transform.localScale = originalScale * 1.15f;
                    slotImage.color = highlightColor;
                    return;
                }
            }
            transform.localScale = originalScale;
            slotImage.color = new Color(0.6f, 0.2f, 0.8f);
            return;
        }

        if (hasPlague)
        {
            slotImage.color = Color.green;
            return;
        }

        if (isPlacingCard && !isReplaceMode)
        {
            FakeEnemyPlayButton.GetSlotRange(out int minSlot, out int maxSlot);
            if (slotID >= minSlot && slotID <= maxSlot && !hasCard)
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
        }
        if (isPlacingCard && isReplaceMode)
        {
            FakeEnemyPlayButton.GetSlotRange(out int minSlot, out int maxSlot);
            if (slotID >= minSlot && slotID <= maxSlot && hasCard)
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
        }
        if (isAttachSelectMode && slotID >= 6)
        {
            if (hasCard || (attachCanBeIndependent && !hasCard))
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
        }
        if (isTargetingMode && !isAttachSelectMode && !isReplaceMode && IsValidTarget(currentTargetType))
        {
            if (currentTargetType == TargetType.SingleAlly || currentTargetType == TargetType.SingleEnemy || currentTargetType == TargetType.AllMinions)
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
            else
            {
                HighlightRow(true);
            }
        }
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isBlocked)
        {
            slotImage.color = Color.black;
            return;
        }

        if (prisonBlocked)
        {
            slotImage.color = new Color(0.6f, 0.2f, 0.8f);
            return;
        }

        if (hasPlague)
        {
            slotImage.color = Color.green;
            return;
        }

        if (isTargetingMode && IsValidTarget(currentTargetType))
            HighlightRow(false);

        transform.localScale = originalScale;
        if (isBlocked) slotImage.color = Color.gray;
        else if (prisonBlocked) slotImage.color = new Color(0.6f, 0.2f, 0.8f);
        else if (hasPlague) slotImage.color = Color.green;
        else slotImage.color = normalColor;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (CardView.IsAnyCardDragging || Time.time - lastClickTime < 0.2f)
        {
            return;
        }
        lastClickTime = Time.time;
        if (isTargetingMode && IsValidTarget(currentTargetType))
        {
            onTargetSelected?.Invoke(this);
            return;
        }

        if (isPlacingCard && isReplaceMode && slotID >= 6 && hasCard && cardToPlace != null)
        {
            CardInstance inst = cardToPlace.GetComponent<CardInstance>();
            if (inst != null && inst.canAttach && attachCanBeIndependent)
            {
                ReplaceOrAttachModal.Instance.Show(
                    onReplace: () => { ExecuteReplace(this); },
                    onAttach: () => { ExecuteAttach(this); }
                );
            }
            else
            {
                ExecuteReplace(this);
            }
            return;
        }
        FakeEnemyPlayButton.GetSlotRange(out int minSlot, out int maxSlot);
        if (isPlacingCard && slotID >= minSlot && slotID <= maxSlot && !hasCard && !isReplaceMode && cardToPlace != null)
        {
            if (isBlocked) return;
            if (prisonBlocked && slotID >= 6 && prisonAllowYuan)
            {
                CardInstance checkCI = cardToPlace?.GetComponent<CardInstance>();
                if (checkCI == null || !checkCI.prefixes.Contains("渊")) return;
            }
            else if (prisonBlocked)
            {
                return;
            }

            if (ignoreNextClickSlot >= 0 && slotID == ignoreNextClickSlot)
            {
                ignoreNextClickSlot = -1;
                return;
            }
            ignoreNextClickSlot = -1;

            string playTemplateID = "";
            CardInstance ciPre = cardToPlace?.GetComponent<CardInstance>();
            if (ciPre != null) playTemplateID = ciPre.templateID;

            bool wasAttachFlow = ciPre != null && ciPre.canAttach;

            HandManager hm = FindObjectOfType<HandManager>();
            if (hm != null)
            {
                hm.PlaceCardToSlot(this, cardToPlace);

                CardInstance inst = currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                CardData template = CardDatabase.Instance?.GetTemplate(inst?.templateID);

                // 蛊惑之音重定向：生命值降为1
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.PendingEnterRedirectTemplate != null
                    && template == GlobalEventManager.Instance.PendingEnterRedirectTemplate)
                {
                    GlobalEventManager.Instance.PendingEnterRedirectInstance = inst;
                    inst.currentHealth = 1;
                    inst.currentMaxHealth = Mathf.Max(1, inst.currentMaxHealth);
                    currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }

                HandManager hmX = FindObjectOfType<HandManager>();
                if (hmX != null)
                {
                    BoardManager bmX = FindObjectOfType<BoardManager>();
                    if (bmX != null)
                    {
                        for (int i = 6; i <= 11; i++)
                        {
                            BoardSlot slotX = bmX.GetSlot(i);
                            if (slotX?.currentCard3D == null) continue;
                            CardInstance ciX = slotX.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ciX != null && ciX.isXValue)
                                hmX.UpdateXValues(ciX);
                        }
                    }
                }

                isPlacingCard = false;
                cardToPlace = null;

                // 进场效果前检查反制牌（02304 蛊惑之音等需在进场前触发重定向）。
                // Host/server 侧同步触发——否则 GlobalEventManager 重定向标记来不及被 StartOnEnterEffect 读到。
                // 无畏者(01319)不触发任何反制牌，跳过。
                if (NetworkServer.active && template != null && template.hasOnEnter
                    && template.templateID != "01319")
                {
                    CounterManager.Instance?.ServerCheckOnCardPlayed(template, true);
                }

                if (template != null && template.hasOnEnter && inst != null)
                {
                    StartOnEnterEffect(template, inst);
                }

                // 清理重定向标记
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.PendingEnterRedirectTemplate == template)
                {
                    GlobalEventManager.Instance.PendingEnterRedirectTemplate = null;
                    GlobalEventManager.Instance.PendingEnterRedirectInstance = null;
                }
            }

            // 附着流程：模型尚未放置（PlaceCardToSlot 异步等待选择目标），由 HandManager.PlaceCardToSlot 回调处理同步和清理
            if (wasAttachFlow) return;

            // Sync to remote client after placement is fully complete.
            if (NetworkClient.isConnected && !string.IsNullOrEmpty(playTemplateID))
            {
                Card3DInstance placedC3D = currentCard3D?.GetComponent<Card3DInstance>();
                int atk = placedC3D?.cardInstance?.currentAttack ?? -1;
                int hp = placedC3D?.cardInstance?.currentHealth ?? -1;
                int maxHp = placedC3D?.cardInstance?.currentMaxHealth ?? -1;
                string iid = placedC3D?.cardInstance?.instanceID ?? "";
                NetworkPlayer.Local?.CmdPlayCard(playTemplateID, slotID, atk, hp, maxHp, iid);
                BoardSyncManager.MarkDirty();
            }

            // 协程型进场效果尚未完成——跳过 CleanupAfterPlacement，由协程末尾自行清理
            CardInstance ciAfter = currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ciAfter != null && ciAfter._hasPendingCoroutine) return;

            CleanupAfterPlacement();
            return;
        }
    }

    bool IsValidTarget(TargetType type)
    {
        if (isAttachSelectMode)
        {
            if (slotID >= 6 && slotID <= 11)
            {
                if (hasCard) return true;
                if (attachCanBeIndependent) return true;
            }
            return false;
        }
        int[] ids = GetRowSlots(type);
        foreach (int id in ids)
        {
            if (id == slotID)
            {
                if (extraTargetFilter != null && !extraTargetFilter(this)) return false;
                return true;
            }
        }
        return false;
    }

    int[] GetRowSlots(TargetType type)
    {
        switch (type)
        {
            case TargetType.EnemyFrontRow: return new int[] { 0, 1, 2 };
            case TargetType.EnemyBackRow: return new int[] { 3, 4, 5 };
            case TargetType.AllyFrontRow: return new int[] { 6, 7, 8 };
            case TargetType.AllyBackRow: return new int[] { 9, 10, 11 };
            case TargetType.AllEnemies: return new int[] { 0, 1, 2, 3, 4, 5 };
            case TargetType.AllAllies: return new int[] { 6, 7, 8, 9, 10, 11 };
            case TargetType.EnemyAnyRow:
                if (slotID >= 0 && slotID <= 5) return new int[] { slotID < 3 ? 0 : 3, slotID < 3 ? 1 : 4, slotID < 3 ? 2 : 5 };
                break;
            case TargetType.AllyAnyRow:
                if (slotID >= 6 && slotID <= 11) return new int[] { slotID < 9 ? 6 : 9, slotID < 9 ? 7 : 10, slotID < 9 ? 8 : 11 };
                break;
            case TargetType.AllMinions:
                return new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            case TargetType.SingleEnemy:
                if (isStrengtheningSlot)
                {
                    if (slotID >= 0 && slotID <= 5 && !isBlocked) return new int[] { slotID };
                }
                else
                {
                    if (slotID >= 0 && slotID <= 5 && hasCard) return new int[] { slotID };
                }
                break;
            case TargetType.SingleAlly:
                if (isStrengtheningSlot)
                {
                    if (slotID >= 6 && slotID <= 11 && !isBlocked) return new int[] { slotID };
                }
                else
                {
                    if (slotID >= 6 && slotID <= 11 && hasCard) return new int[] { slotID };
                }
                break;

        }
        return new int[0];
    }

    void HighlightRow(bool highlight)
    {
        if (currentTargetType == TargetType.SingleAlly || currentTargetType == TargetType.SingleEnemy)
        {
            transform.localScale = highlight ? originalScale * 1.15f : originalScale;
            slotImage.color = highlight ? highlightColor : normalColor;
            return;
        }
        int[] rowSlots = GetRowSlots(currentTargetType);
        if (rowSlots == null) return;
        foreach (int id in rowSlots)
        {
            BoardSlot slot = FindObjectOfType<BoardManager>()?.GetSlot(id);
            if (slot != null)
            {
                slot.transform.localScale = highlight ? originalScale * 1.15f : originalScale;
                slot.slotImage.color = highlight ? highlightColor : normalColor;
            }
        }
    }

    public void StartOnEnterEffect(CardData template, CardInstance inst)
    {
       
        Debug.Log($"StartOnEnterEffect: template={template?.cardName}, templateID={template?.templateID}");
        if (template == null || string.IsNullOrEmpty(template.templateID)) return;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(inst, "进场"))
        {
            CleanupAfterPlacement();
            return;
        }
                // 清理重定向标记
        if (GlobalEventManager.Instance?.PendingEnterRedirectInstance == inst)
        {
            CardData redirectTemplate = GlobalEventManager.Instance.PendingEnterRedirectTemplate;
            bool redirectToHost = GlobalEventManager.Instance.PendingEnterRedirectToHost;
            GlobalEventManager.Instance.PendingEnterRedirectTemplate = null;
            GlobalEventManager.Instance.PendingEnterRedirectInstance = null;

            // 根据反制者所属半场选择目标类型：
            //   Host 反制 → 选 Host 己方 (6-11) = SingleAlly
            //   Remote 反制 → 选 Remote 己方，对 Host 来说是 SingleEnemy (0-5)
            TargetType redirectTargetType = redirectToHost ? TargetType.SingleAlly : TargetType.SingleEnemy;

            // 检查目标方是否有可选槽位
            bool hasTarget = false;
            BoardManager bmRedirect = FindObjectOfType<BoardManager>();
            if (bmRedirect != null)
            {
                int start = redirectToHost ? 6 : 0;
                int end = redirectToHost ? 11 : 5;
                for (int j = start; j <= end; j++)
                    if (bmRedirect.GetSlot(j)?.currentCard3D != null) { hasTarget = true; break; }
            }

            if (!hasTarget)
            {
                // 无可用目标 → 仅阻止进场，不重定向
                CleanupAfterPlacement();
                return;
            }

            SelectionManager.Instance.BeginSelection(redirectTargetType, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    CardInstance targetInst = target.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (targetInst != null && redirectTemplate != null)
                        target.StartOnEnterEffect(redirectTemplate, targetInst);
                }
                CleanupAfterPlacement();
            });
            return;
        }

        if (template.templateID == "01309") {CleanupAfterPlacement();return;}

        // ── Step 3: 进场效果分发（新 → EffectRegistry，回退 → 旧 switch）──
        if (inst != null) inst._enterEffectRunning = true;
        var enterCtx = EffectContext.ForEnter(template, inst, this);
        if (EffectDispatcher.Dispatch(Trigger.Enter, enterCtx))
            return; // handler 已处理全部逻辑（含 CleanupAfterPlacement）

        // ── 未注册卡回退 ───────────────────────────────────
        Debug.LogWarning($"[StartOnEnterEffect] 未注册: {template.templateID}");
        CleanupAfterPlacement();
    }

  

    public void CleanupAfterPlacement()
    {
        if (currentCard3D != null)
        {
            var crd = currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (crd != null) crd._enterEffectRunning = false;
        }
        isPlacingCard = false;
        cardToPlace = null;

        if (!isTargetingMode && !isAttachSelectMode)
        {
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.SetHandAreaRaycast(true);
            hm?.ShowAllCards();
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        }

        // 手牌为空时强制启用按钮（防止放置/效果链路中残留禁用状态）
        NetworkPlayer.Local?.handCards.RemoveAll(c => c == null);
        if (NetworkPlayer.Local != null && NetworkPlayer.Local.handCards.Count == 0)
        {
            EndTurnButton endBtn = FindObjectOfType<EndTurnButton>();
            if (endBtn != null)
            {
                CanvasGroup endCG = endBtn.GetComponent<CanvasGroup>() ?? endBtn.gameObject.AddComponent<CanvasGroup>();
                endCG.interactable = true;
                endCG.blocksRaycasts = true;
            }
            DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
            if (drawUI != null)
            {
                CanvasGroup drawCG = drawUI.GetComponent<CanvasGroup>() ?? drawUI.gameObject.AddComponent<CanvasGroup>();
                drawCG.interactable = true;
                drawCG.blocksRaycasts = true;
            }
        }

        BoardSyncManager.MarkDirty();
    }

    public void SetBlocked(bool blocked)
    {
        isBlocked = blocked;
        slotImage.color = blocked ? Color.gray : normalColor;
    }

    public void SetCard(GameObject card3D)
    {
        currentCard3D = card3D;
        hasCard = card3D != null;
        if (card3D != null)
        {
            var ci = card3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null) ci._placedAtTime = Time.time;
        }
    }

    /// <summary>Force-refresh slot visual after syncing flags from server.</summary>
    public void SyncVisual()
    {
        if (isBlocked) slotImage.color = Color.gray;
        else if (prisonBlocked) slotImage.color = new Color(0.6f, 0.2f, 0.8f);
        else if (hasPlague) slotImage.color = Color.green;
        else slotImage.color = normalColor;
    }

    public bool HasEnemyTarget()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int id = 0; id <= 5; id++)
        {
            BoardSlot slot = bm?.GetSlot(id);
            if (slot != null && !slot.isBlocked && slot.hasCard) return true;
        }
        return false;
    }

    public bool HasAllyTargetExceptSelf()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int id = 6; id <= 11; id++)
        {
            BoardSlot slot = bm?.GetSlot(id);
            if (slot != null && !slot.isBlocked && slot.hasCard && slot != this) return true;
        }
        return false;
    }

    public static void CheckAndHandleDeaths()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        // ── 同时窗口：退场前快照伤害来源 → 存入全局待反击队列 ──
        pendingRevenges.Clear();
        for (int i = 0; i < 12; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D == null) continue;
            var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || ci.currentHealth > 0) continue;
            if (ci._enterEffectRunning) continue; // 进场中，不参与死亡/反击
            if (!ci.hasRevenge || string.IsNullOrEmpty(ci.revengeEffect)) continue;

            var sourceIDs = new List<string>();
            var marker = s.currentCard3D.GetComponent<DamageSourceMarker>();
            if (marker != null)
            {
                sourceIDs = marker.GetMinionDamageSources()
                    .FindAll(g => g != null && g.GetComponent<Card3DInstance>()?.cardInstance != null)
                    .ConvertAll(g => g.GetComponent<Card3DInstance>().cardInstance.instanceID);
            }
            pendingRevenges.Add((s.slotID, ci.revengeEffect, sourceIDs));
            ci.revengeSnapshotIDs = sourceIDs;
        }

        // ── Step 2c: DeathCheckAction 替代同步 do-while ─────────────────
        ActionQueueManager.Enqueue(new DeathCheckAction(
            "CheckAndHandleDeaths",
            scanDeaths: () =>
            {
                var list = new List<DeathInfo>();
                BoardManager bmScan = FindObjectOfType<BoardManager>();
                if (bmScan == null) return list;
                for (int i = 0; i < 12; i++)
                {
                    BoardSlot s = bmScan.GetSlot(i);
                    if (s?.currentCard3D == null) continue;
                    Card3DInstance c3d = s.currentCard3D.GetComponent<Card3DInstance>();
                    var sc = c3d?.cardInstance;
                    if (sc != null && sc.currentHealth <= 0)
                    {
                        // 进场效果执行中的卡跳过死亡扫描——等 CleanupAfterPlacement 后再判定
                        if (sc._enterEffectRunning) continue;
                        list.Add(new DeathInfo
                        {
                            slotID = s.slotID,
                            templateID = sc.templateID,
                            cardObject = s.currentCard3D,
                            cardInstance = sc,
                            isActiveExit = sc.isActiveExit,
                        });
                    }
                }
                return list;
            },
            handleDeath: (death) =>
            {
                BoardManager bmHandle = FindObjectOfType<BoardManager>();
                if (bmHandle == null) return;
                BoardSlot s = bmHandle.GetSlot(death.slotID);
                if (s != null && s.currentCard3D == death.cardObject)
                    s.HandleDeath(death.cardObject);
            },
            onAllDeathsResolved: () =>
            {
                HandManager hm = FindObjectOfType<HandManager>();
                BoardManager bmFinal = FindObjectOfType<BoardManager>();
                if (hm != null && bmFinal != null)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        BoardSlot s = bmFinal.GetSlot(i);
                        if (s?.currentCard3D == null) continue;
                        CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isXValue)
                            hm.UpdateXValues(ci);
                    }
                }
                BoardSyncManager.MarkDirty();
            }));
    }

    public void HandleDeath(GameObject dyingCard)
    {
        if (dyingCard == null) return;
        Card3DInstance c3d = dyingCard.GetComponent<Card3DInstance>();
        if (c3d == null || c3d.cardInstance == null) return;
        // 标记本帧已被本地处理——防 ApplySync 的 EnsureEmpty 双杀（问题 8）
        c3d.cardInstance._deathProcessed = true;
        c3d.cardInstance.hasLifePriestBlessing = false;
        c3d.cardInstance.lifePriestBlessingSource = null;
        string templateID = c3d.cardInstance.templateID;
        bool isActiveExit = c3d.cardInstance.isActiveExit;  
                // 清理重定向标记
        GlobalDeathEventHandler.Trigger(c3d.cardInstance, slotID, c3d.cardInstance.damageSourceInstanceIDs, isActiveExit);
                // 清理重定向标记
        if (c3d.cardInstance != null)
        {
            foreach (string sourceID in c3d.cardInstance.damageSourceInstanceIDs)
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                bool fromGravekeeper = false;
                BoardManager.GetSideRange(slotID, out int gkSideStart, out int gkSideEnd);
                for (int i = gkSideStart; i <= gkSideEnd; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s?.currentCard3D == null) continue;
                    CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.instanceID == sourceID && ci.templateID == "01330")
                    {
                        fromGravekeeper = true;
                        break;
                    }
                }
                if (fromGravekeeper)
                {
                    c3d.cardInstance.hasOnDeath = false;
                    c3d.cardInstance.hasActiveExit = false;
                    break;
                }
            }
        }
                // 清理重定向标记
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(c3d.cardInstance, "退场"))
        {
            c3d.cardInstance.hasOnDeath = false;
            c3d.cardInstance.hasActiveExit = false;
        }
                // 清理重定向标记
        Debug.Log($"未弃之人检测: templateID={templateID}, hasOnDeath={c3d.cardInstance.hasOnDeath}, hasActiveExit={c3d.cardInstance.hasActiveExit}, isActiveExit={isActiveExit}");
        if (c3d.cardInstance != null)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            foreach (GameObject obj in bm.attachedModels)
            {
                Card3DInstance c3dAtt = obj?.GetComponent<Card3DInstance>();
                if (c3dAtt?.cardInstance != null && c3dAtt.cardInstance.templateID == "01131"
                    && c3dAtt.cardInstance.hostSlotID == slotID)
                {
                    bool canConvert = c3d.cardInstance.hasActiveExit;
                    if (canConvert && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(c3dAtt.cardInstance)))
                    {
                        c3d.cardInstance.isActiveExit = true;
                        c3d.cardInstance.hasOnDeath = false;
                        isActiveExit = true;
                        Debug.Log("未弃之人 执行替换");
                    }
                    break;
                }
            }
        }

        // ── 退场效果分发 ──────────────────────────────────────────────
        bool shouldReturn03504 = false;
        CardData template03504 = null;
        bool shouldReturn01117 = false;
        CardData template01117 = null;
        bool shouldReturn03009 = false;
        CardData template03009 = null;

        var exitCtx = EffectContext.ForExit(c3d.cardInstance, this, isActiveExit);
        Trigger exitTrigger = isActiveExit ? Trigger.ActiveExit : Trigger.Exit;
        EffectDispatcher.Dispatch(exitTrigger, exitCtx);

        // ── 动态赋予的死亡特性（01117/苦难给予者赋予的"退场：减一能量"等）──
        if (c3d.cardInstance.templateID != "01117" &&
            c3d.cardInstance.grantedTraitTexts != null &&
            c3d.cardInstance.grantedTraitTexts.Count > 0)
        {
            ProcessGrantedDeathTraits(c3d.cardInstance, slotID);
        }

        shouldReturn03504 = exitCtx.shouldReturn03504;
        template03504 = exitCtx.template03504;
        shouldReturn01117 = exitCtx.shouldReturn01117;
        template01117 = exitCtx.template01117;
        shouldReturn03009 = exitCtx.shouldReturn03009;
        template03009 = exitCtx.template03009;

        // ── 通用死亡后处理管线（Step 2a 提取） ─────────────────────────
        DeathPipeline.ExecuteCommon(new DeathPipelineParams
        {
            dyingCard = dyingCard,
            c3d = c3d,
            slot = this,
            shouldReturn03504 = shouldReturn03504,
            template03504 = template03504,
            shouldReturn01117 = shouldReturn01117,
            template01117 = template01117,
            shouldReturn03009 = shouldReturn03009,
            template03009 = template03009,
        });
    }

    /// <summary>处理动态赋予的死亡特性（苦难给予者等）。HandleDeath 中调用。</summary>
    static void ProcessGrantedDeathTraits(CardInstance ci, int slotID)
    {
        if (ci.grantedTraitTexts == null || ci.grantedTraitTexts.Count == 0) return;
        NetworkPlayer traitOwner = BoardManager.GetOwnerPlayer(slotID);

        foreach (string trait in ci.grantedTraitTexts)
        {
            switch (trait)
            {
                case "退场：减一能量":
                    if (traitOwner != null) { traitOwner.currentEnergy -= 1; traitOwner.UpdateUI(); }
                    break;
                case "退场：己方全体受到一伤害":
                    BoardManager bmG = FindObjectOfType<BoardManager>();
                    if (bmG != null)
                    {
                        BoardManager.GetSideRange(slotID, out int gs, out int ge);
                        for (int i = gs; i <= ge; i++)
                        {
                            var si = bmG.GetSlot(i);
                            if (si?.currentCard3D != null)
                            {
                                var tci = si.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                if (tci != null)
                                {
                                    tci.currentHealth -= 1;
                                    si.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    DamagePipeline.ShowFloaterAt(tci, 1, FloaterType.Damage);
                                }
                            }
                        }
                    }
                    CheckAndHandleDeaths();
                    break;
                case "退场：己方玩家扣一血":
                    traitOwner?.TakeDamage(1);
                    break;
            }
        }
    }

    static int FindSlotID(CardInstance ci)
    {
        var bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return i;
        return -1;
    }

    public static void TriggerDeathEffect(CardInstance ci, bool isActive)
    {
        if (ci == null) return;
        NetworkPlayer dp = BoardManager.GetOwnerPlayer(FindSlotID(ci));
        string id = ci.templateID;
        // dp is already set above: NetworkPlayer dp = BoardManager.GetOwnerPlayer(FindSlotID(ci));
        if (isActive)
        {
            switch (id)
            {
                case "01106": dp?.AddEnergy(3); break;
                case "01107":
                    dp?.AddEnergy(2);
                    {
                        bool hasAlly = false;
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        BoardManager.GetSideRangeOf(ci, out int fcSideStart, out int fcSideEnd);
                        for (int i = fcSideStart; i <= fcSideEnd; i++)
                        {
                            if (bm?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
                        }
                        if (hasAlly)
                        {
                            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
                            {
                                if (target?.currentCard3D != null)
                                {
                                    CardInstance ti = target.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (ti != null)
                                    {
                                        ti.GrantShield(true, false, false);
                                        target.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    }
                                }
                            });
                        }
                    }
                    break;
            }
        }
        else
        {
            switch (id)
            {
                case "01106": dp?.AddEnergy(1); break;
                case "03513":
                    Do03513AOE(ci);
                    break;
            }
        }
    }

    /// <summary>03513 断罪者死亡时：对对方全部随从造成 1 伤害。从有 this 和无 this 的两处调用点提取。</summary>
    static void Do03513AOE(BoardSlot mySlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        int enemyStart = mySlot.slotID >= 6 ? 0 : 6;
        for (int j = enemyStart; j < enemyStart + 6; j++)
        {
            BoardSlot es = bm.GetSlot(j);
            if (es?.currentCard3D != null)
            {
                Card3DInstance ei = es.currentCard3D.GetComponent<Card3DInstance>();
                if (ei?.cardInstance != null)
                {
                    BattleManager.Instance.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                    ei.UpdateValues();
                }
            }
        }
    }

    static void Do03513AOE(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            { Do03513AOE(bm.GetSlot(i)); return; }
        }
    }

    public void ApplySufferingGiverEffect(CardInstance giver, CardInstance target, string chosenTrait)
    {
        if (chosenTrait == null || giver == null || target == null) return;
        giver.giveableDeathTraits.Remove(chosenTrait);
        giver.RemoveGrantedTrait(chosenTrait);
        target.GrantTrait(chosenTrait);
        RefreshCardDisplay(target);
        // 如果详情面板正打开着，即时刷新显示
        RefreshTest1Panel(giver);
        RefreshTest1Panel(target);
        // 同步赋予的特性文本到对方视角（target 在对方半场，需要上报板面变化）
        TurnManager.SyncMyBoardToOpponent();
    }

    static void RefreshTest1Panel(CardInstance ci)
    {
        if (ci == null) return;
        var panel = Test1Panel.Instance;
        if (panel == null || !panel.panelRoot.activeSelf) return;
        panel.Show(ci);
    }
    public static void ClearAllHighlights()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot != null)
            {
                slot.transform.localScale = slot.originalScale;
                slot.slotImage.color = slot.isBlocked ? Color.gray : slot.normalColor;
            }
        }
    }
    void RefreshCardDisplay(CardInstance ci)
    {
        if (ci == null) return;
        BoardManager bm = FindObjectOfType<BoardManager>();
        // 1) 3D 模型刷新
        if (bm != null)
            for (int i = 0; i < 12; i++)
            {
                Card3DInstance c3d = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance == ci) { c3d.UpdateValues(); break; }
            }
        // 2) 2D 手牌刷新（如果同 instanceID 在手牌中）
        if (NetworkPlayer.Local == null) return;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            var inst = card.GetComponent<CardInstance>();
            if (inst != null && inst.instanceID == ci.instanceID)
            {
                card.GetComponent<CardDisplay2D>()?.RefreshWithInstance(inst);
                break;
            }
        }
    }

    void CleanupAfterSelection() { }

    public IEnumerator ReformerEnterEffect(CardInstance giver)
    {
        yield return null;
      
        SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        List<GameObject> handSummons = new List<GameObject>();

        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData t = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (t == null) continue;
            if (t.cardType == CardType.Spell) { card.SetActive(false); spellCards.Add(card); }
            else if (t.cardType == CardType.Summon)
            {
                handSummons.Add(card);
                CardClickHandler handler = card.GetComponent<CardClickHandler>();
                if (handler == null) handler = card.AddComponent<CardClickHandler>();
                handler.onClick = () =>
                {
                    SelectionManager.Instance.ForceEndAll();
                    CleanupReformerUI(spellCards, handSummons);
                    ApplyReformerEffect(card);
                    CleanupAfterPlacement();
                   
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.ForceEndAll();
                CleanupReformerUI(spellCards, handSummons);
                ApplyReformerEffect(targetSlot.currentCard3D);
                CleanupAfterPlacement();
                
            }
        };
    }

    void CleanupReformerUI(List<GameObject> hiddenSpells, List<GameObject> handSummons)
    {
        foreach (GameObject card in hiddenSpells) { if (card != null) card.SetActive(true); }
        foreach (GameObject card in handSummons)
        {
            if (card == null) continue;
            CardClickHandler handler = card.GetComponent<CardClickHandler>();
            if (handler != null) Destroy(handler);
        }
    }

    void ApplyReformerEffect(GameObject target)
    {
        if (target == null) return;
        CardInstance targetCI = target.GetComponent<CardInstance>();
        if (targetCI == null)
        {
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            if (c3d != null) targetCI = c3d.cardInstance;
        }
        if (targetCI != null)
        {
            if (!targetCI.prefixes.Contains("灵能"))
            {
                if (string.IsNullOrEmpty(targetCI.prefixes) || targetCI.prefixes == "无")
                    targetCI.prefixes = "灵能";
                else targetCI.prefixes += " 灵能";
            }
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();
            // 前缀修改同步到对方
            TurnManager.SyncMyBoardToOpponent();
        }
    }

    private void ExecuteReplace(BoardSlot targetSlot)
    {
        GameObject oldCard = targetSlot.currentCard3D;
        HandManager hm = FindObjectOfType<HandManager>();
        hm.PlaceCardToSlot(targetSlot, cardToPlace);
        CardInstance newInst = targetSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        CardData newTemplate = CardDatabase.Instance?.GetTemplate(newInst?.templateID);
        if (newTemplate != null && newTemplate.hasOnEnter && newInst != null)
        {
            targetSlot.StartOnEnterEffect(newTemplate, newInst);
        }
        if (oldCard != null)
        {
            Card3DInstance oldInst = oldCard.GetComponent<Card3DInstance>();
            if (oldInst?.cardInstance != null)
            {
                oldInst.cardInstance.isActiveExit = false;
                oldInst.cardInstance.hasRevenge = false;

                // 清理重定向标记
                GlobalDeathEventHandler.Trigger(oldInst.cardInstance, targetSlot.slotID,
                    oldInst.cardInstance.damageSourceInstanceIDs, false);
            }

            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
                for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
                {
                    GameObject obj = bm.attachedModels[i];
                    if (obj == null) continue;
                    Card3DInstance c3d = obj.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance != null && c3d.cardInstance.hostSlotID == targetSlot.slotID)
                    { bm.attachedModels.RemoveAt(i); Destroy(obj); }
                }

            // 旧模型可以销毁，HandleDeath会在SetCard后触发新卡
            GraveEntry entry = new GraveEntry
            {
                templateID = oldInst.cardInstance.templateID,
                instanceID = oldInst.cardInstance.instanceID,
                deathPhase = TurnManager.Instance.phaseCount,
                handledReturnToHand = false
            };
            GraveyardManager.Instance.AddToGraveyard(entry);
            Destroy(oldCard);
        }

        CleanupAfterPlacement();
    }

    private void ExecuteAttach(BoardSlot hostSlot)
    {
        HandManager hm = FindObjectOfType<HandManager>();
        hm.PlaceCardToSlot(null, cardToPlace);
        CleanupAfterPlacement();
    }

    public GameObject currentCard3D
    {
        get => _currentCard;
        set
        {
            if (_currentCard != null)
            {
                Card3DInstance oc = _currentCard.GetComponent<Card3DInstance>();
                if (oc?.cardInstance != null)
                {
                    if (!oc.cardInstance.isXValue)
                        oc.cardInstance.currentAttack -= slotTempAttackBoost;
                    oc.cardInstance.currentAttack += deepSeaAttackDebuff;
                    oc.cardInstance.currentTier -= spotlightTierBoost;
                    oc.UpdateValues();
                }
                if (hasPlague)
                {
                    hasPlague = false;
                    plagueRoundCount = 0;
                }
            }
            _currentCard = value;
            if (_currentCard != null)
            {
                Card3DInstance nc = _currentCard.GetComponent<Card3DInstance>();
                if (nc?.cardInstance != null)
                {
                    if (!nc.cardInstance.isXValue)
                        nc.cardInstance.currentAttack += slotTempAttackBoost;
                    nc.cardInstance.currentAttack -= deepSeaAttackDebuff;
                    nc.cardInstance.currentTier += spotlightTierBoost;
                    nc.UpdateValues();
                }
            }
        }
    }
    public static void CleanupAttachSelect()
    {
        isAttachSelectMode = false;
        isReplaceMode = false;
        attachCanBeIndependent = false;
    }
    public static void StartAttachSelect(bool canBeIndependent, Action<BoardSlot> onSelected)
    {
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, onSelected);
        isAttachSelectMode = true;
        attachCanBeIndependent = canBeIndependent;
    }
    public void OnDisasterWalkerDamage(int amount)
    {
        Debug.Log($"灾厄行者触发: 扣血{amount}");
        for (int i = 0; i < amount; i++)
        {
            NetworkPlayer.Local.DrawCardWithoutLimit();
        }
    }
    void CopyToGrave(CardInstance dest, CardInstance src)
    {
        dest.templateID = src.templateID;
        dest.instanceID = src.instanceID;
        dest.currentCost = src.currentCost;
        dest.currentAttack = src.currentAttack;
        dest.baseAttack = src.baseAttack;
        dest.currentHealth = src.currentHealth;
        dest.baseHealth = src.baseHealth;
        dest.baseMaxHealth = src.baseMaxHealth;
        dest.currentMaxHealth = src.currentMaxHealth;
        dest.currentTier = src.currentTier;
        dest.baseTier = src.baseTier;
        dest.prefixes = src.prefixes;
        dest.handledReturnToHand = src.handledReturnToHand;
        dest.hasOnDeath = src.hasOnDeath;
        dest.hasActiveExit = src.hasActiveExit;
        dest.hasOnEnter = src.hasOnEnter;
        dest.hasFirstStrike = src.hasFirstStrike;
        dest.hasRevenge = src.hasRevenge;
        dest.hasDiscard = src.hasDiscard;
        dest.canAttach = src.canAttach;
        dest.grantedTraitTexts = src.grantedTraitTexts != null ? new List<string>(src.grantedTraitTexts) : new List<string>();
        dest.giveableDeathTraits = src.giveableDeathTraits != null ? new List<string>(src.giveableDeathTraits) : new List<string>();
    }
    public IEnumerator HeartthrobEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<GameObject> heroCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero)
                heroCards.Add(card);
        }

        if (heroCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero;
        });

        GameObject selectedCard = null;
        bool cardChosen = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; cardChosen = true; };
        }
        yield return new WaitUntil(() => cardChosen);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard == null)
        {
            CleanupAfterPlacement();
            yield break;
        }

        CardInstance selInst = selectedCard.GetComponent<CardInstance>();
        bool isAttachCard = selInst != null && selInst.canAttach;

        if (isAttachCard)
        {
            // Use the standard PlaceCardToSlot flow for attach cards —
            // this handles attach/independent/replace correctly.
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.HideOtherCards(null);    // show all cards; cardToPlace gameobject stays visible
            hm?.SetHandAreaRaycast(false);
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
            hm.PlaceCardToSlot(null, selectedCard);
            // PlaceCardToSlot starts an async callback flow (StartAttachSelect or direct placement).
            // Wait for it to finish.
            yield return new WaitWhile(() => BoardSlot.isPlacingCard || BoardSlot.isAttachSelectMode);
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        }
        else
        {
            // Non-attach card: standard direct placement via isPlacingCard flag
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            BoardSlot.cardToPlace = selectedCard;
            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
        }

        // Ensure cleanup
        NetworkPlayer.Local.handCards.Remove(selectedCard);
        CleanupAfterPlacement();
    }
    public IEnumerator MartyrDeathEffectCoroutine(CardInstance giver)
    {
        yield return null;
        yield return StartCoroutine(BattleManager.Instance.WaitForSelection((onDone) =>
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            bool hasAlly = false;
            for (int j = 6; j <= 11; j++)
            {
                if (bm?.GetSlot(j)?.currentCard3D != null) { hasAlly = true; break; }
            }
            if (hasAlly)
            {
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                {
                    if (targetSlot?.currentCard3D != null)
                    {
                        CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci != giver)
                        {
                            if (!ci.cannotHealOrGainMaxHP)
                            {
                                ci.currentHealth += 5;
                                ci.currentMaxHealth += 5;
                            }
                            ci.currentAttack += 4;
                            targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                        }
                    }
                    onDone();
                });
            }
            else
            {
                onDone();
            }
        }));
    }
    public IEnumerator RogueDeathEffect(CardInstance giver)
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<GameObject> heroCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero)
                heroCards.Add(card);
        }

        if (heroCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            yield break;
        }

        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero;
        });

        GameObject selectedCard = null;
        bool done = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; done = true; };
        }
        yield return new WaitUntil(() => done);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard != null)
        {
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            BoardSlot.cardToPlace = selectedCard;
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.HideAllCards();
            hm?.SetHandAreaRaycast(false);
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
            Card3DHover.allowDiscard = false;
            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
            NetworkPlayer.Local.handCards.Remove(selectedCard);
        }
    }
    public IEnumerator GreedySnakeCopyProcess(CardInstance giver, CardInstance target)
    {
        List<(string key, string fullText)> traits = new List<(string, string)>();

        // 反击
        if (target.hasFirstStrike)
        {
            string text = GetTraitFullText(target, "反击");
            traits.Add(("反击", text));
        }
                // 清理重定向标记
        if (target.hasOnDeath)
        {
            string text = GetTraitFullText(target, "先手");
            traits.Add(("先手", text));
        }
                // 清理重定向标记
        if (target.hasRevenge)
        {
            string text = GetTraitFullText(target, "先手");
            traits.Add(("先手", text));
        }

        if (traits.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        if (traits.Count == 1)
        {
            ApplyGreedySnakeCopy(giver, target, traits[0].key);
            CleanupAfterPlacement();
            yield break;
        }

        foreach (var (key, fullText) in traits)
        {
            bool chosen = false;
            bool thisDone = false;
            ConfirmPanel.Instance.Show($"是否复制{fullText}？",
                () => { chosen = true; thisDone = true; },
                () => { thisDone = true; }
            );
            yield return new WaitUntil(() => thisDone);

            if (chosen)
            {
                ApplyGreedySnakeCopy(giver, target, key);
                break;
            }
        }

        CleanupAfterPlacement();
    }

    string GetTraitFullText(CardInstance ci, string traitKey)
    {
        // 1. 从赋予的特性中查找
        foreach (string gt in ci.grantedTraitTexts)
        {
            if (gt.Contains(traitKey)) return gt;
        }

        // 2. 反击从 revengeEffect
        if (traitKey == "反击" && !string.IsNullOrEmpty(ci.revengeEffect))
            return $"反击：{ci.revengeEffect}";

        // 3. 从模板特性文本中查找对应行
        CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
        if (td != null && !string.IsNullOrEmpty(td.traits))
        {
            string[] lines = td.traits.Split('\n');
            foreach (string line in lines)
            {
                if (line.Contains(traitKey)) return line.Trim();
            }
        }

        return traitKey;
    }

    void ApplyGreedySnakeCopy(CardInstance giver, CardInstance target, string key)
    {
        string fullText = GetTraitFullText(target, key);
        giver.GrantTrait(fullText);
        giver.greedySnakeEnterCount++;
        Debug.Log($"贪欲之蛇复制了{key}，进场次数={giver.greedySnakeEnterCount}");
    }
  
    public IEnumerator RemnantEnterEffect(CardInstance giver)
    {
        List<CardInstance> allyMinions = new List<CardInstance>();
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardManager.GetSideRangeOf(giver, out int rmSideStart, out int rmSideEnd);
        for (int i = rmSideStart; i <= rmSideEnd; i++)
        {
            BoardSlot slot = bm?.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci != giver && !ci.isAttached)
                allyMinions.Add(ci);
        }

        if (allyMinions.Count < 2)
        {
            Debug.Log("残篇：己方召唤物不足2个");
            CleanupAfterPlacement();
            yield break;
        }

                // 清理重定向标记
        CardInstance firstTarget = null;
        bool firstDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (slot) =>
        {
            if (slot?.currentCard3D != null)
            {
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci != giver && !ci.isAttached)
                {
                    firstTarget = ci;
                    firstDone = true;
                }
            }
        });
        yield return new WaitUntil(() => firstDone);
        if (firstTarget == null) { CleanupAfterPlacement(); yield break; }

                // 清理重定向标记
        CardInstance secondTarget = null;
        bool secondDone = false;
        BoardSlot.extraTargetFilter = (slot) =>
        {
            if (slot?.currentCard3D == null) return false;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            return ci != null && ci != giver && ci != firstTarget && !ci.isAttached;
        };
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (slot) =>
        {
            if (slot?.currentCard3D != null)
            {
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci != giver && ci != firstTarget && !ci.isAttached)
                {
                    secondTarget = ci;
                    secondDone = true;
                }
            }
        });
        yield return new WaitUntil(() => secondDone);
        BoardSlot.extraTargetFilter = null;
        if (secondTarget == null) { CleanupAfterPlacement(); yield break; }

                // 清理重定向标记
        GenericChoicePanel.Instance.Show("选择一个返回手牌",
      new List<string>
      {
        CardDatabase.Instance?.GetTemplate(firstTarget.templateID)?.cardName ?? "召唤物1",
        CardDatabase.Instance?.GetTemplate(secondTarget.templateID)?.cardName ?? "召唤物2"
      },
      (index) =>
      {
          HandManager hm = FindObjectOfType<HandManager>();
          hm.RemnantFinalize(firstTarget, secondTarget, index == 0);
      });
    }
    public IEnumerator PirateEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        int mySlot = -1;
        for (int i = 0; i < 12; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == giver)
            { mySlot = i; break; }
        }

        if (mySlot < 0) { CleanupAfterPlacement(); yield break; }

        int rowStart = mySlot < 9 ? 0 : 3;

        // 检查目标排是否有至少2个可操作的格子
        int validCount = 0;
        for (int j = rowStart; j < rowStart + 3; j++)
        {
            BoardSlot s = bm?.GetSlot(j);
            if (s != null && !s.isBlocked) validCount++;
        }
        if (validCount < 2) { CleanupAfterPlacement(); yield break; }

        BoardSlot.isStrengtheningSlot = true;
        BoardSlot.extraTargetFilter = (slot) =>
        {
            return slot != null && slot.slotID >= rowStart && slot.slotID < rowStart + 3;
        };
        SelectionManager.Instance.BeginSelection(TargetType.EnemyAnyRow, null);

        BoardSlot firstSlot = null;
        bool confirmed = false;
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);

        System.Text.StringBuilder swapLog = null;
        if (NetworkClient.isConnected)
            swapLog = new System.Text.StringBuilder();

        BoardSlot.onTargetSelected = (slot) =>
        {
            if (slot == null || slot.isBlocked || slot.slotID < rowStart || slot.slotID >= rowStart + 3) return;
            if (firstSlot == null)
            {
                firstSlot = slot;
            }
            else if (slot != firstSlot)
            {
                BoardSlot secondSlot = slot;
                int idA = firstSlot.slotID;
                int idB = secondSlot.slotID;
                GameObject c1 = firstSlot.currentCard3D;
                GameObject c2 = secondSlot.currentCard3D;
                Vector3 p1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(firstSlot.slotID);
                Vector3 p2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(secondSlot.slotID);

                firstSlot.SetCard(null);
                secondSlot.SetCard(null);
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
                firstSlot = null;

                // Record swap pair for network sync on confirm
                if (swapLog != null)
                {
                    if (swapLog.Length > 0) swapLog.Append(';');
                    swapLog.Append(idA).Append(',').Append(idB);
                }
            }
        };

        yield return new WaitUntil(() => confirmed);
        SelectionManager.Instance.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        BoardSlot.extraTargetFilter = null;
        ConfirmSelectionButton.Instance.Hide();

        // Sync all pirate swaps to other client at once
        TurnManager.SyncMyBoardToOpponent();

        CleanupAfterPlacement();
    }
    public IEnumerator PrisonEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        bool hasMyEmpty = false;
        BoardManager.GetSideRangeOf(giver, out int prSideStart, out int prSideEnd);
        for (int i = prSideStart; i <= prSideEnd; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked) { hasMyEmpty = true; break; }
        }
        if (!hasMyEmpty) { CleanupAfterPlacement(); yield break; }

        BoardSlot myPrison = null;
        bool myDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked && s.slotID >= prSideStart && s.slotID <= prSideEnd)
            { myPrison = s; myDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => myDone);
        BoardSlot.isStrengtheningSlot = false;
        if (myPrison == null) { CleanupAfterPlacement(); yield break; }

        bool hasEnemyEmpty = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked) { hasEnemyEmpty = true; break; }
        }
        if (!hasEnemyEmpty) { CleanupAfterPlacement(); yield break; }

        BoardSlot enemyPrison = null;
        bool enemyDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked && s.slotID <= 5)
            { enemyPrison = s; enemyDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => enemyDone);
        BoardSlot.isStrengtheningSlot = false;
        if (enemyPrison == null) { CleanupAfterPlacement(); yield break; }

        myPrison.prisonBlocked = true;
        myPrison.prisonAllowYuan = true;
        myPrison.slotImage.color = new Color(0.6f, 0.2f, 0.8f);

        enemyPrison.prisonBlocked = true;
        enemyPrison.prisonAllowYuan = false;
        enemyPrison.slotImage.color = new Color(0.6f, 0.2f, 0.8f);

        giver.prisonMySlot = myPrison.slotID;
        giver.prisonEnemySlot = enemyPrison.slotID;

        // Sync slot prison flags to opponent — must reach server & remote
        TurnManager.SyncMyBoardToOpponent();

        CleanupAfterPlacement();
    }
    public bool CanPlaceCard(CardInstance ci)
    {
        if (isBlocked) return false;
        if (!prisonBlocked) return true;
        if (slotID >= 6 && prisonAllowYuan && ci != null && ci.prefixes.Contains("渊"))
            return true;
        return false;
    }
    public IEnumerator EmperorEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        HandManager hm = FindObjectOfType<HandManager>();
        hm?.ShowAllCards();

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
                CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
                h.onClick = () =>
                {
                    SelectionManager.Instance.ForceEndAll();
                    CleanupEmperorUI(spellCards, handSummons);
                    ApplyEmperorPrefix(card);
                    CleanupAfterPlacement();
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.ForceEndAll();
                CleanupEmperorUI(spellCards, handSummons);
                ApplyEmperorPrefix(targetSlot.currentCard3D);
                CleanupAfterPlacement();
            }
        };
    }

    void CleanupEmperorUI(List<GameObject> hiddenSpells, List<GameObject> handSummons)
    {
        foreach (GameObject card in hiddenSpells) if (card != null) card.SetActive(true);
        foreach (GameObject card in handSummons)
        {
            if (card == null) continue;
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
    }

    void ApplyEmperorPrefix(GameObject target)
    {
        if (target == null) return;
        CardInstance ci = target.GetComponent<CardInstance>();
        if (ci == null) { Card3DInstance c3d = target.GetComponent<Card3DInstance>(); if (c3d != null) ci = c3d.cardInstance; }
        if (ci != null && !ci.prefixes.Contains("渊"))
        {
            if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                ci.prefixes = "渊";
            else ci.prefixes += " 渊";
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();
        }
    }
    public void SetHighlightColor(Color color)
    {
        slotImage.color = color;
    }

    public Color GetNormalColor()
    {
        return normalColor;
    }
    public IEnumerator RiddlerDeathEffect(CardInstance giver)
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

                // 清理重定向标记
        List<GameObject> counterCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Spell && (td.spellType & SpellType.Counter) != 0)
                counterCards.Add(card);
        }

        if (counterCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            yield break;
        }

                // 清理重定向标记
        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Spell && (td.spellType & SpellType.Counter) != 0;
        });

        GameObject selectedCard = null;
        bool done = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; done = true; };
        }
        yield return new WaitUntil(() => done);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard != null)
        {
                // 清理重定向标记
            CounterManager.Instance?.PlayCounter(selectedCard, true);
                // 清理重定向标记
            var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
            if (counter != null) counter.noCostOnTrigger = true;
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            Destroy(selectedCard);
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.RefreshLayout(true);
        }
    }
    public IEnumerator BlockerEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        bool hasEnemyEmpty = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked) { hasEnemyEmpty = true; break; }
        }
        if (!hasEnemyEmpty) { CleanupAfterPlacement(); yield break; }

        BoardSlot target = null;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked && s.slotID <= 5)
            {
                target = s;
                done = true;
            }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => done);
        BoardSlot.isStrengtheningSlot = false;

        if (target != null)
        {
            target.isBlocked = true;
            target.slotImage.color = Color.black;
            Debug.Log($"封锁者永久封锁槽位{target.slotID}");
            // Sync slot block to opponent — slot flags must reach server & remote
            TurnManager.SyncMyBoardToOpponent();
        }

        CleanupAfterPlacement();
    }
    public IEnumerator InkEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        List<CardInstance> allies = new List<CardInstance>();
        BoardManager.GetSideRangeOf(giver, out int inkSideStart, out int inkSideEnd);
        for (int i = inkSideStart; i <= inkSideEnd; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci != giver && !ci.isAttached && ci.templateID != "01523")
                    allies.Add(ci);
            }
        }

        if (allies.Count == 0) { CleanupAfterPlacement(); yield break; }

        // 使己方其它召唤物退场并回到手牌
        foreach (CardInstance ci in allies)
        {
            ci.isActiveExit = true;
            ci.handledReturnToHand = false;
            BoardSlot slot = FindSlotOf(ci);
            if (slot != null)
            {
                slot.HandleDeath(slot.currentCard3D);
                // HandleDeath 可能已通过退场特性处理回手；若未处理则手动回手
                if (!ci.handledReturnToHand)
                {
                    CardData tt = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (tt != null)
                        NetworkPlayer.Local.AddCardToHandFromInstance(tt, ci);
                }
                yield return null;
                // 防止退场效果残留的选择状态阻塞
                if (SelectionManager.Instance.IsSelecting)
                    SelectionManager.Instance.ForceEndAll();
            }
        }

        // 每退场一个 +1+1
        int count = allies.Count;
        giver.currentHealth += count;
        giver.currentMaxHealth += count;
        giver.currentAttack += count;

        Card3DInstance giver3D = FindGiver3D(giver);
        giver3D?.UpdateValues();

        // 同步增强后的属性到服务器
        BoardSlot giverSlot = FindSlotOf(giver);
        if (giverSlot != null && NetworkClient.isConnected)
            NetworkPlayer.Local?.CmdUpdateCardStats(giverSlot.slotID,
                giver.currentAttack, giver.currentHealth, giver.currentMaxHealth);

        CleanupAfterPlacement();
    }
    BoardSlot FindSlotOf(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return s;
        }
        return null;
    }

    public Card3DInstance FindGiver3D(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return s.currentCard3D?.GetComponent<Card3DInstance>();
        }
        return null;
    }

    /// <summary>猩红圣徒(01533)：进场为己方手牌或场上一召唤物附加血歌前缀。</summary>
    public IEnumerator ScarletSaintEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData t = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (t?.cardType == CardType.Spell) { card.SetActive(false); spellCards.Add(card); }
        }

        bool done = false;

        // Click handler for hand summon cards
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData t = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (t?.cardType == CardType.Spell) continue;
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () =>
            {
                if (!ci.prefixes.Contains("血歌"))
                {
                    ci.prefixes = string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无"
                        ? "血歌" : ci.prefixes + " 血歌";
                    CardDisplay2D d2d = card.GetComponent<CardDisplay2D>();
                    d2d?.Refresh();
                }
                SelectionManager.Instance.ForceEndAll();
                foreach (var sc in spellCards) sc?.SetActive(true);
                done = true;
            };
        }

        // Click handler for board ally slots
        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                Card3DInstance c3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance != null && !c3d.cardInstance.prefixes.Contains("血歌"))
                {
                    c3d.cardInstance.prefixes = string.IsNullOrEmpty(c3d.cardInstance.prefixes) || c3d.cardInstance.prefixes == "无"
                        ? "血歌" : c3d.cardInstance.prefixes + " 血歌";
                    c3d.UpdateValues();
                }
            }
            SelectionManager.Instance.ForceEndAll();
            foreach (var sc in spellCards) sc?.SetActive(true);
            done = true;
        };

        yield return new WaitUntil(() => done);

        // Cleanup click handlers
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            var h = card?.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        foreach (var sc in spellCards) sc?.SetActive(true);

        CleanupAfterPlacement();
    }

    public IEnumerator ApprenticeMageEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Spell)
                spellCards.Add(card);
        }

        if (spellCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Spell;
        });

        GameObject selectedCard = null;
        bool done = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; done = true; };
        }
        yield return new WaitUntil(() => done);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard != null)
        {
            CardInstance spellInst = selectedCard.GetComponent<CardInstance>();
            CardData spellTemplate = CardDatabase.Instance?.GetTemplate(spellInst?.templateID);

            if (spellTemplate != null)
            {
                if ((spellTemplate.spellType & SpellType.Counter) != 0)
                {
                    // 有目标法术
                    CounterManager.Instance?.PlayCounter(selectedCard, true);
                    var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
                    if (counter != null) counter.noCostOnTrigger = true;
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                }
                else if (spellTemplate.targetType == TargetType.None)
                {
                // 清理重定向标记
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                    SpellEffectExecutor.Execute(spellTemplate, null);
                }
                else
                {
                // 清理重定向标记
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                    SelectionManager.Instance.BeginSelection((TargetType)spellTemplate.targetType, (slot) =>
                    {
                        SpellEffectExecutor.Execute(spellTemplate, slot);
                    });
                }
            }
        }

        CleanupAfterPlacement();
    }
    public IEnumerator ConductorDoubleDeathEffect(DeathEffectData data)
    {
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        if (data != null)
        {
    // 基于数据包触发退场效果
            data.isFullySilenced = GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(null); // 实际逻辑后补，暂时放这里
        // 全局退场事件检测
            if (!data.isFullySilenced && !data.isDeathBlocked)
            {
                TriggerDeathEffectFromData(data);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
        }
    }

                // 清理重定向标记
    void TriggerDeathEffectFromData(DeathEffectData data)
    {
        if (data == null) return;

                // 清理重定向标记
        GlobalDeathEventHandler.Trigger(null, data.slotID, data.damageSourceInstanceIDs, data.isActiveExit);

        if (data.isFullySilenced) return;
        if (data.isDeathBlocked) return;
        NetworkPlayer tOwner = BoardManager.GetOwnerPlayer(data.slotID);
        var dp = tOwner;
        string id = data.templateID;
        if (data.isActiveExit)
        {
            switch (id)
            {
                case "01106": tOwner?.AddEnergy(3); break;
                case "01107":
                    tOwner?.AddEnergy(2);
                    {
                        bool hasAlly = false;
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        BoardManager.GetSideRange(data.slotID, out int fcSideStart, out int fcSideEnd);
                        for (int i = fcSideStart; i <= fcSideEnd; i++)
                        {
                            if (bm?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
                        }
                        if (hasAlly)
                        {
                            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
                            {
                                if (target?.currentCard3D != null)
                                {
                                    CardInstance ti = target.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (ti != null)
                                    {
                                        ti.GrantShield(true, false, false);
                                        target.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    }
                                }
                            });
                        }
                    }
                    break;
            }
        }
        else
        {
            switch (id)
            {
                case "01106": dp?.AddEnergy(1); break;
                case "03513":
                    Do03513AOE(this);
                    break;
            }
        }

        NetworkPlayer traitOwner = BoardManager.GetOwnerPlayer(data.slotID);

        // ── 01117 自己的可给予退场列表（旧路径，保留）──
        if (id == "01117" && data.giveableDeathTraits != null)
        {
            bool shouldReturn = !data.isActiveExit;
            foreach (string trait in data.giveableDeathTraits)
            {
                switch (trait)
                {
                    case "退场：摸一张牌":
                        if (traitOwner != null) { traitOwner.currentEnergy -= 1; traitOwner.UpdateUI(); }
                        break;
                    case "退场：己方全体受到一伤害":
                        BoardManager bmDH = FindObjectOfType<BoardManager>();
                        if (bmDH != null)
                        {
                            BoardManager.GetSideRange(slotID, out int dhSideStart, out int dhSideEnd);
                            for (int i = dhSideStart; i <= dhSideEnd; i++)
                            {
                                BoardSlot slot = bmDH.GetSlot(i);
                                if (slot?.currentCard3D != null)
                                    BattleManager.Instance.ApplyDamageToMinionPublic(slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance, 1, null);
                            }
                        }
                        break;
                    case "退场：己方玩家扣一血":
                        traitOwner?.TakeDamage(1);
                        break;
                }
            }
            if (shouldReturn && !data.handledReturnToHand)
            {
                CardData template = CardDatabase.Instance?.GetTemplate(data.templateID);
                if (template != null)
                {
                // 清理重定向标记
                }
            }
        }
    }
    /// <summary>碎片(01110)：进场选择己方召唤物触发主动退场。</summary>
    public IEnumerator FragmentEnterEffect(CardInstance giver, BoardSlot mySlot)
    {
        if (!HasAllyTargetExceptSelf())
        {
            giver._hasPendingCoroutine = false;
            CleanupAfterPlacement();
            yield break;
        }

        BoardSlot selectedTarget = null;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null && targetSlot != mySlot)
                selectedTarget = targetSlot;
            done = true;
        });

        yield return new WaitUntil(() => done);

        if (selectedTarget != null)
        {
            // 等一帧确保 EndSelection 完全执行完，选择栈清空
            yield return null;
            var t3d = selectedTarget.currentCard3D?.GetComponent<Card3DInstance>();
            if (t3d?.cardInstance != null)
            {
                t3d.cardInstance.isActiveExit = true;
                selectedTarget.HandleDeath(selectedTarget.currentCard3D);
                // 等主动退场内的交互完成（如妖精的护盾选择）
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                yield return null;
                // 退场完成后广播板面变化到对方
                TurnManager.SyncMyBoardToOpponent();
            }
        }

        giver._hasPendingCoroutine = false;
        CleanupAfterPlacement();
    }

    public IEnumerator ConductorEnterEffect(CardInstance giver)
    {
        if (!HasAllyTargetExceptSelf()) { CleanupAfterPlacement(); yield break; }

        CardInstance targetCI = null;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (slot) =>
        {
            if (slot != null && slot.currentCard3D != null && slot != this)
            {
                targetCI = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            }
            done = true;
        });
        yield return new WaitUntil(() => done);
        yield return null;

        if (targetCI != null)
        {
            targetCI.isActiveExit = true;
            targetCI._conductorDoubleDeath = true;
            BoardSlot targetSlot = FindSlotOf(targetCI);
            if (targetSlot != null)
                targetSlot.HandleDeath(targetSlot.currentCard3D);
        }

        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        CleanupAfterPlacement();
    }
    public IEnumerator DeepSeaActiveExitEffect()
    {
        BoardSlot.isStrengtheningSlot = true;

                // 清理重定向标记
        BoardSlot first = null;
        bool firstDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null) { first = s; firstDone = true; }
        });
        yield return new WaitUntil(() => firstDone);

                // 清理重定向标记
        BoardSlot second = null;
        bool secondDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && s != first) { second = s; secondDone = true; }
        });
        yield return new WaitUntil(() => secondDone);

        BoardSlot.isStrengtheningSlot = false;

        NetworkPlayer.Local.AddEnergy(1);
    }
    public IEnumerator FanaticShamanEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardManager.GetSideRangeOf(giver, out int fsSideStart, out int fsSideEnd);
        List<BoardSlot> allies = new List<BoardSlot>();
        for (int i = fsSideStart; i <= fsSideEnd; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null && s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance != giver)
                allies.Add(s);
        }

        GlobalEventManager.Instance.RegisterAura(new FanaticShamanAura { source = giver });

        foreach (BoardSlot s in allies)
        {
            CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            CardData td = CardDatabase.Instance?.GetTemplate(ci?.templateID);
            Debug.Log($"萨满检测: templateID={ci?.templateID}, hasOnEnter={td?.hasOnEnter}, td={td != null}");
            if (td != null && td.hasOnEnter && ci != null)
            {
                s.StartOnEnterEffect(td, ci);
                yield return null;
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
        }

        CleanupAfterPlacement();
    }


    public IEnumerator SummonAllShadows()
    {
        CardData shadowTemplate = CardDatabase.Instance?.GetTemplate("03007");
        if (shadowTemplate?.prefab3D == null) yield break;

        BoardManager bm = FindObjectOfType<BoardManager>();
        int currentShadows = 0;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isShadow) currentShadows++;
            }
        }

        int toSummon = CardInstance.shadowLimit - currentShadows;
        Debug.Log($"SummonAllShadows: limit={CardInstance.shadowLimit}, current={currentShadows}, toSummon={toSummon}");

        for (int k = 0; k < toSummon; k++)
        {
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

            bool placed = false;
            BoardSlot.onTargetSelected = (selectedSlot) =>
            {
                if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.slotID < 6) return;
                string shid = CardZoneManager.GenerateInstanceID(shadowTemplate.templateID);
                GameObject temp = new GameObject("TempShadow");
                CardInstance ti = temp.AddComponent<CardInstance>();
                ti.InitFromTemplate(shadowTemplate, 0, shid);
                ti.isShadow = true;
                ti.currentAttack += CardInstance.shadowAtkBonus;
                ti.currentTier += CardInstance.shadowTierBonus;
                HandManager hm = FindObjectOfType<HandManager>();
                hm.PlaceCardToSlot(selectedSlot, temp);
                Destroy(temp);

                // Sync shadow to opponent
                if (NetworkClient.isConnected)
                    NetworkPlayer.Local?.CmdPlayCard(shadowTemplate.templateID, selectedSlot.slotID,
                        ti.currentAttack, ti.currentHealth, ti.currentMaxHealth, ti.instanceID);

                placed = true;
                SelectionManager.Instance.ForceEndAll();
                BoardSlot.isPlacingCard = false;
                BoardSlot.isStrengtheningSlot = false;
            };
            yield return new WaitUntil(() => placed);
        }
    }

    public IEnumerator ShadowMasterEnterEffect(CardInstance giver)
    {
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        Debug.Log($"影舞者进场: shadowLimit before={CardInstance.shadowLimit}");
        CardInstance.shadowLimit++;
        CardInstance.shadowMasterAlive = true;
        Debug.Log($"影舞者进场: shadowLimit after={CardInstance.shadowLimit}");
        yield return StartCoroutine(SummonAllShadows());
        CleanupAfterPlacement();
    }
    public IEnumerator LordEnterEffect(CardInstance giver)
    {
        CardData ghostTemplate = CardDatabase.Instance?.GetTemplate("03002");
        if (ghostTemplate?.prefab3D == null) { CleanupAfterPlacement(); yield break; }

        for (int k = 0; k < 2; k++)
        {
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

            bool placed = false;
            BoardSlot.onTargetSelected = (selectedSlot) =>
            {
                if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.slotID < 6) return;
                GameObject temp = new GameObject("TempGhost");
                CardInstance ti = temp.AddComponent<CardInstance>();
                string giid = CardZoneManager.GenerateInstanceID(ghostTemplate.templateID);
                ti.InitFromTemplate(ghostTemplate, 0, giid);
                HandManager hm = FindObjectOfType<HandManager>();
                hm.PlaceCardToSlot(selectedSlot, temp);
                Destroy(temp);

                // Sync ghost to opponent — same instanceID as placed model
                if (NetworkClient.isConnected)
                    NetworkPlayer.Local?.CmdPlayCard(ghostTemplate.templateID, selectedSlot.slotID, -1, -1, -1, giid);

                placed = true;
                SelectionManager.Instance.ForceEndAll();
                BoardSlot.isPlacingCard = false;
                BoardSlot.isStrengtheningSlot = false;
            };
            yield return new WaitUntil(() => placed);
        }

        CleanupAfterPlacement();
    }
    public IEnumerator AmplifierEnterEffect(CardInstance giver)
    {
        // 2a. 召唤两名杂兵
        CardData soldierTemplate = CardDatabase.Instance?.GetTemplate("03004");
        if (soldierTemplate?.prefab3D != null)
        {
            for (int k = 0; k < 2; k++)
            {
                BoardSlot.isPlacingCard = true;
                BoardSlot.isStrengtheningSlot = true;
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                bool placed = false;
                BoardSlot.onTargetSelected = (selectedSlot) =>
                {
                    if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.slotID < 6) return;
                    string siid = CardZoneManager.GenerateInstanceID(soldierTemplate.templateID);
                    GameObject temp = new GameObject("TempSoldier");
                    CardInstance ti = temp.AddComponent<CardInstance>();
                    ti.InitFromTemplate(soldierTemplate, 0, siid);
                    HandManager hm = FindObjectOfType<HandManager>();
                    hm.PlaceCardToSlot(selectedSlot, temp);
                    Destroy(temp);

                    // Sync soldier to opponent — same instanceID as the placed model
                    if (NetworkClient.isConnected)
                        NetworkPlayer.Local?.CmdPlayCard(soldierTemplate.templateID, selectedSlot.slotID, -1, -1, -1, siid);

                    placed = true;
                    SelectionManager.Instance.ForceEndAll();
                    BoardSlot.isPlacingCard = false;
                    BoardSlot.isStrengtheningSlot = false;
                };
                yield return new WaitUntil(() => placed);
            }
        }

        // 2b. 选择己方场上或手牌一召唤物附加机械前缀
        yield return StartCoroutine(AmplifierAddMechPrefix(giver));
        CleanupAfterPlacement();
    }

    IEnumerator AmplifierAddMechPrefix(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

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
                CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
                h.onClick = () =>
                {
                    SelectionManager.Instance.ForceEndAll();
                    CleanupPrefixUI(spellCards, handSummons);
                    ApplyMechPrefix(card);
                    CleanupAfterPlacement();
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.ForceEndAll();
                CleanupPrefixUI(spellCards, handSummons);
                ApplyMechPrefix(targetSlot.currentCard3D);
                CleanupAfterPlacement();
            }
        };
    }

    void CleanupPrefixUI(List<GameObject> hiddenSpells, List<GameObject> handSummons)
    {
        foreach (GameObject card in hiddenSpells) if (card != null) card.SetActive(true);
        foreach (GameObject card in handSummons)
        {
            if (card == null) continue;
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
    }

    void ApplyMechPrefix(GameObject target)
    {
        if (target == null) return;
        CardInstance ci = target.GetComponent<CardInstance>();
        if (ci == null) { Card3DInstance c3d = target.GetComponent<Card3DInstance>(); if (c3d != null) ci = c3d.cardInstance; }
        if (ci != null && !ci.prefixes.Contains("渊"))
        {
            if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                ci.prefixes = "渊";
            else ci.prefixes += " 渊";
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();

            // 旧伤未愈：还未登录前缀+1+0
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
    }
    public IEnumerator WolfKingEnterEffect(CardInstance giver)
    {
        CardData wolfTemplate = CardDatabase.Instance?.GetTemplate("03006");
        if (wolfTemplate?.prefab3D == null) { CleanupAfterPlacement(); yield break; }

        BoardManager bm = FindObjectOfType<BoardManager>();
        int mySlot = -1;
        for (int i = 0; i <= 11; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == giver)
            { mySlot = i; break; }
        }

        // 只在 Wolf King 所在半场替换
        int sideStart = (mySlot >= 6) ? 6 : 0;
        int sideEnd   = (mySlot >= 6) ? 11 : 5;

        for (int i = sideStart; i <= sideEnd; i++)
        {
            if (i == mySlot) continue;
            BoardSlot slot = bm?.GetSlot(i);
            if (slot == null || slot.isBlocked) continue;

            int stackAtk = 0, stackHp = 0, stackMaxHp = 0;

            if (slot.currentCard3D != null)
            {
                CardInstance oldCI = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (oldCI == null || oldCI.currentTier >= 3 || oldCI == giver)
                    continue;
                stackAtk = oldCI.currentAttack;
                stackHp = oldCI.currentHealth;
                stackMaxHp = oldCI.currentMaxHealth;
                oldCI.isActiveExit = true;
                slot.HandleDeath(slot.currentCard3D);
                yield return null;
            }

            // 生成狼（空位或有被替换的随从）
            Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(i);
            GameObject model = Instantiate(wolfTemplate.prefab3D, pos, Quaternion.Euler(0, 180, 0));
            Card3DInstance c3d = model.GetComponent<Card3DInstance>();
            if (c3d != null)
            {
                CardInstance wolfCI = model.AddComponent<CardInstance>();
                wolfCI.InitFromTemplate(wolfTemplate, 0);
                wolfCI.currentAttack += stackAtk;
                wolfCI.currentHealth += stackHp;
                wolfCI.currentMaxHealth += stackMaxHp;
                wolfCI.wolfKingInstanceID = giver.instanceID;
                c3d.cardInstance = wolfCI;
                c3d.UpdateValues();
            }
            slot.SetCard(model);
        }

        CleanupAfterPlacement();
    }
    void UpdateKingDisplay(CardInstance king)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == king)
            {
                bm.GetSlot(i).currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                break;
            }
        }
    }
    public IEnumerator TerroristEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        List<GameObject> diedThisRound = new List<GameObject>();

                // 清理重定向标记
        HashSet<string> beforeEnter = new HashSet<string>();
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null) beforeEnter.Add(ci.instanceID);
            }
        }

        // 第一次AOE
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                Card3DInstance ei = s.currentCard3D.GetComponent<Card3DInstance>();
                if (ei?.cardInstance != null)
                {
                    BattleManager.Instance?.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                    ei.UpdateValues();
                }
            }
        }
        BoardSlot.CheckAndHandleDeaths();
        yield return ActionQueueManager.WaitForDrain();
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);

                // 清理重定向标记
        bool anyDied = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D == null && beforeEnter.Count > 0)
            {
                // 清理重定向标记
                anyDied = true;
                break;
            }
        }
        // 准备确认并校验当前instanceID
        HashSet<string> afterEnter = new HashSet<string>();
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null) afterEnter.Add(ci.instanceID);
            }
        }
        anyDied = beforeEnter.Count > afterEnter.Count || !beforeEnter.SetEquals(afterEnter);

                // 清理重定向标记
        while (anyDied)
        {
            beforeEnter = new HashSet<string>(afterEnter);

            for (int i = 0; i <= 5; i++)
            {
                BoardSlot s = bm?.GetSlot(i);
                if (s?.currentCard3D != null)
                {
                    Card3DInstance ei = s.currentCard3D.GetComponent<Card3DInstance>();
                    if (ei?.cardInstance != null)
                    {
                        BattleManager.Instance?.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                        ei.UpdateValues();
                    }
                }
            }
            BoardSlot.CheckAndHandleDeaths();
            yield return ActionQueueManager.WaitForDrain();
            yield return null;
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);

            afterEnter.Clear();
            for (int i = 0; i <= 5; i++)
            {
                BoardSlot s = bm?.GetSlot(i);
                if (s?.currentCard3D != null)
                {
                    CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null) afterEnter.Add(ci.instanceID);
                }
            }
            anyDied = beforeEnter.Count > afterEnter.Count || !beforeEnter.SetEquals(afterEnter);
        }

        CleanupAfterPlacement();

        TurnManager.SyncMyBoardToOpponent();
    }
    public IEnumerator AncientFairyReattach(GameObject fairy, int oldHostSlotID)
    {
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        Debug.Log($"AncientFairyReattach 进入");
        CardInstance fairyCI = fairy.GetComponent<Card3DInstance>()?.cardInstance;
        if (fairyCI == null) yield break;

        bool done = false;
        BoardSlot newHost = null;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            if (s != null && s.hasCard && s.slotID != oldHostSlotID)
            {
                newHost = s;
                done = true;
            }
        });

        yield return new WaitUntil(() => done);

        if (newHost != null)
        {
            fairyCI.hostSlotID = newHost.slotID;
            int maxOrder = -1;
            BoardManager bm = FindObjectOfType<BoardManager>();
            foreach (GameObject obj in bm.attachedModels)
            {
                CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isAttached && ci.hostSlotID == newHost.slotID)
                {
                    if (ci.attachOrder > maxOrder) maxOrder = ci.attachOrder;
                }
            }
            fairyCI.attachOrder = maxOrder + 1;
            bm.attachedModels.Add(fairy);
            BoardSyncManager.MarkDirty();

            if (newHost.hasCard && newHost.currentCard3D != null && newHost.currentCard3D.GetComponent<Card3DInstance>() != null)
            {
                BoardManager.SyncAttachedModels(newHost);
            }

            CardInstance newHostCI = newHost.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (newHostCI != null)
            {
                if (!newHostCI.cannotHealOrGainMaxHP)
                {
                    newHostCI.currentHealth += 5;
                    newHostCI.currentMaxHealth += 5;
                }
                newHost.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }
        else
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            bm.attachedModels.Remove(fairy);
            Destroy(fairy);
        }
    }
    public IEnumerator MistHiderEnterEffect(CardInstance giver)
    {
        yield return null;

        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

        BoardSlot firstSlot = null;
        bool confirmed = false;
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);

        BoardSlot.onTargetSelected = (selected) =>
        {
            if (firstSlot == null) { firstSlot = selected; }
            else if (selected != firstSlot)
            {
                BoardSlot secondSlot = selected;
                GameObject c1 = firstSlot.currentCard3D;
                GameObject c2 = secondSlot.currentCard3D;
                Debug.Log($"换位前 c1 active={c1?.activeSelf}, c2 active={c2?.activeSelf}");
                Vector3 p1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(firstSlot.slotID);
                Vector3 p2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(secondSlot.slotID);
                firstSlot.SetCard(null); secondSlot.SetCard(null);
                if (c2 != null) { c2.transform.position = p1; firstSlot.SetCard(c2); }
                if (c1 != null) { c1.transform.position = p2; secondSlot.SetCard(c1); }
                Debug.Log($"换位前 c1 active={c1?.activeSelf}, c2 active={c2?.activeSelf}");
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
        SelectionManager.Instance.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        ConfirmSelectionButton.Instance.Hide();
        CleanupAfterPlacement();
        BoardSlot.SyncMistHiderDisplay();
    }
    public static void SyncMistHiderDisplay()
    {
        var allAuras = GlobalEventManager.Instance?.GetAllAuras();
        if (allAuras == null) return;
        foreach (var a in allAuras)
        {
            if (a is MistHiderAura mist)
                mist.IsActive(); // 触发同步
        }
    }
    public IEnumerator BrilliantMageEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<CardInstance> spellList = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Spell)
                spellList.Add(ci);
        }

        if (spellList.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        CardDisplayPanel.Instance.multiSelect = true;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(spellList, ci => true, () =>
        {
            confirmed = true;
        }, "打出");

        yield return new WaitUntil(() => confirmed);

        List<CardInstance> selected = CardDisplayPanel.Instance.GetSelectedCards();

        if (selected.Count == 0)
        {
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
            CleanupAfterPlacement();
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
            Debug.Log($"辉煌法师：法术费用和={totalCost}，限制为8");
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
            CleanupAfterPlacement();
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

            if ((td.spellType & SpellType.Counter) != 0)
            {
                NetworkPlayer.Local.handCards.Remove(cardObj);
                CounterManager.Instance?.PlayCounter(cardObj, true);
                var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
                if (counter != null) counter.noCostOnTrigger = true;
                Destroy(cardObj);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
            else if (td.targetType == TargetType.None)
            {
                NetworkPlayer.Local.handCards.Remove(cardObj);
                CardDrag.ExecuteSpellEffect(td, null);
                Destroy(cardObj);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
            else
            {
                if (!CardDrag.HasValidTargetStatic((TargetType)td.targetType))
                {
                Debug.Log($"辉煌法师：打出{td.cardName}无合法目标，跳过");
                    NetworkPlayer.Local.handCards.Remove(cardObj);
                    Destroy(cardObj);
                    continue;
                }
                NetworkPlayer.Local.handCards.Remove(cardObj);
                bool targetDone = false;
                SelectionManager.Instance.BeginSelection((TargetType)td.targetType, (slot) =>
                {
                    CardDrag.ExecuteSpellEffect(td, slot);
                    Destroy(cardObj);
                    targetDone = true;
                });
                yield return new WaitUntil(() => targetDone);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
        }

        CardDisplayPanel.Instance.Hide();
        CardDisplayPanel.Instance.multiSelect = false;
        CleanupAfterPlacement();
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
    public IEnumerator ThiefActiveExitEffect()
    {
        List<CardInstance> enemyCards = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null) enemyCards.Add(ci);
        }

        if (enemyCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            yield break;
        }

        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardInstance selected = null;

        CardDisplayPanel.Instance.ShowWithCallback(enemyCards, ci => true, () =>
        {
            selected = CardDisplayPanel.Instance.GetSelectedCard();
            confirmed = true;
        }, "打出");

        yield return new WaitUntil(() => confirmed);

        if (selected != null)
        {
            GameObject toRemove = null;
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                CardInstance ci = card?.GetComponent<CardInstance>();
                if (ci != null && ci.instanceID == selected.instanceID)
                {
                    toRemove = card;
                    break;
                }
            }
            if (toRemove != null)
            {
                NetworkPlayer.Local.handCards.Remove(toRemove);
                CardData template = CardDatabase.Instance?.GetTemplate(selected.templateID);
                if (template != null)
                    NetworkPlayer.Local.AddCardToHand(template);
                Destroy(toRemove);
            }
        }

        CardDisplayPanel.Instance.Hide();
    }
    public IEnumerator HonorAttendantActiveExit()
    {
        NetworkPlayer.Local.AddEnergy(2);

                // 清理重定向标记
        List<CardInstance> enemyCards = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null) enemyCards.Add(ci);
        }

        if (enemyCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            yield break;
        }

                // 清理重定向标记
        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(enemyCards, ci => true, () =>
        {
            confirmed = true;
        }, "打出");

                // 清理重定向标记
        ConfirmSelectionButton.Instance?.gameObject.SetActive(true);
        ConfirmSelectionButton.Instance?.Show(() =>
        {
            confirmed = true;
        });

        yield return new WaitUntil(() => confirmed);

                // 清理重定向标记
        List<GameObject> toRemove = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && (td.spellType & SpellType.Evil) != 0)
            {
                toRemove.Add(card);
            }
        }

        foreach (GameObject card in toRemove)
        {
            NetworkPlayer.Local.handCards.Remove(card);
            Destroy(card);
        }

        Debug.Log($"荣誉侍者弃掉{toRemove.Count}张邪恶法术");

        CardDisplayPanel.Instance.Hide();
    }
    public IEnumerator FearlessEnterEffect()
    {
        List<CounterCard> enemyCounters = CounterManager.Instance?.enemyCounters;
        if (enemyCounters == null || enemyCounters.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        foreach (var cc in enemyCounters)
        {
            if (cc.model != null)
            {
                Button btn = cc.model.GetComponent<Button>() ?? cc.model.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                var captured = cc;
                btn.onClick.AddListener(() => OnFearlessSelected(captured));
            }
        }

        selectedFearless = null;
        yield return new WaitUntil(() => selectedFearless != null);

        foreach (var cc in enemyCounters)
        {
            if (cc.model != null)
            {
                Button btn = cc.model.GetComponent<Button>();
                if (btn != null) Destroy(btn);
            }
        }

        if (selectedFearless != null)
        {
            CounterManager.Instance.TriggerEnemyCounterNoEffect(selectedFearless);
        }

        CleanupAfterPlacement();
    }

    CounterCard selectedFearless;

    void OnFearlessSelected(CounterCard cc)
    {
        selectedFearless = cc;
    }
    public IEnumerator MindScholarEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        // 1. 从赋予的特性中查找
        foreach (string trait in giver.mindScholarCopiedTraits)
        {
            if (trait.Contains("进场") && !giver.mindScholarEnterTriggeredThisPhase)
            {
                // 清理重定向标记
                string originalTemplateID = ExtractTemplateIDFromTrait(trait);
                if (!string.IsNullOrEmpty(originalTemplateID))
                {
                    CardData originalTD = CardDatabase.Instance?.GetTemplate(originalTemplateID);
                    if (originalTD != null && originalTD.hasOnEnter)
                    {
                        BoardSlot mySlot = FindSlotOf(giver);
                        if (mySlot != null)
                            mySlot.StartOnEnterEffect(originalTD, giver);
                        yield return null;
                        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                    }
                }
            }
        }
        giver.mindScholarEnterTriggeredThisPhase = true;

                // 清理重定向标记
        if (giver.HasDiscard && !giver.mindScholarDiscardTriggeredThisPhase)
        {
            foreach (string trait in giver.mindScholarCopiedTraits)
            {
                if (trait.Contains("抛置"))
                {
                    giver.mindScholarDiscardTriggeredThisPhase = true;
                    TriggerDiscardEffectFromTrait(giver, trait);
                    yield return null;
                    yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                    break;
                }
            }
        }

        // 2. 限制最多4张，太少退回去
        if (giver.mindScholarCopyCount >= 4)
        {
            CleanupAfterPlacement();
            yield break;
        }

        // 3. 选择对方基础费用1或3的召唤物
        List<CardInstance> targets = new List<CardInstance>();
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                CardData td = CardDatabase.Instance?.GetTemplate(ci?.templateID);
                if (td != null && (td.baseCost == 1 || td.baseCost == 3) && (td.hasOnEnter || ci.HasDiscard))
                    targets.Add(ci);
            }
        }

        if (targets.Count == 0) { CleanupAfterPlacement(); yield break; }

        bool done = false;
        CardInstance selected = null;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                CardData td = CardDatabase.Instance?.GetTemplate(ci?.templateID);
                if (td != null && (td.baseCost == 1 || td.baseCost == 3) && (td.hasOnEnter || ci.HasDiscard))
                {
                    selected = ci;
                    done = true;
                }
            }
        });
        yield return new WaitUntil(() => done);
        if (selected == null) { CleanupAfterPlacement(); yield break; }

        // 4. 选择复制进场还是抛置
        List<string> copyable = new List<string>();
        CardData selTD = CardDatabase.Instance?.GetTemplate(selected.templateID);
        if (selTD != null && selTD.hasOnEnter) copyable.Add("进场");
        if (selected.HasDiscard) copyable.Add("抛置");

        string chosenTrait = copyable.Count == 1 ? copyable[0] : null;
        if (copyable.Count == 2)
        {
            bool choiceDone = false;
            GenericChoicePanel.Instance.Show("选择复制特性", copyable, (index) =>
            {
                chosenTrait = copyable[index];
                choiceDone = true;
            });
            yield return new WaitUntil(() => choiceDone);
        }

        if (chosenTrait == null) { CleanupAfterPlacement(); yield break; }

        // 5. 复制并触发
        giver.mindScholarCopyCount++;
        string traitText = GetTraitFullText(selected, chosenTrait);
        string recordText = $"{selected.templateID}:{chosenTrait}:{traitText}";
        giver.mindScholarCopiedTraits.Add(recordText);
        giver.GrantTrait(traitText);

        if (chosenTrait == "进场")
        {
            CardData originalTD = CardDatabase.Instance?.GetTemplate(selected.templateID);
            if (originalTD != null && originalTD.hasOnEnter)
            {
                BoardSlot mySlot = FindSlotOf(giver);
                if (mySlot != null)
                    mySlot.StartOnEnterEffect(originalTD, giver);
            }
        }
        else if (chosenTrait == "抛置")
        {
            if (!giver.mindScholarDiscardTriggeredThisPhase)
            {
                giver.mindScholarDiscardTriggeredThisPhase = true;
                TriggerDiscardEffectFromTrait(giver, recordText);
            }
        }

        CleanupAfterPlacement();
    }

    string ExtractTemplateIDFromTrait(string recordText)
    {
        string[] parts = recordText.Split(':');
        return parts.Length > 0 ? parts[0] : null;
    }
    void TriggerDiscardEffectFromTrait(CardInstance ci, string recordText)
    {
        string templateID = ExtractTemplateIDFromTrait(recordText);
        if (string.IsNullOrEmpty(templateID)) return;

        // 根据原卡牌的templateID触发抛置效果
        switch (templateID)
        {
            case "01343": // 不稳定实验品：对对方一召唤物造成攻击力数值的伤害
                if (HasEnemyTarget())
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, ci.currentAttack, null);
                                t3d.UpdateValues();
                            }
                        }
                        BoardSlot.CheckAndHandleDeaths();
                    });
                }
                break;
            case "01136": // 难民：对对方一召唤物造成1伤害
                if (HasEnemyTarget())
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlotID, (target) =>
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
                break;
            case "01346": // 士兵：为己方一召唤物恢复3生命值
                if (HasAllyTarget(ci))
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleAlly, mySlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Minion);
                            t3d?.UpdateValues();
                        }
                    });
                }
                break;
            case "01344": // 诅咒女巫：使对方攻击力永久-2
                if (HasEnemyTarget())
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                t3d.cardInstance.baseAttack -= 2;
                                t3d.cardInstance.currentAttack = Mathf.Max(0, t3d.cardInstance.currentAttack - 2);
                                t3d.UpdateValues();
                            }
                        }
                    });
                }
                break;
            case "01135": // 杂耍大师：交换己方两召唤物
                if (HasAllyTarget(ci))
                {
                    HandManager hm = FindObjectOfType<HandManager>();
                    hm.StartCoroutine(hm.SwapTwoAllies());
                }
                break;
           
        }
    }
    bool HasAllyTarget(CardInstance source)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (!BoardManager.GetSideRangeOf(source, out int s, out int e)) return false;
        for (int i = s; i <= e; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) return true;
        return false;
    }

    /// <summary>
    /// 弃牌专用选择方法，自动排除掉自己的槽位
    /// </summary>
    public static void StartDiscardSelection(TargetType targetType, int ignoreSlotID, Action<BoardSlot> onSelected)
{
    Card3DHover.ignoreSlotID = ignoreSlotID;
    SelectionManager.Instance.BeginSelection(targetType, (selectedSlot) =>
    {
        if (selectedSlot.slotID == Card3DHover.ignoreSlotID)
        {
            Card3DHover.ignoreSlotID = -1;
            return;
        }
        Card3DHover.ignoreSlotID = -1;
        onSelected?.Invoke(selectedSlot);
    });
}

}