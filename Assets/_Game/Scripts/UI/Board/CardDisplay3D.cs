using UnityEngine;
using TMPro;

public class CardDisplay3D : MonoBehaviour
{
    [Header("3D文字")]
    public TextMeshPro attackText;
    public TextMeshPro healthText;
    public TextMeshPro nameText;
    public TextMeshPro costText;
    public TextMeshPro prefixText;
    public TextMeshPro effectText;

    [Header("卡面三层 SpriteRenderer（比例在预制体里手动调，运行时不重算/不覆盖/不缩放）")]
    public SpriteRenderer frameSR;      // 卡框（费用卡框）
    public SpriteRenderer prefixBgSR;   // 前缀背景（按前缀类型切换 sprite，比例用预制体手调值）
    public SpriteRenderer cardArtSR;    // 卡图（无对应卡图时隐藏，露出前缀背景兜底）

    [Header("预览 Sprite（拖入即显示；运行时优先使用，未拖入走路径加载，对齐 2D 拖入字段）")]
    [Tooltip("卡框预览")]
    public Sprite previewFrameSprite;
    [Tooltip("卡图预览")]
    public Sprite previewArtSprite;
    [Tooltip("前缀背景预览")]
    public Sprite previewPrefixBgSprite;

    MaterialPropertyBlock _mpb;
    bool _artInitialized;

    void Awake()
    {
        // 使用 MaterialPropertyBlock 替代每卡独立 Material 实例
        // —— 避免 new Material() 的 GPU 端资源分配，所有卡共享同一材质
        _mpb = new MaterialPropertyBlock();
    }

    /// <summary>设置三层合成贴图（通过 MaterialPropertyBlock，避免每卡独立材质）。</summary>
    public void SetCompositeTextures(Texture2D bg, Texture2D border, Texture2D art)
    {
        var mr = GetComponentInChildren<MeshRenderer>(); // 网格在 ModelRoot 子层级（模型可独立缩放）
        if (mr == null || _mpb == null) return;

        mr.GetPropertyBlock(_mpb);
        if (bg     != null) _mpb.SetTexture("_BgTex", bg);
        if (border != null) _mpb.SetTexture("_BorderTex", border);
        if (art    != null) _mpb.SetTexture("_ArtTex", art);
        mr.SetPropertyBlock(_mpb);
    }

    /// <summary>
    /// 根据卡牌数据和实例自动选择三张贴图并应用（对齐新 2D 卡 Card00_New_2D 的图片加载）：
    ///   _BgTex    = 费用卡框  Cards/SummonCard_{cost}（0-5费）
    ///   _BorderTex = 前缀底图 Cards/PrefixArtBG/{Abyss|Blood|Mech|Psychic|Scroll|Common}
    ///   _ArtTex   = 卡面原画  cardSprite2D → Cards/{templateID}_Front → 镜像 Cards/Summon 目录 → 白
    /// 路径加载失败回退 CardArtConfig；再失败留白（露出底层）。
    /// </summary>
    public void ApplyArtFromCard(CardInstance instance)
    {
        if (instance == null) return;
        CardData template = CardDatabase.Instance?.GetTemplate(instance.templateID);
        if (template == null) return;

        // ── 卡框：预览优先 → 路径 Cards/SummonCard_{cost}；比例用预制体手调值，不重算 ──
        if (frameSR != null)
        {
            Sprite frame = previewFrameSprite != null
                ? previewFrameSprite
                : LoadSprite("Cards/SummonCard_" + Mathf.Clamp(template.baseCost, 0, 5));
            if (frame != null) { frameSR.sprite = frame; frameSR.enabled = true; }
        }

        // ── 前缀背景：预览优先 → 路径 Cards/PrefixArtBG/{English}（按前缀切换，比例用预制体手调值）──
        if (prefixBgSR != null)
        {
            Sprite prefix = previewPrefixBgSprite != null
                ? previewPrefixBgSprite
                : LoadSprite("Cards/PrefixArtBG/" + PrefixEnglish(instance.prefixes));
            if (prefix != null) { prefixBgSR.sprite = prefix; prefixBgSR.enabled = true; }
        }

        // ── 卡图：预览优先 → cardSprite2D → 路径；无卡图 → 隐藏 CardArt，露出前缀背景兜底 ──
        if (cardArtSR != null)
        {
            Sprite art = previewArtSprite != null ? previewArtSprite : ResolveArtSprite(template);
            if (art != null) { cardArtSR.sprite = art; cardArtSR.gameObject.SetActive(true); }
            else { cardArtSR.gameObject.SetActive(false); }
        }
    }

    /// <summary>编辑期预览：拖入预览 Sprite 立即显示到对应层（对齐 2D 拖入字段；仅设属性不改层级）。
    /// 运行时 Refresh 会按"预览优先→路径"重新填 sprite，预制体里手调的比例不被覆盖。</summary>
    void OnValidate()
    {
        if (Application.isPlaying) return;
        bool changed = false;
        if (frameSR != null && previewFrameSprite != null) { frameSR.sprite = previewFrameSprite; frameSR.enabled = true; changed = true; }
        if (prefixBgSR != null && previewPrefixBgSprite != null) { prefixBgSR.sprite = previewPrefixBgSprite; prefixBgSR.enabled = true; changed = true; }
        if (cardArtSR != null && previewArtSprite != null) { cardArtSR.sprite = previewArtSprite; cardArtSR.gameObject.SetActive(true); changed = true; }
#if UNITY_EDITOR
        if (changed) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>卡面 Sprite：真实 cardSprite2D → {tid}_Front → 镜像 Cards/Summon 目录（含法术 Normal/Special）→ null（调用方隐藏 CardArt 露出前缀背景）。</summary>
    Sprite ResolveArtSprite(CardData template)
    {
        if (template != null && template.cardSprite2D != null && !IsLegacyPlaceholder(template.cardSprite2D))
            return template.cardSprite2D;
        if (template == null || string.IsNullOrEmpty(template.templateID))
            return null;

        string tid = template.templateID;
        Sprite s = LoadSprite("Cards/" + tid + "_Front");
        if (s != null) return s;

        if (template.cardType == CardType.Spell)
        {
            int scost = Mathf.Clamp(template.baseCost, 0, 5);
            s = LoadSprite("Cards/Spell/Normal/" + scost + "/SpellCard_{" + tid + "}")
             ?? LoadSprite("Cards/Spell/Special/" + scost + "/SpellCard_{" + tid + "}")
             ?? LoadSprite("Cards/Spell/Normal/" + scost + "/SpellCard_" + tid)
             ?? LoadSprite("Cards/Spell/Special/" + scost + "/SpellCard_" + tid);
        }
        else
        {
            string sub;
            switch (template.summonType)
            {
                case SummonType.Hero:      sub = "Hero/" + template.baseCost; break;
                case SummonType.ChosenOne: sub = "ChosenOne"; break;
                default:                   sub = "Special"; break;
            }
            s = LoadSprite("Cards/Summon/" + sub + "/SummonCard_{" + tid + "}")
             ?? LoadSprite("Cards/Summon/" + sub + "/SummonCard_" + tid);
        }
        return s;
    }

    /// <summary>卡面原画解析：真实 cardSprite2D → {tid}_Front → 镜像 Cards/Summon 目录（含法术 Normal/Special）→ 白。</summary>
    Texture2D ResolveArtTexture(CardData template)
    {
        if (template != null && template.cardSprite2D != null && !IsLegacyPlaceholder(template.cardSprite2D))
            return template.cardSprite2D.texture;
        if (template == null || string.IsNullOrEmpty(template.templateID))
            return Texture2D.whiteTexture;

        string tid = template.templateID;
        // 旧平铺命名 Cards/{tid}_Front
        Sprite s = LoadSprite("Cards/" + tid + "_Front");
        if (s != null) return s.texture;

        // 镜像目录（真实新卡面，兼容花括号/无花括号）
        if (template.cardType == CardType.Spell)
        {
            int scost = Mathf.Clamp(template.baseCost, 0, 5);
            s = LoadSprite("Cards/Spell/Normal/" + scost + "/SpellCard_{" + tid + "}")
             ?? LoadSprite("Cards/Spell/Special/" + scost + "/SpellCard_{" + tid + "}")
             ?? LoadSprite("Cards/Spell/Normal/" + scost + "/SpellCard_" + tid)
             ?? LoadSprite("Cards/Spell/Special/" + scost + "/SpellCard_" + tid);
        }
        else
        {
            string sub;
            switch (template.summonType)
            {
                case SummonType.Hero:      sub = "Hero/" + template.baseCost; break;
                case SummonType.ChosenOne: sub = "ChosenOne"; break;
                default:                   sub = "Special"; break;
            }
            s = LoadSprite("Cards/Summon/" + sub + "/SummonCard_{" + tid + "}")
             ?? LoadSprite("Cards/Summon/" + sub + "/SummonCard_" + tid);
        }
        return s != null ? s.texture : Texture2D.whiteTexture;
    }

    /// <summary>前缀 → 前缀底图文件名（Abyss/Blood/Mech/Psychic/Scroll/Common）。</summary>
    static string PrefixEnglish(string prefixes)
    {
        if (string.IsNullOrEmpty(prefixes) || prefixes == "无") return "Common";
        if (prefixes.Contains("渊"))       return "Abyss";
        if (prefixes.Contains("机械"))     return "Mech";
        if (prefixes.Contains("灵能"))     return "Psychic";
        if (prefixes.Contains("血歌"))     return "Blood";
        if (prefixes.Contains("神灵画卷")) return "Scroll";
        return "Common";
    }

    /// <summary>旧占位卡面（Card000_Front / CardSpell000_Front）——视为未分配真实卡面。</summary>
    static bool IsLegacyPlaceholder(Sprite s)
    {
        if (s == null) return false;
        return s.name == "Card000_Front" || s.name == "CardSpell000_Front";
    }

    /// <summary>从 Art/Sprites/ 相对路径加载 Sprite：先 Resources，编辑器回退 AssetDatabase。</summary>
    Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Sprite s = Resources.Load<Sprite>(path);
        if (s != null) return s;
#if UNITY_EDITOR
        string full = "Assets/_Game/Art/Sprites/" + path + ".png";
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(full);
        if (s != null) return s;
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(full.Replace(".png", ".jpg"));
#endif
        return s;
    }

    public void Refresh()
    {
        Card3DInstance c3d = GetComponent<Card3DInstance>();
        if (c3d == null || c3d.cardInstance == null) return;
        CardInstance instance = c3d.cardInstance;
        CardData template = CardDatabase.Instance?.GetTemplate(instance.templateID);

        // 贴图仅在首次 Refresh 时设置一次——纹理在整个生命周期内不变
        if (!_artInitialized)
        {
            ApplyArtFromCard(instance);
            _artInitialized = true;
        }

        if (nameText != null) nameText.text = template?.cardName ?? "";
        if (prefixText != null) prefixText.text = instance.prefixes;
        // 法术牌显示效果文本
        if (template.cardType == CardType.Spell && effectText != null)
        {
            effectText.text = template.effect ?? "";
        }

        // 法术牌隐藏攻击力和生命值
        if (template.cardType == CardType.Spell)
        {
            if (attackText != null) attackText.gameObject.SetActive(false);
            if (healthText != null) healthText.gameObject.SetActive(false);
        }
        if (costText != null) costText.gameObject.SetActive(false);

        if (attackText != null) attackText.text = instance.Attack.ToString();
        if (healthText != null)
        {

            healthText.text = instance.currentHealth.ToString();
        }
    }
    [System.Obsolete]
    private bool IsSuppressorOnField()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        BoardSlot[] slots = bm.GetAllSlots();
        if (slots == null) return false;
        for (int i = 6; i <= 11; i++)
        {
            if (slots[i] == null || slots[i].currentCard3D == null) continue;
            CardInstance ci = slots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "03501")
                return true;
        }
        return false;
    }
    /// <summary>
     /// 隐藏所有3D文字和信息（对方视角用）
     /// </summary>
    public void HideAllInfo()
    {
        if (nameText != null) nameText.gameObject.SetActive(false);
        if (attackText != null) attackText.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (costText != null) costText.gameObject.SetActive(false);
        if (prefixText != null) prefixText.gameObject.SetActive(false);
        if (effectText != null) effectText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示所有3D文字和信息（己方视角用）
    /// </summary>
    public void ShowAllInfo()
    {
        if (nameText != null) nameText.gameObject.SetActive(true);
        if (attackText != null) attackText.gameObject.SetActive(true);
        if (healthText != null) healthText.gameObject.SetActive(true);
        if (costText != null) costText.gameObject.SetActive(true);
        if (prefixText != null) prefixText.gameObject.SetActive(true);
        if (effectText != null) effectText.gameObject.SetActive(true);
    }
}