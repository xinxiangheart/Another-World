using UnityEngine;

public static class SpellEffectExecutor
{
    public static void Execute(CardData template, BoardSlot targetSlot)
    {
        // 复用 CardDrag 中的法术效果结算逻辑
        // 临时方案：直接调 ResolveSpellEffect 的静态版本
        CardDrag.ExecuteSpellEffect(template, targetSlot);
    }
}