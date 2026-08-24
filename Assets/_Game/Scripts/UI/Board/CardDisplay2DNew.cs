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
/// 加载：先 Resources.Load；编辑器下直接读 Art 资产；缺失 → 纯色占位，不报错。
/// 翻面：ShowFront()/ShowBack() 切换 FrontFace/BackFace。
/// </summary>
public class CardDisplay2DNew : MonoBehaviour
{
    [Header("正反面")]
    public GameObject frontFace;   // 正面（初始显示）
    public GameObject backFace;    // 反面（初始隐藏）

    [Header("正面元素（Image 组件引用，从预制体拖入）")]
    public Image costFrame;        // 费用底图
    public Image artwork;          // 卡面插画区
    public TMP_Text nameText;
    public Image costIcon;         // 能量图标
    public TMP_Text costText;
    public Image typeIcon;         // 类型图标
    public Image healthIcon;
    public TMP_Text healthText;
    public Image attackIcon;
    public TMP_Text attackText;
    public Image prefixIcon;       // 前缀图标
    public RectTransform traitIconsArea;  // 特性图标容器

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
    [Tooltip("通用卡面插画（为空则按 templateID 路径加载）")]
    public Sprite artworkSprite;
    [Tooltip("能量/攻击/生命图标")]
    public Sprite energyIconSprite;
    public Sprite attackIconSprite;
    public Sprite healthIconSprite;
    [Tooltip("类型图标：召唤/法术")]
    public Sprite typeSummonSprite;
    public Sprite typeSpellSprite;
    [Tooltip("前缀图标：渊/机械/灵能/血歌/神灵画卷")]
    public Sprite prefixAbyssSprite;
    public Sprite prefixMechSprite;
    public Sprite prefixPsychicSprite;
    public Sprite prefixBloodsongSprite;
    public Sprite prefixScrollSprite;
    [Tooltip("特性图标：先手/亡语/反击/主动退场/中毒/沉默/光环")]
    public Sprite traitFirstStrikeSprite;
    public Sprite traitDeathrattleSprite;
    public Sprite traitRevengeSprite;
    public Sprite traitActiveExitSprite;
    public Sprite traitPoisonSprite;
    public Sprite traitSilenceSprite;
    public Sprite traitAuraSprite;

    // ============================================================
    // 路径加载（相对 Art/Sprites/，见类注释）
    // ============================================================
    [Header("资源路径（相对 Art/Sprites/）")]
    [Tooltip("费用底图路径模板，{0}=费用(0-5)")]
    public string costFramePath = "Cards/SummonCard_{0}";
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
    [Tooltip("类型图标路径模板，{0}=summon/spell")]
    public string typeIconPath = "UI/type_{0}";
    [Tooltip("前缀图标路径模板，{0}=前缀(渊/机械/灵能/血歌/神灵画卷)")]
    public string prefixIconPath = "Icons/Prefixes/prefix_{0}";
    [Tooltip("特性图标根路径（key 追加在其后，如 trait_firststrike）")]
    public string traitIconPath = "Icons/Buffs/";

    [Header("特性图标")]
    [Tooltip("特性图标尺寸（TraitIconsArea 内动态创建的 Image 大小）")]
    public Vector2 traitIconSize = new Vector2(16f, 16f);

    CardInstance _inst;
    static Sprite _placeholder;

    // ================= 对外 API =================

    public void RefreshWithInstance(CardInstance inst)
    {
        _inst = inst;
        Refresh();
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

        // ── 卡面插画（直接 Sprite 或按 templateID 路径）──
        if (artwork != null)
        {
            artwork.sprite = PickSprite(artworkSprite, string.Format(artworkPath, _inst.templateID));
            artwork.enabled = true;
        }

        // ── 静态图标 ──
        if (costIcon != null) SetImageSprite(costIcon, energyIconSprite, energyIconPath);
        if (attackIcon != null) SetImageSprite(attackIcon, attackIconSprite, attackIconPath);
        if (healthIcon != null) SetImageSprite(healthIcon, healthIconSprite, healthIconPath);
        if (typeIcon != null)
            SetImageSprite(typeIcon, isSpell ? typeSpellSprite : typeSummonSprite,
                string.Format(typeIconPath, isSpell ? "spell" : "summon"));

        // ── 前缀图标 ──
        if (prefixIcon != null)
        {
            if (string.IsNullOrEmpty(_inst.prefixes) || _inst.prefixes == "无")
                prefixIcon.gameObject.SetActive(false);
            else
            {
                prefixIcon.gameObject.SetActive(true);
                SetImageSprite(prefixIcon, GetPrefixSprite(_inst.prefixes), string.Format(prefixIconPath, _inst.prefixes));
            }
        }

        // ── 特性图标 ──
        RefreshTraitIcons(_inst, template);

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
            default:         return null;
        }
    }

    // ================= 特性图标 =================

    void RefreshTraitIcons(CardInstance inst, CardData template)
    {
        if (traitIconsArea == null) return;
        for (int i = traitIconsArea.childCount - 1; i >= 0; i--)
            DestroyImmediate(traitIconsArea.GetChild(i).gameObject);

        if (inst.hasFirstStrike) AddTraitIcon("trait_firststrike", traitFirstStrikeSprite);
        if (inst.hasOnDeath)     AddTraitIcon("trait_deathrattle", traitDeathrattleSprite);
        if (inst.hasRevenge)     AddTraitIcon("trait_revenge", traitRevengeSprite);
        if (inst.hasActiveExit)  AddTraitIcon("trait_activeexit", traitActiveExitSprite);
        if (inst.poisoned)       AddTraitIcon("trait_poison", traitPoisonSprite);
        if (inst.silencedThisPhase) AddTraitIcon("trait_silence", traitSilenceSprite);
        if (HasTraitKeyword(inst, template, "光环")) AddTraitIcon("trait_aura", traitAuraSprite);
        // TODO: 扩展其他特性
    }

    static bool HasTraitKeyword(CardInstance inst, CardData template, string keyword)
    {
        if (template != null && !string.IsNullOrEmpty(template.traits) && template.traits.Contains(keyword))
            return true;
        if (inst.grantedTraitTexts != null)
            foreach (var t in inst.grantedTraitTexts)
                if (!string.IsNullOrEmpty(t) && t.Contains(keyword)) return true;
        return false;
    }

    void AddTraitIcon(string key, Sprite direct)
    {
        var go = new GameObject(key, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(traitIconsArea, false);
        go.GetComponent<RectTransform>().sizeDelta = traitIconSize;
        SetImageSprite(go.GetComponent<Image>(), direct, traitIconPath + key);
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
