using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private Image slotImage;
    private Color normalColor;
    public Color highlightColor = Color.yellow;

    public static bool attachCanBeIndependent = false;
    public int slotTempAttackBoost;
    private GameObject _currentCard;
    public static bool isStrengtheningSlot = false;
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
    // 退场效果数据包（不依赖CardInstance，支持延迟触发）
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

        if (isTargetingMode && IsValidTarget(currentTargetType))
            HighlightRow(false);

        transform.localScale = originalScale;
        slotImage.color = isBlocked ? Color.gray : normalColor;
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
        // 进场重定向拦截
        if (GlobalEventManager.Instance?.PendingEnterRedirectInstance == inst)
        {
            CardData redirectTemplate = GlobalEventManager.Instance.PendingEnterRedirectTemplate;
            GlobalEventManager.Instance.PendingEnterRedirectTemplate = null;
            GlobalEventManager.Instance.PendingEnterRedirectInstance = null;
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
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
        if (template.templateID == "03504")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            for (int i = 0; i <= 5; i++)
            {
                BoardSlot es = bm?.GetSlot(i);
                if (es?.currentCard3D != null)
                {
                    Card3DInstance ei = es.currentCard3D.GetComponent<Card3DInstance>();
                    if (ei?.cardInstance != null)
                    {
                        BattleManager.Instance?.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                        ei.UpdateValues();
                    }
                }
            }
            BoardSlot.CheckAndHandleDeaths();
            CleanupAfterPlacement();
            return;
        }

        if (template.templateID == "03506")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            for (int i = 0; i <= 5; i++)
            {
                BoardSlot es = bm?.GetSlot(i);
                if (es?.currentCard3D != null)
                {
                    Card3DInstance ei = es.currentCard3D.GetComponent<Card3DInstance>();
                    if (ei?.cardInstance != null)
                    {
                        BattleManager.Instance?.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                        ei.UpdateValues();
                    }
                }
            }
            BoardSlot.CheckAndHandleDeaths();
            CleanupAfterPlacement();
            return;
        }

        switch (template.templateID)
        {
            case "03501":
                GlobalEventManager.Instance.RegisterAura(new SuppressorAura { source = inst });
                if (!HasEnemyTarget()) { CleanupAfterPlacement(); return; }
                {
                    SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                    {
                        if (targetSlot != null && targetSlot.currentCard3D != null)
                        {
                            targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.silencedThisPhase = true;
                        }
                    });
                }
                return;
            case "03503":
                GlobalEventManager.Instance.RegisterAura(new SageAura { source = inst });
                CleanupAfterPlacement();
                return;
            case "03511":
                Debug.Log("灾厄行者进场，准备订阅");
                GlobalEventManager.Instance.OnPlayerDamaged += OnDisasterWalkerDamage;
                inst._disasterWalkerHandler = OnDisasterWalkerDamage;
                CleanupAfterPlacement();
                return;
            case "01104":
                if (!HasEnemyTarget()) { CleanupAfterPlacement(); return; }
                SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                {
                    if (targetSlot != null && targetSlot.currentCard3D != null)
                    {
                        Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (t3d != null)
                        {
                            BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 1, currentCard3D);
                            t3d.UpdateValues();
                        }
                    }
                    BoardSlot.CheckAndHandleDeaths();
                    CleanupAfterPlacement();
                });
                break;
          
            case "01110":
                if (!HasAllyTargetExceptSelf()) { CleanupAfterPlacement(); return; }
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                {
                    if (targetSlot != null && targetSlot.currentCard3D != null && targetSlot != this)
                    {
                        targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.isActiveExit = true;
                        targetSlot.HandleDeath(targetSlot.currentCard3D);
                    }
                    CleanupAfterPlacement();
                });
                break;
            case "01311":
                StartCoroutine(ConductorEnterEffect(inst));
                return;
            case "01313":
                if (!HasAllyTargetExceptSelf()) { CleanupAfterPlacement(); return; }
                {
                    string jdLayerId = SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                    BoardSlot.onTargetSelected = (targetSlot) =>
                    {
                        if (targetSlot == this || targetSlot == null || targetSlot.currentCard3D == null) return;

                        SelectionManager.Instance.EndSelection(jdLayerId);

                        Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (t3d != null)
                        {
                            int atk = t3d.cardInstance.currentAttack;
                            int hp = t3d.cardInstance.currentHealth;
                            CardInstance targetInst = t3d.cardInstance;
                            t3d.cardInstance.isActiveExit = true;
                            targetSlot.HandleDeath(t3d.gameObject);
                            if (!targetInst.handledReturnToHand)
                            {
                                CardData tt = CardDatabase.Instance?.GetTemplate(targetInst.templateID);
                                if (tt != null) NetworkPlayer.Local.AddCardToHandFromInstance(tt, targetInst);
                            }
                            Card3DInstance self3D = currentCard3D?.GetComponent<Card3DInstance>();
                            if (self3D != null)
                            {
                                self3D.cardInstance.currentAttack += atk;
                                self3D.cardInstance.currentHealth += hp;
                                self3D.cardInstance.currentMaxHealth += hp;
                                self3D.UpdateValues();
                            }
                        }
                        CleanupAfterPlacement();
                    };
                }
                break;
            case "01314":
                StartCoroutine(HeartthrobEnterEffect(inst));
                return;
            case "01317":
                if (inst.greedySnakeEnterCount >= 3)
                {
                    Debug.Log("贪欲之蛇：进场已达3次，无效果");
                    CleanupAfterPlacement();
                    return;
                }
                if (!HasEnemyTarget())
                {
                    Debug.Log("贪欲之蛇：敌方无召唤物");
                    CleanupAfterPlacement();
                    return;
                }
                {
                    string layerId = SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                    {
                        if (targetSlot?.currentCard3D != null)
                        {
                            CardInstance targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (targetCI != null)
                            {
                                StartCoroutine(GreedySnakeCopyProcess(inst, targetCI));
                                return;
                            }
                        }
                        CleanupAfterPlacement();
                    });
                }
                return;
            case "01319":
                StartCoroutine(FearlessEnterEffect());
                return;
            case "01322":
                StartCoroutine(RemnantEnterEffect(inst));
                return;
            case "01329":
                StartCoroutine(ApprenticeMageEnterEffect(inst));
                return;
            case "01331":
                StartCoroutine(PrisonEnterEffect(inst));
                return;
            case "01335":
                {
                    BoardManager bmScroll = FindObjectOfType<BoardManager>();
                    int mySlot = -1;
                    if (inst.isAttached)
                    {
                        mySlot = inst.hostSlotID;
                    }
                    else
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            if (bmScroll?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == inst)
                            { mySlot = i; break; }
                        }
                    }
                    if (mySlot >= 0)
                    {
                        GlobalEventManager.Instance.RegisterAura(new EnergyHackerAura
                        {
                            source = inst,
                            hostSlotID = mySlot,
                            mySlotID = mySlot
                        });
                    }
                }
                CleanupAfterPlacement();
                return;
            case "01337":
                StartCoroutine(PirateEnterEffect(inst));
                return;
            case "01323":
                GlobalEventManager.Instance.RegisterAura(new JudgeAura { source = inst });
                CleanupAfterPlacement();
                return;
            case "01348":
                if (CounterManager.Instance == null || CounterManager.Instance.enemyCounters.Count == 0)
                {
                    Debug.Log("阴谋家进场：对方无反制牌");
                    CleanupAfterPlacement();
                    return;
                }
                GenericChoicePanel.Instance.Show("选择强化", new List<string> { "+3+0", "+0+3" }, (index) =>
                {
                    if (index == 0)
                    {
                        inst.currentHealth += 3;
                        inst.currentMaxHealth += 3;
                    }
                    else
                    {
                        inst.currentAttack += 3;
                    }
                    Card3DInstance c3d = FindGiver3D(inst);
                    c3d?.UpdateValues();
                    CleanupAfterPlacement();
                });
                return;
            case "01349":
                HandManager hmCollector = FindObjectOfType<HandManager>();
                hmCollector.StartCoroutine(hmCollector.CollectorEnterEffect(inst));
                return;

            case "01108":
                if (CounterManager.Instance != null && CounterManager.Instance.enemyCounters.Count > 0)
                {
                    NetworkPlayer.Local.currentEnergy -= 1;
                    NetworkPlayer.Local.UpdateUI();
                }
                CleanupAfterPlacement();
                return;

            case "01117":
                if (!HasEnemyTarget()) { CleanupAfterPlacement(); return; }
                if (inst.giveableDeathTraits == null || inst.giveableDeathTraits.Count == 0) { CleanupAfterPlacement(); return; }
                SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                {
                    if (targetSlot != null && targetSlot.currentCard3D != null)
                    {
                        CardInstance targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (targetCI != null)
                        {
                            SufferingGiverPanel.Instance.Show(
                                new List<string>(inst.giveableDeathTraits),
                                (chosenTrait) =>
                                {
                                    ApplySufferingGiverEffect(inst, targetCI, chosenTrait);
                                    CleanupAfterPlacement();
                                }
                            );
                            return;
                        }
                    }
                    CleanupAfterPlacement();
                });
                break;

            case "01127":
                StartCoroutine(ReformerEnterEffect(inst));
                return;
            case "01501":
                StartCoroutine(EmperorEnterEffect(inst));
                return;
            case "01502":
                StartCoroutine(ShadowMasterEnterEffect(inst));
                return;
            case "01503":
                StartCoroutine(LordEnterEffect(inst));
                return;
            case "01504":
                StartCoroutine(WolfKingEnterEffect(inst));
                return;
            case "01505":
                StartCoroutine(BlockerEnterEffect(inst));
                return;
            case "01506":
                StartCoroutine(AmplifierEnterEffect(inst));
                return;
            case "01507":
                if (!HasAllyTargetExceptSelf()) { CleanupAfterPlacement(); return; }
                {
                    string layerId = SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                    {
                        if (targetSlot != null && targetSlot.currentCard3D != null && targetSlot != this)
                        {
                            CardInstance targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (targetCI != null)
                            {
                                targetCI.hasLifePriestBlessing = true;
                                targetCI.lifePriestBlessingSource = inst;
                            }
                        }
                        CleanupAfterPlacement();
                    });
                }
                return;
            case "01509":
                StartCoroutine(TerroristEnterEffect(inst));
                return;
            case "01511":
                if (inst.mindScholarEnterTriggeredThisPhase)
                {
                    CleanupAfterPlacement();
                    return;
                }
                StartCoroutine(MindScholarEnterEffect(inst));
                return;
            case "01514":
                CardData follower = CardDatabase.Instance?.GetTemplate("03001");
                if (follower != null)
                {
                    NetworkPlayer.Local.AddCardToHand(follower);
                    NetworkPlayer.Local.AddCardToHand(follower);
                }
                CleanupAfterPlacement();
                return;
            case "01515":
                StartCoroutine(FanaticShamanEnterEffect(inst));
                return;
            case "01516":
                inst.GrantShield(false, false, true);
                CleanupAfterPlacement();
                return;
            case "01517":
                {
                    var aura = new MistHiderAura { source = inst };
                    GlobalEventManager.Instance.RegisterAura(aura);
                    aura.ApplyHide();
                }
                StartCoroutine(MistHiderEnterEffect(inst));
                return;
            case "01520":
                GlobalEventManager.Instance.RegisterAura(new MerchantAura { source = inst });
                foreach (GameObject card in NetworkPlayer.Local.handCards)
                {
                    if (card == null) continue;
                    CardInstance ci = card.GetComponent<CardInstance>();
                    if (ci == null) continue;
                    CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (td != null && td.cardType == CardType.Summon && !ci.merchantDiscounted)
                    {
                        ci.merchantDiscounted = true;
                        card.GetComponent<CardDisplay2D>()?.Refresh();
                    }
                }
                CleanupAfterPlacement();
                return;
            case "01521":
                StartCoroutine(BrilliantMageEnterEffect(inst));
                return;
            case "01523":
                StartCoroutine(InkEnterEffect(inst));
                return;
            case "01524":
                int scrollCount = 0;
                BoardManager bmCore = FindObjectOfType<BoardManager>();
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot s = bmCore?.GetSlot(i);
                    if (s?.currentCard3D != null)
                    {
                        CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.prefixes.Contains("神灵画卷") && ci != inst)
                            scrollCount++;
                    }
                }
                Debug.Log($"画卷之核进场: scrollCount={scrollCount}");
                if (scrollCount >= 2)
                {
                    for (int i = 0; i <= 5; i++)
                    {
                        BoardSlot s = bmCore?.GetSlot(i);
                        if (s?.currentCard3D != null)
                        {
                            Debug.Log($"画卷之核令槽位{s.slotID}退场");
                            CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ci != null)
                            {
                                ci.isActiveExit = true;
                                s.HandleDeath(s.currentCard3D);
                            }
                        }
                    }
                }
                CleanupAfterPlacement();
                return;
            case "01528":
                if (!inst.isAttached)
                    GlobalEventManager.Instance.RegisterAura(new EnergyReaperAura { source = inst });
                foreach (GameObject card in NetworkPlayer.Local.handCards)
                {
                    if (card == null) continue;
                    CardInstance ci = card.GetComponent<CardInstance>();
                    if (ci == null) continue;
                    CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (td != null && td.cardType == CardType.Summon && ci.prefixes.Contains("灵能") && !ci.energyReaperDiscounted)
                    {
                        ci.energyReaperDiscounted = true;
                        card.GetComponent<CardDisplay2D>()?.Refresh();
                    }
                }
                CleanupAfterPlacement();
                return;
        }
        BoardSlot.SyncMistHiderDisplay();
    }

  

    void CleanupAfterPlacement()
    {
        isPlacingCard = false;
        cardToPlace = null;

        if (!isTargetingMode && !isAttachSelectMode)
        {
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.SetHandAreaRaycast(true);
            hm?.ShowAllCards();
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        }
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
    }

    bool HasEnemyTarget()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int id = 0; id <= 5; id++)
        {
            BoardSlot slot = bm?.GetSlot(id);
            if (slot != null && !slot.isBlocked && slot.hasCard) return true;
        }
        return false;
    }

    bool HasAllyTargetExceptSelf()
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
        bool anyDied;
        do
        {
            anyDied = false;
            List<GameObject> died = new List<GameObject>();
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance != null && c3d.cardInstance.currentHealth <= 0)
                    died.Add(slot.currentCard3D);
            }
            foreach (GameObject dead in died)
            {
                for (int i = 0; i < 12; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot.currentCard3D == dead) { slot.HandleDeath(dead); anyDied = true; break; }
                }
            }
        } while (anyDied);

        // 更新所有X数值单位
        HandManager hm = FindObjectOfType<HandManager>();
        if (hm != null)
        {
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isXValue)
                    hm.UpdateXValues(ci);
            }
        }
    }

    public void HandleDeath(GameObject dyingCard)
    {
        if (dyingCard == null) return;
        Card3DInstance c3d = dyingCard.GetComponent<Card3DInstance>();
        if (c3d == null || c3d.cardInstance == null) return;
        c3d.cardInstance.hasLifePriestBlessing = false;
        c3d.cardInstance.lifePriestBlessingSource = null;
        string templateID = c3d.cardInstance.templateID;
        bool isActiveExit = c3d.cardInstance.isActiveExit;  
        // 全局退场事件检测
        GlobalDeathEventHandler.Trigger(c3d.cardInstance, slotID, c3d.cardInstance.damageSourceInstanceIDs, isActiveExit);
        // 守墓人导致的退场不触发退场效果
        if (c3d.cardInstance != null)
        {
            foreach (string sourceID in c3d.cardInstance.damageSourceInstanceIDs)
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                bool fromGravekeeper = false;
                for (int i = 6; i <= 11; i++)
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
        // 退场特性拦截
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(c3d.cardInstance, "退场"))
        {
            c3d.cardInstance.hasOnDeath = false;
            c3d.cardInstance.hasActiveExit = false;
        }
        // 未弃之人：退场改为主动退场
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

        if (templateID == "03503" || templateID == "03501")
        {
            GlobalEventManager.Instance?.UnregisterAuraOfSource(c3d.cardInstance);
        }
        if (templateID == "03501")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot ally = bm.GetSlot(i);
                    if (ally?.currentCard3D != null)
                        ally.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
        }
        if (templateID == "03511")
        {
            if (c3d.cardInstance._disasterWalkerHandler != null)
                GlobalEventManager.Instance.OnPlayerDamaged -= c3d.cardInstance._disasterWalkerHandler;
        }
        if (templateID == "01106")
        {
            if (isActiveExit) NetworkPlayer.Local.AddEnergy(3);
            else NetworkPlayer.Local.AddEnergy(1);
        }

        bool shouldReturn03504 = false;
        CardData template03504 = null;
        if (templateID == "03504")
        {
            shouldReturn03504 = c3d.cardInstance.currentCost > 0 && !c3d.cardInstance.enteredWithZeroCost;
            c3d.cardInstance.costReduction++;
            c3d.cardInstance.currentCost = Mathf.Max(0, c3d.cardInstance.currentCost - 1);
            if (shouldReturn03504)
            {
                c3d.cardInstance.handledReturnToHand = true;
                template03504 = CardDatabase.Instance?.GetTemplate(templateID);
            }
        }

        if (templateID == "01107" && isActiveExit)
        {
            Debug.Log("妖精护盾选择前");
            NetworkPlayer.Local.AddEnergy(2);
            bool hasAlly = false;
            BoardManager bm = FindObjectOfType<BoardManager>();
            for (int i = 6; i <= 11; i++)
            {
                if (bm?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
            }
            Debug.Log($"妖精 hasAlly={hasAlly}");
            if (hasAlly)
            {
                Debug.Log("妖精 BeginSelection 调用");
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
                {
                    Debug.Log($"妖精选择回调: target={target?.slotID}");
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
                Debug.Log("妖精 BeginSelection 返回");
            }
        }

        if (templateID == "01111" && isActiveExit)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D == null) continue;
                    CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.templateID != "01111" && ci.templateID != "01301")
                    {
                        TriggerDeathEffect(ci, true);
                    }
                }
        }

        if (templateID == "01301")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D == null) continue;
                    CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.templateID != "01111" && ci.templateID != "01301")
                        TriggerDeathEffect(ci, isActiveExit);
                }
        }

        if (templateID == "01306" && isActiveExit)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                int highestAtk = -1;
                BoardSlot targetSlot = null;
                for (int i = 0; i <= 5; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D == null) continue;
                    Card3DInstance ce = slot.currentCard3D.GetComponent<Card3DInstance>();
                    if (ce?.cardInstance != null && ce.cardInstance.currentAttack > highestAtk)
                    { highestAtk = ce.cardInstance.currentAttack; targetSlot = slot; }
                }
                if (targetSlot != null)
                {
                    targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.isActiveExit = true;
                    targetSlot.HandleDeath(targetSlot.currentCard3D);
                }
            }
        }

        if (templateID == "01307" && isActiveExit)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                int highestHp = -1;
                BoardSlot targetSlot = null;
                for (int i = 0; i <= 5; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D == null) continue;
                    Card3DInstance ce = slot.currentCard3D.GetComponent<Card3DInstance>();
                    if (ce?.cardInstance != null && ce.cardInstance.currentHealth > highestHp)
                    { highestHp = ce.cardInstance.currentHealth; targetSlot = slot; }
                }
                if (targetSlot != null)
                {
                    targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.isActiveExit = true;
                    targetSlot.HandleDeath(targetSlot.currentCard3D);
                }
            }
        }
        if (templateID == "01309")
        {
            StartCoroutine(RogueDeathEffect(c3d.cardInstance));
        }
        if (templateID == "01311" && isActiveExit)
        {
            // 选己方召唤物退场，双倍+返还费用
            if (HasAllyTargetExceptSelf())
            {
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                {
                    if (targetSlot != null && targetSlot.currentCard3D != null && targetSlot != this)
                    {
                        CardInstance targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (targetCI != null)
                        {
                            NetworkPlayer.Local.AddEnergy(targetCI.currentCost);
                            targetCI.isActiveExit = true;
                            targetCI._conductorDoubleDeath = true;
                            targetSlot.HandleDeath(targetSlot.currentCard3D);
                        }
                    }
                });
            }
        }
        if (templateID == "01316")
        {
            if (isActiveExit)
            {
                // 主动退场：从对方手牌挑选一张获得
                StartCoroutine(ThiefActiveExitEffect());
            }
            else
            {
                // 普通退场：摸两张牌
                NetworkPlayer.Local.DrawCardWithoutLimit();
                NetworkPlayer.Local.DrawCardWithoutLimit();
            }
        }
        if (templateID == "01320")
        {
            int targetSum = isActiveExit ? 10 : 4;
            int totalCost = 0;
            int drawnCount = 0;
            while (totalCost < targetSum && drawnCount < 20)
            {
                CardData data = DeckManager.Instance?.DrawFromMain();
                if (data == null) break;
                NetworkPlayer.Local.AddCardToHand(data);
                totalCost += data.baseCost;
                drawnCount++;
            }
            Debug.Log($"魔术师退场：摸牌{drawnCount}张，总基础费用{totalCost}");
        }
        if (templateID == "01321")
        {
            StartCoroutine(RiddlerDeathEffect(c3d.cardInstance));
        }
        if (templateID == "01323")
        {
            GlobalEventManager.Instance?.UnregisterAuraOfSource(c3d.cardInstance);
        }
        if (templateID == "01325" && isActiveExit)
        {
            int baseHP = Mathf.Max(0, c3d.cardInstance.currentHealth);
            int energyGain = baseHP * 2;
            NetworkPlayer.Local._energyCanExceedLimit = true;
            NetworkPlayer.Local.AddEnergy(energyGain);
        }
        if (templateID == "01331")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (c3d.cardInstance.prisonMySlot >= 0)
            {
                BoardSlot s = bm?.GetSlot(c3d.cardInstance.prisonMySlot);
                if (s != null)
                {
                    s.prisonBlocked = false;
                    s.prisonAllowYuan = false;
                    s.slotImage.color = s.isBlocked ? Color.gray : s.normalColor;
                }
            }
            if (c3d.cardInstance.prisonEnemySlot >= 0)
            {
                BoardSlot s = bm?.GetSlot(c3d.cardInstance.prisonEnemySlot);
                if (s != null)
                {
                    s.prisonBlocked = false;
                    s.prisonAllowYuan = false;
                    s.slotImage.color = s.isBlocked ? Color.gray : s.normalColor;
                }
            }
        }
        if (templateID == "01335")
        {
            GlobalEventManager.Instance?.UnregisterAuraOfSource(c3d.cardInstance);
        }
        if (templateID == "01338" && isActiveExit)
        {
            StartCoroutine(DeepSeaActiveExitEffect());
        }
        if (templateID == "01347")
        {
            if (isActiveExit)
            {
                // 主动退场：+2能量，展示对方手牌并弃掉邪恶法术
                StartCoroutine(HonorAttendantActiveExit());
            }
            else
            {
                // 普通退场：对对方一召唤物造成2伤害
                if (HasEnemyTarget())
                {
                    SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 2, null);
                                t3d.UpdateValues();
                            }
                        }
                        BoardSlot.CheckAndHandleDeaths();
                    });
                }
            }
        }
        if (templateID == "01502")
        {
            CardInstance.shadowMasterAlive = false;
        }
        if (templateID == "01511")
        {
            if (!c3d.cardInstance.handledReturnToHand)
            {
                c3d.cardInstance.handledReturnToHand = true;
                CardData template = CardDatabase.Instance?.GetTemplate(templateID);
                if (template != null)
                    NetworkPlayer.Local.AddCardToHandFromInstance(template, c3d.cardInstance);
            }
        }
        if (templateID == "01515")
        {
            GlobalEventManager.Instance?.UnregisterAuraOfSource(c3d.cardInstance);
        }
        if (templateID == "01517")
        {
            var auras = GlobalEventManager.Instance?.GetAurasOfSource(c3d.cardInstance);
            if (auras != null)
            {
                foreach (var a in auras)
                {
                    if (a is MistHiderAura mistAura)
                        mistAura.RemoveHide();
                }
            }
            GlobalEventManager.Instance?.UnregisterAuraOfSource(c3d.cardInstance);
        }
        if (templateID == "01520")
        {
            NetworkPlayer.Local.DrawCardWithoutLimit();
            GlobalEventManager.Instance?.UnregisterAuraOfSource(c3d.cardInstance);

            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                if (card == null) continue;
                CardInstance ci = card.GetComponent<CardInstance>();
                if (ci != null && ci.merchantDiscounted)
                {
                    ci.merchantDiscounted = false;
                    card.GetComponent<CardDisplay2D>()?.Refresh();
                }
            }
        }
        if (templateID == "01528")
        {
            GlobalEventManager.Instance?.UnregisterAuraOfSource(c3d.cardInstance);
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                if (card == null) continue;
                CardInstance ci = card.GetComponent<CardInstance>();
                if (ci != null && ci.energyReaperDiscounted)
                {
                    ci.energyReaperDiscounted = false;
                    card.GetComponent<CardDisplay2D>()?.Refresh();
                }
            }
        }
        if (templateID == "03513")
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                for (int i = 0; i <= 5; i++)
                {
                    BoardSlot es = bm.GetSlot(i);
                    if (es?.currentCard3D != null)
                    {
                        Card3DInstance ei = es.currentCard3D.GetComponent<Card3DInstance>();
                        if (ei?.cardInstance != null)
                        {
                            BattleManager.Instance.ApplyDamageToMinionPublic(ei.cardInstance, 1, dyingCard);
                            ei.UpdateValues();
                        }
                    }
                }
            }
           
        }
        if (templateID == "03020" && !c3d.cardInstance.handledReturnToHand)
        {
            if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(c3d.cardInstance))
            {
                CardData next = CardDatabase.Instance?.GetTemplate("03021");
                if (next != null) NetworkPlayer.Local.AddCardToHand(next);
            }
        }
        if (templateID == "03021" && !c3d.cardInstance.handledReturnToHand)
        {
            if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(c3d.cardInstance))
            {
                CardData next = CardDatabase.Instance?.GetTemplate("03022");
                if (next != null) NetworkPlayer.Local.AddCardToHand(next);
            }
        }

        bool shouldReturn03009 = false;
        CardData template03009 = null;
        if (templateID == "03009")
        {
            if (!c3d.cardInstance.handledReturnToHand)
            {
                c3d.cardInstance.handledReturnToHand = true;
                template03009 = CardDatabase.Instance?.GetTemplate(templateID);
                shouldReturn03009 = true;
            }
        }

        bool shouldReturn01117 = false;
        CardData template01117 = null;
        if (templateID == "01117")
        {
            bool shouldReturnToHand = false;
            if (!isActiveExit) shouldReturnToHand = true;

            foreach (string trait in c3d.cardInstance.giveableDeathTraits)
            {
                switch (trait)
                {
                    case "退场：减一能量":
                        NetworkPlayer.Local.currentEnergy -= 1;
                        NetworkPlayer.Local.UpdateUI();
                        break;
                    case "退场：己方全体受到一伤害":
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        if (bm != null)
                            for (int i = 6; i <= 11; i++)
                            {
                                BoardSlot slot = bm.GetSlot(i);
                                if (slot?.currentCard3D != null)
                                {
                                    Card3DInstance ca = slot.currentCard3D.GetComponent<Card3DInstance>();
                                    if (ca?.cardInstance != null && ca.cardInstance != c3d.cardInstance)
                                    {
                                        BattleManager.Instance.ApplyDamageToMinionPublic(ca.cardInstance, 1, dyingCard);
                                        ca.UpdateValues();
                                    }
                                }
                            }
                        
                        break;
                    case "退场：己方玩家扣一血":
                        NetworkPlayer.Local.TakeDamage(1);
                        break;
                }
            }

            if (shouldReturnToHand && !c3d.cardInstance.handledReturnToHand)
            {
                c3d.cardInstance.handledReturnToHand = true;
                template01117 = CardDatabase.Instance?.GetTemplate(templateID);
                shouldReturn01117 = true;
            }
        }
        if (templateID == "01522")
        {
            // 退场效果，无论主动还是普通
            StartCoroutine(MartyrDeathEffectCoroutine(c3d.cardInstance));
        }
        if (c3d.cardInstance.tempHealthBoost > 0)
            c3d.cardInstance.currentHealth -= c3d.cardInstance.tempHealthBoost;
        c3d.cardInstance.currentAttack -= c3d.cardInstance.tempAttackBoost;
        c3d.cardInstance.tempAttackBoost = 0;
        c3d.cardInstance.tempHealthBoost = 0;
       
        if (c3d.cardInstance.tempHealthBoost > 0)
            c3d.cardInstance.currentHealth -= c3d.cardInstance.tempHealthBoost;
        c3d.cardInstance.currentAttack -= c3d.cardInstance.tempAttackBoost;
        c3d.cardInstance.tempAttackBoost = 0;
        c3d.cardInstance.tempHealthBoost = 0;
        c3d.cardInstance.currentAttack = c3d.cardInstance.baseAttack;
        c3d.cardInstance.currentHealth = c3d.cardInstance.baseHealth;
        c3d.cardInstance.currentMaxHealth = c3d.cardInstance.baseMaxHealth;
        c3d.cardInstance.currentTier = c3d.cardInstance.baseTier;
        GraveEntry entry = new GraveEntry();
        entry.templateID = c3d.cardInstance.templateID;
        entry.instanceID = c3d.cardInstance.instanceID;
        entry.currentCost = c3d.cardInstance.currentCost;
        entry.currentAttack = c3d.cardInstance.currentAttack;
        entry.baseAttack = c3d.cardInstance.baseAttack;
        entry.currentHealth = c3d.cardInstance.currentHealth;
        entry.baseHealth = c3d.cardInstance.baseHealth;
        entry.baseMaxHealth = c3d.cardInstance.baseMaxHealth;
        entry.currentMaxHealth = c3d.cardInstance.currentMaxHealth;
        entry.currentTier = c3d.cardInstance.currentTier;
        entry.baseTier = c3d.cardInstance.baseTier;
        entry.prefixes = c3d.cardInstance.prefixes;
        entry.handledReturnToHand = false;
        entry.deathPhase = TurnManager.Instance.phaseCount;
        GraveyardManager.Instance.AddToGraveyard(entry);
     
        // 指挥家双倍退场
        if (c3d.cardInstance._conductorDoubleDeath)
        {
            c3d.cardInstance._conductorDoubleDeath = false;
            DeathEffectData data = ExtractDeathData(c3d.cardInstance);
            data.slotID = this.slotID;
            StartCoroutine(ConductorDoubleDeathEffect(data));
        }
        // 古老精灵：宿主退场后重新附着
        if (c3d.cardInstance != null && c3d.cardInstance.isAttached == false)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            List<GameObject> fairies = new List<GameObject>();
            foreach (GameObject obj in bm.attachedModels)
            {
                Card3DInstance c3dAtt = obj?.GetComponent<Card3DInstance>();
                if (c3dAtt?.cardInstance != null && c3dAtt.cardInstance.isAncientFairy && c3dAtt.cardInstance.hostSlotID == slotID)
                {
                    fairies.Add(obj);
                }
            }

            foreach (GameObject fairy in fairies)
            {
                // 先从列表移除，避免重复
                bm.attachedModels.Remove(fairy);

                bool hasOtherAlly = false;
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s != null && s.hasCard && s.slotID != slotID)
                    {
                        hasOtherAlly = true;
                        break;
                    }
                }

                if (hasOtherAlly)
                {
                    StartCoroutine(AncientFairyReattach(fairy, slotID));
                    Debug.Log($"古老精灵协程启动: fairy={fairy.name}, oldHost={slotID}");
                }
                else
                {
                    CardInstance fairyCI = fairy.GetComponent<Card3DInstance>()?.cardInstance;
                    if (fairyCI != null)
                    {
                        fairyCI.isActiveExit = true;
                    }
                    Destroy(fairy);
                }
            }
        }
        SetCard(null);

        // 复生造物：原地召唤杂兵
        if (c3d.cardInstance._rebornSummon)
        {
            CardData soldierTemplate = CardDatabase.Instance?.GetTemplate("03004");
            if (soldierTemplate?.prefab3D != null && !this.isBlocked)
            {
                GameObject temp = new GameObject("TempSoldier");
                CardInstance ti = temp.AddComponent<CardInstance>();
                ti.InitFromTemplate(soldierTemplate, 0);
                HandManager hm = FindObjectOfType<HandManager>();
                hm.PlaceCardToSlot(this, temp);
                Destroy(temp);
            }
        }
        if (shouldReturn03504 && template03504 != null)
            NetworkPlayer.Local.AddCardToHandFromInstance(template03504, c3d.cardInstance);
        if (shouldReturn01117 && template01117 != null)
            NetworkPlayer.Local.AddCardToHandFromInstance(template01117, c3d.cardInstance);
        if (shouldReturn03009 && template03009 != null)
            NetworkPlayer.Local.AddCardToHandFromInstance(template03009, c3d.cardInstance);
        Destroy(dyingCard);
        HandManager hmDeath = FindObjectOfType<HandManager>();
        if (hmDeath != null)
        {
            BoardManager bmDeath = FindObjectOfType<BoardManager>();
            if (bmDeath != null)
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot sd = bmDeath.GetSlot(i);
                    if (sd?.currentCard3D == null) continue;
                    CardInstance ci = sd.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.isXValue) hmDeath.UpdateXValues(ci);
                }
        }

        BoardManager bmAtt = FindObjectOfType<BoardManager>();
        if (bmAtt != null)
            for (int i = bmAtt.attachedModels.Count - 1; i >= 0; i--)
            {
                GameObject obj = bmAtt.attachedModels[i];
                if (obj == null) continue;
                Card3DInstance ca = obj.GetComponent<Card3DInstance>();
                if (ca?.cardInstance != null && ca.cardInstance.hostSlotID == slotID)
                {
                    if (ca.cardInstance.isAncientFairy) continue;
                    bmAtt.attachedModels.RemoveAt(i);
                    Destroy(obj);
                }
            }
        BoardSlot.SyncMistHiderDisplay();
    }

    public static void TriggerDeathEffect(CardInstance ci, bool isActive)
    {
        if (ci == null) return;
        string id = ci.templateID;
        if (isActive)
        {
            switch (id)
            {
                case "01106": NetworkPlayer.Local.AddEnergy(3); break;
                case "01107":
                    NetworkPlayer.Local.AddEnergy(2);
                    {
                        bool hasAlly = false;
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        for (int i = 6; i <= 11; i++)
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
                case "01106": NetworkPlayer.Local.AddEnergy(1); break;
                case "03513":
                    BoardManager bm = FindObjectOfType<BoardManager>();
                    if (bm != null)
                        for (int j = 0; j <= 5; j++)
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
                   
                    break;
            }
        }
    }

    void ApplySufferingGiverEffect(CardInstance giver, CardInstance target, string chosenTrait)
    {
        if (chosenTrait == null || giver == null || target == null) return;
        giver.giveableDeathTraits.Remove(chosenTrait);
        giver.RemoveGrantedTrait(chosenTrait);
        target.GrantTrait(chosenTrait);
        RefreshCardDisplay(target);
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
        if (bm != null)
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D != null)
                {
                    Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance == ci) { c3d.UpdateValues(); return; }
                }
            }
    }

    void CleanupAfterSelection() { }

    IEnumerator ReformerEnterEffect(CardInstance giver)
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

                // 手动触发全局退场检测
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

            // 清理旧卡（不经过HandleDeath，避免SetCard清掉新卡）
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
    void OnDisasterWalkerDamage(int amount)
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
    IEnumerator HeartthrobEnterEffect(CardInstance giver)
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
            Debug.Log("万人迷：手牌无英雄");
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

        NetworkPlayer.Local.handCards.Remove(selectedCard);

        BoardSlot.isPlacingCard = true;
        BoardSlot.isStrengtheningSlot = true;
        BoardSlot.cardToPlace = selectedCard;

        yield return new WaitWhile(() => BoardSlot.isPlacingCard);

        NetworkPlayer.Local.handCards.Remove(selectedCard);
    }
    IEnumerator MartyrDeathEffectCoroutine(CardInstance giver)
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
    IEnumerator RogueDeathEffect(CardInstance giver)
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
            Debug.Log("无赖退场：手牌无英雄");
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
    IEnumerator GreedySnakeCopyProcess(CardInstance giver, CardInstance target)
    {
        List<(string key, string fullText)> traits = new List<(string, string)>();

        // 先手
        if (target.hasFirstStrike)
        {
            string text = GetTraitFullText(target, "先手");
            traits.Add(("先手", text));
        }
        // 退场
        if (target.hasOnDeath)
        {
            string text = GetTraitFullText(target, "退场");
            traits.Add(("退场", text));
        }
        // 反击
        if (target.hasRevenge)
        {
            string text = GetTraitFullText(target, "反击");
            traits.Add(("反击", text));
        }

        if (traits.Count == 0)
        {
            Debug.Log("贪欲之蛇：目标无可复制特性");
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
  
    IEnumerator RemnantEnterEffect(CardInstance giver)
    {
        List<CardInstance> allyMinions = new List<CardInstance>();
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 6; i <= 11; i++)
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

        // 选择第一个
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

        // 选择第二个（排除已选）
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

        // 弹窗选择哪个回手
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
    IEnumerator PirateEnterEffect(CardInstance giver)
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
            }
        };

        yield return new WaitUntil(() => confirmed);
        SelectionManager.Instance.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        BoardSlot.extraTargetFilter = null;
        ConfirmSelectionButton.Instance.Hide();
        CleanupAfterPlacement();
    }
    IEnumerator PrisonEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        bool hasMyEmpty = false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked) { hasMyEmpty = true; break; }
        }
        if (!hasMyEmpty) { CleanupAfterPlacement(); yield break; }

        BoardSlot myPrison = null;
        bool myDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked && s.slotID >= 6)
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
    IEnumerator EmperorEnterEffect(CardInstance giver)
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
    IEnumerator RiddlerDeathEffect(CardInstance giver)
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        // 收集手牌中的反制牌
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
            Debug.Log("谜语人退场：手牌无反制牌");
            yield break;
        }

        // 手牌选择
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
            // 打出反制牌
            CounterManager.Instance?.PlayCounter(selectedCard, true);
            // 设标记不扣费
            var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
            if (counter != null) counter.noCostOnTrigger = true;
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            Destroy(selectedCard);
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.RefreshLayout(true);
        }
    }
    IEnumerator BlockerEnterEffect(CardInstance giver)
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
        }

        CleanupAfterPlacement();
    }
    IEnumerator InkEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        List<CardInstance> allies = new List<CardInstance>();
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci != giver && !ci.isAttached)
                    allies.Add(ci);
            }
        }

        if (allies.Count == 0) { CleanupAfterPlacement(); yield break; }

        // 逐个退场
        foreach (CardInstance ci in allies)
        {
            ci.isActiveExit = true;
            BoardSlot slot = FindSlotOf(ci);
            if (slot != null)
            {
                slot.HandleDeath(slot.currentCard3D);
                yield return null;
            }
        }

        // 水墨+1+1
        giver.currentHealth += 1;
        giver.currentMaxHealth += 1;
        giver.currentAttack += 1;
        // 数量加成
        int count = allies.Count;
        giver.currentHealth += count - 1;
        giver.currentMaxHealth += count - 1;
        giver.currentAttack += count - 1;

        Card3DInstance giver3D = FindGiver3D(giver);
        giver3D?.UpdateValues();

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

    Card3DInstance FindGiver3D(CardInstance ci)
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
    IEnumerator ApprenticeMageEnterEffect(CardInstance giver)
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
            Debug.Log("初学魔法师：手牌无法术");
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
                    // 反制牌
                    CounterManager.Instance?.PlayCounter(selectedCard, true);
                    var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
                    if (counter != null) counter.noCostOnTrigger = true;
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                }
                else if (spellTemplate.targetType == TargetType.None)
                {
                    // 无目标法术
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                    SpellEffectExecutor.Execute(spellTemplate, null);
                }
                else
                {
                    // 有目标法术
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
    IEnumerator ConductorDoubleDeathEffect(DeathEffectData data)
    {
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        if (data != null)
        {
            // 重新检测沉默和禁止退场（第一次触发后状态可能变了）
            data.isFullySilenced = GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(null); // 实例已销毁，无法检测
                                                                                                                             // 改用保存的标记
            if (!data.isFullySilenced && !data.isDeathBlocked)
            {
                TriggerDeathEffectFromData(data);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
        }
    }

    // 基于数据包触发退场效果
    void TriggerDeathEffectFromData(DeathEffectData data)
    {
        if (data == null) return;

        // 全局退场事件检测
        GlobalDeathEventHandler.Trigger(null, data.slotID, data.damageSourceInstanceIDs, data.isActiveExit);

        if (data.isFullySilenced) return;
        if (data.isDeathBlocked) return;
        string id = data.templateID;
        if (data.isActiveExit)
        {
            switch (id)
            {
                case "01106": NetworkPlayer.Local.AddEnergy(3); break;
                case "01107":
                    NetworkPlayer.Local.AddEnergy(2);
                    {
                        bool hasAlly = false;
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        for (int i = 6; i <= 11; i++)
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
                case "01106": NetworkPlayer.Local.AddEnergy(1); break;
                case "03513":
                    BoardManager bm = FindObjectOfType<BoardManager>();
                    if (bm != null)
                        for (int j = 0; j <= 5; j++)
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
                   
                    break;
            }
        }

        // 苦难给予者双倍退场
        if (id == "01117" && data.giveableDeathTraits != null)
        {
            bool shouldReturn = !data.isActiveExit;
            foreach (string trait in data.giveableDeathTraits)
            {
                switch (trait)
                {
                    case "退场：减一能量":
                        NetworkPlayer.Local.currentEnergy -= 1;
                        NetworkPlayer.Local.UpdateUI();
                        break;
                    case "退场：己方全体受到一伤害":
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        if (bm != null)
                            for (int i = 6; i <= 11; i++)
                            {
                                BoardSlot slot = bm.GetSlot(i);
                                if (slot?.currentCard3D != null)
                                    BattleManager.Instance.ApplyDamageToMinionPublic(slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance, 1, null);
                            }
                        
                        break;
                    case "退场：己方玩家扣一血":
                        NetworkPlayer.Local.TakeDamage(1);
                        break;
                }
            }
            if (shouldReturn && !data.handledReturnToHand)
            {
                CardData template = CardDatabase.Instance?.GetTemplate(data.templateID);
                if (template != null)
                {
                    // 回手逻辑——但数据包不能回手，实例已销毁。保留数据包用于后续扩展
                }
            }
        }
    }
    IEnumerator ConductorEnterEffect(CardInstance giver)
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
    IEnumerator DeepSeaActiveExitEffect()
    {
        BoardSlot.isStrengtheningSlot = true;

        // 选第一个格子
        BoardSlot first = null;
        bool firstDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null) { first = s; firstDone = true; }
        });
        yield return new WaitUntil(() => firstDone);

        // 选第二个格子
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
    IEnumerator FanaticShamanEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        List<BoardSlot> allies = new List<BoardSlot>();
        for (int i = 6; i <= 11; i++)
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
                GameObject temp = new GameObject("TempShadow");
                CardInstance ti = temp.AddComponent<CardInstance>();
                ti.InitFromTemplate(shadowTemplate, k);
                ti.isShadow = true;
                ti.currentAttack += CardInstance.shadowAtkBonus;
                ti.currentTier += CardInstance.shadowTierBonus;
                HandManager hm = FindObjectOfType<HandManager>();
                hm.PlaceCardToSlot(selectedSlot, temp);
                Destroy(temp);
                placed = true;
                SelectionManager.Instance.ForceEndAll();
                BoardSlot.isPlacingCard = false;
                BoardSlot.isStrengtheningSlot = false;
            };
            yield return new WaitUntil(() => placed);
        }
    }

    IEnumerator ShadowMasterEnterEffect(CardInstance giver)
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
    IEnumerator LordEnterEffect(CardInstance giver)
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
                ti.InitFromTemplate(ghostTemplate, k);
                HandManager hm = FindObjectOfType<HandManager>();
                hm.PlaceCardToSlot(selectedSlot, temp);
                Destroy(temp);
                placed = true;
                SelectionManager.Instance.ForceEndAll();
                BoardSlot.isPlacingCard = false;
                BoardSlot.isStrengtheningSlot = false;
            };
            yield return new WaitUntil(() => placed);
        }

        CleanupAfterPlacement();
    }
    IEnumerator AmplifierEnterEffect(CardInstance giver)
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
                    GameObject temp = new GameObject("TempSoldier");
                    CardInstance ti = temp.AddComponent<CardInstance>();
                    ti.InitFromTemplate(soldierTemplate, k);
                    HandManager hm = FindObjectOfType<HandManager>();
                    hm.PlaceCardToSlot(selectedSlot, temp);
                    Destroy(temp);
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
        if (ci != null && !ci.prefixes.Contains("机械"))
        {
            if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                ci.prefixes = "机械";
            else ci.prefixes += " 机械";
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();

            // 复生造物：获得机械前缀+1+0
            CardInstance reborn = FindRebornOnField();
            if (reborn != null && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(reborn)))
            {
                Debug.Log($"复生造物增幅前: health={reborn.currentHealth}, maxHealth={reborn.currentMaxHealth}");
                reborn.currentHealth += 1;
                reborn.currentMaxHealth += 1;
                Debug.Log($"复生造物增幅后: health={reborn.currentHealth}, maxHealth={reborn.currentMaxHealth}");
                UpdateRebornDisplay(reborn);
            }
        }
    }
    IEnumerator WolfKingEnterEffect(CardInstance giver)
    {
        CardData wolfTemplate = CardDatabase.Instance?.GetTemplate("03006");
        if (wolfTemplate?.prefab3D == null) { CleanupAfterPlacement(); yield break; }

        BoardManager bm = FindObjectOfType<BoardManager>();
        int mySlot = -1;
        for (int i = 6; i <= 11; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == giver)
            { mySlot = i; break; }
        }

        for (int i = 6; i <= 11; i++)
        {
            if (i == mySlot) continue;
            BoardSlot slot = bm?.GetSlot(i);
            if (slot == null || slot.isBlocked) continue;

            int stackAtk = 0, stackHp = 0, stackMaxHp = 0;

            if (slot.currentCard3D != null)
            {
                CardInstance oldCI = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (oldCI != null && oldCI.currentTier < 3 && oldCI != giver)
                {
                    stackAtk = oldCI.currentAttack;
                    stackHp = oldCI.currentHealth;
                    stackMaxHp = oldCI.currentMaxHealth;
                    oldCI.isActiveExit = true;
                    slot.HandleDeath(slot.currentCard3D);
                    yield return null;
                    yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                }
                else
                {
                    continue; // 阶位>=3跳过
                }
            }

            // 放置狼
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
    IEnumerator TerroristEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        List<GameObject> diedThisRound = new List<GameObject>();

        // 记录进场前对方场上所有召唤物
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
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);

        // 检测是否有新退场的（不在进场前列表中的）
        bool anyDied = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D == null && beforeEnter.Count > 0)
            {
                // 有槽位变空了，说明退场了
                anyDied = true;
                break;
            }
        }
        // 更准确：比较前后instanceID
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

        // 如果使任何召唤物退场，再触发一次
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
    }
    IEnumerator AncientFairyReattach(GameObject fairy, int oldHostSlotID)
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
    IEnumerator MistHiderEnterEffect(CardInstance giver)
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
                Debug.Log($"换位后 c1 active={c1?.activeSelf}, c2 active={c2?.activeSelf}");
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
    IEnumerator BrilliantMageEnterEffect(CardInstance giver)
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
            Debug.Log("辉煌法师：手牌无法术");
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
            Debug.Log($"辉煌法师：法术费用和={totalCost}，超过8");
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
                    Debug.Log($"辉煌法师：法术{td.cardName}无合法目标，跳过");
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
    IEnumerator ThiefActiveExitEffect()
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
            Debug.Log("窃贼主动退场：对方无手牌");
            yield break;
        }

        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardInstance selected = null;

        CardDisplayPanel.Instance.ShowWithCallback(enemyCards, ci => true, () =>
        {
            selected = CardDisplayPanel.Instance.GetSelectedCard();
            confirmed = true;
        }, "获得");

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
    IEnumerator HonorAttendantActiveExit()
    {
        NetworkPlayer.Local.AddEnergy(2);

        // 收集对方手牌
        List<CardInstance> enemyCards = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null) enemyCards.Add(ci);
        }

        if (enemyCards.Count == 0)
        {
            Debug.Log("荣誉侍者：对方无手牌");
            yield break;
        }

        // 大弹窗展示，不允许选择，确认按钮常显示
        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(enemyCards, ci => true, () =>
        {
            confirmed = true;
        }, "确认");

        // 强制显示确认按钮
        ConfirmSelectionButton.Instance?.gameObject.SetActive(true);
        ConfirmSelectionButton.Instance?.Show(() =>
        {
            confirmed = true;
        });

        yield return new WaitUntil(() => confirmed);

        // 弃掉所有邪恶法术
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
    IEnumerator FearlessEnterEffect()
    {
        List<CounterCard> enemyCounters = CounterManager.Instance?.enemyCounters;
        if (enemyCounters == null || enemyCounters.Count == 0)
        {
            Debug.Log("无畏者：对方无反制牌");
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
    IEnumerator MindScholarEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        // 1. 触发已复制的所有特性
        foreach (string trait in giver.mindScholarCopiedTraits)
        {
            if (trait.Contains("进场") && !giver.mindScholarEnterTriggeredThisPhase)
            {
                // 触发复制的进场效果——从原始卡牌获取并执行
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

        // 触发已复制的抛置
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

        // 2. 如果已满4个，不再复制
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
                if (HasAllyTarget())
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
            case "01344": // 诅咒女巫：使对方一召唤物攻击力永久-2
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
            case "01135": // 杂耍大师：交换两个己方召唤物
                if (HasAllyTarget())
                {
                    HandManager hm = FindObjectOfType<HandManager>();
                    hm.StartCoroutine(hm.SwapTwoAllies());
                }
                break;
           
        }
    }
    bool HasAllyTarget()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 6; i <= 11; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) return true;
        return false;
    }

    /// <summary>
    /// 抛置专用选择方法，自动忽略抛置点击的槽位
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