using System.Collections.Generic;

/// <summary>
/// [PLACEHOLDER] Centralized prefix definitions and effects.
/// Each prefix has hooks: OnTurnStart, OnPhaseEnd, OnDamageCalculate, etc.
/// Current prefixes: 渊, 机械, 灵能, 血歌, 神灵画卷, 群峦, 潮汐.
/// </summary>
public static class PrefixRegistry
{
    public delegate void PrefixHook(CardInstance owner);

    private static Dictionary<string, PrefixDef> _prefixes = new Dictionary<string, PrefixDef>();

    public class PrefixDef
    {
        public string name;
        public string iconID;
        public PrefixHook onTurnStart;
        public PrefixHook onBattleEnd;
    }

    public static void Register(string key, PrefixDef def) { }
    public static PrefixDef Get(string key) => null;
    public static bool HasPrefix(CardInstance ci, string key) =>
        ci != null && ci.prefixes != null && ci.prefixes.Contains(key);
}
