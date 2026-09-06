using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 新法术 2D 手牌卡显示（SpellCard00_New_2D 专用，独立脚本，不改旧卡/召唤物卡）。
///
/// 思路：继承旧 CardDisplay2D → 手牌法术旧刷新路径 GetComponent&lt;CardDisplay2D&gt;().Refresh/
/// RefreshWithInstance 直接命中，无需 CardDisplay2DCompat。
/// 卡面风格借鉴新召唤物 CardDisplay2DNew：FrontFace/BackFace 双容器、按费用的卡框、
/// 前缀底图(PrefixArtBG)+原画(CardArt)分层、能量图标+文本、独立卡背节点。
///
/// 剔除：攻击/生命/类别 UI 与文本、三排图标（前缀排/特性排/状态排）——预制体不建对应节点、字段不绑定。
/// 保留：能量UI+文本、卡框、卡背、原画、卡名、效果描述文本。
/// 纯显示；精灵来源：拖入 Sprite 优先 → Resources 路径兜底 → 占位/留白。
/// </summary>
public class CardDisplay2DSpell : CardDisplay2D
{
    [Header("正反面容器")]
    public GameObject frontFace;   // 正面（初始显示）
    public GameObject backFace;    // 反面（初始隐藏，含 CardBackImage）

    [Header("正面元素（Image，从预制体拖入）")]
    public Image costFrame;        // 费用卡框（按费用 0-5，baseCost 决定）
    public Image prefixArtBG;      // 前缀底图（读取模板前缀 CardData.prefix）
    public Image cardArt;          // 原画（cardSprite2D → 路径 Cards/Spell/...）
    public Image costIcon;         // 能量图标

    [Header("背面")]
    public Image cardBackImage;    // 卡背图

    [Header("直接拖入 Sprite（优先于路径）")]
    public Sprite[] costFrameSprites;      // 费用卡框 0-5（index=费用）
    public Sprite cardBackSprite;
    public Sprite energyIconSprite;
    [Tooltip("通用前缀底图（无前缀/其他前缀）")]
    public Sprite defaultPrefixArtSprite;
    [Tooltip("五前缀底图（0=灵能,1=渊,2=机械,3=血歌,4=神灵画卷）")]
    public Sprite[] prefixArtSprites;

    [Header("资源路径（相对 Assets/_Game/Resources/）")]
    [Tooltip("法术卡费用卡框（SpellCard_0..5；与召唤的 SummonCard 卡框不同）")]
    public string costFramePath = "Cards/Back And Front/Spell/SpellCard_{0}";
    public string cardBackPath = "Cards/Back";
    public string energyIconPath = "UI/Energy";
    public string prefixArtBGPath = "Icons/Prefixes/prefixbg_{0}";

    static Sprite _placeholder;

    // 卡框缓存：只由模板 baseCost 决定，换模板才重解
    string _frameTemplateID;
    int _frameIndex = -1;

    void Start()
    {
        // 兼容未走 RefreshWithInstance 的创建路径：兜底从组件取实例刷一次；
        // 显式切正面，避免残留背面/空状态导致只看到预设文字
        if (frontFace == null) frontFace = transform.Find("FrontFace")?.gameObject;
        if (backFace == null) backFace = transform.Find("BackFace")?.gameObject;
        ShowFront();
        if (instance == null) instance = GetComponent<CardInstance>();
        Refresh();
    }

    public override void Refresh()
    {
        if (instance == null) return;
        CardData template = FindTemplate(instance.templateID);
        if (template == null) return;

        // ── 文本：卡名 / 能量（GetDisplayCost 含减费光环显示折扣）/ 效果描述 ──
        if (nameText != null) nameText.text = template.cardName;
        if (costText != null) costText.text = instance.GetDisplayCost().ToString();
        if (effectText != null) effectText.text = template.effect ?? "";

        // ── 能量图标 ──
        if (costIcon != null) SetImageSprite(costIcon, energyIconSprite, energyIconPath);

        // ── 费用卡框：只由模板 baseCost（缓存，01524 无法术特判）──
        if (costFrame != null)
        {
            int c = ResolveCostFrameIndex(template);
            if (c != _frameIndex || template.templateID != _frameTemplateID)
            {
                _frameIndex = c;
                _frameTemplateID = template.templateID;
                Sprite direct = costFrameSprites != null && c < costFrameSprites.Length ? costFrameSprites[c] : null;
                costFrame.sprite = direct != null ? direct : LoadSprite(string.Format(costFramePath, c));
            }
            costFrame.enabled = true;
        }

        // ── 前缀底图（读取模板前缀；法术按需）──
        if (prefixArtBG != null)
        {
            prefixArtBG.sprite = GetPrefixArtBGSprite(template);
            prefixArtBG.enabled = true;
        }

        // ── 原画：cardSprite2D 优先 → 路径 Cards/Spell/{Normal|Special}/{cost}/SpellCard_{id} → 留白 ──
        if (cardArt != null)
        {
            Sprite art = GetSpellArt(template);
            if (art == null) cardArt.gameObject.SetActive(false); // 无原画：隐藏露出 prefixArtBG
            else { cardArt.gameObject.SetActive(true); cardArt.sprite = art; cardArt.enabled = true; }
        }
    }

    /// <summary>背面（隐藏/对手视角）：切 BackFace 容器 + 卡背图。</summary>
    public override void ShowBack(CardData template, string label = "反制牌")
    {
        if (frontFace != null) frontFace.SetActive(false);
        if (backFace != null) backFace.SetActive(true);
        if (cardBackImage != null)
        {
            Sprite back = cardBackSprite != null ? cardBackSprite : LoadSprite(cardBackPath);
            cardBackImage.sprite = back;
            cardBackImage.enabled = true;
        }
    }

    /// <summary>正面状态。</summary>
    public void ShowFront()
    {
        if (frontFace != null) frontFace.SetActive(true);
        if (backFace != null) backFace.SetActive(false);
    }

    // ================= 费用卡框 / 前缀底图 / 原画 =================

    /// <summary>费用档位（0-5）：法术按 baseCost（与模板一致，不随 currentCost）。</summary>
    int ResolveCostFrameIndex(CardData template)
    {
        if (template == null) return 0;
        return Mathf.Clamp(template.baseCost, 0, 5);
    }

    /// <summary>前缀底图：拖入数组 → 路径 → 通用底图 → 占位。</summary>
    Sprite GetPrefixArtBGSprite(CardData template)
    {
        string prefix = template != null ? template.prefix : "";
        int idx = PrefixToIndex(prefix);
        Sprite direct = null;
        string path = null;
        if (idx >= 0)
        {
            if (prefixArtSprites != null && idx < prefixArtSprites.Length) direct = prefixArtSprites[idx];
            path = string.Format(prefixArtBGPath, prefix);
        }
        else direct = defaultPrefixArtSprite;
        if (direct != null) return direct;
        Sprite s = !string.IsNullOrEmpty(path) ? LoadSprite(path) : null;
        if (s == null) s = GetPlaceholder();
        if (s == GetPlaceholder() && defaultPrefixArtSprite != null) s = defaultPrefixArtSprite;
        return s;
    }

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

    /// <summary>法术原画：路径 Spell/{Normal|Special}/{cost}/SpellCard_{id}（兼容花括号/无；cardSprite2D 字段已移除）。</summary>
    Sprite GetSpellArt(CardData template)
    {
        if (template == null || string.IsNullOrEmpty(template.templateID)) return null;

        string tid = template.templateID;
        int cost = Mathf.Clamp(template.baseCost, 0, 5);
        foreach (string sub in new[] { "Normal", "Special" })
        {
            string basePath = "Cards/Spell/" + sub + "/" + cost + "/SpellCard";
            Sprite s = LoadSprite(basePath + "_" + tid) ?? LoadSprite(basePath + "_{" + tid + "}");
            if (s != null && s != GetPlaceholder()) return s;
        }
        return null;
    }

    // ================= 资源加载 =================

    /// <summary>按 templateID 解析卡模板：CardDatabase → 资源级兜底（总览/编辑器等 DB 未加载场景用，
    /// 对齐 CardDisplay2DNew.FindTemplate）。</summary>
    CardData FindTemplate(string tid)
    {
        if (string.IsNullOrEmpty(tid)) return null;
        CardData t = CardDatabase.Instance != null ? CardDatabase.Instance.GetTemplate(tid) : null;
        if (t != null) return t;
        foreach (string folder in new[] { "CardData", "ChosenOneData" })
        {
            var all = Resources.LoadAll<CardData>(folder);
            foreach (var c in all)
                if (c != null && c.templateID == tid) return c;
        }
        return null;
    }

    void SetImageSprite(Image img, Sprite direct, string path)
    {
        if (img == null) return;
        img.sprite = direct != null ? direct : (LoadSprite(path) ?? GetPlaceholder());
        img.enabled = true;
    }

    Sprite LoadSprite(string artRelativePath)
    {
        if (string.IsNullOrEmpty(artRelativePath)) return null;
        Sprite s = Resources.Load<Sprite>(artRelativePath);
        if (s != null) return s;
#if UNITY_EDITOR
        string fullPath = "Assets/_Game/Resources/" + artRelativePath + ".png";
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
        if (s != null) return s;
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fullPath.Replace(".png", ".jpg"));
        if (s != null) return s;
#endif
        return null;
    }

    static Sprite GetPlaceholder()
    {
        if (_placeholder != null) return _placeholder;
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var cols = new Color[16 * 16];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color(0.8f, 0.8f, 0.8f, 1f);
        tex.SetPixels(cols);
        tex.Apply();
        _placeholder = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 100f);
        return _placeholder;
    }
}
