using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using TMPro;

public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer Local { get; private set; }
    public static NetworkPlayer Remote { get; private set; }

    [Header("Player Stats")]
    public int maxHealth = 20;
    public int maxEnergy = 15;
    public int maxHandSize = 20;

    [SyncVar(hook = nameof(OnHealthChanged))]
    public int currentHealth;

    [SyncVar(hook = nameof(OnEnergyChanged))]
    public int currentEnergy;

    [SyncVar(hook = nameof(OnHandCountChanged))]
    public int handCardCount;

    [SyncVar]
    public bool isReady;

    public bool _energyCanExceedLimit;

    [Header("Hand")]
    public Transform handArea;
    public HandManager handManager;
    public GameObject cardPrefab2D;
    public GameObject spellCardPrefab2D;
    public List<GameObject> handCards = new List<GameObject>();

    TextMeshProUGUI _healthText;
    TextMeshProUGUI _energyText;

    // ========== Heartbeat ==========

    static System.Collections.Generic.Dictionary<int, float> s_lastHeartbeat = new System.Collections.Generic.Dictionary<int, float>();
    const float HEARTBEAT_INTERVAL = 3f;
    const float HEARTBEAT_TIMEOUT = 8f;
    static bool s_heartbeatRunning;

    [Command]
    void CmdHeartbeat()
    {
        s_lastHeartbeat[connectionToClient.connectionId] = Time.time;
    }

    void StartHeartbeat()
    {
        if (s_heartbeatRunning) return;
        s_heartbeatRunning = true;
        if (isLocalPlayer) StartCoroutine(HeartbeatClientLoop());
        if (isServer) StartCoroutine(HeartbeatServerLoop());
    }

    IEnumerator HeartbeatClientLoop()
    {
        while (NetworkClient.isConnected)
        {
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);
            if (!NetworkClient.isConnected) break;
            CmdHeartbeat();
        }
    }

    IEnumerator HeartbeatServerLoop()
    {
        while (NetworkServer.active)
        {
            yield return new WaitForSeconds(1f);
            float now = Time.time;
            foreach (var kv in NetworkServer.connections)
            {
                if (s_lastHeartbeat.TryGetValue(kv.Key, out float last) && now - last > HEARTBEAT_TIMEOUT)
                {
                    Debug.LogWarning($"[NetworkPlayer] Heartbeat timeout for connId={kv.Key}, disconnecting");
                    kv.Value.Disconnect();
                }
            }
        }
    }

    void OnDisable()
    {
        if (isServer)
        {
            var conn = connectionToClient;
            if (conn != null) s_lastHeartbeat.Remove(conn.connectionId);
        }
    }

    // ========== Mirror Lifecycle ==========

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"OnStartLocalPlayer: netId={netId}, isServer={isServer}");
        Local = this;
        currentHealth = maxHealth;
        currentEnergy = 0;
        _energyCanExceedLimit = false;
        handArea = GameObject.Find("HandArea")?.transform;
        handManager = FindObjectOfType<HandManager>();
        // 用 Player 的场景绑定（准确），不用 GameObject.Find("Health")（场景有两个同名对象会找错）
        if (Player.Instance != null)
        {
            _healthText = Player.Instance.healthText;
            _energyText = Player.Instance.energyText;
        }
        else
        {
            _healthText = FindTMP("Health");
            _energyText = FindTMP("Energy");
        }
        RefreshUI();
        StartHeartbeat();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"[NetworkPlayer] OnStartServer: netId={netId}, isLocalPlayer={isLocalPlayer}");

        currentHealth = maxHealth;
        currentEnergy = 0;

        // Only the local player finds Remote, to avoid race conditions.
        if (isLocalPlayer)
        {
            TrySetRemote();
            if (Remote == null)
                StartCoroutine(DelayedSetRemote());
        }
        StartHeartbeat();
    }

    void TrySetRemote()
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            var player = conn.identity?.GetComponent<NetworkPlayer>();
            if (player != null && player != this)
            {
                Remote = player;
                Debug.Log($"[NetworkPlayer] Remote set: netId={Remote.netId}");
                return;
            }
        }
    }

    IEnumerator DelayedSetRemote()
    {
        float waited = 0f;
        while (waited < 300f && Remote == null)
        {
            yield return new WaitForSeconds(0.2f);
            waited += 0.2f;
            TrySetRemote();
        }
        if (Remote == null)
            Debug.LogError("[NetworkPlayer] Failed to find Remote after 300s!");
    }

    public override void OnStartClient()
    {
        if (isLocalPlayer) return;

        // Non-local player = enemy. Cache as Remote for easy access.
        if (Remote == null)
        {
            Remote = this;
            Debug.Log($"[NetworkPlayer] OnStartClient: Remote set to netId={netId}");
        }
        // UI-only references — only used on the actual client for enemy health/energy display.
        // Do NOT set handArea/handManager for Remote — server-side draws for Remote must
        // use TargetReceiveCard RPC, never local Instantiate (which would land on EnemyHandArea).
        _healthText = FindTMP("EnemyHealthLabel");
        _energyText = FindTMP("EnemyEnergyLabel");
        RefreshUI();
    }

    /// <summary>
    /// Network-safe card draw for a player. On the server, draws for Remote use
    /// TargetReceiveCard RPC; draws for Local use the local Instantiate path.
    /// </summary>
    public static void DrawCardForPlayer(NetworkPlayer player)
    {
        if (player == null) return;
        if (NetworkServer.active && player != Local)
        {
            CardData data = DeckManager.Instance?.DrawFromMain();
            if (data != null)
            {
                string iid = data._instanceID ?? "";
                player.TargetReceiveCard(player.connectionToClient, data.templateID, iid);
                player.AddServerSideCard(data, iid);
            }
        }
        else
        {
            player.DrawCardWithoutLimit();
        }
    }

    /// <summary>
    /// Network-safe AddCardToHand for a player. On the server, adds for Remote use
    /// TargetReceiveCard RPC; adds for Local use the local path.
    /// </summary>
    public static void AddCardToHandForPlayer(NetworkPlayer player, CardData data, string oldInstanceID = null)
    {
        if (player == null || data == null) return;
        if (NetworkServer.active && player != Local)
        {
            string iid = data._instanceID ?? "";
            player.TargetReceiveCard(player.connectionToClient, data.templateID, iid);
            player.AddServerSideCard(data, iid);
        }
        else
        {
            if (!string.IsNullOrEmpty(oldInstanceID))
                player.AddCardToHandFromInstance(data, null, false);
            else
                player.AddCardToHand(data);
        }
    }

    TextMeshProUGUI FindTMP(string name)
    {
        var t = GameObject.Find(name)?.GetComponent<TextMeshProUGUI>();
        if (t == null) t = GameObject.Find(name + " ")?.GetComponent<TextMeshProUGUI>();
        if (t == null) Debug.LogWarning($"[NetworkPlayer] FindTMP({name}) failed");
        return t;
    }

    Transform FindTransform(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) go = GameObject.Find(name + " ");
        return go?.transform;
    }

    // ========== UI ==========

    void RefreshUI()
    {
        if (isServer && !isClient) return;
        if (_healthText != null) _healthText.text = isLocalPlayer ? $" {currentHealth}" : currentHealth.ToString();
        if (_energyText != null) _energyText.text = isLocalPlayer ? $" {currentEnergy}/{maxEnergy}" : $"{currentEnergy}/{maxEnergy}";
    }

    // ========== Debug UI ==========

    void OnGUI()
    {
        if (!isLocalPlayer) return;
        GUI.Label(new Rect(10, 10, 400, 30),
            $"Server active: {NetworkServer.active}, connections: {NetworkServer.connections.Count}");
        GUI.Label(new Rect(10, 40, 400, 30),
            $"Client active: {NetworkClient.active}, connected: {NetworkClient.isConnected}");
        GUI.Label(new Rect(10, 70, 400, 30),
            $"isLocalPlayer: {isLocalPlayer}, handCards: {handCards.Count}");
    }

    // ========== SyncVar Hooks ==========

    void OnHandCountChanged(int oldValue, int newValue)
    {
        Debug.Log($"[NetworkPlayer] Hand count: {oldValue} -> {newValue}, isLocal={isLocalPlayer}");
    }

    void OnHealthChanged(int oldValue, int newValue)
    {
        Debug.Log($"[NetworkPlayer] Health: {oldValue} -> {newValue}, isLocal={isLocalPlayer}, netId={netId}");
        RefreshUI();
        if (newValue <= 0 && isServer)
            Debug.Log("[NetworkPlayer] Player died");
    }

    void OnEnergyChanged(int oldValue, int newValue)
    {
        Debug.Log($"[NetworkPlayer] Energy: {oldValue} -> {newValue}, isLocal={isLocalPlayer}, netId={netId}");
        RefreshUI();
    }

    // ========== ClientRpc ==========

    [ClientRpc]
    public void RpcStartTurn(int energyGain)
    {
        if (!isLocalPlayer) return;
        Debug.Log($"[NetworkPlayer] RpcStartTurn: gaining {energyGain} energy");
        AddEnergy(energyGain);
        FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();

        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm != null)
            tm.SetPhaseFromNetwork(TurnManager.TurnPhase.MyTurn);
    }

    // ========== Commands ==========

    [Command]
    public void CmdRequestDraw()
    {
        Debug.Log($"[NetworkPlayer] CmdRequestDraw from netId={netId}");
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm == null) return;

        if (!IsMyTurnOnServer(tm)) return;

        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null && drawUI.GetRemainingDraws() <= 0) return;

        if (currentEnergy < 1) return;

        CardData data = DeckManager.Instance?.DrawFromMain();
        if (data == null) { Debug.Log("[NetworkPlayer] CmdRequestDraw: deck empty"); return; }

        currentEnergy -= 1;
        string iid = data._instanceID ?? "";
        TargetReceiveCard(connectionToClient, data.templateID, iid);
        TargetConfirmDraw(connectionToClient);

        // Server-side tracking: add a lightweight card so CmdPlayCard can find it
        AddServerSideCard(data, iid);
    }

    /// <summary>
    /// Server-side card placement. overrideAtk/overrideHP/overrideMaxHP (-1 = use template default)
    /// are applied after InitFromTemplate so enter-effect stat boosts survive the server's fresh spawn.
    /// </summary>
    [Command]
    public void CmdPlayCard(string templateID, int slotID, int overrideAtk, int overrideHP, int overrideMaxHP, int overrideCost, string instanceID)
    {
        Debug.Log($"[NetworkPlayer] CmdPlayCard: templateID={templateID}, slotID={slotID}, cost={overrideCost}, netId={netId}, isLocal={this==NetworkPlayer.Local}");
        // slotID 仅允许 -1（手牌消耗通知）、0-11（板面放置）。其他值为非法输入，拒绝。
        if (slotID < -1 || slotID >= 12) return;
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm == null) return;
        if (!IsMyTurnOnServer(tm)) return;

        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template == null) return;

        // 附着专用卡（baseHealth==0 && canAttach）不应作为独立槽位模型 — 防同步竞态产生幻影
        if (template.cardType == CardType.Summon && template.canAttach && template.baseHealth == 0)
        {
            Debug.LogWarning($"[CmdPlayCard] 拒绝为附着专用卡 {templateID} 建独立槽位模型");
            BoardSyncManager.MarkDirty();
            return;
        }

        if (template.cardType == CardType.Summon)
        {
            // slotID=-1 是手牌消耗通知，不创建板面模型
            if (slotID < 0) return;
            if (this != NetworkPlayer.Local)
            {
                // Remote's card — spawn on server for BattleCoroutine. Host=server, no TargetRpc needed.
                // Mirror remote's local slot to server slot: remote 6-11→server 0-5, remote 0-5→server 6-11
                int enemySlot = slotID >= 6 ? slotID - 6 : slotID + 6;
                if (template.prefab3D != null)
                {
                    BoardManager bm = FindObjectOfType<BoardManager>();
                    BoardSlot slot = bm?.GetSlot(enemySlot);
                    if (slot != null)
                    {
                        if (slot.currentCard3D != null)
                        {
                            // 清理重定向标记—确保狼替换时王者(01504)得到特性2加成
                            CardInstance oldCI = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (oldCI != null && oldCI.templateID == "03006")
                            {
                                // 远程方在服务器 0-5，王者也在 0-5
                                for (int ki = 0; ki <= 5; ki++)
                                {
                                    var ks = bm.GetSlot(ki);
                                    var kci = ks?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (kci != null && kci.templateID == "01504"
                                        && !(GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(kci)))
                                    {
                                        kci.currentHealth += 1;
                                        kci.currentMaxHealth += 1;
                                        kci.currentAttack += 1;
                                        break;
                                    }
                                }
                            }
                            Destroy(slot.currentCard3D);
                            slot.SetCard(null);
                        }
                        Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(enemySlot);
                        GameObject model = Instantiate(template.prefab3D, pos, Quaternion.Euler(0, 180, 0));
                        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
                        if (c3d != null)
                        {
                            CardInstance ci = model.AddComponent<CardInstance>();
                            ci.InitFromTemplate(template, 0, instanceID);
                            if (templateID == "01502") CardInstance.shadowMasterAlive = true;
                            // 01515 狂热萨满 / 01520 商户 — 光环需在服务器侧注册
                            if (templateID == "01515") GlobalEventManager.Instance?.RegisterAura(new FanaticShamanAura { source = ci });
                            if (templateID == "01520") GlobalEventManager.Instance?.RegisterAura(new MerchantAura { source = ci });
                            if (templateID == "01533") GlobalEventManager.Instance?.RegisterAura(new ScarletSaintAura { source = ci });
                            if (overrideAtk >= 0) ci.currentAttack = overrideAtk;
                            if (overrideHP >= 0) ci.currentHealth = overrideHP;
                            if (overrideMaxHP >= 0) ci.currentMaxHealth = overrideMaxHP;
                            if (overrideCost >= 0) ci.currentCost = overrideCost;
                            // 影子(03007)：overrideAtk 已含 bonus（客户端传入），不再重复加 currentAttack
                            // baseAttack 需单独补上（overrideAtk 不影响 base）
                            if (templateID == "03007")
                            {
                                ci.isShadow = true;
                                ci.baseAttack += CardInstance.shadowAtkBonus;
                                ci.currentTier += CardInstance.shadowTierBonus;
                                ci.baseTier += CardInstance.shadowTierBonus;
                            }

                            // 01309: 进场护盾（攻击回合开始消失）
                            if (ci.templateID == "01309") ci.GrantShield(false, true, false);

                            c3d.cardInstance = ci;
                            c3d.UpdateValues();

                            // Compute x-value stats from visible board (server perspective)
                            if (ci.isXValue)
                            {
                                HandManager hmX = FindObjectOfType<HandManager>();
                                if (hmX != null) hmX.UpdateXValues(ci);
                                c3d.UpdateValues();
                            }
                        }
                        slot.SetCard(model);
                        // 非Token卡 → 玩家放置，禁止过期SyncNow覆盖
                        var placedCI = model.GetComponent<Card3DInstance>()?.cardInstance;
                        if (placedCI != null && !placedCI.templateID.StartsWith("03")) placedCI._hadEnterEffect = true;
                    }
                }
            }
            else
            {
                // Host's card — broadcast to other client so they see the opponent model
                NetworkConnectionToClient other = null;
                foreach (var kv in NetworkServer.connections)
                    if (kv.Value != connectionToClient) { other = kv.Value; break; }
                if (other != null)
                    TargetSpawnCard3D(other, templateID, slotID, overrideAtk, overrideHP, overrideMaxHP, overrideCost, instanceID);
            }
        }
        // Non-counter cards: trigger opponent's OnCardPlayed counters on server.
        // Host's own placement already checks in OnPointerClick before enter effect runs.
        // Here we only need to handle the Remote→Host counter direction.
        if ((template.spellType & SpellType.Counter) == 0 && this != NetworkPlayer.Local)
        {
            CounterManager.Instance?.ServerCheckOnCardPlayed(template, false);

            // 蛊惑之音(02304): if Host's counter redirected this Remote card's enter effect,
            // tell Host client to select an ally and run the redirected enter effect.
            if (GlobalEventManager.Instance != null &&
                GlobalEventManager.Instance.PendingEnterRedirectTemplate == template)
            {
                GlobalEventManager.Instance.PendingEnterRedirectTemplate = null;
                TargetHandleEnterRedirect(NetworkPlayer.Local.connectionToClient, templateID);
            }
        }

        // Always sync after placement so the other side sees the new model
        if (template.cardType == CardType.Summon)
            BoardSyncManager.MarkDirty();

        // Counter spell sync is handled entirely by CardDrag.OnEndDrag's counter branch
        // (TargetSpawnCounterCard for Host→Remote, CmdPlayCounter for Client→Server).
        // CmdPlayCard is NEVER called for counters — the counter path has an early return.
    }

    /// <summary>
    /// Client → server: update a card's stats after enter effect modified them locally.
    /// Used when CmdPlayCard was called before the enter effect (HandManager path).
    /// localSlotID is in the calling client's coordinate system (6-11 = ally).
    /// </summary>
    [Command]
    public void CmdUpdateCardStats(int localSlotID, int atk, int hp, int maxHp)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        // Map client's local slot to server slot
        int serverSlot;
        if (isLocalPlayer)
            serverSlot = localSlotID;
        else
            serverSlot = localSlotID >= 6 ? localSlotID - 6 : localSlotID + 6;

        BoardSlot slot = bm.GetSlot(serverSlot);
        var ci = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci == null) return;

        ci.currentAttack = atk;
        ci.currentHealth = hp;
        ci.currentMaxHealth = maxHp;
        slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
        BoardSyncManager.MarkDirty();
    }

    [Command]
    public void CmdEndTurn()
    {
        Debug.Log($"[NetworkPlayer] CmdEndTurn from netId={netId}");
        TurnManager tm = FindObjectOfType<TurnManager>();
        tm?.ServerEndTurn(this);
    }

    /// <summary>远程客户端影舞者(01502)影子进场完成，通知服务器继续分配先行权。</summary>
    [Command]
    public void CmdPhaseStartReady()
    {
        TurnManager.Instance?.OnRemotePhaseStartReady();
    }

    // ========== Health ==========

    public System.Action<int, CardInstance.HealSourceType> OnBeforePlayerHeal;

    public void ReceiveHeal(int amount, CardInstance.HealSourceType sourceType)
    {
        OnBeforePlayerHeal?.Invoke(amount, sourceType);
        Heal(amount);
    }

    public void TakeDamage(int amount)
    {
        if (NetworkServer.active)
            currentHealth -= amount;
        else
            CmdTakeDamage(amount);
    }

    [Command]
    void CmdTakeDamage(int amount)
    {
        currentHealth -= amount;
    }

    public void Heal(int amount)
    {
        if (NetworkServer.active)
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        else
            CmdHeal(amount);
    }

    [Command]
    void CmdHeal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    // ========== Energy ==========

    public void AddEnergy(int amount)
    {
        if (NetworkServer.active)
        {
            currentEnergy += amount;
            if (!_energyCanExceedLimit && currentEnergy > maxEnergy)
                currentEnergy = maxEnergy;
        }
        else
            CmdAddEnergy(amount);
    }

    [Command]
    void CmdAddEnergy(int amount)
    {
        currentEnergy += amount;
        if (!_energyCanExceedLimit && currentEnergy > maxEnergy)
            currentEnergy = maxEnergy;
    }

    public bool UseEnergy(int amount)
    {
        if (currentEnergy >= amount)
        {
            if (NetworkServer.active)
                currentEnergy -= amount;
            else
                CmdUseEnergy(amount);
            return true;
        }
        return false;
    }

    [Command]
    void CmdUseEnergy(int amount)
    {
        currentEnergy -= amount;
    }

    public int GetEnergy() => currentEnergy;

    /// <summary>
    /// No-op kept for backwards compatibility. Actual display is handled
    /// by PlayerStatsUI polling NetworkPlayer.Local/Remote every frame.
    /// </summary>
    public void UpdateUI() => RefreshUI();

    // ========== Hand Management ==========

    [Server]
    public void DrawCardOnServer()
    {
        DrawCard();
    }

    public void DrawCard()
    {
        handCards.RemoveAll(c => c == null);

        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null && drawUI.GetRemainingDraws() <= 0)
        {
            Debug.Log("[NetworkPlayer] DrawCard: no remaining draws");
            return;
        }

        if (handCards.Count >= maxHandSize)
        {
            Debug.Log("[NetworkPlayer] DrawCard: hand full");
            return;
        }

        CardData data = DeckManager.Instance?.DrawFromMain();
        if (data == null)
        {
            Debug.Log("[NetworkPlayer] DrawCard: deck empty");
            return;
        }

        GameObject prefab = GetCardPrefab(data.cardType);
        if (prefab == null)
        {
            Debug.LogError($"[NetworkPlayer] DrawCard: prefab is null for cardType={data.cardType}");
            return;
        }

        GameObject card = Instantiate(prefab, handArea);
        CardInstance instance = card.GetComponent<CardInstance>();
        if (instance != null)
        {
            // 使用牌库预生成的唯一 instanceID
            string iid = data._instanceID;
            instance.InitFromTemplate(data, 0, iid);
            if (!string.IsNullOrEmpty(iid))
                CardZoneManager.Instance?.RegisterInstanceID(iid);
        }

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        if (display != null)
            display.RefreshWithInstance(instance);

        handCards.Add(card);
        CardView cv = card.GetComponent<CardView>();
        if (cv != null)
        {
            cv.handManager = handManager;
            handManager?.RegisterCard(cv);
        }

        handCardCount = handCards.Count;
        Debug.Log($"[NetworkPlayer] DrawCard: templateID={data.templateID}, instanceID={data._instanceID}, handCount={handCardCount}");
        // Registry
        RegistrySyncManager.Instance?.UpdateCard(instance, this == Local ? 0 : 1, CardZone.Hand, -1);
    }

    public void DrawCardWithoutLimit()
    {
        handCards.RemoveAll(c => c == null);

        if (handCards.Count >= maxHandSize)
        {
            Debug.Log("[NetworkPlayer] DrawCardWithoutLimit: hand full");
            return;
        }

        CardData data = DeckManager.Instance?.DrawFromMain();
        if (data == null)
        {
            Debug.Log("[NetworkPlayer] DrawCardWithoutLimit: deck empty");
            return;
        }

        GameObject prefab = GetCardPrefab(data.cardType);
        if (prefab == null)
        {
            Debug.LogError($"[NetworkPlayer] DrawCardWithoutLimit: prefab is null");
            return;
        }

        GameObject card = Instantiate(prefab, handArea);
        CardInstance instance = card.GetComponent<CardInstance>();
        if (instance != null)
        {
            string iid = data._instanceID;
            instance.InitFromTemplate(data, 0, iid);
            if (!string.IsNullOrEmpty(iid))
                CardZoneManager.Instance?.RegisterInstanceID(iid);
        }

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        if (display != null)
            display.RefreshWithInstance(instance);

        handCards.Add(card);
        CardView cv = card.GetComponent<CardView>();
        if (cv != null)
        {
            cv.handManager = handManager;
            handManager?.RegisterCard(cv);
        }

        handCardCount = handCards.Count;
        // Registry: 本地抽牌入区
        RegistrySyncManager.Instance?.UpdateCard(instance, this == Local ? 0 : 1, CardZone.Hand, -1);
    }

    public void RemoveCardFromHand(GameObject card)
    {
        if (handCards.Contains(card))
        {
            var ciRemove = card.GetComponent<CardInstance>();
            handCards.Remove(card);
            Destroy(card);
            handCards.RemoveAll(c => c == null);
            FindObjectOfType<HandManager>()?.RefreshLayout(true);
            // Registry: 手牌移除
            if (ciRemove != null)
                RegistrySyncManager.Instance?.Remove(ciRemove.instanceID, this == Local ? 0 : 1);
            handCardCount = handCards.Count;
        }
    }

    GameObject GetCardPrefab(CardType cardType)
    {
        if (cardPrefab2D == null) cardPrefab2D = FindObjectOfType<Player>()?.cardPrefab2D;
        if (spellCardPrefab2D == null) spellCardPrefab2D = FindObjectOfType<Player>()?.spellCardPrefab2D;
        return cardType == CardType.Spell ? spellCardPrefab2D : cardPrefab2D;
    }

    public void AddCardToHand(CardData template, string instanceID = null)
    {
        // 先清理已打出/销毁的手牌（GameObject 被 Destroy 后仅剩 null 残留在列表里），
        // 否则陈旧计数会把未满手误判为满手，导致抽牌被错误拦截。
        handCards.RemoveAll(c => c == null);
        if (handCards.Count >= maxHandSize) return;

        GameObject prefab = GetCardPrefab(template.cardType);
        if (prefab == null)
        {
            Debug.LogError($"[NetworkPlayer] AddCardToHand: prefab is null for cardType={template.cardType}");
            return;
        }

        GameObject card = Instantiate(prefab, handArea);

        CardInstance inst = card.GetComponent<CardInstance>();
        if (inst == null)
            inst = card.AddComponent<CardInstance>();
        inst.InitFromTemplate(template, 0, instanceID);
        if (!string.IsNullOrEmpty(instanceID))
            CardZoneManager.Instance?.RegisterInstanceID(instanceID);

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        if (display != null) display.RefreshWithInstance(inst);

        handCards.Add(card);

        CardView cv = card.GetComponent<CardView>();
        if (cv != null)
        {
            cv.handManager = handManager;
            handManager?.RegisterCard(cv);
        }

        // 商户/能量收割者光环——新抽到手牌的召唤物减费
        if (IsMerchantOnField() && template.cardType == CardType.Summon && !inst.merchantDiscounted)
        {
            inst.merchantDiscounted = true;
            display?.Refresh();
        }
        if (IsEnergyReaperOnField() && template.cardType == CardType.Summon
            && inst.prefixes.Contains("灵能") && !inst.energyReaperDiscounted)
        {
            inst.energyReaperDiscounted = true;
            display?.Refresh();
        }

        handCardCount = handCards.Count;
        // Registry
        RegistrySyncManager.Instance?.UpdateCard(inst, this == Local ? 0 : 1, CardZone.Hand, -1);
    }

    public void AddCardToHandFromInstance(CardData template, CardInstance oldInstance, bool isEnemy = false)
    {
        // 使用实例字段而非 static Local/Remote——当服务器对 Remote 牌退场回手时，this 是 Remote，
        // 必须用 Remote.handArea/maxHandSize 而非 Local 的，否则牌会加到错误的手牌区域
        NetworkPlayer target = isEnemy ? Remote : this;
        if (target == null) return;

        int maxSize = target.maxHandSize;
        Transform targetHandArea = target.handArea;
        GameObject prefab = target.GetCardPrefab(template.cardType);

        if (prefab == null)
        {
            Debug.LogError($"[NetworkPlayer] AddCardToHandFromInstance: prefab is null isEnemy={isEnemy}");
            return;
        }

        if (!isEnemy) handCards.RemoveAll(c => c == null); // 同 AddCardToHand：清理陈旧手牌后再判上限
        if (!isEnemy && handCards.Count >= maxSize) return;

        GameObject card = Instantiate(prefab, targetHandArea);
        CardInstance inst = card.GetComponent<CardInstance>();
        if (inst == null) inst = card.AddComponent<CardInstance>();

        inst.CopyFrom(oldInstance);
        inst.currentAttack = Mathf.Max(0, inst.baseAttack);
        inst.currentHealth = Mathf.Max(0, inst.baseHealth);
        inst.currentMaxHealth = Mathf.Max(0, inst.baseMaxHealth);
        inst.currentTier = inst.baseTier;
        inst.tempAttackBoost = 0;
        inst.tempHealthBoost = 0;
        inst.handledReturnToHand = false;

        // 回手后根据光环状态同步标志——板面 currentCost 始终=baseCost，无需 +1/-1
        if (inst.templateID == "01524")
        {
            inst.scrollCorePhaseCount = 0;
            inst.currentCost = 0;
        }
        if (inst.merchantDiscounted && !IsMerchantOnField())
            inst.merchantDiscounted = false;
        if (inst.energyReaperDiscounted && !IsEnergyReaperOnField())
            inst.energyReaperDiscounted = false;
        if (!inst.merchantDiscounted && IsMerchantOnField()
            && template.cardType == CardType.Summon)
            inst.merchantDiscounted = true;
        if (!inst.energyReaperDiscounted && IsEnergyReaperOnField()
            && template.cardType == CardType.Summon && inst.prefixes.Contains("灵能"))
            inst.energyReaperDiscounted = true;
        if (inst.isShadow)
        {
            Destroy(card);
            return;
        }

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        if (display != null) display.RefreshWithInstance(inst);

        if (!isEnemy)
        {
            handCards.Add(card);
            CardView cv = card.GetComponent<CardView>();
            if (cv != null)
            {
                HandManager hm = FindObjectOfType<HandManager>();
                cv.handManager = hm;
                hm?.RegisterCard(cv);
            }
            handCardCount = handCards.Count;
            // Registry
            RegistrySyncManager.Instance?.UpdateCard(inst, this == Local ? 0 : 1, CardZone.Hand, -1);
        }
        else
        {
            // isEnemy 路径：对手手牌增加 → 注册到 Remote 侧
            RegistrySyncManager.Instance?.UpdateCard(inst, 1, CardZone.Hand, -1);
        }
    }

    // ========== Helpers ==========

    bool IsMerchantOnField()
    {
        var allAuras = GlobalEventManager.Instance?.GetAllAuras();
        if (allAuras == null) return false;
        foreach (var a in allAuras)
            if (a is MerchantAura && a.IsActive()) return true;
        return false;
    }

    bool IsEnergyReaperOnField()
    {
        var allAuras = GlobalEventManager.Instance?.GetAllAuras();
        if (allAuras == null) return false;
        foreach (var a in allAuras)
            if (a is EnergyReaperAura && a.IsActive()) return true;
        return false;
    }

    public bool IsMerchantOnFieldPublic() => IsMerchantOnField();
    public bool IsEnergyReaperOnFieldPublic() => IsEnergyReaperOnField();

    /// <summary>03004 杂兵放置后触发 01513 复生造物的机械 buff。</summary>
    static void ApplyRebornBuff(int soldierSlotID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        bool soldierIsAlly = soldierSlotID >= 6;
        // 遍历全局 12 槽搜索 01513——远端 soldier 在 0-5 但 01513 在 6-11
        for (int i = 0; i < 12; i++)
        {
            var ci = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01513")
            {
                // 只 buff 与 soldier 同侧的 01513
                if ((i >= 6) != soldierIsAlly) continue;
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                    return;
                ci.currentHealth += 1;
                ci.currentMaxHealth += 1;
                bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
                return;
            }
        }
    }

    // ========== Server-side card tracking helpers ==========

    /// <summary>Validate this player should be acting in the current server-side phase.</summary>
    bool IsMyTurnOnServer(TurnManager tm)
    {
        // PhaseStart: 双方都可能通过 CmdPlayCard 放置影舞者影子——允许
        if (tm.currentPhase == TurnManager.TurnPhase.PhaseStart)
            return true;
        // BattlePhase: 死亡触发的token召唤（01513复生造物→03004等）由服务端权威处理
        if (tm.currentPhase == TurnManager.TurnPhase.BattlePhase)
            return true;
        if (tm.currentPhase == TurnManager.TurnPhase.MyTurn)
            return (this == NetworkPlayer.Local);
        if (tm.currentPhase == TurnManager.TurnPhase.EnemyTurn)
            return (this == NetworkPlayer.Remote);
        return false;
    }

    /// <summary>Create a lightweight card object on the server for hand tracking.</summary>
    public void AddServerSideCard(CardData data, string instanceID = null)
    {
        GameObject card = new GameObject($"ServerCard_{data.templateID}");
        CardInstance ci = card.AddComponent<CardInstance>();
        ci.InitFromTemplate(data, 0, instanceID);
        if (!string.IsNullOrEmpty(instanceID))
            CardZoneManager.Instance?.RegisterInstanceID(instanceID);
        handCards.Add(card);
        handCardCount = handCards.Count;
        Debug.Log($"[NetworkPlayer] AddServerSideCard: {data.templateID} iid={instanceID}, handCount={handCardCount}");
        // Registry: 手牌入区
        RegistrySyncManager.Instance?.UpdateCard(ci, this == Local ? 0 : 1, CardZone.Hand, -1);
    }

    int GetCopyIndex(string templateID)
    {
        handCards.RemoveAll(card => card == null);
        int count = 0;
        foreach (var card in handCards)
        {
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.templateID == templateID)
                count++;
        }
        return count;
    }

    // ========== Network RPCs ==========

    [TargetRpc]
    public void TargetReceiveCard(NetworkConnectionToClient target, string templateID, string instanceID)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template != null)
        {
            Local.AddCardToHand(template, instanceID);
            Debug.Log($"[NetworkPlayer] TargetReceiveCard: {templateID} iid={instanceID}");
        }
    }

    [TargetRpc]
    public void TargetConfirmDraw(NetworkConnectionToClient target)
    {
        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null)
        {
            drawUI.UseOneDraw();
            drawUI.UpdateDisplay();
        }
    }

    /// <summary>
    /// Server tells a client to spawn a 3D card model at a board slot.
    /// The card is an enemy/opponent card, so it renders with opposite rotation
    /// and SetEnemyView (no hover interaction, no discard).
    /// overrideAtk/overrideHP/overrideMaxHP (-1 = use template default) carry
    /// enter-effect stat boosts across the network.
    /// </summary>
    [TargetRpc]
    public void TargetSpawnCard3D(NetworkConnectionToClient target, string templateID, int slotID,
        int overrideAtk, int overrideHP, int overrideMaxHP, int overrideCost, string instanceID)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template?.prefab3D == null) return;

        // 附着专用卡不应建独立槽位模型
        if (template.canAttach && template.baseHealth == 0)
        {
            Debug.LogWarning($"[TargetSpawnCard3D] 拒绝为附着专用卡 {templateID} 建独立模型");
            return;
        }

        // Mirror slot for other client: 6-11↔0-5 (both directions)
        int enemySlot = slotID >= 6 ? slotID - 6 : slotID + 6;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        BoardSlot slot = bm.GetSlot(enemySlot);
        if (slot == null) return;

        // 若槽位已有同模板ID的卡 → BoardSyncManager 已处理，跳过避免重复
        var existing = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (existing != null && existing.templateID == templateID)
            return;
        // 不同模板ID → 替换场景（如狼王把旧卡变成狼、玩家顶替狼），先清旧卡
        if (slot.currentCard3D != null)
        {
            Destroy(slot.currentCard3D);
            slot.SetCard(null);
        }

        Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(enemySlot);
        GameObject model = Instantiate(template.prefab3D, pos, Quaternion.Euler(0, 180, 0));
        model.name = templateID + "_enemy";

        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
        if (c3d != null)
        {
            CardInstance ci = model.AddComponent<CardInstance>();
            ci.InitFromTemplate(template, 0, instanceID);
            if (templateID == "01502") CardInstance.shadowMasterAlive = true;
            // 01515 / 01520 — 远程客户端也注册光环
            if (templateID == "01515") GlobalEventManager.Instance?.RegisterAura(new FanaticShamanAura { source = ci });
            if (templateID == "01520") GlobalEventManager.Instance?.RegisterAura(new MerchantAura { source = ci });
            if (templateID == "01533") GlobalEventManager.Instance?.RegisterAura(new ScarletSaintAura { source = ci });

            // Apply enter-effect stat overrides
            if (overrideAtk >= 0) ci.currentAttack = overrideAtk;
            if (overrideHP >= 0) ci.currentHealth = overrideHP;
            if (overrideMaxHP >= 0) ci.currentMaxHealth = overrideMaxHP;
            if (overrideCost >= 0) ci.currentCost = overrideCost;
            // 影子(03007)：overrideAtk 已含 bonus（客户端传入），不再重复加 currentAttack
            // baseAttack 需单独补上（overrideAtk 不影响 base）
            if (templateID == "03007")
            {
                ci.isShadow = true;
                ci.baseAttack += CardInstance.shadowAtkBonus;
                ci.currentTier += CardInstance.shadowTierBonus;
                ci.baseTier += CardInstance.shadowTierBonus;
            }

            // 01309: 进场护盾（攻击回合开始消失）
            if (ci.templateID == "01309") ci.GrantShield(false, true, false);

            c3d.cardInstance = ci;
            c3d.UpdateValues();

            // Compute x-values for 阴/阳/阴阳/万象镜面 from visible board
            if (ci.isXValue)
            {
                HandManager hmX = FindObjectOfType<HandManager>();
                if (hmX != null) hmX.UpdateXValues(ci);
                c3d.UpdateValues();
            }
        }
        slot.SetCard(model);
        // 非Token卡（templateID不以"03"开头）→ 玩家放置，禁止过期SyncNow覆盖
        var tci = model.GetComponent<Card3DInstance>()?.cardInstance;
        if (tci != null && !tci.templateID.StartsWith("03")) tci._hadEnterEffect = true;
        // 03004 杂兵 → 01513 复生造物 buff
        if (templateID == "03004") ApplyRebornBuff(enemySlot);

        // If opponent has MistHider, immediately hide this new enemy card
        if (Card3DHover.EnemyCardsAreHidden)
            Card3DHover.SetHidden(model, true, false);

        Debug.Log($"[NetworkPlayer] TargetSpawnCard3D: {templateID} to enemySlot={enemySlot}");
    }

    /// <summary>
    /// Server tells a client to spawn an enemy counter card.
    /// Position is mirrored across the screen center axis automatically by CounterManager.
    /// </summary>
    [TargetRpc]
    public void TargetSpawnCounterCard(NetworkConnectionToClient target, string templateID)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template == null) return;

        GameObject prefab = template.spellPrefab3D != null ? template.spellPrefab3D : template.prefab3D;
        if (prefab == null) return;

        CounterManager cm = CounterManager.Instance;
        if (cm == null) return;

        int count = cm.enemyCounters.Count;
        Vector3 pos = new Vector3(7.5f + count * 0.5f, 1f, -5.5f - count * 0.1f);

        GameObject model = Instantiate(prefab, pos, Quaternion.Euler(0, 180, 0));
        model.name = $"counter_enemy_{templateID}";

        // Opponent's counter is hidden — flipped, no text, no hover panel
        Card3DHover.SetHidden(model, true, false);

        CounterCard counter = new CounterCard();
        counter.model = model;
        counter.template = template;
        counter.isMine = false;
        counter.remainingDuration = template.counterDuration;
        if (template.counterTiming == CounterTriggerTiming.OnCardPlayed)
            counter.decreaseTiming = CounterTriggerTiming.OnPhaseEnd;
        else if (template.counterTiming == CounterTriggerTiming.OnPhaseStart)
            counter.decreaseTiming = CounterTriggerTiming.OnPhaseStart;
        else if (template.counterTiming == CounterTriggerTiming.OnPlayerDying)
        { counter.decreaseTiming = CounterTriggerTiming.OnPlayerDying; counter.remainingDuration = -1; }
        else
            counter.decreaseTiming = template.counterTiming;
        cm.enemyCounters.Add(counter);

        Debug.Log($"[NetworkPlayer] TargetSpawnCounterCard: {templateID} at {pos}");
    }

    /// <summary>Server tells a client to remove a card from its local hand by templateID.</summary>
    [TargetRpc]
    public void TargetRemoveCardFromHand(NetworkConnectionToClient target, string templateID)
    {
        handCards.RemoveAll(c => c == null);
        foreach (GameObject card in handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.templateID == templateID)
            {
                Debug.Log($"[NetworkPlayer] TargetRemoveCardFromHand: removing {templateID}");
                handCards.Remove(card);
                Destroy(card);
                FindObjectOfType<HandManager>()?.RefreshLayout(true);
                handCardCount = handCards.Count;
                return;
            }
        }
        Debug.LogWarning($"[NetworkPlayer] TargetRemoveCardFromHand: card {templateID} not found in local hand");
    }

    [TargetRpc]
    public void TargetSetPhase(NetworkConnectionToClient target, int phaseId)
    {
        TurnManager.TurnPhase phase = (TurnManager.TurnPhase)phaseId;
        Debug.Log($"[NetworkPlayer] TargetSetPhase: {phase}");
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm != null) tm.SetPhaseFromNetwork(phase);
    }

    /// <summary>Server syncs full 12-slot board + attachments to client.</summary>
    [TargetRpc]
    public void RpcSyncBoard(NetworkConnectionToClient target, string[] allSlots, string attachBlock)
    {
        BoardSyncManager.Instance?.ApplySync(allSlots, attachBlock);
    }

    /// <summary>Server → client: host slots 6-11 with full stats mapped to client enemy 0-5.</summary>
    [TargetRpc]
    public void TargetSyncHostBoard(NetworkConnectionToClient target, string[] data)
    {
        BoardSyncManager.Instance?.ApplySync(data, "");
    }

    /// <summary>
    /// Unified sync: client reports full 12-slot board snapshot + attachments.
    /// Server applies state, runs CheckAndHandleDeaths, then MarkDirty to broadcast.
    /// </summary>
    [Command]
    public void CmdReportAllSlots(string[] allStats, string attachBlock)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        if (allStats == null || allStats.Length < 12) return;

        for (int i = 0; i < 12; i++)
        {
            string raw = allStats[i];
            int serverSlot = isLocalPlayer ? i : (i >= 6 ? i - 6 : i + 6);
            BoardSlot slot = bm.GetSlot(serverSlot);
            if (slot == null) continue;

            // 判断该槽位是否属于上报方：
            //   Host(isLocalPlayer) 上报 → 仅更新其己方 6-11（serverSlot=6-11），对方 0-5 只读取不销毁
            //   Remote 上报 → 仅更新其己方 0-5（serverSlot=0-5），对方 6-11 只读取不销毁
            bool isReportingOwnSlot = isLocalPlayer ? (serverSlot >= 6) : (serverSlot <= 5);

            string[] parts = raw.Split('|');
            string tid = parts.Length > 0 ? parts[0] : "";

            // ── 槽位标记 ──
            // 仅更新上报方自己的槽位——远程的 flag 数据可能尚未同步服务端的最新变更
            //（如 deepSeaMarked/deepSeaHealthDebuff/deepSeaAttackDebuff），覆盖会导致 flag 回退。
            // 敌方格子的标记由服务端权威维护，通过 SyncNow 下发。
            if (isReportingOwnSlot && parts.Length >= 4)
            {
                string flags = parts[parts.Length - 4];
                if (flags.Length >= 4)
                {
                    slot.isBlocked = flags[0] == '1';
                    slot.prisonBlocked = flags[1] == '1';
                    slot.hasPlague = flags[2] == '1';
                    slot.hasSpotlight = flags[3] == '1';
                    slot.deepSeaMarked = flags.Length >= 5 && flags[4] == '1';
                    slot.deepSeaHealthDebuff = flags.Length >= 6 && flags[5] == '1';
                    slot.permaBlocked = flags.Length >= 7 && flags[6] == '1';
                    slot.SyncVisual();
                }
                if (int.TryParse(parts[parts.Length - 3], out int prc)) slot.plagueRoundCount = prc;
                if (int.TryParse(parts[parts.Length - 2], out int stb)) slot.spotlightTierBoost = stb;
                // 最后一段 "sTAB~dSAD"（~deepSeaAttackDebuff 为可选向后兼容）
                string lastField = parts[parts.Length - 1];
                string[] sub = lastField.Split('~');
                if (sub.Length > 0 && int.TryParse(sub[0], out int boost)) slot.slotTempAttackBoost = boost;
                if (sub.Length > 1 && int.TryParse(sub[1], out int dsa)) slot.deepSeaAttackDebuff = dsa;
            }

            // ── 属性/模板数据更新 ──
            // 仅更新上报方自己的槽位。敌方槽位由服务端保持权威——服务端可能在
            // DeepSeaPhaseStartDamage 等阶段处理中修改了 HP/攻击力，客户端上报的
            // 是尚未同步的过时值，覆盖会导致数据回退。
            if (!isReportingOwnSlot) continue;

            if (!string.IsNullOrEmpty(tid))
            {
                var ci = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                // 换位/附体/变身导致 templateID 不一致 → 无法判断是换位（应等RPC）还是变身（应重建），
                // 直接跳过不修补——等下次同步数据一致后再应用属性更新。
                if (ci != null && ci.templateID != tid)
                    continue;
                if (ci != null && ci.templateID == tid)
                {
                    string[] p = raw.Split('|'); int v;
                    if (p.Length > 1 && int.TryParse(p[1], out v))
                    {
                        // 深海恶物 debuff：服务器已扣血 → 拒绝客户端上报的旧（更高）HP
                        if (slot.deepSeaHealthDebuff && v > ci.currentHealth)
                        { /* 保留服务器权威的更低的 HP */ }
                        else ci.currentHealth = v;
                    }
                    if (p.Length > 2 && int.TryParse(p[2], out v))
                    {
                        // 服务端在当前回合已修改了临时攻击力 → 保护不覆盖。
                        // tempAttackBoost > 0：AddTempAttack（01324 猎杀者等）
                        // originalAttackBeforeDebuff > 0：01318 弱化棱晶已将攻击力临时设为 1
                        // 两种情况下远程上报的 currentAttack 都是过时旧值，不应覆盖。
                        bool serverModified = ci.tempAttackBoost > 0 || ci.originalAttackBeforeDebuff > 0;
                        if (serverModified)
                        {
                            // 保留服务端已修改的值，不信任远程上报的旧 currentAttack
                        }
                        else if (ci.currentAttack > v && HasAttachBonusOn(ci, serverSlot, bm))
                        {
                            // 远程上报值低于服务端，且服务端该卡有附着加成——说明
                            // 远端尚未同步到附着加成（01327 等），跳过覆盖。
                        }
                        else
                        {
                            // 无服务端临时修改：远程上报值可能是 RunRemoteFirstStrikes
                            // 的 debuff 结果。仅在上报值更低（实际 debuff）时保存
                            // originalAttackBeforeDebuff。
                            if (ci.originalAttackBeforeDebuff <= 0 && v < ci.currentAttack)
                                ci.originalAttackBeforeDebuff = ci.currentAttack;
                            ci.currentAttack = v;
                        }
                    }
                    if (p.Length > 3 && int.TryParse(p[3], out v)) ci.currentMaxHealth = v;
                    if (p.Length > 4 && int.TryParse(p[4], out v)) ci.baseAttack = v;
                    if (p.Length > 5 && int.TryParse(p[5], out v)) ci.baseHealth = v;
                    if (p.Length > 6 && int.TryParse(p[6], out v)) ci.baseMaxHealth = v;
                    // 场上费用绝对锁定——CmdPlayCard时已设，不覆盖
                    if (p.Length > 8 && int.TryParse(p[8], out v)) ci.currentTier = v;
                    if (p.Length > 9 && int.TryParse(p[9], out v)) ci.baseTier = v;
                    if (p.Length > 10 && int.TryParse(p[10], out int shieldEnc) && shieldEnc > 0)
                    {
                        // 服务端已从战斗/处理中赋予护盾（非永久），不信任客户端过时上报
                        if (!isLocalPlayer && ci.hasShield && !ci.shieldIsPermanent)
                        { /* 保留服务端刚赋予的护盾 */ }
                        else
                        {
                            ci.hasShield = true;
                            ci.shieldIsPermanent = (shieldEnc & 1) != 0;
                            ci.shieldEndAtBattleStart = (shieldEnc & 2) != 0;
                            ci.shieldEndAtBattleEnd = (shieldEnc & 4) != 0;
                        }
                    }
                    else if (!isLocalPlayer && ci.hasShield && !ci.shieldIsPermanent)
                    {
                        // 服务端有非永久护盾，不信任客户端过时上报 shield=0
                    }
                    else
                    {
                        ci.hasShield = false;
                        ci.shieldIsPermanent = false;
                        ci.shieldEndAtBattleStart = false;
                        ci.shieldEndAtBattleEnd = false;
                    }
                    if (p.Length > 11) ci.silencedThisPhase = (p[11] == "1");
                    if (p.Length > 12) ci.isAttached = (p[12] == "1");
                    if (p.Length > 13) ci.poisoned = (p[13] == "1");
                    if (p.Length > 14) ci.prefixes = p[14];
                    if (p.Length > 15)
                    {
                        var newList = new System.Collections.Generic.List<string>(
                            p[15].Split(new[] { ";;" }, System.StringSplitOptions.None));
                        newList.RemoveAll(t => string.IsNullOrEmpty(t));
                        if (ci.grantedTraitTexts == null) ci.grantedTraitTexts = new System.Collections.Generic.List<string>();
                        var oldCopy = new System.Collections.Generic.List<string>(ci.grantedTraitTexts);
                        foreach (var t in oldCopy)
                            if (!newList.Contains(t)) ci.RemoveGrantedTrait(t);
                        foreach (var t in newList)
                            if (!oldCopy.Contains(t)) ci.GrantTrait(t);
                    }
                    // totalDamageTaken (17th field) — 01534 活化母巢需要服务端权威值
                    if (p.Length > 16 && int.TryParse(p[16], out int tdt))
                        ci.totalDamageTaken = Mathf.Max(ci.totalDamageTaken, tdt);
                    if (parts[0] == "03007") ci.isShadow = true;
                    slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
            // 仅上报方自己的槽位空→销毁（不销毁对方卡模型）
            else if (isReportingOwnSlot && slot.currentCard3D != null)
            {
                Destroy(slot.currentCard3D); slot.SetCard(null);
            }
        }
        ApplyAttachDiff(bm, attachBlock, isLocalPlayer);

        BoardSlot.CheckAndHandleDeaths();
        BoardSyncManager.MarkDirty();
    }

    /// <summary>检查 slotID 上的卡牌是否有附着物提供攻击力加成（01327等）。</summary>
    static bool HasAttachBonusOn(CardInstance ci, int slotID, BoardManager bm)
    {
        if (ci == null || bm == null) return false;
        foreach (var obj in bm.attachedModels)
        {
            var aci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
            if (aci != null && aci.isAttached && aci.hostSlotID == slotID)
            {
                // 01327 +3攻击、01112 +1攻击、01126 +1攻击 等
                if (aci.templateID == "01327" || aci.templateID == "01112"
                    || aci.templateID == "01126" || aci.templateID == "01127"
                    || aci.templateID == "01333" || aci.templateID == "01334")
                    return true;
            }
        }
        return false;
    }

    /// <summary>附着物 diff 更新：已有→移坐标，不存在→创建。</summary>
    static void ApplyAttachDiff(BoardManager bm, string attachBlock, bool isLocalPlayer)
    {
        bm.attachedModels.RemoveAll(a => a == null);

        // 解析客户端 gen 前缀 "G{gen}|rest"
        int incomingGen = 0;
        string actualAttachBlock = attachBlock;
        if (!string.IsNullOrEmpty(attachBlock) && attachBlock.StartsWith("G"))
        {
            int pipeIdx = attachBlock.IndexOf('|');
            if (pipeIdx > 0 && int.TryParse(attachBlock.Substring(1, pipeIdx - 1), out int g))
            {
                incomingGen = g;
                actualAttachBlock = attachBlock.Substring(pipeIdx + 1);
            }
        }

        bool isServerProcessingClientReport = Mirror.NetworkServer.active && !isLocalPlayer;

        var incoming = new System.Collections.Generic.List<(string tid, int hs, int order, string iid)>();
        if (!string.IsNullOrEmpty(actualAttachBlock))
        {
            foreach (var item in actualAttachBlock.Split(new[] { "||" }, System.StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(item)) continue;
                var p = item.Split('|');
                if (p.Length < 3) continue;
                if (!int.TryParse(p[1], out int h) || !int.TryParse(p[2], out int o)) continue;
                string iid = p.Length > 3 ? p[3] : "";
                incoming.Add((p[0], h, o, iid));
            }
        }

        var slotTids = new System.Collections.Generic.HashSet<string>();
        for (int si = 0; si < 12; si++)
        {
            var sci = bm.GetSlot(si)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (sci != null && !string.IsNullOrEmpty(sci.templateID)) slotTids.Add(sci.templateID);
        }

        if (!isServerProcessingClientReport)
        {
            for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
            {
                var obj = bm.attachedModels[i];
                if (obj == null) { bm.attachedModels.RemoveAt(i); continue; }
                var ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci == null || !ci.isAttached) { bm.attachedModels.RemoveAt(i); continue; }
                bool stillExists = incoming.Exists(x => {
                    if (!string.IsNullOrEmpty(x.iid) && x.iid == ci.instanceID) return true;
                    int xServerHS = isLocalPlayer ? x.hs : (x.hs >= 6 ? x.hs - 6 : x.hs + 6);
                    return x.tid == ci.templateID && xServerHS == ci.hostSlotID && x.order == ci.attachOrder;
                });
                if (!stillExists) { Destroy(obj); bm.attachedModels.RemoveAt(i); }
            }
        }

        var hm = FindObjectOfType<HandManager>();
        foreach (var (tid, hs, o, iid) in incoming)
        {
            if (slotTids.Contains(tid)) continue;
            int mapped = isLocalPlayer ? hs : (hs >= 6 ? hs - 6 : hs + 6);

            GameObject existing = null;
            foreach (var obj in bm.attachedModels)
            {
                var ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == tid)
                {
                    if (!string.IsNullOrEmpty(iid) && ci.instanceID == iid) { existing = obj; break; }
                    if (ci.attachOrder == o && ci.hostSlotID == mapped) { existing = obj; break; }
                }
            }

            if (existing != null)
            {
                existing.transform.position = HandManager.GetAttachWorldPos(mapped, o);
                var eci = existing.GetComponent<Card3DInstance>()?.cardInstance;
                if (eci != null) eci.hostSlotID = mapped;
                continue;
            }

            // 过期数据中被明确移除过的附件 → 不重建
            if (!string.IsNullOrEmpty(iid) && BoardManager.removedAttachIDs.Contains(iid))
                continue;

            var t = CardDatabase.Instance?.GetTemplate(tid);
            if (t?.prefab3D == null || hm == null) continue;
            var m = Instantiate(t.prefab3D, HandManager.GetAttachWorldPos(mapped, o), Quaternion.Euler(0, 180, 0));
            var c = m.GetComponent<Card3DInstance>();
            if (c != null)
            {
                var n = m.AddComponent<CardInstance>(); n.InitFromTemplate(t, 0);
                n.isAttached = true; n.hostSlotID = mapped; n.attachOrder = o;
                c.cardInstance = n; c.UpdateValues();
            }
            bm.attachedModels.Add(m);
        }
    }

    /// <summary>Client → server: report my 6-11 stats + attachments, server updates its 0-5 then re-syncs.</summary>
    [Command]
    public void CmdReportMyBoard(string[] myStats, string attachBlock)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 6 && i < myStats.Length; i++)
        {
            string raw = myStats[i];
            BoardSlot slot = bm.GetSlot(i); // server 0-5 = reporting client
            if (slot == null) continue;

            string tid = string.IsNullOrEmpty(raw) ? "" : raw.Split('|')[0];

            // Empty report → clear the slot
            if (string.IsNullOrEmpty(tid))
            {
                if (slot.currentCard3D != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }
                continue;
            }

            var ci = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;

            // 服务器该槽为空，但客户端上报有牌 = 陈旧上报。
            // 服务器对"移除"是权威的（战斗死亡/退场都在服务器结算），客户端上报绝不能把已移除的牌复活。
            // 否则出现"召唤物死亡后又进场"（陈旧上报重建了已死单位）。新牌走 CmdPlayCard 等专用通道，不依赖此处创建。
            if (ci == null)
            {
                // 拒绝客户端陈旧上报复活已移除单位（ci==null 时 Create 会复活）


                continue;
            }

            // 变身（腐化/飞升/阴阳）：服务器仍有这张牌，但 templateID 变了 → 重建模型。
            if (ci.templateID != tid)
            {
                if (slot.currentCard3D != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }
                CardData t = CardDatabase.Instance?.GetTemplate(tid);
                if (t?.prefab3D != null)
                {
                    Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(i);
                    GameObject model = Instantiate(t.prefab3D, pos, Quaternion.Euler(0, 180, 0));
                    Card3DInstance c3d = model.GetComponent<Card3DInstance>();
                    if (c3d != null)
                    {
                        CardInstance nci = model.AddComponent<CardInstance>();
                        nci.InitFromTemplate(t, 0);
                        c3d.cardInstance = nci;
                    }
                    slot.SetCard(model);
                    ci = model.GetComponent<Card3DInstance>()?.cardInstance;
                }
            }

            if (ci != null && ci.templateID == tid)
            {
                string[] p = raw.Split('|');
                int hp2, atk2, mh2, ba2, bh2, bmh2, tier2, bt2;
                if (p.Length > 1 && int.TryParse(p[1], out hp2)) ci.currentHealth = hp2;
                if (p.Length > 2 && int.TryParse(p[2], out atk2)) ci.currentAttack = atk2;
                if (p.Length > 3 && int.TryParse(p[3], out mh2)) ci.currentMaxHealth = mh2;
                if (p.Length > 4 && int.TryParse(p[4], out ba2)) ci.baseAttack = ba2;
                if (p.Length > 5 && int.TryParse(p[5], out bh2)) ci.baseHealth = bh2;
                if (p.Length > 6 && int.TryParse(p[6], out bmh2)) ci.baseMaxHealth = bmh2;
                // 场上费用锁定——不覆盖
                if (p.Length > 8 && int.TryParse(p[8], out tier2)) ci.currentTier = tier2;
                if (p.Length > 9 && int.TryParse(p[9], out bt2)) ci.baseTier = bt2;
                if (p.Length > 10) ci.hasShield = (p[10] == "1");
                if (p.Length > 11) ci.silencedThisPhase = (p[11] == "1");
                if (p.Length > 12) ci.isAttached = (p[12] == "1");
                if (p.Length > 13) ci.poisoned = (p[13] == "1");
                if (p.Length > 14) ci.prefixes = p[14];
                // granted trait texts (16th field)
                if (p.Length > 15)
                {
                    var newList = new System.Collections.Generic.List<string>(
                        p[15].Split(new[] { ";;" }, System.StringSplitOptions.None));
                    newList.RemoveAll(t => string.IsNullOrEmpty(t));
                    if (ci.grantedTraitTexts == null) ci.grantedTraitTexts = new System.Collections.Generic.List<string>();
                    var oldCopy = new System.Collections.Generic.List<string>(ci.grantedTraitTexts);
                    foreach (var t in oldCopy)
                        if (!newList.Contains(t)) ci.RemoveGrantedTrait(t);
                    foreach (var t in newList)
                        if (!oldCopy.Contains(t)) ci.GrantTrait(t);
                }
                slot.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }

        // Apply attachment block — reporting client's allied attachments (host 6-11) remap to server 0-5
        // Clear existing attachments for the reporting side first
        int targetHostRange = isLocalPlayer ? 6 : 0; // always Remote → 0-5
        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
        {
            var aci = bm.attachedModels[i]?.GetComponent<Card3DInstance>()?.cardInstance;
            if (aci != null && aci.isAttached && aci.hostSlotID >= targetHostRange && aci.hostSlotID < targetHostRange + 6)
            { Destroy(bm.attachedModels[i]); bm.attachedModels.RemoveAt(i); }
        }

        if (!string.IsNullOrEmpty(attachBlock))
        {
            HandManager hm2 = FindObjectOfType<HandManager>();
            // 去重：附着物 templateID 若已存在于 slot（=独立放置过的牌），跳过创建，防止重复模型。
            var slotTids = new System.Collections.Generic.HashSet<string>();
            for (int si = 0; si < 12; si++)
            {
                var sci = bm.GetSlot(si)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (sci != null && !string.IsNullOrEmpty(sci.templateID)) slotTids.Add(sci.templateID);
            }
            foreach (var item in attachBlock.Split(new[] { "||" },
                System.StringSplitOptions.RemoveEmptyEntries))
            {
                var p = item.Split('|');
                if (p.Length < 3) continue;
                if (!int.TryParse(p[1], out int hs) || !int.TryParse(p[2], out int o)) continue;
                int serverHostSlot = isLocalPlayer ? hs : hs - 6; // client 6-11 → server 0-5 for Remote
                // 去重：若该模板已在 slot 中存在，跳过附着物创建
                if (slotTids.Contains(p[0])) continue;
                var t = CardDatabase.Instance?.GetTemplate(p[0]);
                if (t?.prefab3D == null || hm2 == null) continue;
                GameObject model = Instantiate(t.prefab3D, HandManager.GetAttachWorldPos(serverHostSlot, o), Quaternion.Euler(0, 180, 0));
                Card3DInstance c3dAtt = model.GetComponent<Card3DInstance>();
                if (c3dAtt != null)
                {
                    CardInstance nci = model.AddComponent<CardInstance>();
                    nci.InitFromTemplate(t, 0);
                    nci.isAttached = true; nci.hostSlotID = serverHostSlot; nci.attachOrder = o;
                    c3dAtt.cardInstance = nci; c3dAtt.UpdateValues();
                }
                bm.attachedModels.Add(model);
            }
        }

        BoardSyncManager.MarkDirty();
    }

    /// <summary>
    /// Pirate (01337) effect: confirm all swaps at once.
    /// pairs: "a1,b1;a2,b2;..." — acting player's local enemy slot IDs (0-5).
    /// </summary>
    [Command]
    public void CmdPirateFinalize(string pairs)
    {
        if (string.IsNullOrEmpty(pairs)) return;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        bool hostActing = isLocalPlayer;

        if (hostActing)
        {
            // Host already swapped locally (server IS host, same BoardManager).
            // Only need to tell Remote.
            if (Remote != null)
                TargetFinalizePirate(Remote.connectionToClient, pairs);
        }
        else
        {
            // Remote acted — server BoardManager needs the swap (remote enemy 0-5 → server 6-11).
            // Host shares server's BoardManager, so host sees it automatically. No TargetRpc to host.
            foreach (string pair in pairs.Split(';'))
            {
                string[] ab = pair.Split(',');
                if (ab.Length != 2) continue;
                if (!int.TryParse(ab[0], out int a) || !int.TryParse(ab[1], out int b)) continue;
                SwapBoardSlots(bm, a + 6, b + 6);
            }
        }

        BoardSyncManager.MarkDirty();
    }

    [TargetRpc]
    public void TargetFinalizePirate(NetworkConnectionToClient target, string pairs)
    {
        if (string.IsNullOrEmpty(pairs)) return;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        foreach (string pair in pairs.Split(';'))
        {
            string[] ab = pair.Split(',');
            if (ab.Length != 2) continue;
            if (!int.TryParse(ab[0], out int a) || !int.TryParse(ab[1], out int b)) continue;

            // Acting enemy (0-5) → this client's ally (6-11)
            SwapBoardSlots(bm, a + 6, b + 6);
        }
    }

    /// <summary>Swap the currentCard3D GameObjects between two board slots.</summary>
    static void SwapBoardSlots(BoardManager bm, int slotA, int slotB)
    {
        BoardManager.SwapCards(slotA, slotB);
    }

    // ========== Enemy board damage sync (03504 on-enter, etc.) ==========

    /// <summary>
    /// Sync enemy slot health after on-enter damage. Acting player's enemy (0-5) → server-side opponent ally.
    /// enemyStats[0..5] = "templateID|health" or empty for empty slots.
    /// </summary>
    [Command]
    public void CmdSyncEnemyDamage(string[] enemyStats)
    {
        if (enemyStats == null || enemyStats.Length < 6) return;

        // Host acting → enemy is server 0-5; Remote acting → enemy is server 6-11
        int offset = isLocalPlayer ? 0 : 6;

        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 6; i++)
        {
            if (string.IsNullOrEmpty(enemyStats[i])) continue;
            string[] parts = enemyStats[i].Split('|');
            if (parts.Length < 2) continue;
            string tid = parts[0];
            if (!int.TryParse(parts[1], out int hp)) continue;

            int serverSlot = i + offset;
            BoardSlot slot = bm?.GetSlot(serverSlot);
            var ci = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == tid)
                ci.currentHealth = hp;
            slot?.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
        }
        BoardSlot.CheckAndHandleDeaths();
        BoardSyncManager.MarkDirty();
    }

    /// <summary>
    /// Send a card back to the correct player's hand. Called from server-side HandleDeath.
    /// If slotID indicates a Remote-owned card (0-5 on server), TargetRpc to Remote.
    /// </summary>
    [Server]
    public void RouteReturnToHand(int slotID, CardInstance ci)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(ci.templateID);
        if (template == null) return;

        if (slotID >= 0 && slotID < 6)
        {
            // Card in server 0-5 belongs to Remote player
            if (Remote != null)
                TargetReturnToHand(Remote.connectionToClient, ci.templateID);
        }
        else
        {
            // Card in server 6-11 belongs to Host
            Local?.AddCardToHandFromInstance(template, ci);
        }
    }

    [TargetRpc]
    public void TargetReturnToHand(NetworkConnectionToClient target, string templateID)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template != null)
            AddCardToHand(template);
    }





    // ========== 蛊惑之音(02304) 进场重定向 ==========

    /// <summary>
    /// Server → Host client: enemy's enter effect countered by 02304.
    /// Host selects an ally to receive the redirected enter effect.
    /// </summary>
    [TargetRpc]
    public void TargetHandleEnterRedirect(NetworkConnectionToClient target, string redirectTemplateID)
    {
        CardData redirectTemplate = CardDatabase.Instance?.GetTemplate(redirectTemplateID);
        if (redirectTemplate == null) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        bool hasAlly = false;
        for (int i = 6; i <= 11; i++)
            if (bm.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }

        if (!hasAlly)
        {
            Debug.Log($"[02304] No ally to redirect — enter effect of {redirectTemplateID} blocked");
            return;
        }

        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                CardInstance targetInst = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (targetInst != null)
                    targetSlot.StartCoroutine(targetSlot.StartOnEnterEffect(redirectTemplate, targetInst));
            }
            SelectionManager.Instance.ForceEndAll();
            BoardSyncManager.MarkDirty();
        });
    }

    // ========== Damage floater broadcast ==========

    /// <summary>Server → clients: show a damage/heal/buff floater above the card in serverSlotID.</summary>
    [ClientRpc]
    public void RpcShowDamageFloater(int serverSlotID, int value, int typeInt)
    {
        // Host already shows floaters locally via the server-side DamagePipeline call; skip.
        if (isLocalPlayer) return;

        // Map server slot to this client's local board layout.
        int localSlot = serverSlotID >= 6 ? serverSlotID - 6 : serverSlotID + 6;

        BoardManager bm = FindObjectOfType<BoardManager>();
        Vector3 worldPos;
        BoardSlot slot = bm?.GetSlot(localSlot);
        if (slot?.currentCard3D != null)
            worldPos = slot.currentCard3D.transform.position + Vector3.up * 2.5f;
        else
        {
            HandManager hm = FindObjectOfType<HandManager>();
            worldPos = (hm != null ? hm.GetSlotWorldPosition(localSlot) : Vector3.zero) + Vector3.up * 2.5f;
        }
        DamageFloater.Show(worldPos, value, (FloaterType)typeInt);
    }

    // ========== Transform sync (腐化/飞升 on any slot) ==========

    /// <summary>
    /// Client → server: a minion at localSlotID transformed into newTemplateID.
    /// localSlotID is in the acting client's coordinates (0-5 enemy, 6-11 ally).
    /// Server remaps and rebuilds its model, then broadcasts to the other client.
    /// </summary>
    [Command]
    public void CmdReportTransform(int localSlotID, string newTemplateID)
    {
        bool hostActing = isLocalPlayer;
        // Acting ally 6-11 → server: host keeps 6-11, remote maps to 0-5
        // Acting enemy 0-5 → server: host keeps 0-5, remote maps to 6-11
        int serverSlot;
        if (localSlotID >= 6) serverSlot = hostActing ? localSlotID : localSlotID - 6;
        else serverSlot = hostActing ? localSlotID : localSlotID + 6;

        RebuildSlotModel(serverSlot, newTemplateID);

        // Broadcast to the other client (their view mirrors the acting client)
        NetworkPlayer other = hostActing ? Remote : Local;
        if (other != null)
        {
            // Other client sees the acting client's slots mirrored: ally↔enemy
            int otherSlot = localSlotID >= 6 ? localSlotID - 6 : localSlotID + 6;
            other.TargetReportTransform(other.connectionToClient, otherSlot, newTemplateID);
        }
        BoardSyncManager.MarkDirty();
    }

    [TargetRpc]
    public void TargetReportTransform(NetworkConnectionToClient target, int slotID, string newTemplateID)
    {
        RebuildSlotModel(slotID, newTemplateID);
    }

    /// <summary>Replace the model at slotID with a fresh instance of newTemplateID.</summary>
    static void RebuildSlotModel(int slotID, string newTemplateID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardSlot slot = bm?.GetSlot(slotID);
        if (slot == null) return;

        if (slot.currentCard3D != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }

        CardData t = CardDatabase.Instance?.GetTemplate(newTemplateID);
        if (t?.prefab3D == null) return;

        HandManager hm = FindObjectOfType<HandManager>();
        Vector3 pos = hm.GetSlotWorldPosition(slotID);
        GameObject model = UnityEngine.Object.Instantiate(t.prefab3D, pos, Quaternion.Euler(0, 180, 0));
        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
        if (c3d != null)
        {
            CardInstance ci = model.AddComponent<CardInstance>();
            ci.InitFromTemplate(t, 0);
            c3d.cardInstance = ci;
            c3d.UpdateValues();
        }
        slot.SetCard(model);
    }

    // ========== Surrender ==========

    [Command]
    public void CmdSurrender()
    {
        Debug.Log($"[NetworkPlayer] CmdSurrender from netId={netId}");

        // Tell the other player to return to lobby
        NetworkPlayer other = this == Local ? Remote : Local;
        if (other != null)
            TargetSurrender(other.connectionToClient);

        // Each side already handles its own lobby return:
        // - Surrendering player: Surrender() starts DoReturnToLobby after sending CmdSurrender
        // - Other player: TargetSurrender triggers OnOpponentSurrendered()
    }

    [TargetRpc]
    public void TargetSurrender(NetworkConnectionToClient target)
    {
        Debug.Log("[NetworkPlayer] TargetSurrender received");
        FindObjectOfType<SettingsButton>()?.OnOpponentSurrendered();
    }

    // ========== Counter sync ==========

    /// <summary>
    /// Client → server: counter spell played. Rebuilds the enemy counter model
    /// on the server so the host sees it. PlayCounter uses inst only for templateID
    /// lookup and model naming; the CardInstance copy is skipped for isMine=false.
    /// </summary>
    [Command]
    public void CmdPlayCounter(string templateID)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template == null) return;

        // PlayCounter(isMine=false) 只读 templateID 后 Instantiate 新 prefab 存为 model，
        // temp 在 PlayCounter 返回后不再被引用——直接销毁。
        GameObject temp = new GameObject("TempCounterCmd");
        CardInstance ci = temp.AddComponent<CardInstance>();
        ci.InitFromTemplate(template, 0);
        CounterManager.Instance?.PlayCounter(temp, false);
        Destroy(temp);
    }

    /// <summary>
    /// 客户端→服务器：无畏者(01319)选择了要无效果触发的反制牌。
    /// 服务器权威处理 ExpireWithNoEffect（扣能量+移除+同步）。
    /// </summary>
    [Command]
    public void CmdFearlessTriggerCounter(string templateID)
    {
        CounterManager cm = CounterManager.Instance;
        if (cm == null) return;

        // 在两类列表中都查找，自动确定 isMine
        for (int i = cm.myCounters.Count - 1; i >= 0; i--)
        {
            if (cm.myCounters[i].template.templateID == templateID)
            {
                cm.ExpireWithNoEffectPublic(cm.myCounters[i], i, true);
                return;
            }
        }
        for (int i = cm.enemyCounters.Count - 1; i >= 0; i--)
        {
            if (cm.enemyCounters[i].template.templateID == templateID)
            {
                cm.ExpireWithNoEffectPublic(cm.enemyCounters[i], i, false);
                return;
            }
        }
        Debug.LogWarning($"[CmdFearlessTriggerCounter] 未找到反制牌: {templateID}");
    }

    /// <summary>
    /// 远端客户端→服务器：告知服务器 01331 囚牢封锁了哪两个格子。
    /// 服务器端 CardInstance 未运行 PrisonEnterEffect，退场时需要此信息精确解锁。
    /// </summary>
    [Command]
    public void CmdSetPrisonSlots(string instanceID, int myPrisonSlot, int enemyPrisonSlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            var slot = bm.GetSlot(i);
            var ci = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.instanceID == instanceID && ci.templateID == "01331")
            {
                ci.prisonMySlot = myPrisonSlot;
                ci.prisonEnemySlot = enemyPrisonSlot;
                return;
            }
        }
    }

    /// <summary>
    /// 远端客户端→服务器：告知服务器 01505 封锁者永久封锁了哪个敌方格子。
    /// 远端上报的敌方格子视角与服务器相反，需要镜像映射后应用。
    /// </summary>
    [Command]
    public void CmdBlockSlot(int reportedEnemySlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        // 远程视角的0-5 = 服务器视角的6-11，反过来也适用
        int serverSlot = isLocalPlayer ? reportedEnemySlot : (reportedEnemySlot >= 6 ? reportedEnemySlot - 6 : reportedEnemySlot + 6);
        BoardSlot target = bm.GetSlot(serverSlot);
        if (target != null)
        {
            target.isBlocked = true;
            target.permaBlocked = true;
            target.SyncVisual();
            BoardSyncManager.MarkDirty();
        }
    }

    /// <summary>客户端→服务器：01507 祝福目标同步。服务器侧 DamagePipeline 依赖此信息。</summary>
    [Command]
    public void CmdBlessTarget(string priestInstanceID, string targetInstanceID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        CardInstance priest = null, target = null;
        for (int i = 0; i < 12; i++)
        {
            var ci = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.instanceID == priestInstanceID) priest = ci;
            if (ci != null && ci.instanceID == targetInstanceID) target = ci;
        }
        if (priest != null && target != null)
        {
            target.hasLifePriestBlessing = true;
            target.lifePriestBlessingSource = priest;
        }
    }

    /// <summary>客户端→服务器：通知服务器将指定模板的卡加入该客户端手牌追踪。</summary>
    [Command]
    public void CmdAddCardToHand(string templateID, int count)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template == null) return;
        for (int i = 0; i < count; i++)
        {
            string iid = CardZoneManager.GenerateInstanceID(templateID);
            AddServerSideCard(template, iid);
        }
    }

    /// <summary>客户端→服务器：同步 01535 执行之剑消耗的法术费用。</summary>
    [Command]
    public void CmdSetSwordCost(string swordInstanceID, int cost)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            var ci = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.instanceID == swordInstanceID && ci.templateID == "01535")
            {
                ci.consumedSpellCost = cost;
                return;
            }
        }
    }

    /// <summary>远程客户端→服务器：01511死亡回手。state 由客户端序列化——服务端的 ci 从未跑过 MindScholarEnterEffect，状态为空。</summary>
    [Command]
    public void CmdReturnScholarToHand(string scholarInstanceID, int clientSideSlotID, string scholarState)
    {
        int serverSlot = isLocalPlayer ? clientSideSlotID : clientSideSlotID - 6;
        BoardManager bm = FindObjectOfType<BoardManager>();
        var ci = bm?.GetSlot(serverSlot)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci == null || ci.templateID != "01511" || ci.instanceID != scholarInstanceID) return;
        TargetReceiveReturnedCard(connectionToClient, "01511", scholarState);
        var slot = bm.GetSlot(serverSlot);
        if (slot?.currentCard3D != null) { Destroy(slot.currentCard3D); slot.SetCard(null); }
        BoardSyncManager.MarkDirty();
    }

    /// <summary>客户端→服务器：同步赋予特性(grantedTraitTexts)到服务器侧CardInstance。</summary>
    [Command]
    public void CmdSyncGrantedTraits(int localSlotID, string traitsSerialized)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        int serverSlot = isLocalPlayer ? localSlotID : (localSlotID >= 6 ? localSlotID - 6 : localSlotID + 6);
        var ci = bm.GetSlot(serverSlot)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci == null || string.IsNullOrEmpty(traitsSerialized)) return;
        var traits = new string[0];
        if (!string.IsNullOrEmpty(traitsSerialized))
            traits = traitsSerialized.Split(new[] { ";;" }, StringSplitOptions.None);
        foreach (string t in traits)
            if (!string.IsNullOrEmpty(t) && (ci.grantedTraitTexts == null || !ci.grantedTraitTexts.Contains(t)))
                ci.GrantTrait(t);
        BoardSyncManager.MarkDirty();
    }

    /// <summary>客户端→服务器：对敌方一张卡造成 N 伤害。CmdReportAllSlots 不写 enemy slot HP。</summary>
    [Command]
    public void CmdApplyDamageToCard(int clientSlotID, int damage)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        // 远程上报的 slot 视角与服务器相反，镜像映射
        int serverSlot = isLocalPlayer ? clientSlotID : (clientSlotID >= 6 ? clientSlotID - 6 : clientSlotID + 6);
        var slot = bm.GetSlot(serverSlot);
        var ci = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci != null)
        {
            // ── 01514 追随者挡致命伤害（服务端权威）──
            if (ci.braveTemplateID == "01514" && ci.currentHealth - damage <= 0)
            {
                GameObject follower = DamagePipeline.FindTopFollower(ci);
                if (follower != null)
                {
                    DamagePipeline.RemoveFollower(follower);
                    ci.currentHealth = 2;
                    DamagePipeline.ReorderAttachments(serverSlot);
                    BoardManager.SyncAttachedModels(slot);
                    BoardSyncManager.MarkDirty();
                    return;
                }
            }
            // 记录敌方伤害来源——01513 复生造物需要此信息检测敌方导致的死亡
            if (!isLocalPlayer)
            {
                if (ci.enemyDamageSourceIDs == null) ci.enemyDamageSourceIDs = new List<string>();
                ci.enemyDamageSourceIDs.Add("ENEMY_CMD");
            }
            ci.currentHealth -= damage;
            if (ci.currentHealth < 0) ci.currentHealth = 0;
            slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            BoardSlot.CheckAndHandleDeaths();
        }
        BoardSyncManager.MarkDirty();
    }

    /// <summary>客户端→服务器：01524 神灵画卷消灭全部敌方召唤物。</summary>
    [Command]
    public void CmdDestroyCard01524(int clientSlotID, int lethalDamage)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        int serverSlot = isLocalPlayer ? clientSlotID : (clientSlotID >= 6 ? clientSlotID - 6 : clientSlotID + 6);
        var slot = bm.GetSlot(serverSlot);
        var ci = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci != null)
        {
            ci.isActiveExit = true;
            slot.HandleDeath(slot.currentCard3D);
        }
        BoardSyncManager.MarkDirty();
    }

    /// <summary>服务端→远端：01510 古老精灵重附着选择。oldHostLocalSlot为远程本地视角(6-11)。</summary>
    [TargetRpc]
    public void TargetFairyReattachSelect(NetworkConnectionToClient target, int oldHostLocalSlot)
    {
        BoardSlot bs = FindObjectOfType<BoardSlot>();
        if (bs != null) bs.StartCoroutine(bs.RemoteFairyReattachSelect(oldHostLocalSlot));
    }

    /// <summary>远端→服务器：01510 古老精灵重附着结果。newHostLocalSlot为远程本地视角(6-11)。</summary>
    [Command]
    public void CmdFairyReattachResult(int newHostLocalSlot)
    {
        int serverSlot = isLocalPlayer ? newHostLocalSlot : newHostLocalSlot - 6;
        BoardSlot.OnFairyReattachResult(newHostLocalSlot >= 0 ? serverSlot : -1);
    }

    /// <summary>服务端→远端：01117/01511等卡牌回手（通过 CopyFrom 完整继承板面状态）。</summary>
    [TargetRpc]
    public void TargetReceiveReturnedCard(NetworkConnectionToClient target, string templateID, string srcState)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template == null) return;
        handCards.RemoveAll(c => c == null);
        if (handCards.Count >= maxHandSize) return;
        GameObject prefab = GetCardPrefab(template.cardType);
        if (prefab == null) return;
        GameObject card = Instantiate(prefab, handArea);
        CardInstance inst = card.GetComponent<CardInstance>();
        if (inst == null) inst = card.AddComponent<CardInstance>();
        inst.InitFromTemplate(template, 0);
        ApplyReturnedCardState(inst, srcState);
        inst.currentAttack = Mathf.Max(0, inst.baseAttack);
        inst.currentHealth = Mathf.Max(0, inst.baseHealth);
        inst.currentMaxHealth = Mathf.Max(0, inst.baseMaxHealth);
        inst.currentTier = inst.baseTier;
        inst.tempAttackBoost = 0;
        inst.tempHealthBoost = 0;
        inst.handledReturnToHand = false;
        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        if (display != null) display.RefreshWithInstance(inst);
        handCards.Add(card);
        CardView cv = card.GetComponent<CardView>();
        if (cv != null) { cv.handManager = handManager; handManager?.RegisterCard(cv); }
    }

    static void ApplyReturnedCardState(CardInstance inst, string state)
    {
        if (string.IsNullOrEmpty(state)) return;
        string[] parts = state.Split('|');
        if (parts.Length < 1) return;
        // copyCount
        if (int.TryParse(parts[0], out int cc)) inst.mindScholarCopyCount = cc;
        // copiedTraits (;; separated)
        inst.mindScholarCopiedTraits = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
            ? new List<string>(parts[1].Split(new[] { ";;" }, StringSplitOptions.None))
            : new List<string>();
        // triggeredKeys (;; separated)
        inst.mindScholarTriggeredKeys = parts.Length > 2 && !string.IsNullOrEmpty(parts[2])
            ? new List<string>(parts[2].Split(new[] { ";;" }, StringSplitOptions.None))
            : new List<string>();
        // grantedTraitTexts (;; separated) + call GrantTrait for each
        if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
        {
            foreach (string t in parts[3].Split(new[] { ";;" }, StringSplitOptions.None))
            {
                if (!string.IsNullOrEmpty(t))
                {
                    inst.grantedTraitTexts.Add(t);
                    inst.GrantTrait(t);
                }
            }
        }
    }

    /// <summary>服务端→远端客户端：销毁指定槽位的板面模型。serverSlot 为服务端坐标(0-11)。</summary>
    [TargetRpc]
    public void TargetDestroyCard(NetworkConnectionToClient target, int serverSlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        // 服务端坐标 → 客户端本地坐标：服务端 0-5 = 客户端敌方(0-5)，服务端 6-11 = 客户端己方(6-11)
        // TargetRpc 接收端是远端，需要镜像映射：server 0-5 → client 6-11，server 6-11 → client 0-5
        int clientSlot = serverSlot >= 6 ? serverSlot - 6 : serverSlot + 6;
        BoardSlot slot = bm.GetSlot(clientSlot);
        if (slot?.currentCard3D != null)
        {
            // 清除附着在此槽位的附着模型
            for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
            {
                var am = bm.attachedModels[i];
                if (am == null) { bm.attachedModels.RemoveAt(i); continue; }
                var aci = am.GetComponent<Card3DInstance>()?.cardInstance;
                if (aci != null && aci.isAttached && aci.hostSlotID == clientSlot)
                {
                    if (aci.isAncientFairy) { bm.attachedModels.RemoveAt(i); BoardSlot._fairyPending.Add(am); }
                    else { Destroy(am); bm.attachedModels.RemoveAt(i); }
                }
            }
            Destroy(slot.currentCard3D);
            slot.SetCard(null);
        }
    }

    /// <summary>服务端→远端：委托远程玩家进行目标选择。</summary>
    [TargetRpc]
    public void TargetRequestSelection(NetworkConnectionToClient target, int targetType, int ownerSlotLocal)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) { CmdSelectionResult(-1); return; }
        SelectionManager.Instance.BeginSelection((TargetType)targetType, (s) =>
        {
            CmdSelectionResult(s != null ? s.slotID : -1);
        });
    }

    /// <summary>远端→服务器：远程玩家选完目标，服务器由NotifyRemoteSelectionDone解除阻塞。</summary>
    [Command]
    public void CmdSelectionResult(int selectedLocalSlot)
    {
        int serverSlot = isLocalPlayer ? selectedLocalSlot : (selectedLocalSlot >= 6 ? selectedLocalSlot - 6 : selectedLocalSlot + 6);
        BoardSlot.NotifyRemoteSelectionDone(serverSlot);
        BoardSlot.NotifyMartyrDone(serverSlot);
    }

    /// <summary>客户端→服务器：纯客户端放置卡牌后委托服务器执行进场效果。</summary>
    [Command]
    public void CmdStartEnterEffect(int clientSlotID, string templateID, string instanceID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        int serverSlot = isLocalPlayer ? clientSlotID : (clientSlotID >= 6 ? clientSlotID - 6 : clientSlotID + 6);
        BoardSlot slot = bm.GetSlot(serverSlot);
        if (slot?.currentCard3D == null) return;
        CardInstance inst = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (inst != null && template != null && template.hasOnEnter)
            slot.StartCoroutine(slot.StartOnEnterEffect(template, inst));
    }

    // ═══════════════════════════════════════════════════════════════════
    // 深海恶物(01338) 反击选择委托
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>服务端→远端客户端：你的01338死了，选一个敌方格子施加debuff。</summary>
    [TargetRpc]
    public void TargetDeepSeaRevengeSelect(NetworkConnectionToClient target, int serverDeadSlotID)
    {
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) { CmdDeepSeaRevengeResult(-1); return; }

        BoardSlot.isStrengtheningSlot = true;
        // 选择敌方格子（从本地视角 0-5 = 敌方）
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            BoardSlot.isStrengtheningSlot = false;
            int result = (s != null && !s.isBlocked) ? s.slotID : -1;
            CmdDeepSeaRevengeResult(result);
        });
    }

    /// <summary>远端→服务器：01338 反击选择了 localSlot（远端本地坐标）。</summary>
    [Command]
    public void CmdDeepSeaRevengeResult(int localSlot)
    {
        // 映射到服务端坐标
        int serverSlot = localSlot < 0 ? -1 : (isLocalPlayer ? localSlot : (localSlot >= 6 ? localSlot - 6 : localSlot + 6));
        BoardSlot.NotifyDeepSeaRevengeDone(serverSlot);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 01527 为己方一召唤物+2+1 反击选择委托
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>服务端→远端客户端：你的01527死了，选一个己方召唤物+2+1。</summary>
    [TargetRpc]
    public void TargetAllyBuffRevengeSelect(NetworkConnectionToClient target, int serverDeadSlotID)
    {
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) { CmdAllyBuffRevengeResult(-1); return; }

        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            BoardSlot.isStrengtheningSlot = false;
            int result = (s != null && s.currentCard3D != null) ? s.slotID : -1;
            CmdAllyBuffRevengeResult(result);
        });
    }

    /// <summary>远端→服务器：01527 反击选择了 localSlot（远端本地坐标）。</summary>
    [Command]
    public void CmdAllyBuffRevengeResult(int localSlot)
    {
        int serverSlot = localSlot < 0 ? -1 : (isLocalPlayer ? localSlot : (localSlot >= 6 ? localSlot - 6 : localSlot + 6));
        BoardSlot.NotifyAllyBuffRevengeDone(serverSlot);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 01344 诅咒女巫：抛置→敌方攻击力永久-2（纯客户端→服务器权威）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>纯客户端→服务器：01344 抛置对敌方槽位施加攻击力永久-2。</summary>
    [Command]
    public void CmdDiscardDebuff01344(int localTargetSlot)
    {
        // 远程客户端 localSlot 0-5 → 服务端坐标 6-11（敌方从远程视角=主机己方）
        int serverSlot = isLocalPlayer ? localTargetSlot : (localTargetSlot + 6);
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardSlot target = bm?.GetSlot(serverSlot);
        if (target != null)
        {
            DiscardHandlers.Apply01344Debuff(target);
            BoardSyncManager.MarkDirty();
        }
    }

    /// <summary>客户端→服务器：01346 士兵抛置为己方一召唤物恢复3生命值。</summary>
    [Command]
    public void CmdDiscardHeal01346(int localTargetSlot)
    {
        int serverSlot = isLocalPlayer ? localTargetSlot : (localTargetSlot >= 6 ? localTargetSlot - 6 : localTargetSlot + 6);
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardSlot target = bm?.GetSlot(serverSlot);
        if (target?.currentCard3D != null)
        {
            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
            t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Minion);
            t3d?.UpdateValues();
            BoardSyncManager.MarkDirty();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 01347 荣誉侍者：退场→对敌方造成2伤害 目标选择委托
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>服务端→远端客户端：你的01347死了，选一个敌方随从造成2伤害。</summary>
    [TargetRpc]
    public void TargetHonorAttendantExitSelect(NetworkConnectionToClient target, int serverDeadSlotID)
    {
        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            BoardSlot.isStrengtheningSlot = false;
            int result = (s != null && s.currentCard3D != null) ? s.slotID : -1;
            CmdHonorAttendantExitResult(result);
        });
    }

    /// <summary>远端→服务器：01347 退场伤害选择了 localSlot（远端本地坐标）。</summary>
    [Command]
    public void CmdHonorAttendantExitResult(int localSlot)
    {
        int serverSlot = localSlot < 0 ? -1 : (isLocalPlayer ? localSlot : (localSlot >= 6 ? localSlot - 6 : localSlot + 6));
        BoardSlot.NotifyHonorAttendantExitDone(serverSlot);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 01347 荣誉侍者：主动退场→+2能量+看对手手牌+弃邪恶法术（遵循01316窃贼模式）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>纯客户端→服务器：请求执行荣誉侍者主动退场流程。</summary>
    [Command]
    public void CmdRequestHonorAttendantActiveExit(int slotID)
    {
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        NetworkPlayer oppNp = BoardManager.GetOpponentPlayer(slotID);
        if (owner == null) return;
        owner.AddEnergy(2);
        StartCoroutine(WaitAndSendHonorAttendantHand(slotID, owner, oppNp));
    }

    IEnumerator WaitAndSendHonorAttendantHand(int slotID, NetworkPlayer owner, NetworkPlayer oppNp)
    {
        List<string> handData = new List<string>();

        if (oppNp == Local || oppNp == null)
        {
            foreach (var card in Local.handCards)
            {
                if (card == null) continue;
                var ci = card.GetComponent<CardInstance>();
                if (ci != null && !string.IsNullOrEmpty(ci.templateID))
                    handData.Add($"{ci.templateID}|{ci.instanceID}|{ci.currentCost}|{ci.currentAttack}|{ci.currentHealth}|{ci.currentMaxHealth}|{ci.currentTier}|{ci.prefixes ?? ""}|{(ci.hasShield ? "1" : "0")}|{(ci.poisoned ? "1" : "0")}");
            }
        }
        else
        {
            _handReportDone = false;
            oppNp.TargetRequestHandReport(oppNp.connectionToClient);
            yield return new WaitWhile(() => !_handReportDone);
            foreach (var card in oppNp.handCards)
            {
                if (card == null) continue;
                var ci = card.GetComponent<CardInstance>();
                if (ci != null && !string.IsNullOrEmpty(ci.templateID))
                    handData.Add($"{ci.templateID}|{ci.instanceID}|{ci.currentCost}|{ci.currentAttack}|{ci.currentHealth}|{ci.currentMaxHealth}|{ci.currentTier}|{ci.prefixes ?? ""}|{(ci.hasShield ? "1" : "0")}|{(ci.poisoned ? "1" : "0")}");
            }
        }

        Local.TargetShowHonorAttendantHand(owner.connectionToClient, handData.ToArray(), slotID);

        BoardSlot._honorAttendantDone = false;
        yield return new WaitWhile(() => !BoardSlot._honorAttendantDone);

        foreach (string entry in handData)
        {
            string[] parts = entry.Split('|');
            if (parts.Length < 2) continue;
            string tid = parts[0];
            string iid = parts[1];
            CardData td = CardDatabase.Instance?.GetTemplate(tid);
            if (td != null && (td.spellType & SpellType.Evil) != 0)
            {
                if (oppNp == Local || oppNp == null)
                    RemoveCardFromLocalHand(iid);
                else
                    oppNp.TargetRemoveHandCard(oppNp.connectionToClient, iid);
            }
        }

        owner.TargetHonorAttendantComplete(owner.connectionToClient);
        BoardSyncManager.MarkDirty();
    }

    /// <summary>服务器→客户端：展示对手手牌用于荣誉侍者弹窗。</summary>
    [TargetRpc]
    public void TargetShowHonorAttendantHand(NetworkConnectionToClient target, string[] handData, int slotID)
    {
        List<CardInstance> cards = new List<CardInstance>();
        foreach (string entry in handData)
        {
            string[] parts = entry.Split('|');
            if (parts.Length < 2) continue;
            var go = new GameObject("HonorCard");
            var ci = go.AddComponent<CardInstance>();
            ci.templateID = parts[0];
            ci.instanceID = parts[1];
            if (parts.Length > 2 && int.TryParse(parts[2], out int v)) ci.currentCost = v;
            if (parts.Length > 3 && int.TryParse(parts[3], out v)) ci.currentAttack = v;
            if (parts.Length > 4 && int.TryParse(parts[4], out v)) ci.currentHealth = v;
            if (parts.Length > 5 && int.TryParse(parts[5], out v)) ci.currentMaxHealth = v;
            if (parts.Length > 6 && int.TryParse(parts[6], out v)) ci.currentTier = v;
            if (parts.Length > 7) ci.prefixes = parts[7];
            if (parts.Length > 8) ci.hasShield = parts[8] == "1";
            if (parts.Length > 9) ci.poisoned = parts[9] == "1";
            cards.Add(ci);
        }

        CardDisplayPanel.Instance.multiSelect = false;
        int capturedSlot = slotID;
        CardDisplayPanel.Instance.Show(cards, _ => true, "确认");

        ConfirmSelectionButton.Instance?.gameObject.SetActive(true);
        ConfirmSelectionButton.Instance?.Show(() =>
        {
            Local.CmdConfirmHonorAttendant();
            foreach (var c in cards) if (c != null) Destroy(c.gameObject);
            CardDisplayPanel.Instance.Hide();
        });
    }

    /// <summary>客户端→服务器：荣誉侍者弹窗确认。</summary>
    [Command]
    public void CmdConfirmHonorAttendant()
    {
        BoardSlot._honorAttendantDone = true;
    }

    /// <summary>服务器→客户端：荣誉侍者流程完成。</summary>
    [TargetRpc]
    public void TargetHonorAttendantComplete(NetworkConnectionToClient target)
    {
        BoardSlot._honorAttendantDone = true;
    }

    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Remove a counter model after it's been triggered/expired on the server.</summary>
    [TargetRpc]
    public void TargetRemoveCounter(NetworkConnectionToClient target, string templateID, string listType)
    {
        CounterManager cm = CounterManager.Instance;
        if (cm == null) return;

        var list = listType == "mine" ? cm.myCounters : cm.enemyCounters;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].template.templateID == templateID)
            {
                if (list[i].model != null) Destroy(list[i].model);
                list.RemoveAt(i);
                Debug.Log($"[NetworkPlayer] TargetRemoveCounter: removed {templateID} from {listType}");
                return;
            }
        }
    }

    // ========== 无赖(01309) 跨端退场召唤 ==========

    /// <summary>服务端→远端客户端：你的01309死了，在你本地执行选择召唤。</summary>
    [TargetRpc]
    public void TargetRogueDeathEffect(NetworkConnectionToClient target, int originalDeadSlotID)
    {
        BoardSlot slot = FindObjectOfType<BoardSlot>();
        if (slot != null)
            slot.StartCoroutine(slot.RogueSummonRemote());
    }

    /// <summary>远端客户端完成召唤后通知服务端解除阻塞。</summary>
    [Command]
    public void CmdRogueDone()
    {
        BoardSlot.NotifyRogueRpcDone();
    }

    /// <summary>服务端→远端客户端：运行你己方的交互式先手。</summary>
    [TargetRpc]
    public void TargetRunRemoteFirstStrikes(NetworkConnectionToClient target)
    {
        BoardSlot slot = FindObjectOfType<BoardSlot>();
        if (slot != null)
            slot.StartCoroutine(slot.RunRemoteFirstStrikes());
    }

    /// <summary>服务端→远端客户端：交换己方（服务端6-11）两个槽位的卡牌。
    /// slotA/slotB 已映射为远端视角索引（0-5）。</summary>
    [TargetRpc]
    public void TargetSwapCards(NetworkConnectionToClient target, int slotA, int slotB)
    {
        BoardManager.SwapCards(slotA, slotB);
    }

    /// <summary>客户端上报交换请求——同时支持 ally(6-11) 和 enemy(0-5) 槽位映射。</summary>
    [Command]
    public void CmdSwapCards(int slotA, int slotB)
    {
        int serverA = isLocalPlayer ? slotA : (slotA >= 6 ? slotA - 6 : slotA + 6);
        int serverB = isLocalPlayer ? slotB : (slotB >= 6 ? slotB - 6 : slotB + 6);
        BoardManager.SwapCards(serverA, serverB);
        BoardSyncManager.MarkDirty();
    }

    /// <summary>远端客户端完成全部交互式先手后通知服务端。</summary>
    [Command]
    public void CmdRemoteFirstStrikeDone()
    {
        BoardSlot.NotifyRemoteFirstStrikeDone();
    }

    // ═══════════════════════════════════════════════════════════════
    // 手牌上报：客户端 → 服务端，确保服务端始终有双方手牌数据
    // ═══════════════════════════════════════════════════════════════

    public static bool _handReportDone;

    /// <summary>服务端要求某个客户端上报手牌数据。</summary>
    [TargetRpc]
    public void TargetRequestHandReport(NetworkConnectionToClient target)
    {
        List<string> list = new List<string>();
        foreach (var card in Local.handCards)
        {
            if (card == null) continue;
            var ci = card.GetComponent<CardInstance>();
            if (ci != null && !string.IsNullOrEmpty(ci.templateID))
                list.Add($"{ci.templateID}|{ci.instanceID}");
        }
        Local.CmdReportHand(list.ToArray());
    }

    /// <summary>客户端上报手牌给服务端，服务端重建轻量跟踪数据。</summary>
    [Command]
    public void CmdReportHand(string[] handData)
    {
        // 仅 Remote 侧用：this 是 Remote，handCards 存轻量跟踪，可以安全清空重建
        // Host 侧由 ServerThiefFlow 直接读 Local.handCards，不走此路
        handCards.Clear();
        foreach (string entry in handData)
        {
            string[] parts = entry.Split('|');
            if (parts.Length < 2) continue;
            var data = CardDatabase.Instance?.GetTemplate(parts[0]);
            if (data != null) AddServerSideCard(data, parts[1]);
        }
        _handReportDone = true;
    }

    /// <summary>服务端告知客户端：从手牌移除指定 instanceID 的卡。</summary>
    [TargetRpc]
    public void TargetRemoveHandCard(NetworkConnectionToClient target, string instanceID)
    {
        RemoveCardFromLocalHand(instanceID);
    }

    // ═══════════════════════════════════════════════════════
    // 窃贼主动退场 RPC 链：客户端→服务器→对手客户端→服务器→本客户端
    // ═══════════════════════════════════════════════════════

    public static bool _thiefDone;
    public static string[] _thiefResult;

    /// <summary>客户端→服务器：请求对手手牌用于窃贼弹窗。</summary>
    [Command]
    public void CmdRequestThiefHand(int slotID)
    {
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        NetworkPlayer oppNp = BoardManager.GetOpponentPlayer(slotID);
        if (oppNp == null || oppNp == owner) return;
        StartCoroutine(WaitAndSendThiefHand(slotID, owner, oppNp));
    }

    IEnumerator WaitAndSendThiefHand(int slotID, NetworkPlayer owner, NetworkPlayer oppNp)
    {
        List<string> handData = new List<string>();

        if (oppNp == Local)
        {
            // 对手是主机：直接读 Local.handCards，不通过网络
            foreach (var card in Local.handCards)
            {
                if (card == null) continue;
                var ci = card.GetComponent<CardInstance>();
                if (ci != null && !string.IsNullOrEmpty(ci.templateID))
                    handData.Add($"{ci.templateID}|{ci.instanceID}|{ci.currentCost}|{ci.currentAttack}|{ci.currentHealth}|{ci.currentMaxHealth}|{ci.currentTier}|{ci.prefixes ?? ""}|{(ci.hasShield ? "1" : "0")}|{(ci.poisoned ? "1" : "0")}");
            }
        }
        else
        {
            // 对手是远端：请求上报然后读 oppNp.handCards
            _handReportDone = false;
            oppNp.TargetRequestHandReport(oppNp.connectionToClient);
            yield return new WaitWhile(() => !_handReportDone);
            foreach (var card in oppNp.handCards)
            {
                if (card == null) continue;
                var ci = card.GetComponent<CardInstance>();
                if (ci != null && !string.IsNullOrEmpty(ci.templateID))
                    handData.Add($"{ci.templateID}|{ci.instanceID}|{ci.currentCost}|{ci.currentAttack}|{ci.currentHealth}|{ci.currentMaxHealth}|{ci.currentTier}|{ci.prefixes ?? ""}|{(ci.hasShield ? "1" : "0")}|{(ci.poisoned ? "1" : "0")}");
            }
        }

        // 发送给窃贼客户端展示
        Local.TargetShowThiefHand(owner.connectionToClient, handData.ToArray(), slotID);

        // 等待客户端选完回报 — _thiefDone 在服务器进程被 CmdConfirmThiefSteal 设置
        _thiefDone = false;
        yield return new WaitWhile(() => !_thiefDone);

        // 处理后通知客户端结束（解除客户端的 WaitWhile 阻塞）
        owner.TargetThiefComplete(owner.connectionToClient);

        if (_thiefResult != null && _thiefResult.Length >= 2)
        {
            string stolenTID = _thiefResult[0];
            string stolenIID = _thiefResult[1];

            // 从对手删除手牌
            if (oppNp == Local)
                RemoveCardFromLocalHand(stolenIID);
            else
                oppNp.TargetRemoveHandCard(oppNp.connectionToClient, stolenIID);

            CardData template = CardDatabase.Instance?.GetTemplate(stolenTID);
            if (template != null) AddCardToHandForPlayer(owner, template);
        }
    }

    public static void RemoveCardFromLocalHand(string instanceID)
    {
        var cards = Local?.handCards;
        if (cards == null) return;
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var ci = cards[i]?.GetComponent<CardInstance>();
            if (ci != null && ci.instanceID == instanceID)
            {
                CardView cv = cards[i].GetComponent<CardView>();
                if (cv != null) FindObjectOfType<HandManager>()?.RemoveCard(cv);
                else Destroy(cards[i]);
                cards.RemoveAt(i);
                FindObjectOfType<HandManager>()?.RefreshLayout(true);
                return;
            }
        }
    }

    /// <summary>服务器→请求客户端：展示对手手牌选单。</summary>
    [TargetRpc]
    public void TargetShowThiefHand(NetworkConnectionToClient target, string[] handData, int slotID)
    {
        if (handData == null || handData.Length == 0) return;

        List<CardInstance> cards = new List<CardInstance>();
        foreach (string entry in handData)
        {
            string[] p = entry.Split('|');
            if (p.Length < 2) continue;
            var go = new GameObject("ThiefCard");
            var ci = go.AddComponent<CardInstance>();
            ci.templateID = p[0];
            ci.instanceID = p[1];
            if (p.Length > 2 && int.TryParse(p[2], out int v)) ci.currentCost = v;
            if (p.Length > 3 && int.TryParse(p[3], out v)) ci.currentAttack = v;
            if (p.Length > 4 && int.TryParse(p[4], out v)) ci.currentHealth = v;
            if (p.Length > 5 && int.TryParse(p[5], out v)) ci.currentMaxHealth = v;
            if (p.Length > 6 && int.TryParse(p[6], out v)) ci.currentTier = v;
            if (p.Length > 7) ci.prefixes = p[7];
            if (p.Length > 8) ci.hasShield = p[8] == "1";
            if (p.Length > 9) ci.poisoned = p[9] == "1";
            cards.Add(ci);
        }

        int capturedSlot = slotID;

        CardDisplayPanel.Instance.multiSelect = false;
        CardDisplayPanel.Instance.ShowWithCallback(cards, ci => true, () =>
        {
            var sel = CardDisplayPanel.Instance.GetSelectedCard();
            if (sel != null)
                Local.CmdConfirmThiefSteal(sel.templateID, sel.instanceID, capturedSlot);
            else
                Local.CmdConfirmThiefSteal("", "", capturedSlot);

            foreach (var t in FindObjectsOfType<GameObject>())
                if (t.name == "ThiefCard") Destroy(t);
            CardDisplayPanel.Instance.Hide();
        }, "窃取");
    }

    /// <summary>客户端→服务器：确认窃取选择。</summary>
    [Command]
    public void CmdConfirmThiefSteal(string templateID, string instanceID, int slotID)
    {
        _thiefResult = string.IsNullOrEmpty(instanceID) ? null : new[] { templateID, instanceID };
        _thiefDone = true;
    }

    /// <summary>服务器→客户端：窃贼流程完成，解除客户端阻塞。</summary>
    [TargetRpc]
    public void TargetThiefComplete(NetworkConnectionToClient target)
    {
        _thiefDone = true;
    }

    // ═══════════════════════════════════════════════════════
    // Registry 通用 RPC 管道
    // ═══════════════════════════════════════════════════════

    /// <summary>服务端增量推送 Registry delta 到远端客户端。</summary>
    [TargetRpc]
    public void RpcSyncRegistry(NetworkConnectionToClient target, string payload)
    {
        RegistrySyncManager.Instance?.ApplyDelta(payload);
    }
}
