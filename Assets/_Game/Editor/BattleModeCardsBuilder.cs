using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// 在大厅「战斗子全屏弹窗」（Panel_Battle）里生成入场用的两张模式卡。
/// </summary>
/// <remarks>2026-09-27：用户「出一张类似于卡片的匹配图和排位图……进入战斗场景时会从最右边滑入到
/// 最左边，匹配在最左边，排位在其次，二者之间以及和边框之间有一定距离，位置在屏幕中心线上，大小较大」，
/// 随后澄清「这个是要做在战斗子弹窗里的」—— 所以卡挂在 Panel_Battle 下，**点「战斗」打开子弹窗时播滑入**。
///
/// 结构：
///   Panel_Battle（Lobby 场景现有，LobbySubPanel 的子全屏弹窗）
///     ├─ Bg / Text_Title / Text_Hint / Body_Content / Btn_Close   ← 原有的
///     └─ ModeCards                        ← 挂在 Bg 之后（标题 / 提示 / 关闭叉仍压在卡之上）
///          ├─ Card_Match   （RawImage = BattleModeCard_Match.png）└─ Text_Title「匹配」
///          └─ Card_Ranked  （RawImage = BattleModeCard_Ranked.png）└─ Text_Title「排位」
///
/// 播放时机靠 BattleModeCards.playOnEnable —— Panel_Battle 存成 active、由 LobbySubPanel.closeOnStart
/// 在 Start 里关掉，之后每次 Open 都是一次 SetActive(false)->(true)，正好触发 OnEnable。
///
/// 停位不在场景里烤死：由 BattleModeCards 按 gap / anchorX / edgeMargin / offsetX / offsetY 现算，
/// 所以改版式只要调组件上的数，卡片改宽高也不用重新摆。
/// 贴图出图脚本：Tools/cardframe/BattleModeV1.ps1。
/// </remarks>
public static class BattleModeCardsBuilder
{
    const string RootName  = "ModeCards";
    const string PanelName = "Panel_Battle";
    const string GenDir    = "Assets/_Game/Art/Sprites/Generated/battle-mode-v1/";
    const float  CardW     = 300f;      // 屏幕 px（贴图 900 = 3x）
    const float  CardH     = 420f;
    const float  SplitY    = 0.345f;    // 标题槽 / 徽记区 的分界（占卡高）—— 与出图脚本 $SPLIT_Y 同值
    const float  TitlePx   = 40f;
    const float  Gap       = 60f;

    static readonly Color Cream = new Color32(240, 232, 210, 236);   // 同大厅文字色 #F0E8D2

    static readonly string[] Files  = { "BattleModeCard_Match.png", "BattleModeCard_Ranked.png" };
    static readonly string[] Labels = { "匹配", "排位" };

    static TMP_FontAsset _font;

    [MenuItem("Tools/异界/大厅：战斗子弹窗里生成模式卡（匹配/排位）")]
    public static void Build()
    {
        var panel = FindPanelBattle();
        if (panel == null)
        {
            Debug.LogError("[ModeCards] 当前场景找不到 Panel_Battle（LobbySubPanel）—— 请先打开 Lobby.unity");
            return;
        }
        _font = FindChineseFont();

        // 幂等：已有就整棵重建
        Transform old = panel.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(panel.transform, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // 排在 Bg 之后：标题 / 提示 / 关闭叉仍压在卡之上
        Transform bg = panel.transform.Find("Bg");
        if (bg != null) root.transform.SetSiblingIndex(bg.GetSiblingIndex() + 1);
        else root.transform.SetAsFirstSibling();

        var comp = root.AddComponent<BattleModeCards>();
        comp.gap = Gap;
        comp.anchorX = 0f;        // 最终位置贴左边框（用户 2026-09-27）
        comp.edgeMargin = 120f;   // 与左边框留出的那一段间隔
        comp.delay = 0.06f;       // 迅速滑入（用户 2026-09-27：更快 + 更有「动态」感）
        comp.duration = 0.42f;
        comp.stagger = 0.07f;
        comp.scaleFrom = 0.90f;
        comp.ease = BattleModeCards.PunchEase();
        comp.cards = new RectTransform[Files.Length];

        for (int i = 0; i < Files.Length; i++)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + Files[i]);
            if (tex == null) Debug.LogWarning("[ModeCards] 找不到贴图 " + GenDir + Files[i]);

            var cardGO = new GameObject("Card_" + Files[i].Replace("BattleModeCard_", "").Replace(".png", ""),
                                        typeof(RectTransform), typeof(RawImage));
            cardGO.transform.SetParent(root.transform, false);
            var crt = cardGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot     = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(CardW, CardH);
            crt.anchoredPosition = Vector2.zero;
            var raw = cardGO.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = true;
            comp.cards[i] = crt;

            var labelGO = new GameObject("Text_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(cardGO.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot     = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(CardW - 24f, 64f);
            lrt.anchoredPosition = new Vector2(0f, CardH * (0.5f - SplitY * 0.5f));   // 标题槽正中
            var text = labelGO.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = Labels[i];
            text.fontSize = TitlePx;
            text.color = Cream;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        comp.SnapToRest();               // 编辑器里就摆在停位，方便直接看
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[ModeCards] 已生成到 " + PanelName + " 下。停位 / 间距 / 时长在 BattleModeCards 组件上调。");
    }

    [MenuItem("Tools/异界/大厅：删除战斗子弹窗里的模式卡")]
    public static void Remove()
    {
        var panel = FindPanelBattle();
        if (panel == null) { Debug.LogWarning("[ModeCards] 当前场景找不到 Panel_Battle"); return; }
        Transform old = panel.transform.Find(RootName);
        if (old == null) { Debug.Log("[ModeCards] Panel_Battle 下没有 " + RootName + "，无需删除"); return; }
        Object.DestroyImmediate(old.gameObject);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Debug.Log("[ModeCards] 已从 " + PanelName + " 下删除 " + RootName);
    }

    /// <summary>场景里名为 Panel_Battle 的 LobbySubPanel（含未激活的）。</summary>
    static LobbySubPanel FindPanelBattle()
    {
        foreach (var p in Object.FindObjectsOfType<LobbySubPanel>(true))
        {
            if (p == null) continue;
            if (p.gameObject.name == PanelName) return p;
        }
        return null;
    }

    /// <summary>复用场景里已有 TMP 的字体（保证中文正常），兜底 TMP 默认字体。</summary>
    static TMP_FontAsset FindChineseFont()
    {
        var any = Object.FindObjectOfType<TextMeshProUGUI>(true);
        if (any != null && any.font != null) return any.font;
        return TMP_Settings.defaultFontAsset;
    }
}