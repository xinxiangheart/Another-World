using System;
using System.Collections.Generic;

// ============================================================================
// RuntimeTrait — 运行时特性（数据/逻辑层）。
// 复用 CardInstance 的 TraitEntry 数据（固有来自模板 CardData.GetTraitEntryList，
// 授予来自 grantedTraits），不重建特性结构。每卡一个 TraitGroup 持有。
//
// traitId 生成规则：
//   属性优先级：进场 > 先手 > 反击 > 退场 > 主动退场 > 抛置 > 附着 > 赋予
//   单属性："先手:文本"；多属性取最高优先级；赋予类："赋予:文本"；冲突加 "#N" 后缀
// ============================================================================

public class RuntimeTrait
{
    public string traitId;          // 唯一标识（属性优先级 + ":" + 文本；冲突 #N）
    public string text;             // 特性文本
    /// <summary>属性类集合（进场/先手/反击/退场/主动退场/抛置/附着/赋予；多属性可同存）。
    /// 固有来自模板 TraitEntry.GetAttributes()，授予来自 grantedTraits.attributes（空则文本前缀兜底）。
    /// 5.x 起按此做 per-class 拥有/激活查询（HasClass/HasActiveClass），不靠文本前缀（多属性/无前缀条目会漏判）。</summary>
    public string[] attributes;
    public string sourceTemplateID; // null=固有（模板自带）；非空=授予来源（供 RefreshGranted 区分/移除）
    public bool isPersistent;       // 常驻？人工声明，默认 false（一次性不重结算）
    public bool hasTargets;         // 有作用目标？人工声明，默认 false
    public EffectCategory receiveBlocks; // 常驻生效时拦截的接收类别（如禁疗 → Healed）；特性被禁则恢复可接收
    public Action applyEffect;      // 生效方法（空=零变化；未迁移特性为空）
    public Action removeEffect;     // 失效方法（空=零变化）

    /// <summary>属性优先级：进场 > 先手 > 反击 > 退场 > 主动退场 > 抛置 > 附着 > 赋予。</summary>
    public static readonly string[] AttributePriority =
        { "进场", "先手", "反击", "退场", "主动退场", "抛置", "附着", "赋予" };

    /// <summary>生成基础 traitId：取属性优先级最高的属性 + ":" + 文本；无属性用 "无属性:"。</summary>
    public static string BuildBaseId(string[] attributes, string text)
    {
        string chosen = null;
        if (attributes != null)
        {
            foreach (var p in AttributePriority)
                if (System.Array.IndexOf(attributes, p) >= 0) { chosen = p; break; }
        }
        return (chosen != null ? chosen + ":" : "无属性:") + (text ?? "");
    }

    /// <summary>在现有列表里生成唯一 traitId：基础 id 已存在 → 追加 "#N" 后缀（N 从 2 起）。</summary>
    public static string BuildUniqueId(string[] attributes, string text, List<RuntimeTrait> existing)
    {
        string baseId = BuildBaseId(attributes, text);
        if (existing == null || !Existing(existing, baseId)) return baseId;
        int n = 2;
        while (Existing(existing, baseId + "#" + n)) n++;
        return baseId + "#" + n;
    }

    static bool Existing(List<RuntimeTrait> list, string id)
    {
        foreach (var t in list) if (t != null && t.traitId == id) return true;
        return false;
    }
}
