using UnityEngine;
using TMPro;

public class CardDisplay2D : MonoBehaviour
{
    [Header("文字组件")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI prefixText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI effectText;

    private CardInstance instance;

    // 恢复 instance 引用的备用方法
    private void EnsureInstance()
    {
        if (instance == null)
            instance = GetComponent<CardInstance>();
    }

    // 外部调用：直接注入实例
    public void RefreshWithInstance(CardInstance inst)
    {
        instance = inst;
        if (instance == null)
        {
            Debug.LogWarning("RefreshWithInstance: inst 为 null");
            return;
        }
        Debug.Log($"RefreshWithInstance: templateID={instance.templateID}, atk={instance.currentAttack}, hp={instance.currentHealth}, cost={instance.currentCost}");
        Refresh();
    }
    public void Refresh()
    {
        if (instance == null) return;

        // X数值手牌实时更新
        if (instance.isXValue)
        {
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.UpdateXValues(instance);
        }

        CardData template = CardDatabase.Instance?.GetTemplate(instance.templateID);
        if (template == null) return;

        bool isSpell = template.cardType == CardType.Spell;

        if (nameText != null) nameText.text = template.cardName;
        if (prefixText != null) prefixText.text = instance.prefixes;
        int displayCost = instance.currentCost;
        if (instance.merchantDiscounted && NetworkPlayer.Local.IsMerchantOnFieldPublic())
            displayCost = Mathf.Max(0, displayCost - 1);
        if (instance.energyReaperDiscounted && NetworkPlayer.Local.IsEnergyReaperOnFieldPublic())
            displayCost = Mathf.Max(0, displayCost - 1);
        if (costText != null) costText.text = displayCost.ToString();

        if (attackText != null)
        {
            attackText.gameObject.SetActive(!isSpell);
            if (!isSpell) attackText.text = instance.Attack.ToString();
        }
        if (healthText != null)
        {
            healthText.gameObject.SetActive(!isSpell);
            if (!isSpell) healthText.text = $"{instance.currentHealth}/{instance.currentMaxHealth}";
        }

        if (effectText != null)
        {
            if (isSpell)
                effectText.text = template.effect;
            else
                effectText.gameObject.SetActive(false);
        }
    }
}