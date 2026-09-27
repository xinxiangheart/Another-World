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
///                                                        **建好就提到 Canvas 下并排到最后** —— 见下面「接入实际功能」那段
/// 逻辑分两处：四个「压墙」图标 悬停换贴图（LobbyIconHover）；八块入口板 悬停文字变金 + 点击弹占位弹窗（LobbyPlateHover）。
/// 左上头像是真数据（SteamDataManager → PlayerProfilePanel），右上齿轮开全局设置面板（LobbySettingsButton → SettingsPanel）。
/// 2026-09-27「接入实际功能」写在这份脚本末尾：菜单「大厅：接入实际功能」是补丁式的（不重建），
/// BuildLobbyUi / BuildCornerRow 里也各有一份同样的调用 —— 重跑整套构建不会丢接线。
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
    static readonly Color GoldBright = new Color32(228, 203, 132, 255); // 本套亮金 #E4CB84（入口板悬停文字色）

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

        // 两个层（HUD 常驻 / 全屏子弹窗）—— 见本文件末尾「分层 + 子全屏弹窗」那段
        Transform subLayer, hudLayer;
        EnsureUiLayers(canvas, out subLayer, out hudLayer);

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
        RawImage profile = NewRaw(hudLayer, "Plate_Profile", UiDir + "LobbyProfilePlate.png", AnchorTL, PivotTL, Vector2.zero, new Vector2(467f, 96f));
        NewRaw(profile.rectTransform, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL, new Vector2(36f, -2f), new Vector2(88f, 88f));
        NewLabel(profile.rectTransform, "Text_PlayerName", "名字", new Vector2(184f, -12f), new Vector2(240f, 44f), 26f);
        RawImage iconFriend = NewRaw(profile.rectTransform, "Icon_Friend", UiDir + "Icon_LobbyFriend.png", AnchorTL, PivotTL, new Vector2(160f, -57f), new Vector2(46f, 46f));

        // ── 右上：横栏 + 两个货币 + 齿轮；栏下商城 / 活动 / 教程（无底板），整簇锚屏幕右上角 ──
        RawImage band = NewRaw(hudLayer, "Plate_TopBand", UiDir + "LobbyBandRight.png", AnchorTR, PivotTL, new Vector2(-538f, 0f), new Vector2(538f, 95f));
        NewRaw(band.rectTransform, "Icon_Coin", UiDir + "Icon_LobbyCoin.png", AnchorTL, PivotTL, new Vector2(110f, -16f), new Vector2(48f, 48f));
        NewLabel(band.rectTransform, "Text_Coin", "1,280", new Vector2(164f, -24f), new Vector2(150f, 46f), 28f);
        NewRaw(band.rectTransform, "Icon_Ticket", UiDir + "Icon_LobbyTicket.png", AnchorTL, PivotTL, new Vector2(300f, -16f), new Vector2(48f, 48f));
        NewLabel(band.rectTransform, "Text_Ticket", "360", new Vector2(354f, -24f), new Vector2(150f, 46f), 28f);
        NewRaw(band.rectTransform, "Icon_Gear", UiDir + "Icon_LobbyGear.png", AnchorTL, PivotTL, new Vector2(446f, -2f), new Vector2(92f, 92f));
        // 四个「压墙」图标（2026-09-26 十五次修正）：加了「邮件」，四个在横栏下等距重排
        //   板心 x = 100 / 190 / 280 / 370（步进 90）· 板心 y = -66 · 60x60
        //   左端距横栏左下角（局部 51,56）留 49px，右端距齿轮（局部 446..538）留 16px
        RawImage iconShop = NewRaw(band.rectTransform, "Icon_Shop", UiDir + "Icon_LobbyShop.png", AnchorTL, PivotTL, new Vector2(100f, -66f), new Vector2(60f, 60f));
        RawImage iconEvent = NewRaw(band.rectTransform, "Icon_Event", UiDir + "Icon_LobbyEvent.png", AnchorTL, PivotTL, new Vector2(190f, -66f), new Vector2(60f, 60f));
        RawImage iconTutorial = NewRaw(band.rectTransform, "Icon_Tutorial", UiDir + "Icon_LobbyTutorial.png", AnchorTL, PivotTL, new Vector2(280f, -66f), new Vector2(60f, 60f));
        RawImage iconMail = NewRaw(band.rectTransform, "Icon_Mail", UiDir + "Icon_LobbyMail.png", AnchorTL, PivotTL, new Vector2(370f, -66f), new Vector2(60f, 60f));

        // ── 右半：四块入口板（板心锚屏幕右上角；名字是子物体，跟着板一起倾斜）──
        // ── 右半：入口板（上排两块板心锚屏幕右上角；下排两块挂在那块透明大框下）──
        RawImage entryBattle = NewEntry(rootRT, "Entry_Battle", "LobbyEntryPlate_Battle.png", "战斗", AnchorTR, new Vector2(-453.5f, -350f), new Vector2(666f, 145f), 44f, 1.6f);
        RawImage entryCards = NewEntry(rootRT, "Entry_Cards", "LobbyEntryPlate_Cards.png", "卡牌总览", AnchorTR, new Vector2(-480f, -540f), new Vector2(661f, 149f), 46f, 0f);

        // 下排：房间 / 其它 是**一个透明大框的两个格子** —— 大框本身不画，只把两块的位置钉在格子左上角。
        // 这样整排一起拖就改 BoxPos，两块各自的倾角仍是自己的 Rotation Z。
        RectTransform bottomRow = NewRect(rootRT, "Entry_BottomRow", AnchorTR, PivotTL, BoxPos, BoxSize);
        RawImage entryRoom = NewEntry(bottomRow, "Entry_Room", "LobbyEntryPlate_Room.png", "房间", AnchorTL, CellCenter(CellRoom, new Vector2(324f, 130f)), new Vector2(324f, 130f), 30f, -1.5f);
        RawImage entryMore = NewEntry(bottomRow, "Entry_More", "LobbyEntryPlate_More.png", "其它", AnchorTL, CellCenter(CellMore, new Vector2(324f, 130f)), new Vector2(324f, 130f), 30f, -1.5f);

        // ── 悬停 / 点击（2026-09-26）：四个「压墙」图标挂悬停组件，点开同一个占位弹窗 ──
        LobbyPopup popup = NewPlaceholderPopup(subLayer, new Vector2(900f, 520f));
        WireIconHover(iconFriend, "Icon_LobbyFriend.png", "Icon_LobbyFriendHover.png", popup, "好友", null, true);   // 好友 = 弹窗里显示自己的「异界号」
        WireIconHover(iconShop, "Icon_LobbyShop.png", "Icon_LobbyShopHover.png", popup, "商城");
        WireIconHover(iconEvent, "Icon_LobbyEvent.png", "Icon_LobbyEventHover.png", popup, "活动");
        WireIconHover(iconTutorial, "Icon_LobbyTutorial.png", "Icon_LobbyTutorialHover.png", popup, "教程");
        WireIconHover(iconMail, "Icon_LobbyMail.png", "Icon_LobbyMailHover.png", popup, "邮件");

        // ── 全屏子弹窗：战斗（通用背景 + 通用关闭叉）——HUD 层在它后面，所以左上 / 右上永远可见 ──
        GameObject battlePanel = BuildSubPanel(subLayer, hudLayer.gameObject, "Panel_Battle", "战斗");

        // ── 接入实际功能（2026-09-27）：入口板 悬停变金 + 点击占位；左上 Steam 资料；右上齿轮接设置 ──
        WirePlateHover(entryBattle, popup, "战斗");
        WirePlateHover(entryCards, popup, "卡牌总览");
        WirePlateHover(entryRoom, popup, "房间");
        WirePlateHover(entryMore, popup, "其它");
        entryBattle.GetComponent<LobbyPlateHover>().subPanel = battlePanel.GetComponent<LobbySubPanel>();   // 战斗 → 全屏子弹窗
        WireProfile(profile.rectTransform);
        WireSettingsGear(band.rectTransform);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[LobbyUI] 已在 Canvas 下生成 " + RootName + "（占位）：位置 / 尺寸在 Scene 里拖；四张「压墙」图标已接悬停 + 点击弹占位弹窗，倾角改 Entry_* 的 Rotation Z；下排整排位置改 Entry_BottomRow（透明大框，只限位）；形体（斜切 / 远端收缩）改 LobbyUIv1.ps1 的 $ENTRY_SHAPE 后重新出图再跑本菜单；悬停 / 弹窗改本脚本的 WireIconHover / NewPlaceholderPopup。");
    }

    // ── 右下角入口条（2026-09-26 十四次修正）────────────────────────────────────
    // 用户：在右下角加入类似于这几个形状的**紧挨着的、平放着的、不参与视差的**四块，
    //       从右到左分别是 赛季 / 公告 / 藏品 / 战绩，大小和第二张图（房间）差不多大小。
    //       2026-09-26 十七次修正（用户：将战绩改为成就，ui 也变一下，战绩会后续做到其它地方）：
    //       第四块 **战绩 -> 成就** —— 键名 'record' -> 'achievement'（贴图 LobbyCornerPlate_Achievement.png），
    //       徽记由「三柱 + 基线」换成「奖章（两条绶带 + 圆盘 + 中央菱形）」；战绩那支的出图分支与 'record' 形体键**保留备用**，不在这一条里。
    //   · 平放      这四块不写 Rotation Z（场景里就是 0°），贴图也按 $ENTRY_SHAPE = 0/0/0 出（无透视）
    //   · 紧挨      板身宽 300，格距就是 300 —— 板身首尾相接；贴图各自带 $ENTRY_PAD 的透明边（8 屏 px），
    //               相邻两块的外框会叠 16px，那圈是透明的，看着就是紧挨
    //   · 不参与视差 **不挂**进 Bg_v2 的 LobbyBgParallax.layers —— 它就是静止的
    // 与上面那一簇刻意区分：那一簇是「微透视」（每块自己的倾角 / 端头斜切 / 远端收缩），这一条是平的。
    // 摆位（用户 2026-09-26：**紧贴右下角**）：板身右沿贴屏幕右沿 1920、下沿贴屏幕下沿 0 —— 一丝余量都不留。
    // 只动自己这棵 CornerRow_v1，**不重建 LobbyUI_v1** —— 上面那些块的手调位置不会被冲掉。
    const float CornerBodyW = 300f;
    const float CornerBodyH = 120f;
    const float CornerRight = 1920f;
    const float CornerBottom = 0f;
    const float CornerFontSize = 28f;
    static readonly string[] CornerKinds = { "season", "notice", "collection", "achievement" };   // 屏幕上从右到左
    static readonly string[] CornerLabels = { "赛季", "公告", "藏品", "成就" };

    [MenuItem("Tools/异界/生成大厅右下角入口条（赛季/公告/藏品/成就）")]
    public static void BuildCornerRow()
    {
        Transform parent = null;
        GameObject ui = GameObject.Find(RootName);
        if (ui != null) parent = ui.transform;
        if (parent == null)
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null) parent = canvas.transform;
        }
        if (parent == null)
        {
            Debug.LogError("[LobbyUI] 场景里既没有 " + RootName + " 也没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity");
            return;
        }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        GameObject previous = GameObject.Find(CornerRootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);

        var popup = Object.FindObjectOfType<LobbyPopup>(true);

        // 容器只用于限位，自己什么都不画（用法同 Entry_BottomRow）
        float rowW = CornerBodyW * CornerKinds.Length;
        RectTransform rowRT = NewRect(parent, CornerRootName, new Vector2(1f, 0f), new Vector2(1f, 0f),
                                      new Vector2(CornerRight - 1920f, CornerBottom),
                                      new Vector2(rowW, CornerBodyH));
        Undo.RegisterCreatedObjectUndo(rowRT.gameObject, "生成大厅右下角入口条");
        rowRT.SetAsLastSibling();

        for (int i = 0; i < CornerKinds.Length; i++)
        {
            string kind = CornerKinds[i];
            string cap = char.ToUpper(kind[0]) + kind.Substring(1);
            var center = new Vector2(-(CornerBodyW * 0.5f + CornerBodyW * i), CornerBodyH * 0.5f);
            RawImage corner = NewEntry(rowRT, "Entry_" + cap, "LobbyCornerPlate_" + cap + ".png", CornerLabels[i],
                     new Vector2(1f, 0f), center, new Vector2(CornerBodyW, CornerBodyH), CornerFontSize, 0f);
            WirePlateHover(corner, popup, CornerLabels[i]);
        }

        Selection.activeGameObject = rowRT.gameObject;
        EditorSceneManager.MarkSceneDirty(rowRT.gameObject.scene);
        Debug.Log("[LobbyUI] 已在 " + parent.name + " 下生成 " + CornerRootName +
                  "（赛季 / 公告 / 藏品 / 成就 · 平放 + 紧挨 + 不参与视差）。" +
                  "整条位置改 " + CornerRootName + " 的 anchoredPosition（紧贴右下角 = 0,0）；尺寸 / 间距改本方法的 CornerBodyW / CornerBodyH；" +
                  "要加新入口就往 CornerKinds / CornerLabels 里各加一项（贴图名 LobbyCornerPlate_<首字母大写>.png）。");
    }

    [MenuItem("Tools/异界/删除大厅右下角入口条")]
    public static void DeleteCornerRow()
    {
        GameObject go = GameObject.Find(CornerRootName);
        if (go == null) { Debug.Log("[LobbyUI] 场景里没有 " + CornerRootName); return; }
        Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[LobbyUI] 已删除 " + CornerRootName);
    }

    const string CornerRootName = "CornerRow_v1";

    // ── 补「邮件」并重排四个压墙图标（2026-09-26 十五次修正）────────────────────
    // 用户：出一个邮件的小 ui 放到右上横栏下那一排，并重排这四个。
    // 给**已经建好的** LobbyUI_v1 打补丁：只动这四个图标，别的一概不碰、**不重建**。
    // 为什么不走「生成大厅 UI v1」：重建会把 CornerRow_v1 一起删掉，还会把 Ref_Emblem 重新打开
    //（背景换成 Bg_v2 之后，那个旧徽记不该再出现）。四个 x 与 BuildLobbyUi 里那四行是同一组数，
    // 所以重跑整套构建也会得到同样的结果。
    static readonly string[] IconRowNames = { "Icon_Shop", "Icon_Event", "Icon_Tutorial", "Icon_Mail" };
    static readonly string[] IconRowFiles = { "Icon_LobbyShop.png", "Icon_LobbyEvent.png", "Icon_LobbyTutorial.png", "Icon_LobbyMail.png" };
    static readonly string[] IconRowHover = { "Icon_LobbyShopHover.png", "Icon_LobbyEventHover.png", "Icon_LobbyTutorialHover.png", "Icon_LobbyMailHover.png" };
    static readonly string[] IconRowTitles = { "商城", "活动", "教程", "邮件" };
    const float IconRowFirst = 100f;
    const float IconRowStep = 90f;
    const float IconRowY = -66f;

    [MenuItem("Tools/异界/大厅：补邮件图标并重排四个压墙图标")]
    public static void ApplyMailIconRow()
    {
        GameObject ui = GameObject.Find(RootName);
        if (ui == null) { Debug.LogError("[LobbyUI] 场景里没有 " + RootName + " —— 先跑「生成大厅 UI v1」"); return; }
        Transform band = ui.transform.Find("Plate_TopBand");
        if (band == null) { Debug.LogError("[LobbyUI] 找不到 " + RootName + "/Plate_TopBand"); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var popup = Object.FindObjectOfType<LobbyPopup>(true);
        if (popup == null) Debug.LogWarning("[LobbyUI] 场景里找不到 LobbyPopup —— 图标先不接点击");

        for (int i = 0; i < IconRowNames.Length; i++)
        {
            Transform t = band.Find(IconRowNames[i]);
            RawImage img;
            var pos = new Vector2(IconRowFirst + IconRowStep * i, IconRowY);
            if (t == null)
            {
                img = NewRaw(band, IconRowNames[i], UiDir + IconRowFiles[i], AnchorTL, PivotTL, pos, new Vector2(60f, 60f));
                Undo.RegisterCreatedObjectUndo(img.gameObject, "补 " + IconRowNames[i]);
            }
            else
            {
                var rt = t as RectTransform;
                Undo.RecordObject(rt, "重排压墙图标");
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(60f, 60f);
                img = t.GetComponent<RawImage>();
                if (img == null) img = t.gameObject.AddComponent<RawImage>();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + IconRowFiles[i]);
                if (tex != null) img.texture = tex;
            }
            if (img != null && popup != null) WireIconHover(img, IconRowFiles[i], IconRowHover[i], popup, IconRowTitles[i]);
        }
        EditorSceneManager.MarkSceneDirty(ui.scene);
        Debug.Log("[LobbyUI] 压墙图标已重排为四个：商城 / 活动 / 教程 / 邮件（x = 100 / 190 / 280 / 370 · y = -66 · 60x60）。");
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
    static RawImage NewEntry(Transform parent, string name, string texture, string label, Vector2 anchor, Vector2 centerPos, Vector2 body, float fontSize, float tilt)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + texture);
        if (tex == null) Debug.LogWarning($"[LobbyUI] 找不到贴图：{UiDir + texture}");
        var frame = tex != null ? new Vector2(tex.width / 3f, tex.height / 3f) : body;
        RectTransform rt = NewRect(parent, name, anchor, PivotC, centerPos, frame);
        var image = rt.gameObject.AddComponent<RawImage>();
        image.texture = tex;
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
        NewLabel(rt, "Label", label, EntryLabelPos(body, frame, fontSize), new Vector2(body.x - 90f, fontSize * 1.6f), fontSize);
        return image;
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
    static void WireIconHover(RawImage icon, string normalFile, string hoverFile, LobbyPopup popup, string title,
                                            string hint = null, bool showMyId = false)
    {
        var hover = icon.GetComponent<LobbyIconHover>();
        if (hover == null) hover = icon.gameObject.AddComponent<LobbyIconHover>();
        hover.icon = icon;
        hover.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + normalFile);
        hover.hoverTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + hoverFile);
        hover.popup = popup;
        hover.title = title;
        hover.hint = hint;
        hover.showMyId = showMyId;
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
        popup.hintText = hint;

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

    // ── 接入实际功能（2026-09-27）────────────────────────────────────────────────
    // 用户：左上角头像和名称要实际显示 Steam 头像与昵称；这些方块文字鼠标悬停变金、点击打开占位图；
    //       右上角设置打开就是设置页面。
    //   ① 八块入口板（上排两块 + 透明大框两格 + 右下角四块）挂 LobbyPlateHover —— 悬停文字变金（#E4CB84）、
    //      点击开同一个占位弹窗。板自己的 RawImage 开 raycastTarget，板上 Label 关掉（指针落在字上冒泡到板）。
    //   ② 左上 Plate_Profile 挂 PlayerProfilePanel：金圆框的「井」上盖一块 68x68 的头像 RawImage（Avatar_Image），
    //      圆形裁切走 PlayerProfilePanel.circularCrop。井的口径来自 LobbyAvatarRing.png
    //      （贴图 264 里半径 102 → 屏幕 88 上 34 → 直径 68）—— 头像位置按**框自己的尺寸现算**，框挪了头像跟着挪。
    //   ③ 右上 Icon_Gear 挂 LobbySettingsButton —— 运行时补 Button 接 SettingsPanel.Toggle（旧那个 inactive 的
    //      名字叫 Setting 的按钮 SettingsLauncher 扫不到，所以不靠它）。
    // 补丁式：只动上面这几处，**不重建 LobbyUI_v1 / CornerRow_v1**，手调的位置不会被冲掉。

    [MenuItem("Tools/异界/大厅：接入实际功能（Steam 资料 / 悬停变金 / 点击占位 / 设置）")]
    public static void WireFunctionalLobby()
    {
        GameObject ui = GameObject.Find(RootName);
        if (ui == null) { Debug.LogError("[LobbyUI] 场景里没有 " + RootName + " —— 先跑「生成大厅 UI v1（占位）」"); return; }

        // 先把两个层钉好（HUD 常驻 / 全屏子弹窗）—— 左上与右上会被搬进 HUD 层
        Canvas canvas = ui.GetComponentInParent<Canvas>();
        Transform sub = null, hud = null;
        if (canvas != null) EnsureUiLayers(canvas, out sub, out hud);
        else Debug.LogWarning("[LobbyUI] 找不到 Canvas —— 分层跳过（HUD 就不会常驻在弹窗之上）");

        var popup = Object.FindObjectOfType<LobbyPopup>(true);
        if (popup == null) Debug.LogWarning("[LobbyUI] 场景里找不到 LobbyPopup —— 点击先不接弹窗");
        else if (sub != null)
        {
            // 占位弹窗归到子弹窗层：SetAsLastSibling 只在**本层**里抬，抬不过 HUD 层
            popup.transform.SetParent(sub, false);
            popup.transform.SetAsLastSibling();
        }

        int wired = 0;
        wired += WirePlate(ui.transform, "Entry_Battle", "战斗", popup);
        wired += WirePlate(ui.transform, "Entry_Cards", "卡牌总览", popup);
        wired += WirePlate(ui.transform, "Entry_BottomRow/Entry_Room", "房间", popup);
        wired += WirePlate(ui.transform, "Entry_BottomRow/Entry_More", "其它", popup);
        wired += WirePlate(ui.transform, CornerRootName + "/Entry_Season", "赛季", popup);
        wired += WirePlate(ui.transform, CornerRootName + "/Entry_Notice", "公告", popup);
        wired += WirePlate(ui.transform, CornerRootName + "/Entry_Collection", "藏品", popup);
        wired += WirePlate(ui.transform, CornerRootName + "/Entry_Achievement", "成就", popup);

        // 左上 / 右上 现在都在 HUD 层里（先按层找，找不到再退回 LobbyUI_v1 —— 兼容没分层的旧场景）
        Transform profile = hud != null ? hud.Find("Plate_Profile") : null;
        if (profile == null) profile = ui.transform.Find("Plate_Profile");
        if (profile != null) WireProfile(profile);
        else Debug.LogWarning("[LobbyUI] 找不到 Plate_Profile —— 头像 / 昵称没接");

        Transform band = hud != null ? hud.Find("Plate_TopBand") : null;
        if (band == null) band = ui.transform.Find("Plate_TopBand");
        if (band != null) WireSettingsGear(band);
        else Debug.LogWarning("[LobbyUI] 找不到 Plate_TopBand —— 齿轮没接");

        // 战斗 → 全屏子弹窗（已存在就复用，不重建）
        int subPanels = 0;
        if (sub != null && hud != null)
        {
            GameObject battle = BuildSubPanel(sub, hud.gameObject, "Panel_Battle", "战斗");
            Transform bt = ui.transform.Find("Entry_Battle");
            var btHover = bt != null ? bt.GetComponent<LobbyPlateHover>() : null;
            if (btHover != null) { btHover.subPanel = battle.GetComponent<LobbySubPanel>(); subPanels = 1; }
        }

        EditorSceneManager.MarkSceneDirty(ui.scene);
        Debug.Log($"[LobbyUI] 已接入实际功能：{wired} 块入口板「悬停变金 + 点击占位」；左上接 Steam 头像 / 昵称；右上齿轮接设置面板；" +
                  "分层已就位（" + SubLayerName + " / " + HudLayerName + "）；战斗子全屏弹窗 " + subPanels + " 个。" +
                  "HUD 层是 Canvas 最后一个子物体 ⇒ 左上 / 右上永远压在全屏弹窗之上（要藏就在 LobbySubPanel 里勾 hideHudOnOpen）。");
    }

    /// <summary>按场景路径取一块入口板，接「悬停文字变金 + 点击占位弹窗」。返回 1 = 接上了。</summary>
    static int WirePlate(Transform root, string path, string title, LobbyPopup popup)
    {
        Transform t = root.Find(path);
        if (t == null) { Debug.LogWarning("[LobbyUI] 找不到入口板：" + RootName + "/" + path); return 0; }

        var plate = t.GetComponent<RawImage>();
        if (plate == null) { Debug.LogWarning("[LobbyUI] " + path + " 上没有 RawImage"); return 0; }

        WirePlateHover(plate, popup, title);
        return 1;
    }

    /// <summary>入口板悬停 / 点击：只改文字色（板贴图不换，形体不动），点击弹占位窗。</summary>
    static void WirePlateHover(RawImage plate, LobbyPopup popup, string title)
    {
        if (plate == null) return;
        plate.raycastTarget = true;

        var label = plate.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.raycastTarget = false;   // 指针落在字上也要冒泡到板这一层

        var hover = plate.GetComponent<LobbyPlateHover>();
        if (hover == null) hover = plate.gameObject.AddComponent<LobbyPlateHover>();
        hover.label = label;
        hover.popup = popup;
        hover.title = title;
        hover.hoverColor = GoldBright;
    }

    /// <summary>左上衬托板：金圆框的「井」上盖一块圆形裁切的头像 + 把名字接到 Steam 昵称。</summary>
    static void WireProfile(Transform profile)
    {
        Transform ring = profile.Find("Avatar_Ring");
        if (ring == null) { Debug.LogWarning("[LobbyUI] 找不到 Avatar_Ring —— 头像没接"); return; }

        // 井的口径：LobbyAvatarRing.png 264 里井半径 102 → 屏幕 88 上 34 → 直径 68
        const float WellDia = 68f;
        var ringRT = ring as RectTransform;
        Vector2 ringPos = ringRT.anchoredPosition;     // 锚 / 轴都是左上角
        Vector2 ringSize = ringRT.sizeDelta;
        var pos = new Vector2(ringPos.x + (ringSize.x - WellDia) * 0.5f,
                              ringPos.y - (ringSize.y - WellDia) * 0.5f);

        RawImage avatar;
        Transform found = profile.Find("Avatar_Image");
        if (found != null)
        {
            avatar = found.GetComponent<RawImage>();
            if (avatar == null) avatar = found.gameObject.AddComponent<RawImage>();
        }
        else
        {
            RectTransform rt = NewRect(profile, "Avatar_Image", AnchorTL, PivotTL, pos, new Vector2(WellDia, WellDia));
            avatar = rt.gameObject.AddComponent<RawImage>();
        }
        avatar.rectTransform.anchoredPosition = pos;
        avatar.rectTransform.sizeDelta = new Vector2(WellDia, WellDia);
        avatar.raycastTarget = false;   // 头像不吃点击，指针走到底下的衬托板 / 金框
        avatar.rectTransform.SetSiblingIndex(Mathf.Min(ring.GetSiblingIndex() + 1, profile.childCount - 1));

        var view = profile.GetComponent<PlayerProfilePanel>();
        if (view == null) view = profile.gameObject.AddComponent<PlayerProfilePanel>();
        view.avatarImage = avatar;
        view.circularCrop = true;

        Transform nameT = profile.Find("Text_PlayerName");
        if (nameT != null)
        {
            view.nameText = nameT.GetComponent<TMP_Text>();
            var nameUI = nameT.GetComponent<TextMeshProUGUI>();
            if (nameUI != null) nameUI.overflowMode = TextOverflowModes.Ellipsis;   // Steam 名可能很长
        }
    }

    /// <summary>右上齿轮：点击开全局设置面板（LobbySettingsButton 运行时补 Button 接 SettingsPanel.Toggle）。</summary>
    static void WireSettingsGear(Transform band)
    {
        Transform gear = band.Find("Icon_Gear");
        if (gear == null) { Debug.LogWarning("[LobbyUI] 找不到 Icon_Gear —— 设置入口没接"); return; }

        var raw = gear.GetComponent<RawImage>();
        if (raw != null) raw.raycastTarget = true;

        if (gear.GetComponent<LobbySettingsButton>() == null)
            gear.gameObject.AddComponent<LobbySettingsButton>();
        else
            Debug.Log("[LobbyUI] Icon_Gear 已经有 LobbySettingsButton，跳过");
    }

    // ── 分层 + 子全屏弹窗（2026-09-27）──────────────────────────────────────────
    // 用户：①「左上角和右上角的显示是在那些全屏显示的弹窗界面中仍显示在屏幕上（除非明确说明隐藏这些）的」
    //      ②「现在做战斗的子全屏弹窗，通用背景，先生成一个通用的叉ui图标，用于关闭弹窗」
    //
    // 层级（Canvas 下的同级先后顺序，**不嵌套 Canvas、不加第二套 raycaster**）：
    //   …  Ref_Backdrop / Bg_v2 / Entry_* / Entry_BottomRow      大厅本体
    //      CornerRow_v1                                          右下角入口条
    //      Layer_Sub_v1                                          全屏子弹窗层：Popup_Placeholder / Panel_Battle
    //      Layer_Hud_v1                                          **常驻 HUD**：Plate_Profile（左上）+ Plate_TopBand（右上）
    // UGUI 同层里后面的画在上面 ⇒ HUD 层是 Canvas 最后一个子物体 = 永远压在所有弹窗之上
    // = 那半句「仍显示在屏幕上」。例外那半句由 LobbySubPanel.hideHudOnOpen 承担（勾上才藏）。
    // 两块 HUD 板是从 LobbyUI_v1 里**搬**过来的：锚 / 轴都还钉在屏幕角、父级仍是全屏框 —— 位置一个数都没动。
    // 右下角入口条**没有**放进 HUD：用户只点名了左上与右上（要一起常驻就说一声，搬进去是两行）。
    const string SubLayerName = "Layer_Sub_v1";
    const string HudLayerName = "Layer_Hud_v1";
    const string CommonBgPath = "Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png";
    static readonly Vector2 AnchorBL = new Vector2(0f, 0f);   // 屏幕左下角（左下角那行常驻 ID 用）
    const string CloseIconPath = UiDir + "Icon_Close.png";
    const string CloseIconHoverPath = UiDir + "Icon_CloseHover.png";

    // 子全屏弹窗的版式：内容左右留白 64（让开通用背景自带那圈内缩 46 的金细框）
    //                   内容顶距屏幕 128（让开右上横栏那 95）—— 关闭叉与标题同一行，都在 128
    const float SubPanelPadX = 64f;
    const float SubPanelTop = 128f;
    // 2026-09-27：关闭叉改到右上角**邮件那一格**（用户「叉ui改到右上角和邮箱位置一样」）——
    // 锚右上（AnchorTR + PivotTL），与 Plate_TopBand / Icon_Mail 同一坐标系：
    //   横栏 left = 屏右 - 538，邮件 left = 横栏 left + 370 = 屏右 - 168、top = 屏顶 - 66、60x60
    const float SubPanelCloseX = -168f;
    const float SubPanelCloseY = -66f;
    const float SubPanelCloseSize = 60f;

    [MenuItem("Tools/异界/大厅：分层（HUD 常驻层 + 全屏子弹窗层）")]
    public static void ApplyUiLayers()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 层级已就位：… / " + CornerRootName + " / " + SubLayerName + "（全屏子弹窗）/ " + HudLayerName +
                  "（常驻 HUD = 左上 Plate_Profile + 右上 Plate_TopBand，最后画、永远在最上面）。");
    }

    /// <summary>建 / 取两个层，并把顺序钉死成「… / Layer_Sub_v1 / Layer_Hud_v1(最后)」，同时把两块 HUD 板与弹窗归位。</summary>
    static void EnsureUiLayers(Canvas canvas, out Transform sub, out Transform hud)
    {
        sub = EnsureLayer(canvas.transform, SubLayerName, "新建全屏子弹窗层");
        hud = EnsureLayer(canvas.transform, HudLayerName, "新建常驻 HUD 层");

        hud.SetAsLastSibling();                     // HUD 永远最后 → 画在最上面
        sub.SetSiblingIndex(Mathf.Max(0, hud.GetSiblingIndex() - 1));

        GameObject ui = GameObject.Find(RootName);
        if (ui != null)
        {
            Reparent(ui.transform, hud, "Plate_Profile");
            Reparent(ui.transform, hud, "Plate_TopBand");

            Transform popup = ui.transform.Find("Popup_Placeholder");
            if (popup != null) popup.SetParent(sub, false);
        }

        // 占位弹窗以前被提到 Canvas 下过，这里也收进子弹窗层（SetAsLastSibling 只在本层里抬，压不到 HUD）
        var lp = Object.FindObjectOfType<LobbyPopup>(true);
        if (lp != null && lp.transform.parent != sub) lp.transform.SetParent(sub, false);

        Reparent(canvas.transform, hud, "Plate_Profile");     // 已经在 HUD 里的就不动
        Reparent(canvas.transform, hud, "Plate_TopBand");
    }

    static Transform EnsureLayer(Transform parent, string name, string undoName)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;

        RectTransform rt = NewRect(parent, name, Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Undo.RegisterCreatedObjectUndo(rt.gameObject, undoName);
        return rt;
    }

    /// <summary>把 from 下叫 name 的子物体搬到 to 下（锚 / 轴都不动，位置不变）。没有就跳过。</summary>
    static void Reparent(Transform from, Transform to, string name)
    {
        if (from == null || to == null) return;
        Transform t = from.Find(name);
        if (t == null || t.parent == to) return;
        t.SetParent(to, false);
    }

    [MenuItem("Tools/异界/大厅：生成战斗子全屏弹窗（通用背景 + 通用关闭叉）")]
    public static void BuildBattleSubPanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);
        GameObject panel = BuildSubPanel(sub, hud.gameObject, "Panel_Battle", "战斗", true, false);
        // ⚠ 重建会把旧的 LobbySubPanel 组件销毁 —— 入口板上那条 subPanel 引用会被序列化成 null，
        //   点击就退回占位弹窗（2026-09-27 用户实测踩到）。所以重建后必须由**本菜单自己**把引用接回去。
        WireBattleEntryToPanel(canvas, panel);
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 已生成 Panel_Battle（全屏子弹窗）：通用背景 CommonBack_A_clean + 通用关闭叉（Icon_Close，右上角邮件格，锚右上 / 位置 SubPanelCloseX/Y / 尺寸 SubPanelCloseSize）；" +
                  "本面板不出标题与提示（withHeader:false）；内容区 = Body_Content（只限位、不画东西）；" +
                  "hideOnOpen 自动填「无底衬的 HUD 图标」（好友 / 商城 / 活动 / 教程 / 邮件），开面板时临时藏、关时还原；" +
                  "要让这个面板把 HUD 也一起藏掉就把 LobbySubPanel 的 hideHudOnOpen 勾上。");
    }

    /// <summary>把「战斗」入口板重接到子全屏弹窗 —— **不重建**面板，只修那条 null 引用。
    /// （重建面板后入口板会指向已销毁的旧组件，点击就退回占位弹窗；那条路径见 BuildBattleSubPanelMenu。）</summary>
    [MenuItem("Tools/异界/大厅：把「战斗」入口板重接到子全屏弹窗（修点到占位弹窗）")]
    public static void RewireBattleEntryMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }
        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);
        GameObject panel = BuildSubPanel(sub, hud.gameObject, "Panel_Battle", "战斗", false, false);   // rebuild:false = 已有就复用
        WireBattleEntryToPanel(canvas, panel);
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
    }
    // ── 左下角常驻玩家 ID（2026-09-27 用户：「左下角会常态以小字显示自己id」）────────────
    const string IdTagName = "Text_PlayerId";
    const float  IdTagX = 64f;        // 距左边框
    const float  IdTagY = 26f;        // 距下边框
    const float  IdTagFont = 24f;
    const string IdTagPrefix = "ID  ";
    const string IdTagToastName = "Text_CopyToast";
    const string IdTagToastMsg  = "已复制到剪贴板";
    const float  IdTagToastX = 380f;      // 提示语距 ID 行左端
    const float  IdTagToastW = 520f;
    static readonly Color IdTagColor      = new Color32(240, 232, 210, 140);   // 常态：奶油 #F0E8D2 · 55%
    static readonly Color IdTagHoverColor = new Color32(252, 246, 228, 205);   // 悬停：只微亮一档，不换色相
    static readonly Color IdTagToastColor = new Color32(228, 203, 132, 255);   // 提示语：本套亮金 #E4CB84

    const string OtherPanelName = "Panel_Other";

    /// <summary>「其它」子全屏弹窗：与 Panel_Battle 同一个壳（通用背景 + 通用关闭叉），
    /// 内容只有一张模式卡「离线模式」—— 点它直接进 Game 场景（离线 Host + AI 对手）。
    /// 用户 2026-09-27：「现在做其它，和战斗点开几乎一模一样，只是目前只有一个『离线模式』点击后直接跳转到 Game 场景」。</summary>
    [MenuItem("Tools/异界/大厅：生成「其它」子全屏弹窗（离线模式）")]
    public static void BuildOtherSubPanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);
        // 壳与 Panel_Battle 完全一致（withHeader:false = 不出标题 / 占位提示）
        GameObject panel = BuildSubPanel(sub, hud.gameObject, OtherPanelName, "其它", true, false);
        BattleModeCardsBuilder.BuildOfflineCard(panel);
        WireEntryToPanel(canvas, "Entry_More", panel, "其它");
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 已生成 Panel_Other（全屏子弹窗）：壳与 Panel_Battle 一致（CommonBack_A_clean + 通用关闭叉），" +
                  "内容 = 一张「离线模式」模式卡（BattleModeCardsBuilder.BuildOfflineCard）；Entry_More（其它）已接到它。");
    }


    /// <summary>把某块入口板的点击目标接到子全屏弹窗上（重建面板后必调 —— 见 BuildBattleSubPanelMenu 里的注释）。</summary>
    static void WireEntryToPanel(Canvas canvas, string entryName, GameObject panel, string title)
    {
        if (canvas == null || panel == null) return;
        var sub = panel.GetComponent<LobbySubPanel>();
        Transform entry = FindDeep(canvas.transform, entryName);
        if (entry == null) { Debug.LogWarning("[LobbyUI] 找不到 " + entryName + " —— 入口板的点击没接上子全屏弹窗"); return; }
        var hover = entry.GetComponent<LobbyPlateHover>();
        if (hover == null) { Debug.LogWarning("[LobbyUI] " + entryName + " 上没有 LobbyPlateHover —— 点击没接上"); return; }
        hover.subPanel = sub;
        hover.popup = null;          // 有子全屏弹窗就别再退回占位弹窗
        hover.title = title;
        EditorUtility.SetDirty(hover);
        Debug.Log("[LobbyUI] " + entryName + " → " + panel.name + " 的点击引用已重接（原来指向已被销毁的旧组件时会是 null）。");
    }

    static void WireBattleEntryToPanel(Canvas canvas, GameObject panel)
    {
        WireEntryToPanel(canvas, "Entry_Battle", panel, "战斗");
    }

    /// <summary>那几个**没有底衬**、直接压在墙上的 HUD 图标 —— 开子全屏弹窗时临时藏掉。</summary>
    static readonly string[] NoBackdropHudIcons = { "Icon_Friend", "Icon_Shop", "Icon_Event", "Icon_Tutorial", "Icon_Mail" };

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static GameObject[] FindNoBackdropHudIcons(GameObject hudLayer)
    {
        var list = new List<GameObject>();
        if (hudLayer == null) return list.ToArray();
        foreach (string n in NoBackdropHudIcons)
        {
            Transform t = FindDeep(hudLayer.transform, n);
            if (t != null) list.Add(t.gameObject);
            else Debug.LogWarning("[LobbyUI] HUD 层里找不到 " + n + " —— hideOnOpen 会少一个");
        }
        return list.ToArray();
    }

    /// <summary>左下角那行常驻小字：显示自己的「异界号」；点击复制到剪贴板 + 右侧弹一句提示；悬停微亮。
    /// 挂进 HUD 层（Canvas 最后一层 ⇒ 全屏子弹窗压不住它）；文字内容运行时由 <see cref="LobbyPlayerIdTag"/>
    /// 从 SteamDataManager 现取。幂等：已有就只改位置 / 字号 / 颜色 / 补提示语子物体。</summary>
    [MenuItem("Tools/异界/大厅：加左下角常驻玩家 ID")]
    public static void AddPlayerIdTagMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);

        Transform t = hud.Find(IdTagName);
        TextMeshProUGUI text;
        if (t == null)
        {
            RectTransform rt = NewRect(hud, IdTagName, AnchorBL, AnchorBL, new Vector2(IdTagX, IdTagY), new Vector2(600f, 34f));
            Undo.RegisterCreatedObjectUndo(rt.gameObject, "加左下角玩家 ID");
            text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            text = t.GetComponent<TextMeshProUGUI>();
            if (text == null) text = t.gameObject.AddComponent<TextMeshProUGUI>();
            Undo.RecordObject(text, "左下角玩家 ID");
            var rt = (RectTransform)t;
            rt.anchorMin = AnchorBL; rt.anchorMax = AnchorBL; rt.pivot = AnchorBL;
            rt.anchoredPosition = new Vector2(IdTagX, IdTagY);
            rt.sizeDelta = new Vector2(600f, 34f);
        }

        text.font = _font;
        text.text = "";                                   // 运行时由 LobbyPlayerIdTag 填
        text.fontSize = IdTagFont;
        text.color = IdTagColor;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = true;                        // 要接悬停 / 点击（只是显示，所以以前关掉了）

        // 提示语：ID 行的子物体（跟着一起走）；raycastTarget = false —— 别把父物体的悬停 / 点击抢走
        TextMeshProUGUI toast = null;
        Transform tt = text.transform.Find(IdTagToastName);
        if (tt == null)
        {
            RectTransform trt = NewRect(text.transform, IdTagToastName, AnchorBL, AnchorBL,
                                        new Vector2(IdTagToastX, 0f), new Vector2(IdTagToastW, 34f));
            Undo.RegisterCreatedObjectUndo(trt.gameObject, "加复制提示语");
            toast = trt.gameObject.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            toast = tt.GetComponent<TextMeshProUGUI>();
            if (toast == null) toast = tt.gameObject.AddComponent<TextMeshProUGUI>();
            Undo.RecordObject(toast, "复制提示语");
            var trt = (RectTransform)tt;
            trt.anchorMin = AnchorBL; trt.anchorMax = AnchorBL; trt.pivot = AnchorBL;
            trt.anchoredPosition = new Vector2(IdTagToastX, 0f);
            trt.sizeDelta = new Vector2(IdTagToastW, 34f);
        }
        toast.font = _font;
        toast.text = IdTagToastMsg;                       // 编辑器里能看清写的是什么；运行时 alpha 归 0 后才显示
        toast.fontSize = IdTagFont;
        toast.color = new Color(IdTagToastColor.r, IdTagToastColor.g, IdTagToastColor.b, 0f);
        toast.alignment = TextAlignmentOptions.MidlineLeft;
        toast.enableWordWrapping = false;
        toast.overflowMode = TextOverflowModes.Overflow;
        toast.raycastTarget = false;

        var tag = text.GetComponent<LobbyPlayerIdTag>();
        if (tag == null) tag = text.gameObject.AddComponent<LobbyPlayerIdTag>();
        tag.label = text;
        tag.prefix = IdTagPrefix;
        tag.pending = "";
        tag.normalColor = IdTagColor;
        tag.hoverColor = IdTagHoverColor;
        tag.toastText = toast;
        tag.toastMessage = IdTagToastMsg;
        tag.toastColor = IdTagToastColor;
        tag.toastOffsetX = IdTagToastX;
        EditorUtility.SetDirty(tag);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 左下角常驻玩家 ID 已就位：" + HudLayerName + "/" + IdTagName +
                  "（锚左下角 · 距边 " + IdTagX + "/" + IdTagY + " · 字号 " + IdTagFont + "）。" +
                  "点击 = 复制 ID 到剪贴板 + 右侧弹「" + IdTagToastMsg + "」；悬停微亮。提示语 = 它的子物体 " + IdTagToastName + "。" +
                  "位置 / 字号 / 颜色改本方法顶上的 IdTag* 常量。");
    }


    /// <summary>通用子全屏弹窗的壳：全屏通用背景 +（可选标题 / 提示）+ 右上角通用关闭叉 + 留给内容的大框。</summary>
    static GameObject BuildSubPanel(Transform parent, GameObject hudLayer, string name, string title, bool rebuild = false, bool withHeader = true)
    {
        Transform old = parent.Find(name);
        if (old != null && !rebuild) return old.gameObject;   // 幂等：已经有的不重建（免得冲掉以后往里放的内容）
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        RectTransform root = NewRect(parent, name, Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        var panel = root.gameObject.AddComponent<LobbySubPanel>();

        // 通用背景：**保持拉伸锚**（非等比拉伸正是这张图的设计前提，见 common-bg-v1/README）
        // raycastTarget 开着 = 整层挡点击，面板开着时下面的入口板点不到
        RawImage bg = NewRaw(root, "Bg", CommonBgPath, Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = Vector2.zero;
        bg.rectTransform.offsetMax = Vector2.zero;
        bg.raycastTarget = true;

        // 2026-09-27：withHeader = false 就不出标题 / 占位提示（用户「左上角的战斗占位测试文字删掉」）。
        if (withHeader)
        {
            panel.titleText = NewLabel(root, "Text_Title", title, new Vector2(SubPanelPadX, -SubPanelTop), new Vector2(720f, 64f), 48f);
            TextMeshProUGUI hint = NewLabel(root, "Text_Hint", "占位 · 内容待接入", new Vector2(SubPanelPadX, -SubPanelTop - 78f), new Vector2(720f, 34f), 24f);
            hint.color = new Color(240f / 255f, 232f / 255f, 210f / 255f, 0.62f);
        }

        // 留给真实内容的大框（只限位、自己不画东西 —— 用法同 Entry_BottomRow）
        RectTransform body = NewRect(root, "Body_Content", Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(SubPanelPadX, 64f);
        body.offsetMax = new Vector2(-SubPanelPadX, -(SubPanelTop + 132f));

        // 通用关闭叉：Icon_Close.png（Tools/cardframe/LobbyUIv1.ps1 出）+ 悬停换贴图 + Button → Close()
        RawImage close = NewRaw(root, "Btn_Close", CloseIconPath, AnchorTR, PivotTL,
                                new Vector2(SubPanelCloseX, SubPanelCloseY), new Vector2(SubPanelCloseSize, SubPanelCloseSize));
        var hover = close.gameObject.AddComponent<LobbyIconHover>();
        hover.icon = close;
        hover.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CloseIconPath);
        hover.hoverTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CloseIconHoverPath);
        hover.popup = null;      // 叉子不弹占位窗
        hover.title = "";
        if (hover.hoverTexture == null) Debug.LogWarning("[LobbyUI] 找不到 " + CloseIconHoverPath + " —— 关闭叉没有悬停态");

        var btn = close.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = close;
        UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(panel.Close));

        panel.hudLayer = hudLayer;
        panel.hideHudOnOpen = false;      // 默认：HUD 压在面板之上、一直可见
        // 开窗时临时藏掉那几个**没有底衬**的 HUD 件（好友 / 商城 / 活动 / 教程 / 邮件）；
        // 有底衬的（头像板 / 横栏 / 货币 / 齿轮 / 右下角入口条）照旧常驻 —— 用户 2026-09-27。
        panel.hideOnOpen = FindNoBackdropHudIcons(hudLayer);

        root.SetAsLastSibling();
        // **存成 active**：在编辑器里就能直接看到版式、拖里面的东西；运行时由 LobbySubPanel.Start 的
        // closeOnStart 自己关掉（Start 在第一帧渲染前跑，不会闪一下），再由入口板 Open。
        panel.closeOnStart = true;
        root.gameObject.SetActive(true);
        return root.gameObject;
    }


    // ── 好友侧边栏（2026-09-27）────────────────────────────────────────────────
    // 用户：「现在做好友侧边栏展示，点击好友后从屏幕左侧滑出（速度较快）一个侧边栏，大概到左上角那个
    //       图案的右边缘，再次点击好友或者点击侧边栏之外的区域会滑动回去」；随后「不遮挡左上角的组件，
    //       以及下面的 id」→ 澄清「不是不贴边，而是在它们层级之下」。
    //
    // 几何：板是**满高**的浮层（贴屏幕上沿 / 左沿 / 下沿），宽 FriendsPanelW = 左上头像衬托板
    //       LobbyProfilePlate 的右沿（它贴图右缘 1396 / 3 = 465.3 → 465）。底图 LobbyFriendPanel.png
    //       就按屏幕 465x1080 定尺出（贴图 1395x3240，3 倍口径）。
    // 层级：「不遮挡左上角组件 + 左下 ID 行」**不靠躲**，靠层级 —— 本面板挂 Layer_Sub_v1，而
    //       Layer_Hud_v1（头像板 + 横栏 + 五个压墙图标 + 左下那行常驻 ID）是 Canvas 最后一个子物体，
    //       永远画在它之上。好友图标也因此能在侧边栏开着时再点一次把它关掉。
    //       FriendsPanelTop / Bottom 是留给日后微调的「上下让开量」，现在都是 0（满高）。
    const string FriendsPanelName = "Panel_Friends";
    const string FriendsPanelTex = UiDir + "LobbyFriendPanel.png";
    const float FriendsPanelW = 465f;
    const float FriendsPanelTop = 0f;
    const float FriendsPanelBottom = 0f;

    /// <summary>把 Selectable 的键盘 / 手柄导航关掉（这几个 Button 只是「吃掉点击」用的，不该参与 Tab 导航）。</summary>
    static Navigation NoNav(Navigation nav)
    {
        nav.mode = Navigation.Mode.None;
        return nav;
    }

    /// <summary>好友侧边栏：点好友图标从屏幕左侧滑出，再点一次 / 点面板以外滑回去。
    /// 场景里存成 active（方便拖版式），运行时由 LobbyFriendPanel.Start 的 closeOnStart 自己关掉。</summary>
    [MenuItem("Tools/异界/大厅：生成好友侧边栏（点击好友从左侧滑出）")]
    public static void BuildFriendsPanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);

        Transform old = sub.Find(FriendsPanelName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        // ── 根：铺满全屏 + 一张全透明 Image（raycastTarget 开着才吃得到点击；α=0 也挡，Unity 不看 α）──
        RectTransform root = NewRect(sub, FriendsPanelName, Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        var panel = root.gameObject.AddComponent<LobbyFriendPanel>();

        // 全透明 Image：raycastTarget 开着才吃得到点击（Unity 不看 α）。它就是「面板以外」那块。
        var blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;
        // 用内置 Button 接「点面板以外 = 关」——**别用自定义的第二个 MonoBehaviour**：
        // 同一个 .cs 里除文件名那个类，Unity 都序列化不了（2026-09-27 实测存成 missing script）。
        var blockerButton = root.gameObject.AddComponent<Button>();
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.targetGraphic = blocker;
        blockerButton.navigation = NoNav(blockerButton.navigation);
        UnityEventTools.AddPersistentListener(blockerButton.onClick, new UnityAction(panel.Close));

        // ── 板身：满高、贴屏幕左沿；只有横向参与滑动 ──
        RectTransform body = NewRect(root, "Body", new Vector2(0f, 0f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        body.anchorMin = new Vector2(0f, 0f);
        body.anchorMax = new Vector2(0f, 1f);
        body.pivot = new Vector2(0f, 0.5f);
        body.offsetMin = new Vector2(0f, FriendsPanelBottom);
        body.offsetMax = new Vector2(FriendsPanelW, -FriendsPanelTop);
        panel.body = body;
        panel.width = FriendsPanelW;

        var bg = body.gameObject.AddComponent<RawImage>();
        bg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FriendsPanelTex);
        if (bg.texture == null) Debug.LogWarning("[LobbyUI] 找不到贴图：" + FriendsPanelTex);
        bg.raycastTarget = true;                 // 点在板上不该穿到整屏挡板
        // 板身也挂一个 Button，但**不挂任何监听**：它只是把点击吃掉，免得冒泡到根节点那个 Button 把面板关了。
        var bodyButton = body.gameObject.AddComponent<Button>();
        bodyButton.transition = Selectable.Transition.None;
        bodyButton.targetGraphic = bg;
        bodyButton.navigation = NoNav(bodyButton.navigation);

        // ── 内容：标题 + 金细线 + 列表位（好友系统还没做，先一句「暂无好友」）──
        // 标题不摆在面板左上角，而是**紧挨好友图标右边**（用户 2026-09-27：「左上角的好友字改为这个地方，
        // ui图标右边一点」）—— 图标本身在 Plate_Profile 局部 (160,-57) 46x46，所以 x 从 160+46+16 = 222 起；
        // 竖向对齐图标中线（y = 57+23 = 80）。这段在头像板下沿之下，不会被那块板压住。
        const float FriendIconRight = 160f + 46f;
        TextMeshProUGUI title = NewLabel(body, "Text_Title", "好友",
                                         new Vector2(FriendIconRight + 16f, -58f), new Vector2(200f, 44f), 30f);
        title.alignment = TextAlignmentOptions.Left;   // 中线左对齐 = 竖向居中
        title.color = Cream;

        var divider = NewRect(body, "Line_Divider", AnchorTL, PivotTL,
                              new Vector2(28f, -104f), new Vector2(FriendsPanelW - 56f, 2f)).gameObject.AddComponent<Image>();
        divider.color = new Color32(200, 164, 74, 132);           // 本套金 #C8A44A · 52%
        divider.raycastTarget = false;

        TextMeshProUGUI empty = NewLabel(body, "Text_Empty", "暂无好友", new Vector2(0f, -487f), new Vector2(FriendsPanelW, 40f), 24f);
        empty.alignment = TextAlignmentOptions.Center;
        empty.color = new Color32(142, 162, 180, 170);            // 钢 #8EA2B4 · 67%
        empty.raycastTarget = false;

        // ── 名单：容器 + 行模板（2026-09-27：好友表接实际数据）────────────────────────
        // 数据链：SteamFriendSource（互为 Steam 好友 + 正在玩 / 一起玩过）→ FriendFilterChain
        //         → FriendListService（并上手动加的游戏内好友、排序、20 秒重扫）→ 本 List 克隆行。
        // 改造位：拿到 Steam Web API Key 后只要装 FriendEvidence.OwnedLookup（GetOwnedGames），
        //         「拥有即加」自动生效 —— 这里和 UI 都不用动。
        RectTransform list = NewRect(body, "List", AnchorTL, PivotTL,
                                     new Vector2(28f, -124f), new Vector2(FriendsPanelW - 56f, 900f));

        // 行模板：挂在 List 下、**存成 inactive**（运行时克隆 + Bind，真名单里一行一个）
        const float RowH = 84f;
        float rowW = FriendsPanelW - 56f;
        var rowRT = NewRect(list, "RowTemplate", AnchorTL, PivotTL, Vector2.zero, new Vector2(rowW, RowH));
        var row = rowRT.gameObject.AddComponent<FriendRowUI>();

        // 整行一块悬停底（常态 α=0 → 悬停淡金）：走内置 Button 的 ColorTint，不写代码
        var rowBg = rowRT.gameObject.AddComponent<Image>();
        rowBg.color = new Color32(200, 164, 74, 0);
        rowBg.raycastTarget = true;
        var rowBtn = rowRT.gameObject.AddComponent<Button>();
        rowBtn.transition = Selectable.Transition.ColorTint;
        rowBtn.targetGraphic = rowBg;
        rowBtn.navigation = NoNav(rowBtn.navigation);
        var rowColors = rowBtn.colors;
        rowColors.normalColor = new Color32(200, 164, 74, 0);
        rowColors.highlightedColor = new Color32(200, 164, 74, 20);
        rowColors.pressedColor = new Color32(200, 164, 74, 40);
        rowColors.selectedColor = new Color32(200, 164, 74, 0);
        rowColors.disabledColor = new Color32(200, 164, 74, 0);
        rowColors.fadeDuration = 0.06f;
        rowBtn.colors = rowColors;

        // 头环 + 井里头像：井的口径 = LobbyAvatarRing.png 264 里井半径 102（直径 204）→ 72 上 55.6 → 取 56
        RawImage rowRing = NewRaw(rowRT, "Avatar_Ring", UiDir + "LobbyAvatarRing.png",
                                 AnchorTL, PivotTL, new Vector2(0f, -6f), new Vector2(72f, 72f));
        rowRing.raycastTarget = false;
        var avatarRT = NewRect(rowRT, "Avatar_Image", AnchorTL, PivotTL, new Vector2(8f, -14f), new Vector2(56f, 56f));
        var rowAvatar = avatarRT.gameObject.AddComponent<RawImage>();
        rowAvatar.texture = null;                       // 运行时填：先灰盘，Steam 头像到货自己换
        rowAvatar.raycastTarget = false;
        avatarRT.SetSiblingIndex(rowRing.transform.GetSiblingIndex() + 1);

        TextMeshProUGUI rowName = NewLabel(rowRT, "Text_Name", "名字",
                                          new Vector2(88f, -14f), new Vector2(rowW - 96f, 40f), 26f);
        rowName.alignment = TextAlignmentOptions.Left;
        rowName.color = Cream;
        TextMeshProUGUI rowStatus = NewLabel(rowRT, "Text_Status", "在线",
                                            new Vector2(88f, -50f), new Vector2(rowW - 96f, 30f), 20f);
        rowStatus.alignment = TextAlignmentOptions.Left;
        rowStatus.color = new Color32(142, 162, 180, 225);   // 钢 #8EA2B4（运行时按状态改色）

        var rowLine = NewRect(rowRT, "Line_Row", AnchorTL, PivotTL,
                              new Vector2(0f, -83f), new Vector2(rowW, 1f)).gameObject.AddComponent<Image>();
        rowLine.color = new Color32(200, 164, 74, 90);       // 比面板那条分隔线更淡
        rowLine.raycastTarget = false;

        row.avatarImage = rowAvatar;
        row.nameText = rowName;
        row.statusText = rowStatus;
        rowRT.gameObject.SetActive(false);                   // 模板自己藏着，只给克隆用

        var listUI = list.gameObject.AddComponent<LobbyFriendListUI>();
        listUI.rowTemplate = row;
        listUI.emptyText = empty;                            // 空表时那句「暂无好友」

        // 服务挂在大厅 Canvas 上（本菜单会反复重建 Panel_Friends，服务别跟着一起没）
        if (canvas.gameObject.GetComponent<FriendListService>() == null)
            canvas.gameObject.AddComponent<FriendListService>();

        // ── 接到好友图标（左上头像板下沿那颗）：点击不再弹占位窗，改成开 / 关这个侧边栏 ──
        Transform friend = FindDeep(hud, "Icon_Friend");
        if (friend == null) Debug.LogWarning("[LobbyUI] HUD 层里找不到 Icon_Friend —— 侧边栏没有入口");
        else
        {
            var hover = friend.GetComponent<LobbyIconHover>();
            if (hover == null) hover = friend.gameObject.AddComponent<LobbyIconHover>();
            hover.friendsPanel = panel;
            hover.showMyId = false;              // 「我的 ID」那行退出这个入口；左下角常驻那行仍在
            EditorUtility.SetDirty(hover);
        }

        panel.closeOnStart = true;
        root.SetAsLastSibling();
        root.gameObject.SetActive(true);

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 已生成 " + FriendsPanelName + "（好友侧边栏）：屏幕 " + FriendsPanelW + " x 1080 满高浮层" +
                  "（距上沿 " + FriendsPanelTop + " / 下沿 " + FriendsPanelBottom + "，让开量留给日后微调）+ " +
                  "整屏透明挡板（点它收回去）；好友图标 Icon_Friend 已改接它（再点一次也收回去）。" +
                  "宽 = Plate_Profile 右沿，底图 " + FriendsPanelTex + " 由 Tools/cardframe/LobbyUIv1.ps1 出。");
    }
}
