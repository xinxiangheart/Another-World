using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    public string templateID;
    public string cardName;
    public CardType cardType;
    public int copyCount = 3;

    [Header("召唤物属性")]
    public bool addToMainDeck = true; // 是否加入主牌库
    public SummonType summonType;
    public int baseCost;
    public int baseTier = 1;
    public string prefix = "无";
    public int baseHealth;
    public int baseAttack;
    [Header("特性标记")]
    public bool hasFirstStrike;      // 先手
    public bool hasOnEnter;          // 进场
    public bool hasOnDeath;          // 退场
    public bool hasActiveExit;       // 主动退场
    public bool hasRevenge;          // 反击
    public bool hasDiscard;          // 抛置
    public bool canAttach; // 是否拥有附着特性
    public CounterTriggerTiming counterTiming;
    public string counterTriggerCondition;
    public string counterEffect;
    public int counterDuration;
    public bool isXValue;
    public bool xHealthReadsHighest;
    public bool xAttackReadsHighest;
    public bool attacksFrontRow;
    public bool attacksBackRow;
    [TextArea] public string revengeEffect; // 反击效果文本
    [TextArea] public string traits;

    [Header("特性属性标记")]
    [Tooltip("与 traits 平行存储，一行对应 traits 同一行（index 对齐）。属性用顿号/逗号分隔。可选：进场/先手/反击/退场/主动退场/抛置/附着/赋予。空行=无属性。")]
    public string traitProperties;

    [Tooltip("结构化特性条目（新存储，优先于 traits/traitProperties 字符串；空列表则回退旧字符串）。")]
    public List<TraitEntry> traitEntries = new List<TraitEntry>();

    [Tooltip("旧 traits 字符串是否已迁移到 traitEntries。")]
    [HideInInspector] public bool traitsMigrated;

    [Header("法术属性")]
    public SpellType spellType;
    [TextArea] public string effect;

    [Header("表现层（预制体引用）")]
    [Tooltip("2D 召唤物预制体（Card00_New_2D）。仅归档/审计；运行时手牌仍走 Player.cardPrefab2D")]
    public GameObject card2DPrefab;
    [Tooltip("2D 法术预制体（SpellCard00_New_2D）。仅归档/审计；运行时手牌仍走 Player.spellCardPrefab2D")]
    public GameObject spell2DPrefab;
    [Tooltip("3D 召唤物预制体（场上模型，Card00_New_3D）")]
    public GameObject prefab3D;
    [Tooltip("3D 法术预制体（反制/计数模型，SpellCard00_3D）")]
    public GameObject spellPrefab3D;

    [Header("Buff/Debuff 持续状态")]
    [Tooltip("是否有正面持续增益（护盾不算——护盾有独立图标；仅持续型效果，一次性效果不算）")]
    public bool hasBuff;
    [Tooltip("正面增益描述文本（勾选 hasBuff 后显示并填写）")]
    public string buffText;
    [Tooltip("是否有负面持续减益（护盾不算；仅持续型效果，一次性效果不算）")]
    public bool hasDebuff;
    [Tooltip("负面减益描述文本（勾选 hasDebuff 后显示并填写）")]
    public string debuffText;

    [Header("目标选择")]
    public TargetType targetType = TargetType.None;

    /// <summary>牌库唯一实例 ID（运行时由 CardZoneManager 生成，非持久化）。</summary>
    [System.NonSerialized] public string _instanceID;

    // ═══════════════════ 特性条目化辅助 ═══════════════════

    /// <summary>特性属性全集（"赋予"=给予别人的特性，自身详情面板不显示、不参与编号）。</summary>
    public static readonly string[] TraitAttributeNames = { "进场", "先手", "反击", "退场", "主动退场", "抛置", "附着", "赋予" };

    /// <summary>traits 拆行并去掉行首"数字."前缀（兼容现有手写编号数据）。空行/纯"无"跳过。</summary>
    public string[] GetTraitLines()
    {
        if (string.IsNullOrEmpty(traits)) return new string[0];
        var lines = traits.Split('\n');
        var result = new List<string>();
        foreach (var l in lines)
        {
            string s = l.Trim();
            if (string.IsNullOrEmpty(s) || s == "无") continue;
            result.Add(StripTraitNumber(s));
        }
        return result.ToArray();
    }

    /// <summary>去掉行首"数字."前缀，如 "1.反击：xxx" → "反击：xxx"。</summary>
    public static string StripTraitNumber(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        int idx = 0;
        while (idx < line.Length && char.IsDigit(line[idx])) idx++;
        if (idx > 0 && idx < line.Length && line[idx] == '.')
            return line.Substring(idx + 1).TrimStart();
        return line;
    }

    /// <summary>第 index 条特性的属性：优先 traitProperties[index]，为空则从文本前缀兜底解析。</summary>
    public string[] GetTraitPropertiesFor(int index, string lineText)
    {
        if (!string.IsNullOrEmpty(traitProperties))
        {
            var propLines = traitProperties.Split('\n');
            if (index >= 0 && index < propLines.Length)
            {
                string pl = propLines[index].Trim();
                if (!string.IsNullOrEmpty(pl))
                    return SplitAttributes(pl);
            }
        }
        return ParseTraitAttributesFromText(lineText);
    }

    /// <summary>把"属性1、属性2"或"属性1,属性2"拆成数组，过滤未知/空项。</summary>
    public static string[] SplitAttributes(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new string[0];
        var parts = raw.Split(new[] { '、', ',', '，', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        var list = new List<string>();
        foreach (var p in parts)
        {
            string t = p.Trim();
            if (System.Array.IndexOf(TraitAttributeNames, t) >= 0)
                list.Add(t);
        }
        return list.ToArray();
    }

    /// <summary>从特性文本前缀解析属性（如"先手：xxx" → [先手]）。"赋予"只来自 traitProperties，不从文本猜测。</summary>
    public static string[] ParseTraitAttributesFromText(string text)
    {
        if (string.IsNullOrEmpty(text)) return new string[0];
        var list = new List<string>();
        foreach (var attr in TraitAttributeNames)
        {
            if (attr == "赋予") continue;
            if (text.StartsWith(attr + "：") || text.StartsWith(attr + ":"))
                list.Add(attr);
        }
        return list.ToArray();
    }

    /// <summary>
    /// 有效特性列表：优先 traitEntries（结构化存储）；空则触发自动迁移（EnsureMigrated）。
    /// </summary>
    public List<TraitEntry> GetTraitEntryList()
    {
        EnsureMigrated();
        return traitEntries;
    }

    /// <summary>在 traitEntries 中精确定位含某属性的条目（如"进场"→isEnter 的条目），不靠文本 Contains。找不到返回 null。</summary>
    public TraitEntry FindTraitByAttribute(string attr)
    {
        var list = GetTraitEntryList();
        if (list == null) return null;
        foreach (var e in list)
            if (e.MatchesAttribute(attr)) return e;
        return null;
    }

    /// <summary>
    /// 迁移旧 traits 字符串 → traitEntries：
    /// 按行拆分 → 去行首"数字."前缀 → 提取行首属性关键词并剥离（"1.进场：xxx" → isEnter=true, text="xxx"）。
    /// traitEntries 非空或已标记迁移则跳过。返回是否本次发生了迁移。
    /// </summary>
    public bool EnsureMigrated()
    {
        if (traitEntries == null) traitEntries = new List<TraitEntry>();
        if (traitEntries.Count > 0) return false;
        if (traitsMigrated) return false;
        if (string.IsNullOrEmpty(traits) || traits == "无")
        {
            traitsMigrated = true;
            return false;
        }

        traitEntries = new List<TraitEntry>();
        foreach (string line in GetTraitLines())
        {
            string text = line;
            var entry = new TraitEntry();
            foreach (var attr in TraitAttributeNames)
            {
                if (text.StartsWith(attr + "：") || text.StartsWith(attr + ":"))
                {
                    entry.SetAttribute(attr, true);
                    text = text.Substring(attr.Length + 1).TrimStart(); // 只保留冒号后内容
                    break;
                }
            }
            entry.text = text;
            traitEntries.Add(entry);
        }
        traitsMigrated = true;
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 编辑器加载/导入时自动迁移旧 traits → traitEntries，并标记资产需保存
        if (EnsureMigrated())
            UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    // ═══════════════════ 结构化特性条目（新存储） ═══════════════════

    /// <summary>一条特性：文本 + 属性标记。isGrant=赋予（给予别人的特性，自身详情面板不显示、不参与编号）。</summary>
    [System.Serializable]
    public class TraitEntry
    {
        public string text;
        public bool isEnter;       // 进场
        public bool isFirstStrike; // 先手
        public bool isRevenge;     // 反击
        public bool isDeath;       // 退场
        public bool isActiveExit;  // 主动退场
        public bool isDiscard;     // 抛置
        public bool isAttach;      // 附着
        public bool isGrant;       // 赋予（给予型）

        /// <summary>非"赋予"的属性名列表（显示用）。"赋予"由 isGrant 单独处理。</summary>
        public string[] GetAttributes()
        {
            var list = new List<string>();
            if (isEnter) list.Add("进场");
            if (isFirstStrike) list.Add("先手");
            if (isRevenge) list.Add("反击");
            if (isDeath) list.Add("退场");
            if (isActiveExit) list.Add("主动退场");
            if (isDiscard) list.Add("抛置");
            if (isAttach) list.Add("附着");
            return list.ToArray();
        }

        /// <summary>按属性名设置标记（兼容 legacy 转换）。</summary>
        public void SetAttribute(string name, bool on)
        {
            switch (name)
            {
                case "进场": isEnter = on; break;
                case "先手": isFirstStrike = on; break;
                case "反击": isRevenge = on; break;
                case "退场": isDeath = on; break;
                case "主动退场": isActiveExit = on; break;
                case "抛置": isDiscard = on; break;
                case "附着": isAttach = on; break;
                case "赋予": isGrant = on; break;
            }
        }

        /// <summary>是否含某属性（精确匹配，非文本 Contains）。</summary>
        public bool MatchesAttribute(string attr)
        {
            switch (attr)
            {
                case "进场": return isEnter;
                case "先手": return isFirstStrike;
                case "反击": return isRevenge;
                case "退场": return isDeath;
                case "主动退场": return isActiveExit;
                case "抛置": return isDiscard;
                case "附着": return isAttach;
                case "赋予": return isGrant;
            }
            return false;
        }
    }
}