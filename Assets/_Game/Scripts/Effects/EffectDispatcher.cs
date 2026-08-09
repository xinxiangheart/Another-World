using System.Collections;
using UnityEngine;
using TMPro;

public static class EffectDispatcher
{
    public static TMP_Text debugText;

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

            Coroutine prevCoroutine = ctx.StartedCoroutine;
            handler(ctx);

            // 同步handler: 执行完立刻清除文字
            if (ctx.StartedCoroutine == null || ctx.StartedCoroutine == prevCoroutine)
            {
                if (debugText != null) debugText.gameObject.SetActive(false);
            }
            else
            {
                // 异步handler: 挂一个监督协程，等特性协程完成后清除文字
                var runner = ctx.sourceSlot ?? Object.FindObjectOfType<BoardSlot>();
                if (runner != null)
                    runner.StartCoroutine(HideAfterCoroutine(ctx.StartedCoroutine));
                else if (debugText != null)
                    debugText.gameObject.SetActive(false);
            }

            return true;
        }
        return false;
    }

    public static bool IsMigrated(string templateID, Trigger trigger)
        => EffectRegistry.Has(templateID, trigger);

    static System.Collections.IEnumerator HideAfterCoroutine(Coroutine co)
    {
        if (co != null) yield return co;
        if (debugText != null) debugText.gameObject.SetActive(false);
    }
}
