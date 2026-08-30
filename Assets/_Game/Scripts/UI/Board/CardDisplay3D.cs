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

    [Header("直接拖入 Sprite（优先于路径，对齐 2D 新卡 CardDisplay2DNew）")]
    [Tooltip("费用卡框 0-5（index=费用，运行时优先用数组对应费用）")]
    public Sprite[] costFrameSprites;
    [Tooltip("费用卡框预览（拖入即在预制体视图显示 CostFrameBase）")]
    public Sprite costFrameSprite;
    [Tooltip("卡面预览图（拖入即在预制体视图显示正面；运行时优先用它，否则走路径加载）")]
    public Sprite cardFaceSprite;
    [Tooltip("卡背图（拖入即在预制体视图显示背面；运行时优先用它，否则走路径加载）")]
    public Sprite cardBackSprite;
    [Tooltip("通用前缀底图（无前缀/其他前缀）")]
    public Sprite defaultPrefixArtSprite;
    [Tooltip("五前缀底图（index: 0=灵能,1=渊,2=机械,3=血歌,4=神灵画卷）")]
    public Sprite[] prefixArtSprites;
    [Tooltip("原画占位（找不到卡面时兜底）")]
    public Sprite cardArtFallbackSprite;

    [Header("卡面/卡背/前缀/卡框 SpriteRenderer（尺寸沿用预制体手调 localScale，不重算）")]
    [Tooltip("CostFrameBase 节点 SpriteRenderer（费用卡框层，最底层）")]
    public SpriteRenderer costFrameSR;
    [Tooltip("PrefixArtBG 节点 SpriteRenderer（前缀底图层，下层，尺寸用预制体手调 localScale）")]
    public SpriteRenderer prefixArtBGSR;
    [Tooltip("CardArt 节点 SpriteRenderer（正面原画层，上层）")]
    public SpriteRenderer cardArtSR;
    [Tooltip("CardBackImage 节点 SpriteRenderer（背面）")]
    public SpriteRenderer cardBackSR;

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
        var mr = GetComponent<MeshRenderer>();
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
    /// 拖入 Sprite 字段优先 → 路径加载 → CardArtConfig → 白（与 2D CardDisplay2DNew 一致）。
    /// </summary>
    public void ApplyArtFromCard(CardInstance instance)
    {
        if (instance == null) return;
        CardData template = CardDatabase.Instance?.GetTemplate(instance.templateID);
        if (template == null) return;

        // ① _BgTex = 前缀底图（对应 2D PrefixArtBG）：拖入 prefixArtSprites[idx]/defaultPrefixArtSprite → 路径 Cards/PrefixArtBG/{English} → CardArtConfig 前缀边框 → 白
        Sprite bgSpr = GetPrefixArtBGSprite(instance.prefixes);
        if (bgSpr == null) bgSpr = LoadSprite("Cards/PrefixArtBG/" + PrefixEnglish(instance.prefixes));
        Texture2D bg = bgSpr != null ? bgSpr.texture : null;
        if (bg == null && CardArtConfig.Instance != null)
            bg = CardArtConfig.Get2DBorder(instance)?.texture;

        // ② _BorderTex = 费用卡框（对应 2D CostFrameBase）：拖入 costFrameSprites[cost] → 路径 Cards/SummonCard_{cost} → CardArtConfig 底图 → 白
        int cost = Mathf.Clamp(template.baseCost, 0, 5);
        Sprite borderSpr = (costFrameSprites != null && cost < costFrameSprites.Length) ? costFrameSprites[cost] : null;
        if (borderSpr == null) borderSpr = LoadSprite("Cards/SummonCard_" + cost);
        Texture2D border = borderSpr != null ? borderSpr.texture : null;
        if (border == null && CardArtConfig.Instance != null)
            border = CardArtConfig.Get2DBackground(template, instance)?.texture;

        // ③ 卡面原画：cardSprite2D → {templateID}_Front → 镜像 Cards/Summon 目录 → cardArtFallbackSprite → 白
        Texture2D art = ResolveArtTexture(template);

        SetCompositeTextures(bg, border, art);

        // ④ 卡背：拖入 cardBackSprite 则用其纹理覆盖背面 _MainTex
        ApplyCardBack(cardBackSprite);

        // ⑤ 卡面/卡背/前缀/卡框 SpriteRenderer：字段优先，否则路径加载。
        //    尺寸直接沿用预制体里手调好的 localScale，不重算——生成结果与预制体所见一致。
        Sprite costSpr = (costFrameSprites != null && cost < costFrameSprites.Length) ? costFrameSprites[cost] : null;
        if (costSpr == null) costSpr = costFrameSprite; // 预览字段兜底
        if (costSpr == null) costSpr = LoadSprite("Cards/SummonCard_" + cost);
        FillCardFace(costFrameSR, costSpr);               // 费用卡框层（最底层）
        Sprite prefixSpr = GetPrefixArtBGSprite(instance.prefixes);
        if (prefixSpr == null) prefixSpr = LoadSprite("Cards/PrefixArtBG/" + PrefixEnglish(instance.prefixes));
        FillCardFace(prefixArtBGSR, prefixSpr);   // 前缀底图层（下层）
        ApplyPrefixArtBGSize(instance.prefixes, prefixArtBGSR); // 按前缀动态尺寸（读 2D 值换算）
        FillCardFace(cardArtSR, cardFaceSprite != null ? cardFaceSprite : ResolveArtSprite(template)); // 原画层（上层）
        FillCardFace(cardBackSR, cardBackSprite != null ? cardBackSprite : LoadSprite("Cards/Back"));   // 背面
    }

    /// <summary>只填 Sprite，尺寸沿用预制体手调的 localScale（不重算——所见即所得）。</summary>
    void FillCardFace(SpriteRenderer sr, Sprite s)
    {
        if (sr == null || s == null) return;
        sr.sprite = s;
        sr.enabled = true;
    }

    /// <summary>编辑器预览：拖入 cardFaceSprite/cardBackSprite 后立即在预制体视图的 CardArt/CardBackImage 上显示。</summary>
    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (costFrameSR != null && costFrameSprite != null)
        {
            costFrameSR.sprite = costFrameSprite;
            costFrameSR.enabled = true;
        }
        if (prefixArtBGSR != null && defaultPrefixArtSprite != null)
        {
            prefixArtBGSR.sprite = defaultPrefixArtSprite;
            prefixArtBGSR.enabled = true;
        }
        if (cardArtSR != null && cardFaceSprite != null)
        {
            cardArtSR.sprite = cardFaceSprite;
            cardArtSR.enabled = true;
        }
        if (cardBackSR != null && cardBackSprite != null)
        {
            cardBackSR.sprite = cardBackSprite;
            cardBackSR.enabled = true;
        }
    }

    /// <summary>卡面原画 Sprite（供 CardArt SpriteRenderer 铺满显示）：真实 cardSprite2D → {tid}_Front → 镜像目录 → 占位。</summary>
    Sprite ResolveArtSprite(CardData template)
    {
        if (template != null && template.cardSprite2D != null && !IsLegacyPlaceholder(template.cardSprite2D))
            return template.cardSprite2D;
        if (template == null || string.IsNullOrEmpty(template.templateID))
            return cardArtFallbackSprite;

        string tid = template.templateID;
        Sprite s = LoadSprite("Cards/" + tid + "_Front");
        if (s != null) return s;
        if (template.cardType == CardType.Spell)
        {
            int sc = Mathf.Clamp(template.baseCost, 0, 5);
            s = LoadSprite("Cards/Spell/Normal/" + sc + "/SpellCard_{" + tid + "}")
             ?? LoadSprite("Cards/Spell/Special/" + sc + "/SpellCard_{" + tid + "}")
             ?? LoadSprite("Cards/Spell/Normal/" + sc + "/SpellCard_" + tid)
             ?? LoadSprite("Cards/Spell/Special/" + sc + "/SpellCard_" + tid);
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
        return s != null ? s : cardArtFallbackSprite;
    }

    /// <summary>前缀 → 底图数组 index（0=灵能,1=渊,2=机械,3=血歌,4=神灵画卷）。无/其他 → -1。</summary>
    static int PrefixIndex(string prefixes)
    {
        if (string.IsNullOrEmpty(prefixes) || prefixes == "无") return -1;
        if (prefixes.Contains("灵能"))   return 0;
        if (prefixes.Contains("渊"))     return 1;
        if (prefixes.Contains("机械"))   return 2;
        if (prefixes.Contains("血歌"))   return 3;
        if (prefixes.Contains("神灵画卷")) return 4;
        return -1;
    }

    /// <summary>前缀底图：拖入 prefixArtSprites[index]（五前缀）或 defaultPrefixArtSprite（通用）优先。</summary>
    Sprite GetPrefixArtBGSprite(string prefixes)
    {
        int idx = PrefixIndex(prefixes);
        if (idx >= 0 && prefixArtSprites != null && idx < prefixArtSprites.Length)
            return prefixArtSprites[idx];
        return defaultPrefixArtSprite;
    }

    /// <summary>按前缀动态设置 PrefixArtBG 尺寸（完全读 2D 预制体的值：渊/灵能 66×88，其他/无前缀 64×84，
    /// 按 2D 卡 83.33×146.33 换算到 3D 卡 0.9×1.6）。不写死、不改比例。</summary>
    void ApplyPrefixArtBGSize(string prefixes, SpriteRenderer sr)
    {
        if (sr == null) return;
        bool big = !string.IsNullOrEmpty(prefixes) && (prefixes.Contains("渊") || prefixes.Contains("灵能"));
        float w = (big ? 66f : 64f) / 83.33f * 0.9f;   // 渊/灵能 66, 其他 64
        float h = (big ? 88f : 84f) / 146.33f * 1.6f;  // 渊/灵能 88, 其他 84
        sr.transform.localScale = new Vector3(w, h, 1f);
    }

    /// <summary>给全部 TMP 文字加黑描边——白字在浅色卡面/原画上也能看清（运行时设一次，避免编辑器实例化材质泄漏）。</summary>
    void EnsureTextOutline()
    {
        SetOutline(nameText);
        SetOutline(costText);
        SetOutline(attackText);
        SetOutline(healthText);
        SetOutline(prefixText);
    }

    static void SetOutline(TextMeshPro t)
    {
        if (t == null) return;
        t.outlineWidth = 0.08f;
        t.outlineColor = Color.black;
    }

    /// <summary>拖入的 cardBackSprite 覆盖背面材质 _MainTex（卡背用 CardCutout，MPB 设 _MainTex 只影响背槽）。</summary>
    void ApplyCardBack(Sprite backSprite)
    {
        if (backSprite == null) return;
        var mr = GetComponent<MeshRenderer>();
        if (mr == null || _mpb == null) return;
        mr.GetPropertyBlock(_mpb);
        _mpb.SetTexture("_MainTex", backSprite.texture);
        mr.SetPropertyBlock(_mpb);
    }

    /// <summary>卡面原画解析：真实 cardSprite2D → {tid}_Front → 镜像 Cards/Summon 目录（含法术 Normal/Special）→ 白。</summary>
    Texture2D ResolveArtTexture(CardData template)
    {
        if (template != null && template.cardSprite2D != null && !IsLegacyPlaceholder(template.cardSprite2D))
            return template.cardSprite2D.texture;
        if (template == null || string.IsNullOrEmpty(template.templateID))
            return FallbackArtTexture();

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
        return s != null ? s.texture : FallbackArtTexture();
    }

    /// <summary>卡面兜底：拖入 cardArtFallbackSprite 用其纹理，否则白（透明，露出底图/边框）。</summary>
    Texture2D FallbackArtTexture()
        => cardArtFallbackSprite != null ? cardArtFallbackSprite.texture : Texture2D.whiteTexture;

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
            EnsureTextOutline(); // 白字黑描边（编辑器设置会泄漏材质，运行时设一次）
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
        // 费用数字总是显示（新 3D 卡用 TMP 显示费用，不再烘焙进贴图——旧代码无条件隐藏导致费用文字不显示）
        if (costText != null)
        {
            costText.gameObject.SetActive(true);
            costText.text = instance.currentCost.ToString();
        }

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