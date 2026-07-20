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

        // 输入的是 templateID（如 01310），直接从牌库抽一张到手牌
        DrawFromDeckByTemplateID(id);
        Hide();
    }

    /// <summary>从牌库中查找匹配 templateID 的卡，抽出并加入手牌。走正规抽取流程，有唯一 instanceID。</summary>
    void DrawFromDeckByTemplateID(string templateID)
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        // 1. 先查牌库
        var czm = CardZoneManager.Instance;
        if (czm != null && !czm.IsDeckEmpty)
        {
            if (czm.RemoveFromDeckByTemplateID(templateID, out string iid))
            {
                CardData template = CardDatabase.Instance?.GetTemplate(templateID);
                if (template != null)
                {
                    template._instanceID = iid;
                    NetworkPlayer.Local.AddCardToHand(template, iid);
                    Debug.Log($"从牌库抽取 {templateID} iid={iid}，已加入手牌，牌库剩余 {czm.DeckCount} 张");
                    return;
                }
            }
        }

        // 2. 牌库没有 → 检查弃牌堆
        var graveList = GraveyardManager.Instance?.graveyard;
        if (graveList != null)
        {
            for (int i = graveList.Count - 1; i >= 0; i--)
            {
                if (graveList[i].templateID == templateID)
                {
                    var target = graveList[i];
                    var entry = CardZoneManager.Instance?.RemoveFromGraveyard(target.instanceID);
                    if (entry != null || graveList[i] == target)
                        graveList.RemoveAt(i);

                    // 从弃牌堆数据重建进手牌
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
                    CardData template = CardDatabase.Instance?.GetTemplate(target.templateID);
                    if (template != null)
                        NetworkPlayer.Local.AddCardToHandFromInstance(template, ci);
                    Destroy(temp);
                    Debug.Log($"从弃牌堆获取 {templateID}，已加入手牌");
                    return;
                }
            }
        }

        Debug.LogWarning($"未在任何区域找到 templateID={templateID} 的卡牌");
    }

    [System.Obsolete]
    void GetCardByID(string instanceID)
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);
        // 1. 检查己方手牌
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && ci.instanceID == instanceID)
            {
                    // 从场上退场（普通退场），加入手牌
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
                        NetworkPlayer.Local.AddCardToHandFromInstance(template, target);
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
                        NetworkPlayer.Local.AddCardToHandFromInstance(template, target);
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
                        NetworkPlayer.Local.AddCardToHandFromInstance(template, target);
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
                        NetworkPlayer.Local.AddCardToHandFromInstance(template, target);
                    Debug.Log($"从敌方反制牌区获取 {instanceID}，已加入手牌");
                    return;
                }
            }
        }

        // 5. 检查牌库（CardZoneManager）——按 templateID 抽牌
        string searchTid = instanceID.Length >= 5 ? instanceID.Substring(0, 5) : instanceID;
        var czm = CardZoneManager.Instance;
        if (czm != null && !czm.IsDeckEmpty)
        {
            if (czm.RemoveFromDeckByTemplateID(searchTid, out string removedIid))
            {
                CardData template = CardDatabase.Instance?.GetTemplate(searchTid);
                if (template != null)
                {
                    template._instanceID = removedIid;
                    NetworkPlayer.Local.AddCardToHand(template, removedIid);
                }
                Debug.Log($"从牌库抽取 {searchTid} iid={removedIid}，已加入手牌，牌库剩余 {czm.DeckCount} 张");
                return;
            }
        }

        // 6. 检查敌方手牌
        if (NetworkPlayer.Remote != null)
        {
            for (int i = NetworkPlayer.Remote.handCards.Count - 1; i >= 0; i--)
            {
                GameObject card = NetworkPlayer.Remote.handCards[i];
                CardInstance ci = card?.GetComponent<CardInstance>();
                if (ci != null && ci.instanceID == instanceID)
                {
                    NetworkPlayer.Remote.handCards.RemoveAt(i);
                    Destroy(card);
                    CardData template = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (template != null)
                        NetworkPlayer.Local.AddCardToHandFromInstance(template, ci);
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
                        NetworkPlayer.Local.AddCardToHandFromInstance(template, ci);
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