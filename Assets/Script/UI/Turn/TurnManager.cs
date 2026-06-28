using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class TurnManager : MonoBehaviour
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
    [Header("�Ⱥ���")]
    public bool isMyTurnFirst = true;

    [Header("������Ϣ")]
    public int phaseCount = 0;

    void Start()
    {
        // If coming from Lobby, wait for NetworkTurnSync to signal game start
        if (LobbyConfig.FromLobby)
        {
            Debug.Log("[TurnManager] Online mode, waiting for both players...");
            return;
        }
        Debug.Log("=== 游戏开始 ===");
        StartCoroutine(InitialDraw());
    }


    IEnumerator InitialDraw()
    {
        yield return null;

        if (NetworkServer.active)
        {
            yield return StartCoroutine(ServerInitialDraw());
            Debug.Log("[TurnManager] Server initial draw complete");
            StartNewPhase();
        }
        else if (!NetworkClient.isConnected && NetworkPlayer.Local != null)
        {
            for (int i = 0; i < 2; i++)
                NetworkPlayer.Local.DrawCard();
            CardData chosenOne = ChosenOneManager.Instance?.DrawChosenOne();
            if (chosenOne != null)
                NetworkPlayer.Local.AddCardToHand(chosenOne);
            StartNewPhase();
        }
    }

    IEnumerator ServerInitialDraw()
    {
        yield return null;

        NetworkPlayer local = NetworkPlayer.Local;
        NetworkPlayer remote = NetworkPlayer.Remote;

        Debug.Log($"[TurnManager] ServerInitialDraw: Local={local?.netId}, Remote={remote?.netId}, remoteConn={remote?.connectionToClient != null}");

        // Host draws directly
        if (local != null)
        {
            for (int i = 0; i < 2; i++)
            {
                CardData card = DeckManager.Instance?.DrawFromMain();
                if (card != null) local.AddCardToHand(card);
            }
            CardData choLocal = ChosenOneManager.Instance?.DrawChosenOne();
            if (choLocal != null) local.AddCardToHand(choLocal);
            Debug.Log($"[TurnManager] Host local drawn: {local.handCards.Count} cards");
        }

        // Remote gets cards via TargetRpc (fires on remote client only)
        if (remote != null)
        {
            Debug.Log($"[TurnManager] Sending {2} main + 1 chosen to Remote netId={remote.netId}");
            for (int i = 0; i < 2; i++)
            {
                CardData card = DeckManager.Instance?.DrawFromMain();
                if (card != null)
                {
                    Debug.Log($"[TurnManager] Remote TargetRpc: {card.templateID}");
                    remote.TargetReceiveCard(remote.connectionToClient, card.templateID);
                }
            }
            CardData choRemote = ChosenOneManager.Instance?.DrawChosenOne();
            if (choRemote != null)
            {
                Debug.Log($"[TurnManager] Remote TargetRpc (chosen): {choRemote.templateID}");
                remote.TargetReceiveCard(remote.connectionToClient, choRemote.templateID);
            }
        }
        else
        {
            Debug.LogWarning("[TurnManager] ServerInitialDraw: Remote is NULL — single player mode?");
        }
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
        // Single-player flips each phase. Online: swap happens in EndCurrentTurn (battle transition).
        if (!NetworkServer.active)
        {
            isMyTurnFirst = !isMyTurnFirst;
        }

        string firstPlayer = isMyTurnFirst ? "Me" : "Enemy";
        Debug.Log(string.Format("\n========== Phase {0} Start, {1} First ==========", phaseCount, firstPlayer));
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
                    if (ci != null && ci.prefixes.Contains("��е")) mechCount++;
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
        // ׷���ߣ�ÿ�׶ο�ʼΪ����+0+1
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
                       
                        if (!c3dFollow.cardInstance.CanTriggerTrait("�׶ο�ʼ")) continue;
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
        // �۹�ƣ�ÿ�׶ο�ʼ�ָ�2����ֵ
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
        // ��Ƶ���½׶ο�ʼ�۳���������ֵ������ֵ
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
                    Debug.Log($"��Ƶ�ͷ���{ci.instanceID} �۳�{ci.currentAttack}����ֵ");
                }
            }
            BoardSlot.CheckAndHandleDeaths();
        }
        foreach (GameObject card in NetworkPlayer.Local.handCards)
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

        // ����ÿ�׶ο�ʼ��1����ֵ
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
        // ���ˣ��½׶ο�ʼ�Զ��˳�
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
        // С�Ŷ�������
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
        // ���Ŷ����˳�
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
                    NetworkPlayer.Local.AddEnergy(5);
                    StartCoroutine(SummonSmallEvilOnSlot());
                    break;
                }
            }
        }
        // �����˳���������
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
        // ===== �׶ο�ʼ�����ĵ��� =====
        if (slots != null)
        {
            // ����
            for (int i = 6; i <= 11; i++)
            {
                if (slots[i]?.currentCard3D == null) continue;
                CardInstance ci = slots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01525")
                {
                    if (!ci.CanTriggerTrait("�׶ο�ʼ")) continue;
                    ConfirmQueueManager.Instance.EnqueueConfirm("�Ƿ������ٻ��������������",
                        onYes: (done) =>
                        {
                            StartCoroutine(IronSmithSelectCard(done));
                        },
                        onNo: (done) => { done(); }
                    );
                    break;
                }
            }
            // ִ��֮��
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01535")
                {
                    ConfirmQueueManager.Instance.EnqueueConfirm("�Ƿ����ķ�������ִ��֮����",
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
            // ������
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = FindObjectOfType<BoardManager>()?.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance?.templateID == "01526")
                {
                    CardInstance rebelCI = c3d.cardInstance;
                    if (!rebelCI.CanTriggerTrait("�׶ο�ʼ")) continue;
                    BoardSlot rebelSlot = slot;
                    Card3DInstance rebel3D = c3d;

                    ConfirmQueueManager.Instance.EnqueueConfirm("�Ƿ������ٻ���������ߣ�",
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

        // ===== Phase assignment =====
        if (NetworkServer.active)
        {
            // Server sets phase directly for host (energy, buttons), then broadcasts perspective-correct phases to both players.
            if (isMyTurnFirst)
            {
                currentPhase = TurnPhase.MyTurn;
                SetPlayerActionsEnabled(true);
                NetworkPlayer.Local?.AddEnergy(6);
                FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
                TriggerMyTurnStartEffects();
                Debug.Log("[TurnManager] Phase start: Host turn (MyTurn)");
            }
            else
            {
                currentPhase = TurnPhase.EnemyTurn;
                SetPlayerActionsEnabled(false);
                Debug.Log("[TurnManager] Phase start: Remote turn first (EnemyTurn from host view)");
            }
            BroadcastTurnPhase(currentPhase);
        }
        else
        {
            if (isMyTurnFirst)
            {
                currentPhase = TurnPhase.MyTurn;
                SetPlayerActionsEnabled(true);
                NetworkPlayer.Local.AddEnergy(6);
                FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
                TriggerMyTurnStartEffects();
            }
            else
            {
                currentPhase = TurnPhase.EnemyTurn;
                SetPlayerActionsEnabled(false);
                // Enemy goes first — auto-end enemy turn to advance to MyTurn
                StartCoroutine(AutoEndEnemyTurn());
            }
        }
    }

    /// <param name="skipEnergyCleanup">True when ServerEndTurn already cleaned up the requesting player's energy.</param>
    public void EndCurrentTurn(bool skipEnergyCleanup = false)
    {
        if (!skipEnergyCleanup && NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local._energyCanExceedLimit = false;
            if (NetworkPlayer.Local.currentEnergy > NetworkPlayer.Local.maxEnergy)
                NetworkPlayer.Local.currentEnergy = NetworkPlayer.Local.maxEnergy;
            NetworkPlayer.Local.UpdateUI();
        }

        if (currentPhase != TurnPhase.MyTurn && currentPhase != TurnPhase.EnemyTurn) return;

        Debug.Log($"玩家点击结束回合  currentPhase={currentPhase}  isMyTurnFirst={isMyTurnFirst}  isServer={NetworkServer.active}");
        SetPlayerActionsEnabled(false);

        // 额外回合
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

        if (TimeWarpManager.Instance.extraTurnPending)
        {
            TimeWarpManager.Instance.extraTurnPending = false;
            TimeWarpManager.Instance.inExtraTurn = true;
            currentPhase = TurnPhase.MyTurn;
            SetPlayerActionsEnabled(true);
            NetworkPlayer.Local.AddEnergy(6);
            FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
            TriggerMyTurnStartEffects();
            return;
        }

        if (NetworkServer.active)
        {
            // Simple alternation: MyTurn→EnemyTurn→BattlePhase.
            // isMyTurnFirst only matters in StartNewPhase to decide who goes first next round.
            if (currentPhase == TurnPhase.MyTurn)
            {
                currentPhase = TurnPhase.EnemyTurn;
                SetPlayerActionsEnabled(false);
                CounterManager.Instance?.CheckOnEnemyTurnEnd();
                Debug.Log("[TurnManager] Switched to EnemyTurn");
            }
            else // EnemyTurn
            {
                CounterManager.Instance?.CheckOnEnemyTurnEnd();
                NetworkTurnSync ntsSwap = FindObjectOfType<NetworkTurnSync>();
                if (ntsSwap != null) ntsSwap.SwapFirstPlayer();
                currentPhase = TurnPhase.BattlePhase;
                SetPlayerActionsEnabled(false);
                // Broadcast BattlePhase to clients so they disable their UI
                BroadcastTurnPhase(currentPhase);
                Debug.Log("[TurnManager] Both players ended, starting Battle");
                StartCoroutine(BattleManager.Instance.BattleCoroutine());
                // BattleCoroutine calls StartNewPhase at end, which will broadcast the next phase.
                return;
            }
            BroadcastTurnPhase(currentPhase);
        }
        else
        {
            // Offline: player vs AI enemy
            if (currentPhase == TurnPhase.MyTurn)
            {
                currentPhase = TurnPhase.EnemyTurn;
                SetPlayerActionsEnabled(false);
                CounterManager.Instance?.CheckOnEnemyTurnEnd();
                StartCoroutine(AutoEndEnemyTurn());
            }
            else // EnemyTurn
            {
                CounterManager.Instance?.CheckOnEnemyTurnEnd();
                currentPhase = TurnPhase.BattlePhase;
                SetPlayerActionsEnabled(false);
                StartCoroutine(BattleManager.Instance.BattleCoroutine());
            }
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
        // �غϿ�ʼ�˳�+2�����������غϿ�ʼ��֧�У�
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = slots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01511")
            {
                ci.isActiveExit = false;
                slot.HandleDeath(slot.currentCard3D);
                NetworkPlayer.Local.AddEnergy(2);
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
                    if (!c3d.cardInstance.CanTriggerTrait("�غϿ�ʼ")) continue;
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
                    if (!c3d.cardInstance.CanTriggerTrait("�غϿ�ʼ")) continue;
                    NetworkPlayer.Local.DrawCard();
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
                    if (!ci.CanTriggerTrait("�غϿ�ʼ")) continue;
                    hasTeleporter = true;
                    teleporterSlot = slot;
                    break;
                }
            }
        }
        if (hasTeleporter)
        {
            ConfirmQueueManager.Instance.EnqueueConfirm("�Ƿ��뼺��һ�ٻ��ﻥ��λ�ã�",
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
                    if (!ci.CanTriggerTrait("�غϿ�ʼ")) continue;
                    NetworkPlayer.Local.AddEnergy(1);
                    NetworkPlayer.Local.DrawCardWithoutLimit();
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
            ConfirmPanel.Instance.Show("�Ƿ�ȷ�����ģ�",
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

            NetworkPlayer.Local.AddEnergy(energy);
            NetworkPlayer.Local.RemoveCardFromHand(selectedCard.gameObject);
            Destroy(selectedCard.gameObject);

            ironSmithInst.ironSmithTotalConsumedCount++;

            // ÿ����3���ٻ��ǿ��һ����λ
            if (ironSmithInst.ironSmithTotalConsumedCount % 3 == 0)
            {
                yield return StartCoroutine(StrengthenSlot());
            }

            // 1���ٻ�����׶���൯��2�μ�������
            if (cost == 1)
            {
                ironSmithInst.ironSmithOneCostConsumedCount++;
                if (ironSmithInst.ironSmithOneCostConsumedCount < 2)
                {
                    bool continueDone = false;
                    bool continueSelect = false;
                    ConfirmPanel.Instance.Show("�Ƿ�������Ļ�������Ϊ1���ٻ��",
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
            bool isYuan = selectedCard.prefixes.Contains("Ԩ");
            int healAmount = tier + (isYuan ? 1 : 0);

            NetworkPlayer.Local.Heal(healAmount);
            rebelCI.currentHealth = Mathf.Min(rebelCI.currentMaxHealth, rebelCI.currentHealth + healAmount);
            rebel3D.UpdateValues();

            NetworkPlayer.Local.RemoveCardFromHand(selectedCard.gameObject);
            Destroy(selectedCard.gameObject);
        }

        onComplete();
    }

    void SetEndButton(bool enabled)
    {
        EndTurnButton endBtn = FindObjectOfType<EndTurnButton>();
        endBtn?.SetInteractable(enabled);
    }
    void SetDrawButtonInteractable(bool enabled)
    {
        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null) drawUI.SetInteractable(enabled);
    }

    void SetPlayerActionsEnabled(bool enabled)
    {
        Debug.Log($"[TurnManager] SetPlayerActionsEnabled({enabled}), currentPhase={currentPhase}");
        SetEndButton(enabled);
        SetDrawButtonInteractable(enabled);
    }

    /// <summary>
    /// Offline only: auto-end the enemy's turn after a short delay.
    /// In the future this should trigger actual enemy AI.
    /// </summary>
    IEnumerator AutoEndEnemyTurn()
    {
        yield return new WaitForSeconds(1f);
        // Energy cleanup before ending (offline enemy turn)
        if (NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local._energyCanExceedLimit = false;
            if (NetworkPlayer.Local.currentEnergy > NetworkPlayer.Local.maxEnergy)
                NetworkPlayer.Local.currentEnergy = NetworkPlayer.Local.maxEnergy;
            NetworkPlayer.Local.UpdateUI();
        }
        EndCurrentTurn(skipEnergyCleanup: true);
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
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

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
                NetworkPlayer.Local.handCards.Remove(selected);
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
