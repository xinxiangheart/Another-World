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
    [Header("调试信息")]
    public bool isMyTurnFirst = true;

    [Header("调试信息")]
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
        // ALSO create server-side tracking cards so CmdPlayCard can find them
        if (remote != null)
        {
            Debug.Log($"[TurnManager] Sending {2} main + 1 chosen to Remote netId={remote.netId}");
            for (int i = 0; i < 2; i++)
            {
                CardData card = DeckManager.Instance?.DrawFromMain();
                if (card != null)
                {
                    remote.TargetReceiveCard(remote.connectionToClient, card.templateID);
                    remote.AddServerSideCard(card);
                }
            }
            CardData choRemote = ChosenOneManager.Instance?.DrawChosenOne();
            if (choRemote != null)
            {
                remote.TargetReceiveCard(remote.connectionToClient, choRemote.templateID);
                remote.AddServerSideCard(choRemote);
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
        // 检测是否触发额外回合
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
        // 检测是否触发额外回合
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
        // 检测是否触发额外回合
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
        // 检测是否触发额外回合
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
        // 检测是否触发额外回合
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

        // ===== Phase-start triggers (run on each client during their MyTurn) =====
        // In OFF-line mode, process immediately. In ONLINE mode, defer to SetPhaseFromNetwork.
        if (!NetworkServer.active && !NetworkClient.isConnected)
            ProcessPhaseStartTriggers();

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
                ProcessPhaseStartTriggers();
                Debug.Log("[TurnManager] Phase start: Host turn (MyTurn)");
            }
            else
            {
                currentPhase = TurnPhase.EnemyTurn;
                SetPlayerActionsEnabled(false);
                Debug.Log("[TurnManager] Phase start: Remote turn first (EnemyTurn from host view)");
            }
            BroadcastTurnPhase(currentPhase);
            BoardSyncManager.MarkDirty();
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
                ProcessPhaseStartTriggers();
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

        Debug.Log($"[TurnManager] EndCurrentTurn  phase={currentPhase}  isMyTurnFirst={isMyTurnFirst}  isServer={NetworkServer.active}");
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
            StartCoroutine(SafeBattle());
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
            // Both must play before battle. Second-to-last player → battle.
            bool bothPlayed = (currentPhase == TurnPhase.MyTurn && !isMyTurnFirst)
                           || (currentPhase == TurnPhase.EnemyTurn && isMyTurnFirst);

            if (bothPlayed)
            {
                CounterManager.Instance?.CheckOnEnemyTurnEnd();
                NetworkTurnSync ntsSwap = FindObjectOfType<NetworkTurnSync>();
                if (ntsSwap != null) ntsSwap.SwapFirstPlayer();
                currentPhase = TurnPhase.BattlePhase;
                SetPlayerActionsEnabled(false);
                BroadcastTurnPhase(currentPhase);
                Debug.Log("[TurnManager] Both played → BattlePhase");
                StartCoroutine(SafeBattle());
                return;
            }

            // First player ended; giving turn to second player.
            TurnPhase newPhase = (currentPhase == TurnPhase.MyTurn) ? TurnPhase.EnemyTurn : TurnPhase.MyTurn;
            BroadcastTurnPhase(newPhase);
            currentPhase = newPhase;
            CounterManager.Instance?.CheckOnEnemyTurnEnd();
            // Host manually sets its own state — TargetRpc is async and may miss the guard
            if (newPhase == TurnPhase.MyTurn)
            {
                SetPlayerActionsEnabled(true);
                NetworkPlayer.Local?.AddEnergy(6);
                FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
                TriggerMyTurnStartEffects();
                ProcessPhaseStartTriggers();
            }
            else
            {
                SetPlayerActionsEnabled(false);
            }
            Debug.Log($"[TurnManager] First player ended → {newPhase}");
        }
        else
        {
            // Offline — player vs AI. isMyTurnFirst alternates each round via StartNewPhase flip.
            if (currentPhase == TurnPhase.MyTurn)
            {
                if (isMyTurnFirst)
                {
                    // Player went first → enemy's turn now
                    currentPhase = TurnPhase.EnemyTurn;
                    SetPlayerActionsEnabled(false);
                    CounterManager.Instance?.CheckOnEnemyTurnEnd();
                    StartCoroutine(AutoEndEnemyTurn());
                }
                else
                {
                    // Enemy went first, player is second → go to battle
                    CounterManager.Instance?.CheckOnEnemyTurnEnd();
                    currentPhase = TurnPhase.BattlePhase;
                    SetPlayerActionsEnabled(false);
                    StartCoroutine(SafeBattle());
                }
            }
            else // EnemyTurn
            {
                if (isMyTurnFirst)
                {
                    // Enemy was second player → go to battle
                    CounterManager.Instance?.CheckOnEnemyTurnEnd();
                    currentPhase = TurnPhase.BattlePhase;
                    SetPlayerActionsEnabled(false);
                    StartCoroutine(SafeBattle());
                }
                else
                {
                    // Enemy went first → now it's the player's turn
                    currentPhase = TurnPhase.MyTurn;
                    SetPlayerActionsEnabled(true);
                    NetworkPlayer.Local.AddEnergy(6);
                    FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
                    TriggerMyTurnStartEffects();
                    ProcessPhaseStartTriggers();
                }
            }
        }
    }

    /// <summary>
    /// Bulletproof battle wrapper. Guarantees StartNewPhase is called even if
    /// BattleCoroutine fails silently.
    /// </summary>
    IEnumerator SafeBattle()
    {
        if (NetworkServer.active)
        {
            yield return null; // let BattlePhase broadcast reach clients first
            BattleManager bm = BattleManager.Instance;
            if (bm != null)
                yield return StartCoroutine(bm.BattleCoroutine());
            // Sync host board to client after battle
            BoardSyncManager.MarkDirty();
            // BattleCoroutine normally calls StartNewPhase. If it didn't (e.g. allSlots null), fallback:
            if (currentPhase == TurnPhase.BattlePhase)
            {
                Debug.LogWarning("[TurnManager] SafeBattle: BattleCoroutine left us in BattlePhase, forcing StartNewPhase");
                StartNewPhase();
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

            NetworkPlayer.Local.AddEnergy(energy);
            NetworkPlayer.Local.RemoveCardFromHand(selectedCard.gameObject);
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
        if (endBtn != null)
            endBtn.SetInteractable(enabled);
        else
            Debug.LogWarning("[TurnManager] SetEndButton: EndTurnButton not found in scene!");
    }
    void SetDrawButtonInteractable(bool enabled)
    {
        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null)
            drawUI.SetInteractable(enabled);
        else
            Debug.LogWarning("[TurnManager] SetDrawButtonInteractable: DrawCardUI not found in scene!");
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


    /// <summary>
    /// Process phase-start EnqueueConfirm triggers for the LOCAL player's slots (6-11).
    /// Called from SetPhaseFromNetwork(MyTurn) so each client processes its OWN cards.
    /// Server calls this for both players in order (isMyTurnFirst determines priority).
    /// </summary>
    public void ProcessPhaseStartTriggers()
    {
        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (slots == null) return;

        // 01525 铁匠（铁匠）
        for (int i = 6; i <= 11; i++)
        {
            if (slots[i]?.currentCard3D == null) continue;
            CardInstance ci = slots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || ci.templateID != "01525") continue;
            if (!ci.CanTriggerTrait("阶段开始")) continue;
            ConfirmQueueManager.Instance.EnqueueConfirm("是否对铁匠消耗手牌？",
                onYes: (done) => { StartCoroutine(IronSmithSelectCard(done)); },
                onNo: (done) => { done(); });
            break;
        }
        // 01535 执行之剑
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = slots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || ci.templateID != "01535") continue;
            ConfirmQueueManager.Instance.EnqueueConfirm("是否对执行之剑消耗法术？",
                onYes: (done) => { StartCoroutine(ExecutionSwordSelectSpell(ci, done)); },
                onNo: (done) => { ci.consumedSpellCost = 0; done(); });
            break;
        }
        // 01526 忤逆者
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = FindObjectOfType<BoardManager>()?.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance?.templateID != "01526") continue;
            CardInstance rebelCI = c3d.cardInstance;
            if (!rebelCI.CanTriggerTrait("阶段开始")) continue;
            BoardSlot rebelSlot = slot;
            Card3DInstance rebel3D = c3d;
            ConfirmQueueManager.Instance.EnqueueConfirm("是否对忤逆者消耗手牌？",
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
                onNo: (done) => { done(); });
            break;
        }
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
