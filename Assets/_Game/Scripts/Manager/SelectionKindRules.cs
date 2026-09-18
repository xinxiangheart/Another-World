using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 选择语义类型：决定槽位选择指示器（SlotSelectionIndicator）的颜色。
/// Auto 只用于「发起选择时先不指定」，由 SelectionManager 依当前正在执行的特性自动判定。
/// </summary>
public enum SelectionKind
{
    Auto = -1,   // 自动判定（由 SelectionManager 解析，不会真的用于着色）
    Neutral = 0, // 中性：放置 / 移动 / 加护盾 / 加前缀 / 位置调整 / 判不出来 —— 金色
    Damage = 1,  // 伤害 —— 红色
    Heal = 2,    // 治愈 —— 绿色
    Debuff = 3,  // 减益 —— 紫色
    Discard = 4, // 抛置语境（抛置特性触发的选择 / 抛置悬停提示）—— 绿色
}

/// <summary>
/// 选择类型判定与配色。
/// 判定顺序：显式表（逐张裁定）→ 特性/效果文字关键词 → 中性。
/// 说明：文字命中率有限，凡「文字读不出语义」或「文字会判错」的卡一律写进 <see cref="Explicit"/>，
/// 新增卡若判错，照同样格式补一行即可。
/// </summary>
public static class SelectionKindRules
{
    // ── 配色 ──
    public static readonly Color NeutralColor = new Color(1f, 0.82f, 0.22f, 1f);    // 金（旧基准色）
    public static readonly Color DamageColor = new Color(0.90f, 0.17f, 0.17f, 1f);  // 红
    public static readonly Color HealColor = new Color(0.22f, 0.86f, 0.38f, 1f);    // 绿
    public static readonly Color DebuffColor = new Color(0.68f, 0.32f, 0.96f, 1f);  // 紫
    public static readonly Color DiscardColor = new Color(0.20f, 0.92f, 0.32f, 1f); // 绿（抛置）

    public static Color ColorOf(SelectionKind kind)
    {
        switch (kind)
        {
            case SelectionKind.Damage: return DamageColor;
            case SelectionKind.Heal: return HealColor;
            case SelectionKind.Debuff: return DebuffColor;
            case SelectionKind.Discard: return DiscardColor;
            default: return NeutralColor;
        }
    }

    // 减益关键词先于伤害判定：如 02408 疫病「受到…伤害，并且永久-1攻击力」是减益，不是伤害
    static readonly string[] DebuffKeywords =
    {
        "攻击力降", "攻击力将", "攻击力临时变", "攻击力临时-", "攻击力永久-",
        "攻击力永久减", "攻击力临时减", "减一攻击力", "永久-1攻击力",
        "生命值降为", "沉默", "无法攻击", "无法获得护盾"
    };
    static readonly string[] DamageKeywords = { "伤害" };
    static readonly string[] HealKeywords = { "恢复", "回复", "治疗" };

    // 逐张裁定表（模板ID → 类型）：文字判不出来 / 会判错的卡
    static readonly Dictionary<string, SelectionKind> Explicit = new Dictionary<string, SelectionKind>
    {
        { "01117", SelectionKind.Debuff }, // 苦难给予者：进场把「己方全体受一伤害」的退场塞给对方召唤物
        { "01308", SelectionKind.Debuff }, // 麻烦制造者：给对方召唤物塞一个扣血的先手
        { "01318", SelectionKind.Debuff }, // 弱化棱晶：攻击力临时变为1
        { "02213", SelectionKind.Debuff }, // 属性聚集：攻击力将为1
        { "02408", SelectionKind.Debuff }, // 疫病：指定格子受伤并永久-1攻击力
        { "03502", SelectionKind.Debuff }, // 毒巫：中毒 + 无法获得护盾
        { "01331", SelectionKind.Neutral }, // 囚牢：选空格封锁（文字里的「囚牢退场恢复」不是治愈）
        { "01503", SelectionKind.Neutral }, // 领主：选空格召唤幽魂（文字里的「受到的伤害」不由本次选择造成）
        { "03012", SelectionKind.Neutral }, // 阴阳：选己方召唤物把低数值顶到高数值（文字里的「受到伤害时伤害修正-1」不是本次选择）
        { "01531", SelectionKind.Neutral }, // 亡命之徒：「无法以任何形式恢复生命值」不是治愈
    };

    /// <summary>按模板ID判定；查不到模板时返回中性。</summary>
    public static SelectionKind Classify(string templateID)
    {
        if (string.IsNullOrEmpty(templateID)) return SelectionKind.Neutral;
        if (Explicit.TryGetValue(templateID, out SelectionKind pinned)) return pinned;
        CardData card = CardDatabase.Instance != null ? CardDatabase.Instance.GetTemplate(templateID) : null;
        return Classify(card);
    }

    /// <summary>按卡牌数据判定（效果文本 + 特性文本）。</summary>
    public static SelectionKind Classify(CardData card)
    {
        if (card == null) return SelectionKind.Neutral;
        if (!string.IsNullOrEmpty(card.templateID) &&
            Explicit.TryGetValue(card.templateID, out SelectionKind pinned))
            return pinned;

        string text = (card.effect ?? string.Empty) + "\n" + (card.traits ?? string.Empty);
        if (text.Length == 0) return SelectionKind.Neutral;

        if (ContainsAny(text, DebuffKeywords)) return SelectionKind.Debuff;
        if (ContainsAny(text, DamageKeywords)) return SelectionKind.Damage;
        // 「无法恢复生命值」这类否定句不是治愈（如 01531 亡命之徒）
        if (ContainsAny(text, HealKeywords) && !text.Contains("无法恢复")) return SelectionKind.Heal;
        return SelectionKind.Neutral;
    }

    static bool ContainsAny(string text, string[] keywords)
    {
        for (int i = 0; i < keywords.Length; i++)
            if (text.Contains(keywords[i])) return true;
        return false;
    }
}
