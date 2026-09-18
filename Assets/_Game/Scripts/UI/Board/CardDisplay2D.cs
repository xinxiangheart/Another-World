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

    protected CardInstance instance; // protected：新法术版 CardDisplay2DSpell 等子类需读取

    // 恢复 instance 引用的备用方法
    [System.Obsolete]
    private void EnsureInstance()
    {
        if (instance == null)
            instance = GetComponent<CardInstance>();
    }

    // 外部调用：直接注入实例
    public virtual void RefreshWithInstance(CardInstance inst)
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
    public virtual void Refresh()
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

    /// <summary>显示2D卡牌背面——无畏者弹窗等隐藏状态展示用。virtual：新法术卡 CardDisplay2DSpell 覆写为新卡面翻面。</summary>
    public virtual void ShowBack(CardData template, string label = "反制牌")
    {
        if (nameText != null) nameText.text = label;
        if (prefixText != null) prefixText.text = "";
        if (attackText != null) attackText.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (costText != null) costText.gameObject.SetActive(false);
        if (effectText != null) effectText.gameObject.SetActive(false);

        // 背面贴图：统一取通用 2D 卡背（cardSprite2D/卡背字段已移除，改路径加载）
        Sprite backSprite = Resources.Load<Sprite>("Cards/Back");

        if (backSprite != null)
        {
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.sprite = backSprite;
        }
    }

    /// <summary>卡背图铺满整张卡面。预制体里 CardBackImage 的 RectTransform 原本是 20×20 的居中占位，
    /// 于是所有「展示卡背」的场合（反制牌揭示 / 隐藏卡 / 面板卡背）都只显示一小块方图。
    /// 这里统一把卡背改成铺满父级（BackFace 本身已铺满整卡）；幂等——已是铺满状态直接返回。
    /// 卡背贴图 Back.png 为 768×1344，与卡面 83.33×146.33 的比例几乎一致，拉伸不失真。</summary>
    public static void StretchBackToCard(UnityEngine.UI.Image backImage)
    {
        if (backImage == null) return;
        RectTransform rt = backImage.rectTransform;
        if (rt == null) return;
        if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one
            && rt.offsetMin == Vector2.zero && rt.offsetMax == Vector2.zero) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}