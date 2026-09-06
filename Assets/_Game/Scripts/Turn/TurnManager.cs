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

    // 影舞者(01502) PhaseStart 同步
    bool _waitingForPhaseStartReady = false;
    bool _shadowsReenteredThisPhase = false;
    // 增幅结构(01506) 每阶段每半场只触发一次
    bool[] _amplifiedHalfThisPhase = new bool[2];
    public void OnRemotePhaseStartReady() { _waitingForPhaseStartReady = false; }

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
            // 等 Local + Remote 都就绪：在线等真实对手连接，离线等 OfflineAIHost 创建 AI。
            // autoCreatePlayer(Local) 和 OfflineAIHost(Remote) 都是异步的，直接发牌会踩 null。
            float deadline = Time.time + 15f;
            while ((NetworkPlayer.Local == null || NetworkPlayer.Remote == null) && Time.time < deadline)
                yield return null;

            if (NetworkPlayer.Local == null || NetworkPlayer.Remote == null)
            {
                Debug.LogError($"[TurnManager] InitialDraw 超时：Local={NetworkPlayer.Local != null}, Remote={NetworkPlayer.Remote != null}");
                yield break;
            }

            yield return StartCoroutine(ServerInitialDraw());
            Debug.Log("[TurnManager] Server initial draw complete");
            yield return StartNewPhase();
        }
        else if (!NetworkClient.isConnected && NetworkPlayer.Local != null)
        {
            // 先收集三张牌（神选者 + 2 普通），再统一从左到右依次飞入
            var newCards = new List<CardView>();
            CardData chosenOne = ChosenOneManager.Instance?.DrawChosenOne();
            if (chosenOne != null)
            {
                CardView cv = NetworkPlayer.Local.AddCardToHand(chosenOne, animate: false);
                if (cv != null) newCards.Add(cv);
            }
            for (int i = 0; i < 2; i++)
            {
                CardView cv = NetworkPlayer.Local.DrawCard(animate: false);
                if (cv != null) newCards.Add(cv);
            }

            HandManager hm = FindObjectOfType<HandManager>();
            if (hm != null && newCards.Count > 0)
                hm.StartCoroutine(hm.AnimateCardDraw(newCards));

            yield return StartNewPhase();
        }
    }

    IEnumerator ServerInitialDraw()
    {
        yield return null;

        NetworkPlayer local = NetworkPlayer.Local;
        NetworkPlayer remote = NetworkPlayer.Remote;

        Debug.Log($"[TurnManager] ServerInitialDraw: Local={local?.netId}, Remote={remote?.netId}, remoteConn={remote?.connectionToClient != null}");

        // Host draws — 先收集三张（神选者 + 2 普通），再统一从左到右依次飞入
        if (local != null)
        {
            var localNewCards = new List<CardView>();
            CardData choLocal = ChosenOneManager.Instance?.DrawChosenOne();
            if (choLocal != null)
            {
                CardView cv = local.AddCardToHand(choLocal, animate: false);
                if (cv != null) localNewCards.Add(cv);
            }
            for (int i = 0; i < 2; i++)
            {
                CardData card = DeckManager.Instance?.DrawFromMain();
                if (card != null)
                {
                    CardView cv = local.AddCardToHand(card, animate: false);
                    if (cv != null) localNewCards.Add(cv);
                }
            }
            HandManager hmLocal = FindObjectOfType<HandManager>();
            if (hmLocal != null && localNewCards.Count > 0)
                hmLocal.StartCoroutine(hmLocal.AnimateCardDraw(localNewCards));
            Debug.Log($"[TurnManager] Host local drawn: {local.handCards.Count} cards");
        }

        // Remote gets cards via TargetRpc — 先收集三张，再一次性批量发送，统一飞入
        if (remote != null)
        {
            Debug.Log($"[TurnManager] Sending 1 chosen + 2 main to Remote netId={remote.netId}");
            var tids = new List<string>();
            var iids = new List<string>();

            CardData choRemote = ChosenOneManager.Instance?.DrawChosenOne();
            if (choRemote != null)
            {
                tids.Add(choRemote.templateID);
                iids.Add(choRemote._instanceID ?? CardZoneManager.GenerateInstanceID(choRemote.templateID));
            }
            for (int i = 0; i < 2; i++)
            {
                CardData card = DeckManager.Instance?.DrawFromMain();
                if (card != null)
                {
                    tids.Add(card.templateID);
                    iids.Add(card._instanceID ?? CardZoneManager.GenerateInstanceID(card.templateID));
                }
            }

            // 服务端追踪（每张牌都要 AddServerSideCard）
            for (int i = 0; i < tids.Count; i++)
            {
                var td = CardDatabase.Instance?.GetTemplate(tids[i]);
                if (td != null) remote.AddServerSideCard(td, iids[i]);
            }

            // 真实远程玩家 → 批量 RPC 让客户端建 UI + 飞入动画。
            // AI（connectionToClient == null）→ server-only 手牌，无客户端，不发 RPC。
            if (remote.connectionToClient != null)
            {
                remote.TargetReceiveInitialCards(remote.connectionToClient, tids.ToArray(), iids.ToArray());
            }
            else
            {
                Debug.Log($"[TurnManager] AI 对手无客户端连接，{tids.Count} 张牌仅服务端追踪（无 UI）");
            }
        }
        else
        {
            Debug.LogWarning("[TurnManager] ServerInitialDraw: Remote is NULL — single player mode?");
        }
    }

    /// <summary>处理"下阶段开始退场"等阶段转换死亡。服务端和客户端都需要执行。</summary>
    public static void ProcessPhaseStartDeaths()
    {
        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (slots == null) return;

        // 先收集再处理，避免迭代中修改
        var toDie = new System.Collections.Generic.List<BoardSlot>();
        foreach (BoardSlot slot in slots)
        {
            if (slot?.currentCard3D == null) continue;
            var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01101")
                toDie.Add(slot);
        }
        foreach (var s in toDie)
            s.HandleDeath(s.currentCard3D);

        // 清理本阶段标记（双方都要）
        foreach (BoardSlot slot in slots)
        {
            if (slot?.currentCard3D == null) continue;
            var c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance == null) continue;
            var ci = c3d.cardInstance;
            if (ci.templateID == "01531")
                ci._outlawPlayerDamageThisTurn = false;
            bool wasSilenced = ci.silencedThisPhase;
            bool wasPoisoned = ci.poisoned;
            ci.silencedThisPhase = false;
            ci.ApplySilenceToTraits(); // 阶段边界：特性组解除沉默（UnblockAll）→ 翻转时自刷显示（图标恢复）
            ci.poisoned = false;
            if (wasSilenced) ci.RemoveStatusBySource("03501"); // 4.2 神官阶段沉默到期
            if (wasPoisoned)
            {
                ci.RemoveStatusBySource("03502"); // 4.2 毒巫阶段中毒到期
                ci.RefreshDisplay(); // 6.x：中毒图标消失
            }
            ci.enemyDamageSourceIDs.Clear();
            ci.damageSourceInstanceIDs.Clear();
            ci.ironSmithOneCostConsumedCount = 0;
            _ironSmithPromptedPhase = false; // 每阶段复位：铁匠确认提示每阶段只入队一次（防 ProcessPhaseStartTriggers 重复调用弹两次）
            // 5.x 先手重装（整侧扫描）：凡本回合被消耗(_firstStrikeConsumed=true)的先手单位一律清零，覆盖白名单外
            // 远端消耗单位（03012/01519/01318/03502 等）；host/AI 服务端本地消耗也在本循环作用域内。
            if (ci._firstStrikeConsumed)
                ci._firstStrikeConsumed = false;
            if (ci.templateID == "01511")
            {
                ci.mindScholarTriggeredKeys?.Clear();
                ci._mindScholarCopyPrompted = false;
            }
        }
        // 4.2 光环受害者状态：阶段边界全板重算（消化 被完全沉默/新对位卡/源退场 的自愈）
        GlobalEventManager.Instance?.RefreshAuraStatusesForBoard();
    }

    /// <summary>深海恶物(01338)：每阶段开始扣1生命值。只在 StartNewPhase 中调用，确保每阶段仅服务端执行一次。</summary>
    static void DeepSeaPhaseStartDamage()
    {
        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (slots == null) return;

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
                    // 格子伤害实际来源=格子本身：不归因击杀（不把 01338 塞进 damageSourceInstanceIDs），
                    // 溯源显示用槽位 deepSeaSourceInstanceID 即可
                    DamagePipeline.ShowFloaterAt(ci, 1, FloaterType.Damage, null, i); // 格子伤害特殊轨迹（深海每阶段扣血）
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
        }
        BoardSlot.CheckAndHandleDeaths();
    }

    public IEnumerator StartNewPhase()
    {
        ProcessPhaseStartDeaths();
        _amplifiedHalfThisPhase[0] = false;
        _amplifiedHalfThisPhase[1] = false;

        currentPhase = TurnPhase.PhaseStart;
        phaseCount++;
        // Single-player flips each phase. Online: swap happens in EndCurrentTurn (battle transition).
        if (!NetworkServer.active)
        {
            isMyTurnFirst = !isMyTurnFirst;
        }

        string firstPlayer = isMyTurnFirst ? "Me" : "Enemy";
        Debug.Log(string.Format("\n========== Phase {0} Start, {1} First ==========", phaseCount, firstPlayer));

        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();

        if (CardInstance.shadowMasterAlive)
        {
            BoardSlot bs = FindObjectOfType<BoardSlot>();
            if (bs != null)
                yield return bs.SummonAllShadows();
        }

        // ── 增幅结构(01506)：在 PhaseStart 广播之前执行 ——
        // 先对服务器侧双半场应用 +1+1，MarkDirty 推给远程后，远程再处理 PhaseStart 回报
        // 时才不会用旧值覆盖服务器已 buff 的数值
        if (slots != null)
        {
            for (int half = 0; half <= 6; half += 6)
            {
                int mechCount = 0;
                for (int i = half; i < half + 6; i++)
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
                    for (int i = half; i < half + 6; i++)
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
                        if (_amplifiedHalfThisPhase[half == 6 ? 1 : 0]) continue;
                        _amplifiedHalfThisPhase[half == 6 ? 1 : 0] = true;
                        for (int i = half; i < half + 6; i++)
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
                                    s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                }
                            }
                        }
                    }
                }
            }
        }

        // ── 追随者(03001)：在 MarkDirty 之前执行 ——
        // 每阶段开始为宿主+1攻击力，须在 SyncNow 之前 buff 否则远程收不到
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

        // ── 联机：先 MarkDirty 推 buff 后数据给远程，再广播 PhaseStart ——
        // 否则远程的 CmdReportAllSlots 会用旧数据覆盖服务器已完成的 buff
        if (NetworkServer.active)
        {
            BoardSyncManager.MarkDirty();
            yield return null; // LateUpdate → SyncNow → remote gets buffed stats
            ProcessPhaseStartTriggers();
            // 仅在需远程交互时才等待——否则发 PhaseStart 后直接继续
            if (CardInstance.shadowMasterAlive)
            {
                _waitingForPhaseStartReady = true;
                BroadcastTurnPhase(TurnPhase.PhaseStart);
                yield return null;
                float deadline = Time.time + 20f;
                yield return new WaitUntil(() => !_waitingForPhaseStartReady || Time.time > deadline);
                if (Time.time > deadline)
                    Debug.LogWarning("[TurnManager] PhaseStart ready timeout — proceeding without remote ack");
            }
            else
            {
                BroadcastTurnPhase(TurnPhase.PhaseStart);
            }
        }
        // 聚光灯：每阶段开始恢复2生命值 — 双方都要检查
        if (slots != null)
        {
            for (int i = 0; i < 12; i++)
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
                    // 自伤来源：补自身 instanceID（非敌方，不进 enemyDamageSourceIDs）
                    if (ci.damageSourceInstanceIDs == null) ci.damageSourceInstanceIDs = new System.Collections.Generic.List<string>();
                    if (!ci.damageSourceInstanceIDs.Contains(ci.instanceID))
                        ci.damageSourceInstanceIDs.Add(ci.instanceID);
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    ci.overclocked = false;
                    ci.RemoveStatusBySource("02215"); // 4.2 超频：buff+自伤预告 到期整源清除
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

        // 打工人(03009)/小团恶念(03010)/大团恶念(03011) — 双方都要检查
        if (slots != null)
        {
            for (int i = 0; i < 12; i++)
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
        if (slots != null)
        {
            for (int i = 0; i < 12; i++)
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
                        Card3DInstance.PlaySummonOn(model); // 召唤动画
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
        // 03010→03011 进化后同步板面——远程需看到大团恶念模型
        BoardSyncManager.MarkDirty();
        {
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = slots[i];
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "03011" && !ci._justTransformed)
                {
                    slot.HandleDeath(slot.currentCard3D);
                    BoardManager.GetOwnerPlayer(i)?.AddEnergy(5);
                    StartCoroutine(SummonSmallEvilOnSlot());
                    break;
                }
            }
        }
        if (slots != null)
        {
            for (int i = 0; i < 12; i++)
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
                Debug.Log("[TurnManager] Phase start: Host turn (MyTurn)");
            }
            else
            {
                currentPhase = TurnPhase.EnemyTurn;
                SetPlayerActionsEnabled(false);
                Debug.Log("[TurnManager] Phase start: Remote turn first (EnemyTurn from host view)");
                // 离线 AI 先手：给 AI 加能量并让其行动（AI 无客户端，需服务器侧补能量）
                if (IsOfflineAI())
                {
                    NetworkPlayer.Remote?.AddEnergy(6);
                    StartCoroutine(AutoEndEnemyTurn());
                }
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
                for (int i = 0; i < 12; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D != null)
                    {
                        CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && !ci.isAttached && i >= 6)
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
            // 先更新 currentPhase 再广播：Host 下 BroadcastTurnPhase 同步回调 SetPhaseFromNetwork，
            // 若 currentPhase 未更新会重入 EnableMyTurnActions → 双重触发回合开始效果/弹窗。
            currentPhase = newPhase;
            BroadcastTurnPhase(newPhase);
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
                // 离线 AI 对局：给 AI 加能量并让其行动
                if (IsOfflineAI())
                {
                    NetworkPlayer.Remote?.AddEnergy(6);
                    StartCoroutine(AutoEndEnemyTurn());
                }
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
            // Sync host board to client after battle——必须在 BroadcastTurnPhase 之前执行
            BoardSyncManager.MarkDirty();
            yield return null; // 让 LateUpdate 中的 SyncNow 执行，确保远端收到恢复后的 currentAttack
            yield return StartNewPhase();    // 广播阶段变化（SetPhaseFromNetwork → 客户端开始处理新阶段）
            // 让一帧给客户端处理 SetPhaseFromNetwork（含 ProcessPhaseStartTriggers + ReportAllSlots），
            // 避免客户端上报的旧 HP 竞态覆盖服务器即将执行的深海扣血
            yield return null;
            // 深海恶物(01338)：每阶段开始扣1生命值
            DeepSeaPhaseStartDamage();
            BoardSyncManager.MarkDirty();
            yield return null; // 让 LateUpdate 中的 SyncNow 把扣血后的 HP 同步给客户端
            // 处理扣血引发的死亡 + 反击
            if (bm != null)
                yield return StartCoroutine(BattleManager.WaitForSimultaneousWindow());
        }
        else
        {
            // 单机模式：执行战斗并推进阶段
            BattleManager bm = BattleManager.Instance;
            if (bm != null)
                yield return StartCoroutine(bm.BattleCoroutine());
            yield return StartNewPhase();
        }
    }
    void TriggerMyTurnStartEffects()
    {
        BoardSlot[] slots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (slots == null) return;

        // 滋养者(01129)自愈 — 双方都要检查
        for (int i = 0; i < 12; i++)
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
        // 心灵学者(01511)回合开始退场+2能量 — 双方各自检查自己半场
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot msSlot = slots[i];
            if (msSlot?.currentCard3D == null) continue;
            CardInstance msCI = msSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (msCI != null && msCI.templateID == "01511")
            {
                msCI.isActiveExit = false;
                msSlot.HandleDeath(msSlot.currentCard3D);
                BoardManager.GetOwnerPlayer(i)?.AddEnergy(2);
                break;
            }
        }
        // 滋养者(01129)附着宿主回血 — 双方都要检查
        for (int i = 0; i < 12; i++)
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
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = bmHeal.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance?.templateID == "01302")
                {
                    if (!c3d.cardInstance.CanTriggerTrait("回合开始")) continue;
                    int ownHalfStart = i >= 6 ? 6 : 0;
                    int myRow = (i - ownHalfStart) < 3 ? 0 : 3;
                    int rowStart = ownHalfStart + myRow;
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
    static bool _ironSmithPromptedPhase;   // 每阶段铁匠确认提示是否已入队（阶段边界复位；static 因复位在 static 方法内）
    static bool _smithCompleteNotified;    // IronSmithSelectCard 是否已回调完成（防 done 双触发致队列多跑一轮）

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
            if (!_smithCompleteNotified) { _smithCompleteNotified = true; onComplete(); }
            yield break;
        }
        _smithCompleteNotified = false;

        bool done = false;
        bool onlyOneCost = false; // 进入"继续消耗1费"后，本阶段后续只能选基础费用=1
        while (!done)
        {
            ConfirmQueueManager.EnterSelectionMode();
            System.Func<CardInstance, bool> costOk = ci =>
            {
                var td = CardDatabase.Instance?.GetTemplate(ci.templateID);
                if (td == null || td.cardType != CardType.Summon) return false;
                if (onlyOneCost) return td.baseCost == 1; // 续选阶段只列1费
                return td.baseCost == 1 || td.baseCost == 3 || td.baseCost == 5;
            };
            HandManager ironHm = FindObjectOfType<HandManager>();
            List<CardInstance> candidates = ironHm != null ? ironHm.BuildHandCardList(costOk) : new List<CardInstance>();
            if (candidates.Count == 0)
            {
                ConfirmQueueManager.ExitSelectionMode();
                break;
            }

            CardInstance selectedCard = null;
            bool selectionDone = false;
            CardDisplayPanel.Instance.multiSelect = false;
            CardDisplayPanel.Instance.ShowWithCallback(candidates, costOk, () => selectionDone = true, "消耗");
            float iscDeadline = Time.time + 30f;
            while (!selectionDone && Time.time < iscDeadline) yield return null;
            if (!selectionDone)
            {
                ConfirmQueueManager.ExitSelectionMode();
                ironHm?.EndHandSelectionCleanup();
                break;
            }
            CardInstance chosen = CardDisplayPanel.Instance.GetSelectedCard();
            selectedCard = chosen != null
                ? ironHm?.ResolveHandCardByInstanceID(chosen.instanceID)?.GetComponent<CardInstance>()
                : null;
            ironHm?.EndHandSelectionCleanup();
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
                yield return StartCoroutine(StrengthenSlot(ironSmithInst));
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
                    if (continueSelect) onlyOneCost = true; // 继续 → 之后只能再选1费
                    else done = true;
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
        if (!_smithCompleteNotified) { _smithCompleteNotified = true; onComplete(); }
    }
    IEnumerator StrengthenSlot(CardInstance source)
    {
        BoardSlot.isStrengtheningSlot = true;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
        {
            if (target != null && !target.isBlocked)
            {
                target.slotTempAttackBoost += 2;
                // 4.3 铁匠强化记施加者（来源=触发强化的熔能铁匠 01525 实例）
                target.slotTempAttackBoostSourceInstanceID = source?.instanceID ?? "";
                if (target.currentCard3D != null)
                {
                    Card3DInstance c3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance != null && !c3d.cardInstance.isXValue)
                    {
                        c3d.cardInstance.currentAttack += 2;
                        // 4.2 熔能铁匠：给强化格子当前卡记状态（换卡时随 setter 转移）
                        c3d.cardInstance.AddStatus(false, "攻击力临时+2", "01525");
                        c3d.UpdateValues();
                    }
                }
            }
            BoardSlot.isStrengtheningSlot = false;
            ConfirmQueueManager.ExitSelectionMode();
            TurnManager.SyncMyBoardToOpponent();
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

            if (NetworkClient.isConnected && !NetworkServer.active)
            {
                // 纯客户端：委托服务器权威执行（参考 01347/01316 远程委托模式）
                int serverSlot = rebelSlot.slotID >= 6 ? rebelSlot.slotID - 6 : rebelSlot.slotID + 6;
                BoardSlot._rebelConsumeDone = false;
                NetworkPlayer.Local?.CmdRebelConsumeHand(serverSlot, selectedCard.instanceID, healAmount);
                yield return new WaitUntil(() => BoardSlot._rebelConsumeDone);
                // 客户端本地也刷新——SyncNow 后续会覆盖为权威值
                NetworkPlayer.Local.RemoveCardFromHand(selectedCard.gameObject);
                Destroy(selectedCard.gameObject);
            }
            else
            {
                NetworkPlayer.Local.Heal(healAmount);
                rebelCI.currentHealth = Mathf.Min(rebelCI.currentMaxHealth, rebelCI.currentHealth + healAmount);
                rebel3D.UpdateValues();

                NetworkPlayer.Local.RemoveCardFromHand(selectedCard.gameObject);
                Destroy(selectedCard.gameObject);
            }
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
    /// AI 回合：让 AI（NetworkPlayer.Remote）行动后结束回合。
    /// SimpleAI 未就绪时回退到延迟占位（保证回合能推进）。
    /// </summary>
    IEnumerator AutoEndEnemyTurn()
    {
        // AI 回合开始：先处理 AI 半场的阶段开始触发器（铁匠/执行之剑/忤逆者自动处理）
        ProcessAIPhaseStartTriggers();

        // AI 行动：抽牌 + 出牌（SimpleAI.EvaluateAndPlay 内部会 ServerEndTurn 结束回合）
        if (SimpleAI.Instance != null)
            yield return SimpleAI.Instance.EvaluateAndPlay();
        else
        {
            yield return new WaitForSeconds(1f);
            if (NetworkPlayer.Remote != null)
                ServerEndTurn(NetworkPlayer.Remote);
        }
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

        // 01525 铁匠（铁匠）——每阶段只入队一次（防 ProcessPhaseStartTriggers 重复调用时同阶段弹两次）
        if (!_ironSmithPromptedPhase)
        {
            for (int i = 6; i <= 11; i++)
            {
                if (slots[i]?.currentCard3D == null) continue;
                CardInstance ci = slots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci == null || ci.templateID != "01525") continue;
                if (!ci.CanTriggerTrait("阶段开始")) continue;
                _ironSmithPromptedPhase = true;
                ConfirmQueueManager.Instance.EnqueueConfirm("是否对铁匠消耗手牌？",
                    onYes: (done) => { StartCoroutine(IronSmithSelectCard(done)); },
                    onNo: (done) => { done(); });
                break;
            }
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

    /// <summary>
    /// AI 半场（0-5）的阶段开始触发器。离线 AI 对局中，AI 的铁匠/执行之剑/忤逆者
    /// 在 AI 回合开始自动处理（无 UI，自动选第一个合法手牌）。
    /// </summary>
    public void ProcessAIPhaseStartTriggers()
    {
        if (!IsOfflineAI()) return;
        NetworkPlayer ai = NetworkPlayer.Remote;
        if (ai == null) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        // 01525 铁匠：循环消耗 1/3/5 费召唤物换能量
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || ci.templateID != "01525") continue;
            if (!ci.CanTriggerTrait("阶段开始")) continue;
            AIIronSmithConsume(ai, ci);
            break;
        }
        // 01535 执行之剑：消耗一个法术
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || ci.templateID != "01535") continue;
            AIExecutionSwordConsume(ai, ci);
            break;
        }
        // 01526 忤逆者：消耗一个召唤物回血
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || ci.templateID != "01526") continue;
            if (!ci.CanTriggerTrait("阶段开始")) continue;
            AIRebelConsume(ai, ci, slot);
            break;
        }
    }

    /// <summary>AI 铁匠：循环消耗 1/3/5 费召唤物换能量（0/2/4）。</summary>
    void AIIronSmithConsume(NetworkPlayer ai, CardInstance ironSmith)
    {
        bool consumed = true;
        while (consumed)
        {
            consumed = false;
            GameObject target = null;
            CardInstance targetCI = null;
            foreach (GameObject card in ai.handCards)
            {
                if (card == null) continue;
                CardInstance c = card.GetComponent<CardInstance>();
                if (c == null) continue;
                CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
                if (td == null || td.cardType != CardType.Summon) continue;
                if (td.baseCost != 1 && td.baseCost != 3 && td.baseCost != 5) continue;
                target = card;
                targetCI = c;
                break;
            }
            if (target == null || targetCI == null) break;

            int cost = targetCI.currentCost;
            int energy = cost switch { 1 => 0, 3 => 2, 5 => 4, _ => 0 };
            ai.AddEnergy(energy);
            ai.handCards.Remove(target);
            Destroy(target);
            ironSmith.ironSmithTotalConsumedCount++;
            consumed = true;
        }
    }

    /// <summary>AI 执行之剑：消耗一个法术，记录 consumedSpellCost。</summary>
    void AIExecutionSwordConsume(NetworkPlayer ai, CardInstance sword)
    {
        GameObject target = null;
        CardInstance targetCI = null;
        foreach (GameObject card in ai.handCards)
        {
            if (card == null) continue;
            CardInstance c = card.GetComponent<CardInstance>();
            if (c == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
            if (td == null || td.cardType != CardType.Spell) continue;
            target = card;
            targetCI = c;
            break;
        }
        if (target == null || targetCI == null) { sword.consumedSpellCost = 0; return; }

        CardData sTD = CardDatabase.Instance?.GetTemplate(targetCI.templateID);
        sword.consumedSpellCost = sTD?.baseCost ?? 0;
        ai.handCards.Remove(target);
        Destroy(target);
    }

    /// <summary>AI 忤逆者：消耗一个召唤物回血（tier + 渊?1:0）。</summary>
    void AIRebelConsume(NetworkPlayer ai, CardInstance rebel, BoardSlot rebelSlot)
    {
        GameObject target = null;
        CardInstance targetCI = null;
        foreach (GameObject card in ai.handCards)
        {
            if (card == null) continue;
            CardInstance c = card.GetComponent<CardInstance>();
            if (c == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
            if (td == null || td.cardType != CardType.Summon) continue;
            target = card;
            targetCI = c;
            break;
        }
        if (target == null || targetCI == null) return;

        int tier = targetCI.currentTier;
        bool isYuan = targetCI.prefixes.Contains("渊");
        int heal = tier + (isYuan ? 1 : 0);
        ai.Heal(heal);
        rebel.currentHealth = Mathf.Min(rebel.currentMaxHealth, rebel.currentHealth + heal);
        rebelSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();

        ai.handCards.Remove(target);
        Destroy(target);
    }

    /// <summary>是否离线 AI 对局：Host 模式下 Remote 是 AI（无客户端连接，connectionToClient == null）。</summary>
    bool IsOfflineAI()
    {
        return NetworkServer.active
            && NetworkPlayer.Remote != null
            && NetworkPlayer.Remote.connectionToClient == null;
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
            card2.GetComponent<Card3DAnimator>()?.UpdateBaseLocalPos();
            slot1.SetCard(card2);
        }
        if (card1 != null)
        {
            if (!slot2.CanPlaceCard(card1.GetComponent<Card3DInstance>()?.cardInstance)) return;
            card1.transform.position = pos2;
            card1.GetComponent<Card3DAnimator>()?.UpdateBaseLocalPos();
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
            BoardManager.SyncAttachedModels(slot1);
            BoardManager.SyncAttachedModels(slot2);
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
                if (NetworkClient.isConnected && !NetworkServer.active)
                    NetworkPlayer.Local?.CmdSetSwordCost(sword.instanceID, td.baseCost);
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
