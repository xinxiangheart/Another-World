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

    [Header("拖入 Sprite（对齐 2D Card00_New_2D：数组按费用/前缀；运行时优先于路径，预览字段最优先）")]
    [Tooltip("费用卡框 0-5（6 张，index=费用，运行时按费用取）")]
    public Sprite[] costFrameSprites;
    [Tooltip("通用前缀底图（无前缀/其他前缀）")]
    public Sprite defaultPrefixArtSprite;
    [Tooltip("五前缀底图（index: 0=灵能,1=渊,2=机械,3=血歌,4=神灵画卷）")]
    public Sprite[] prefixArtSprites;
    [Tooltip("卡背图（拖入则覆盖网格背槽 _MainTex，MPB 不污染共享材质）")]
    public Sprite cardBackSprite;

    [Header("正反面（对应 2D FrontFace/BackFace）")]
    [Tooltip("正面容器（UIComponents：卡框/前缀底图/卡图/文字/图标/三排）。运行时为空则按名字找")]
    public GameObject frontFace;

    MaterialPropertyBlock _mpb;
    bool _artInitialized;
    string _lastAttackText;
    string _lastHealthText;

    void Awake()
    {
        // 使用 MaterialPropertyBlock 替代每卡独立 Material 实例
        // —— 避免 new Material() 的 GPU 端资源分配，所有卡共享同一材质
        _mpb = new MaterialPropertyBlock();
        // 默认正面：隐藏模型盒（己方卡放置不调 SetHidden(false)，在此确保；对手卡随后 SetHidden(true) 会翻回背面）
        ShowFront();
    }

    /// <summary>设置三层合成贴图（通过 MaterialPropertyBlock，避免每卡独立材质）。</summary>
    public void SetCompositeTextures(Texture2D bg, Texture2D border, Texture2D art)
    {
        // (true) 包含 inactive：ModelRoot 正面被隐藏时也能命中卡面网格（第0子物体），
        // 否则 GetComponentInChildren 会跳过 inactive 模型、误抓到第一个 TMP 文字渲染器，把它的 _MainTex 覆盖成白块！
        var mr = GetComponentInChildren<MeshRenderer>(true);
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

        // ── 卡框：预览 → costFrameSprites[费用] → 路径 Cards/SummonCard_{cost}；比例用预制体手调值，不重算。
        //    费用档只由模板决定（baseCost；01524 画卷之核模板0费但用5费框），与 currentCost 无关。──
        if (frameSR != null)
        {
            int cost = ResolveCostFrameIndex(template);
            Sprite frame = previewFrameSprite != null
                ? previewFrameSprite
                : (costFrameSprites != null && cost < costFrameSprites.Length ? costFrameSprites[cost] : null)
                  ?? LoadSprite("Cards/SummonCard_" + cost);
            if (frame != null) { frameSR.sprite = frame; frameSR.enabled = true; }
        }

        // ── 前缀背景：预览 → prefixArtSprites[前缀]/defaultPrefixArtSprite → 路径（按模板前缀，对齐 2D）──
        if (prefixBgSR != null)
        {
            Sprite prefix = previewPrefixBgSprite != null
                ? previewPrefixBgSprite
                : ResolvePrefixBgSprite(template.prefix);
            if (prefix != null) { prefixBgSR.sprite = prefix; prefixBgSR.enabled = true; }
        }

        // ── 卡图：预览优先 → cardSprite2D → 路径；无卡图 → 隐藏 CardArt，露出前缀背景兜底 ──
        if (cardArtSR != null)
        {
            Sprite art = previewArtSprite != null ? previewArtSprite : ResolveArtSprite(template);
            if (art != null) { cardArtSR.sprite = art; cardArtSR.gameObject.SetActive(true); }
            else { cardArtSR.gameObject.SetActive(false); }
        }

        // ── 卡背：拖入 cardBackSprite 则覆盖网格背槽 _MainTex（MPB，仅当前卡，不污染共享材质）──
        if (cardBackSprite != null)
        {
            // (true) 必须命中卡面网格而非 TMP 文字渲染器（见 SetCompositeTextures 注释）
            var mr = GetComponentInChildren<MeshRenderer>(true);
            if (mr != null && _mpb != null)
            {
                mr.GetPropertyBlock(_mpb);
                _mpb.SetTexture("_MainTex", cardBackSprite.texture);
                mr.SetPropertyBlock(_mpb);
            }
        }
    }

    /// <summary>卡框费用档位（0-5）：只由模板决定。baseCost 直接映射；
    /// 01524 画卷之核模板费用0但强制用5费卡框。结果与 currentCost 无关。</summary>
    int ResolveCostFrameIndex(CardData template)
    {
        if (template == null) return 0;
        if (template.templateID == "01524") return 5; // 画卷之核特判：0费 → 5费框
        return Mathf.Clamp(template.baseCost, 0, 5);
    }

    /// <summary>前缀底图：拖入 prefixArtSprites[idx]（五前缀）或 defaultPrefixArtSprite（通用）→ 路径 Cards/PrefixArtBG/{English}。对齐 2D GetPrefixArtBGSprite。</summary>
    Sprite ResolvePrefixBgSprite(string prefix)
    {
        int idx = PrefixToIndex(prefix);
        if (idx >= 0)
        {
            if (prefixArtSprites != null && idx < prefixArtSprites.Length && prefixArtSprites[idx] != null)
                return prefixArtSprites[idx];
            return LoadSprite("Cards/PrefixArtBG/" + PrefixEnglish(prefix)) ?? defaultPrefixArtSprite;
        }
        return defaultPrefixArtSprite != null ? defaultPrefixArtSprite : LoadSprite("Cards/PrefixArtBG/Common");
    }

    /// <summary>前缀 → 底图数组 index（0=灵能,1=渊,2=机械,3=血歌,4=神灵画卷）。未知/无 → -1。</summary>
    static int PrefixToIndex(string prefix)
    {
        switch (prefix)
        {
            case "灵能": return 0;
            case "渊": return 1;
            case "机械": return 2;
            case "血歌": return 3;
            case "神灵画卷": return 4;
            default: return -1;
        }
    }

    /// <summary>编辑期预览：拖入预览 Sprite 立即显示到对应层（对齐 2D 拖入字段；仅设属性不改层级）。
    /// 运行时 Refresh 会按"预览优先→路径"重新填 sprite，预制体里手调的比例不被覆盖。
    /// OnValidate 是编辑器回调（引 UnityEditor），玩家构建不编译 → 避免 CS0219（changed 只读于此）。</summary>
#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        bool changed = false;
        if (frameSR != null && previewFrameSprite != null) { frameSR.sprite = previewFrameSprite; frameSR.enabled = true; changed = true; }
        if (prefixBgSR != null && previewPrefixBgSprite != null) { prefixBgSR.sprite = previewPrefixBgSprite; prefixBgSR.enabled = true; changed = true; }
        if (cardArtSR != null && previewArtSprite != null) { cardArtSR.sprite = previewArtSprite; cardArtSR.gameObject.SetActive(true); changed = true; }
        if (changed) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    /// <summary>卡面 Sprite：真实 cardSprite2D → {tid}_Front → 镜像 Cards/Summon 目录（含法术 Normal/Special）→ null（调用方隐藏 CardArt 露出前缀背景）。</summary>
    Sprite ResolveArtSprite(CardData template)
    {
        // 卡面统一路径加载（cardSprite2D 字段已移除）
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
        // 卡面统一路径加载（cardSprite2D 字段已移除）
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
        string full = "Assets/_Game/Resources/" + path + ".png";
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(full);
        if (s != null) return s;
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(full.Replace(".png", ".jpg"));
#endif
        return s;
    }

    public virtual void Refresh()
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
        // 费用文字跟随正反面：ShowFront(UIComponents 激活)时显示，ShowBack 时随整组隐藏 —— 不在此强制隐藏
        if (costText != null) costText.text = instance.GetDisplayCost().ToString();

        // ── 攻击/生命数值变化 → 该 Text 弹跳（纯表现）。法术牌隐藏攻/血不弹；
        //    召唤期/首刷经 Card3DInstance.ElementBounceAllowed 抑制；无 FX(旧卡)跳过 ──
        bool bounceText = c3d != null && c3d.ElementBounceAllowed
            && (template == null || template.cardType != CardType.Spell);
        CardFaceBounceFX bounceFx = bounceText ? GetComponent<CardFaceBounceFX>() : null;
        if (attackText != null)
        {
            string atk = instance.Attack.ToString();
            bool atkChanged = _lastAttackText != null && _lastAttackText != atk;
            _lastAttackText = atk;
            attackText.text = atk;
            if (atkChanged && bounceFx != null)
            {
                bounceFx.EnsureElement("atk", attackText.transform);
                bounceFx.Bounce("atk", attackText.transform, true);
            }
        }
        if (healthText != null)
        {
            string hp = instance.currentHealth.ToString();
            bool hpChanged = _lastHealthText != null && _lastHealthText != hp;
            _lastHealthText = hp;
            healthText.text = hp;
            if (hpChanged && bounceFx != null)
            {
                bounceFx.EnsureElement("hp", healthText.transform);
                bounceFx.Bounce("hp", healthText.transform, true);
            }
        }

        // 召唤物文字动态变色（2D/3D 通用，只作用文本；法术不适用）。规则见 CardInstance.Get*Color()。
        if (template != null && template.cardType != CardType.Spell)
        {
            if (nameText != null) nameText.color = instance.GetNameColor();
            if (costText != null) costText.color = instance.GetCostColor();
            if (attackText != null) attackText.color = instance.GetAttackColor();
            if (healthText != null) healthText.color = instance.GetHealthColor();
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
    /// <summary>正面容器（UIComponents：卡框/前缀底图/卡图/文字/图标/三排）；未指定则按名字找。</summary>
    GameObject GetFrontFace()
    {
        if (frontFace != null) return frontFace;
        Transform t = transform.Find("UIComponents");
        if (t != null) frontFace = t.gameObject;
        return frontFace;
    }

    /// <summary>正面状态（默认）：显示正面全部组件（UIComponents），隐藏 CardModel 模型盒。不改变比例/位置。</summary>
    public void ShowFront()
    {
        GameObject f = GetFrontFace();
        if (f != null) f.SetActive(true);
        Transform m = GetCardModel();
        if (m != null) m.gameObject.SetActive(false); // 正面：隐藏模型盒，卡背随之完全不可见
    }

    /// <summary>背面状态：显示 CardModel 模型盒（SetHidden 已把整体模型翻转使卡背朝相机），隐藏正面全部组件。不改变比例/位置。</summary>
    public void ShowBack()
    {
        GameObject f = GetFrontFace();
        if (f != null) f.SetActive(false);
        Transform m = GetCardModel();
        if (m != null) m.gameObject.SetActive(true); // 背面：显示模型盒（卡背）
    }

    /// <summary>模型盒（ModelRoot，内含 fbx 网格：正面白底 + 背面/侧面卡背）。隐藏它=隐藏整个模型。
    /// 注意：fbx 实例名可能被嵌套预制体剥离，ModelRoot 是固定名且为 CardRoot 第0子物体。</summary>
    Transform GetCardModel()
    {
        Transform t = transform.Find("ModelRoot");
        if (t != null) return t;
        var mr = GetComponentInChildren<MeshRenderer>(true); // (true) 避免抓到 TMP 文字渲染器
        return mr != null ? mr.transform : null;
    }

    /// <summary>隐藏所有3D文字和信息（对方视角/附件用）：保持正面卡面，仅隐藏文字。</summary>
    public void HideAllInfo()
    {
        ShowFront(); // 附件/对手视角仍显示正面卡面，仅隐藏文字
        SetInfoVisible(false);
    }

    /// <summary>显示所有3D文字和信息（己方视角用）。</summary>
    public void ShowAllInfo()
    {
        ShowFront();
        SetInfoVisible(true);
    }

    void SetInfoVisible(bool v)
    {
        if (nameText != null) nameText.gameObject.SetActive(v);
        if (attackText != null) attackText.gameObject.SetActive(v);
        if (healthText != null) healthText.gameObject.SetActive(v);
        if (costText != null) costText.gameObject.SetActive(v);
        if (prefixText != null) prefixText.gameObject.SetActive(v);
        if (effectText != null) effectText.gameObject.SetActive(v);
    }
}