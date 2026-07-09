using UnityEngine;

// ============================================================================
// EffectDispatcher — 三大 switch 的统一分发入口（双轨迁移）
// ============================================================================
//
// 迁移期：每个旧 switch 调用点前置一次 Dispatch。命中注册表则由 handler 入队执行，
// 未命中返回 false，调用点回退到旧 switch。迁一张卡 → 注册一条 → 删对应旧 case，
// 任意中途游戏都可运行。
//
//   // 进场（BoardSlot.StartOnEnterEffect 顶部）
//   var ctx = EffectContext.ForEnter(template, inst, this);
//   if (EffectDispatcher.Dispatch(Trigger.Enter, ctx)) return;
//   // ...原 switch 作为回退...
//
// 全部迁完后（Step 7）删除回退分支。
// ============================================================================

public static class EffectDispatcher
{
    /// <summary>
    /// 按 (templateID, trigger) 分发。命中并执行返回 true；未命中返回 false（调用点回退旧逻辑）。
    /// templateID 优先取 ctx.source，其次 ctx.template。
    /// </summary>
    public static bool Dispatch(Trigger trigger, EffectContext ctx)
    {
        if (ctx == null) return false;
        EffectRegistry.EnsureRegistered();

        string id = ctx.TemplateID;
        if (string.IsNullOrEmpty(id)) return false;

        ctx.trigger = trigger;
        if (EffectRegistry.TryGet(id, trigger, out var handler))
        {
            handler(ctx);
            return true;
        }
        return false;
    }

    /// <summary>是否已有该卡该时机的注册（调用点可用来决定是否走新路径）。</summary>
    public static bool IsMigrated(string templateID, Trigger trigger)
        => EffectRegistry.Has(templateID, trigger);
}
