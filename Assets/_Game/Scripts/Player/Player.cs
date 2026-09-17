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
            {
                Debug.Log("玩家死亡，游戏结束");
                GameEndPanel.Instance?.OnPlayerDied(true);
            }
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
        Scale2DCard(card);

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
            cv.IsFlying = true; // 飞行中不参与布局；RefreshLayout 延迟到 AnimateCardDraw 内部 60% 时触发
            myHandManager?.RegisterCard(cv, false); // 只加入列表，不刷新布局
        }

        // 抽牌入场动画
        if (cv != null && myHandManager != null)
        {
            cv.SetAlpha(0f);
            var newCards = new System.Collections.Generic.List<CardView> { cv };
            myHandManager.StartCoroutine(myHandManager.AnimateCardDraw(newCards));
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
        Scale2DCard(card);
        CardInstance inst = card.GetComponent<CardInstance>();
        if (inst == null) inst = card.AddComponent<CardInstance>();

        inst.CopyFrom(oldInstance);
        inst.RemoveGrantedTraitsBySource("01336"); // 5.x：01336 修正者附着授予离场即清，防幻影先手特性重打
        // 商人/收割者"召唤费用-1"来源状态：回手即清（回手后重判，来源在场才重新打标 flag；手牌不携带 AddStatus）
        inst.RemoveStatusBySource("01520");
        inst.RemoveStatusBySource("01528");
        inst.currentAttack = Mathf.Max(0, inst.baseAttack);
        inst.currentHealth = Mathf.Max(0, inst.baseHealth);
        inst.currentMaxHealth = Mathf.Max(0, inst.baseMaxHealth);
        inst.currentTier = inst.baseTier;
        inst.tempAttackBoost = 0;
        inst.tempHealthBoost = 0;
        // 回手清光环标记：英雄离场周期结束，再进场可重新获得智者(03503)/皇帝(01501) buff
        inst.buffedBySage = false;
        inst.buffedByEmperor = false;
        // 护盾 = 场上状态：离场即清除，重掷从干净状态开始
        inst.RemoveShield();
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
            inst.merchantDiscounted = false;
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
                cv.IsFlying = true; // 飞行中不参与布局；RefreshLayout 延迟到 AnimateCardDraw 内部 60% 时触发
                hm?.RegisterCard(cv, false); // 只加入列表，不刷新布局

                // 回手入场动画
                cv.SetAlpha(0f);
                var newCards = new System.Collections.Generic.List<CardView> { cv };
                hm?.StartCoroutine(hm.AnimateCardDraw(newCards));
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
        Scale2DCard(card);
        CardInstance instance = card.GetComponent<CardInstance>();
        if (instance != null)
        {
            string iid = data._instanceID;
            instance.InitFromTemplate(data, 0, iid);
            if (!string.IsNullOrEmpty(iid))
                CardZoneManager.Instance?.RegisterInstanceID(iid);
        }

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        display.RefreshWithInstance(instance);

        handCards.Add(card);
        CardView cv = card.GetComponent<CardView>();
        if (cv != null)
        {
            cv.handManager = myHandManager;
            cv.IsFlying = true; // 飞行中不参与布局；RefreshLayout 延迟到 AnimateCardDraw 内部 60% 时触发
            myHandManager?.RegisterCard(cv, false); // 只加入列表，不刷新布局
        }

        // 抽牌入场动画
        if (cv != null && myHandManager != null)
        {
            cv.SetAlpha(0f);
            var newCards = new System.Collections.Generic.List<CardView> { cv };
            myHandManager.StartCoroutine(myHandManager.AnimateCardDraw(newCards));
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
        Scale2DCard(card);
        CardInstance instance = card.GetComponent<CardInstance>();
        if (instance != null)
        {
            string iid = data._instanceID;
            instance.InitFromTemplate(data, 0, iid);
            if (!string.IsNullOrEmpty(iid))
                CardZoneManager.Instance?.RegisterInstanceID(iid);
        }

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        display.RefreshWithInstance(instance);

        handCards.Add(card);
        CardView cv = card.GetComponent<CardView>();
        if (cv != null)
        {
            cv.handManager = myHandManager;
            cv.IsFlying = true; // 飞行中不参与布局；RefreshLayout 延迟到 AnimateCardDraw 内部 60% 时触发
            myHandManager?.RegisterCard(cv, false); // 只加入列表，不刷新布局
        }

        // 抽牌入场动画
        if (cv != null && myHandManager != null)
        {
            cv.SetAlpha(0f);
            var newCards = new System.Collections.Generic.List<CardView> { cv };
            myHandManager.StartCoroutine(myHandManager.AnimateCardDraw(newCards));
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
    /// <summary>中枢(03027)灵能光环：只认本机(6-11)自己半场的中枢，作用于本机场上全体 + 手牌召唤物。
    /// 结算统一走 HandManager.ApplyCorePsiAura —— 旧实现里 NetworkPlayer 侧扫全场 12 槽，
    /// 会把 AI 放在 0-5 的中枢也算进来、给玩家新抽的牌加灵能前缀，这里不再各写一份。</summary>
    public void ApplyCorePrefix(CardInstance ci)
    {
        if (ci == null) return;
        if (ci.prefixes != null && ci.prefixes.Contains("灵能")) return;
        HandManager.ApplyCorePsiAura(NetworkPlayer.Local);
    }
    // 商人(01520)/能量收割者(01528)：只认本机自己半场(6-11)的光环——AI 的商人/收割者只减 AI 手牌费
    bool IsMerchantOnField()
        => GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsAuraActiveOwnedBy<MerchantAura>(true);
    bool IsEnergyReaperOnField()
        => GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsAuraActiveOwnedBy<EnergyReaperAura>(true);

   
    // ========== UI ==========

    public void UpdateUI()
    {
        if (healthText != null)
            healthText.text = $" {currentHealth}";
        if (energyText != null)
            energyText.text = $" {currentEnergy}/{maxEnergy}";
    }

    /// <summary>Game 场景 2D 卡牌整体 ×3 + 补视觉层（预制体未 ×3，运行时补）</summary>
    public static void Scale2DCard(GameObject card)
    {
        card.transform.localScale = Vector3.one * 3f;
        var cv = card.GetComponent<CardView>();
        if (cv != null) cv.RefreshOriginalScale();
    }

    /// <summary>Game 场景 3D 模型还原预制体原始 localScale（不做额外缩放）</summary>
    public static void Scale3DModel(GameObject model) { }
}
