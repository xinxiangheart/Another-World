using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GetCardPanel : MonoBehaviour
{
    public static GetCardPanel Instance { get; private set; }

    public GameObject panelRoot;
    public TMP_InputField inputField;
    public Button confirmButton;
    public Button closeButton;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        panelRoot.SetActive(false);
    }

    void Start()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        closeButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        panelRoot.SetActive(true);
        inputField.text = "";
        inputField.Select();
        inputField.ActivateInputField();
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }

    void OnConfirm()
    {
        string id = inputField.text.Trim();
        if (string.IsNullOrEmpty(id))
        {
            Debug.Log("请输入卡牌ID");
            return;
        }

        GetCardByID(id);
        Hide();
    }

    void GetCardByID(string instanceID)
    {
        Player.Instance.handCards.RemoveAll(c => c == null);
        // 1. 检查己方手牌
        foreach (GameObject card in Player.Instance.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && ci.instanceID == instanceID)
            {
                // 手牌里找到，不用动，已经在手牌了
                Debug.Log($"卡牌 {instanceID} 已在手牌中");
                return;
            }
        }

        // 2. 检查己方场上
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance != null && c3d.cardInstance.instanceID == instanceID)
                {
                    // 从场上退场（普通退场），加入手牌
                    CardInstance target = c3d.cardInstance;
                    slot.HandleDeath(slot.currentCard3D);
                    CardData template = CardDatabase.Instance?.GetTemplate(target.templateID);
                    if (template != null)
                        Player.Instance.AddCardToHandFromInstance(template, target);
                    Debug.Log($"从场上获取 {instanceID}，已加入手牌");
                    return;
                }
            }
        }

        // 3. 检查敌方场上
        if (bm != null)
        {
            for (int i = 0; i <= 5; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance != null && c3d.cardInstance.instanceID == instanceID)
                {
                    CardInstance target = c3d.cardInstance;
                    slot.HandleDeath(slot.currentCard3D);
                    CardData template = CardDatabase.Instance?.GetTemplate(target.templateID);
                    if (template != null)
                        Player.Instance.AddCardToHandFromInstance(template, target);
                    Debug.Log($"从敌方场上获取 {instanceID}，已加入手牌");
                    return;
                }
            }
        }

        // 4. 检查反制牌区
        CounterManager cm = FindObjectOfType<CounterManager>();
        if (cm != null)
        {
            for (int i = cm.myCounters.Count - 1; i >= 0; i--)
            {
                if (cm.myCounters[i].cardInstance.instanceID == instanceID)
                {
                    CardInstance target = cm.myCounters[i].cardInstance;
                    CardData template = CardDatabase.Instance?.GetTemplate(target.templateID);
                    cm.myCounters.RemoveAt(i);
                    if (template != null)
                        Player.Instance.AddCardToHandFromInstance(template, target);
                    Debug.Log($"从反制牌区获取 {instanceID}，已加入手牌");
                    return;
                }
            }
            for (int i = cm.enemyCounters.Count - 1; i >= 0; i--)
            {
                if (cm.enemyCounters[i].cardInstance.instanceID == instanceID)
                {
                    CardInstance target = cm.enemyCounters[i].cardInstance;
                    CardData template = CardDatabase.Instance?.GetTemplate(target.templateID);
                    cm.enemyCounters.RemoveAt(i);
                    if (template != null)
                        Player.Instance.AddCardToHandFromInstance(template, target);
                    Debug.Log($"从敌方反制牌区获取 {instanceID}，已加入手牌");
                    return;
                }
            }
        }

        // 5. 检查牌库
        DeckManager dm = FindObjectOfType<DeckManager>();
        if (dm != null)
        {
            for (int i = dm.mainDeck.Count - 1; i >= 0; i--)
            {
                if (dm.mainDeck[i].templateID == instanceID || dm.mainDeck[i].templateID == instanceID.Substring(0, 5))
                {
                    CardData data = dm.mainDeck[i];
                    dm.mainDeck.RemoveAt(i);
                    Player.Instance.AddCardToHand(data);
                    Debug.Log($"从牌库获取 {data.templateID}，已加入手牌");
                    return;
                }
            }
        }

        // 6. 检查敌方手牌
        if (EnemyPlayer.Instance != null)
        {
            for (int i = EnemyPlayer.Instance.handCards.Count - 1; i >= 0; i--)
            {
                GameObject card = EnemyPlayer.Instance.handCards[i];
                CardInstance ci = card?.GetComponent<CardInstance>();
                if (ci != null && ci.instanceID == instanceID)
                {
                    EnemyPlayer.Instance.handCards.RemoveAt(i);
                    Destroy(card);
                    CardData template = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (template != null)
                        Player.Instance.AddCardToHandFromInstance(template, ci);
                    Debug.Log($"从敌方手牌获取 {instanceID}，已加入己方手牌");
                    return;
                }
            }
        }

        // 7. 检查弃牌堆
        List<GraveEntry> graveyard = GraveyardManager.Instance?.graveyard;
        if (graveyard != null)
        {
            for (int i = graveyard.Count - 1; i >= 0; i--)
            {
                if (graveyard[i].instanceID == instanceID)
                {
                    GraveEntry target = graveyard[i];
                    graveyard.RemoveAt(i);
                    CardData template = CardDatabase.Instance?.GetTemplate(target.templateID);
                    if (template != null)
                    {
                        // 从墓地数据创建临时CardInstance加入手牌
                        GameObject temp = new GameObject("TempGrave");
                        CardInstance ci = temp.AddComponent<CardInstance>();
                        ci.templateID = target.templateID;
                        ci.instanceID = target.instanceID;
                        ci.currentCost = target.currentCost;
                        ci.currentAttack = target.baseAttack;
                        ci.baseAttack = target.baseAttack;
                        ci.currentHealth = target.baseHealth;
                        ci.baseHealth = target.baseHealth;
                        ci.baseMaxHealth = target.baseMaxHealth;
                        ci.currentMaxHealth = target.baseMaxHealth;
                        ci.currentTier = target.currentTier;
                        ci.baseTier = target.baseTier;
                        ci.prefixes = target.prefixes;
                        Player.Instance.AddCardToHandFromInstance(template, ci);
                        Destroy(temp);
                    }
                    Debug.Log($"从弃牌堆获取 {instanceID}，已加入手牌");
                    return;
                }
            }
        }
        Debug.Log($"未找到卡牌 {instanceID}");
    }
}