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
        while (waited < 5f && Remote == null)
        {
            yield return new WaitForSeconds(0.2f);
            waited += 0.2f;
            TrySetRemote();
        }
        if (Remote == null)
            Debug.LogError("[NetworkPlayer] Failed to find Remote after 5s!");
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

        int cost = 0;

        // Try to find card in server-side hand (tracking cards from AddServerSideCard)
        handCards.RemoveAll(c => c == null);
        GameObject serverCard = null;
        foreach (GameObject c in handCards)
        {
            CardInstance ci = c.GetComponent<CardInstance>();
            if (ci != null && ci.templateID == templateID) { serverCard = c; cost = ci.currentCost; break; }
        }

        // Fallback cost from template (for host where card was already removed locally)
        if (serverCard == null) cost = template.baseCost;

        if (currentEnergy < cost) return;

        // Deduct energy server-side
        currentEnergy -= cost;

        // Clean up server-side tracking card (if exists; host may have already removed it)
        if (serverCard != null)
        {
            handCards.Remove(serverCard);
            Destroy(serverCard);
            handCardCount = handCards.Count;
        }

        // Tell the requesting client to remove the card from its local hand
        // (only needed for pure client; host already handled locally)
        TargetRemoveCardFromHand(connectionToClient, templateID);

        // Summon: broadcast 3D model to the OTHER client
        if (template.cardType == CardType.Summon)
        {
            int modelSlotID = slotID;
            if (slotID < 0)
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                    for (int i = 6; i <= 11; i++)
                    {
                        BoardSlot s = bm.GetSlot(i);
                        if (s != null && !s.isBlocked && !s.hasCard) { modelSlotID = i; break; }
                    }
            }

            NetworkPlayer other = (this == NetworkPlayer.Local) ? NetworkPlayer.Remote : NetworkPlayer.Local;
            if (other != null && other.connectionToClient != null)
            {
                other.TargetSpawnCard3D(other.connectionToClient, templateID, modelSlotID);
                Debug.Log($"[NetworkPlayer] CmdPlayCard: spawned enemy model {templateID} at slot {modelSlotID} for netId={other.netId}");
            }
        }
        else if (template.cardType == CardType.Spell)
        {
            // Counter card: spawn at mirrored position on the other client
            NetworkPlayer other = (this == NetworkPlayer.Local) ? NetworkPlayer.Remote : NetworkPlayer.Local;
            if (other != null && other.connectionToClient != null && template.spellPrefab3D != null)
            {
                other.TargetSpawnCounterCard(other.connectionToClient, templateID);
                Debug.Log($"[NetworkPlayer] CmdPlayCard: spawned enemy counter {templateID} for netId={other.netId}");
            }
        }
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

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        BoardSlot slot = bm.GetSlot(slotID);
        if (slot == null) return;

        HandManager hm = FindObjectOfType<HandManager>();
        Vector3 worldPos = hm.GetSlotWorldPosition(slotID);

        GameObject model = Instantiate(template.prefab3D, worldPos, Quaternion.identity);
        model.name = templateID + "_enemy";
        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
        if (c3d != null)
        {
            CardInstance ci = model.AddComponent<CardInstance>();
            ci.InitFromTemplate(template, 0);
            c3d.cardInstance = ci;
            c3d.UpdateValues();
        }
        slot.SetCard(model);

        Card3DHover hover = model.GetComponent<Card3DHover>();
        if (hover != null) hover.SetEnemyView();

        Debug.Log($"[NetworkPlayer] TargetSpawnCard3D: {templateID} at slot {slotID}");
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

        // Create a temporary card object for CounterManager to use
        GameObject tempCard = new GameObject($"counter_{templateID}");
        CardInstance ci = tempCard.AddComponent<CardInstance>();
        ci.InitFromTemplate(template, 0);

        CounterManager.Instance?.PlayCounter(tempCard, false); // isMine=false → enemy position

        Destroy(tempCard);
        Debug.Log($"[NetworkPlayer] TargetSpawnCounterCard: {templateID}");
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
}
