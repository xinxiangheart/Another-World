using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// EffectRegistry — (templateID, Trigger) → 效果 handler 注册表
// ============================================================================
//
// 取代三大 switch 的分发核心。每张卡把原 case body 迁为一次 Register：
//   EffectRegistry.Register("01509", Trigger.Enter, ctx => { ... 入队 IGameAction ... });
//
// key 一律用 templateID（稳定 ID），彻底摆脱按中文效果串分发 —— 这是多语言的前提：
// 效果逻辑与显示文本解耦，翻译只需替换文本表，注册逻辑零改动。
//
// 注册在各 handler 文件的静态方法里完成，由 EnsureRegistered() 汇总触发一次。
// ============================================================================

/// <summary>效果 handler：读 ctx，把若干 IGameAction 入队。</summary>
public delegate void EffectHandler(EffectContext ctx);

public static class EffectRegistry
{
    static readonly Dictionary<(string, Trigger), EffectHandler> _handlers
        = new Dictionary<(string, Trigger), EffectHandler>();

    static bool _initialized;

    /// <summary>注册一张卡在某触发时机的效果。重复注册会覆盖并告警。</summary>
    public static void Register(string templateID, Trigger trigger, EffectHandler handler)
    {
        if (string.IsNullOrEmpty(templateID) || handler == null) return;
        var key = (templateID, trigger);
        if (_handlers.ContainsKey(key))
            Debug.LogWarning($"[EffectRegistry] 重复注册 {templateID}:{trigger}，已覆盖。");
        _handlers[key] = handler;
    }

    /// <summary>查表。命中返回 true 并输出 handler。</summary>
    public static bool TryGet(string templateID, Trigger trigger, out EffectHandler handler)
    {
        handler = null;
        if (string.IsNullOrEmpty(templateID)) return false;
        return _handlers.TryGetValue((templateID, trigger), out handler);
    }

    public static bool Has(string templateID, Trigger trigger)
        => !string.IsNullOrEmpty(templateID) && _handlers.ContainsKey((templateID, trigger));

    /// <summary>
    /// 汇总各卡注册。迁移期各 handler 文件的 [RuntimeInitializeOnLoadMethod] 会自行注册，
    /// 此处仅作幂等保护 / 汇总入口。
    /// </summary>
    public static void EnsureRegistered()
    {
        if (_initialized) return;
        _initialized = true;
        // 迁移期：各 handler 文件通过 [RuntimeInitializeOnLoadMethod] 独立注册，
        // 无需在此集中调用。若后续改为集中注册，在这里调用各 RegisterXxx()。
    }

    /// <summary>调试：当前已注册条目数。</summary>
    public static int Count => _handlers.Count;
}
