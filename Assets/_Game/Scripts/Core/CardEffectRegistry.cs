using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Central registry mapping CardData.templateID→effect handlers.
/// Replaces the large switch-case in BoardSlot.StartOnEnterEffect / CardDrag.ResolveSpellEffect.
/// Format: "03504_ENTER" → Action<CardInstance, BoardSlot> lambda.
/// </summary>
public static class CardEffectRegistry
{
    private static Dictionary<string, Action<CardInstance, BoardSlot>> _enterEffects =
        new Dictionary<string, Action<CardInstance, BoardSlot>>();
    private static Dictionary<string, Action<CardData, BoardSlot>> _spellEffects =
        new Dictionary<string, Action<CardData, BoardSlot>>();
    private static Dictionary<string, Action<CardInstance, int>> _deathEffects =
        new Dictionary<string, Action<CardInstance, int>>();

    public static void RegisterEnter(string templateID, Action<CardInstance, BoardSlot> handler)
    {
        _enterEffects[templateID + "_ENTER"] = handler;
    }

    public static Action<CardInstance, BoardSlot> GetEnter(string key)
    {
        _enterEffects.TryGetValue(key, out var h);
        return h;
    }

    public static void RegisterSpell(string templateID, Action<CardData, BoardSlot> handler)
    {
        _spellEffects[templateID + "_SPELL"] = handler;
    }

    public static Action<CardData, BoardSlot> GetSpell(string key)
    {
        _spellEffects.TryGetValue(key, out var h);
        return h;
    }
}
