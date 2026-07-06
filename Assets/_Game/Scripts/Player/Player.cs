using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("基础属性")]
    public int maxHealth = 20;
    public int maxEnergy = 15;
    public int maxHandSize = 20;

    [Header("当前状态")]
    public int currentHealth;
    public int currentEnergy;

    [Header("手牌")]
    public Transform handArea;
    public HandManager myHandManager;
    public GameObject cardPrefab2D;
    public GameObject spellCardPrefab2D;
    public List<GameObject> handCards = new List<GameObject>();

    [Header("UI 绑定")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI energyText;
    public bool IsEnergyReaperOnFieldPublic() => IsEnergyReaperOnField();
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
        currentEnergy = 0;
    }

    void Start()
    {
        UpdateUI();
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        UpdateUI();
        GlobalEventManager.Instance?.TriggerPlayerDamaged(amount);

        if (currentHealth <= 0)
        {
            CounterManager.Instance?.CheckOnPlayerDying();
            if (currentHealth <= 0)
                Debug.Log("玩家死亡，游戏结束");
        }
    }

    public void Heal(int amount)
    {
        ReceiveHeal(amount, CardInstance.HealSourceType.Any);
    }

    public void AddEnergy(int amount)
    {
        currentEnergy += amount;
        if (!_energyCanExceedLimit)
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy);
        UpdateUI();
    }

    public bool _energyCanExceedLimit = false;

    public bool UseEnergy(int amount)
    {
        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            UpdateUI();
            return true;
        }
        return false;
    }
    public bool IsMerchantOnFieldPublic() => IsMerchantOnField();
    public int GetEnergy() => currentEnergy;

    public void OnPhaseStart()
    {
        Debug.Log($"OnPhaseStart 执行，当前能量：{currentEnergy}");
        FindObjectOfType<DrawCardUI>()?.ResetForNewPhase();
    }

    // ========== 从模板全新创建（抽牌用） ==========

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
            cv.handManager = myHandManager;
            myHandManager?.RegisterCard(cv);
        }
    }

    // ========== 从实例复制（回手用） ==========
    public void AddCardToHandFromInstance(CardData template, CardInstance oldInstance, bool isEnemy = false)
    {
        if (isEnemy && EnemyPlayer.Instance == null) return;
        if (!isEnemy && Player.Instance == null) return;

        int maxSize = isEnemy ? EnemyPlayer.Instance.maxHandSize : Player.Instance.maxHandSize;
        Transform handArea = isEnemy ? EnemyPlayer.Instance.handArea : Player.Instance.handArea;
        GameObject prefab = isEnemy ? EnemyPlayer.Instance.cardPrefab2D : cardPrefab2D;

        if (!isEnemy && handCards.Count >= maxSize) return;

        GameObject card = Instantiate(prefab, handArea);
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
        {
            inst.energyReaperDiscounted = false;
        }
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
    // ========== 手牌管理 ==========

    public void DrawCard()
    {
        handCards.RemoveAll(c => c == null);

        DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
        if (drawUI != null && drawUI.GetRemainingDraws() <= 0)
        {
            Debug.Log("本回合抽牌次数已用完");
            return;
        }

        Debug.Log($"当前手牌数: {handCards.Count}, 上限: {maxHandSize}");
        if (handCards.Count >= maxHandSize)
        {
            Debug.Log("手牌已满，抽牌失败");
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
            cv.handManager = myHandManager;
            myHandManager?.RegisterCard(cv);
        }
        if (IsEnergyReaperOnField() && instance != null)
        {
            CardData td = CardDatabase.Instance?.GetTemplate(instance.templateID);
            if (td != null && td.cardType == CardType.Summon && instance.prefixes.Contains("灵能") && !instance.energyReaperDiscounted)
            {
                instance.energyReaperDiscounted = true;
                display?.Refresh();
            }
        }
        if (IsMerchantOnField() && instance != null)
        {
            CardData td = CardDatabase.Instance?.GetTemplate(instance.templateID);
            if (td != null && td.cardType == CardType.Summon && !instance.merchantDiscounted)
            {
                instance.merchantDiscounted = true;
                display?.Refresh();
            }
        }
        // 中枢在场时，新抽到的召唤物附加灵能前缀
        CardData drawnTemplate = CardDatabase.Instance?.GetTemplate(instance.templateID);
        if (drawnTemplate != null && drawnTemplate.cardType == CardType.Summon)
        {
            ApplyCorePrefix(instance);
        }

        Debug.Log($"抽牌成功，当前手牌数：{handCards.Count}");
    }
    /// <summary>不消耗抽牌次数的抽牌（卡牌效果用）</summary>
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
            cv.handManager = myHandManager;
            myHandManager?.RegisterCard(cv);
        }
        if (IsEnergyReaperOnField() && instance != null)
        {
            CardData td = CardDatabase.Instance?.GetTemplate(instance.templateID);
            if (td != null && td.cardType == CardType.Summon && instance.prefixes.Contains("灵能") && !instance.energyReaperDiscounted)
            {
                instance.energyReaperDiscounted = true;
                display?.Refresh();
            }
        }
        if (IsMerchantOnField() && instance != null)
        {
            CardData td = CardDatabase.Instance?.GetTemplate(instance.templateID);
            if (td != null && td.cardType == CardType.Summon && !instance.merchantDiscounted)
            {
                instance.merchantDiscounted = true;
                display?.Refresh();
            }
        }
        // 中枢在场时，新抽到的召唤物附加灵能前缀
        CardData drawnTemplate = CardDatabase.Instance?.GetTemplate(instance.templateID);
        if (drawnTemplate != null && drawnTemplate.cardType == CardType.Summon)
        {
            ApplyCorePrefix(instance);
        }
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
    /// <summary>受到治疗时触发，返回实际治疗量</summary>
    public static event System.Func<int, CardInstance.HealSourceType, int> OnBeforePlayerHeal;

    public void ReceiveHeal(int amount, CardInstance.HealSourceType sourceType)
    {
        if (OnBeforePlayerHeal != null)
            amount = OnBeforePlayerHeal(amount, sourceType);
        if (amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateUI();
    }
    void ApplyCorePrefix(CardInstance ci)
    {
        if (ci == null) return;
        if (ci.prefixes.Contains("灵能")) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        bool coreOnField = false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance fieldCI = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (fieldCI != null && fieldCI.templateID == "03027")
            {
                coreOnField = true;
                break;
            }
        }

        if (coreOnField)
        {
            if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                ci.prefixes = "灵能";
            else
                ci.prefixes += " 灵能";
            CardDisplay2D d2d = ci.GetComponent<CardDisplay2D>();
            d2d?.Refresh();
        }
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

   
    // ========== UI ==========

    public void UpdateUI()
    {
        if (healthText != null)
            healthText.text = $" {currentHealth}";
        if (energyText != null)
            energyText.text = $" {currentEnergy}/{maxEnergy}";
    }
}