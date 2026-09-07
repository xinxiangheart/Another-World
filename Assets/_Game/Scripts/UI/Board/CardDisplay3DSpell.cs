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
        // base.Refresh：模板卡名/费用/效果文本、卡面三层(框=Spell 框数组)+Composite、法术隐藏攻/血(无节点则跳过)
        base.Refresh();
        if (costIcon != null && energyIconSprite != null)
            costIcon.sprite = energyIconSprite;
    }
}
