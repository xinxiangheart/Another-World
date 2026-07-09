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
        _healthText = FindTMP("Health");
        _energyText = FindTMP("Energy");
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
        handArea = FindTransform("EnemyHandArea");
        handManager = FindObjectOfType<HandManager>();
        _healthText = FindTMP("EnemyHealthLabel");
        _energyText = FindTMP("EnemyEnergyLabel");
        RefreshUI();
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
        TargetReceiveCard(connectionToClient, data.templateID);
        TargetConfirmDraw(connectionToClient);

        // Server-side tracking: add a lightweight card so CmdPlayCard can find it
        AddServerSideCard(data);
    }

    [Command]
    public void CmdPlayCard(string templateID, int slotID)
    {
        Debug.Log($"[NetworkPlayer] CmdPlayCard: templateID={templateID}, slotID={slotID}, netId={netId}");
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm == null) return;
        if (!IsMyTurnOnServer(tm)) return;

        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template == null) return;

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
                            ci.InitFromTemplate(template, 0);
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
                    TargetSpawnCard3D(other, templateID, slotID);
            }
        }
        // Non-counter cards: trigger opponent's OnCardPlayed counters on server
        if ((template.spellType & SpellType.Counter) == 0)
        {
            bool hostPlayed = (this == NetworkPlayer.Local);
            CounterManager.Instance?.ServerCheckOnCardPlayed(template, hostPlayed);
        }

        // Always sync after placement so the other side sees the new model
        if (template.cardType == CardType.Summon)
            BoardSyncManager.MarkDirty();

        // Counter spell sync is handled entirely by CardDrag.OnEndDrag's counter branch
        // (TargetSpawnCounterCard for Host→Remote, CmdPlayCounter for Client→Server).
        // CmdPlayCard is NEVER called for counters — the counter path has an early return.
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
        if (isServer)
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
        if (isServer)
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
        if (isServer)
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
            if (isServer)
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
            instance.InitFromTemplate(data, GetCopyIndex(data.templateID));

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
        Debug.Log($"[NetworkPlayer] DrawCard: templateID={data.templateID}, handCount={handCardCount}");
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
            instance.InitFromTemplate(data, GetCopyIndex(data.templateID));

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
    }

    public void RemoveCardFromHand(GameObject card)
    {
        if (handCards.Contains(card))
        {
            handCards.Remove(card);
            Destroy(card);
            handCards.RemoveAll(c => c == null);
            FindObjectOfType<HandManager>()?.RefreshLayout(true);
            handCardCount = handCards.Count;
        }
    }

    GameObject GetCardPrefab(CardType cardType)
    {
        if (cardPrefab2D == null) cardPrefab2D = FindObjectOfType<Player>()?.cardPrefab2D;
        if (spellCardPrefab2D == null) spellCardPrefab2D = FindObjectOfType<Player>()?.spellCardPrefab2D;
        return cardType == CardType.Spell ? spellCardPrefab2D : cardPrefab2D;
    }

    public void AddCardToHand(CardData template)
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
        inst.InitFromTemplate(template, 0);

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
    public void AddServerSideCard(CardData data)
    {
        GameObject card = new GameObject($"ServerCard_{data.templateID}");
        CardInstance ci = card.AddComponent<CardInstance>();
        ci.InitFromTemplate(data, 0);
        handCards.Add(card);
        handCardCount = handCards.Count;
        Debug.Log($"[NetworkPlayer] AddServerSideCard: {data.templateID}, handCount={handCardCount}");
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
    public void TargetReceiveCard(NetworkConnectionToClient target, string templateID)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template != null)
        {
            AddCardToHand(template);
            Debug.Log($"[NetworkPlayer] TargetReceiveCard: {templateID}");
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
    /// </summary>
    [TargetRpc]
    public void TargetSpawnCard3D(NetworkConnectionToClient target, string templateID, int slotID)
    {
        CardData template = CardDatabase.Instance?.GetTemplate(templateID);
        if (template?.prefab3D == null) return;

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
            ci.InitFromTemplate(template, 0);
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
                // [死亡重生溯源] 修复生效验证：这里拦截的就是原来会复活死亡单位的上报。定位稳定后可删此日志。
                Debug.LogWarning($"[死亡重生溯源] 拦截陈旧上报，拒绝复活 tid={tid} 服务器槽={i} 相位={TurnManager.Instance?.currentPhase}");
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
                int hp2, atk2, mh2, cost2, tier2;
                if (p.Length > 1 && int.TryParse(p[1], out hp2)) ci.currentHealth = hp2;
                if (p.Length > 2 && int.TryParse(p[2], out atk2)) ci.currentAttack = atk2;
                if (p.Length > 3 && int.TryParse(p[3], out mh2)) ci.currentMaxHealth = mh2;
                if (p.Length > 4 && int.TryParse(p[4], out cost2)) ci.currentCost = cost2;
                if (p.Length > 5 && int.TryParse(p[5], out tier2)) ci.currentTier = tier2;
                if (p.Length > 6) ci.hasShield = (p[6] == "1");
                if (p.Length > 7) ci.silencedThisPhase = (p[7] == "1");
                if (p.Length > 8) ci.isAttached = (p[8] == "1");
                if (p.Length > 9) ci.poisoned = (p[9] == "1");
                if (p.Length > 10) ci.prefixes = p[10];
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
            foreach (var item in attachBlock.Split(new[] { "||" },
                System.StringSplitOptions.RemoveEmptyEntries))
            {
                var p = item.Split('|');
                if (p.Length < 3) continue;
                if (!int.TryParse(p[1], out int hs) || !int.TryParse(p[2], out int o)) continue;
                int serverHostSlot = isLocalPlayer ? hs : hs - 6; // client 6-11 → server 0-5 for Remote
                var t = CardDatabase.Instance?.GetTemplate(p[0]);
                if (t?.prefab3D == null || hm2 == null) continue;
                Vector3 hostPos = hm2.GetSlotWorldPosition(serverHostSlot);
                Vector3 attachPos = new Vector3(hostPos.x - 0.5f - o * 0.5f, hostPos.y,
                    hostPos.z + 0.1f + o * 0.1f);
                GameObject model = Instantiate(t.prefab3D, attachPos, Quaternion.Euler(0, 180, 0));
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
        if (bm == null) return;
        BoardSlot sa = bm.GetSlot(slotA);
        BoardSlot sb = bm.GetSlot(slotB);
        if (sa == null || sb == null) return;

        GameObject cardA = sa.currentCard3D;
        GameObject cardB = sb.currentCard3D;

        sa.SetCard(null);
        sb.SetCard(null);

        HandManager hm = FindObjectOfType<HandManager>();
        if (cardB != null)
        {
            cardB.transform.position = hm.GetSlotWorldPosition(slotA);
            sa.SetCard(cardB);
        }
        if (cardA != null)
        {
            cardA.transform.position = hm.GetSlotWorldPosition(slotB);
            sb.SetCard(cardA);
        }
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
        GameObject temp = new GameObject("TempCounterCmd");
        CardInstance ci = temp.AddComponent<CardInstance>();
        ci.InitFromTemplate(template, 0);
        CounterManager.Instance?.PlayCounter(temp, false);
        DontDestroyOnLoad(temp);
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
}
