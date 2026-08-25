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

    // ================= 工具 =================

    /// <summary>创建一排水平排列的图标容器（HorizontalLayoutGroup，紧凑左对齐）。</summary>
    static RectTransform CreateIconRow(RectTransform parent, string name)
    {
        RectTransform rt = CreateChild(parent, name, typeof(HorizontalLayoutGroup));
        var hlg = rt.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
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
