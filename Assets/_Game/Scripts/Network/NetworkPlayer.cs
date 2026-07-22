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
    public void CmdPlayCard(string templateID, int slotID, int overrideAtk, int overrideHP, int overrideMaxHP, string instanceID)
    {
        Debug.Log($"[NetworkPlayer] CmdPlayCard: templateID={templateID}, slotID={slotID}, netId={netId}");
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
                        if (slot.currentCard3D != null) Destroy(slot.currentCard3D);
                        Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(enemySlot);
                        GameObject model = Instantiate(template.prefab3D, pos, Quaternion.Euler(0, 180, 0));
                        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
                        if (c3d != null)
                        {
                            CardInstance ci = model.AddComponent<CardInstance>();
                            ci.InitFromTemplate(template, 0, instanceID);

                            // Apply enter-effect stat overrides before first sync
                            if (overrideAtk >= 0) ci.currentAttack = overrideAtk;
                            if (overrideHP >= 0) ci.currentHealth = overrideHP;
                            if (overrideMaxHP >= 0) ci.currentMaxHealth = overrideMaxHP;

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
                    TargetSpawnCard3D(other, templateID, slotID, overrideAtk, overrideHP, overrideMaxHP, instanceID);
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

        handCardCount = handCards.Count;
        // Registry
        RegistrySyncManager.Instance?.UpdateCard(inst, this == Local ? 0 : 1, CardZone.Hand, -1);
    }

    public void AddCardToHandFromInstance(CardData template, CardInstance oldInstance, bool isEnemy = false)
    {
        if (isEnemy && Remote == null) return;
        if (!isEnemy && Local == null) return;

        int maxSize = isEnemy ? Remote.maxHandSize : Local.maxHandSize;
        Transform targetHandArea = isEnemy ? Remote.handArea : Local.handArea;
        GameObject prefab = isEnemy ? Remote.GetCardPrefab(template.cardType) : GetCardPrefab(template.cardType);

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
        inst.currentAttack = inst.baseAttack;
        inst.currentHealth = inst.baseHealth;
        inst.currentMaxHealth = inst.baseMaxHealth;
        inst.currentTier = inst.baseTier;
        inst.tempAttackBoost = 0;
        inst.tempHealthBoost = 0;
        inst.handledReturnToHand = false;

        if (inst.energyReaperDiscounted && !IsEnergyReaperOnField())
            inst.energyReaperDiscounted = false;
        if (inst.templateID == "01524")
        {
            inst.scrollCorePhaseCount = 0;
            inst.currentCost = 0;
        }
        if (inst.merchantDiscounted && !IsMerchantOnField())
        {
            inst.merchantDiscounted = false;
            inst.currentCost += 1;
        }
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

    // ========== Server-side card tracking helpers ==========

    /// <summary>Validate this player should be acting in the current server-side phase.</summary>
    bool IsMyTurnOnServer(TurnManager tm)
    {
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
        int overrideAtk, int overrideHP, int overrideMaxHP, string instanceID)
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

        // Skip if slot already has this card (BoardSyncManager already handled it)
        if (slot.currentCard3D != null)
            return;

        Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(enemySlot);
        GameObject model = Instantiate(template.prefab3D, pos, Quaternion.Euler(0, 180, 0));
        model.name = templateID + "_enemy";

        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
        if (c3d != null)
        {
            CardInstance ci = model.AddComponent<CardInstance>();
            ci.InitFromTemplate(template, 0, instanceID);

            // Apply enter-effect stat overrides
            if (overrideAtk >= 0) ci.currentAttack = overrideAtk;
            if (overrideHP >= 0) ci.currentHealth = overrideHP;
            if (overrideMaxHP >= 0) ci.currentMaxHealth = overrideMaxHP;

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

            // ── 槽位标记（始终应用，包含 isBlocked/prisonBlocked 等）──
            if (parts.Length >= 4)
            {
                string flags = parts[parts.Length - 4];
                if (flags.Length >= 4)
                {
                    slot.isBlocked = flags[0] == '1';
                    slot.prisonBlocked = flags[1] == '1';
                    slot.hasPlague = flags[2] == '1';
                    slot.hasSpotlight = flags[3] == '1';
                    slot.SyncVisual();
                }
                if (int.TryParse(parts[parts.Length - 3], out int prc)) slot.plagueRoundCount = prc;
                if (int.TryParse(parts[parts.Length - 2], out int stb)) slot.spotlightTierBoost = stb;
                if (int.TryParse(parts[parts.Length - 1], out int boost)) slot.slotTempAttackBoost = boost;
            }

            // ── 属性/模板数据更新（全部 12 槽通用——客户端上报的对方卡牌属性是"我刚打过的"，信任它）──
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
                    if (p.Length > 1 && int.TryParse(p[1], out v)) ci.currentHealth = v;
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
                        else
                        {
                            // 无服务端临时修改：远程上报值可能是 RunRemoteFirstStrikes
                            // 的 debuff 结果，正常保存原始攻击力后应用。
                            if (ci.originalAttackBeforeDebuff <= 0 && v != ci.currentAttack)
                                ci.originalAttackBeforeDebuff = ci.currentAttack;
                            ci.currentAttack = v;
                        }
                    }
                    if (p.Length > 3 && int.TryParse(p[3], out v)) ci.currentMaxHealth = v;
                    if (p.Length > 4 && int.TryParse(p[4], out v)) ci.baseAttack = v;
                    if (p.Length > 5 && int.TryParse(p[5], out v)) ci.baseHealth = v;
                    if (p.Length > 6 && int.TryParse(p[6], out v)) ci.baseMaxHealth = v;
                    if (p.Length > 7 && int.TryParse(p[7], out v)) ci.currentCost = v;
                    if (p.Length > 8 && int.TryParse(p[8], out v)) ci.currentTier = v;
                    if (p.Length > 9 && int.TryParse(p[9], out v)) ci.baseTier = v;
                    if (p.Length > 10) ci.hasShield = (p[10] == "1");
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

    /// <summary>附着物 diff 更新：已有→移坐标，不存在→创建。服务端从不基于客户端上报删除附着模型。</summary>
    static void ApplyAttachDiff(BoardManager bm, string attachBlock, bool isLocalPlayer)
    {
        bm.attachedModels.RemoveAll(a => a == null);

        var incoming = new System.Collections.Generic.List<(string tid, int hs, int order)>();
        if (!string.IsNullOrEmpty(attachBlock))
        {
            foreach (var item in attachBlock.Split(new[] { "||" }, System.StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(item)) continue;
                var p = item.Split('|');
                if (p.Length < 3) continue;
                if (!int.TryParse(p[1], out int h) || !int.TryParse(p[2], out int o)) continue;
                incoming.Add((p[0], h, o));
            }
        }

        // 服务端收到客户端上报时，不删除现有附着模型（客户端可能尚未同步到最新状态）
        bool isServerProcessingClientReport = Mirror.NetworkServer.active && !isLocalPlayer;

        var slotTids = new System.Collections.Generic.HashSet<string>();
        for (int si = 0; si < 12; si++)
        {
            var sci = bm.GetSlot(si)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (sci != null && !string.IsNullOrEmpty(sci.templateID)) slotTids.Add(sci.templateID);
        }

        // 仅在非"服务端处理客户端上报"时删除 incoming 中不存在的附着物
        if (!isServerProcessingClientReport)
        {
            for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
            {
                var obj = bm.attachedModels[i];
                if (obj == null) { bm.attachedModels.RemoveAt(i); continue; }
                var ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci == null || !ci.isAttached) { bm.attachedModels.RemoveAt(i); continue; }
                // incoming hs is CLIENT coordinate; ci.hostSlotID is SERVER coordinate.
                // Map incoming to server space for exact comparison.
                bool stillExists = incoming.Exists(x => {
                    int xServerHS = isLocalPlayer ? x.hs : (x.hs >= 6 ? x.hs - 6 : x.hs + 6);
                    return x.tid == ci.templateID && xServerHS == ci.hostSlotID && x.order == ci.attachOrder;
                });
                if (!stillExists) { Destroy(obj); bm.attachedModels.RemoveAt(i); }
            }
        }

        var hm = FindObjectOfType<HandManager>();
        foreach (var (tid, hs, o) in incoming)
        {
            if (slotTids.Contains(tid)) continue;
            int mapped = isLocalPlayer ? hs : (hs >= 6 ? hs - 6 : hs + 6);

            GameObject existing = null;
            foreach (var obj in bm.attachedModels)
            {
                var ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == tid && ci.attachOrder == o && ci.hostSlotID == mapped)
                { existing = obj; break; }
            }

            if (existing != null)
            {
                existing.transform.position = HandManager.GetAttachWorldPos(mapped, o);
                var eci = existing.GetComponent<Card3DInstance>()?.cardInstance;
                if (eci != null) eci.hostSlotID = mapped;
                continue;
            }

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
                int hp2, atk2, mh2, ba2, bh2, bmh2, cost2, tier2, bt2;
                if (p.Length > 1 && int.TryParse(p[1], out hp2)) ci.currentHealth = hp2;
                if (p.Length > 2 && int.TryParse(p[2], out atk2)) ci.currentAttack = atk2;
                if (p.Length > 3 && int.TryParse(p[3], out mh2)) ci.currentMaxHealth = mh2;
                if (p.Length > 4 && int.TryParse(p[4], out ba2)) ci.baseAttack = ba2;
                if (p.Length > 5 && int.TryParse(p[5], out bh2)) ci.baseHealth = bh2;
                if (p.Length > 6 && int.TryParse(p[6], out bmh2)) ci.baseMaxHealth = bmh2;
                if (p.Length > 7 && int.TryParse(p[7], out cost2)) ci.currentCost = cost2;
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
        GameObject model = Object.Instantiate(t.prefab3D, pos, Quaternion.Euler(0, 180, 0));
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

        // Temporary CardInstance only needed for PlayCounter to get templateID.
        // PlayCounter's isMine=false path does NOT copy card data — it just
        // adds the entry to enemyCounters. Keep temp alive to avoid dangling ref.
        // 已知设计折衷：DontDestroyOnLoad 防止 PlayCounter 持有引用失效。
        // 后续可优化为 PlayCounter 完成回调后销毁 temp。
        GameObject temp = new GameObject("TempCounterCmd");
        CardInstance ci = temp.AddComponent<CardInstance>();
        ci.InitFromTemplate(template, 0);
        CounterManager.Instance?.PlayCounter(temp, false);
        DontDestroyOnLoad(temp);
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

    /// <summary>远端客户端→服务端：交换己方（远端6-11）两个槽位的卡牌。
    /// 服务端将其映射为对手侧（0-5）后执行交换。</summary>
    [Command]
    public void CmdSwapCards(int slotA, int slotB)
    {
        BoardManager.SwapCards(slotA - 6, slotB - 6);
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

    static void RemoveCardFromLocalHand(string instanceID)
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
