using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
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

    // Network guard: only server/Host can resolve card plays
    if (NetworkClient.isConnected && !NetworkServer.active)
    {
        Debug.Log("[CardDrag] Client mode - card play must go through server");
        Destroy(tempCanvas);
        tempCanvas = null;
        SetButtonsInteractable(true);
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = Vector2.zero;
        transform.localScale = originalScale;
        handManager.SetHandAreaRaycast(true);
        handManager.RefreshLayout(true);
        return;
    }

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
		if (template.effect == "1.ѡ���ٻ��Ͻ׶����ƶ��е�һ���ٻ���")
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
		
		// ����ƴ�ˣ�����ֵ<=3ʱ����ѡ���Ƿ�����
		if (template.effect.Contains("�������������ֵ<=3ʱ�����������Ʋ���һ����"))
        {
            if (NetworkPlayer.Local.currentHealth <= 3)
            {
                player.AddEnergy(inst.currentCost);

                ConfirmPanel.Instance.Show("�Ƿ��������Ʋ���һ���ƣ�",
                    () =>
                    {
                        // ѡ�ǣ����Ƴ���
                        CardView cv = GetComponent<CardView>();
                        HandManager hm = FindObjectOfType<HandManager>();
                        if (cv != null) hm?.RemoveCard(cv);
                        NetworkPlayer.Local.DrawCardWithoutLimit();
                        SetButtonsInteractable(true);
                        handManager.SetHandAreaRaycast(true);
                        Debug.Log("����ƴ�ˣ����Ʋ���һ����");
                    },
                    () =>
                    {
                        // ѡ�񣺻�����
                        SetButtonsInteractable(true);
                        transform.SetParent(originalParent);
                        rectTransform.anchoredPosition = Vector2.zero;
                        transform.localScale = originalScale;
                        handManager.SetHandAreaRaycast(true);
                        handManager.RefreshLayout(true);
                        Debug.Log("����ƴ�ˣ�ȡ������");
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
            Debug.Log("���뷴���Ʒ�֧");
            CounterManager.Instance?.PlayCounter(this.gameObject, true);
            CardView cv = GetComponent<CardView>();
            if (cv != null) handManager.RemoveCard(cv);
            else Destroy(gameObject);
            SetButtonsInteractable(true);
            handManager.SetHandAreaRaycast(true);
            CardView.IsAnyCardDragging = false;
            return;
        }

        // ����ֵΪ0�ĸ����ƣ������޼����ٻ���ʱ�޷����
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
                Debug.Log("����û�м����ٻ���޷����");
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
            actualCost = Mathf.Max(0, actualCost - 1);
        if (player == null || !player.UseEnergy(actualCost))
        {
            Debug.Log("�������㣡");
            SetButtonsInteractable(true);
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.localScale = originalScale;
            handManager.SetHandAreaRaycast(true);
            handManager.RefreshLayout(true);
            return;
        }
        inst.currentCost = actualCost;
        // ������Ч����
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.NextCardNullified)
        {
            GlobalEventManager.Instance.NextCardNullified = false;
            CardInstance nullInst = GetComponent<CardInstance>();
            if (nullInst != null) nullInst.ClearAllTraits();
            player.AddEnergy(inst.currentCost); // �˻�����
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
            Debug.Log("�����㷨���ͷ�������");
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
            Debug.Log("û�кϷ�Ŀ�꣬�����޷������");
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
                if (template.effect.Contains("����ֵ>=4"))
                {
                    BoardSlot.extraTargetFilter = (slot) =>
                    {
                        if (slot?.currentCard3D == null) return false;
                        CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        return ci != null && ci.currentHealth >= 4;
                    };

                    if (!HasValidTarget((TargetType)template.targetType))
                    {
                        Debug.Log("û�кϷ�Ŀ�꣬�޷����");
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
                if (template.effect.Contains("��������һ�ٻ���"))
                {
                    BoardSlot.extraTargetFilter = (slot) =>
                    {
                        return slot?.currentCard3D != null;
                    };
                }
                if (template.effect.Contains("���ܶԸ�����ʹ��"))
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
                Debug.Log("�Է�����û���ٻ����/��/�����޷����");
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
        Debug.Log($"ResolveSpellEffect ���룺effect=\"{template.effect}\"");

        if (template.effect.Contains("ʹ����һ�ٻ����˳�") && template.effect.Contains("��1����"))
        {
            Debug.Log($"Ч��ִ�У�targetSlot={targetSlot?.slotID}");
            if (targetSlot != null && targetSlot.currentCard3D != null)
            {
                Card3DInstance target3D = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                if (target3D?.cardInstance != null)
                {
                    target3D.cardInstance.isActiveExit = false;
                }
                targetSlot.HandleDeath(targetSlot.currentCard3D);
            }
            NetworkPlayer.Local.DrawCard();
            CardDrag.CleanupSpellResources();
            return;
        }

        switch (template.effect)
        {
            case "1.������>=8ʱ�������\n2.��������":
                NetworkPlayer player = NetworkPlayer.Local;
                if (player != null)
                {
                    player.DrawCard();
                    player.DrawCard();
                }
                CardDrag.CleanupSpellResources();
                break;

            case "1.�ԶԷ�������1�˺�":
                NetworkPlayer.Remote?.TakeDamage(1);
                CardDrag.CleanupSpellResources();
                break;

            case "1.Ϊ�������ƻ���һ�ٻ��︽��Ԩǰ׺\n2.��1����":
                SelectionManager.Instance.StartSafeCoroutine(EmperorsApprovalEffectCoroutine());
                break;

            case "1.�ָ�����һ�ٻ���3����ֵ":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Spell);
                    t3d?.UpdateValues();
                }
                CleanupSpellResources();
                break;
            case "1.������һ�Ž�/ľ/ˮ/��/��Ԫ��":
                string[] elementIDs = { "03015", "03016", "03017", "03018", "03019" };
                string randomID = elementIDs[Random.Range(0, elementIDs.Length)];
                CardData element = CardDatabase.Instance?.GetTemplate(randomID);
                if (element != null)
                {
                    NetworkPlayer.Local.AddCardToHand(element);
                    Debug.Log($"Ʈ��Ԫ�أ���� {element.cardName}");
                }
                CleanupSpellResources();
                break;
            case "1.�ڶԷ������ٻ�һ��ͽ��������Ա���޷��ٻ�":
                HandManager hmBetray = FindObjectOfType<HandManager>();
                hmBetray.StartCoroutine(hmBetray.BetrayalEffect());
                break;
            case "1.���һ��׷����":
                CardData followerTemplate = CardDatabase.Instance?.GetTemplate("03001");
                if (followerTemplate != null)
                {
                    NetworkPlayer.Local.AddCardToHand(followerTemplate);
                    Debug.Log("����С�ӣ����׷���߼�������");
                }
                CardDrag.CleanupSpellResources();
                break;

            case "1.������������һ�ٻ���Ľ���":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    CardInstance targetCI = t3d?.cardInstance;
                    CardData targetTemplate = CardDatabase.Instance?.GetTemplate(targetCI?.templateID);
                    Debug.Log($"ѹեǱ�ܴ�������: templateID={targetTemplate?.templateID}, hasOnEnter={targetTemplate?.hasOnEnter}, inst={targetCI != null}");

                    if (targetTemplate != null && targetTemplate.hasOnEnter)
                    {
                        Debug.Log($"ѹեǱ�ܵ��� StartOnEnterEffect");
                        targetSlot.StartOnEnterEffect(targetTemplate, targetCI);
                    }
                    else
                    {
                        Debug.Log($"ѹեǱ�ܣ�Ŀ�� {targetTemplate?.cardName} û�н���Ч��");
                    }
                }
                CleanupSpellResources();
                break;
            case "1.�ԶԷ�һ�ٻ������3�˺�":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance target3D = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (target3D?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(target3D.cardInstance, 3, null);
                        target3D.UpdateValues();
                        Debug.Log($"����һ���� {target3D.cardInstance.instanceID} ���3�˺�");
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
                CardDrag.CleanupSpellResources();
                break;
            case "1.���������ٻ����λ��":
                HandManager hm = FindObjectOfType<HandManager>();
                hm.StartCoroutine(hm.ReformFormationEffect(this));
                break;
            case "1.�ٻ�������е���ӱ�":
                HandManager hmSummon = FindObjectOfType<HandManager>();
                hmSummon.StartCoroutine(hmSummon.SummonTwoMinions());
                break;
            case "1.�ָ����2����ֵ":
                NetworkPlayer.Local.ReceiveHeal(2, CardInstance.HealSourceType.Spell);
                CleanupSpellResources();
                break;
            case "1.�ָ����4����ֵ":
                NetworkPlayer.Local.ReceiveHeal(4, CardInstance.HealSourceType.Spell);
                CleanupSpellResources();
                break;
            case "1.��ౣ��4�����ƣ�������������������":
                HandManager hmHandCleanse = FindObjectOfType<HandManager>();
                hmHandCleanse.StartCoroutine(hmHandCleanse.HandCleanseEffect());
                break;
            case "1.�Լ���һ������ֵ>=4���ٻ������4�˺���+4����":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null && t3d.cardInstance.currentHealth >= 4)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 4, null);
                        t3d.UpdateValues();
                        NetworkPlayer.Local.AddEnergy(4);
                        BoardSlot.CheckAndHandleDeaths();
                    }
                }
                BoardSlot.extraTargetFilter = null;
                CleanupSpellResources();
                break;
            case "1.��7����Ȼ����4���ƣ�ÿ��һ�Ż���ʹ�÷���Ϊ5����+1����":
                HandManager hmMany = FindObjectOfType<HandManager>();
                hmMany.StartCoroutine(hmMany.ManyCardsEffect());
                break;
            case "1.�ԶԷ�ȫ�����2�˺������˺������Ӳ��Ƴ�����":
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
                                // ���������
                                if (c3d.cardInstance.hasShield)
                                {
                                    c3d.cardInstance.RemoveShield();
                                }
                                // ����ͳһ�˺�����
                                BattleManager.Instance.ApplyDamageToMinionPublic(c3d.cardInstance, 2, null);
                                c3d.UpdateValues();
                            }
                        }
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
                CleanupSpellResources();
                break;
            case "1.���һ��Ͷ����":
                CardData investorTemplate = CardDatabase.Instance?.GetTemplate("03026");
                if (investorTemplate != null)
                {
                    NetworkPlayer.Local.AddCardToHand(investorTemplate);
                    Debug.Log("����Ͷ�ʣ����Ͷ���߼�������");
                }
                CleanupSpellResources();
                break;
            case "1.���һ������":
                CardData hunterTemplate = CardDatabase.Instance?.GetTemplate("03014");
                if (hunterTemplate != null)
                {
                    NetworkPlayer.Local.AddCardToHand(hunterTemplate);
                    Debug.Log("�����ӵ���������˼�������");
                }
                CleanupSpellResources();
                break;
            case "1.ѡ������һ���ӣ��ø������ٻ����λ��ʱ+2��ÿ�׶ο�ʼ�ָ�2����ֵ����Ч�����ɵ���":
                HandManager hmSpot = FindObjectOfType<HandManager>();
                hmSpot.StartCoroutine(hmSpot.SpotlightEffect());
                break;
            case "1.ʹ��������һ�ٻ��ﵱǰ����ֵ�����ޣ��͵�ǰ�����������ޣ�����":
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
            case "1.ʹ��������һ�ٻ��﹥������Ϊ1������������������ֵ�����������ޣ�����ͬʱ������":
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
            case "1.ʹ����һ�ٻ���ת��Ϊ��е�������ߣ�����Ϊ�˳������ܶԸ�����ʹ��":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null && !t3d.cardInstance.isAttached)
                    {
                        CardData ascensionTemplate = CardDatabase.Instance?.GetTemplate("03005");
                        if (ascensionTemplate?.prefab3D != null)
                        {
                            // ���پ�ģ��
                            Destroy(targetSlot.currentCard3D);
                            targetSlot.SetCard(null);

                            // ����������
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
                            Debug.Log($"��е��������λ{targetSlot.slotID}ת��Ϊ������");
                        }
                    }
                }
                CleanupSpellResources();
                break;
            case "1.ʹ����һ�ٻ���ת��ΪԨ���������ߣ�����Ϊ�˳������ܶԸ�����ʹ��":
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
                            Debug.Log($"��Ԩ֮Ϣ����λ{targetSlot.slotID}ת��Ϊ��������");
                        }
                    }
                }
                CleanupSpellResources();
                break;
            case "1.�ۼ������3����ֵ��+5����\n2.�������������ֵ<=3ʱ�����������Ʋ���һ����":
                NetworkPlayer.Local.TakeDamage(3);
                NetworkPlayer.Local.AddEnergy(5);
                CleanupSpellResources();
                break;
            case "1.ʹ����һ�ٻ����˳����ص����ƣ���һ����":
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
                            NetworkPlayer.Local.AddCardToHandFromInstance(returnTemplate, t3d.cardInstance);
                        NetworkPlayer.Local.DrawCardWithoutLimit();
                    }
                }
                CleanupSpellResources();
                break;
            case "1.�ٻ�һ�����ܣ����࣬Ϊ����ȫ����������ٻ��︽������ǰ׺":
                HandManager hmCore = FindObjectOfType<HandManager>();
                hmCore.StartCoroutine(hmCore.SummonCoreEffect());
                break;
            case "1.�ԶԷ�һ�ŵ��ٻ������2�˺���λ���м�Ķ���+1":
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
            case "1.ѡ��������һ�ٻ�����ι����غϹ����˺�����x2�����½׶ο�ʼ�۳���������ֵ������ֵ":
                if (targetSlot != null && targetSlot.currentCard3D != null)
                {
                    Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        t3d.cardInstance.overclocked = true;
                        Debug.Log($"��Ƶ��{t3d.cardInstance.instanceID} ���׶��˺�x2");
                    }
                }
                CleanupSpellResources();
                break;
            case "1.ʹ����ȫ���ٻ����˳����������κΣ��������˳��������ٻ�����":
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

                    // �ȱ�����У���ͳһ�˳�
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
                    NetworkPlayer.Local.AddEnergy(totalRefund);
                }
                CleanupSpellResources();
                break;
            case "1.ʹ�������ϻ�������һ�ٻ����λ����+3":
                HandManager hmEvo = FindObjectOfType<HandManager>();
                hmEvo.StartCoroutine(hmEvo.GreatEvolutionEffect());
                break;
            case "1.��Ч�������Է�����һ�ŷ������Լ�����÷��ƣ��÷��ƴ���ʱֻ��1����":
                HandManager hmCK = FindObjectOfType<HandManager>();
                hmCK.StartCoroutine(hmCK.CounterKillerEffect());
                break;
            case "1.ʹ����һ���ٻ���Զ�λ�ٻ�����ɹ�������ֵ���˺���Ȼ��+0-1":
                HandManager hmCharge = FindObjectOfType<HandManager>();
                hmCharge.StartCoroutine(hmCharge.ChargeHornEffect());
                break;
            case "1.Ϊ����ȫ���ٻ��︽�ӻ��ܣ��û��ܿ�һֱ���У���+1+0���ָ�����һ�ٻ���2����ֵ":
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
            case "1.ָ���������ӣ��ø������ٻ�����ڹ����غϿ�ʼ�ܵ�λ�ڸø��ӻغ������˺�����������-1������":
                HandManager hmPlague = FindObjectOfType<HandManager>();
                hmPlague.StartCoroutine(hmPlague.PlagueEffect());
                break;
            case "1.���һ��ʯͷ��":
                CardData stoneGolem = CardDatabase.Instance?.GetTemplate("03020");
                if (stoneGolem != null)
                    NetworkPlayer.Local.AddCardToHand(stoneGolem);
                CleanupSpellResources();
                break;
            case "1.���һ�Ŵ��ˣ���ѡ�ߣ�":
                CardData worker = CardDatabase.Instance?.GetTemplate("03009");
                if (worker != null)
                    NetworkPlayer.Local.AddCardToHand(worker);
                CleanupSpellResources();
                break;
            case "1.�ٻ�һ��С�Ŷ���":
                HandManager hmEvil = FindObjectOfType<HandManager>();
                hmEvil.StartCoroutine(hmEvil.SummonSmallEvilEffect());
                break;
            case "1.�ۼ���1�������ֵ���ٻ������е��ٻ���ٻ�����֮��<=8":
                HandManager hmDoor = FindObjectOfType<HandManager>();
                hmDoor.StartCoroutine(hmDoor.DoorEffect());
                break;
            case "1.�ۼ���2�������ֵ�����¸��Է��غϽ����󼺷���ø���һ�ж��غϣ�����غϽ���Ϊ����ȫ���ٻ���ָ�2����ֵ":
                NetworkPlayer.Local.TakeDamage(2);
                TimeWarpManager.Instance.Activate();
                CleanupSpellResources();
                break;
            default:
                Debug.Log($"δʵ�ֵķ���Ч����{template.effect}");
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
                            NetworkPlayer.Local.TakeDamage(1);
                            Debug.Log("����Ч�����Է����а��������1Ѫ");
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
        Debug.Log($"HasValidTarget �����ã�type={type}");
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
                    Debug.Log($"����λ{id}��slot={slot != null}, hasCard={slot?.hasCard}, isBlocked={slot?.isBlocked}");
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
        if (template.effect.Contains("ʹ����һ�ٻ����˳�") && template.effect.Contains("��1����"))
            return true;

        switch (template.effect)
        {
            case "1.������>=8ʱ�������\n2.��������":
                return NetworkPlayer.Local.GetEnergy() >= 8;
            case "1.�ۼ������3����ֵ��+5����\n2.�������������ֵ<=3ʱ�����������Ʋ���һ����":
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
            // ֻҪ��һ����λû��������û������˵��û��
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
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);
        // ���������ƣ�ֱ�ӽ���ѡ��ģʽ�����ƺͳ��϶�����ѡ��
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
            if (!targetCI.prefixes.Contains("Ԩ"))
            {
                if (string.IsNullOrEmpty(targetCI.prefixes) || targetCI.prefixes == "��")
                    targetCI.prefixes = "Ԩ";
                else
                    targetCI.prefixes += " Ԩ";
            }

            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D display2D = target.GetComponent<CardDisplay2D>();
            display2D?.Refresh();

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