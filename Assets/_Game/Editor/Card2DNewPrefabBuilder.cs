using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 一键生成新 2D 手牌卡牌预制体（独立于旧卡牌，不修改任何旧预制体/旧脚本）。
/// 结构：Card00_New_2D → FrontFace(正) / BackFace(反)。
/// 正面：CostFrameBase / ArtworkArea / 文字 / 图标 / 三排图标容器（PrefixIconsArea·TraitIconsArea·StatusIconsArea）。
/// 菜单：Tools → 卡牌 → 生成新2D手牌预制体
/// 生成后位置在 Scene 中手动摆，代码不写死坐标。
/// </summary>
public static class Card2DNewPrefabBuilder
{
    const string FontPath = "Assets/_Game/Fonts/NotoSerifCJKsc-Black SDF.asset";
    const string PrefabPath = "Assets/_Game/Prefabs/Cards/Summon/Card00_New_2D.prefab";
    const string SpellPrefabPath = "Assets/_Game/Prefabs/Cards/Spell/SpellCard00_New_2D.prefab";

    [MenuItem("Tools/卡牌/生成新2D手牌预制体")]
    public static void CreatePrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[Card2DNew] 找不到字体: {FontPath}");
            return;
        }

        // ── 根 ──
        GameObject root = new GameObject("Card00_New_2D", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(CanvasGroup), typeof(CardInstance), typeof(CardView),
            typeof(CardDrag), typeof(CardHover), typeof(CardDisplay2DNew));
        root.layer = 5;
        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(83.33f, 146.33f); // 参考旧卡尺寸，可自行调整

        // ── 正面容器 ──
        RectTransform front = CreateChild(root.transform, "FrontFace", null);
        StretchToCard(front);
        front.gameObject.SetActive(true);

        // ── 反面容器（初始隐藏）──
        RectTransform back = CreateChild(root.transform, "BackFace", null);
        StretchToCard(back);
        back.gameObject.SetActive(false);

        // ── 正面元素（Image 用 null sprite，运行时由 CardDisplay2DNew 填充/占位）──
        Image costFrame   = CreateImage(front, "CostFrameBase");
        // ArtworkArea 为容器：下层 PrefixArtBG（前缀底图）+ 上层 CardArt（原画）
        RectTransform artwork = CreateChild(front, "ArtworkArea", null);
        Image prefixArtBG = CreateImage(artwork, "PrefixArtBG");
        Image cardArt = CreateImage(artwork, "CardArt");
        TMP_Text nameText = CreateText(front, "NameText", "卡名", font);
        Image costIcon    = CreateImage(front, "CostIcon");
        TMP_Text costText = CreateText(front, "CostText", "0", font);
        Image typeIcon    = CreateImage(front, "TypeIcon");
        Image healthIcon  = CreateImage(front, "HealthIcon");
        TMP_Text healthText = CreateText(front, "HealthText", "0", font);
        Image attackIcon  = CreateImage(front, "AttackIcon");
        TMP_Text attackText = CreateText(front, "AttackText", "0", font);

        // 三排图标容器（水平排列，运行时动态添加子图标）
        RectTransform prefixArea = CreateIconRow(front, "PrefixIconsArea");
        RectTransform traitArea  = CreateIconRow(front, "TraitIconsArea");
        RectTransform statusArea = CreateIconRow(front, "StatusIconsArea");

        // ── 背面元素 ──
        Image cardBack = CreateImage(back, "CardBackImage");

        // ── 接线 CardDisplay2DNew ──
        var display = root.GetComponent<CardDisplay2DNew>();
        display.frontFace = front.gameObject;
        display.backFace = back.gameObject;
        display.costFrame = costFrame;
        display.prefixArtBG = prefixArtBG;
        display.cardArt = cardArt;
        display.cardNameText = nameText as TextMeshProUGUI;
        display.costIcon = costIcon;
        display.cardCostText = costText as TextMeshProUGUI;
        display.typeIcon = typeIcon;
        display.healthIcon = healthIcon;
        display.cardHealthText = healthText as TextMeshProUGUI;
        display.attackIcon = attackIcon;
        display.cardAttackText = attackText as TextMeshProUGUI;
        display.prefixIconsArea = prefixArea;
        display.traitIconsArea = traitArea;
        display.statusIconsArea = statusArea;
        display.cardBackImage = cardBack;

        // ── 保存预制体 ──
        string dir = System.IO.Path.GetDirectoryName(PrefabPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, folder);
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Card2DNew] 预制体已生成: {PrefabPath}（三排图标位置请在场景中手动摆）");
    }

    /// <summary>
    /// 从召唤物 Card00_New_2D 克隆生成独立法术手牌预制体 SpellCard00_New_2D（不改原预制体）。
    /// 去掉：三栏图标（前缀/特性/状态排）、攻击UI+文本、生命UI+文本、类别UI。
    /// 保留：能量UI+文本、卡名、卡框(CostFrameBase)、原画区(PrefixArtBG+CardArt)、卡背、正反面结构。
    /// 显示脚本：移除 CardDisplay2DNew(+Compat)，改绑法术专用 CardDisplay2DSpell（继承 CardDisplay2D）。
    /// 新增：EffectText（TMP，卡面中央偏下）。布局随克隆保留，生成后可在场景微调 EffectText。
    /// </summary>
    [MenuItem("Tools/卡牌/生成新2D法术手牌预制体（克隆召唤物）")]
    public static void CreateSpellPrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"[Card2DNew] 找不到字体: {FontPath}"); return; }
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (src == null) { Debug.LogError($"[Card2DNew] 找不到源召唤物预制体: {PrefabPath}"); return; }

        GameObject clone = (GameObject)PrefabUtility.InstantiatePrefab(src);
        clone.name = "SpellCard00_New_2D";

        // 去掉：三栏图标 / 攻击 / 生命 / 类别（保留 能量、卡名、卡框、原画区、卡背）
        string[] remove = { "PrefixIconsArea", "TraitIconsArea", "StatusIconsArea",
                            "AttackIcon", "AttackText", "HealthIcon", "HealthText", "TypeIcon" };
        foreach (string n in remove)
        {
            Transform t = clone.transform.Find("FrontFace/" + n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        // 移除召唤物显示组件，改绑法术专用显示脚本 CardDisplay2DSpell
        // （继承 CardDisplay2D → 旧 GetComponent<CardDisplay2D>().Refresh 路径直接命中，无需 Compat）
        var displayNew = clone.GetComponent<CardDisplay2DNew>();
        if (displayNew != null) Object.DestroyImmediate(displayNew);
        var compat = clone.GetComponent<CardDisplay2DCompat>();
        if (compat != null) Object.DestroyImmediate(compat);

        var display = clone.AddComponent<CardDisplay2DSpell>();
        Transform front = clone.transform.Find("FrontFace");
        Transform back = clone.transform.Find("BackFace");
        display.frontFace = front != null ? front.gameObject : null;
        display.backFace = back != null ? back.gameObject : null;
        display.cardBackImage = back != null ? back.Find("CardBackImage")?.GetComponent<Image>() : null;
        // 攻击/生命/前缀文本不绑定（法术卡无对应节点）
        display.prefixText = null;
        display.attackText = null;
        display.healthText = null;
        if (front != null)
        {
            display.nameText = front.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            display.costText = front.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            display.costIcon = front.Find("CostIcon")?.GetComponent<Image>();
            display.costFrame = front.Find("CostFrameBase")?.GetComponent<Image>();
            display.prefixArtBG = front.Find("ArtworkArea/PrefixArtBG")?.GetComponent<Image>();
            display.cardArt = front.Find("ArtworkArea/CardArt")?.GetComponent<Image>();

            // 新增：效果描述文本（卡面中央偏下；生成后可在场景微调位置/字号）
            TMP_Text eff = CreateText(front as RectTransform, "EffectText", "", font);
            eff.fontSize = 9f;
            eff.alignment = TextAlignmentOptions.Center;
            eff.enableWordWrapping = true;
            RectTransform effRT = eff.rectTransform;
            effRT.sizeDelta = new Vector2(78f, 42f);
            effRT.anchoredPosition = new Vector2(0f, -20f); // 中央偏下
            display.effectText = (TextMeshProUGUI)eff;
        }

        // 存为新预制体（SaveAsPrefabAsset 自动生成独立 guid/.meta，不改原召唤物预制体）
        string dir = System.IO.Path.GetDirectoryName(SpellPrefabPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, folder);
        }
        PrefabUtility.SaveAsPrefabAsset(clone, SpellPrefabPath);
        Object.DestroyImmediate(clone);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Card2DNew] 法术手牌预制体已生成: {SpellPrefabPath}（EffectText 位置请在场景中手动微调）");
    }

    // ================= 工具 =================

    /// <summary>创建一排水平排列的图标容器（HorizontalLayoutGroup，紧凑左对齐）。</summary>
    static RectTransform CreateIconRow(RectTransform parent, string name)
    {
        RectTransform rt = CreateChild(parent, name, typeof(HorizontalLayoutGroup));
        var hlg = rt.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        // 关闭 Child Control/Force Expand：图标用自身 sizeDelta，不拉伸不铺满
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        rt.sizeDelta = new Vector2(120f, 16f);
        return rt;
    }

    static RectTransform CreateChild(Transform parent, string name, System.Type extra)
    {
        var types = new System.Collections.Generic.List<System.Type> { typeof(RectTransform) };
        if (extra != null) types.Add(extra);
        var go = new GameObject(name, types.ToArray());
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image CreateImage(RectTransform parent, string name)
    {
        RectTransform rt = CreateChild(parent, name, typeof(CanvasRenderer), typeof(Image));
        rt.sizeDelta = new Vector2(20f, 20f); // 默认图标/区域尺寸，可自行调整
        return rt.GetComponent<Image>();
    }

    static RectTransform CreateChild(Transform parent, string name, System.Type a, System.Type b)
    {
        var go = new GameObject(name, typeof(RectTransform), a, b);
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static TMP_Text CreateText(RectTransform parent, string name, string content, TMP_FontAsset font)
    {
        RectTransform rt = CreateChild(parent, name, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var text = rt.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = font;
        text.fontSize = 10f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        rt.sizeDelta = new Vector2(40f, 20f);
        return text;
    }

    /// <summary>把子容器拉伸铺满父卡片（FrontFace/BackFace 用）。</summary>
    static void StretchToCard(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
