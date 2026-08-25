using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Test1Panel : MonoBehaviour
{
    public static Test1Panel Instance { get; private set; }

    public GameObject panelRoot;
    public TextMeshProUGUI infoText;

    /// <summary>当前正在显示的卡片索引信息，同步回调中刷新</summary>
    CardInstance _cachedInstance;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 悬停信息面板：只显示，不拦截射线——目标选择时让悬停/点击穿透到下方格子
        // （卡牌 Collider 保持启用，弹窗照常弹出；格子高亮和点击选中同时生效）
        // 直接设 raycastTarget=false（不新增/查找组件，避免 MissingComponentException）
        if (panelRoot != null)
        {
            foreach (var g in panelRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                g.raycastTarget = false;
        }
        Hide();
    }

    /// <summary>如果面板正打开，用最新的 CardInstance 重新渲染</summary>
    public void RefreshIfOpen()
    {
        if (panelRoot.activeSelf && _cachedInstance != null)
            Show(_cachedInstance);
    }

    public void Show(CardInstance instance)
    {
        Debug.Log($"Test1Panel.Show: instanceID={instance?.instanceID}, templateID={instance?.templateID}");
        if (instance == null) return;

        _cachedInstance = instance;

        CardData template = CardDatabase.Instance?.GetTemplate(instance.templateID);
        if (template == null) return;

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();

        if (template.cardType == CardType.Spell)
        {
            infoText.text = $"ID: {instance.instanceID}\n" +
                            $"名称: {template.cardName}\n" +
                            $"类型: {template.cardType} ({template.spellType})\n" +
                            $"费用: {instance.currentCost} (基础 {template.baseCost})\n" +
                            $"效果: {template.effect}";
        }
        else
        {
            string traits = BuildTraitsText(instance);
            string shieldText = instance.hasShield ? "（护盾）" : "";
            int displayCost = instance.currentCost;
            if (Player.Instance != null)
            {
                Player.Instance.handCards.RemoveAll(c => c == null);
                if (instance.merchantDiscounted && Player.Instance.IsMerchantOnFieldPublic())
                    displayCost = Mathf.Max(0, displayCost - 1);
                if (instance.energyReaperDiscounted && Player.Instance.IsEnergyReaperOnFieldPublic())
                    displayCost = Mathf.Max(0, displayCost - 1);
            }
            infoText.text = $"ID: {instance.instanceID}\n" +
                            $"名称: {template.cardName}\n" +
                            $"类型: {template.cardType} ({template.summonType})\n" +
                            $"前缀: {instance.prefixes}\n" +
                            $"费用: {displayCost} (基础 {template.baseCost})\n" +
                            $"生命: {instance.currentHealth}/{instance.currentMaxHealth} (基础 {instance.baseHealth}){shieldText}\n" +
                            $"攻击: {instance.Attack} (基础 {instance.baseAttack})\n" +
                            $"阶位: {instance.currentTier} (基础 {instance.baseTier})\n" +
                            $"特性: {traits}";
        }

        // 显示附着物信息
        if (!instance.isAttached)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                BoardSlot[] slots = bm.GetAllSlots();
                int hostSlotID = -1;
                if (slots != null)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        if (slots[i]?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == instance)
                        {
                            hostSlotID = slots[i].slotID;
                            break;
                        }
                    }
                }
                if (hostSlotID != -1)
                {
                    foreach (GameObject obj in bm.attachedModels)
                    {
                        if (obj == null) continue;
                        CardInstance ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isAttached && ci.hostSlotID == hostSlotID)
                        {
                            CardData attachTemplate = CardDatabase.Instance?.GetTemplate(ci.templateID);
                            if (attachTemplate != null && !string.IsNullOrEmpty(attachTemplate.traits))
                            {
                                infoText.text += $"\n附着物：{attachTemplate.cardName}：{attachTemplate.traits}";
                            }
                        }
                    }
                }
            }
        }

        // 01534 活化母巢：显示累计受伤计数
        if (instance.templateID == "01534")
            infoText.text += $"\n当前计数：{instance.totalDamageTaken}";
    }

    string BuildTraitsText(CardInstance ci)
    {
        Debug.Log($"BuildTraitsText: templateID={ci.templateID}, grantedCount={ci.grantedTraitTexts?.Count}, visibleCount={ci.GetTraitCount()}");
        // 特性条目化：固有（跳过"赋予"标记）+ 获得的赋予特性，统一编号排序
        var entries = ci.GetVisibleTraitEntries();
        if (entries.Count == 0) return "无";
        var lines = new List<string>();
        for (int i = 0; i < entries.Count; i++)
            lines.Add(CardInstance.FormatTraitEntry(i + 1, entries[i]));
        return string.Join("\n", lines);
    }
    public void Hide() => panelRoot.SetActive(false);
}