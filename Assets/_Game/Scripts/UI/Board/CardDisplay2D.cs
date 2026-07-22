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
    [System.Obsolete]
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
            Debug.LogWarning("RefreshWithInstance: inst Ϊ null");
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
        if (costText != null) costText.gameObject.SetActive(false);

        if (attackText != null)
        {
            attackText.gameObject.SetActive(!isSpell);
            if (!isSpell) attackText.text = instance.Attack.ToString();
        }
        if (healthText != null)
        {
            healthText.gameObject.SetActive(!isSpell);
            if (!isSpell) healthText.text = instance.currentHealth.ToString();
        }

        if (effectText != null)
        {
            if (isSpell)
                effectText.text = template.effect;
            else
                effectText.gameObject.SetActive(false);
        }
    }

    /// <summary>显示2D卡牌背面——无畏者弹窗等隐藏状态展示用。</summary>
    public void ShowBack(CardData template)
    {
        if (nameText != null) nameText.text = "反制牌";
        if (prefixText != null) prefixText.text = "";
        if (attackText != null) attackText.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (costText != null) costText.gameObject.SetActive(false);
        if (effectText != null) effectText.gameObject.SetActive(false);

        // 替换为卡背贴图
        if (template != null && template.spellCardBackSprite2D != null)
        {
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.sprite = template.spellCardBackSprite2D;
        }
    }
}