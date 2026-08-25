using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 新 2D 手牌卡牌刷新脚本（独立于旧 CardDisplay2D，不修改旧卡）。
/// 精灵来源两种：① 直接拖 Sprite 到下方字段（优先）；② 按 Art 分支路径加载。
/// 路径分支（Assets/_Game/Art/Sprites/）：
///   - 费用底图   Cards/SummonCard_{0}（0-5费）
///   - 卡面插画   Cards/{templateID}_Front
///   - 卡背       Cards/Back
///   - 能量/攻击/生命 UI/Energy、UI/Attack、UI/Health
///   - 前缀图标   Icons/Prefixes/prefix_{前缀}
///   - 特性图标   Icons/Buffs/trait_*
///   - 状态图标   Icons/Buffs/status_*
/// 加载：先 Resources.Load；编辑器下直接读 Art 资产；缺失 → 纯色占位，不报错。
/// 翻面：ShowFront()/ShowBack() 切换 FrontFace/BackFace。
/// 三排图标（各自独立动态生成/移除/重排）：
///   - PrefixIconsArea 前缀排：prefixes 为空格分隔的多前缀串（如"渊 机械"），逐前缀生成图标
///   - TraitIconsArea  特性排：先手/进场/亡语/主动退场/反击/抛置/附着/攻击前排/攻击后排/光环；完全沉默时整体隐藏
///   - StatusIconsArea 状态排：中毒/护盾/沉默/增益/减益
/// 右键"刷新预览"：读取本物体 CardInstance.templateID 初始化并刷新显示。
/// </summary>
public class CardDisplay2DNew : MonoBehaviour
{
    [Header("正反面")]
    public GameObject frontFace;   // 正面（初始显示）
    public GameObject backFace;    // 反面（初始隐藏）

    [Header("正面元素（Image 组件引用，从预制体拖入）")]
    public Image costFrame;        // 费用底图
    public Image prefixArtBG;      // 前缀底图（读取模板前缀 CardData.prefix，非实例）
    public Image cardArt;          // 召唤物原画（按 templateID 加载）
    public TMP_Text nameText;
    public Image costIcon;         // 能量图标
    public TMP_Text costText;
    public Image typeIcon;         // 类型图标
    public Image healthIcon;
    public TMP_Text healthText;
    public Image attackIcon;
    public TMP_Text attackText;

    [Header("三排图标容器（HorizontalLayoutGroup，运行时动态填充）")]
    public RectTransform prefixIconsArea;  // 前缀排
    public RectTransform traitIconsArea;   // 特性排
    public RectTransform statusIconsArea;  // 状态排

    [Header("背面")]
    public Image cardBackImage;    // 卡背图

    // ============================================================
    // 直接拖入的 Sprite（优先使用；为空则走下面的路径加载）
    // ============================================================
    [Header("直接指定 Sprite（拖入即用，优先于路径）")]
    [Tooltip("费用底图 0-5（6 张，index=费用）")]
    public Sprite[] costFrameSprites;
    [Tooltip("卡背图")]
    public Sprite cardBackSprite;

    [Header("前缀底图（ArtworkArea 下层）")]
    [Tooltip("通用底图（无前缀/其他前缀）")]
    public Sprite defaultPrefixArtSprite;
    [Tooltip("五前缀底图（index: 0=灵能,1=渊,2=机械,3=血歌,4=神灵画卷）")]
    public Sprite[] prefixArtSprites;

    [Header("静态图标")]
    [Tooltip("能量图标")]
    public Sprite energyIconSprite;
    [Tooltip("攻击力图标")]
    public Sprite attackIconSprite;
    [Tooltip("生命值图标")]
    public Sprite healthIconSprite;
    [Tooltip("原画占位图（可选，没有则纯灰）")]
    public Sprite cardArtFallbackSprite;

    [Header("类型图标（召唤物类别）")]
    [Tooltip("英雄")]
    public Sprite heroTypeSprite;
    [Tooltip("神选者")]
    public Sprite chosenOneTypeSprite;
    [Tooltip("特殊")]
    public Sprite specialTypeSprite;

    [Header("前缀图标")]
    [Tooltip("灵能")]
    public Sprite prefixPsychicSprite;
    [Tooltip("渊")]
    public Sprite prefixAbyssSprite;
    [Tooltip("机械")]
    public Sprite prefixMechSprite;
    [Tooltip("血歌")]
    public Sprite prefixBloodsongSprite;
    [Tooltip("神灵画卷")]
    public Sprite prefixScrollSprite;

    [Header("特性图标")]
    [Tooltip("先手")]
    public Sprite traitFirstStrikeSprite;
    [Tooltip("进场")]
    public Sprite traitOnEnterSprite;
    [Tooltip("反击")]
    public Sprite traitRevengeSprite;
    [Tooltip("退场（亡语）")]
    public Sprite traitDeathrattleSprite;
    [Tooltip("主动退场")]
    public Sprite traitActiveExitSprite;
    [Tooltip("抛置")]
    public Sprite traitDiscardSprite;
    [Tooltip("附着")]
    public Sprite traitAttachSprite;

    [Header("状态图标")]
    [Tooltip("护盾")]
    public Sprite statusShieldSprite;
    [Tooltip("正向增益")]
    public Sprite statusBuffSprite;
    [Tooltip("负面减益（中毒/沉默/其他减益统一一个图标）")]
    public Sprite statusDebuffSprite;

    // ============================================================
    // 路径加载（相对 Art/Sprites/，见类注释）
    // ============================================================
    [Header("资源路径（相对 Art/Sprites/）")]
    [Tooltip("费用底图路径模板，{0}=费用(0-5)")]
    public string costFramePath = "Cards/SummonCard_{0}";
    [Tooltip("前缀底图路径模板，{0}=前缀名")]
    public string prefixArtBGPath = "Icons/Prefixes/prefixbg_{0}";
    [Tooltip("卡面插画路径模板，{0}=templateID")]
    public string artworkPath = "Cards/{0}_Front";
    [Tooltip("卡背图路径")]
    public string cardBackPath = "Cards/Back";
    [Tooltip("能量图标路径")]
    public string energyIconPath = "UI/Energy";
    [Tooltip("攻击图标路径")]
    public string attackIconPath = "UI/Attack";
    [Tooltip("生命图标路径")]
    public string healthIconPath = "UI/Health";
    [Tooltip("前缀图标路径模板，{0}=前缀(渊/机械/灵能/血歌/神灵画卷)")]
    public string prefixIconPath = "Icons/Prefixes/prefix_{0}";
    [Tooltip("特性图标根路径（key 追加在其后，如 trait_firststrike）")]
    public string traitIconPath = "Icons/Buffs/";
    [Tooltip("状态图标根路径（key 追加在其后，如 status_poison）")]
    public string statusIconPath = "Icons/Buffs/";

    [Header("三排图标尺寸")]
    public Vector2 prefixIconSize = new Vector2(16f, 16f);
    public Vector2 traitIconSize = new Vector2(16f, 16f);
    public Vector2 statusIconSize = new Vector2(16f, 16f);

    CardInstance _inst;
    static Sprite _placeholder;

    // ================= 对外 API =================

    public void RefreshWithInstance(CardInstance inst)
    {
        _inst = inst;
        Refresh();
    }

    /// <summary>构建编号特性显示文本（固有 + 获得的赋予特性，统一编号），供特性文本元素使用。</summary>
    public string BuildTraitDisplayText()
    {
        if (_inst == null) return "";
        var entries = _inst.GetVisibleTraitEntries();
        if (entries.Count == 0) return "";
        var lines = new List<string>();
        for (int i = 0; i < entries.Count; i++)
            lines.Add(CardInstance.FormatTraitEntry(i + 1, entries[i]));
        return string.Join("\n", lines);
    }

    /// <summary>右键"刷新预览"：编辑器下按 templateID 从模板初始化并刷新显示。</summary>
    [ContextMenu("刷新预览")]
    public void RefreshPreview()
    {
        if (_inst == null) _inst = GetComponent<CardInstance>();
        if (_inst != null && !string.IsNullOrEmpty(_inst.templateID) && string.IsNullOrEmpty(_inst.instanceID))
        {
            CardData t = FindTemplate(_inst.templateID);
            if (t != null) _inst.InitFromTemplate(t, 0);
        }
        Refresh();
    }

    CardData FindTemplate(string tid)
    {
        CardData t = CardDatabase.Instance != null ? CardDatabase.Instance.GetTemplate(tid) : null;
        if (t != null) return t;
        var all = Resources.LoadAll<CardData>("CardData");
        foreach (var c in all) if (c.templateID == tid) return c;
        return null;
    }

    public void Refresh()
    {
        if (_inst == null) return;
        CardData template = CardDatabase.Instance?.GetTemplate(_inst.templateID);
        bool isSpell = template != null && template.cardType == CardType.Spell;

        // ── 数字文字 ──
        if (costText != null) costText.text = _inst.currentCost.ToString();
        if (attackText != null) attackText.text = _inst.Attack.ToString();
        if (healthText != null) healthText.text = _inst.currentHealth.ToString();
        if (nameText != null) nameText.text = template != null ? template.cardName : "";

        // ── 费用底图（0-5 费，直接 Sprite 或路径）──
        if (costFrame != null)
        {
            int c = Mathf.Clamp(_inst.currentCost, 0, 5);
            Sprite direct = costFrameSprites != null && c < costFrameSprites.Length ? costFrameSprites[c] : null;
            costFrame.sprite = PickSprite(direct, string.Format(costFramePath, c));
            costFrame.enabled = true;
        }

        // ── 前缀底图（读取模板前缀，非实例前缀；后续赋予的前缀不影响）──
        if (prefixArtBG != null)
        {
            string tplPrefix = template != null ? template.prefix : "";
            prefixArtBG.sprite = GetPrefixArtBGSprite(tplPrefix);
            prefixArtBG.enabled = true;
        }

        // ── 召唤物原画（按 templateID 加载；缺失用占位图或纯灰）──
        if (cardArt != null)
        {
            Sprite art = LoadSprite(string.Format(artworkPath, _inst.templateID));
            if (art == GetPlaceholder() && cardArtFallbackSprite != null)
                art = cardArtFallbackSprite;
            cardArt.sprite = art;
            cardArt.enabled = true;
        }

        // ── 静态图标 ──
        if (costIcon != null) SetImageSprite(costIcon, energyIconSprite, energyIconPath);
        if (attackIcon != null) SetImageSprite(attackIcon, attackIconSprite, attackIconPath);
        if (healthIcon != null) SetImageSprite(healthIcon, healthIconSprite, healthIconPath);
        // ── 类型图标：按召唤物类别（法术不显示）──
        if (typeIcon != null)
        {
            if (isSpell)
            {
                typeIcon.gameObject.SetActive(false);
            }
            else
            {
                typeIcon.gameObject.SetActive(true);
                Sprite s = null;
                switch (_inst.summonType)
                {
                    case SummonType.Hero:      s = heroTypeSprite; break;
                    case SummonType.ChosenOne: s = chosenOneTypeSprite; break;
                    case SummonType.Special:   s = specialTypeSprite; break;
                }
                typeIcon.sprite = s != null ? s : GetPlaceholder();
                typeIcon.enabled = true;
            }
        }

        // ── 三排图标（各自独立清除/生成）──
        RefreshPrefixIcons(_inst);
        RefreshTraitIcons(_inst, template);
        RefreshStatusIcons(_inst);

        // ── 法术隐藏攻击/生命 ──
        bool showCombat = !isSpell;
        if (attackText != null) attackText.gameObject.SetActive(showCombat);
        if (healthText != null) healthText.gameObject.SetActive(showCombat);
        if (attackIcon != null) attackIcon.gameObject.SetActive(showCombat);
        if (healthIcon != null) healthIcon.gameObject.SetActive(showCombat);
    }

    // ================= 翻面 =================

    public void ShowFront()
    {
        if (frontFace != null) frontFace.SetActive(true);
        if (backFace != null) backFace.SetActive(false);
    }

    public void ShowBack()
    {
        if (frontFace != null) frontFace.SetActive(false);
        if (backFace != null) backFace.SetActive(true);
        if (cardBackImage != null)
        {
            cardBackImage.sprite = PickSprite(cardBackSprite, cardBackPath);
            cardBackImage.enabled = true;
        }
    }

    // ================= 前缀排 =================

    /// <summary>前缀串为空格分隔（如"渊 机械"、"无"、"血歌"），逐前缀生成图标，去重跳过"无"。</summary>
    void RefreshPrefixIcons(CardInstance inst)
    {
        if (prefixIconsArea == null) return;
        ClearChildren(prefixIconsArea);
        if (inst == null || string.IsNullOrEmpty(inst.prefixes)) return;

        var parts = inst.prefixes.Split(' ');
        var seen = new HashSet<string>();
        foreach (var p in parts)
        {
            string prefix = p.Trim();
            if (string.IsNullOrEmpty(prefix) || prefix == "无") continue;
            if (!seen.Add(prefix)) continue;
            AddRowIcon(prefixIconsArea, "prefix_" + prefix, GetPrefixSprite(prefix),
                string.Format(prefixIconPath, prefix), prefixIconSize);
        }
    }

    // ================= 特性排 =================

    void RefreshTraitIcons(CardInstance inst, CardData template)
    {
        if (traitIconsArea == null) return;
        ClearChildren(traitIconsArea);
        if (inst == null) return;

        // 完全沉默 → 特性全部失效，统一隐藏（沉默图标由状态排显示）
        if (IsFullySilenced(inst)) return;

        if (inst.hasFirstStrike)  AddRowIcon(traitIconsArea, "trait_firststrike", traitFirstStrikeSprite, traitIconPath + "trait_firststrike", traitIconSize);
        if (inst.hasOnEnter)      AddRowIcon(traitIconsArea, "trait_onenter", traitOnEnterSprite, traitIconPath + "trait_onenter", traitIconSize);
        if (inst.hasOnDeath)      AddRowIcon(traitIconsArea, "trait_deathrattle", traitDeathrattleSprite, traitIconPath + "trait_deathrattle", traitIconSize);
        if (inst.hasActiveExit)   AddRowIcon(traitIconsArea, "trait_activeexit", traitActiveExitSprite, traitIconPath + "trait_activeexit", traitIconSize);
        if (inst.hasRevenge)      AddRowIcon(traitIconsArea, "trait_revenge", traitRevengeSprite, traitIconPath + "trait_revenge", traitIconSize);
        if (inst.hasDiscard)      AddRowIcon(traitIconsArea, "trait_discard", traitDiscardSprite, traitIconPath + "trait_discard", traitIconSize);
        if (inst.canAttach)       AddRowIcon(traitIconsArea, "trait_attach", traitAttachSprite, traitIconPath + "trait_attach", traitIconSize);
        // 攻击前后排是目标选择逻辑（非图标）、光环是状态（非特性）——均不显示特性图标
    }

    // ================= 状态排 =================

    /// <summary>中毒/护盾/沉默/增益/减益（按此顺序显示）。</summary>
    void RefreshStatusIcons(CardInstance inst)
    {
        if (statusIconsArea == null) return;
        ClearChildren(statusIconsArea);
        if (inst == null) return;

        if (inst.hasShield)    AddRowIcon(statusIconsArea, "status_shield", statusShieldSprite, statusIconPath + "status_shield", statusIconSize);
        if (IsBuffed(inst))    AddRowIcon(statusIconsArea, "status_buff", statusBuffSprite, statusIconPath + "status_buff", statusIconSize);
        // 中毒/沉默/其他减益 → 统一一个负面减益图标
        if (inst.poisoned || IsFullySilenced(inst) || IsDebuffed(inst))
            AddRowIcon(statusIconsArea, "status_debuff", statusDebuffSprite, statusIconPath + "status_debuff", statusIconSize);
    }

    /// <summary>增益：贤者/皇帝 buff，或临时攻击/生命为正。</summary>
    static bool IsBuffed(CardInstance inst) =>
        inst.buffedBySage || inst.buffedByEmperor || inst.tempAttackBoost > 0 || inst.tempHealthBoost > 0;

    /// <summary>减益：临时攻击/生命为负，或处于攻击力压制（originalAttackBeforeDebuff 已记录原攻击）。</summary>
    static bool IsDebuffed(CardInstance inst) =>
        inst.tempAttackBoost < 0 || inst.tempHealthBoost < 0 || inst.originalAttackBeforeDebuff > 0;

    /// <summary>完全沉默：本阶段被沉默，或被场上沉默光环压制（GlobalEventManager 兜底）。</summary>
    static bool IsFullySilenced(CardInstance inst)
    {
        if (inst == null) return false;
        if (inst.silencedThisPhase) return true;
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(inst);
    }

    // ================= 前缀底图（ArtworkArea 下层） =================

    /// <summary>按【模板前缀】取前缀底图：拖入数组 → 路径 → 通用底图 → 占位。</summary>
    Sprite GetPrefixArtBGSprite(string prefix)
    {
        int idx = PrefixToIndex(prefix);
        Sprite direct = null;
        string path = null;
        if (idx >= 0)
        {
            // 已知前缀 → 数组底图；未拖入走路径
            if (prefixArtSprites != null && idx < prefixArtSprites.Length)
                direct = prefixArtSprites[idx];
            path = string.Format(prefixArtBGPath, prefix);
        }
        else
        {
            // 无/其他前缀 → 通用底图
            direct = defaultPrefixArtSprite;
        }
        if (direct != null) return direct;
        Sprite s = !string.IsNullOrEmpty(path) ? LoadSprite(path) : null;
        if (s == null) s = GetPlaceholder();
        if (s == GetPlaceholder() && defaultPrefixArtSprite != null) s = defaultPrefixArtSprite;
        return s;
    }

    /// <summary>前缀 → 底图数组 index（0=灵能,1=渊,2=机械,3=血歌,4=神灵画卷）。未知/无 → -1。</summary>
    int PrefixToIndex(string prefix)
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

    // ================= 前缀图标映射 =================

    Sprite GetPrefixSprite(string prefix)
    {
        switch (prefix)
        {
            case "渊":       return prefixAbyssSprite;
            case "机械":     return prefixMechSprite;
            case "灵能":     return prefixPsychicSprite;
            case "血歌":     return prefixBloodsongSprite;
            case "神灵画卷": return prefixScrollSprite;
            default:         return null; // 群峦/潮汐 等未定义前缀 → 占位图
        }
    }

    // ================= 三排通用（动态图标） =================

    /// <summary>清空一排容器下的所有子图标。</summary>
    static void ClearChildren(RectTransform area)
    {
        for (int i = area.childCount - 1; i >= 0; i--)
            DestroyImmediate(area.GetChild(i).gameObject);
    }

    /// <summary>在指定排容器内创建一个图标（直接 Sprite → 路径 → 占位）。</summary>
    void AddRowIcon(RectTransform area, string key, Sprite direct, string path, Vector2 size)
    {
        var go = new GameObject(key, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(area, false);
        go.GetComponent<RectTransform>().sizeDelta = size;
        SetImageSprite(go.GetComponent<Image>(), direct, path);
    }

    // ================= 资源加载（直接 Sprite 优先 → 路径 → 占位） =================

    /// <summary>直接 Sprite 优先，否则路径加载（含占位兜底）。</summary>
    Sprite PickSprite(Sprite direct, string path)
    {
        if (direct != null) return direct;
        return LoadSprite(path);
    }

    void SetImageSprite(Image img, Sprite direct, string path)
    {
        if (img == null) return;
        img.sprite = PickSprite(direct, path);
        img.enabled = true;
    }

    Sprite LoadSprite(string artRelativePath)
    {
        Sprite s = Resources.Load<Sprite>(artRelativePath);
        if (s != null) return s;
#if UNITY_EDITOR
        string fullPath = "Assets/_Game/Art/Sprites/" + artRelativePath + ".png";
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
        if (s != null) return s;
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fullPath.Replace(".png", ".jpg"));
        if (s != null) return s;
#endif
        return GetPlaceholder();
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
