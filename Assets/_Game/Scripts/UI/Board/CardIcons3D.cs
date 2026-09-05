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

    [Header("图标材质（与卡面一致写深度，防止被槽位 UI 嵌入遮挡；缺省回退卡面材质）")]
    public Material iconMaterial;

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
    public string energyIconPath = "UI/Cost"; // 费用图标实际文件是 UI/Cost.png（2D 的 UI/Energy 不存在）
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
    [Tooltip("三排图标中心间距（默认值；各排可单独覆盖）")]
    public float rowSpacing = 0.15f;
    [Tooltip("前缀排图标中心间距（0 则用 rowSpacing）")]
    public float prefixRowSpacing;
    [Tooltip("特性排图标中心间距（0 则用 rowSpacing）")]
    public float traitRowSpacing;
    [Tooltip("状态排图标中心间距（0 则用 rowSpacing）")]
    public float statusRowSpacing;

    float GetRowSpacing(float rowOverride) => rowOverride > 0f ? rowOverride : rowSpacing;

    [Header("三排预览图标（拖入即显示；运行时优先，未拖入走动态路径）")]
    [Tooltip("前缀排预览图标（任意数量，各自保持大小、固定间距，不拉伸/不占满）")]
    public Sprite[] previewPrefixIcons;
    [Tooltip("特性排预览图标（任意数量，各自保持大小、固定间距，不拉伸/不占满）")]
    public Sprite[] previewTraitIcons;
    [Tooltip("状态排预览图标（任意数量，各自保持大小、固定间距，不拉伸/不占满）")]
    public Sprite[] previewStatusIcons;

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

        // ── 三排图标（各自清除重建；预览数组优先，未拖入走动态路径加载；居中排列，间距可调）──
        PopulateRow(prefixIconsRow, previewPrefixIcons, BuildPrefixEntries(_inst), GetRowSpacing(prefixRowSpacing));
        // 6.x 特性图标置灰：被禁（完全沉默 BlockAll / 光环类禁）的 key 集合传入，命中即灰显而非隐藏
        var blockedTraitKeys = ComputeBlockedTraitKeys(_inst);
        PopulateRow(traitIconsRow,  previewTraitIcons,  BuildTraitEntries(_inst),  GetRowSpacing(traitRowSpacing), blockedTraitKeys);
        PopulateRow(statusIconsRow, previewStatusIcons, BuildStatusEntries(_inst), GetRowSpacing(statusRowSpacing));
    }

    // ================= 角标图标 =================

    void SetCornerIcon(SpriteRenderer sr, Sprite s, bool show)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(show);
        if (!show) return;
        sr.sprite = s != null ? s : GetPlaceholder();
        ApplyIconMaterial(sr);
        SetFixedSize(sr, cornerIconSize);
    }

    /// <summary>图标统一使用卡面写深度材质（CardFaceSprite：ZWrite On + Cull Off）。
    /// 写深度后正确遮挡后面的槽位/棋盘 UI（与卡面三层同理），防止图标被嵌入槽位预制体；
    /// 缺省回退 frameSR 的卡面材质。</summary>
    void ApplyIconMaterial(SpriteRenderer sr)
    {
        if (sr == null) return;
        if (iconMaterial == null)
        {
            var display = GetComponent<CardDisplay3D>();
            if (display != null && display.frameSR != null)
                iconMaterial = display.frameSR.sharedMaterial;
        }
        if (iconMaterial != null) sr.sharedMaterial = iconMaterial;
    }

    // ================= 三排图标 =================

    /// <summary>填充一排图标（对齐 2D HLG 关闭 Child Force Expand：不拉伸、不占满）：
    /// 预览数组优先 → 无则动态条目（直接 sprite → 路径）。
    /// 以中心为原点向两边居中，相邻图标中心间距 = spacing（可调）。</summary>
    void PopulateRow(Transform row, Sprite[] preview, List<(string key, Sprite direct, string path)> entries, float spacing,
        HashSet<string> grayKeys = null)
    {
        if (row == null) return;
        ClearChildren(row);
        int n = preview != null ? preview.Length : 0;
        if (n > 0)
        {
            int valid = CountNonNull(preview);
            if (valid == 0) return;
            float x = -(valid - 1) * spacing * 0.5f; // 居中起点：向两边展开
            for (int i = 0; i < n; i++)
            {
                if (preview[i] == null) continue;
                x = PlaceIcon(row, "icon_preview_" + i, preview[i], x, spacing, false);
            }
            return;
        }
        int en = entries.Count;
        if (en == 0) return;
        float dx = -(en - 1) * spacing * 0.5f; // 居中起点
        foreach (var e in entries)
        {
            Sprite s = e.direct != null ? e.direct : LoadSprite(e.path);
            bool gray = grayKeys != null && grayKeys.Contains(e.key); // 6.x 特性被禁 → 置灰
            dx = PlaceIcon(row, "icon_" + e.key, s != null ? s : GetPlaceholder(), dx, spacing, gray);
        }
    }

    /// <summary>统计非空 sprite 数量（用于居中按有效图标数计算）。</summary>
    static int CountNonNull(Sprite[] arr)
    {
        int c = 0;
        for (int i = 0; i < arr.Length; i++) if (arr[i] != null) c++;
        return c;
    }

    /// <summary>在 x 处放置一个图标（identity 朝向，卡根 Y180 后世界法线朝相机），返回下一个 x（间距 spacing）。
    /// blocked=true 置灰（6.x：特性被禁）。</summary>
    float PlaceIcon(Transform row, string name, Sprite s, float x, float spacing, bool blocked)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(row, false);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localPosition = new Vector3(x, 0, 0);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = s;
        ApplyIconMaterial(sr); // 写深度材质，防止被槽位 UI 嵌入遮挡
        sr.color = blocked ? TraitBanQuery.BlockedTint : Color.white; // 6.x 乘法着灰（白=原色）
        SetFixedSize(sr, rowIconSize); // 保持各自比例、统一边长，不拉伸
        return x + spacing;
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

#if UNITY_EDITOR
    // ================= 编辑期预览（对齐 2D PopulatePreviewRow） =================

    /// <summary>拖入/修改预览图标时立即重建三排预览。OnValidate 内禁止改层级，
    /// 用 delayCall 延迟到编辑器循环执行；DontSave 仅编辑期可见，不入预制体。</summary>
    void OnValidate()
    {
        if (Application.isPlaying) return;
        // 预制体资产上的组件不允许改层级，直接跳过（PrefabStage/场景实例由 delayCall 重建）
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
        SchedulePreviewRebuild();
    }

    bool _previewRebuildScheduled;

    void SchedulePreviewRebuild()
    {
        if (_previewRebuildScheduled) return;
        _previewRebuildScheduled = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            _previewRebuildScheduled = false;
            if (this == null || Application.isPlaying) return;
            PopulatePreviewRow(prefixIconsRow, previewPrefixIcons, GetRowSpacing(prefixRowSpacing));
            PopulatePreviewRow(traitIconsRow,  previewTraitIcons,  GetRowSpacing(traitRowSpacing));
            PopulatePreviewRow(statusIconsRow, previewStatusIcons, GetRowSpacing(statusRowSpacing));
        };
    }

    /// <summary>预览排（DontSave 不入预制体）：以中心为原点向两边居中，各自保持比例、间距可调。</summary>
    void PopulatePreviewRow(Transform row, Sprite[] preview, float spacing)
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--)
            DestroyImmediate(row.GetChild(i).gameObject);
        if (preview == null) return;
        int valid = CountNonNull(preview);
        if (valid == 0) return;
        float x = -(valid - 1) * spacing * 0.5f; // 居中起点
        foreach (Sprite s in preview)
        {
            if (s == null) continue;
            var go = new GameObject("icon_preview", typeof(SpriteRenderer));
            go.hideFlags = HideFlags.DontSave; // 仅编辑期预览，不序列化进预制体
            go.transform.SetParent(row, false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = new Vector3(x, 0, 0);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = s;
            ApplyIconMaterial(sr); // 编辑期预览与运行时一致，用写深度材质
            SetFixedSize(sr, rowIconSize);
            x += spacing;
        }
    }
#endif

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
        if (inst == null) return list;
        // 6.x 置灰保留：完全沉默不再整排隐藏——图标仍建，置灰交给 ComputeBlockedTraitKeys/PopulateRow
        if (inst.hasFirstStrike) list.Add(("firststrike", traitFirstStrikeSprite, traitIconPath + "trait_firststrike"));
        if (inst.hasOnEnter)     list.Add(("onenter",     traitOnEnterSprite,     traitIconPath + "trait_onenter"));
        if (inst.hasOnDeath)     list.Add(("deathrattle", traitDeathrattleSprite, traitIconPath + "trait_deathrattle"));
        if (inst.hasActiveExit)  list.Add(("activeexit",  traitActiveExitSprite,  traitIconPath + "trait_activeexit"));
        if (inst.hasRevenge)     list.Add(("revenge",     traitRevengeSprite,     traitIconPath + "trait_revenge"));
        if (inst.hasDiscard)     list.Add(("discard",     traitDiscardSprite,     traitIconPath + "trait_discard"));
        if (inst.canAttach)      list.Add(("attach",      traitAttachSprite,      traitIconPath + "trait_attach"));
        return list;
    }

    /// <summary>特性排被禁图标 key 集合（与 BuildTraitEntries 同源 7 bool）：类被禁（含完全沉默）→ 该 key 置灰。
    /// 与 2D CardDisplay2DNew 的类映射保持一致。</summary>
    static HashSet<string> ComputeBlockedTraitKeys(CardInstance inst)
    {
        var set = new HashSet<string>();
        if (inst == null) return set;
        void AddIfBlocked(bool present, string key, string cls)
        {
            if (present && TraitBanQuery.ClassBlocked(inst, cls)) set.Add(key);
        }
        AddIfBlocked(inst.hasFirstStrike, "firststrike", "先手");
        AddIfBlocked(inst.hasOnEnter,     "onenter",     "进场");
        AddIfBlocked(inst.hasOnDeath,     "deathrattle", "退场");
        AddIfBlocked(inst.hasActiveExit,  "activeexit",  "主动退场");
        AddIfBlocked(inst.hasRevenge,     "revenge",     "反击");
        AddIfBlocked(inst.hasDiscard,     "discard",     "抛置");
        AddIfBlocked(inst.canAttach,      "attach",      "附着");
        return set;
    }

    List<(string, Sprite, string)> BuildStatusEntries(CardInstance inst)
    {
        var list = new List<(string, Sprite, string)>();
        if (inst == null) return list;
        if (inst.hasShield) list.Add(("shield", statusShieldSprite, statusIconPath + "Shield"));
        if (IsBuffed(inst)) list.Add(("buff",   statusBuffSprite,   statusIconPath + "Buff"));
        // 减益图标：仅状态类（中毒/沉默/临时减攻/攻击压制）；特性禁制(HasActiveBlock)是规则失效不是状态，不显示
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
        string fullPath = "Assets/_Game/Resources/" + path + ".png";
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
