using System.Collections;
using UnityEngine;
using TMPro;

public static class EffectDispatcher
{
    public static TMP_Text debugText;

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
            handler(ctx);

            // 挂监督协程：等特性完成后清除文字+广播隐藏
            var runner = ctx.sourceSlot ?? Object.FindObjectOfType<BoardSlot>();
            if (ctx.StartedCoroutine != null && ctx.StartedCoroutine != prevCoroutine)
            {
                // 异步handler: 等待协程完成
                if (runner != null)
                    runner.StartCoroutine(HideAfterCoroutine(ctx.StartedCoroutine));
                else
                {
                    if (debugText != null) debugText.gameObject.SetActive(false);
                    EffectTextBroadcaster.Hide();
                }
            }
            else
            {
                // 同步handler: 延迟0.5s再清除（给客户端 TargetRpc 到达时间）
                if (runner != null)
                    runner.StartCoroutine(HideAfterDelay(0.5f));
                else
                {
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

    static System.Collections.IEnumerator HideAfterCoroutine(Coroutine co)
    {
        if (co != null) yield return co;
        if (debugText != null) debugText.gameObject.SetActive(false);
        EffectTextBroadcaster.Hide();
    }

    static System.Collections.IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (debugText != null) debugText.gameObject.SetActive(false);
        EffectTextBroadcaster.Hide();
    }
}
