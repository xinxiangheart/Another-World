using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Multi-stage damage calculation pipeline inspired by Slay the Spire.
/// Stage1: attacker buffs (power/tier/prefix) → Stage2: defender debuffs (vulnerable/shield)
/// → Stage3: final modifications → Stage4: apply (shield absorb → HP subtraction).
/// Replaces hard-coded trait checks scattered in BattleManager and BoardSlot.
/// </summary>
public static class DamagePipeline
{
    public enum Stage { Give, Receive, FinalGive, FinalReceive }

    public delegate int Modifier(CardInstance owner, int damage, Stage stage);

    public static void RegisterModifier(string powerID, Modifier mod) { }

    public static int Calculate(int baseDamage, CardInstance attacker, CardInstance defender,
        DamageType type = DamageType.Normal)
    {
        int dmg = baseDamage;
        // Stage1: attacker buffs
        // Stage2: defender debuffs
        // Stage3: final give
        // Stage4: final receive
        return Mathf.Max(0, dmg);
    }

    public enum DamageType { Normal, True, LifeLoss }
}
