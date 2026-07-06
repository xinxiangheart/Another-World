using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class EnemyPlayer : MonoBehaviour
{
    public static EnemyPlayer Instance { get; private set; }

    public int maxHealth = 20;
    public int maxEnergy = 15;
    public int maxHandSize = 20;

    public int currentHealth;
    public int currentEnergy;

    [Header("手牌")]
    public Transform handArea;
    public GameObject cardPrefab2D;
    public GameObject spellCardPrefab2D;
    public List<GameObject> handCards = new List<GameObject>();

    [Header("UI")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI energyText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
        currentEnergy = 0;
    }

    void Start() => UpdateUI();

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        UpdateUI();
        if (currentHealth <= 0) Debug.Log("敌方玩家死亡");
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateUI();
    }

    public void AddEnergy(int amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        UpdateUI();
    }

    public void AddCardToHand(CardData template)
    {
        if (handCards.Count >= maxHandSize) return;

        GameObject prefab = template.cardType == CardType.Spell ? spellCardPrefab2D : cardPrefab2D;
        GameObject card = Instantiate(prefab, handArea);

        CardInstance inst = card.GetComponent<CardInstance>();
        if (inst == null) inst = card.AddComponent<CardInstance>();
        inst.InitFromTemplate(template, 0);

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        if (display != null) display.RefreshWithInstance(inst);

        handCards.Add(card);
    }

    public void AddCardToHandFromInstance(CardData template, CardInstance oldInstance)
    {
        if (handCards.Count >= maxHandSize) return;

        GameObject prefab = template.cardType == CardType.Spell ? spellCardPrefab2D : cardPrefab2D;
        GameObject card = Instantiate(prefab, handArea);

        CardInstance inst = card.GetComponent<CardInstance>();
        if (inst == null) inst = card.AddComponent<CardInstance>();

        inst.CopyFrom(oldInstance);
        inst.handledReturnToHand = false;

        if (inst.templateID == "01524")
        {
            inst.scrollCorePhaseCount = 0;
            inst.currentCost = 0;
        }

        CardDisplay2D display = card.GetComponent<CardDisplay2D>();
        if (display != null) display.RefreshWithInstance(inst);

        handCards.Add(card);
    }
    public void UpdateUI()
    {
        if (healthText != null) healthText.text = currentHealth.ToString();
        if (energyText != null) energyText.text = $"{currentEnergy}/{maxEnergy}";
    }
}