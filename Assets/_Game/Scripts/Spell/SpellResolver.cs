using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Unified spell effect resolver.
/// Replaces the 2000-line ResolveSpellEffect switch-case in CardDrag.
/// Each spell registers its effect handler via CardEffectRegistry.
/// </summary>
public static class SpellResolver
{
    public static void Resolve(CardData template, BoardSlot targetSlot)
    {
        var handler = CardEffectRegistry.GetSpell(template.templateID + "_SPELL");
        handler?.Invoke(template, targetSlot);
    }
}
