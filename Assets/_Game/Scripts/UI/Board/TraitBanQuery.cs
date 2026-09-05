using UnityEngine;

// ============================================================================
// TraitBanQuery — 6.x 表现层共享助手：特性类"被禁"判定 + 紧凑禁制原因 + 状态来源文案。
//
// 与规则判定同源（不另造一套）：
//   - 完全沉默(BlockAll)：ci.silencedThisPhase(神官03501) ∪ 01335 能量骇客对位
//     → GlobalEventManager.IsFullySilenced(ci)（已含本地光环 + 板面兜底）。
//   - 光环类禁(BlockCategory)：GlobalEventManager.IsTraitBlocked(ci, cls)
//     → 01515 狂热萨满禁敌半场 进场/抛置；01323 法官禁敌半场 退场（含 主动退场，查询映射"退场"）。
//   - 先手/反击/附着 等只可能被完全沉默禁（无法被光环类禁）。
// 禁制来源卡名由模板ID查 CardInstance.GetCardName（空守卫、查不到回退ID）。
//
// 备注：当前运行时的"光环类禁"实际是持续现查（AuraBase.BlocksTrait / IsTraitBlockedByBoardState），
// 并非 TraitGroup 计数框架的 BlockCategory/BlockTrait（那套运行时基本未接线，只有 BlockAll 由
// silencedThisPhase→ApplySilenceToTraits 驱动）。本查询以实时谓词为准，与图标/悬停展示一致。
// ============================================================================

public static class TraitBanQuery
{
    /// <summary>特性图标被禁置灰色调（乘法着色：压暗原彩图以示失效）。可调。</summary>
    public static readonly Color BlockedTint = new Color(0.6f, 0.6f, 0.6f, 1f);

    /// <summary>完全沉默：本阶段被沉默(03501)，或被 01335 能量骇客对位/沉默光环压制。</summary>
    public static bool IsFullySilenced(CardInstance ci)
    {
        if (ci == null) return false;
        if (ci.silencedThisPhase) return true;
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci);
    }

    /// <summary>该特性类当前是否被禁（完全沉默 BlockAll 或光环类禁都算）。非板面卡恒 false。
    /// cls ∈ 进场/先手/反击/退场/主动退场/抛置/附着/赋予…；"主动退场"由法官按"退场"一并禁。</summary>
    public static bool ClassBlocked(CardInstance ci, string cls)
    {
        if (ci == null || string.IsNullOrEmpty(cls)) return false;
        if (IsFullySilenced(ci)) return true;
        string q = cls == "主动退场" ? "退场" : cls;
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(ci, q);
    }

    /// <summary>禁制类型文案（光环类禁；完全沉默有专门整卡文案不走此函数）。</summary>
    public static string BanTypeLabel(string cls)
    {
        switch (cls)
        {
            case "进场":
            case "抛置":      return "禁进场/抛置";
            case "退场":
            case "主动退场":  return "禁退场";
            default:          return "";
        }
    }

    /// <summary>某特性类被禁的完整原因来源模板ID（光环类禁）。完全沉默不在此列。</summary>
    public static string BanSourceTemplateID(string cls)
    {
        switch (cls)
        {
            case "进场":
            case "抛置":      return "01515"; // 狂热萨满
            case "退场":
            case "主动退场":  return "01323"; // 法官
            default:          return "";
        }
    }

    /// <summary>紧凑禁制原因："被X禁制（禁退场）"。未禁 → 空串。供悬停/详情行尾标注。</summary>
    public static string ClassBanReason(CardInstance ci, string cls)
    {
        if (!ClassBlocked(ci, cls)) return "";
        // 完全沉默优先给整卡原因（整卡所有类都被禁，逐类报光环太琐碎）
        if (IsFullySilenced(ci))
        {
            bool phaseSilence = ci.silencedThisPhase;
            string srcID = phaseSilence ? "03501" : "01335";
            string type = phaseSilence ? "完全沉默" : "封锁全部特性";
            return $"被{CardInstance.GetCardName(srcID)}禁制（{type}）";
        }
        string srcID2 = BanSourceTemplateID(cls);
        string typeLabel = BanTypeLabel(cls);
        if (string.IsNullOrEmpty(srcID2) || string.IsNullOrEmpty(typeLabel)) return "";
        return $"被{CardInstance.GetCardName(srcID2)}禁制（{typeLabel}）";
    }

    /// <summary>完全沉默的整卡原因（供无 attribute 条目 / 兜底）。未全沉默 → 空串。</summary>
    public static string FullSilenceReason(CardInstance ci)
    {
        if (!IsFullySilenced(ci)) return "";
        bool phaseSilence = ci.silencedThisPhase;
        string srcID = phaseSilence ? "03501" : "01335";
        string type = phaseSilence ? "完全沉默" : "封锁全部特性";
        return $"被{CardInstance.GetCardName(srcID)}禁制（{type}）";
    }

    /// <summary>单条状态条目的展示文案：描述 [+ ｜来源：卡名]。sourceName 为空则只描述；空条目返回 null。</summary>
    public static string StatusWithSource(CardInstance.ActiveStatus a)
    {
        if (a == null) return null;
        string desc = (a.description ?? "").Trim();
        if (desc.Length == 0) return null;
        string src = (a.sourceName ?? "").Trim();
        if (src.Length == 0) return desc;
        return desc + "｜来源：" + src;
    }
}
