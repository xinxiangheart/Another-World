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
    /// <summary>最近一次异步法术的协程（StartedCoroutine 的唯一等待者见 EffectContext.SupervisorCoroutine）。
    /// 消费方必须用 TakeSpellPending() 原子取走：旧的"读 → yield → 置空"在 yield 挂起期间字段仍非空，
    /// 另一处同帧读到会等同一协程 → Unity "Another coroutine is already waiting" 双等待卡死。</summary>
    static Coroutine _spellPending;

    /// <summary>登记一次异步法术协程（ResolveSpellEffect 在 Dispatch 后调用；null=本次为同步法术，清掉旧值）。</summary>
    public static void SetSpellPending(Coroutine co) => _spellPending = co;

    /// <summary>原子取走待等协程：取走即清空，第二处同帧调用返回 null（不会双等待）。</summary>
    public static Coroutine TakeSpellPending()
    {
        Coroutine co = _spellPending;
        _spellPending = null;
        return co;
    }
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
        // 抽牌动画期间禁止拖拽
        if (handManager != null && handManager.IsDrawAnimating) return;

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
        handManager?.MarkBoundsDirty();
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

        // 反制免疫（无畏者01319 等）：特性组优先（receiveBlocks=Countered，被禁/沉默则恢复可被反制）；
        // 无特性组时回退旧 ignoreAllCounters bool。
        bool counterImmune = inst != null &&
            (inst.traits != null ? !inst.traits.CanReceive(EffectCategory.Countered) : inst.ignoreAllCounters);
        if (counterImmune)
        {
            FakeEnemyPlayButton.nextPlayAsEnemy = false;
        }
        else
        {
            FakeEnemyPlayButton.OnCardPlayed(template);
        }

        // 守望者(01339)：反制牌「打出后就造成伤害」→ 即时触发。紧随其后的反制分支会 return，
        // 这里就是成交点（其后没有能让本牌回手的校验），所以放在这里不会误触发。
        bool isCounterCard = template.cardType == CardType.Spell && (template.spellType & SpellType.Counter) != 0;
        if (isCounterCard && (isEnemyPlay || SimpleAI.IsAIMatch))
        {
            // 离线代打（本端替对手打出）→ 守望者在本端 6-11；离线 AI 对局（本端打出）→ AI 侧守望者 0-5
            HandManager hmCounterWatcher = FindObjectOfType<HandManager>();
            if (hmCounterWatcher != null)
                hmCounterWatcher.StartCoroutine(hmCounterWatcher.WatcherCounterCheckFor(!isEnemyPlay));
        }

        // 非反制牌的守望者判定不再在这里触发：此处早于下面的能量 / 法术条件 / 合法目标校验，
        // 打不出去、回手的牌也会让守望者生效。改为「真正成交时」触发 ——
        // 联机由 HandManager（落板）/ ResolveSpellEffect（施法）通知对端；离线在这里只登记归属侧，
        // 成交时由 HandManager.NotifyOpponentCardPlayed 的离线分支按它触发（同次打出的嵌套代打共用）。

        if (template.cardType == CardType.Spell && (template.spellType & SpellType.Counter) != 0)
        {
            Debug.Log("进入反制牌分支");
            CounterManager.Instance?.PlayCounter(this.gameObject, true);

            // Network sync: tell the other side about this counter（AI 无连接跳过）
            if (NetworkServer.active && NetworkPlayer.Remote != null
                && NetworkPlayer.Remote.connectionToClient != null)
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
        // 守望者(01339)离线：登记本次打出的归属侧（成交时由 NotifyOpponentCardPlayed 按它触发，
        // 同一次打出衍生的嵌套代打共用；打出失败由下面两道校验撤销）。
        // 只登记、不触发 —— 其后还有法术条件 / 合法目标两道校验，失败回手的牌不该让守望者生效。
        if (!NetworkServer.active && !NetworkClient.isConnected)
            HandManager.SetOfflinePlaySide(isEnemyPlay);
        if (template.cardType == CardType.Spell)
    {
        if (!CheckSpellCondition(template))
        {
            Debug.Log("不满足法术释放条件！");
            player.AddEnergy(inst.currentCost);
            HandManager.ClearOfflinePlaySide(); // 本牌打不出去 → 撤销守望者登记
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
            HandManager.ClearOfflinePlaySide(); // 本牌打不出去 → 撤销守望者登记
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
                    // 血拼 02110：只能选己方生命值>=4的召唤物。预检必须带上这条过滤，
                    // 否则"己方有召唤物但都<4血"时会通过预检 → 空放（能量已花、牌已消耗）。
                    System.Func<BoardSlot, bool> bloodbathFilter = (slot) =>
                    {
                        if (slot?.currentCard3D == null) return false;
                        CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        return ci != null && ci.currentHealth >= 4;
                    };
                    BoardSlot.extraTargetFilter = bloodbathFilter;

                    if (!HasValidTarget((TargetType)template.targetType, bloodbathFilter))
                    {
                        Debug.Log("没有合法目标（己方无生命值>=4的召唤物），血拼无法打出");
                        player.AddEnergy(inst.currentCost);
                        HandManager.ClearOfflinePlaySide(); // 本牌打不出去 → 撤销守望者登记
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
                // 征服者免疫关卡1：对方法术选目标排除敌方免疫卡(01508)。人类 UI 敌方半场=0-5；叠加既有 per-spell 过滤。
                var immuneBaseFilter = BoardSlot.extraTargetFilter;
                BoardSlot.extraTargetFilter = (slot) =>
                {
                    if (immuneBaseFilter != null && !immuneBaseFilter(slot)) return false;
                    CardInstance cc = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                    if (cc != null && cc.ImmuneToEnemySpells && slot.slotID < 6) return false;
                    return true;
                };
                // 通用兜底预检：本次法术的全部过滤（含上面叠加的免疫过滤）都要参与，
                // 过滤后无合法目标 → 退费回手，避免"能打出却无处可选"的空放 / 选择卡死。
                if (!HasValidTarget((TargetType)template.targetType, BoardSlot.extraTargetFilter))
                {
                    Debug.Log("没有合法目标（过滤后），法术无法打出");
                    player.AddEnergy(inst.currentCost);
                    HandManager.ClearOfflinePlaySide(); // 本牌打不出去 → 撤销守望者登记
                    BoardSlot.extraTargetFilter = null;
                    SetButtonsInteractable(true);
                    transform.SetParent(originalParent);
                    rectTransform.anchoredPosition = Vector2.zero;
                    transform.localScale = originalScale;
                    handManager.SetHandAreaRaycast(true);
                    handManager.RefreshLayout(true);
                    return;
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
                }, SelectionKindRules.Classify(template));
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
        // 本次法术的待等协程先清空：纯客户端非 UI 法术走 CmdResolveSpell（不设值），
        // 不清会把上一张法术的协程留在槽里，被消费方误当本次法术等待
        SetSpellPending(null);

        // [打出展示] 法术打出（本地/离线/01329等迭代召唤复用法术）→ 正面（法术无场上模型、无隐藏机制）
        if (template != null)
            PlayRevealManager.Show(template, false);

        // 守望者(01339)：本端打出一张法术牌 → 对侧守望者判定（法术一律按「打出」计，含 01521 辉煌法师代打）
        HandManager.NotifyOpponentCardPlayed(true);

        Debug.Log($"ResolveSpellEffect 进入：effect=\"{template.effect}\"");

        // 纯客户端：委托服务器权威执行法术效果。
        // 例外：需要客户端本地 UI 选择面板的法术（它们的协程调用了 SelectionManager/BeginOpenSelection 等），
        // 必须在客户端本地 Dispatch，否则面板会错误地出现在 Host 端。
        // 判断依据：handler 内部是否调用了 StartCoroutine 启动含 UI 的协程。
        bool needsLocalUI = template.templateID switch
        {
            "02004" => true,  // 皇帝认可 — EmperorsApprovalEffectCoroutine
            "02005" => true,  // 爬! — 使己方一召唤物退场+摸1牌（需目标选择+退场+抽牌分离执行）
            "02010" => true,  // 背叛 — BetrayalEffect
            "02106" => true,  // 改编列队 — ReformFormationEffect
            "02111" => true,  // 手牌净化 — HandCleanseEffect
            "02203" => true,  // 伟大进化 — GreatEvolutionEffect
            "02212" => true,  // 核心召唤 — SummonCoreEffect
            "02307" => true,  // 多卡效应 — ManyCardsEffect
            "02310" => true,  // 聚光灯 — SpotlightEffect
            "02311" => true,  // 冲锋号角 — ChargeHornEffect
            "02403" => true,  // 小型邪恶召唤 — SummonSmallEvilEffect
            "02408" => true,  // 瘟疫 — PlagueEffect
            "02501" => true,  // 传送门 — DoorEffect（异步协程需要本地上下文）
            _ => false,
        };
        if (NetworkClient.isConnected && !NetworkServer.active && !needsLocalUI)
        {
            int slotID = targetSlot?.slotID ?? -1;
            NetworkPlayer.Local?.CmdResolveSpell(template.templateID, slotID);
            // 服务器侧 CmdResolveSpell 会执行 Dispatch→CheckDeaths→MarkDirty，
            // 客户端通过 SyncNow 获得最终板面。坟场记录保留在本地。
        }
        else
        {
            var spellCtx = EffectContext.ForSpell(template, targetSlot);
            spellCtx.spellCasterIsHost = NetworkServer.active; // 本地/离线=主机侧(6-11)；远程客户端本地放→false（法伤侧判定用）
            EffectDispatcher.Dispatch(Trigger.Spell, spellCtx);
            // 等 Dispatch 的监督协程（StartedCoroutine 的唯一等待者）——直接等后者会撞
            // "Another coroutine is already waiting" → 法术 pipeline 永久挂起
            SetSpellPending(spellCtx.SupervisorCoroutine ?? spellCtx.StartedCoroutine);

            // ── 通用法术收尾（仅 Host/离线/客户端 UI 法术）────────────
            // 智者惩罚 + 法术进坟场抽到 CardDrag.ApplySagePunishment / RecordSpellToGraveyard：
            // AI 路径（SimpleAI.PlaySpell）与纯客户端 CmdResolveSpell 也调它们 ——
            // 原先只有这条路径有这段，那两条路径整段缺失（AI 打邪恶法术时对方的智者不惩罚）。
            ApplySagePunishment(template, NetworkPlayer.Local);
        }

        RecordSpellToGraveyard(GetComponent<CardInstance>());

        // 法术已造成死亡 → 启动嵌套树结算。GameObject 可能已被销毁，挂到 BattleManager
        BattleManager.Instance?.StartCoroutine(WaitForSpellTreeCoroutine());
    }

    /// <summary>智者(03503)惩罚：场上「对方」的智者在对方打出邪恶法术时扣施法者 1HP。
    /// 玩家侧 ResolveSpellEffect、纯客户端 CmdResolveSpell、AI SimpleAI.PlaySpell 都要走 ——
    /// 原先只有 ResolveSpellEffect 有这段，AI / 纯客户端施法整段缺失。
    /// caster：本次施法者（谁打出这张法术）；缺省回退 NetworkPlayer.Local（RunAsLocal 期间就是施法者）。</summary>
    public static void ApplySagePunishment(CardData template, NetworkPlayer caster)
    {
        if (template == null || (template.spellType & SpellType.Evil) == 0) return;
        NetworkPlayer punished = caster != null ? caster : NetworkPlayer.Local;
        if (punished == null) return;
        BoardManager bm = Object.FindObjectOfType<BoardManager>();
        BoardSlot[] slots = bm?.GetAllSlots();
        if (slots == null) return;
        foreach (BoardSlot slot in slots)
        {
            CardInstance cardInst = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (cardInst == null || cardInst.templateID != "03503") continue;
            // 只算「对方」的智者：自己场上的智者不惩罚自己（旧写法不分归属 → 自己打邪恶法术自己掉血）
            NetworkPlayer sageOwner = BoardManager.GetOwnerPlayer(slot.slotID);
            if (sageOwner == punished) continue;
            punished.TakeDamage(1, cardInst.templateID, cardInst.instanceID); // 智者(03503)惩罚
            Debug.Log("智者效果：对方打出邪恶法术，扣1血");
        }
    }

    /// <summary>法术进坟场（统一入口）：玩家侧 ResolveSpellEffect / AI SimpleAI.PlaySpell 共用。</summary>
    public static void RecordSpellToGraveyard(CardInstance spellInst)
    {
        if (spellInst == null) return;
        GraveEntry spellData = new GraveEntry();
        spellData.templateID = spellInst.templateID;
        spellData.instanceID = spellInst.instanceID;
        GraveyardManager.Instance?.AddToGraveyard(spellData);
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
            case TargetType.SingleAny: return new int[] { clickedSlot };
            case TargetType.EnemyFrontRow: return new int[] { 0, 1, 2 };
            case TargetType.EnemyBackRow: return new int[] { 3, 4, 5 };
            case TargetType.AllyFrontRow: return new int[] { 6, 7, 8 };
            case TargetType.AllyBackRow: return new int[] { 9, 10, 11 };
            case TargetType.AllEnemies: return new int[] { 0, 1, 2, 3, 4, 5 };
            case TargetType.AllAllies: return new int[] { 6, 7, 8, 9, 10, 11 };
            default: return new int[0];
        }
    }

    /// <summary>预检：槽位能否作为"法术目标"。己方(6-11)放行；敌方(0-5)的免疫卡(征服者01508)不可选——
    /// 排除后仍无目标 → HasValidTarget 返 false → 空发退费（征服者免疫残余UX修复）。
    /// extra：本次法术自带的额外过滤（如血拼 02110 的"己方生命值>=4"）。预检必须与选择期
    /// （BoardSlot.CanBeSelected 走 extraTargetFilter）同口径，否则会出现"过滤后无合法目标却
    /// 仍允许打出"——02110 空放，02206/02207 全场只剩附着物时无处可选。</summary>
    bool HasValidSpellTarget(BoardSlot slot, System.Func<BoardSlot, bool> extra = null)
    {
        if (slot == null || slot.isBlocked || !slot.hasCard) return false;
        if (extra != null && !extra(slot)) return false;
        if (slot.slotID < 6)
        {
            CardInstance ci = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.ImmuneToEnemySpells) return false;
        }
        return true;
    }

    bool HasValidTarget(TargetType type, System.Func<BoardSlot, bool> extra = null)
    {
        Debug.Log($"HasValidTarget 被调用：type={type}");
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;

        switch (type)
        {
            case TargetType.SingleEnemy:
                for (int id = 0; id <= 5; id++)
                {
                    if (HasValidSpellTarget(bm.GetSlot(id), extra))
                        return true;
                }
                return false;

            case TargetType.SingleAlly:
                for (int id = 6; id <= 11; id++)
                {
                    BoardSlot slot = bm.GetSlot(id);
                    Debug.Log($"检查槽位{id}：slot={slot != null}, hasCard={slot?.hasCard}, isBlocked={slot?.isBlocked}");
                    if (HasValidSpellTarget(slot, extra))
                        return true;
                }
                return false;
            case TargetType.EnemyAnyRow:
                for (int id = 0; id <= 5; id++)
                {
                    if (HasValidSpellTarget(bm.GetSlot(id), extra)) return true;
                }
                return false;
            case TargetType.AllyAnyRow:
                for (int id = 6; id <= 11; id++)
                {
                    if (HasValidSpellTarget(bm.GetSlot(id), extra)) return true;
                }
                return false;
            case TargetType.AllMinions:
                for (int id = 0; id <= 11; id++)
                {
                    if (HasValidSpellTarget(bm.GetSlot(id), extra))
                        return true;
                }
                return false;
            case TargetType.SingleAny:
                // 任意目标：敌方(0-5)或己方(6-11)任一召唤物可施放（敌方免疫卡排除）
                for (int id = 0; id <= 11; id++)
                {
                    if (HasValidSpellTarget(bm.GetSlot(id), extra))
                        return true;
                }
                return false;

            default:
                int[] ids = GetTargetSlots(type, -1);
                foreach (int id in ids)
                {
                    if (HasValidSpellTarget(bm.GetSlot(id), extra))
                        return true;
                }
                return false;
        }
    }
    /// <summary>
    /// 兼容旧调用点（拖拽/选择/放置/弹窗各处的"关门/开门"）。
    /// 现在不再直接写 CanvasGroup —— 按钮开关是派生状态（见 TurnButtonGate）：
    ///   阶段权威由 TurnManager 写；UI 交互锁(拖拽/选择/放置/弹窗/战斗动画)每帧重算。
    /// 因此本例的 enabled 只作"请求重算"，真正生效的是调用点对应的状态本身。
    /// 这修掉了"某条协程没走到收尾就把按钮留在禁用态"的偶发问题（表现为自己回合点不动结束回合/抽牌）。
    /// </summary>
    public void SetButtonsInteractable(bool enabled)
    {
        TurnButtonGate.Refresh();
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
    /// <param name="casterIsHost">施法方是否为本地(主机)侧；false 且 AI 对局 → 施法方是 AI(Remote,0-5)。</param>
    public IEnumerator EmperorsApprovalEffectCoroutine(bool casterIsHost = true)
    {
        // [AI] 02004：AI 施法 → 只作用于 AI 自己（先场上 0-5、后 AI 手牌），不弹玩家面板。
        // 旧写法借 BuildHandPlusFieldCardList（本机手牌 + 6-11 场）取候选，而 ShowWithCallback 在
        // IsAIEvaluating 下自动确认第一张 → AI 的牌会给「玩家」的一张牌加渊前缀，再让玩家白摸 1 张。
        if (SimpleAI.IsAIMatch && !casterIsHost)
        {
            CardInstance pick2004 = HandManager.PickAIOwnSummon(
                ci => ci.prefixes == null || !ci.prefixes.Contains("渊"));
            if (pick2004 != null)
            {
                pick2004.GivePrefix("渊");
                HandManager.CommitAIOwnCardPrefix(pick2004, "渊");
                Debug.Log($"[AI] 02004 皇帝的认可：{pick2004.instanceID} 附加「渊」前缀");
            }
            else
            {
                Debug.LogWarning("[AI] 02004 皇帝的认可：AI 己方无可加前缀的召唤物");
            }
            NetworkPlayer.Remote?.DrawCard();
            CardDrag.CleanupSpellResources();
            yield break;
        }

        yield return null;
        HandManager hm = FindObjectOfType<HandManager>();
        // 手牌+场上混合弹窗（收藏家 01349 模式）：候选 = 己方召唤物(手牌6? 手牌一律 + 场上6-11)；排除已带渊
        List<CardInstance> candidates = hm != null
            ? hm.BuildHandPlusFieldCardList(
                ci => CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Summon
                    && (ci.prefixes == null || !ci.prefixes.Contains("渊")))
            : new List<CardInstance>();
        if (candidates.Count == 0) { CardDrag.CleanupSpellResources(); yield break; }

        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(candidates, ci => true, () => confirmed = true, "认可");
        float deadline = Time.time + 30f;
        while (!confirmed && Time.time < deadline) yield return null;
        if (!confirmed) { hm?.EndHandSelectionCleanup(); CardDrag.CleanupSpellResources(); yield break; }

        CardInstance chosen = CardDisplayPanel.Instance.GetSelectedCard();
        string iid = chosen != null ? chosen.instanceID : null;
        hm?.EndHandSelectionCleanup();

        if (!string.IsNullOrEmpty(iid)) ApplyEmperorApproval(iid);
        CardDrag.CleanupSpellResources();
    }

    /// <summary>皇帝认可：按 iid 回扫真身（场上 3D / 手牌 2D），给渊前缀 + 摸1；只同步对应的端。</summary>
    void ApplyEmperorApproval(string iid)
    {
        HandManager hm = FindObjectOfType<HandManager>();
        Card3DInstance c3d = hm?.ResolveFieldCardByInstanceID(iid);
        CardInstance targetCI = c3d?.cardInstance;
        GameObject handCard = null;
        if (targetCI == null) { handCard = hm?.ResolveHandCardByInstanceID(iid); targetCI = handCard?.GetComponent<CardInstance>(); }
        if (targetCI == null) return;

        if (!targetCI.prefixes.Contains("渊")) targetCI.GivePrefix("渊");

        if (c3d != null)
        {
            c3d.UpdateValues();
            TurnManager.SyncMyBoardToOpponent(); // 场上走板面同步
        }
        else if (handCard != null)
        {
            handCard.GetComponent<CardDisplay2D>()?.Refresh();
            if (NetworkClient.isConnected) NetworkPlayer.Local?.CmdSetHandCardPrefix(targetCI.instanceID, "渊");
        }
        NetworkPlayer.Local?.DrawCard();
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
            case TargetType.SingleAny:
                // 任意目标：敌方(0-5)或己方(6-11)任一召唤物可施放
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
