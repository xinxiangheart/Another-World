using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public static class EffectDispatcher
{
    public static TMP_Text debugText;

    /// <summary>正在执行的特性栈（模板ID）。槽位选择指示器靠它判定「这次选择属于哪张卡」以决定颜色
    /// （Damage 红 / Heal 绿 / Debuff 紫 / Neutral 金），所以必须覆盖整个特性耗时——
    /// Dispatch 入栈，特性收尾（隐藏文字）时出栈。客户端由 TargetShowEffectText / TargetHideEffectText 同步。
    /// 判不出（栈空）时下游按中性处理，不会报错。</summary>
    static readonly List<(string templateID, Trigger trigger)> inFlightEffects =
        new List<(string templateID, Trigger trigger)>();

    /// <summary>当前正在执行的特性所属模板ID；没有正在执行的特性时为 null。</summary>
    public static string CurrentEffectTemplateID =>
        inFlightEffects.Count > 0 ? inFlightEffects[inFlightEffects.Count - 1].templateID : null;

    /// <summary>是否有特性正在执行（选择类型判定的前提）。</summary>
    public static bool HasCurrentEffect => inFlightEffects.Count > 0;

    /// <summary>当前正在执行的特性触发类型（进场/退场/抛置/法术…）。无执行中的特性时结果无意义，先看 <see cref="HasCurrentEffect"/>。</summary>
    public static Trigger CurrentEffectTrigger =>
        inFlightEffects.Count > 0 ? inFlightEffects[inFlightEffects.Count - 1].trigger : Trigger.Enter;

    static void PushInFlight(string templateID, Trigger trigger)
    {
        if (string.IsNullOrEmpty(templateID)) return;
        inFlightEffects.Add((templateID, trigger));
    }

    static void PopInFlight(string templateID)
    {
        if (inFlightEffects.Count == 0) return;
        // 正常情况栈顶就是它；嵌套/异常时序下不强求，退到能找到的那一层为止
        if (templateID != null && inFlightEffects[inFlightEffects.Count - 1].templateID == templateID)
            inFlightEffects.RemoveAt(inFlightEffects.Count - 1);
        else
            inFlightEffects.RemoveAll(e => e.templateID == templateID);
    }

    static void ClearInFlight() => inFlightEffects.Clear();

    /// <summary>客户端收到服务端广播的特性文字。带上模板ID，指示器据此着色。</summary>
    public static void ShowDebugText(string templateID, string cardName, string traitCN)
    {
        PushInFlight(templateID, TriggerFromTraitCN(traitCN));
        ShowDebugText(cardName, traitCN);
    }

    /// <summary>广播的特性中文名 → 触发类型（客户端拿到的是中文串，没有 Trigger 字段）。</summary>
    static Trigger TriggerFromTraitCN(string traitCN)
    {
        switch (traitCN)
        {
            case "退场": return Trigger.Exit;
            case "主动退场": return Trigger.ActiveExit;
            case "抛置": return Trigger.Discard;
            case "法术": return Trigger.Spell;
            case "先手": return Trigger.FirstStrike;
            case "附着": return Trigger.Attach;
            case "攻击对位": return Trigger.AttackPair;
            case "反击": return Trigger.Revenge;
            default: return Trigger.Enter;
        }
    }

    /// <summary>客户端直接设文字（不经过 Dispatch）</summary>
    public static void ShowDebugText(string cardName, string traitCN)
    {
        if (debugText != null)
        {
            debugText.text = $"{cardName}的{traitCN}";
            debugText.gameObject.SetActive(true);
        }
    }

    /// <summary>客户端清除文字</summary>
    public static void HideDebugText()
    {
        ClearInFlight();
        if (debugText != null)
            debugText.gameObject.SetActive(false);
    }

    public static bool Dispatch(Trigger trigger, EffectContext ctx)
    {
        if (ctx == null) return false;
        EffectRegistry.EnsureRegistered();

        string id = ctx.TemplateID;
        if (string.IsNullOrEmpty(id)) return false;

        ctx.trigger = trigger;
        if (EffectRegistry.TryGet(id, trigger, out var handler))
        {
            string cardName = CardDatabase.Instance?.GetTemplate(id)?.cardName ?? id;
            string traitCN = trigger switch
            {
                Trigger.Enter => "进场",
                Trigger.Exit => "退场",
                Trigger.ActiveExit => "主动退场",
                Trigger.Discard => "抛置",
                Trigger.Spell => "法术",
                Trigger.FirstStrike => "先手",
                Trigger.Attach => "附着",
                Trigger.AttackPair => "攻击对位",
                Trigger.Revenge => "反击",
                _ => trigger.ToString()
            };
            if (debugText != null)
            {
                debugText.text = $"{cardName}[{id}]的{traitCN}";
                debugText.gameObject.SetActive(true);
            }

            // 广播给所有客户端（包括远程玩家）
            EffectTextBroadcaster.Show(id, cardName, traitCN);

            Coroutine prevCoroutine = ctx.StartedCoroutine;
            ctx.SupervisorCoroutine = null;
            PushInFlight(id, trigger);
            handler(ctx);

            // 挂监督协程：等特性完成后清除文字+广播隐藏。
            // 它是 StartedCoroutine 的唯一等待者——其它地方（StartOnEnterEffect / SpellPending / 子 dispatch）
            // 必须等 SupervisorCoroutine，否则同一协程两个等待者 → 第二个永久挂起 → Enter_xxx 嵌套泄漏。
            var runner = ctx.sourceSlot ?? Object.FindObjectOfType<BoardSlot>();
            if (ctx.StartedCoroutine != null && ctx.StartedCoroutine != prevCoroutine)
            {
                // 异步handler: 等待协程完成
                if (runner != null)
                    ctx.SupervisorCoroutine = runner.StartCoroutine(HideAfterCoroutine(ctx.StartedCoroutine, id));
                else
                {
                    PopInFlight(id);
                    if (debugText != null) debugText.gameObject.SetActive(false);
                    EffectTextBroadcaster.Hide();
                }
            }
            else
            {
                // 同步handler: 延迟0.5s再清除（给客户端 TargetRpc 到达时间）
                if (runner != null)
                    runner.StartCoroutine(HideAfterDelay(0.5f, id));
                else
                {
                    PopInFlight(id);
                    if (debugText != null) debugText.gameObject.SetActive(false);
                    EffectTextBroadcaster.Hide();
                }
            }

            return true;
        }
        return false;
    }

    public static bool IsMigrated(string templateID, Trigger trigger)
        => EffectRegistry.Has(templateID, trigger);

    static System.Collections.IEnumerator HideAfterCoroutine(Coroutine co, string templateID)
    {
        if (co != null) yield return co;
        PopInFlight(templateID);
        if (debugText != null) debugText.gameObject.SetActive(false);
        EffectTextBroadcaster.Hide();
    }

    static System.Collections.IEnumerator HideAfterDelay(float seconds, string templateID)
    {
        yield return new WaitForSeconds(seconds);
        PopInFlight(templateID);
        if (debugText != null) debugText.gameObject.SetActive(false);
        EffectTextBroadcaster.Hide();
    }
}
