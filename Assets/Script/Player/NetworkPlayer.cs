using System.Collections.Generic;
using UnityEngine;
using Mirror;
using TMPro;

public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer Local { get; private set; }
    public static NetworkPlayer Remote { get; private set; }

    [Header("基础属性")]
    public int maxHealth = 20;
    public int maxEnergy = 15;
    public int maxHandSize = 20;

    [SyncVar(hook = nameof(OnHealthChanged))]
    public int currentHealth;

    [SyncVar(hook = nameof(OnEnergyChanged))]
    public int currentEnergy;

    [Header("手牌")]
    public Transform handArea;
    public HandManager handManager;
    public GameObject cardPrefab2D;
    public GameObject spellCardPrefab2D;
    public List<GameObject> handCards = new List<GameObject>();

    [Header("UI")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI energyText;

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"OnStartLocalPlayer: netId={netId}, isServer={isServer}");
        Local = this;
        currentHealth = maxHealth;
        currentEnergy = 0;
        UpdateUI();
        // 动态绑定本地玩家UI
        healthText = GameObject.Find("Health")?.GetComponent<TextMeshProUGUI>();
        energyText = GameObject.Find("Energy")?.GetComponent<TextMeshProUGUI>();
        handArea = GameObject.Find("HandArea")?.transform;

        UpdateUI();
    }
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 400, 30), $"Server active: {NetworkServer.active}, connections: {NetworkServer.connections.Count}");
        GUI.Label(new Rect(10, 40, 400, 30), $"Client active: {NetworkClient.active}, connected: {NetworkClient.isConnected}");
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        foreach (var conn in NetworkServer.connections.Values)
        {
            var player = conn.identity?.GetComponent<NetworkPlayer>();
            if (player != null && player != this)
            {
                NetworkPlayer.Remote = player;
            }
        }
    }

    public override void OnStartClient()
    {
        UpdateUI();
    }

    void EnableLocalInteraction(bool enabled)
    {
        if (handManager != null)
            handManager.SetHandAreaRaycast(enabled);
    }

    void OnHealthChanged(int oldValue, int newValue)
    {
        UpdateUI();
        if (newValue <= 0 && isServer)
            Debug.Log("玩家死亡");
    }

    void OnEnergyChanged(int oldValue, int newValue)
    {
        UpdateUI();
    }

    // ========== 生命值 ==========

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

    // ========== 能量 ==========

    public void AddEnergy(int amount)
    {
        if (isServer)
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        else
            CmdAddEnergy(amount);
    }

    [Command]
    void CmdAddEnergy(int amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
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

    // ========== UI ==========

    public void UpdateUI()
    {
        if (healthText != null)
            healthText.text = isLocalPlayer ? $" {currentHealth}" : currentHealth.ToString();
        if (energyText != null)
            energyText.text = isLocalPlayer ? $" {currentEnergy}/{maxEnergy}" : $"{currentEnergy}/{maxEnergy}";
    }

    // ========== 抽牌 ==========

    public void DrawCard()
    {
        handCards.RemoveAll(c => c == null);

        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null && drawUI.GetRemainingDraws() <= 0)
        {
            Debug.Log("本回合抽牌次数已用完");
            return;
        }

        if (handCards.Count >= maxHandSize)
        {
            Debug.Log("手牌已满");
            return;
        }

        CardData data = DeckManager.Instance?.DrawFromMain();
        if (data == null)
        {
            Debug.Log("牌库为空");
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

        Debug.Log($"抽牌成功，当前手牌数：{handCards.Count}");
    }

    public void DrawCardWithoutLimit()
    {
        handCards.RemoveAll(c => c == null);

        if (handCards.Count >= maxHandSize)
        {
            Debug.Log("手牌已满");
            return;
        }

        CardData data = DeckManager.Instance?.DrawFromMain();
        if (data == null)
        {
            Debug.Log("牌库为空");
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

    public void RemoveCardFromHand(GameObject card)
    {
        if (handCards.Contains(card))
        {
            handCards.Remove(card);
            Destroy(card);
            handCards.RemoveAll(c => c == null);
            FindObjectOfType<HandManager>()?.RefreshLayout(true);
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
        }
    }

    // ========== 辅助方法 ==========

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
}