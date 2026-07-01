using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

    CardInstance inst = GetComponent<CardInstance>();
    CardData template = CardDatabase.Instance?.GetTemplate(inst?.templateID);
    Player player = Player.Instance;
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
            if (Player.Instance.currentHealth <= 3)
            {
                player.AddEnergy(inst.currentCost);

                ConfirmPanel.Instance.Show("是否弃掉该牌并抽一张牌？",
                    () =>
                    {
                        // 选是：弃牌抽牌
                        CardView cv = GetComponent<CardView>();
                        HandManager hm = FindObjectOfType<HandManager>();
                        if (cv != null) hm?.RemoveCard(cv);
                        Player.Instance.DrawCardWithoutLimit();
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
        if (inst.merchantDiscounted && Player.Instance.IsMerchantOnFieldPublic())
            actualCost = Mathf.Max(0, actualCost - 1);
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

        if (template.effect.Contains("使己方一召唤物退场") && template.effect.Contains("摸1张牌"))
        {
            Debug.Log($"效果执行：targetSlot={targetSlot?.slotID}");
            if (targetSlot != null && targetSlot.currentCard3D != null)
            {
                Card3DInstance target3D = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                if (target3D?.cardInstance != null)
                {
                    target3D.cardInstance.isActiveExit = false;
                }
                targetSlot.HandleDeath(targetSlot.currentCard3D);
            }
            Player.Instance.DrawCard();
            CardDrag.CleanupSpellResources();
            return;
        }

        switch (template.effect)
        {
            case "1.当能量>=8时允许打出\n2.摸两张牌":
                Player player = Player.Instance;
                if (player != null)
                {
                    player.DrawCard();
                    player.DrawCard();
                }
                CardDrag.CleanupSpellResources();
                break;

            case "1.对对方玩家造成1伤害":
                EnemyPlayer.Instance?.TakeDamage(1);
                CardDrag.CleanupSpellResources();
                break;

            case "1.为己方手牌或场上一召唤物附加渊前缀\n2.摸1张牌":
                SelectionManager.Instance.StartSafeCoroutine(EmperorsApprovalEffectCoroutine());
                break;

            case "1.恢复己方一召唤物3生命值":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Spell);
                    t3d?.UpdateValues();
                }
                CleanupSpellResources();
                break;
            case "1.随机获得一张金/木/水/火/土元素":
                string[] elementIDs = { "03015", "03016", "03017", "03018", "03019" };
                string randomID = elementIDs[Random.Range(0, elementIDs.Length)];
                CardData element = CardDatabase.Instance?.GetTemplate(randomID);
                if (element != null)
                {
                    Player.Instance.AddCardToHand(element);
                    Debug.Log($"飘荡元素：获得 {element.cardName}");
                }
                CleanupSpellResources();
                break;
            case "1.在对方区域召唤一叛徒，若已满员则无法召唤":
                HandManager hmBetray = FindObjectOfType<HandManager>();
                hmBetray.StartCoroutine(hmBetray.BetrayalEffect());
                break;
            case "1.获得一张追随者":
                CardData followerTemplate = CardDatabase.Instance?.GetTemplate("03001");
                if (followerTemplate != null)
                {
                    Player.Instance.AddCardToHand(followerTemplate);
                    Debug.Log("勇者小队：获得追随者加入手牌");
                }
                CardDrag.CleanupSpellResources();
                break;

            case "1.触发己方场上一召唤物的进场":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    CardInstance targetCI = t3d?.cardInstance;
                    CardData targetTemplate = CardDatabase.Instance?.GetTemplate(targetCI?.templateID);
                    Debug.Log($"压榨潜能触发进场: templateID={targetTemplate?.templateID}, hasOnEnter={targetTemplate?.hasOnEnter}, inst={targetCI != null}");

                    if (targetTemplate != null && targetTemplate.hasOnEnter)
                    {
                        Debug.Log($"压榨潜能调用 StartOnEnterEffect");
                        targetSlot.StartOnEnterEffect(targetTemplate, targetCI);
                    }
                    else
                    {
                        Debug.Log($"压榨潜能：目标 {targetTemplate?.cardName} 没有进场效果");
                    }
                }
                CleanupSpellResources();
                break;
            case "1.对对方一召唤物造成3伤害":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance target3D = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (target3D?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(target3D.cardInstance, 3, null);
                        target3D.UpdateValues();
                        Debug.Log($"致命一击对 {target3D.cardInstance.instanceID} 造成3伤害");
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
                CardDrag.CleanupSpellResources();
                break;
            case "1.调整己方召唤物的位置":
                HandManager hm = FindObjectOfType<HandManager>();
                hm.StartCoroutine(hm.ReformFormationEffect(this));
                break;
            case "1.召唤两名机械：杂兵":
                HandManager hmSummon = FindObjectOfType<HandManager>();
                hmSummon.StartCoroutine(hmSummon.SummonTwoMinions());
                break;
            case "1.恢复玩家2生命值":
                Player.Instance.ReceiveHeal(2, CardInstance.HealSourceType.Spell);
                CleanupSpellResources();
                break;
            case "1.恢复玩家4生命值":
                Player.Instance.ReceiveHeal(4, CardInstance.HealSourceType.Spell);
                CleanupSpellResources();
                break;
            case "1.最多保留4张手牌，其余弃掉并摸等量牌":
                HandManager hmHandCleanse = FindObjectOfType<HandManager>();
                hmHandCleanse.StartCoroutine(hmHandCleanse.HandCleanseEffect());
                break;
            case "1.对己方一名生命值>=4的召唤物造成4伤害并+4能量":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null && t3d.cardInstance.currentHealth >= 4)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 4, null);
                        t3d.UpdateValues();
                        Player.Instance.AddEnergy(4);
                        BoardSlot.CheckAndHandleDeaths();
                    }
                }
                BoardSlot.extraTargetFilter = null;
                CleanupSpellResources();
                break;
            case "1.抽7张牌然后弃4张牌，每弃一张基础使用费用为5的牌+1能量":
                HandManager hmMany = FindObjectOfType<HandManager>();
                hmMany.StartCoroutine(hmMany.ManyCardsEffect());
                break;
            case "1.对对方全体造成2伤害，该伤害将无视并破除护盾":
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                {
                    for (int i = 0; i <= 5; i++)
                    {
                        BoardSlot slot = bm.GetSlot(i);
                        if (slot?.currentCard3D != null)
                        {
                            Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                            if (c3d?.cardInstance != null)
                            {
                                // 先清除护盾
                                if (c3d.cardInstance.hasShield)
                                {
                                    c3d.cardInstance.RemoveShield();
                                }
                                // 再走统一伤害流程
                                BattleManager.Instance.ApplyDamageToMinionPublic(c3d.cardInstance, 2, null);
                                c3d.UpdateValues();
                            }
                        }
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
                CleanupSpellResources();
                break;
            case "1.获得一名投资者":
                CardData investorTemplate = CardDatabase.Instance?.GetTemplate("03026");
                if (investorTemplate != null)
                {
                    Player.Instance.AddCardToHand(investorTemplate);
                    Debug.Log("风险投资：获得投资者加入手牌");
                }
                CleanupSpellResources();
                break;
            case "1.获得一名猎人":
                CardData hunterTemplate = CardDatabase.Instance?.GetTemplate("03014");
                if (hunterTemplate != null)
                {
                    Player.Instance.AddCardToHand(hunterTemplate);
                    Debug.Log("猎人子弹：获得猎人加入手牌");
                }
                CleanupSpellResources();
                break;
            case "1.选定己方一格子，该格子上召唤物阶位临时+2，每阶段开始恢复2生命值，该效果不可叠加":
                HandManager hmSpot = FindObjectOfType<HandManager>();
                hmSpot.StartCoroutine(hmSpot.SpotlightEffect());
                break;
            case "1.使场上任意一召唤物当前生命值（上限）和当前攻击力（上限）互换":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    CardInstance ci = t3d?.cardInstance;
                    if (ci != null)
                    {
                        int oldAtk = ci.currentAttack;
                        int oldHp = ci.currentHealth;
                        int oldMaxHp = ci.currentMaxHealth;

                        ci.currentAttack = oldHp;
                        ci.currentMaxHealth = oldAtk;
                        ci.currentHealth = oldAtk;

                        t3d.UpdateValues();

                        if (ci.currentHealth <= 0)
                        {
                            BoardSlot.CheckAndHandleDeaths();
                        }
                    }
                }
                CleanupSpellResources();
                break;
            case "1.使场上任意一召唤物攻击力将为1，降低量增加至生命值（若超过上限，上限同时提升）":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    CardInstance ci = t3d?.cardInstance;
                    if (ci != null && ci.currentAttack > 1)
                    {
                        int reduction = ci.currentAttack - 1;
                        ci.currentAttack = 1;
                        ci.currentHealth += reduction;
                        if (ci.currentHealth > ci.currentMaxHealth)
                        {
                            ci.currentMaxHealth = ci.currentHealth;
                        }
                        t3d.UpdateValues();
                    }
                }
                CleanupSpellResources();
                break;
            case "1.使任意一召唤物转变为机械：飞升者，不视为退场，不能对附着物使用":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null && !t3d.cardInstance.isAttached)
                    {
                        CardData ascensionTemplate = CardDatabase.Instance?.GetTemplate("03005");
                        if (ascensionTemplate?.prefab3D != null)
                        {
                            // 销毁旧模型
                            Destroy(targetSlot.currentCard3D);
                            targetSlot.SetCard(null);

                            // 创建飞升者
                            Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(targetSlot.slotID);
                            GameObject model = Instantiate(ascensionTemplate.prefab3D, pos, Quaternion.Euler(0, 180, 0));
                            Card3DInstance new3D = model.GetComponent<Card3DInstance>();
                            if (new3D != null)
                            {
                                CardInstance newInst = model.AddComponent<CardInstance>();
                                newInst.InitFromTemplate(ascensionTemplate, 0);
                                new3D.cardInstance = newInst;
                                new3D.UpdateValues();
                            }
                            targetSlot.SetCard(model);
                            Debug.Log($"机械飞升：槽位{targetSlot.slotID}转变为飞升者");
                        }
                    }
                }
                CleanupSpellResources();
                break;
            case "1.使任意一召唤物转变为渊：被腐化者，不视为退场，不能对附着物使用":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null && !t3d.cardInstance.isAttached)
                    {
                        CardData corruptTemplate = CardDatabase.Instance?.GetTemplate("03003");
                        if (corruptTemplate?.prefab3D != null)
                        {
                            Destroy(targetSlot.currentCard3D);
                            targetSlot.SetCard(null);

                            Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(targetSlot.slotID);
                            GameObject model = Instantiate(corruptTemplate.prefab3D, pos, Quaternion.Euler(0, 180, 0));
                            Card3DInstance new3D = model.GetComponent<Card3DInstance>();
                            if (new3D != null)
                            {
                                CardInstance newInst = model.AddComponent<CardInstance>();
                                newInst.InitFromTemplate(corruptTemplate, 0);
                                new3D.cardInstance = newInst;
                                new3D.UpdateValues();
                            }
                            targetSlot.SetCard(model);
                            Debug.Log($"深渊之息：槽位{targetSlot.slotID}转变为被腐化者");
                        }
                    }
                }
                CleanupSpellResources();
                break;
            case "1.扣己方玩家3生命值，+5能量\n2.当己方玩家生命值<=3时允许弃掉该牌并抽一张牌":
                Player.Instance.TakeDamage(3);
                Player.Instance.AddEnergy(5);
                CleanupSpellResources();
                break;
            case "1.使己方一召唤物退场并回到手牌，摸一张牌":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        t3d.cardInstance.isActiveExit = false;
                        t3d.cardInstance.handledReturnToHand = true;
                        CardData returnTemplate = CardDatabase.Instance?.GetTemplate(t3d.cardInstance.templateID);
                        targetSlot.HandleDeath(targetSlot.currentCard3D);
                        if (returnTemplate != null)
                            Player.Instance.AddCardToHandFromInstance(returnTemplate, t3d.cardInstance);
                        Player.Instance.DrawCardWithoutLimit();
                    }
                }
                CleanupSpellResources();
                break;
            case "1.召唤一名灵能：中枢，为己方全体和手牌中召唤物附加灵能前缀":
                HandManager hmCore = FindObjectOfType<HandManager>();
                hmCore.StartCoroutine(hmCore.SummonCoreEffect());
                break;
            case "1.对对方一排的召唤物造成2伤害，位于中间的额外+1":
                if (targetSlot != null)
                {
                    int rowStart = targetSlot.slotID < 3 ? 0 : 3;
                    BoardManager bmPierce = FindObjectOfType<BoardManager>();
                    for (int col = 0; col < 3; col++)
                    {
                        int slotID = rowStart + col;
                        BoardSlot pierceSlot = bmPierce?.GetSlot(slotID);
                        if (pierceSlot?.currentCard3D != null)
                        {
                            Card3DInstance t3d = pierceSlot.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                int damage = col == 1 ? 3 : 2;
                                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, damage, null);
                                t3d.UpdateValues();
                            }
                        }
                    }
                    BoardSlot.CheckAndHandleDeaths();
                }
                CleanupSpellResources();
                break;
            case "1.选择场上任意一召唤物，本次攻击回合攻击伤害修正x2，在下阶段开始扣除攻击力数值的生命值":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        t3d.cardInstance.overclocked = true;
                        Debug.Log($"超频：{t3d.cardInstance.instanceID} 本阶段伤害x2");
                    }
                }
                CleanupSpellResources();
                break;
            case "1.使己方全体召唤物退场并不触发任何（主动）退场，返还召唤费用":
                BoardManager bmTableFlip = FindObjectOfType<BoardManager>();
                if (bmTableFlip != null)
                {
                    int totalRefund = 0;
                    List<BoardSlot> toRemove = new List<BoardSlot>();
                    for (int i = 6; i <= 11; i++)
                    {
                        BoardSlot slot = bmTableFlip.GetSlot(i);
                        if (slot?.currentCard3D != null)
                        {
                            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ci != null && !ci.isAttached)
                            {
                                totalRefund += ci.currentCost;
                                toRemove.Add(slot);
                            }
                        }
                    }

                    // 先标记所有，再统一退场
                    foreach (BoardSlot slot in toRemove)
                    {
                        CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null)
                        {
                            ci.hasOnDeath = false;
                            ci.hasActiveExit = false;
                            ci.hasRevenge = false;
                            ci.handledReturnToHand = true;
                            ci.giveableDeathTraits?.Clear();
                            ci.grantedTraitTexts?.Clear();
                        }
                    }
                    foreach (BoardSlot slot in toRemove)
                    {
                        slot.HandleDeath(slot.currentCard3D);
                    }
                    Player.Instance.AddEnergy(totalRefund);
                }
                CleanupSpellResources();
                break;
            case "1.使己方场上或手牌中一召唤物阶位永久+3":
                HandManager hmEvo = FindObjectOfType<HandManager>();
                hmEvo.StartCoroutine(hmEvo.GreatEvolutionEffect());
                break;
            case "1.无效果触发对方场上一张反制且自己打出该反制，该反制触发时只扣1能量":
                HandManager hmCK = FindObjectOfType<HandManager>();
                hmCK.StartCoroutine(hmCK.CounterKillerEffect());
                break;
            case "1.使己方一排召唤物对对位召唤物造成攻击力数值的伤害，然后+0-1":
                HandManager hmCharge = FindObjectOfType<HandManager>();
                hmCharge.StartCoroutine(hmCharge.ChargeHornEffect());
                break;
            case "1.为己方全体召唤物附加护盾，该护盾可一直持有，并+1+0，恢复己方一召唤物2生命值":
                BoardManager bmBlood = FindObjectOfType<BoardManager>();
                if (bmBlood != null)
                {
                    for (int i = 6; i <= 11; i++)
                    {
                        BoardSlot slot = bmBlood.GetSlot(i);
                        if (slot?.currentCard3D != null)
                        {
                            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ci != null && !ci.isAttached)
                            {
                                ci.GrantShield(true, false, false);
                                if (!ci.cannotHealOrGainMaxHP)
                                {
                                    ci.currentHealth += 1;
                                    ci.currentMaxHealth += 1;
                                }
                                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                            }
                        }
                    }
                }
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    t3d?.cardInstance?.ReceiveHeal(2, CardInstance.HealSourceType.Spell);
                    t3d?.UpdateValues();
                }
                CleanupSpellResources();
                break;
            case "1.指定两个格子，该格子上召唤物会在攻击回合开始受到位于该格子回合数的伤害，并且永久-1攻击力":
                HandManager hmPlague = FindObjectOfType<HandManager>();
                hmPlague.StartCoroutine(hmPlague.PlagueEffect());
                break;
            case "1.获得一张石头人":
                CardData stoneGolem = CardDatabase.Instance?.GetTemplate("03020");
                if (stoneGolem != null)
                    Player.Instance.AddCardToHand(stoneGolem);
                CleanupSpellResources();
                break;
            case "1.获得一张打工人（神选者）":
                CardData worker = CardDatabase.Instance?.GetTemplate("03009");
                if (worker != null)
                    Player.Instance.AddCardToHand(worker);
                CleanupSpellResources();
                break;
            case "1.召唤一名小团恶念":
                HandManager hmEvil = FindObjectOfType<HandManager>();
                hmEvil.StartCoroutine(hmEvil.SummonSmallEvilEffect());
                break;
            case "1.扣己方1玩家生命值，召唤手牌中的召唤物，召唤费用之和<=8":
                HandManager hmDoor = FindObjectOfType<HandManager>();
                hmDoor.StartCoroutine(hmDoor.DoorEffect());
                break;
            case "1.扣己方2玩家生命值，在下个对方回合结束后己方获得格外一行动回合，额外回合结束为己方全体召唤物恢复2生命值":
                Player.Instance.TakeDamage(2);
                TimeWarpManager.Instance.Activate();
                CleanupSpellResources();
                break;
            default:
                Debug.Log($"未实现的法术效果：{template.effect}");
                CardDrag.CleanupSpellResources();
                break;
        }

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
                            Player.Instance.TakeDamage(1);
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
                return Player.Instance.GetEnergy() >= 8;
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
    }
    IEnumerator EmperorsApprovalEffectCoroutine()
    {
        Player.Instance.handCards.RemoveAll(c => c == null);
        // 不隐藏手牌，直接进入选择模式（手牌和场上都可以选）
        SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in Player.Instance.handCards)
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
        foreach (GameObject card in Player.Instance.handCards)
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

            Player.Instance.DrawCard();
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