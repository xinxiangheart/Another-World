using UnityEngine;
using TMPro;

public class CardDisplay3D : MonoBehaviour
{
    [Header("3D文字")]
    public TextMeshPro attackText;
    public TextMeshPro healthText;
    public TextMeshPro nameText;
    public TextMeshPro costText;
    public TextMeshPro prefixText;
    public TextMeshPro effectText;
    public void Refresh()
    {
        Card3DInstance c3d = GetComponent<Card3DInstance>();
        if (c3d == null || c3d.cardInstance == null) return;
        CardInstance instance = c3d.cardInstance;
        CardData template = CardDatabase.Instance?.GetTemplate(instance.templateID);

        if (nameText != null) nameText.text = template?.cardName ?? "";
        if (prefixText != null) prefixText.text = instance.prefixes;
        // 法术牌显示效果文本
        if (template.cardType == CardType.Spell && effectText != null)
        {
            effectText.text = template.effect ?? "";
        }

        // 法术牌隐藏攻击力和生命值
        if (template.cardType == CardType.Spell)
        {
            if (attackText != null) attackText.gameObject.SetActive(false);
            if (healthText != null) healthText.gameObject.SetActive(false);
        }
        if (costText != null)
            costText.text = $"{instance.currentCost}费({instance.currentTier}阶)";

        if (attackText != null) attackText.text = instance.Attack.ToString();
        if (healthText != null)
        {
           
            healthText.text = $"{instance.currentHealth}/{instance.currentMaxHealth}";
        }
    }
    private bool IsSuppressorOnField()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        BoardSlot[] slots = bm.GetAllSlots();
        if (slots == null) return false;
        for (int i = 6; i <= 11; i++)
        {
            if (slots[i] == null || slots[i].currentCard3D == null) continue;
            CardInstance ci = slots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "03501")
                return true;
        }
        return false;
    }
    /// <summary>
     /// 隐藏所有3D文字和信息（对方视角用）
     /// </summary>
    public void HideAllInfo()
    {
        if (nameText != null) nameText.gameObject.SetActive(false);
        if (attackText != null) attackText.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (costText != null) costText.gameObject.SetActive(false);
        if (prefixText != null) prefixText.gameObject.SetActive(false);
        if (effectText != null) effectText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示所有3D文字和信息（己方视角用）
    /// </summary>
    public void ShowAllInfo()
    {
        if (nameText != null) nameText.gameObject.SetActive(true);
        if (attackText != null) attackText.gameObject.SetActive(true);
        if (healthText != null) healthText.gameObject.SetActive(true);
        if (costText != null) costText.gameObject.SetActive(true);
        if (prefixText != null) prefixText.gameObject.SetActive(true);
        if (effectText != null) effectText.gameObject.SetActive(true);
    }
}