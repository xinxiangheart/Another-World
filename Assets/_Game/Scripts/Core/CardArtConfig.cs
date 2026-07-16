using UnityEngine;

// ============================================================================
// CardArtConfig — 全局卡牌底图/边框配置（ScriptableObject）
// ============================================================================
//
// 在 Project 窗口右键 → Another World → Card Art Config 创建。
// 拖入各前缀/类型的底图和边框 Sprite，所有卡共用。
//
// 使用方式：
//   材质实例三纹理 = CardArtConfig.GetTextures(cardData, cardInstance)
// ============================================================================

[CreateAssetMenu(menuName = "Another World/Card Art Config", fileName = "CardArtConfig")]
public class CardArtConfig : ScriptableObject
{
    [Header("底图 — 按费用 / 类型")]
    public Sprite bgSummon1Cost;
    public Sprite bgSummon3Cost;
    public Sprite bgSummon5Cost;
    public Sprite bgSpellNormal;
    public Sprite bgSpellEvil;
    public Sprite bgSpellCounter;
    public Sprite bgSpecial;           // 特殊召唤物 / 神选者

    [Header("边框 — 按前缀")]
    public Sprite borderDefault;        // 无前缀
    public Sprite borderAbyss;          // 渊
    public Sprite borderMech;           // 机械
    public Sprite borderPsychic;        // 灵能
    public Sprite borderBloodsong;      // 血歌
    public Sprite borderScroll;         // 神灵画卷
    public Sprite borderChosenOne;      // 神选者

    static CardArtConfig _instance;
    public static CardArtConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<CardArtConfig>("CardArtConfig");
            return _instance;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 公开 API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>获取 3D 材质需要的三张贴图。</summary>
    public static (Texture2D bg, Texture2D border, Texture2D art) Get3DTextures(
        CardData template, CardInstance instance)
    {
        var cfg = Instance;
        Texture2D bg    = cfg != null ? GetBackground(cfg, template, instance).texture : Texture2D.whiteTexture;
        Texture2D border = cfg != null ? GetBorder(cfg, instance).texture : Texture2D.whiteTexture;
        Texture2D art   = template.cardSprite2D != null ? template.cardSprite2D.texture : Texture2D.whiteTexture;
        return (bg, border, art);
    }

    /// <summary>获取 2D 手牌的底图 Sprite。</summary>
    public static Sprite Get2DBackground(CardData template, CardInstance instance)
    {
        var cfg = Instance;
        return cfg != null ? GetBackground(cfg, template, instance) : null;
    }

    /// <summary>获取 2D 手牌的边框 Sprite。</summary>
    public static Sprite Get2DBorder(CardInstance instance)
    {
        var cfg = Instance;
        return cfg != null ? GetBorder(cfg, instance) : null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 内部选择逻辑
    // ═══════════════════════════════════════════════════════════════════

    static Sprite GetBackground(CardArtConfig cfg, CardData template, CardInstance instance)
    {
        if (template.cardType == CardType.Spell)
        {
            if ((template.spellType & SpellType.Counter) != 0 && cfg.bgSpellCounter != null)
                return cfg.bgSpellCounter;
            if ((template.spellType & SpellType.Evil) != 0 && cfg.bgSpellEvil != null)
                return cfg.bgSpellEvil;
            return cfg.bgSpellNormal != null ? cfg.bgSpellNormal : cfg.bgSummon3Cost;
        }
        // 召唤物按费用
        if (template.baseCost <= 1 && cfg.bgSummon1Cost != null)
            return cfg.bgSummon1Cost;
        if (template.baseCost <= 3 && cfg.bgSummon3Cost != null)
            return cfg.bgSummon3Cost;
        if (cfg.bgSummon5Cost != null) return cfg.bgSummon5Cost;
        return cfg.bgSpecial; // fallback
    }

    static Sprite GetBorder(CardArtConfig cfg, CardInstance instance)
    {
        string prefixes = instance.prefixes;
        if (string.IsNullOrEmpty(prefixes) || prefixes == "无")
            return cfg.borderDefault != null ? cfg.borderDefault : null;

        if (prefixes.Contains("渊")   && cfg.borderAbyss     != null) return cfg.borderAbyss;
        if (prefixes.Contains("机械") && cfg.borderMech      != null) return cfg.borderMech;
        if (prefixes.Contains("灵能") && cfg.borderPsychic   != null) return cfg.borderPsychic;
        if (prefixes.Contains("血歌") && cfg.borderBloodsong != null) return cfg.borderBloodsong;
        if (prefixes.Contains("神灵画卷") && cfg.borderScroll != null) return cfg.borderScroll;

        return cfg.borderDefault != null ? cfg.borderDefault : null;
    }
}
