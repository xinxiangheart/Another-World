using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mirror;
using static CardData;


public class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] 
    private Vector3 originalLocalPos;
    private Vector3 originalScale;
    private Transform originalParent;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private HandManager handManager;
    public static Coroutine SpellPending;
    private bool isOutsideHand = false;
    private Canvas tempCanvas;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        handManager = FindObjectOfType<HandManager>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalLocalPos = rectTransform.localPosition;
        originalScale = transform.localScale;
        originalParent = transform.parent;

        tempCanvas = gameObject.AddComponent<Canvas>();
        tempCanvas.overrideSorting = true;
        tempCanvas.sortingOrder = 100;

        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        isOutsideHand = false;

        CardView.IsAnyCardDragging = true;
        SetButtonsInteractable(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvas.transform as RectTransform, eventData.position,
            eventData.pressEventCamera, out Vector3 worldPos))
        {
            rectTransform.position = worldPos;
        }

        RectTransform handRect = handManager.transform as RectTransform;
        bool outside = !RectTransformUtility.RectangleContainsScreenPoint(
            handRect, eventData.position, eventData.pressEventCamera);

        if (outside && !isOutsideHand)
        {
            isOutsideHand = true;
            handManager.HideOtherCards(gameObject);
        }
        else if (!outside && isOutsideHand)
        {
            isOutsideHand = false;
            handManager.ShowAllCards();
        }

        if (!outside)
            handManager.OnDragUpdate(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
{
    if (tempCanvas != null)
    {
        Destroy(tempCanvas);
        tempCanvas = null;
    }

    canvasGroup.blocksRaycasts = true;
    handManager.ShowAllCards();
    CardView.IsAnyCardDragging = false;

    if (!handManager.IsPlayArea(eventData.position))
    {
        SetButtonsInteractable(true);
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = Vector2.zero;
        transform.localScale = originalScale;
        handManager.SetHandAreaRaycast(true);
        handManager.RefreshLayout(true);
        return;
    }

    // 联机：非己方回合禁止出牌，回弹手牌
    if (NetworkClient.isConnected)
    {
        TurnManager tmGuard = FindObjectOfType<TurnManager>();
        if (tmGuard != null && !tmGuard.IsMyTurn())
        {
            Debug.Log("非己方回合，无法出牌");
            SetButtonsInteractable(true);
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.localScale = originalScale;
            handManager.SetHandAreaRaycast(true);
            handManager.RefreshLayout(true);
            return;
        }
    }

    CardInstance inst = GetComponent<CardInstance>();
    CardData template = CardDatabase.Instance?.GetTemplate(inst?.templateID);
    NetworkPlayer player = NetworkPlayer.Local;
        if (template == null)
        {
            SetButtonsInteractable(true);
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.localScale = originalScale;
            handManager.SetHandAreaRaycast(true);
            handManager.RefreshLayout(true);
            return;
        }
		if (template.effect == "1.选择召唤上阶段弃牌堆中的一名召唤物")
		{
			SelectionManager.Instance.RunCoroutine(SelectionManager.Instance.OvertimeEffect());
			CardView cv = GetComponent<CardView>();
			HandManager hm = FindObjectOfType<HandManager>();
			if (cv != null) hm?.RemoveCard(cv);
			handManager.HideAllCards();
			handManager.SetHandAreaRaycast(false);
			SetButtonsInteractable(false);
			gameObject.SetActive(false);
			return;
		}
		
		// 和你拼了：生命值<=3时弹窗选择是否弃牌
		if (template.effect.Contains("当己方玩家生命值<=3时允许弃掉该牌并抽一张牌"))
        {
            if (NetworkPlayer.Local.currentHealth <= 3)
            {
                player.AddEnergy(inst.currentCost);

                ConfirmPanel.Instance.Show("是否弃掉该牌并抽一张牌？",
                    () =>
                    {
                        // 选是：弃牌抽牌
                        CardView cv = GetComponent<CardView>();
                        HandManager hm = FindObjectOfType<HandManager>();
                        if (cv != null) hm?.RemoveCard(cv);
                        NetworkPlayer.Local.DrawCardWithoutLimit();
                        SetButtonsInteractable(true);
                        handManager.SetHandAreaRaycast(true);
                        Debug.Log("和你拼了：弃牌并抽一张牌");
                    },
                    () =>
                    {
                        // 选否：回手牌
                        SetButtonsInteractable(true);
                        transform.SetParent(originalParent);
                        rectTransform.anchoredPosition = Vector2.zero;
                        transform.localScale = originalScale;
                        handManager.SetHandAreaRaycast(true);
                        handManager.RefreshLayout(true);
                        Debug.Log("和你拼了：取消弃牌");
                    }
                );
                return;
            }
        }

        bool isEnemyPlay = FakeEnemyPlayButton.nextPlayAsEnemy;

        if (inst != null && inst.ignoreAllCounters)
        {
            FakeEnemyPlayButton.nextPlayAsEnemy = false;
        }
        else
        {
            FakeEnemyPlayButton.OnCardPlayed(template);
        }

        if (isEnemyPlay)
        {
            HandManager hmWatcher = FindObjectOfType<HandManager>();
            if (hmWatcher != null)
                hmWatcher.StartCoroutine(hmWatcher.WatcherDelayedCheck());
        }

        if (template.cardType == CardType.Spell && (template.spellType & SpellType.Counter) != 0)
        {
            Debug.Log("进入反制牌分支");
            CounterManager.Instance?.PlayCounter(this.gameObject, true);

            // Network sync: tell the other side about this counter
            if (NetworkServer.active && NetworkPlayer.Remote != null)
                NetworkPlayer.Remote.TargetSpawnCounterCard(NetworkPlayer.Remote.connectionToClient, template.templateID);
            else if (NetworkClient.isConnected)
                NetworkPlayer.Local?.CmdPlayCounter(template.templateID);

            CardView cv = GetComponent<CardView>();
            if (cv != null) handManager.RemoveCard(cv);
            else Destroy(gameObject);
            SetButtonsInteractable(true);
            handManager.SetHandAreaRaycast(true);
            CardView.IsAnyCardDragging = false;
            return;
        }

        // 生命值为0的附着牌，场上无己方召唤物时无法打出
        if (inst != null && inst.canAttach && inst.baseHealth == 0)
        {
            bool hasAllyTarget = false;
            BoardManager bmCheck = FindObjectOfType<BoardManager>();
            if (bmCheck != null)
            {
                for (int i = 6; i <= 11; i++)
                {
                    if (bmCheck.GetSlot(i)?.currentCard3D != null) { hasAllyTarget = true; break; }
                }
            }
            if (!hasAllyTarget)
            {
                Debug.Log("场上没有己方召唤物，无法打出");
                SetButtonsInteractable(true);
                transform.SetParent(originalParent);
                rectTransform.anchoredPosition = Vector2.zero;
                transform.localScale = originalScale;
                handManager.SetHandAreaRaycast(true);
                handManager.RefreshLayout(true);
                return;
            }
        }

        int actualCost = inst.currentCost;
        if (inst.merchantDiscounted && NetworkPlayer.Local.IsMerchantOnFieldPublic())
        {
            actualCost = Mathf.Max(0, actualCost - 1);
            inst.merchantDiscounted = false;
        }
        if (inst.energyReaperDiscounted && NetworkPlayer.Local.IsEnergyReaperOnFieldPublic())
        {
            actualCost = Mathf.Max(0, actualCost - 1);
            inst.energyReaperDiscounted = false;
        }
        if (player == null || !player.UseEnergy(actualCost))
        {
            Debug.Log("能量不足！");
            SetButtonsInteractable(true);
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.localScale = originalScale;
            handManager.SetHandAreaRaycast(true);
            handManager.RefreshLayout(true);
            return;
        }
        inst.currentCost = actualCost;
        // 卡牌无效拦截
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.NextCardNullified)
        {
            GlobalEventManager.Instance.NextCardNullified = false;
            CardInstance nullInst = GetComponent<CardInstance>();
            if (nullInst != null) nullInst.ClearAllTraits();
            player.AddEnergy(inst.currentCost); // 退还费用
            SetButtonsInteractable(true);
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.localScale = originalScale;
            handManager.SetHandAreaRaycast(true);
            handManager.RefreshLayout(true);
            return;
        }
        if (template.cardType == CardType.Spell)
    {
        if (!CheckSpellCondition(template))
        {
            Debug.Log("不满足法术释放条件！");
            player.AddEnergy(inst.currentCost);
            SetButtonsInteractable(true);
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.localScale = originalScale;
            handManager.SetHandAreaRaycast(true);
            handManager.RefreshLayout(true);
            return;
        }

        if ((TargetType)template.targetType == TargetType.None)
        {
            ResolveSpellEffect(template, null);
            handManager.SetHandAreaRaycast(true);
            handManager.ShowAllCards();
            SetButtonsInteractable(true);
            CardView cv = GetComponent<CardView>();
            if (cv != null) handManager.RemoveCard(cv);
            return;
        }

        if (!HasValidTarget((TargetType)template.targetType))
        {
            Debug.Log("没有合法目标，法术无法打出！");
            player.AddEnergy(inst.currentCost);
            SetButtonsInteractable(true);
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.localScale = originalScale;
            handManager.SetHandAreaRaycast(true);
            handManager.RefreshLayout(true);
            return;
        }
            if (!BoardSlot.isTargetingMode)
            {
                BoardSlot.extraTargetFilter = null;
                if (template.effect.Contains("生命值>=4"))
                {
                    BoardSlot.extraTargetFilter = (slot) =>
                    {
                        if (slot?.currentCard3D == null) return false;
                        CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        return ci != null && ci.currentHealth >= 4;
                    };

                    if (!HasValidTarget((TargetType)template.targetType))
                    {
                        Debug.Log("没有合法目标，无法打出");
                        player.AddEnergy(inst.currentCost);
                        SetButtonsInteractable(true);
                        transform.SetParent(originalParent);
                        rectTransform.anchoredPosition = Vector2.zero;
                        transform.localScale = originalScale;
                        handManager.SetHandAreaRaycast(true);
                        handManager.RefreshLayout(true);
                        BoardSlot.extraTargetFilter = null;
                        return;
                    }
                }
                if (template.effect.Contains("场上任意一召唤物"))
                {
                    BoardSlot.extraTargetFilter = (slot) =>
                    {
                        return slot?.currentCard3D != null;
                    };
                }
                if (template.effect.Contains("不能对附着物使用"))
                {
                    BoardSlot.extraTargetFilter = (slot) =>
                    {
                        if (slot?.currentCard3D == null) return false;
                        CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        return ci != null && !ci.isAttached;
                    };
                }
                SelectionManager.Instance.BeginOpenSelection((TargetType)template.targetType, (slot) =>
                {
                    CardView cv = GetComponent<CardView>();
                    HandManager hm = FindObjectOfType<HandManager>();
                    if (cv != null) hm?.RemoveCard(cv);

                    ResolveSpellEffect(template, slot);
                    BoardSlot.extraTargetFilter = null;
                    SetButtonsInteractable(true);
                    if (hm != null) { hm.SetHandAreaRaycast(true); hm.ShowAllCards(); }
                });
                handManager.HideAllCards();
                handManager.SetHandAreaRaycast(false);
                SetButtonsInteractable(false);
                gameObject.SetActive(false);
            }
        }
    else
    {
        CardInstance cardInst = GetComponent<CardInstance>();

        if (inst != null && inst.isXValue && (inst.templateID == "01306" || inst.templateID == "01307" || inst.templateID == "03012"))
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
                Debug.Log("对方场上没有召唤物，阴/阳/阴阳无法打出");
                player.AddEnergy(inst.currentCost);
                SetButtonsInteractable(true);
                transform.SetParent(originalParent);
                rectTransform.anchoredPosition = Vector2.zero;
                transform.localScale = originalScale;
                handManager.SetHandAreaRaycast(true);
                handManager.RefreshLayout(true);
                return;
            }
        }

        if (cardInst != null && cardInst.canAttach)
        {
            if (IsBoardFull())
            {
                BoardSlot.isReplaceMode = true;
            }
            handManager.HideAllCards();
            handManager.SetHandAreaRaycast(false);
            SetButtonsInteractable(false);
            gameObject.SetActive(false);
            handManager.PlaceCardToSlot(null, this.gameObject);
        }
        else
        {
            if (IsBoardFull())
            {
                BoardSlot.isReplaceMode = true;
            }
            BoardSlot.isPlacingCard = true;
            BoardSlot.cardToPlace = this.gameObject;
            handManager.HideAllCards();
            handManager.SetHandAreaRaycast(false);
            SetButtonsInteractable(false);
            gameObject.SetActive(false);
        }
    }

}
  public void ResolveSpellEffect(CardData template, BoardSlot targetSlot)
    {
        Debug.Log($"ResolveSpellEffect 进入：effect=\"{template.effect}\"");

        // ── 法术效果分发 ──
        var spellCtx = EffectContext.ForSpell(template, targetSlot);
        EffectDispatcher.Dispatch(Trigger.Spell, spellCtx);
        SpellPending = spellCtx.StartedCoroutine;

        // ── 通用法术收尾 ──────────────────────────────────────────────
        if (template != null && (template.spellType & SpellType.Evil) != 0)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            BoardSlot[] slots = bm?.GetAllSlots();
            if (slots != null)
            {
                foreach (BoardSlot slot in slots)
                {
                    if (slot?.currentCard3D != null)
                    {
                        CardInstance cardInst = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (cardInst != null && cardInst.templateID == "03503")
                        {
                            NetworkPlayer.Local.TakeDamage(1);
                            Debug.Log("智者效果：对方打出邪恶法术，扣1血");
                        }
                    }
                }
            }
        }

        CardInstance spellInst2 = GetComponent<CardInstance>();
        if (spellInst2 != null)
        {
            GraveEntry spellData = new GraveEntry();
            spellData.templateID = spellInst2.templateID;
            spellData.instanceID = spellInst2.instanceID;
            GraveyardManager.Instance?.AddToGraveyard(spellData);
        }

        // 法术已造成死亡 → 启动嵌套树结算。GameObject 可能已被销毁，挂到 BattleManager
        BattleManager.Instance?.StartCoroutine(WaitForSpellTreeCoroutine());
    }

    static IEnumerator WaitForSpellTreeCoroutine()
    {
        yield return null;
        BoardSlot.CheckAndHandleDeaths();
        yield return ActionQueueManager.WaitForDrain();
        yield return new WaitWhile(() => NestingContext.IsNested);
        if (BoardSlot.pendingRevenges.Count > 0 && BattleManager.Instance != null)
            yield return BattleManager.Instance.StartCoroutine(
                BattleManager.ResolveRevengesFromSnapshot());
    }

    int[] GetTargetSlots(TargetType type, int clickedSlot)
    {
        switch (type)
        {
            case TargetType.SingleEnemy: return new int[] { clickedSlot };
            case TargetType.SingleAlly: return new int[] { clickedSlot };
            case TargetType.EnemyFrontRow: return new int[] { 0, 1, 2 };
            case TargetType.EnemyBackRow: return new int[] { 3, 4, 5 };
            case TargetType.AllyFrontRow: return new int[] { 6, 7, 8 };
            case TargetType.AllyBackRow: return new int[] { 9, 10, 11 };
            case TargetType.AllEnemies: return new int[] { 0, 1, 2, 3, 4, 5 };
            case TargetType.AllAllies: return new int[] { 6, 7, 8, 9, 10, 11 };
            default: return new int[0];
        }
    }

    bool HasValidTarget(TargetType type)
    {
        Debug.Log($"HasValidTarget 被调用：type={type}");
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;

        switch (type)
        {
            case TargetType.SingleEnemy:
                for (int id = 0; id <= 5; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard)
                        return true;
                }
                return false;

            case TargetType.SingleAlly:
                for (int id = 6; id <= 11; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    Debug.Log($"检查槽位{id}：slot={slot != null}, hasCard={slot?.hasCard}, isBlocked={slot?.isBlocked}");
                    if (slot != null && !slot.isBlocked && slot.hasCard)
                        return true;
                }
                return false;
            case TargetType.EnemyAnyRow:
                for (int id = 0; id <= 5; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard) return true;
                }
                return false;
            case TargetType.AllyAnyRow:
                for (int id = 6; id <= 11; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard) return true;
                }
                return false;
            case TargetType.AllMinions:
                for (int id = 0; id <= 11; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard)
                        return true;
                }
                return false;

            default:
                int[] ids = GetTargetSlots(type, -1);
                foreach (int id in ids)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard)
                        return true;
                }
                return false;
        }
    }
    public void SetButtonsInteractable(bool enabled)
    {
        ApplyButtonsInteractable(enabled);
        // 每次禁用时，挂到 HandManager(常驻) 上启动延迟守卫
        if (!enabled && handManager != null)
            handManager.StartCoroutine(WatchEmptyHand());
    }

    IEnumerator WatchEmptyHand()
    {
        yield return new WaitForSeconds(0.3f);
        yield return new WaitForSeconds(0.3f);
        NetworkPlayer.Local?.handCards.RemoveAll(c => c == null);
        if (NetworkPlayer.Local != null && NetworkPlayer.Local.handCards.Count == 0)
            ApplyButtonsInteractable(true);
    }

    void ApplyButtonsInteractable(bool enabled)
    {
        EndTurnButton endBtn = FindObjectOfType<EndTurnButton>();
        if (endBtn != null)
        {
            CanvasGroup cg = endBtn.GetComponent<CanvasGroup>();
            if (cg == null) cg = endBtn.gameObject.AddComponent<CanvasGroup>();
            cg.interactable = enabled;
            cg.blocksRaycasts = enabled;
        }

        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null)
        {
            CanvasGroup cg = drawUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = drawUI.gameObject.AddComponent<CanvasGroup>();
            cg.interactable = enabled;
            cg.blocksRaycasts = enabled;
        }
    }

    bool CheckSpellCondition(CardData template)
    {
        if (template.effect.Contains("使己方一召唤物退场") && template.effect.Contains("摸1张牌"))
            return true;

        switch (template.effect)
        {
            case "1.当能量>=8时允许打出\n2.摸两张牌":
                return NetworkPlayer.Local.GetEnergy() >= 8;
            case "1.扣己方玩家3生命值，+5能量\n2.当己方玩家生命值<=3时允许弃掉该牌并抽一张牌":
                return true;
            default:
                return true;
        }

    }
    private bool IsBoardFull()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot == null) continue;
            // 只要有一个槽位没被封锁且没卡，就说明没满
            if (!slot.isBlocked && !slot.hasCard)
                return false;
        }
        return true;
    }
    public static void CleanupSpellResources()
    {
        BoardSyncManager.MarkDirty();
    }
    public IEnumerator EmperorsApprovalEffectCoroutine()
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);
        // 不隐藏手牌，直接进入选择模式（手牌和场上都可以选）
        SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null)
            {
                CardData template = CardDatabase.Instance?.GetTemplate(ci.templateID);
                if (template?.cardType == CardType.Spell)
                {
                    card.SetActive(false);
                    spellCards.Add(card);
                }
            }
        }

        BoardSlot.currentTargetType = TargetType.SingleAlly;

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
                    SelectionManager.Instance.ForceEndAll();
                    OnEmperorTargetSelected(card, spellCards, handSummons);
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.ForceEndAll();
                OnEmperorTargetSelected(targetSlot.currentCard3D, spellCards, handSummons);
            }
        };
        yield return null;
    }

    void OnEmperorTargetSelected(GameObject target, List<GameObject> hiddenSpells, List<GameObject> handSummons)
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
            if (!targetCI.prefixes.Contains("渊"))
            {
                if (string.IsNullOrEmpty(targetCI.prefixes) || targetCI.prefixes == "无")
                    targetCI.prefixes = "渊";
                else
                    targetCI.prefixes += " 渊";
            }

            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D display2D = target.GetComponent<CardDisplay2D>();
            display2D?.Refresh();

            // 前缀修改同步到对方
            TurnManager.SyncMyBoardToOpponent();

            NetworkPlayer.Local.DrawCard();
        }

        foreach (GameObject card in hiddenSpells)
        {
            if (card != null) card.SetActive(true);
        }

        foreach (GameObject card in handSummons)
        {
            CardClickHandler handler = card.GetComponent<CardClickHandler>();
            if (handler != null) Destroy(handler);
        }

        CardDrag.CleanupSpellResources();
    }
    public static void ExecuteSpellEffect(CardData template, BoardSlot targetSlot)
    {
        CardDrag cd = FindObjectOfType<CardDrag>();
        if (cd != null)
            cd.ResolveSpellEffect(template, targetSlot);
    }
    public static bool HasValidTargetStatic(TargetType type)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;

        switch (type)
        {
            case TargetType.SingleEnemy:
                for (int id = 0; id <= 5; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard) return true;
                }
                return false;
            case TargetType.SingleAlly:
                for (int id = 6; id <= 11; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard) return true;
                }
                return false;
            case TargetType.AllMinions:
                for (int id = 0; id <= 11; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    if (slot != null && !slot.isBlocked && slot.hasCard) return true;
                }
                return false;
            default:
                return true;
        }
    }
   
}