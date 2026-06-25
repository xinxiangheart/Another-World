using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Test1Panel : MonoBehaviour
{
    public static Test1Panel Instance { get; private set; }

    public GameObject panelRoot;
    public TextMeshProUGUI infoText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => Hide();

    public void Show(CardInstance instance)
    {
        Debug.Log($"Test1Panel.Show: instanceID={instance?.instanceID}, templateID={instance?.templateID}");
        if (instance == null) return;

        CardData template = CardDatabase.Instance?.GetTemplate(instance.templateID);
        if (template == null) return;

        panelRoot.SetActive(true);

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
            NetworkPlayer.Local.handCards.RemoveAll(c => c == null);
            bool isOnField = !NetworkPlayer.Local.handCards.Exists(c => c?.GetComponent<CardInstance>() == instance);
            int displayCost = instance.currentCost;
            if (instance.merchantDiscounted && NetworkPlayer.Local.IsMerchantOnFieldPublic())
                displayCost = Mathf.Max(0, displayCost - 1);
            if (instance.energyReaperDiscounted && NetworkPlayer.Local.IsEnergyReaperOnFieldPublic())
                displayCost = Mathf.Max(0, displayCost - 1);
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
    }

    string BuildTraitsText(CardInstance ci)
    {
        Debug.Log($"BuildTraitsText: templateID={ci.templateID}, hasOnDeath={ci.hasOnDeath}, grantedCount={ci.grantedTraitTexts?.Count}, giveableCount={ci.giveableDeathTraits?.Count}");
        CardData template = CardDatabase.Instance?.GetTemplate(ci.templateID);
        List<string> traits = new List<string>();

        // 苦难给予者特殊处理
        if (ci.templateID == "01117")
        {
            traits.Add("进场：永久给予对方一召唤物一个自己的退场（自己的退场给予后消失）");
            traits.Add("退场：回到手牌（该退场无法给予）");
            foreach (string t in ci.giveableDeathTraits)
            {
                traits.Add(t);
            }
            return string.Join("\n", traits); // 直接返回，不走通用赋予
        }

        // 普通卡牌：显示模板特性
        if (template != null && !string.IsNullOrEmpty(template.traits) && template.traits != "无")
        {
            traits.Add(template.traits);
        }

        // 通用：动态赋予的特性
        foreach (string granted in ci.grantedTraitTexts)
        {
            traits.Add($"(赋予){granted}");
        }

        return traits.Count > 0 ? string.Join("\n", traits) : "无";
    }
    public void Hide() => panelRoot.SetActive(false);
}