using UnityEngine;

// ============================================================================
// EffectContext — 效果 handler 的统一上下文
// ============================================================================
//
// 承载现有三大 switch 的 case body 从参数/静态字段读取的一切：
//   进场 : source(inst) + sourceSlot(this) + template
//   法术 : template + targetSlot（选择结果）
//   抛置 : source(deadInstance) + discardSlotID + savedAttack
//   退场 : source + sourceSlot + isActiveExit + savedAttack
//
// handler 内部只做一件事：根据 ctx 把 1..N 个 IGameAction 入队（ActionQueueManager）。
// ============================================================================

/// <summary>效果 handler 的统一上下文。字段按需填充，未涉及的留默认值。</summary>
public class EffectContext
{
    /// <summary>效果来源单位（进场/退场为自身，抛置为被抛的牌）。</summary>
    public CardInstance source;

    /// <summary>来源所在槽位（进场/退场时有效）。</summary>
    public BoardSlot sourceSlot;

    /// <summary>卡牌模板（进场/法术使用）。</summary>
    public CardData template;

    /// <summary>目标槽位（法术/需选择的效果，由选择结果填入）。</summary>
    public BoardSlot targetSlot;

    /// <summary>本次退场是否为主动退场（抛置/牺牲）。</summary>
    public bool isActiveExit;

    /// <summary>退场/抛置前保存的攻击力（部分效果按此结算，如 01343）。</summary>
    public int savedAttack;

    /// <summary>抛置来源槽位 ID（Card3DHover.HandleDiscardEffect 用）。</summary>
    public int discardSlotID = -1;

    /// <summary>触发时机（分发时写入，handler 可读）。</summary>
    public Trigger trigger;

    /// <summary>handler 启动的协程引用。父协程通过 yield return 此引用来等待嵌套同时树完成。</summary>
    public Coroutine StartedCoroutine;

    // ---- 死亡后处理回手标志（handler 设置 → DeathPipeline 消费） ----

    public bool shouldReturn03504;
    public CardData template03504;
    public bool shouldReturn01117;
    public CardData template01117;
    public bool shouldReturn03009;
    public CardData template03009;

    // ---- 便捷构造 ----------------------------------------------------------

    public static EffectContext ForEnter(CardData template, CardInstance source, BoardSlot sourceSlot)
        => new EffectContext { template = template, source = source, sourceSlot = sourceSlot, trigger = Trigger.Enter };

    public static EffectContext ForSpell(CardData template, BoardSlot targetSlot)
        => new EffectContext { template = template, targetSlot = targetSlot, trigger = Trigger.Spell };

    public static EffectContext ForDiscard(CardInstance source, int discardSlotID)
        => new EffectContext { source = source, discardSlotID = discardSlotID, savedAttack = source != null ? source.savedAttackForDiscard : 0, trigger = Trigger.Discard };

    public static EffectContext ForExit(CardInstance source, BoardSlot sourceSlot, bool isActiveExit)
        => new EffectContext { source = source, sourceSlot = sourceSlot, isActiveExit = isActiveExit, trigger = isActiveExit ? Trigger.ActiveExit : Trigger.Exit };

    // ---- 便捷访问 ----------------------------------------------------------

    /// <summary>来源模板 ID（优先 source，其次 template）。</summary>
    public string TemplateID =>
        source != null ? source.templateID :
        template != null ? template.templateID : null;

    public int SourceSlotID => sourceSlot != null ? sourceSlot.slotID : -1;
}
