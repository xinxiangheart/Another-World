using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public enum TurnPhase
    {
        PhaseStart,
        MyTurn,
        EnemyTurn,
        BattlePhase
    }

    public TurnPhase currentPhase = TurnPhase.PhaseStart;
    public static TurnManager Instance { get; private set; }
    void Awake() { Instance = this; }
    [Header("先后手")]
    public bool isMyTurnFirst = true;

    [Header("调试信息")]
    public int phaseCount = 0;

    void Start()
    {
        Debug.Log("=== 游戏开始 ===");
        StartCoroutine(InitialDraw());
    }

    IEnumerator InitialDraw()
    {
        yield return null;

        for (int i = 0; i < 2; i++)
            Player.Instance.DrawCard();

        CardData chosenOne = ChosenOneManager.Instance?.DrawChosenOne();
        if (chosenOne != null)
        {
            Player.Instance.AddCardToHand(chosenOne);
        }

        Debug.Log($"开局抽牌完成，手牌：{Player.Instance.handCards.Count} 张");
        StartNewPhase();
    }

    public void StartNewPhase()
    {
       
        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();

        if (slots != null)
        {
            foreach (BoardSlot slot in slots)
            {
                if (slot == null || slot.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d != null && c3d.cardInstance != null)
                {
                    if (c3d.cardInstance.templateID == "01101")
                    {
                        slot.HandleDeath(slot.currentCard3D);
                        continue;
                    }
                }
            }
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = slots[i];
                if (slot == null || slot.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d != null && c3d.cardInstance != null)
                {
                    if (c3d.cardInstance.templateID == "01531")
                        c3d.cardInstance._outlawPlayerDamageThisTurn = false;
                    c3d.cardInstance.silencedThisPhase = false;
                    c3d.cardInstance.poisoned = false;
                    c3d.cardInstance.enemyDamageSourceIDs.Clear();
                    c3d.cardInstance.damageSourceInstanceIDs.Clear();
                    c3d.cardInstance.ironSmithOneCostConsumedCount = 0;
                    if (c3d.cardInstance.templateID == "01124" || c3d.cardInstance.templateID == "01312" || c3d.cardInstance.templateID == "01516")
                    {
                        c3d.cardInstance.hasFirstStrike = true;
                    }
                    if (c3d.cardInstance.templateID == "01511")
                    {
                        c3d.cardInstance.mindScholarEnterTriggeredThisPhase = false;
                        c3d.cardInstance.mindScholarDiscardTriggeredThisPhase = false;
                    }
                }
            }
        }

        currentPhase = TurnPhase.PhaseStart;
        phaseCount++;
        isMyTurnFirst = !isMyTurnFirst;

        string firstPlayer = isMyTurnFirst ? "我方" : "敌方";
        Debug.Log($"\n========== 第 {phaseCount} 阶段开始（{firstPlayer}先手）==========");
        if (CardInstance.shadowMasterAlive)
        {
            BoardSlot bs = FindObjectOfType<BoardSlot>();
            if (bs != null)
                bs.StartCoroutine(bs.SummonAllShadows());
        }
        if (slots != null)
        {
            int mechCount = 0;
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot s = slots[i];
                if (s?.currentCard3D != null)
                {
                    CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.prefixes.Contains("机械")) mechCount++;
                }
            }
            if (mechCount >= 3)
            {
                bool amplifierActive = true;
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot s = slots[i];
                    if (s?.currentCard3D != null)
                    {
                        CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.templateID == "01506" && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                        {
                            amplifierActive = false;
                            break;
                        }
                    }
                }
                if (amplifierActive)
                {
                    for (int i = 6; i <= 11; i++)
                    {
                        BoardSlot s = slots[i];
                        if (s?.currentCard3D != null)
                        {
                            CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ci != null)
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
        }
        // 追随者：每阶段开始为宿主+0+1
        if (slots != null)
        {
            BoardManager bmFollow = FindObjectOfType<BoardManager>();
            if (bmFollow != null)
            {
                foreach (GameObject obj in bmFollow.attachedModels)
                {
                    if (obj == null) continue;
                    Card3DInstance c3dFollow = obj.GetComponent<Card3DInstance>();
                    if (c3dFollow?.cardInstance?.templateID == "03001" && c3dFollow.cardInstance.isAttached)
                    {
                       
                        if (!c3dFollow.cardInstance.CanTriggerTrait("阶段开始")) continue;
                        int hostSlotID = c3dFollow.cardInstance.hostSlotID;
                        BoardSlot hostSlot = bmFollow.GetSlot(hostSlotID);
                        if (hostSlot?.currentCard3D != null)
                        {
                            CardInstance hostCard = hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (hostCard != null)
                            {
                                hostCard.currentAttack += 1;
                                hostSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                            }
                        }
                    }
                }
            }
        }
        // 聚光灯：每阶段开始恢复2生命值
        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot == null || !slot.hasSpotlight || slot.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null)
                {
                    ci.ReceiveHeal(2, CardInstance.HealSourceType.Minion);
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
        } 
        // 超频：下阶段开始扣除攻击力数值的生命值
        if (slots != null)
        {
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.overclocked)
                {
                    ci.currentHealth -= ci.currentAttack;
                    if (ci.currentHealth < 0) ci.currentHealth = 0;
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    ci.overclocked = false;
                    Debug.Log($"超频惩罚：{ci.instanceID} 扣除{ci.currentAttack}生命值");
                }
            }
            BoardSlot.CheckAndHandleDeaths();
        }
        foreach (GameObject card in Player.Instance.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.templateID == "01524")
            {
                ci.scrollCorePhaseCount++;
                if (ci.scrollCorePhaseCount > 5) ci.scrollCorePhaseCount = 5;
                ci.currentCost = ci.scrollCorePhaseCount;
                card.GetComponent<CardDisplay2D>()?.Refresh();
            }
        }
        CounterManager.Instance?.CheckOnPhaseEnd();
        CounterManager.Instance?.CheckOnPhaseStart();

        HandManager hm = FindObjectOfType<HandManager>();
        if (hm != null && slots != null)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D == null) continue;
                    CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.isXValue)
                        hm.UpdateXValues(ci);
                }
            }
        }

        // 深海恶物：每阶段开始扣1生命值
        if (slots != null)
        {
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                if (slot.deepSeaHealthDebuff)
                {
                    CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null)
                    {
                        ci.currentHealth -= 1;
                        slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
            BoardSlot.CheckAndHandleDeaths();
        }
        // 打工人：下阶段开始自动退场
        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "03009")
                {
                    ci.isActiveExit = false;
                    slot.HandleDeath(slot.currentCard3D);
                    break;
                }
            }
        }
        // 小团恶念变大团
        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "03010")
                {
                    CardData bigEvil = CardDatabase.Instance?.GetTemplate("03011");
                    if (bigEvil?.prefab3D != null)
                    {
                        Vector3 pos = slot.currentCard3D.transform.position;
                        Destroy(slot.currentCard3D);
                        slot.SetCard(null);
                        GameObject model = Instantiate(bigEvil.prefab3D, pos, Quaternion.Euler(0, 180, 0));
                        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
                        if (c3d != null)
                        {
                            CardInstance newCI = model.AddComponent<CardInstance>();
                            newCI.InitFromTemplate(bigEvil, 0);
                            newCI._justTransformed = true;
                            c3d.cardInstance = newCI;
                            c3d.UpdateValues();
                        }
                        slot.SetCard(model);
                    }
                }
            }
        }
        // 大团恶念退场
        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "03011" && !ci._justTransformed)
                {
                    slot.HandleDeath(slot.currentCard3D);
                    Player.Instance.AddEnergy(5);
                    StartCoroutine(SummonSmallEvilOnSlot());
                    break;
                }
            }
        }
        // 大团退场后清除标记
        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null) ci._justTransformed = false;
            }
        }
        // ===== 阶段开始触发的弹窗 =====
        if (slots != null)
        {
            // 铁匠
            for (int i = 6; i <= 11; i++)
            {
                if (slots[i]?.currentCard3D == null) continue;
                CardInstance ci = slots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01525")
                {
                    if (!ci.CanTriggerTrait("阶段开始")) continue;
                    ConfirmQueueManager.Instance.EnqueueConfirm("是否消耗召唤物？（熔能铁匠）",
                        onYes: (done) =>
                        {
                            StartCoroutine(IronSmithSelectCard(done));
                        },
                        onNo: (done) => { done(); }
                    );
                    break;
                }
            }
            // 执行之剑
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01535")
                {
                    ConfirmQueueManager.Instance.EnqueueConfirm("是否消耗法术？（执行之剑）",
                        onYes: (done) =>
                        {
                            StartCoroutine(ExecutionSwordSelectSpell(ci, done));
                        },
                        onNo: (done) =>
                        {
                            ci.consumedSpellCost = 0;
                            done();
                        }
                    );
                    break;
                }
            }
            // 忤逆者
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = FindObjectOfType<BoardManager>()?.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance?.templateID == "01526")
                {
                    CardInstance rebelCI = c3d.cardInstance;
                    if (!rebelCI.CanTriggerTrait("阶段开始")) continue;
                    BoardSlot rebelSlot = slot;
                    Card3DInstance rebel3D = c3d;

                    ConfirmQueueManager.Instance.EnqueueConfirm("是否消耗召唤物？（忤逆者）",
                        onYes: (done) =>
                        {
                            ConfirmQueueManager.EnterSelectionMode();
                            var validCards = ConfirmQueueManager.FilterHandCards(ci =>
                                CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Summon);

                            if (validCards.Count == 0)
                            {
                                ConfirmQueueManager.ExitSelectionMode();
                                ConfirmQueueManager.RestoreAllHandCards();
                                done();
                                return;
                            }

                            StartCoroutine(RebelSelectCard(rebelCI, rebelSlot, rebel3D, validCards, done));
                        },
                        onNo: (done) => { done(); }
                    );
                    break;
                }
            }
        }

        // ===== 自己回合开始 =====
        if (isMyTurnFirst)
        {
            currentPhase = TurnPhase.MyTurn;
            SetEndButton(true);
            Player.Instance.AddEnergy(6);
            FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
            TriggerMyTurnStartEffects();
            Debug.Log("我方先手，进入我方主回合");
        }
        else
        {
            Debug.Log("敌方先手 → 跳过（测试模式）");
            currentPhase = TurnPhase.EnemyTurn;
            Debug.Log("敌方回合 → 跳过");
            currentPhase = TurnPhase.MyTurn;
            SetEndButton(true);
            Player.Instance.AddEnergy(6);
            FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
            TriggerMyTurnStartEffects();
            Debug.Log("进入我方主回合");
        }
    }

    public void EndCurrentTurn()
    {
        Player.Instance._energyCanExceedLimit = false;
        if (Player.Instance.currentEnergy > Player.Instance.maxEnergy)
            Player.Instance.currentEnergy = Player.Instance.maxEnergy;
        Player.Instance.UpdateUI();

        if (currentPhase != TurnPhase.MyTurn) return;

        Debug.Log("玩家点击结束回合");
        SetEndButton(false);

        // 额外回合结束时处理
        if (TimeWarpManager.Instance.inExtraTurn)
        {
            TimeWarpManager.Instance.inExtraTurn = false;

            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                for (int i = 6; i <= 11; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D != null)
                    {
                        CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && !ci.isAttached)
                        {
                            ci.ReceiveHeal(2, CardInstance.HealSourceType.Spell);
                            slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                        }
                    }
                }
            }

            currentPhase = TurnPhase.BattlePhase;
            StartCoroutine(BattleManager.Instance.BattleCoroutine());
            return;
        }

        // 检测是否触发额外回合
        if (TimeWarpManager.Instance.extraTurnPending)
        {
            TimeWarpManager.Instance.extraTurnPending = false;
            TimeWarpManager.Instance.inExtraTurn = true;

            currentPhase = TurnPhase.MyTurn;
            SetEndButton(true);
            Player.Instance.AddEnergy(6);
            FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
            TriggerMyTurnStartEffects();
            return;
        }

        if (isMyTurnFirst)
        {
            currentPhase = TurnPhase.EnemyTurn;
            Debug.Log("敌方回合 → 跳过");
            CounterManager.Instance?.CheckOnEnemyTurnEnd();
            currentPhase = TurnPhase.BattlePhase;
            StartCoroutine(BattleManager.Instance.BattleCoroutine());
        }
        else
        {
            CounterManager.Instance?.CheckOnEnemyTurnEnd();
            currentPhase = TurnPhase.BattlePhase;
            StartCoroutine(BattleManager.Instance.BattleCoroutine());
        }
    }

    void TriggerMyTurnStartEffects()
    {
        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (slots == null) return;

        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = slots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01129" && !ci.isAttached
                && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(ci)))
            {
                ci.ReceiveHeal(2, CardInstance.HealSourceType.Minion);
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }
        // 回合开始退场+2能量（己方回合开始分支中）
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = slots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01511")
            {
                ci.isActiveExit = false;
                slot.HandleDeath(slot.currentCard3D);
                Player.Instance.AddEnergy(2);
                break;
            }
        }
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = slots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci._nourisherHost)
            {
                CardInstance nourisher = FindNourisherByInstanceID(ci._nourisherInstanceID);
                if (nourisher != null && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(nourisher)))
                {
                    ci.ReceiveHeal(2, CardInstance.HealSourceType.Minion);
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
        }

        BoardManager bmHeal = FindObjectOfType<BoardManager>();
        if (bmHeal != null && slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = bmHeal.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance?.templateID == "01302")
                {
                    if (!c3d.cardInstance.CanTriggerTrait("回合开始")) continue;
                    int myRow = i < 9 ? 0 : 3;
                    int rowStart = 6 + myRow;
                    int rowEnd = rowStart + 3;
                    for (int j = rowStart; j < rowEnd; j++)
                    {
                        BoardSlot healSlot = bmHeal.GetSlot(j);
                        if (healSlot?.currentCard3D != null)
                        {
                            Card3DInstance heal3D = healSlot.currentCard3D.GetComponent<Card3DInstance>();
                            CardInstance healCI = heal3D?.cardInstance;
                            if (healCI != null)
                            {
                                healCI.ReceiveHeal(2, CardInstance.HealSourceType.Minion);
                                heal3D.UpdateValues();
                            }
                        }
                    }
                }
            }
        }

        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                if (slots[i]?.currentCard3D == null) continue;
                Card3DInstance c3d = slots[i].currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance != null && c3d.cardInstance.templateID == "01105")
                {
                    if (!c3d.cardInstance.CanTriggerTrait("回合开始")) continue;
                    Player.Instance.DrawCard();
                }
            }
        }

        bool hasTeleporter = false;
        BoardSlot teleporterSlot = null;
        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = FindObjectOfType<BoardManager>()?.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01113")
                {
                    if (!ci.CanTriggerTrait("回合开始")) continue;
                    hasTeleporter = true;
                    teleporterSlot = slot;
                    break;
                }
            }
        }
        if (hasTeleporter)
        {
            ConfirmQueueManager.Instance.EnqueueConfirm("是否与己方一召唤物互换位置？",
                onYes: (done) =>
                {
                    SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
                    {
                        if (target != null && target != teleporterSlot && target.currentCard3D != null)
                            SwapSlots(teleporterSlot, target);
                        ConfirmQueueManager.ExitSelectionMode();
                        done();
                    });
                },
                onNo: (done) => { done(); }
            );
        }

        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01315")
                {
                    if (!ci.CanTriggerTrait("回合开始")) continue;
                    Player.Instance.AddEnergy(1);
                    Player.Instance.DrawCardWithoutLimit();
                }
            }
        }
    }
    IEnumerator IronSmithSelectCard(Action onComplete)
    {
        CardInstance ironSmithInst = null;
        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (slots != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                if (slots[i]?.currentCard3D == null) continue;
                Card3DInstance c3d = slots[i].currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance?.templateID == "01525")
                {
                    ironSmithInst = c3d.cardInstance;
                    break;
                }
            }
        }

        if (ironSmithInst == null)
        {
            onComplete();
            yield break;
        }

        bool done = false;
        while (!done)
        {
            ConfirmQueueManager.EnterSelectionMode();
            var validCards = ConfirmQueueManager.FilterHandCards(ci =>
            {
                CardData template = CardDatabase.Instance?.GetTemplate(ci.templateID);
                if (template == null || template.cardType != CardType.Summon) return false;
                return template.baseCost == 1 || template.baseCost == 3 || template.baseCost == 5;
            });

            if (validCards.Count == 0)
            {
                ConfirmQueueManager.ExitSelectionMode();
                ConfirmQueueManager.RestoreAllHandCards();
                break;
            }

            CardInstance selectedCard = null;
            bool selectionDone = false;

            foreach (GameObject card in validCards)
            {
                CardClickHandler handler = card.GetComponent<CardClickHandler>();
                if (handler == null) handler = card.AddComponent<CardClickHandler>();
                handler.onClick = () => { selectedCard = card.GetComponent<CardInstance>(); selectionDone = true; };
            }

            yield return new WaitUntil(() => selectionDone);

            foreach (GameObject card in validCards)
            {
                CardClickHandler handler = card.GetComponent<CardClickHandler>();
                if (handler != null) Destroy(handler);
            }
            ConfirmQueueManager.RestoreAllHandCards();

            if (selectedCard == null)
            {
                ConfirmQueueManager.ExitSelectionMode();
                break;
            }

            bool confirmDone = false;
            bool confirmed = false;
            ConfirmPanel.Instance.Show("是否确认消耗？",
                () => { confirmed = true; confirmDone = true; },
                () => { confirmDone = true; }
            );
            yield return new WaitUntil(() => confirmDone);

            if (!confirmed)
            {
                ConfirmQueueManager.ExitSelectionMode();
                continue;
            }

            CardData template = CardDatabase.Instance?.GetTemplate(selectedCard.templateID);
            int cost = template?.baseCost ?? 0;
            int energy = cost switch { 1 => 0, 3 => 2, 5 => 4, _ => 0 };

            Player.Instance.AddEnergy(energy);
            Player.Instance.RemoveCardFromHand(selectedCard.gameObject);
            Destroy(selectedCard.gameObject);

            ironSmithInst.ironSmithTotalConsumedCount++;

            // 每消耗3个召唤物，强化一个槽位
            if (ironSmithInst.ironSmithTotalConsumedCount % 3 == 0)
            {
                yield return StartCoroutine(StrengthenSlot());
            }

            // 1费召唤物：本阶段最多弹出2次继续弹窗
            if (cost == 1)
            {
                ironSmithInst.ironSmithOneCostConsumedCount++;
                if (ironSmithInst.ironSmithOneCostConsumedCount < 2)
                {
                    bool continueDone = false;
                    bool continueSelect = false;
                    ConfirmPanel.Instance.Show("是否继续消耗基础费用为1的召唤物？",
                        () => { continueSelect = true; continueDone = true; },
                        () => { continueDone = true; }
                    );
                    yield return new WaitUntil(() => continueDone);
                    if (!continueSelect) done = true;
                }
                else
                {
                    done = true;
                }
            }
            else
            {
                done = true;
            }
        }

        ConfirmQueueManager.ExitSelectionMode();
        onComplete();
    }
    IEnumerator StrengthenSlot()
    {
        BoardSlot.isStrengtheningSlot = true;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
        {
            if (target != null && !target.isBlocked)
            {
                target.slotTempAttackBoost += 2;
                if (target.currentCard3D != null)
                {
                    Card3DInstance c3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance != null && !c3d.cardInstance.isXValue)
                    { c3d.cardInstance.currentAttack += 2; c3d.UpdateValues(); }
                }
            }
            BoardSlot.isStrengtheningSlot = false;
            ConfirmQueueManager.ExitSelectionMode();
            done = true;
        });
        yield return new WaitUntil(() => done);
    }
    IEnumerator RebelSelectCard(CardInstance rebelCI, BoardSlot rebelSlot, Card3DInstance rebel3D, List<GameObject> validCards, Action onComplete)
    {
        CardInstance selectedCard = null;
        bool selectionDone = false;

        foreach (GameObject card in validCards)
        {
            CardClickHandler handler = card.GetComponent<CardClickHandler>();
            if (handler == null) handler = card.AddComponent<CardClickHandler>();
            handler.onClick = () => { selectedCard = card.GetComponent<CardInstance>(); selectionDone = true; };
        }

        yield return new WaitUntil(() => selectionDone);

        foreach (GameObject card in validCards)
        {
            CardClickHandler handler = card.GetComponent<CardClickHandler>();
            if (handler != null) Destroy(handler);
        }

        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard != null)
        {
            int tier = selectedCard.currentTier;
            bool isYuan = selectedCard.prefixes.Contains("渊");
            int healAmount = tier + (isYuan ? 1 : 0);

            Player.Instance.Heal(healAmount);
            rebelCI.currentHealth = Mathf.Min(rebelCI.currentMaxHealth, rebelCI.currentHealth + healAmount);
            rebel3D.UpdateValues();

            Player.Instance.RemoveCardFromHand(selectedCard.gameObject);
            Destroy(selectedCard.gameObject);
        }

        onComplete();
    }

    void SetEndButton(bool enabled)
    {
        EndTurnButton endBtn = FindObjectOfType<EndTurnButton>();
        endBtn?.SetInteractable(enabled);
    }

    public bool IsMyTurn()
    {
        return currentPhase == TurnPhase.MyTurn;
    }

    void SwapSlots(BoardSlot slot1, BoardSlot slot2)
    {
        GameObject card1 = slot1.currentCard3D;
        GameObject card2 = slot2.currentCard3D;

        Vector3 pos1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(slot1.slotID);
        Vector3 pos2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(slot2.slotID);

        slot1.SetCard(null);
        slot2.SetCard(null);

        if (card2 != null)
        {
            if (!slot1.CanPlaceCard(card2.GetComponent<Card3DInstance>()?.cardInstance)) return;
            card2.transform.position = pos1;
            slot1.SetCard(card2);
        }
        if (card1 != null)
        {
            if (!slot2.CanPlaceCard(card1.GetComponent<Card3DInstance>()?.cardInstance)) return;
            card1.transform.position = pos2;
            slot2.SetCard(card1);
        }

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            foreach (GameObject obj in bm.attachedModels)
            {
                CardInstance ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isAttached)
                {
                    if (ci.hostSlotID == slot1.slotID) ci.hostSlotID = slot2.slotID;
                    else if (ci.hostSlotID == slot2.slotID) ci.hostSlotID = slot1.slotID;
                }
            }
        }
    }
    IEnumerator SummonSmallEvilOnSlot()
    {
        CardData template = CardDatabase.Instance?.GetTemplate("03010");
        if (template?.prefab3D == null) yield break;

        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasEmpty = false;
        for (int i = 6; i <= 11; i++)
            if (bm.GetSlot(i) != null && !bm.GetSlot(i).isBlocked && !bm.GetSlot(i).hasCard) { hasEmpty = true; break; }
        if (!hasEmpty) yield break;

        BoardSlot.isPlacingCard = true;
        BoardSlot.isStrengtheningSlot = true;
        bool placed = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (selectedSlot) =>
        {
            if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.slotID < 6) return;
            GameObject temp = new GameObject("TempSmallEvil");
            CardInstance ti = temp.AddComponent<CardInstance>();
            ti.InitFromTemplate(template, 0);
            HandManager hm = FindObjectOfType<HandManager>();
            hm.PlaceCardToSlot(selectedSlot, temp);
            Destroy(temp);
            placed = true;
            BoardSlot.isPlacingCard = false;
            BoardSlot.isStrengtheningSlot = false;
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => placed);
    }
    IEnumerator ExecutionSwordSelectSpell(CardInstance sword, Action done)
    {
        Player.Instance.handCards.RemoveAll(c => c == null);

        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Spell;
        });

        if (validCards.Count == 0)
        {
            sword.consumedSpellCost = 0;
            ConfirmQueueManager.RestoreAllHandCards();
            ConfirmQueueManager.ExitSelectionMode();
            done();
            yield break;
        }

        GameObject selected = null;
        bool selectionDone = false;

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selected = card; selectionDone = true; };
        }

        yield return new WaitUntil(() => selectionDone);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selected != null)
        {
            CardInstance ci = selected.GetComponent<CardInstance>();
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null)
            {
                sword.consumedSpellCost = td.baseCost;
                Player.Instance.handCards.Remove(selected);
                Destroy(selected);
                HandManager hm = FindObjectOfType<HandManager>();
                hm?.RefreshLayout(true);
            }
        }

        done();
    }
    CardInstance FindNourisherByInstanceID(string instanceID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        for (int i = 6; i <= 11; i++)
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
}