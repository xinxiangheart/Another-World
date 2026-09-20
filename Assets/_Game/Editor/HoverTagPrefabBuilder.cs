using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// HoverTagPrefabBuilder — 一键生成 3D 召唤物悬停标签预制体 TagLabel.prefab。
// 结构（与需求一致）：
//   TagLabel                        ← 根（RectTransform + HoverTagLabel + ContentSizeFitter）
//   └─ BG                           ← Image（纯色描边层，动态大小）
//       ├─ Fill                     ← Image（深藏青半透明填充，四边内缩 2 露出描边）
//       └─ Text                     ← TextMeshProUGUI（自动换行、跟随文字）
// BG 撑满根；Text 内缩 tagPadding；根尺寸由 HoverTagLabel.SetText 按文字测量驱动。
// 外观对齐 Welcome 场景警告弹窗（UpdateNotice/SteamNotice）的底色：金棕描边 #B9905A + 深藏青 #0E141F(α0.90)。
// 这两处（本预制体与 DetailPanel 背景）共用同一套配色，改动请同步。
// 菜单：Tools → 卡牌 → 生成悬停标签预制体
// ============================================================================
public static class HoverTagPrefabBuilder
{
    const string FontPath = "Assets/_Game/Fonts/NotoSerifCJKsc-Black SDF.asset";
    // TagLabel 与其它 UI 预制体同目录（Prefabs/UI/Panels）；运行时不走 Resources.Load，
    // 由 Resources/Config/HoverTagConfig.asset 持引用（见下）。
    const string PrefabPath = "Assets/_Game/Prefabs/UI/Panels/TagLabel.prefab";
    const string ConfigPath = "Assets/_Game/Resources/Config/HoverTagConfig.asset";

    // 根默认尺寸（SetText 会覆盖），先给非零避免首帧 0
    static readonly Vector2 DefaultSize = new Vector2(120f, 40f);

    [MenuItem("Tools/卡牌/生成悬停标签预制体")]
    public static void CreatePrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"[HoverTag] 找不到字体: {FontPath}"); return; }

        // ── 根（中心锚点，运行时以 anchoredPosition 定位到 HoverTagLayer）──
        GameObject root = new GameObject("TagLabel", typeof(RectTransform));
        RectTransform rootRT = (RectTransform)root.transform;
        rootRT.anchorMin = rootRT.anchorMax = rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.sizeDelta = DefaultSize;

        // ── BG（描边层）─ 子物体、锚点拉伸铺满根 ──
        GameObject bgGo = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform bgRT = (RectTransform)bgGo.transform;
        bgRT.SetParent(rootRT, false);
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;  bgRT.offsetMax = Vector2.zero;
        bgRT.pivot = new Vector2(0.5f, 0.5f);

        // 外观对齐 Welcome 场景警告弹窗底色：外层纯色描边 + 内层深藏青半透明填充，两层都不用 sprite。
        Image bg = bgGo.GetComponent<Image>();
        bg.sprite = null;
        bg.type = Image.Type.Simple;
        bg.color = new Color(0.7254902f, 0.5647059f, 0.3529412f, 1f);
        bg.raycastTarget = false;

        // ── Fill（填充层）─ 四边内缩 2，露出 2px 描边；排在 Text 之前，文字仍在其上 ──
        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRT = (RectTransform)fillGo.transform;
        fillRT.SetParent(bgRT, false);
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(2f, 2f);  fillRT.offsetMax = new Vector2(-2f, -2f);
        fillRT.pivot = new Vector2(0.5f, 0.5f);

        Image fill = fillGo.GetComponent<Image>();
        fill.sprite = null;
        fill.type = Image.Type.Simple;
        fill.color = new Color(0.05490196f, 0.078431375f, 0.1254902f, 0.9019608f);
        fill.raycastTarget = false;

        // ── Text（TMP）─ 子物体、锚点拉伸、四周留 tagPadding 由 HoverTagLabel 运行时调 ──
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRT = (RectTransform)textGo.transform;
        textRT.SetParent(bgRT, false);
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 6f);
        textRT.offsetMax = new Vector2(-10f, -6f);
        textRT.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontMaterial = font != null ? font.material : null;
        tmp.fontSize = 26f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.text = "标签";
        tmp.raycastTarget = false;

        // ── 组件接线 ──
        root.AddComponent<ContentSizeFitter>(); // 布局由 HoverTagLabel 手动驱动，运行时会禁用
        HoverTagLabel label = root.AddComponent<HoverTagLabel>();
        label.bgImage = bg;
        label.labelText = tmp;
        label.tagMaxWidth = 260f;
        label.tagPadding = new Vector2(10f, 6f);

        // ── 保存预制体到 Prefabs/UI/Panels ──
        string dir = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(dir);
            if (AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(parent, folder);
            else
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs/UI", folder);
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        // ── 生成/更新 Config 资产：持预制体引用（运行时 Resources.Load<HoverTagConfig>）──
        WriteConfig();

        AssetDatabase.SaveAssets();
        Debug.Log($"[HoverTag] 预制体已生成: {PrefabPath}；Config: {ConfigPath}");
    }

    /// <summary>创建或更新 Resources/Config/HoverTagConfig.asset，tagLabelPrefab 指向新预制体。</summary>
    static void WriteConfig()
    {
        string dir = System.IO.Path.GetDirectoryName(ConfigPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Game/Resources", "Config");

        var cfg = AssetDatabase.LoadAssetAtPath<HoverTagConfig>(ConfigPath);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<HoverTagConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError($"[HoverTag] 生成失败，找不到预制体: {PrefabPath}"); return; }
        cfg.tagLabelPrefab = prefab;
        EditorUtility.SetDirty(cfg);
    }
}
