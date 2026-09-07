using UnityEngine;

/// <summary>
/// 3D 法术卡显示（SpellCard00_New_3D 专用；继承 CardDisplay3D，Card3DInstance.UpdateValues
/// 经 GetComponent&lt;CardDisplay3D&gt;() 命中子类）。卡框用"法术框"（costFrameSprites 填 SpellCard_0..5），
/// 原画/Composite 走 Cards/Spell/...；攻击/生命/类别/三排图标不建（子类不引）。
/// 额外提供能量图标（2D 法术保留了能量 UI，3D 对齐）。
/// </summary>
public class CardDisplay3DSpell : CardDisplay3D
{
    [Header("法术：能量图标（可选）")]
    public SpriteRenderer costIcon;
    public Sprite energyIconSprite;

    public override void Refresh()
    {
        Card3DInstance c3dv = GetComponent<Card3DInstance>();
        CardInstance ci = c3dv != null ? c3dv.cardInstance : GetComponent<CardInstance>();
        CardData tpl = ci != null && CardDatabase.Instance != null ? CardDatabase.Instance.GetTemplate(ci.templateID) : null;
        Transform uiC = transform.Find("UIComponents");
        Debug.Log("[3DSpellRefresh-probe] tid=" + (ci != null ? ci.templateID : "null")
            + " tpl=" + (tpl != null) + " c3dCI=" + (ci != null) + " front=" + (uiC != null));
        // base.Refresh：模板卡名/费用/效果文本、卡面三层(框=Spell 框数组)+Composite、法术隐藏攻/血(无节点则跳过)
        base.Refresh();
        if (costIcon != null && energyIconSprite != null)
            costIcon.sprite = energyIconSprite;
    }

    // ── 正反面（法术版显式切容器，翻背时正面全部隐藏、只留卡背）──

    Transform FindNamedChild(string name)
    {
        Transform t = transform.Find(name);
        if (t != null) return t;
        for (int i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i).name == name) return transform.GetChild(i);
        return null;
    }

    public override void ShowBack()
    {
        Transform ui = FindNamedChild("UIComponents");
        if (ui != null) ui.gameObject.SetActive(false); // 隐藏正面全部元素（框/原画/费用/效果文本）
        Transform model = FindNamedChild("ModelRoot");
        if (model != null) model.gameObject.SetActive(true); // 只留卡背模型（SetHidden 已翻转）
    }

    public override void ShowFront()
    {
        Transform ui = FindNamedChild("UIComponents");
        if (ui != null) ui.gameObject.SetActive(true);
        Transform model = FindNamedChild("ModelRoot");
        if (model != null) model.gameObject.SetActive(false);
    }
}
