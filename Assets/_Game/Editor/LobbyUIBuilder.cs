using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 一键把大厅 UI 占位套装铺进当前场景（Lobby.unity）的 Canvas 下，根节点固定名 LobbyUI_v1。
/// 版式口径 = Tools/cardframe/LobbyUIv1.ps1 的 New-LobbyUiMockup（屏幕 px · 1920×1080）：
///   Canvas
///    └─ LobbyUI_v1                    ← 紧随 Background（背景之上、弹窗与旧按钮之下）
///         ├─ Ref_Backdrop             ← 深蓝黑石殿底，全屏拉伸
///         ├─ Ref_Emblem               ← 中央棋盘徽记 1020×1020（α 0.6），居中
///         ├─ Plate_Profile            ← 左上头像衬托板（齐屏幕上沿 / 左沿）
///         │    ├─ Avatar_Ring / Text_PlayerName / Icon_Friend
///         ├─ Plate_TopBand            ← 右上横栏（齐屏幕上沿 / 右沿）
///         │    ├─ Icon_Coin + Text_Coin / Icon_Ticket + Text_Ticket / Icon_Gear
///         │    └─ Icon_Shop / Icon_Event / Icon_Tutorial（无底板，挂在栏下）
 ///         ├─ Entry_Battle / Entry_Cards                  ← 上排两块入口板（板心锚屏幕右上角，各带自己的微透视）
 ///         └─ Entry_BottomRow                            ← **透明大框**：与上面两块同尺寸（666x145）的空 RectTransform，不画任何东西、只用于限位
 ///              ├─ Entry_Room                            ← 大框左格（格内左上 0,0，板身 324x130）
 ///              └─ Entry_More                            ← 大框右格（格内左上 342,0，板身 324x130；与左格同形体、同倾角，两条上沿平行）
///         └─ Popup_Placeholder                        ← 占位弹窗（**存成 inactive**）：Dim 遮罩（点一下关）/ Panel / Text_Title / Text_Hint / Btn_Close
/// 摆位与贴图为主；**唯一的逻辑**是那四张「压墙」图标：悬停换贴图（LobbyIconHover）、点击弹占位弹窗（LobbyPopup）。
/// 场景里已有的入口按钮一律不动；重复执行会先删掉 LobbyUI_v1 再重建（接线在脚本里，重跑不会丢）。
/// 生成后位置 / 尺寸 / 倾角直接在 Scene 或 Inspector 里拖。
/// </summary>
public static class LobbyUIBuilder
{
    const string RootName = "LobbyUI_v1";
    const string UiDir = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/";
    const string BgDir = "Assets/_Game/Art/Sprites/Generated/lobby-v1/";
    const string FontPath = "Assets/_Game/Fonts/NotoSerifCJKsc-Bold SDF.asset";

    // 入口板不再统一倾斜：每块板自己的角度写在 BuildLobbyUi 的 NewEntry 调用里（与 LobbyUIv1.ps1 的 $ENTRY_SHAPE 一一对应）。
    // 端头斜切与远端收缩已经烤进贴图 —— 场景这边只给倾角；外框尺寸直接读贴图，不再自己算几何。
    static readonly Vector2 AnchorTL = new Vector2(0f, 1f);
    static readonly Vector2 AnchorTR = new Vector2(1f, 1f);
    static readonly Vector2 AnchorC = new Vector2(0.5f, 0.5f);
    static readonly Vector2 PivotTL = new Vector2(0f, 1f);
    static readonly Vector2 PivotC = new Vector2(0.5f, 0.5f);

    // 下排那块**透明大框**（2026-09-26）：空容器，不画任何东西、只用于限位。
    // 尺寸 = 上面那两块（666x145），竖切成两格（各 324 宽、中缝 18）：房间 = 左格、其它 = 右格，两块左右贴齐大框外沿。
    // 两块是**同一大框的两格** —— 形体（倾角 / 斜切 / 收缩）与尺寸必须完全一样，否则两条上沿不平行。
    // 大框只 145 高、板已 130 高，装不下原来的 57px 错落 —— 两格同一行。
    // 整簇位置（2026-09-26）：**卡牌总览中心 = 右半屏正中 (1440, 540)** —— 上排两块与大框一起平移，相对关系不变。
    // BoxPos = 大框左上角在 1920x1080 版式里的位置（屏幕上沿往下 668.5 / 左沿往右 1133.5）——
    // 与 LobbyUIv1.ps1 的 $BOX_* / $ENTRY_CELL 一一对应，那边动一个数这边也要动。
    static readonly Vector2 BoxSize = new Vector2(666f, 145f);
    static readonly Vector2 BoxPos = new Vector2(-786.5f, -668.5f);
    static readonly Vector2 CellRoom = new Vector2(0f, 0f);
    static readonly Vector2 CellMore = new Vector2(342f, 0f);
    static readonly Color Cream = new Color32(240, 232, 210, 236);      // mockup 文字色 #F0E8D2 α0.93

    static TMP_FontAsset _font;

    [MenuItem("Tools/异界/生成大厅 UI v1（占位）")]
    public static void BuildLobbyUi()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity");
            return;
        }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        var previous = GameObject.Find(RootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);

        var root = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "生成大厅 UI v1");
        var rootRT = (RectTransform)root.transform;
        rootRT.SetParent(canvas.transform, false);
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        rootRT.pivot = PivotC;
        rootRT.SetSiblingIndex(Mathf.Min(1, canvas.transform.childCount - 1));

        // ── 底 + 徽记 ──
        RawImage backdrop = NewRaw(rootRT, "Ref_Backdrop", BgDir + "LobbyBack.png", Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = Vector2.zero;
        backdrop.rectTransform.offsetMax = Vector2.zero;
        backdrop.raycastTarget = false;

        RawImage emblem = NewRaw(rootRT, "Ref_Emblem", BgDir + "LobbyBackEmblem.png", AnchorC, PivotC, Vector2.zero, new Vector2(1020f, 1020f));
        emblem.color = new Color(1f, 1f, 1f, 0.6f);
        emblem.raycastTarget = false;

        // ── 左上：头像衬托板（圆框 = 头像位，右侧 = 名字位，好友图标挂下沿）──
        RawImage profile = NewRaw(rootRT, "Plate_Profile", UiDir + "LobbyProfilePlate.png", AnchorTL, PivotTL, Vector2.zero, new Vector2(467f, 96f));
        NewRaw(profile.rectTransform, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL, new Vector2(36f, -2f), new Vector2(88f, 88f));
        NewLabel(profile.rectTransform, "Text_PlayerName", "名字", new Vector2(184f, -12f), new Vector2(240f, 44f), 26f);
        RawImage iconFriend = NewRaw(profile.rectTransform, "Icon_Friend", UiDir + "Icon_LobbyFriend.png", AnchorTL, PivotTL, new Vector2(160f, -57f), new Vector2(46f, 46f));

        // ── 右上：横栏 + 两个货币 + 齿轮；栏下商城 / 活动 / 教程（无底板），整簇锚屏幕右上角 ──
        RawImage band = NewRaw(rootRT, "Plate_TopBand", UiDir + "LobbyBandRight.png", AnchorTR, PivotTL, new Vector2(-538f, 0f), new Vector2(538f, 95f));
        NewRaw(band.rectTransform, "Icon_Coin", UiDir + "Icon_LobbyCoin.png", AnchorTL, PivotTL, new Vector2(110f, -16f), new Vector2(48f, 48f));
        NewLabel(band.rectTransform, "Text_Coin", "1,280", new Vector2(164f, -24f), new Vector2(150f, 46f), 28f);
        NewRaw(band.rectTransform, "Icon_Ticket", UiDir + "Icon_LobbyTicket.png", AnchorTL, PivotTL, new Vector2(300f, -16f), new Vector2(48f, 48f));
        NewLabel(band.rectTransform, "Text_Ticket", "360", new Vector2(354f, -24f), new Vector2(150f, 46f), 28f);
        NewRaw(band.rectTransform, "Icon_Gear", UiDir + "Icon_LobbyGear.png", AnchorTL, PivotTL, new Vector2(446f, -2f), new Vector2(92f, 92f));
        RawImage iconShop = NewRaw(band.rectTransform, "Icon_Shop", UiDir + "Icon_LobbyShop.png", AnchorTL, PivotTL, new Vector2(109f, -66f), new Vector2(60f, 60f));
        RawImage iconEvent = NewRaw(band.rectTransform, "Icon_Event", UiDir + "Icon_LobbyEvent.png", AnchorTL, PivotTL, new Vector2(232f, -66f), new Vector2(60f, 60f));
        RawImage iconTutorial = NewRaw(band.rectTransform, "Icon_Tutorial", UiDir + "Icon_LobbyTutorial.png", AnchorTL, PivotTL, new Vector2(358f, -66f), new Vector2(60f, 60f));

        // ── 右半：四块入口板（板心锚屏幕右上角；名字是子物体，跟着板一起倾斜）──
        // ── 右半：入口板（上排两块板心锚屏幕右上角；下排两块挂在那块透明大框下）──
        NewEntry(rootRT, "Entry_Battle", "LobbyEntryPlate_Battle.png", "战斗", AnchorTR, new Vector2(-453.5f, -350f), new Vector2(666f, 145f), 44f, 1.6f);
        NewEntry(rootRT, "Entry_Cards", "LobbyEntryPlate_Cards.png", "卡牌总览", AnchorTR, new Vector2(-480f, -540f), new Vector2(661f, 149f), 46f, 0f);

        // 下排：房间 / 其它 是**一个透明大框的两个格子** —— 大框本身不画，只把两块的位置钉在格子左上角。
        // 这样整排一起拖就改 BoxPos，两块各自的倾角仍是自己的 Rotation Z。
        RectTransform bottomRow = NewRect(rootRT, "Entry_BottomRow", AnchorTR, PivotTL, BoxPos, BoxSize);
        NewEntry(bottomRow, "Entry_Room", "LobbyEntryPlate_Room.png", "房间", AnchorTL, CellCenter(CellRoom, new Vector2(324f, 130f)), new Vector2(324f, 130f), 30f, -1.5f);
        NewEntry(bottomRow, "Entry_More", "LobbyEntryPlate_More.png", "其它", AnchorTL, CellCenter(CellMore, new Vector2(324f, 130f)), new Vector2(324f, 130f), 30f, -1.5f);

        // ── 悬停 / 点击（2026-09-26）：四个「压墙」图标挂悬停组件，点开同一个占位弹窗 ──
        LobbyPopup popup = NewPlaceholderPopup(rootRT, new Vector2(900f, 520f));
        WireIconHover(iconFriend, "Icon_LobbyFriend.png", "Icon_LobbyFriendHover.png", popup, "好友");
        WireIconHover(iconShop, "Icon_LobbyShop.png", "Icon_LobbyShopHover.png", popup, "商城");
        WireIconHover(iconEvent, "Icon_LobbyEvent.png", "Icon_LobbyEventHover.png", popup, "活动");
        WireIconHover(iconTutorial, "Icon_LobbyTutorial.png", "Icon_LobbyTutorialHover.png", popup, "教程");
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[LobbyUI] 已在 Canvas 下生成 " + RootName + "（占位）：位置 / 尺寸在 Scene 里拖；四张「压墙」图标已接悬停 + 点击弹占位弹窗，倾角改 Entry_* 的 Rotation Z；下排整排位置改 Entry_BottomRow（透明大框，只限位）；形体（斜切 / 远端收缩）改 LobbyUIv1.ps1 的 $ENTRY_SHAPE 后重新出图再跑本菜单；悬停 / 弹窗改本脚本的 WireIconHover / NewPlaceholderPopup。");
    }


    // ── 临时隐藏旧 UI（2026-09-26）：只看 LobbyUI_v1 时用 ─────────────────────
    // 只动 Canvas 下**除 LobbyUI_v1 之外**的直接子物体（旧的 Background / 入口按钮 / 各种 Panel）；
    // 相机 / 灯光 / 三个 Manager / EventSystem 一律不碰 —— 那些一关，场景既看不见也点不动。
    // 隐藏清单按场景路径存 EditorPrefs，「恢复」照清单勾回来；两步都能 Ctrl+Z 撤。
    const string HiddenKeyPrefix = "AnotherWorld.LobbyUI.Hidden.";

    [MenuItem("Tools/异界/临时隐藏大厅旧 UI（只留 LobbyUI_v1）")]
    public static void HideLegacyLobbyUi()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity");
            return;
        }
        var hidden = new List<string>();
        foreach (Transform child in canvas.transform)
        {
            if (child.name == RootName || !child.gameObject.activeSelf) continue;
            Undo.RecordObject(child.gameObject, "隐藏大厅旧 UI");
            child.gameObject.SetActive(false);
            hidden.Add(child.name);
        }
        EditorPrefs.SetString(HiddenKeyPrefix + EditorSceneManager.GetActiveScene().path, string.Join("\n", hidden.ToArray()));
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log($"[LobbyUI] 临时隐藏了 {hidden.Count} 个旧节点（相机 / 灯光 / Manager / EventSystem 没动）：{string.Join(" / ", hidden.ToArray())}");
    }

    [MenuItem("Tools/异界/恢复大厅旧 UI")]
    public static void RestoreLegacyLobbyUi()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LobbyUI] 当前场景没有 Canvas");
            return;
        }
        string key = HiddenKeyPrefix + EditorSceneManager.GetActiveScene().path;
        string raw = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[LobbyUI] 没有隐藏清单 —— 可能已恢复过，或用 Ctrl+Z 撤回了");
            return;
        }
        string[] names = raw.Split('\n');
        int count = 0;
        foreach (Transform child in canvas.transform)
        {
            if (System.Array.IndexOf(names, child.name) < 0) continue;
            Undo.RecordObject(child.gameObject, "恢复大厅旧 UI");
            child.gameObject.SetActive(true);
            count++;
        }
        EditorPrefs.DeleteKey(key);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log($"[LobbyUI] 恢复 {count} 个旧节点。");
    }

    /// <summary>入口板：定尺贴图 + 文字子物体。body 是**板身**屏幕尺寸（贴图 = 屏幕 ×3）；
    /// 端头斜切与远端收缩已烤进贴图，贴图因此比板身大一圈：外框尺寸**直接读贴图像的像素尺寸 ÷ 3**
    /// （出图脚本是唯一版式口径，这里不再自己算几何）。板身在外框里居中，所以 centerPos 依旧按板心给。
    /// 文字口径与 mockup 的 Get-EntryLabelOffset 一致 —— 距板身左沿 58px、字顶落在板心上方 0.87×字号。</summary>
    static void NewEntry(Transform parent, string name, string texture, string label, Vector2 anchor, Vector2 centerPos, Vector2 body, float fontSize, float tilt)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + texture);
        if (tex == null) Debug.LogWarning($"[LobbyUI] 找不到贴图：{UiDir + texture}");
        var frame = tex != null ? new Vector2(tex.width / 3f, tex.height / 3f) : body;
        RectTransform rt = NewRect(parent, name, anchor, PivotC, centerPos, frame);
        var image = rt.gameObject.AddComponent<RawImage>();
        image.texture = tex;
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
        NewLabel(rt, "Label", label, EntryLabelPos(body, frame, fontSize), new Vector2(body.x - 90f, fontSize * 1.6f), fontSize);
    }

    /// <summary>板心在**大框**里的位置：大框原点 = 左上角、格子坐标 y 向下，这里翻成 anchoredPosition 的 y 向上。</summary>
    static Vector2 CellCenter(Vector2 cell, Vector2 body)
    {
        return new Vector2(cell.x + body.x * 0.5f, -(cell.y + body.y * 0.5f));
    }

    /// <summary>板上文字的落点（相对**贴图外框**左上角 —— Label 的锚 / 轴都是左上角）。
    /// 口径同 mockup 的 Get-EntryLabelOffset：距**板身**左沿 58px、字顶落在板心上方 0.87×字号；
    /// 贴图比板身大一圈（$ENTRY_PAD），所以要把半外框加回来。注意字顶在板心上方，anchoredPosition.y 是负的。</summary>
    static Vector2 EntryLabelPos(Vector2 body, Vector2 frame, float fontSize)
    {
        return new Vector2(58f - body.x * 0.5f + frame.x * 0.5f,
                           body.y * 0.5f - 0.87f * fontSize - frame.y * 0.5f);
    }

    static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RawImage NewRaw(Transform parent, string name, string texturePath, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewRect(parent, name, anchor, pivot, pos, size);
        var image = rt.gameObject.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (image.texture == null) Debug.LogWarning($"[LobbyUI] 找不到贴图：{texturePath}");
        return image;
    }

    static TextMeshProUGUI NewLabel(Transform parent, string name, string content, Vector2 pos, Vector2 size, float fontSize)
    {
        RectTransform rt = NewRect(parent, name, AnchorTL, PivotTL, pos, size);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    // ── 悬停 / 点击（2026-09-26）：四个「压墙」图标 + 占位弹窗 ─────────────────
    /// <summary>给图标挂悬停组件：常态 / 悬停两张贴图只差色调（形体尺寸一致），切换时不会跳位。</summary>
    static void WireIconHover(RawImage icon, string normalFile, string hoverFile, LobbyPopup popup, string title)
    {
        var hover = icon.gameObject.AddComponent<LobbyIconHover>();
        hover.icon = icon;
        hover.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + normalFile);
        hover.hoverTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + hoverFile);
        hover.popup = popup;
        hover.title = title;
        if (hover.hoverTexture == null) Debug.LogWarning($"[LobbyUI] 找不到悬停贴图：{UiDir + hoverFile}");
    }

    /// <summary>占位弹窗：Dim 遮罩（点一下也关）+ 面板 + 标题 + 提示 + 关闭按钮。内容等真实面板接进来；
    /// 根节点存成 inactive —— 场景里看不见，运行时点图标才 Show。</summary>
    static LobbyPopup NewPlaceholderPopup(Transform parent, Vector2 panelSize)
    {
        RectTransform root = NewRect(parent, "Popup_Placeholder", AnchorC, PivotC, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        var popup = root.gameObject.AddComponent<LobbyPopup>();

        var dim = NewRect(root, "Dim", AnchorC, PivotC, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        var dimRT = (RectTransform)dim.transform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;
        dim.color = new Color32(6, 9, 14, 200);
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        RawImage panel = NewRaw(root, "Panel", UiDir + "LobbyPopupPlate.png", AnchorC, PivotC, Vector2.zero, panelSize);
        popup.titleText = NewLabel(panel.rectTransform, "Text_Title", "占位", new Vector2(48f, -34f), new Vector2(panelSize.x - 96f, 64f), 42f);
        TextMeshProUGUI hint = NewLabel(panel.rectTransform, "Text_Hint", "占位 · 待接真实面板", new Vector2(48f, -112f), new Vector2(panelSize.x - 96f, 40f), 24f);
        hint.color = new Color(240f / 255f, 232f / 255f, 210f / 255f, 0.62f);

        RawImage close = NewRaw(panel.rectTransform, "Btn_Close", UiDir + "LobbyPopupBtnPlate.png", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 36f), new Vector2(200f, 64f));
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;
        ColorBlock colors = closeButton.colors;
        colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        closeButton.colors = colors;
        NewCenterLabel(close.rectTransform, "Text", "关闭", 26f);

        UnityEventTools.AddPersistentListener(dimButton.onClick, new UnityAction(popup.Hide));
        UnityEventTools.AddPersistentListener(closeButton.onClick, new UnityAction(popup.Hide));

        popup.gameObject.SetActive(false);
        return popup;
    }

    /// <summary>居中的小字（关闭按钮里那个）。</summary>
    static TextMeshProUGUI NewCenterLabel(Transform parent, string name, string content, float fontSize)
    {
        TextMeshProUGUI text = NewLabel(parent, name, content, Vector2.zero, new Vector2(200f, fontSize * 1.6f), fontSize);
        RectTransform rt = text.rectTransform;
        rt.anchorMin = AnchorC;
        rt.anchorMax = AnchorC;
        rt.pivot = PivotC;
        rt.anchoredPosition = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

}
