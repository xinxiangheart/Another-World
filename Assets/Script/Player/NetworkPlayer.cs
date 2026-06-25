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

    [Header("UI")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI energyText;

    // ========== Mirror Lifecycle ==========

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"OnStartLocalPlayer: netId={netId}, isServer={isServer}");
        Local = this;
        currentHealth = maxHealth;
        currentEnergy = 0;
        _energyCanExceedLimit = false;
        UpdateUI();
        // Find UI references
        healthText = GameObject.Find("Health")?.GetComponent<TextMeshProUGUI>();
        energyText = GameObject.Find("Energy")?.GetComponent<TextMeshProUGUI>();
        handArea = GameObject.Find("HandArea")?.transform;
        UpdateUI();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"[NetworkPlayer] OnStartServer: netId={netId}");
        StartCoroutine(DelayedSetRemote());
    }

    IEnumerator DelayedSetRemote()
    {
        yield return new WaitForSeconds(0.3f);
        foreach (var conn in NetworkServer.connections.Values)
        {
            var player = conn.identity?.GetComponent<NetworkPlayer>();
            if (player != null && player != this)
            {
                Remote = player;
                Debug.Log($"[NetworkPlayer] Remote set: netId={Remote.netId}");
                break;
            }
        }
    }

    public override void OnStartClient()
    {
        UpdateUI();
    }

    // ========== UI ==========

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
        UpdateUI();
        Debug.Log($"[NetworkPlayer] Hand count changed: {oldValue} -> {newValue} (isLocal={isLocalPlayer})");
    }

    void OnHealthChanged(int oldValue, int newValue)
    {
        UpdateUI();
        if (newValue <= 0 && isServer)
            Debug.Log("[NetworkPlayer] Player died");
    }

    void OnEnergyChanged(int oldValue, int newValue)
    {
        UpdateUI();
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

    [ClientRpc]
    public void RpcYourTurnStart()
    {
        if (!isLocalPlayer) return;
        Debug.Log("[NetworkPlayer] Remote turn started - your turn is over");
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm != null && tm.currentPhase == TurnManager.TurnPhase.EnemyTurn)
        {
            // Remote player's "EnemyTurn" is local player's "MyTurn" from their perspective
            // Actually from remote's perspective: EnemyTurn means it's the other player's turn
            tm.SetPhaseFromNetwork(TurnManager.TurnPhase.MyTurn);
        }
    }

    // ========== Commands ==========

    [Command]
    public void CmdRequestDraw()
    {
        Debug.Log($"[NetworkPlayer] CmdRequestDraw from netId={netId}");
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm == null || tm.currentPhase != TurnManager.TurnPhase.MyTurn) return;

        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null && drawUI.GetRemainingDraws() <= 0) return;

        if (currentEnergy < 1) return;

        UseEnergy(1);
        DrawCard();
    }

    [Command]
    public void CmdPlayCard(string templateID, int slotID)
    {
        Debug.Log($"[NetworkPlayer] CmdPlayCard: templateID={templateID}, slotID={slotID}");

        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm == null || tm.currentPhase != TurnManager.TurnPhase.MyTurn) return;

        // Find the card in the player's hand
        foreach (GameObject card in handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.templateID == templateID)
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                BoardSlot slot = bm?.GetSlot(slotID);
                if (slot != null)
                {
                    HandManager hm = FindObjectOfType<HandManager>();
                    hm?.PlaceCardToSlot(slot, card);
                }
                break;
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

    // Event for card effects that need to intercept healing
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
            currentEnergy += amount;
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

        GameObject prefab = data.cardType == CardType.Spell ? spellCardPrefab2D : cardPrefab2D;
        GameObject card = Instantiate(prefab, handArea);
        CardInstance instance = card.GetComponent<CardInstance>();
        if (instance != null)
            instance.InitFromTemplate(data, GetCopyIndex(data.templateID));

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
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

        GameObject prefab = data.cardType == CardType.Spell ? spellCardPrefab2D : cardPrefab2D;
        GameObject card = Instantiate(prefab, handArea);
        CardInstance instance = card.GetComponent<CardInstance>();
        if (instance != null)
            instance.InitFromTemplate(data, GetCopyIndex(data.templateID));

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
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

    public void AddCardToHand(CardData template)
    {
        if (handCards.Count >= maxHandSize) return;

        GameObject prefab = template.cardType == CardType.Spell ? spellCardPrefab2D : cardPrefab2D;
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
        GameObject prefab = isEnemy ? Remote.cardPrefab2D : cardPrefab2D;

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

    // ========== Helper Methods ==========

    void EnableLocalInteraction(bool enabled)
    {
        if (handManager != null)
            handManager.SetHandAreaRaycast(enabled);
    }

    bool IsMerchantOnField()
    {
        var allAuras = GlobalEventManager.Instance?.GetAllAuras();
        if (allAuras == null) return false;
        foreach (var a in allAuras)
        {
            if (a is MerchantAura && a.IsActive()) return true;
        }
        return false;
    }

    bool IsEnergyReaperOnField()
    {
        var allAuras = GlobalEventManager.Instance?.GetAllAuras();
        if (allAuras == null) return false;
        foreach (var a in allAuras)
        {
            if (a is EnergyReaperAura && a.IsActive()) return true;
        }
        return false;
    }

    public bool IsMerchantOnFieldPublic() => IsMerchantOnField();
    public bool IsEnergyReaperOnFieldPublic() => IsEnergyReaperOnField();

    // ========== UI ==========

    public void UpdateUI()
    {
        if (healthText != null)
            healthText.text = isLocalPlayer ? $" {currentHealth}" : currentHealth.ToString();
        if (energyText != null)
            energyText.text = isLocalPlayer ? $" {currentEnergy}/{maxEnergy}" : $"{currentEnergy}/{maxEnergy}";
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
}
