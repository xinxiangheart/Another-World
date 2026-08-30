using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D 卡牌图标显示：角标图标（费用/类型/攻/血）+ 三排图标（前缀/特性/状态）。
/// 镜像 CardDisplay2DNew 的图标逻辑，用 SpriteRenderer 在 3D 卡面上显示。
///
/// 朝向约定：卡根节点运行时为 Quaternion.Euler(0,180,0)，本脚本所有图标 localRotation 用
/// identity（世界法线 -Z 朝相机）——SpriteRenderer 单面渲染，必须正面朝相机才可见；
/// 文字也同用 identity（区别于旧卡的 localY180，那个在双面 TMP 下是镜像显示）。
///
/// 刷新：由 Card3DInstance.UpdateValues() 调用 Refresh()（与 CardDisplay3D 同一触发点）。
/// </summary>
public class CardIcons3D : MonoBehaviour
{
    [Header("角标图标 (SpriteRenderer)")]
    public SpriteRenderer costIcon;
    public SpriteRenderer typeIcon;
    public SpriteRenderer healthIcon;
    public SpriteRenderer attackIcon;

    [Header("三排容器 (Transform，运行时动态生成子 SpriteRenderer)")]
    public Transform prefixIconsRow;
    public Transform traitIconsRow;
    public Transform statusIconsRow;

    [Header("直接拖入 Sprite（优先于路径）")]
    public Sprite energyIconSprite;
    public Sprite attackIconSprite;
    public Sprite healthIconSprite;
    public Sprite heroTypeSprite;
    public Sprite chosenOneTypeSprite;
    public Sprite specialTypeSprite;
    public Sprite prefixAbyssSprite, prefixMechSprite, prefixPsychicSprite, prefixBloodsongSprite, prefixScrollSprite;
    public Sprite traitFirstStrikeSprite, traitOnEnterSprite, traitRevengeSprite, traitDeathrattleSprite, traitActiveExitSprite, traitDiscardSprite, traitAttachSprite;
    public Sprite statusShieldSprite, statusBuffSprite, statusDebuffSprite;

    [Header("路径回退（相对 Art/Sprites/，文件实际名）")]
    public string energyIconPath = "UI/Energy";
    public string attackIconPath = "UI/Attack";
    public string healthIconPath = "UI/Health";
    public string prefixIconPath = "Icons/Prefixes/";
    public string traitIconPath = "Icons/Buffs/";
    public string statusIconPath = "Icons/Buffs/";

    [Header("尺寸（世界单位）")]
    [Tooltip("角标图标边长（费用/类型/攻/血）")]
    public float cornerIconSize = 0.16f;
    [Tooltip("三排单图标边长")]
    public float rowIconSize = 0.12f;
    [Tooltip("三排图标水平间距")]
    public float rowSpacing = 0.15f;

    CardInstance _inst;
    static Sprite _placeholder;

    public void RefreshWithInstance(CardInstance inst) { _inst = inst; Refresh(); }

    public void Refresh()
    {
        if (_inst == null) _inst = GetComponent<Card3DInstance>()?.cardInstance;
        if (_inst == null) return;
        CardData template = CardDatabase.Instance?.GetTemplate(_inst.templateID);
        bool isSpell = template != null && template.cardType == CardType.Spell;

        // ── 角标图标（费用恒显示；类型/攻/血法术隐藏）──
        SetCornerIcon(costIcon,   PickSprite(energyIconSprite, energyIconPath), true);
        SetCornerIcon(typeIcon,   GetTypeSprite(_inst.summonType), !isSpell);
        SetCornerIcon(healthIcon, PickSprite(healthIconSprite, healthIconPath), !isSpell);
        SetCornerIcon(attackIcon, PickSprite(attackIconSprite, attackIconPath), !isSpell);

        // ── 三排图标（各自清除重建）──
        PopulateRow(prefixIconsRow, BuildPrefixEntries(_inst));
        PopulateRow(traitIconsRow,  BuildTraitEntries(_inst));
        PopulateRow(statusIconsRow, BuildStatusEntries(_inst));
    }

    // ================= 角标图标 =================

    void SetCornerIcon(SpriteRenderer sr, Sprite s, bool show)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(show);
        if (!show) return;
        sr.sprite = s != null ? s : GetPlaceholder();
        SetFixedSize(sr, cornerIconSize);
    }

    // ================= 三排图标 =================

    void PopulateRow(Transform row, List<(string key, Sprite direct, string path)> entries)
    {
        if (row == null) return;
        ClearChildren(row);
        float x = 0f;
        foreach (var e in entries)
        {
            var go = new GameObject("icon_" + e.key, typeof(SpriteRenderer));
            go.transform.SetParent(row, false);
            go.transform.localRotation = Quaternion.identity; // 卡根 Y180 → 世界法线朝相机
            go.transform.localPosition = new Vector3(x, 0, 0);
            var sr = go.GetComponent<SpriteRenderer>();
            Sprite s = e.direct != null ? e.direct : LoadSprite(e.path);
            sr.sprite = s != null ? s : GetPlaceholder();
            SetFixedSize(sr, rowIconSize);
            x += rowSpacing;
        }
    }

    /// <summary>按 Sprite 原始宽高比缩放到固定世界尺寸（用 bounds.x 缩放，保持比例）。</summary>
    static void SetFixedSize(SpriteRenderer sr, float worldSize)
    {
        if (sr == null || sr.sprite == null) return;
        float s = worldSize / Mathf.Max(0.001f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    // ================= 条目构建（镜像 CardDisplay2DNew） =================

    List<(string, Sprite, string)> BuildPrefixEntries(CardInstance inst)
    {
        var list = new List<(string, Sprite, string)>();
        if (inst == null || string.IsNullOrEmpty(inst.prefixes)) return list;
        var seen = new HashSet<string>();
        foreach (var p in inst.prefixes.Split(' '))
        {
            string prefix = p.Trim();
            if (string.IsNullOrEmpty(prefix) || prefix == "无" || !seen.Add(prefix)) continue;
            string en = GetPrefixEnglish(prefix);
            list.Add((prefix, GetPrefixSprite(prefix), en != null ? prefixIconPath + en : null));
        }
        return list;
    }

    List<(string, Sprite, string)> BuildTraitEntries(CardInstance inst)
    {
        var list = new List<(string, Sprite, string)>();
        if (inst == null || IsFullySilenced(inst)) return list;
        if (inst.hasFirstStrike) list.Add(("firststrike", traitFirstStrikeSprite, traitIconPath + "trait_firststrike"));
        if (inst.hasOnEnter)     list.Add(("onenter",     traitOnEnterSprite,     traitIconPath + "trait_onenter"));
        if (inst.hasOnDeath)     list.Add(("deathrattle", traitDeathrattleSprite, traitIconPath + "trait_deathrattle"));
        if (inst.hasActiveExit)  list.Add(("activeexit",  traitActiveExitSprite,  traitIconPath + "trait_activeexit"));
        if (inst.hasRevenge)     list.Add(("revenge",     traitRevengeSprite,     traitIconPath + "trait_revenge"));
        if (inst.hasDiscard)     list.Add(("discard",     traitDiscardSprite,     traitIconPath + "trait_discard"));
        if (inst.canAttach)      list.Add(("attach",      traitAttachSprite,      traitIconPath + "trait_attach"));
        return list;
    }

    List<(string, Sprite, string)> BuildStatusEntries(CardInstance inst)
    {
        var list = new List<(string, Sprite, string)>();
        if (inst == null) return list;
        if (inst.hasShield) list.Add(("shield", statusShieldSprite, statusIconPath + "Shield"));
        if (IsBuffed(inst)) list.Add(("buff",   statusBuffSprite,   statusIconPath + "Buff"));
        if (inst.poisoned || IsFullySilenced(inst) || IsDebuffed(inst))
            list.Add(("debuff", statusDebuffSprite, statusIconPath + "DeBuff"));
        return list;
    }

    // ================= 映射与状态 =================

    Sprite GetTypeSprite(SummonType t)
    {
        switch (t)
        {
            case SummonType.Hero:      return heroTypeSprite;
            case SummonType.ChosenOne: return chosenOneTypeSprite;
            default:                   return specialTypeSprite;
        }
    }

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

    /// <summary>前缀 → 图标文件名（Abyss/Blood/Mech/Psychic/Scroll）。未知 → null（走占位）。</summary>
    static string GetPrefixEnglish(string prefix)
    {
        switch (prefix)
        {
            case "渊":       return "Abyss";
            case "机械":     return "Mech";
            case "灵能":     return "Psychic";
            case "血歌":     return "Blood";
            case "神灵画卷": return "Scroll";
            default:         return null;
        }
    }

    static bool IsBuffed(CardInstance inst) =>
        inst.buffedBySage || inst.buffedByEmperor || inst.tempAttackBoost > 0 || inst.tempHealthBoost > 0;

    static bool IsDebuffed(CardInstance inst) =>
        inst.tempAttackBoost < 0 || inst.tempHealthBoost < 0 || inst.originalAttackBeforeDebuff > 0;

    static bool IsFullySilenced(CardInstance inst)
    {
        if (inst == null) return false;
        if (inst.silencedThisPhase) return true;
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(inst);
    }

    // ================= 资源加载 =================

    Sprite PickSprite(Sprite direct, string path)
    {
        if (direct != null) return direct;
        return LoadSprite(path);
    }

    Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Sprite s = Resources.Load<Sprite>(path);
        if (s != null) return s;
#if UNITY_EDITOR
        string fullPath = "Assets/_Game/Art/Sprites/" + path + ".png";
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
