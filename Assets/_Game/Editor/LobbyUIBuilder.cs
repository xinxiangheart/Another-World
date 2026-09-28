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
        // 纯衬托底图：Unity 的命中**不看 alpha**，整块矩形都会吃点击（2026-09-28 实测：不关它会压掉
        // 下层好友侧边栏表头那颗「+」Icon_FriendPlus —— 44x44 只有探出板下沿的 6px 能点）。
        profile.raycastTarget = false;
        NewRaw(profile.rectTransform, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL, new Vector2(36f, -2f), new Vector2(88f, 88f));
        NewLabel(profile.rectTransform, "Text_PlayerName", "名字", new Vector2(184f, -12f), new Vector2(240f, 44f), 26f);
        RawImage iconFriend = NewRaw(profile.rectTransform, "Icon_Friend", UiDir + "Icon_LobbyFriend.png", AnchorTL, PivotTL, new Vector2(160f, -57f), new Vector2(46f, 46f));

        // ── 右上：横栏 + 两个货币 + 齿轮；栏下商城 / 活动 / 教程（无底板），整簇锚屏幕右上角 ──
        RawImage band = NewRaw(hudLayer, "Plate_TopBand", UiDir + "LobbyBandRight.png", AnchorTR, PivotTL, new Vector2(-538f, 0f), new Vector2(538f, 95f));
        band.raycastTarget = false;   // 同上：横栏也是衬托底图，开着会吃掉下层那几个叉上面约 29px
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

        // 两个货币（图标 + 数字）是**纯显示**：4 件一律关掉 raycastTarget —— NewRaw / NewLabel 默认开着（Unity 组件默认值），
        // 开着就会把下层子弹窗右上角那几个「叉 / 返回」按到只剩一条边（2026-09-28 命中体检实测：CloseBtn / ReturnButton）。
        foreach (string cn in new[] { "Icon_Coin", "Icon_Ticket" })
        {
            Transform ct = band.rectTransform.Find(cn);
            if (ct != null) { var ci = ct.GetComponent<RawImage>(); if (ci != null) ci.raycastTarget = false; }
        }
        foreach (string tn in new[] { "Text_Coin", "Text_Ticket" })
        {
            Transform tt = band.rectTransform.Find(tn);
            if (tt != null) { var tmp = tt.GetComponent<TMPro.TextMeshProUGUI>(); if (tmp != null) tmp.raycastTarget = false; }
        }

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
    // ═════════════════════════════════════════════════════════════════════════
    // 卡牌总览（2026-09-28）—— 左边一栏级联筛选 + 右边卡牌网格 + 右下角 n/总数
    // ═════════════════════════════════════════════════════════════════════════
    // 用户原话：「做卡牌总览，点击进入一个类似于作战的全屏，左边一栏（宽度大概为好友宽度的 3/4），右边是
    //   比较大的卡牌预览，大概一排 4-5 个（具体看效果）……右下角显示数量/卡牌总数」。
    //
    // 几何（屏幕 px · 1920x1080）—— 运行时的 LobbyCardCollectionPanel 照抄同一组数：
    //   · 面板挂在 Layer_Sub_v1（HUD 层是 Canvas 最后一个子物体，永远画在上面 ⇒ 左上 / 右上常驻）。
    //   · 内容不用 BuildSubPanel 给的 Body_Content（它是「子全屏弹窗留内容」的通用框，上下内缩很多），
    //     而是在面板根上照好友详情那套「两条金线 + 左右两块」自己定边 —— 口径与 Fd* 一致：
    //       横金线 y = SubPanelTop + 24 = 152（在通用关闭叉 66..126 下面）；竖金线 x = SubPanelPadX + LcRailW = 413。
    //   · 左栏 = 64 .. 413（宽 349 = 好友侧边栏 465 x 3/4，取整）；右栏 = 413 .. 1856。
    //   · 右栏再内缩 LcViewInset = 32（不贴金线）后才是滚动视口。
    //   · ⚠ StretchRect(父, 名, offsetMin, offsetMax) 的 offsetMax 是**距右沿 / 距上沿**，
    //     一律写负数；写成正数会把这件推到父级之外（2026-09-28 二改修的三处）。
    const string  LcPanelName   = "Panel_Cards";
    const string  LcLineTopName = "Line_CardsTop";
    const string  LcLineLeftName= "Line_CardsLeft";
    const string  LcRailName    = "Rail_Filter";
    const string  LcViewportName= "Body_Cards";
    const string  LcCounterName = "Text_CardCount";

    const float LcPadBottom = 64f;                                     // = SubPanelPadX
    const float LcLineTopY  = SubPanelTop + 24f;                       // 152
    const float LcRailW     = 349f;                                    // 465 x 3/4 取整
    const float LcLineLeftX = SubPanelPadX + LcRailW;                  // 413
    const float LcChipW     = 108f;
    const float LcChipH     = 52f;
    const float LcChipGap   = 12f;                                     // 3 x 108 + 2 x 12 = 348 <= 349
    const float LcChipRow   = 60f;                                     // 行距（52 高 + 8）
    const float LcChipFont  = 20f;                                     // 只作记录：**实际字号在 LobbyCardCollectionPanel**（3 字 20 / 4 字 16）
    const float LcLv0Y      = 20f;                                     // 第 0 栏上沿（面板根左上角往下）
    const float LcLv1Y      = 96f;                                     // 第 1 栏上沿（召唤物 / 法术 —— 常驻）
    const float LcLv2Y      = 176f;                                    // 第 2 栏上沿（子集：英雄..特殊 / 普通..反制）
    const float LcLv3Y      = 256f;                                    // 第 3 栏上沿（费数 / 特性）—— 子集不再和第 1 栏挤同一栏
    const float LcViewInset = 32f;                                     // 视口距右栏左右沿
    const float LcViewGapTop= 48f;                                     // 视口上沿距横金线（152 -> 200）
    const float LcViewBottom= 92f;                                     // 视口下沿（给右下角那行 n/总数 + 间隔）
    // ── 右栏卡牌网格（2026-09-28 三改：照旧卡牌总览 CardCollectionPanel 的取值）────────
    //    用户「一排最多4-5张，参考之前的卡牌总览怎么实现的」→ 5 张一排 / cardScale 2 / 间距 25 /
    //    卡面 = 预制体自然尺寸 83.33 x 146.33（不是 64x84：那个尺寸会把卡框压扁）。
    //    块宽 = 5 x 83 x 2.5 + 4 x 25 x 2.5 = 1292，在 1378 的视口里居中（左右各 43）。
    //    这里只给「运行时重建之前的占位尺寸」，真正的摆位在 LobbyCardCollectionPanel.RebuildCards。
    const int   LcColumns   = 5;
    const float LcCellW     = 270f;                                    // (83 + 25) x 2.5
    const float LcCellH     = 427.5f;                                  // (146 + 25) x 2.5
    const float LcGridW     = 1378f;                                   // = 视口宽（内容容器铺满视口，卡片在其中居中）
    const float LcCounterFont = 26f;
    const float LcCounterRx  = 40f;                                    // 计数行右沿距屏幕右沿
    const float LcCounterRy  = 40f;                                    // 计数行下沿距屏幕下沿

    static readonly Color LcLineColor = new Color32(200, 164, 74, 90); // 金细线（与好友详情那两条同α档）

    [MenuItem("Tools/异界/大厅：生成卡牌总览子全屏弹窗（左栏级联筛选 + 右卡牌网格）")]
    public static void BuildCardsSubPanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);
        GameObject panel = BuildSubPanel(sub, hud.gameObject, LcPanelName, "卡牌总览", true, false);
        BuildCardsContent(panel);

        // ⚠ 重建会把旧的 LobbySubPanel 组件销毁 —— 入口板上那条 subPanel 引用会被序列化成 null，
        //   点击就退回占位弹窗。所以重建后必须由**本菜单自己**把引用接回去（同 BuildBattleSubPanelMenu）。
        WireEntryToPanel(canvas, "Entry_Cards", panel, "卡牌总览");
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 已生成 " + LcPanelName + "（卡牌总览）：通用背景 + 通用关闭叉 + 左栏 " + LcRailW +
                  " 宽级联筛选（LobbyChip_Filter 三态）+ 竖金线 + 右栏卡牌网格（一排 " + LcColumns + " 个）+ 右下角 n/总数；" +
                  "骨架由本菜单建，筛选 / 过滤 / 生成卡牌归 LobbyCardCollectionPanel（随面板一起是 active 的，运行时由入口板 Open）。");
    }

    /// <summary>卡牌总览的内容骨架：两条金线 + 左栏（四个筛选容器）+ 右栏（滚动视口 + 卡牌容器 + 计数）。</summary>
    /// <remarks>幂等：先把旧的四个件收掉再建（重跑本菜单不叠）。骨架建好后把引用填进
    /// <see cref="LobbyCardCollectionPanel"/>；筛选格与卡牌本体是运行时按选择现建的。</remarks>
    static void BuildCardsContent(GameObject panel)
    {
        if (panel == null) return;
        Transform bg = panel.transform.Find("Bg");
        int at = bg != null ? bg.GetSiblingIndex() + 1 : 0;

        string[] olds = { LcLineTopName, LcLineLeftName, LcRailName, LcViewportName, LcCounterName };
        for (int i = 0; i < olds.Length; i++)
        {
            Transform o = panel.transform.Find(olds[i]);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        // ① 横金线：从「左栏左沿」一路到屏幕右沿内缩 —— 只跨右栏（左栏上面是第 0 栏「全部」）
        //    锚上沿（y 钉死、x 拉伸），否则纯色方块会被沿拉伸轴拉成一整块金色大板（见 StretchRect 的坑）。
        // ⚠ 锚必须是**满跨**（anchorMin.x = 0 / anchorMax.x = 1），左端用 offsetMin.x 往里收；
        //   把 anchorMin.x 直接写成 413/1919 那种「半跨锚」会让 offset 语义变成相对锚片（实测横线被拉到 61 万 px，
        //   Stage83 抓到）。好友详情那条 Line_DividerTop 就是满跨锚 + offsetMin.x = FdPadX，照抄它。
        var lineTop = StretchRect(panel.transform, LcLineTopName, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                  new Vector2(LcLineLeftX, -LcLineTopY),
                                  new Vector2(-SubPanelPadX, -LcLineTopY + 2f)).gameObject.AddComponent<Image>();
        lineTop.color = LcLineColor;
        lineTop.raycastTarget = false;
        lineTop.transform.SetSiblingIndex(at);

        // ② 竖金线：从横金线一路到下沿（把左栏与右栏分开）
        // ⚠ offsetMax 的那两个数是「距右沿 / 距上沿」，**必须是负的**：竖线原来写 +LcLineTopY，
        //   于是线顶跑到面板上沿之上 152（用户那张图里线一直伸进顶栏），LcLineTopY 处反倒没有端点。
        var lineLeft = StretchRect(panel.transform, LcLineLeftName, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                   new Vector2(LcLineLeftX, LcPadBottom),
                                   new Vector2(LcLineLeftX + 2f, -LcLineTopY)).gameObject.AddComponent<Image>();
        lineLeft.color = LcLineColor;
        lineLeft.raycastTarget = false;
        lineLeft.transform.SetSiblingIndex(at + 1);

        // ③ 左栏：四个容器，竖着排（第 0 栏 = 全部 / 第 1 栏 = 召唤物·法术 / 第 2 栏 = 类别 / 第 3 栏 = 费数·特性）
        // ⚠ 用「钉左沿」的锚（anchorMin.x = anchorMax.x = 0），offsetMax.x 才是「距左沿」；
        //   满跨锚下 offsetMax.x 会被读成「距右沿」、offsetMax.y 为正则栏顶跑到面板之上（筛选格压顶栏）。
        RectTransform rail = StretchRect(panel.transform, LcRailName,
                                         new Vector2(0f, 0f), new Vector2(0f, 1f),
                                         new Vector2(SubPanelPadX, LcPadBottom),
                                         new Vector2(SubPanelPadX + LcRailW, -LcLineTopY));
        rail.SetSiblingIndex(at + 2);
        RectTransform lv0 = NewRect(rail, "Chips_Lv0", AnchorTL, PivotTL, new Vector2(0f, -LcLv0Y), new Vector2(LcRailW, LcChipH));
        RectTransform lv1 = NewRect(rail, "Chips_Lv1", AnchorTL, PivotTL, new Vector2(0f, -LcLv1Y), new Vector2(LcRailW, LcChipH));
        RectTransform lv2 = NewRect(rail, "Chips_Lv2", AnchorTL, PivotTL, new Vector2(0f, -LcLv2Y), new Vector2(LcRailW, LcChipH));         // 一格（3 个一行）
        RectTransform lv3 = NewRect(rail, "Chips_Lv3", AnchorTL, PivotTL, new Vector2(0f, -LcLv3Y), new Vector2(LcRailW, LcChipH * 5f));   // 5 行：主动退场那栏是第 4 行

        // ④ 右栏：滚动视口（RectMask2D 裁剪 + 隐藏式纵向滚动条）
        // ⚠ 上沿 = -(LcLineTopY + LcViewGapTop) = -200（横金线 152 再往下 48）。
        //   原来写的是 +（LcLineTopY - LcViewGapTop）= +104 ⇒ 视口顶跑到画布上沿之上 104，
        //   于是第一行卡被画在画布外、遮罩也裁不到顶部（用户那张图最上面那行就是这么来的）。
        RectTransform body = StretchRect(panel.transform, LcViewportName,
                                         new Vector2(LcLineLeftX + LcViewInset, LcViewBottom),
                                         new Vector2(-SubPanelPadX - LcViewInset, -(LcLineTopY + LcViewGapTop)));
        body.SetSiblingIndex(at + 3);
        body.gameObject.AddComponent<RectMask2D>();
        // 右栏「整框可拖」：视口自己不是 Graphic、不参与射线，所以鼠标按在**卡片之间的空隙 / 卡片区以外的空白**
        // 时命中的是面板底图（Bg），事件从 Bg 往上冒泡到面板 —— 那条链上没有 ScrollRect ⇒ 拖不动。
        // 补一张**全透明**的 Image 当拖动面即可：射线不看 alpha（只有 alphaHitTestMinimumThreshold 才看），
        // 它挂在视口上、画在自己的子物体之下 ⇒ 卡片照旧优先命中，空白处由它接手、冒泡到同一个 ScrollRect。
        var dragSurface = body.gameObject.AddComponent<Image>();
        dragSurface.color = new Color(0f, 0f, 0f, 0f);
        dragSurface.raycastTarget = true;

        var scroll = body.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        RectTransform content = NewRect(body, "Grid_Cards", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        Vector2.zero, new Vector2(LcGridW, LcCellH));
        scroll.viewport = body;
        scroll.content = content;

        Scrollbar bar = BuildCardsScrollbar(body, at + 4);
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarSpacing = 0f;

        // ⑤ 右下角计数行：n / 卡牌总数（锚右下，位置写死 —— 与视口下沿同一条水平线附近）
        TextMeshProUGUI counter = NewLabel(panel.transform, LcCounterName, "0 / 0",
                                           Vector2.zero, new Vector2(260f, 36f), LcCounterFont);
        RectTransform crt = counter.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);
        crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(-LcCounterRx, LcCounterRy);
        counter.alignment = TextAlignmentOptions.BottomRight;
        counter.color = new Color32(228, 203, 132, 236);   // 本套亮金
        counter.raycastTarget = false;
        counter.transform.SetSiblingIndex(at + 5);

        // ⑥ 接线
        var comp = panel.GetComponent<LobbyCardCollectionPanel>();
        if (comp == null) comp = panel.AddComponent<LobbyCardCollectionPanel>();
        comp.railRoot = rail;
        comp.chipLv0 = lv0;
        comp.chipLv1 = lv1;
        comp.chipLv2 = lv2;
        comp.chipLv3 = lv3;
        comp.cardScroll = scroll;
        comp.gridRoot = content;
        comp.counterText = counter;
        comp.font = _font != null ? _font : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        EditorUtility.SetDirty(comp);
    }

    /// <summary>右侧那条细金滚动条（轨道 + 手柄），与整套同源：平底 + 一条金细线，不做倒角。</summary>
    static Scrollbar BuildCardsScrollbar(RectTransform viewport, int sibling)
    {
        Transform old = viewport.Find("Scroll_Cards");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        RectTransform root = NewRect(viewport, "Scroll_Cards", new Vector2(1f, 0f), new Vector2(1f, 1f),
                                     new Vector2(10f, 0f), new Vector2(6f, 0f));
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 1f);
        root.offsetMin = new Vector2(-10f, 0f);
        root.offsetMax = new Vector2(-4f, 0f);
        root.SetSiblingIndex(sibling);

        var track = root.gameObject.AddComponent<Image>();
        track.color = new Color32(6, 9, 14, 150);          // 墨，半透明
        track.raycastTarget = true;

        RectTransform sliding = NewRect(root, "SlidingArea", PivotC, PivotC, Vector2.zero, Vector2.zero);
        sliding.anchorMin = Vector2.zero;
        sliding.anchorMax = Vector2.one;
        sliding.offsetMin = Vector2.zero;
        sliding.offsetMax = Vector2.zero;

        RectTransform handle = NewRect(sliding, "Handle", PivotC, PivotC, Vector2.zero, Vector2.zero);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = new Color32(200, 164, 74, 170);  // 金

        var sb = root.gameObject.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.targetGraphic = handleImg;
        sb.handleRect = handle;
        return sb;
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

    const string RoomPanelName = "Panel_Room";

    // ── 「房间」面板的内容（2026-09-27 二改：点房间直接就是建房界面，那张模式卡撤掉）──────
    // 用户原话：「点击房间就直接进入了创建房间的功能，不需要再次点击，加入房间的功能内嵌在这个总房间功能里，
    //   在现在这个创建房间和好友侧边栏中间靠上边区域是显示房主头像和名称，下面那个是显示加入玩家头像和名称，
    //   把现在这个创建房间的卡牌隐藏掉，另外右上角的叉左边显示：房间号：xxxxxx，悬停变色点击会在下面浮现：已复制到剪切板」
    const string RoomPlayersName = "RoomPlayers";
    const float  RoomSlotX     = 640f;    // 槽列左沿 —— 好友侧边栏右沿 465 与屏幕中心 960 之间
    const float  RoomHostTop   = -220f;   // 房主槽上沿（距面板上沿）
    const float  RoomGuestTop  = -420f;   // 加入玩家槽上沿
    const float  RoomRingSize  = 160f;    // 头像环贴图尺寸（LobbyAvatarRing.png 的比例：环 88 : 井 68）
    const float  RoomWellSize  = 124f;    // 环里那口头像井的直径
    const float  RoomNameX     = 180f;    // 名字 / 身份距槽左沿
    const float  RoomNameFont  = 40f;
    const float  RoomRoleFont  = 26f;
    const string RoomCodeName      = "Text_RoomCode";
    const string RoomCodeToastName = "Text_CopyToast";
    const float  RoomCodeFont      = 30f;
    // 框是**锚右上 + 轴也右上**（BuildRoomCode 传的是 pivot (1,1)）⇒ RoomCodeX 量的是**右沿**距屏幕右沿的距离：
    //   右沿 = 1919 − 252 = 1667（正好停在「加入房间」图标 1675 前 8）
    //   左沿 = 1667 − 238 = 1429 = 板左沿（1381）+ JoinSidePadX 48 ⇒ 整行落在板里
    // （2026-09-27 七改：原本 420 宽的框左探到 1247、压住板左沿 —— 用户「其它超出组件向右平移到内部」。
    //   注意别把它当成「左沿距右沿」：−490 会把整行推到板外。）
    const float  RoomCodeX     = -252f;
    const float  RoomCodeY     = -66f;    // 与关闭叉同一条水平线（框高 60 ⇒ 中线 -96 = 叉的中线）
    const float  RoomCodeW     = 238f;
    const float  RoomCodeH     = 60f;
    const float  RoomCodeToastFont = 24f;
    const float  RoomCodeToastY    = -60f;   // 提示语：紧贴在号那行下面
    static readonly Color RoomRoleColor    = new Color32(240, 232, 210, 140);   // 身份小字：奶油 55%
    static readonly Color RoomPendingColor = new Color32(142, 162, 180, 205);   // 「等待加入…」：本套钢色
    // ── 房间里的新件（2026-09-27 三改）：踢出 / 开始游戏 / 屏幕中央上方那行提示 ──────
    // 用户原话：「在房主自己视角里加入玩家头像左边做一个踢出的按钮（仅在有玩家在房间中显示），当房间内加入
    //   玩家后在双方头像中央靠右一点出现一个开始游戏（只有房主能点，客人视角里虽然出现但是字体颜色是灰色的，
    //   房主视角是白色悬停点击时变金色）……另外若房主点击右上角的叉会退出房间并将房主转交给客人……同时屏幕
    //   中央上方弹出提示：房主已离开，你已成为房主，客人点击叉就单纯离开房间了，同时弹出提示：玩家xxxx离开」
    const string RoomKickName  = "Btn_Kick";
    const float  RoomKickRight = -20f;    // 相对加入玩家槽左沿（环左沿 = 640）再往左让 20
    const float  RoomKickY     = -80f;    // 与环的中线同高
    const float  RoomKickW     = 96f;     // = 子背景 LobbyChip_Kick 的屏幕宽
    const float  RoomKickH     = 48f;     // = 子背景的屏幕高
    const float  RoomKickFont  = 30f;

    const string RoomStartName = "Btn_StartGame";
    const float  RoomStartX    = 820f;    // = 环列右沿 800 再让 20（与「踢出」那 20 对称），也正好落在名字 / 身份那列的左沿（RoomSlotX 640 + RoomNameX 180）
    const float  RoomStartY    = -400f;   // 两条槽的正中（房主环心 -300 与客人环心 -500 的中点）
    const float  RoomStartW    = 212f;    // = 子背景 LobbyChip_Start 的屏幕宽
    const float  RoomStartH    = 64f;
    const float  RoomStartFont = 42f;

    // ── 「加入房间」（2026-09-27 用户：「在叉ui左边做一个大小一样的加入房间的简单ui，button」）──
    //    紧挨通用关闭叉的左边、同尺寸 60x60 —— 位置正好压在「教程」那一格上（面板开着时那几个
    //    无底衬 HUD 图标本来就藏起来了，见 BuildSubPanel 的 hideOnOpen），所以重叠不影响观感。
    const string RoomJoinName  = "Btn_JoinRoom";
    const float  RoomJoinX     = SubPanelCloseX - SubPanelCloseSize - 16f;   // -244：叉的左沿（-228）再让 16
    const float  RoomJoinY     = SubPanelCloseY;                            // 与叉同一条水平线（-66）
    const string RoomJoinIcon  = "Icon_LobbyJoin.png";
    const string RoomJoinHover = "Icon_LobbyJoinHover.png";

    // ── 「加入房间」右侧侧边栏（2026-09-27 四改）────────────────────────────────
    // 用户原话：「加入房间是一个右侧侧边栏（到顶，但不需要完全到底），范围到右上角的左边，不遮挡房间号，
    //   ui 等，再次点击（或者点击范围外）滑动回去，先是显示在右边房间号和 ui 下面的加入房间四个字，然后金线
    //   分割一下，下面是一个输入框，再下面是输入后的预览，主要是展示搜索目标的头像/名称，其下面是人数 1/2
    //   或者红色的 2/2，然后数字右边是加入（有子背景）（根据是否满人为白色（可变金色）或者红色）」
    // 几何（2026-09-27 七改 —— 用户：「缩到和右上角顶端一样位置不变作为其背景」「好友侧边栏不是一样
    //   的要求吗，和右上角最左侧持平」「其它超出组件向右平移到内部」）：
    //   **板 = 右上横栏（Plate_TopBand / LobbyBandRight，538×95）那块的背景** —— 和好友侧边栏
    //   （宽 = 左上 Plate_Profile 的宽度、齐屏幕左沿 / 上沿）同一套口径：
    //     · 上沿 = 屏幕顶（0，与横栏同顶；横栏画在 Layer_Hud_v1 ⇒ 永远压在板顶那条之上）
    //     · 左沿 = 横栏左沿（1920 − 538 = 1382）⇒ 板宽 538、右沿贴屏幕右沿（= 横栏右沿）
    //     · 下沿让开 160（「不需要完全到底」）⇒ 板 538×920
    //   右上角那行（房间号 / 加入 / 叉）靠**层级**压在板之上（本板插在 Text_RoomCode **之前**，见
    //   BuildRoomSubPanelMenu）；房间号那个 420 宽的框原本探出板左沿，已右移进板内（RoomCodeX/W）。
    //   底图 LobbyJoinSidebar.png（Tools/cardframe/LobbyJoinSidebarV1.ps1 出，1614x2760 = 538x920 x3）。
    const string JoinSideName    = "Panel_JoinRoom";
    const string JoinSideTex     = UiDir + "LobbyJoinSidebar.png";
    const float  JoinSideW       = 538f;   // 左边缘（距屏幕右沿）= 右上横栏的左沿（= 它的宽度）
    // 上沿：**屏幕顶**（与横栏同顶 ⇒ 板就是横栏那块的背景）。
    const float  JoinSideTop     = 0f;
    // 内容**位置不变**：Content 那一层仍从**屏幕顶**下来这么多（= 上一版的板顶），与 JoinSideTop 无关。
    const float  JoinSideHead    = 126f;
    // 右沿：贴屏幕右沿（= 横栏右沿）。
    const float  JoinSideInset   = 0f;
    const float  JoinSideBottom  = 160f;
    const float  JoinSidePadX    = 48f;
    const float  JoinTitleFont   = 40f;
    const float  JoinLineY       = 118f;    // 标题下面那条金细线
    const float  JoinInputTop    = 158f;    // 输入框上沿（距板顶）
    const float  JoinInputH      = 76f;
    const string JoinInputName   = "Input_RoomCode";
    const float  JoinPrevTop     = 280f;    // 预览区上沿
    const float  JoinRingSize    = 96f;
    const float  JoinWellSize    = 74f;     // 环 88 : 井 68 的比例（96 → 74.2）
    const string JoinBtnName     = "Btn_Join";
    const string JoinStatusName  = "Text_JoinStatus";
    const float  JoinStatusFont  = 24f;
    static readonly Color JoinWellColor = new Color32(12, 17, 26, 235);    // 输入框那口井：面板渐变的暗端 #0C111A
    static readonly Color JoinLineColor = new Color32(200, 164, 74, 132);  // 金 #C8A44A · 52%（与好友侧边栏那条同值）
    static readonly Color JoinFullRed   = new Color32(182, 72, 72, 255);   // 满员：生命 #B64848

    const string RoomToastName = "Text_LobbyToast";
    const float  RoomToastY    = -150f;   // 屏幕中央上方（压到 -240 会正好叠在房主那行的名字上）
    const float  RoomToastW    = 1200f;
    const float  RoomToastH    = 64f;
    const float  RoomToastFont = 34f;

    /// <summary>客人视角那条「开始游戏」的灰（钢 #8EA2B4 压一档 —— 与 MatchConfirmPanel 的 frameLocked 同值）。</summary>
    static readonly Color RoomStartOffColor = new Color32(110, 119, 131, 255);

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

    /// <summary>「房间」子全屏弹窗：壳与 Panel_Battle / Panel_Other 完全一致，**唯一差别 = 左上角那个
    /// 「好友」图标不再临时藏掉** —— 用户 2026-09-27：「现在做房间，也是类似的全屏，不过左上角的
    /// 好友不再隐藏，并且能在这个界面打开好友侧边栏」。所以 hideOnOpen 走排除版：只藏 商城 / 活动 /
    /// 教程 / 邮件，好友留在屏幕上，点它照旧开左侧好友侧边栏（LobbyFriendPanel）。
    /// 面板内容**直接就是建房界面**（点房间不再需要二次点击）—— 两个玩家槽 + 右上角那行可复制的房间号
    /// + 关闭叉左边那个「加入房间」图标（2026-09-27）。
    [MenuItem("Tools/异界/大厅：生成「房间」子全屏弹窗（创建房间）")]
    public static void BuildRoomSubPanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);
        GameObject panel = BuildSubPanel(sub, hud.gameObject, RoomPanelName, "房间", true, false);
        // ★ 与战斗 / 其它唯一的差别就在这一行：好友（Icon_Friend）点名排除，其余无底衬 HUD 件照旧临时藏。
        //   侧边栏本身挂 Layer_Sub_v1 且 Open 时 SetAsLastSibling ⇒ 画在本面板之上；
        //   而 HUD 层（头像板 / 横栏 / 好友图标 / 左下 ID 行）永远在本层之上，所以图标点得到。
        panel.GetComponent<LobbySubPanel>().hideOnOpen = FindNoBackdropHudIcons(hud.gameObject, "Icon_Friend");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        // 面板内容就是建房界面本身：两个玩家槽（房主 / 加入玩家）+ 右上角那行房间号（可复制）。
        // 那张「创建房间」模式卡已撤（用户 2026-09-27：「把现在这个创建房间的卡牌隐藏掉」）——
        // 贴图与 BattleModeCardsBuilder.BuildRoomCard 都留着备用，只是这里不再挂。
        BuildRoomPlayers(panel);
        BuildRoomCode(panel);
        BuildRoomJoinButton(panel);                // 右上角：关闭叉左边的「加入房间」图标
        BuildRoomRuntime(panel, hud.gameObject);   // 状态机（含 Steam 接入）+ 三条点击 + 顶中提示
        Transform closeBtn = panel.transform.Find("Btn_Close");
        if (closeBtn != null) closeBtn.SetAsLastSibling();   // 叉子始终压在内容之上
        GameObject joinSide = BuildRoomJoinSidebar(panel);
        // ★ 板顶到屏幕顶之后，右上角那行（房间号 / 加入 / 叉）靠**层级**压在它之上（用户 2026-09-27：
        //   「上顶满的意思是像好友那样作为右上角和房间号 ui」）—— 所以插到 Text_RoomCode **之前**，
        //   而不再 SetAsLastSibling；板自己那张全屏透明底仍在最上层，点板以外照旧滑回去。
        if (joinSide != null)
        {
            Transform codeT = panel.transform.Find(RoomCodeName);
            if (codeT != null) joinSide.transform.SetSiblingIndex(codeT.GetSiblingIndex());
            else joinSide.transform.SetAsLastSibling();
        }
        WireEntryToPanel(canvas, "Entry_Room", panel, "房间");
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 已生成 Panel_Room（全屏子弹窗）：壳同 Panel_Battle（CommonBack_A_clean + 通用关闭叉），" +
                  "内容 = 建房界面本身（房主 / 加入玩家两个槽 + 右上角可复制的房间号 + 叉左边的「加入房间」图标），不再挂模式卡；Entry_Room（房间）已接到它；" +
                  "hideOnOpen 排除了 Icon_Friend —— 好友图标在本面板开着时**常驻**，点它开 / 关左侧好友侧边栏。");
    }

    /// <summary>「房间」面板里的两个玩家槽：房主（上）/ 加入玩家（下）。</summary>
    /// <remarks>头像环与左上角那块**同源**（同一张 LobbyAvatarRing.png，只是放大到 160；环 88 : 井 68 的比例不变）。
    /// 房主槽挂 <see cref="PlayerProfilePanel"/> —— 它运行时从 SteamDataManager 取**本机**头像与名字（建房的人就是房主），
    /// 所以场景里那两行只是占位字。
    /// 加入玩家槽现在只有空态（环 + 「等待加入…」）—— 等联机（Steam 大厅）接进来再填头像 / 名字。</remarks>
    static GameObject BuildRoomPlayers(GameObject panel)
    {
        Transform old = panel.transform.Find(RoomPlayersName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        RectTransform root = NewRect(panel.transform, RoomPlayersName, AnchorTL, PivotTL, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        // 排在 Bg 之后：背景之上、关闭叉之下（叉子由调用方 SetAsLastSibling 顶着）
        Transform bg = panel.transform.Find("Bg");
        if (bg != null) root.SetSiblingIndex(bg.GetSiblingIndex() + 1);

        BuildRoomSlot(root, "Slot_Host",  "房主",     "你自己",     RoomHostTop,  true);
        BuildRoomSlot(root, "Slot_Guest", "加入玩家", "等待加入…", RoomGuestTop, false);
        // 开始游戏：出现在**两个头像的正中再靠右一点**（用户原话），房里有人才显示 —— 显隐与「房主能点 / 客人是灰的」
        // 都在 LobbyRoomPanel.Refresh 里控。这里不接点击：目标是 LobbyRoomPanel 上的方法，等 BuildRoomRuntime 接。
        BuildTextChip(root, RoomStartName, "开始游戏", AnchorTL, new Vector2(0f, 0.5f),
                      new Vector2(RoomStartX, RoomStartY), new Vector2(RoomStartW, RoomStartH), RoomStartFont,
                      "LobbyChip_Start.png", "LobbyChip_StartHover.png");

        // 踢出：挂在**加入玩家槽**里、头像环的左边（用户：「加入玩家头像左边」）；只有房主 + 房里有人时才显示。
        Transform guestSlot = root.Find("Slot_Guest");
        if (guestSlot != null)
            BuildTextChip(guestSlot, RoomKickName, "踢出", AnchorTL, new Vector2(1f, 0.5f),
                          new Vector2(RoomKickRight, RoomKickY), new Vector2(RoomKickW, RoomKickH), RoomKickFont,
                          "LobbyChip_Kick.png", "LobbyChip_KickHover.png");

        return root.gameObject;
    }

    static void BuildRoomSlot(Transform parent, string name, string role, string nameText, float top, bool isSelf)
    {
        RectTransform slot = NewRect(parent, name, AnchorTL, PivotTL, new Vector2(RoomSlotX, top),
                                     new Vector2(RoomRingSize, RoomRingSize));

        RawImage ring = NewRaw(slot, "Avatar_Ring", UiDir + "LobbyAvatarRing.png",
                               AnchorTL, PivotTL, Vector2.zero, new Vector2(RoomRingSize, RoomRingSize));
        ring.raycastTarget = false;

        float inset = (RoomRingSize - RoomWellSize) * 0.5f;
        RectTransform well = NewRect(slot, "Avatar_Image", AnchorTL, PivotTL,
                                     new Vector2(inset, -inset), new Vector2(RoomWellSize, RoomWellSize));
        var avatar = well.gameObject.AddComponent<RawImage>();
        avatar.raycastTarget = false;   // 头像不吃点击
        // ⚠ 空 RawImage 默认画**纯白**一块（texture = null 时就是这么显示的）—— 空槽必须把 alpha 压到 0，
        //   否则「加入玩家」那口井是块白方块（2026-09-27 实测踩到）。以后有人加入时由联机侧给 texture 再调回不透明。
        if (!isSelf) avatar.color = new Color(1f, 1f, 1f, 0f);

        float midY = -RoomRingSize * 0.5f;                       // 环的中线
        TextMeshProUGUI label = NewLabel(slot, "Text_Name", nameText,
                                        new Vector2(RoomNameX, midY + 26f), new Vector2(360f, 52f), RoomNameFont);
        label.alignment = TextAlignmentOptions.Left;             // 中线左对齐 = 与头像竖向对齐
        label.color = isSelf ? Cream : RoomPendingColor;

        TextMeshProUGUI roleT = NewLabel(slot, "Text_Role", role,
                                        new Vector2(RoomNameX, midY - 34f), new Vector2(360f, 34f), RoomRoleFont);
        roleT.alignment = TextAlignmentOptions.Left;
        roleT.color = RoomRoleColor;

        if (isSelf)
        {
            var view = slot.gameObject.AddComponent<PlayerProfilePanel>();
            view.avatarImage = avatar;
            view.nameText = label;
            view.circularCrop = true;        // Steam 头像是方的 —— 不裁圆会把金环吃掉
        }
    }

    /// <summary>右上角关闭叉**左边**那行房间号 + 它的复制提示（<see cref="LobbyRoomCodeTag"/>）。</summary>
    /// <remarks>位置按关闭叉算：叉 = 锚右上 / 轴左上 / (-168,-66) / 60×60 ⇒ 左沿在距右沿 228 处；
    /// 号那行右沿取 -252（再让 24），框高 60、中线正好落在叉的中线 -96 上。
    /// 提示语是它的子物体（锚右上、y = RoomCodeToastY），所以「下面浮现」是跟着这行走的。</remarks>
    static GameObject BuildRoomCode(GameObject panel)
    {
        Transform old = panel.transform.Find(RoomCodeName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        RectTransform rt = NewRect(panel.transform, RoomCodeName, AnchorTR, new Vector2(1f, 1f),
                                   new Vector2(RoomCodeX, RoomCodeY), new Vector2(RoomCodeW, RoomCodeH));
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = "房间号：------";
        text.fontSize = RoomCodeFont;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.Right;    // 右对齐 ⇒ 紧挨关闭叉左边
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = true;                      // 它自己就是复制的点击目标

        RectTransform toast = NewRect(rt, RoomCodeToastName, AnchorTR, new Vector2(1f, 1f),
                                      new Vector2(0f, RoomCodeToastY), new Vector2(RoomCodeW, 40f));
        var toastText = toast.gameObject.AddComponent<TextMeshProUGUI>();
        toastText.font = _font;
        toastText.text = "";
        toastText.fontSize = RoomCodeToastFont;
        toastText.color = GoldBright;
        toastText.alignment = TextAlignmentOptions.Right;
        toastText.enableWordWrapping = false;
        toastText.overflowMode = TextOverflowModes.Overflow;
        toastText.raycastTarget = false;                // 别抢父物体那行的悬停 / 点击

        var tag = rt.gameObject.AddComponent<LobbyRoomCodeTag>();
        tag.label = text;
        tag.toastText = toastText;
        tag.toastOffsetY = RoomCodeToastY;
        tag.toastSlide = 12f;
        return rt.gameObject;
    }
    /// <summary>房间面板右上角的「加入房间」：紧挨关闭叉左边、同尺寸 60x60（用户 2026-09-27）。</summary>
    /// <remarks>跟商城 / 活动 / 教程那三个「压墙」图标一样：**不挂 Button** —— 悬停换贴图 + 点击弹占位弹窗
    /// 都走 <see cref="LobbyIconHover"/>（图标自己的 RawImage 就是 raycast 目标）。
    /// 图标 Icon_LobbyJoin.png（Tools/cardframe/LobbyIconJoinV1.ps1 出，与教程 / 叉同族：门框 + 进入箭头）。
    /// 位置（RoomJoinX = 叉左沿再让 16）正好压在「教程」那一格上 —— 房间面板开着时教程本来就是藏起来的。</remarks>
    static GameObject BuildRoomJoinButton(GameObject panel)
    {
        Transform old = panel.transform.Find(RoomJoinName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        RawImage join = NewRaw(panel.transform, RoomJoinName, UiDir + RoomJoinIcon, AnchorTR, PivotTL,
                               new Vector2(RoomJoinX, RoomJoinY), new Vector2(SubPanelCloseSize, SubPanelCloseSize));
        Undo.RegisterCreatedObjectUndo(join.gameObject, "建 " + RoomJoinName);

        var hover = join.gameObject.AddComponent<LobbyIconHover>();
        hover.icon = join;
        hover.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + RoomJoinIcon);
        hover.hoverTexture  = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + RoomJoinHover);
        hover.popup = Object.FindObjectOfType<LobbyPopup>(true);   // Popup_Placeholder（场景里是 inactive）
        hover.title = "加入房间";
        hover.hint  = "输入 6 位房间号加入好友的房（输入框下一步接）";
        if (hover.hoverTexture == null) Debug.LogWarning("[LobbyUI] 找不到 " + UiDir + RoomJoinHover + " ——「加入房间」没有悬停态");
        if (hover.popup == null) Debug.LogWarning("[LobbyUI] 场景里没有 LobbyPopup（Popup_Placeholder）——「加入房间」点了没反应");
        return join.gameObject;
    }

    /// <summary>一条 1px 金细线（输入框那圈线就是四条这个）。</summary>
    static Image Hairline(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        RectTransform rt = NewRect(parent, name, anchorMin, pivot, pos, size);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = JoinLineColor;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>「房间」面板里的「加入房间」右侧侧边栏（点右上角那个加入图标滑出 / 再点滑回）。</summary>
    /// <remarks>结构（自上而下 = 用户口径的顺序）：
    ///   加入房间（四个字）→ 金细线 → 输入框（平底凹井 + 四边金细线，满 6 位自动搜）→
    ///   预览（头像环 + 名称 + 人数 1/2 或红色的 2/2 + 数字右边的「加入」子背景按钮）。
    /// 滑动 / 点板以外滑回：LobbyJoinSidebar（与好友侧边栏同一套 progress 曲线，方向朝右）。
    /// 「加入」的底复用 LobbyChip_Kick（96x48 / 两个字 / 字号 30 —— 与「踢出」同规格，不另出一张）。
    /// 满员 = 生命红 #B64848 且点不动（走 Button 的 disabledColor）；不满 = 白、悬停金。</remarks>
    static GameObject BuildRoomJoinSidebar(GameObject panel)
    {
        Transform old = panel.transform.Find(JoinSideName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ── 根：铺满全屏 + 一张全透明 Image（raycastTarget 开着才吃得到点击 —— 它就是「板以外」那块）──
        RectTransform root = NewRect(panel.transform, JoinSideName, Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        var side = root.gameObject.AddComponent<LobbyJoinSidebar>();

        var blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;
        var blockerBtn = root.gameObject.AddComponent<Button>();
        blockerBtn.transition = Selectable.Transition.None;
        blockerBtn.targetGraphic = blocker;
        blockerBtn.navigation = NoNav(blockerBtn.navigation);
        UnityEventTools.AddPersistentListener(blockerBtn.onClick, new UnityAction(side.Close));

        // ── 板身：贴屏幕右沿，上 / 下都让开 ──
        RectTransform body = NewRect(root, "Body", new Vector2(1f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        body.anchorMin = new Vector2(1f, 0f);
        body.anchorMax = new Vector2(1f, 1f);
        body.pivot = new Vector2(1f, 0.5f);
        body.offsetMin = new Vector2(-JoinSideW, JoinSideBottom);
        body.offsetMax = new Vector2(-JoinSideInset, -JoinSideTop);   // 右沿缩进 JoinSideInset / 上沿 JoinSideTop
        side.body = body;
        side.width = JoinSideW;

        var bg = body.gameObject.AddComponent<RawImage>();
        bg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(JoinSideTex);
        if (bg.texture == null) Debug.LogWarning("[LobbyUI] 找不到贴图：" + JoinSideTex);
        bg.raycastTarget = true;
        var bodyBtn = body.gameObject.AddComponent<Button>();   // 只吃点击：免得冒泡到根节点那个「关」
        bodyBtn.transition = Selectable.Transition.None;
        bodyBtn.targetGraphic = bg;
        bodyBtn.navigation = NoNav(bodyBtn.navigation);

        // ── 内容层：板顶已经顶到屏幕顶，内容仍从 JoinSideHead 下来 —— **位置一个字都不动** ──
        RectTransform content = NewRect(body, "Content", Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = Vector2.zero;
        content.offsetMax = new Vector2(0f, -(JoinSideHead - JoinSideTop));   // 内容顶仍钉在屏幕顶下来 126

        float innerW = JoinSideW - JoinSideInset - JoinSidePadX * 2f;   // 板宽 640 - 左右各 48

        // ① 加入房间（四个字）
        TextMeshProUGUI title = NewLabel(content, "Text_Title", "加入房间", new Vector2(JoinSidePadX, -40f), new Vector2(innerW, 56f), JoinTitleFont);
        title.alignment = TextAlignmentOptions.Left;
        title.color = Cream;
        title.raycastTarget = false;

        // ② 金细线
        var line = NewRect(content, "Line_Divider", AnchorTL, PivotTL, new Vector2(JoinSidePadX, -JoinLineY), new Vector2(innerW, 2f)).gameObject.AddComponent<Image>();
        line.color = JoinLineColor;
        line.raycastTarget = false;

        // ③ 输入框：平底凹井 + 四边金细线（不加渐变、不加内阴影）
        RectTransform well = NewRect(content, JoinInputName, AnchorTL, PivotTL, new Vector2(JoinSidePadX, -JoinInputTop), new Vector2(innerW, JoinInputH));
        var wellImg = well.gameObject.AddComponent<Image>();
        wellImg.color = JoinWellColor;
        wellImg.raycastTarget = true;
        Hairline(well, "Line_Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero);
        Hairline(well, "Line_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), Vector2.zero);
        Hairline(well, "Line_Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f), Vector2.zero);
        Hairline(well, "Line_Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f), Vector2.zero);

        RectTransform area = NewRect(well, "Text Area", AnchorTL, PivotTL, new Vector2(24f, -14f), new Vector2(innerW - 48f, JoinInputH - 28f));
        area.gameObject.AddComponent<RectMask2D>();

        RectTransform textRT = NewRect(area, "Text", Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;
        var text = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.text = ""; text.fontSize = 34f; text.color = Cream;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow;

        RectTransform phRT = NewRect(area, "Placeholder", Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var ph = phRT.gameObject.AddComponent<TextMeshProUGUI>();
        ph.font = _font; ph.text = "输入 6 位房间号"; ph.fontSize = 30f;
        ph.alignment = TextAlignmentOptions.MidlineLeft; ph.raycastTarget = false;
        ph.enableWordWrapping = false; ph.overflowMode = TextOverflowModes.Overflow;
        ph.color = RoomPendingColor;

        var input = well.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = wellImg;
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = ph;
        input.characterLimit = 6;
        input.contentType = TMP_InputField.ContentType.Alphanumeric;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.restoreOriginalTextOnEscape = true;
        input.caretWidth = 2;
        input.customCaretColor = true;
        input.caretColor = Cream;
        input.selectionColor = new Color32(200, 164, 74, 90);
        UnityEventTools.AddPersistentListener(input.onValueChanged, new UnityAction<string>(side.OnCodeChanged));
        side.input = input;

        // 右键 = 把剪贴板里的房间号贴进来（用户 2026-09-27：「加入一个在输入栏右键自动粘贴复制的房间号功能」）。
        // TMP_InputField 只认左键（非左键第一行就 return），右键这一下得自己挂一个监听接。
        var paste = well.gameObject.AddComponent<LobbyJoinInputPaste>();
        paste.owner = side;

        // ④ 预览（搜到之前整组藏着）
        RectTransform prev = NewRect(content, "Core_Preview", AnchorTL, PivotTL, new Vector2(JoinSidePadX, -JoinPrevTop), new Vector2(innerW, JoinRingSize + 8f));
        side.previewGroup = prev.gameObject;

        RawImage ring = NewRaw(prev, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL, Vector2.zero, new Vector2(JoinRingSize, JoinRingSize));
        ring.raycastTarget = false;
        float inset = (JoinRingSize - JoinWellSize) * 0.5f;
        RectTransform wellRT = NewRect(prev, "Avatar_Image", AnchorTL, PivotTL, new Vector2(inset, -inset), new Vector2(JoinWellSize, JoinWellSize));
        var avatar = wellRT.gameObject.AddComponent<RawImage>();
        avatar.raycastTarget = false;
        avatar.color = new Color(1f, 1f, 1f, 0f);      // 空井不画白块（RawImage 无贴图默认纯白）
        side.avatarWell = avatar;

        float colX = JoinRingSize + 28f;
        TextMeshProUGUI who = NewLabel(prev, "Text_Name", "玩家", new Vector2(colX, -8f), new Vector2(innerW - colX, 44f), 32f);
        who.alignment = TextAlignmentOptions.Left;
        who.color = Cream;
        who.raycastTarget = false;
        side.nameText = who;

        TextMeshProUGUI cnt = NewLabel(prev, "Text_Count", "1/2", new Vector2(colX, -60f), new Vector2(140f, 40f), 30f);
        cnt.alignment = TextAlignmentOptions.Left;
        cnt.color = Cream;
        cnt.raycastTarget = false;
        side.countText = cnt;

        Button join = BuildTextChip(prev, JoinBtnName, "加入", AnchorTL, PivotTL,
                                    new Vector2(colX + 156f, -54f), new Vector2(96f, 48f), 30f,
                                    "LobbyChip_Kick.png", "LobbyChip_KickHover.png");
        var joinColors = join.colors;
        joinColors.disabledColor = JoinFullRed;        // 满员：红 + 点不动（用户口径）
        join.colors = joinColors;
        side.joinButton = join;
        UnityEventTools.AddPersistentListener(join.onClick, new UnityAction(side.OnJoinClicked));
        side.joinLabel = TextOf(join.transform, "Text_Label");

        TextMeshProUGUI status = NewLabel(content, JoinStatusName, "",
                                         new Vector2(JoinSidePadX, -(JoinPrevTop + JoinRingSize + 24f)),
                                         new Vector2(innerW, 40f), JoinStatusFont);
        status.alignment = TextAlignmentOptions.Left;
        status.color = RoomPendingColor;
        status.raycastTarget = false;
        side.statusText = status;

        prev.gameObject.SetActive(false);

        // 右上角那个「加入房间」图标：不再弹占位窗，改成开 / 关这个侧边栏
        var roomPanel = panel.GetComponent<LobbyRoomPanel>();
        if (roomPanel != null) roomPanel.joinSidebar = side;

        Transform joinIcon = panel.transform.Find(RoomJoinName);
        if (joinIcon != null)
        {
            var hover = joinIcon.GetComponent<LobbyIconHover>();
            if (hover != null) { hover.joinSidebar = side; hover.popup = null; EditorUtility.SetDirty(hover); }
        }
        else Debug.LogWarning("[LobbyUI] 找不到 " + RoomJoinName + " —— 侧边栏没有入口");

        side.closeOnStart = true;
        return root.gameObject;
    }


    /// <summary>「房间」面板的运行时状态机 + 三条点击的接点 + 屏幕中央上方那行提示。</summary>
    /// <remarks>三条点击（踢出 / 开始游戏 / 右上角的叉）都在这里接：目标方法是 <see cref="LobbyRoomPanel"/> 上的，
    /// 而那个组件是这里现加的 —— 不能像别的件那样在构造时顺手接。
    /// ⚠ 叉上原本那条 `Close()` 监听要**摘掉**：叉得先走房间逻辑（房主走 = 把房主让给客人；客人走 = 单纯离开），
    /// 再由房间逻辑自己调 `Close()`。</remarks>
    static LobbyRoomPanel BuildRoomRuntime(GameObject panel, GameObject hudLayer)
    {
        var room = panel.GetComponent<LobbyRoomPanel>();
        if (room == null) room = panel.AddComponent<LobbyRoomPanel>();
        room.shell = panel.GetComponent<LobbySubPanel>();

        // Steam 接入（2026-09-27）：状态机挂在同一个物体上；右上角那行房间号也连上 ——
        // 建房成功后 LobbyRoomSession 用大厅里的真号回填它（ApplyRealCode）。
        var session = panel.GetComponent<LobbyRoomSession>();
        if (session == null) session = panel.AddComponent<LobbyRoomSession>();
        room.session = session;
        Transform codeRow = panel.transform.Find(RoomCodeName);
        room.codeTag = codeRow != null ? codeRow.GetComponent<LobbyRoomCodeTag>() : null;

        Transform players = panel.transform.Find(RoomPlayersName);
        Transform host = players != null ? players.Find("Slot_Host") : null;
        Transform guest = players != null ? players.Find("Slot_Guest") : null;

        room.hostRoleText  = TextOf(host, "Text_Role");
        room.guestWell     = WellOf(guest);
        room.guestNameText = TextOf(guest, "Text_Name");
        room.guestRoleText = TextOf(guest, "Text_Role");
        room.hostWell     = WellOf(host);                   // 客人视角要把房主槽换成对方
        room.hostNameText = TextOf(host, "Text_Name");
        room.joinSidebar  = null;   // 真引用在 BuildRoomJoinSidebar 末尾回填（那时侧边栏才建出来）

        Transform kick = guest != null ? guest.Find(RoomKickName) : null;
        Transform start = players != null ? players.Find(RoomStartName) : null;
        room.kickGroup   = kick != null ? kick.gameObject : null;
        room.startGroup  = start != null ? start.gameObject : null;
        room.startButton = start != null ? start.GetComponent<Button>() : null;

        // 收尾那两件（拒绝回房间 / 双方确认进战斗加载）都在 HUD 层常驻，场景里各只有一份
        room.confirmPanel  = Object.FindObjectOfType<MatchConfirmPanel>();
        room.battleLoading = Object.FindObjectOfType<BattleLoadingScreen>();

        if (kick != null) WireClick(kick.GetComponent<Button>(), room.OnKickClicked);
        if (room.startButton != null) WireClick(room.startButton, room.OnStartGameClicked);
        Transform close = panel.transform.Find("Btn_Close");
        if (close != null) WireClick(close.GetComponent<Button>(), room.OnCloseClicked);

        EnsureLobbyToast(hudLayer);
        return room;
    }

    /// <summary>把一条 onClick 换成唯一一条持久监听（原来的先摘干净 —— 通用关闭叉上本来那条 Close 就是这么换的）。</summary>
    static void WireClick(Button btn, UnityAction action)
    {
        if (btn == null) return;
        while (btn.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(btn.onClick, 0);
        UnityEventTools.AddPersistentListener(btn.onClick, action);
    }

    /// <summary>文字按钮：颜色全交给 Button 的 ColorTint（TMP 自己设成纯白，别拿它自己的 color 去压）。</summary>
    /// <summary>「子背景 + 一行字」的小按钮（踢出 / 开始游戏）：底 = LobbyChip_*（平底 + 一条金细线），
    /// 字悬停 / 点击变金、底悬停提亮一档（两张底只差色调，切图不跳位）。</summary>
    /// <remarks>用户 2026-09-27：「踢出和开始游戏是有个子背景的」—— 与 MatchWait 那个「取消」的子背景同一套配方
    /// （Tools/cardframe/LobbyRoomChipV1.ps1 出图，屏幕 96x48 / 212x64）。点击归 Button（targetGraphic = 那行字），
    /// 悬停换底归 LobbyIconHover（挂在同一个物体上，popup 留空 → 它只管换贴图）。</remarks>
    static Button BuildTextChip(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot,
                                Vector2 pos, Vector2 size, float fontSize, string chipFile, string chipHoverFile)
    {
        RectTransform rt = NewRect(parent, name, anchor, pivot, pos, size);

        // 底先建（兄弟序在前 = 画在字下面），字后建
        RawImage plate = NewRaw(rt, "Chip", UiDir + chipFile, AnchorC, PivotC, Vector2.zero, size);
        plate.raycastTarget = false;

        TextMeshProUGUI label = NewLabel(rt, "Text_Label", text, Vector2.zero, size, fontSize);
        label.alignment = TextAlignmentOptions.Midline;   // 居中 = 正好压在子背景上
        label.color = Color.white;                        // 纯白 = 把颜色让给 Button 的 ColorTint
        Button btn = MakeTextButton(rt, label, Cream, GoldBright, RoomStartOffColor);

        var hover = rt.gameObject.AddComponent<LobbyChipHover>();
        hover.chip = plate;
        hover.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + chipFile);
        hover.hoverTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiDir + chipHoverFile);
        return btn;
    }

    static Button MakeTextButton(RectTransform rt, TextMeshProUGUI label, Color normal, Color hover, Color disabled)
    {
        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = label;
        var cb = btn.colors;
        cb.normalColor = normal;
        cb.highlightedColor = hover;      // 悬停 → 金
        cb.pressedColor = hover;          // 点击 → 金
        cb.selectedColor = normal;
        cb.disabledColor = disabled;      // 客人那条：灰
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        return btn;
    }

    /// <summary>屏幕中央上方那行一次性提示（房主转交 / 玩家离开）—— 挂 HUD 层，所以压在房间面板之上。</summary>
    static GameObject EnsureLobbyToast(GameObject hudLayer)
    {
        if (hudLayer == null) { Debug.LogWarning("[LobbyUI] 没有 HUD 层 —— 顶中那行提示没地方挂"); return null; }
        Transform old = hudLayer.transform.Find(RoomToastName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        RectTransform rt = NewRect(hudLayer.transform, RoomToastName, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   new Vector2(0f, RoomToastY), new Vector2(RoomToastW, RoomToastH));
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = "";
        text.fontSize = RoomToastFont;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        var toast = rt.gameObject.AddComponent<LobbyToast>();
        toast.text = text;
        return rt.gameObject;
    }

    static RawImage WellOf(Transform slot)
    {
        if (slot == null) return null;
        Transform t = slot.Find("Avatar_Image");
        return t != null ? t.GetComponent<RawImage>() : null;
    }

    static TextMeshProUGUI TextOf(Transform parent, string name)
    {
        if (parent == null) return null;
        Transform t = parent.Find(name);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
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

    /// <param name="except">要**排除**的名字 —— 房间面板传 "Icon_Friend"：好友图标在弹窗开着时照旧留在屏幕上
    /// （用户 2026-09-27：「现在做房间……不过左上角的好友不再隐藏」）。</param>
    static GameObject[] FindNoBackdropHudIcons(GameObject hudLayer, params string[] except)
    {
        var list = new List<GameObject>();
        if (hudLayer == null) return list.ToArray();
        foreach (string n in NoBackdropHudIcons)
        {
            if (except != null && System.Array.IndexOf(except, n) >= 0) continue;   // 点名留着的（房间面板留好友）
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

    // 好友表头右端那颗「+」：打开「好友详情」全屏子弹窗（2026-09-27 用户：「在好友右边靠近右边框的地方加上一个
    // 加号 ui 用于打开好友详情全屏（类似于战斗，但不要做卡牌的，我后续给你说怎么做）」）。
    const string FriendPlusName = "Icon_FriendPlus";
    const float  FriendPlusSize = 44f;     // 屏幕 44（贴图 256，方印 168/256 -> 印面 28.9；配方同通用关闭叉）
    const float  FriendPlusRight = 24f;    // 盒右沿距面板右边框（金细线右端让 28，这颗比它再靠里一点）
    const float  FriendPlusY = -58f;       // 与「好友」标题同一行（标题盒 -58..-102）
    const string FriendDetailPanelName = "Panel_FriendDetail";

    // ── 好友详情：左侧四个格子 + 两条金线 + 右侧大格子（2026-09-27）──────────────
    // 用户原话：「左边有四个格子类似于大厅右下角那四个不过更扁平，分别是好友列表，添加好友，申请列表，
    //   黑名单，初始进入位于好友列表格子，在右上角的叉下面划一道长金线横跨左右分割一下，然后左边也有
    //   从上到下的一个金线将四个从上到下的格子与右边空间分开得到右边一个独立的大格子，四个格子每个格子
    //   的内容都将不一样」。
    // 几何：竖栏 64..404（板 340 宽），四格在竖栏里**等分**（格高 = 竖栏/4、板 104 在格内居中）；
    //       竖金线 x = 428（= 板右沿 404 + 24）；横金线 y = -156（关闭叉占 -66..-126，这条在它下面）；
    //       横线横跨 64..屏幕右沿-64、竖线从上到下到距下沿 64 → 右边得到一块独立的大格子。
    //       两个金线、竖栏、右侧大格子**一律用拉伸锚 + offset 定边**（不写死 1920x1080）：
    //       四格的格心则按竖栏高度的 1/8、3/8、5/8、7/8 锚 —— 换分辨率不会散。
    // 贴图：板 1068x360（= 屏幕 340x104 + 每边 8 的透明边，Tools/cardframe/FriendDetailTabsV1.ps1）；
    //       三态三张、四格共用（板没有格与格的区别）；徽记另出 256 图标，运行时只改 alpha。
    const string FdRailName     = "Rail_Tabs";
    const string FdBodyName     = "Body_Detail";
    const string FdLineTopName  = "Line_DividerTop";
    const string FdLineLeftName = "Line_DividerLeft";
    const string FdTabTex       = UiDir + "LobbyFriendTab.png";
    const string FdTabHoverTex  = UiDir + "LobbyFriendTabHover.png";
    const string FdTabOnTex     = UiDir + "LobbyFriendTabOn.png";
    const float  FdPadX         = 64f;     // 左右让边（= SubPanelPadX）
    const float  FdPadBottom    = 64f;
    const float  FdLineTopY     = -156f;   // 横金线：距屏幕上沿 156（关闭叉底沿 126 之下）
    const float  FdTabW         = 340f;    // 比右下角那四块（300x120）更扁平：340x104 = 3.27:1（那条 2.5:1）
    const float  FdTabH         = 104f;
    const float  FdLineLeftGap  = 24f;     // 竖金线距板右沿
    const float  FdFontSize     = 28f;     // 与右下角入口条同号
    const float  FdLabelX       = 58f;     // 与入口板同口径：距板身左沿 58
    const float  FdLabelW       = 156f;    // 到徽记左边为止（徽记中心在板宽 75.5% 处）
    const float  FdEmblemSize   = 64f;     // 徽记显示尺寸（贴图 256）
    const float  FdEmblemCX     = 0.755f;  // 徽记中心：与入口板同位（板宽 75.5% / 板高 50%）
    const float  FdPadTex       = 8f;      // 贴图外框比板身每边多 8（贴图 PAD 24 / 3）
    static readonly Color FdLineColor = new Color32(200, 164, 74, 150);   // 金 #C8A44A · 59%
    static readonly string[] FdTabNames   = { "Friends", "Add", "Request", "Block" };
    static readonly string[] FdTabLabels  = { "好友列表", "添加好友", "申请列表", "黑名单" };
    static readonly string[] FdTabEmblems = { "Icon_FriendTabList", "Icon_FriendTabAdd", "Icon_FriendTabRequest", "Icon_FriendTabBlock" };

    // ── 好友列表那一格的内容（2026-09-27）──────────────────────────────────────────────
    // 用户：「好友列表（后续每个独立的玩家好友基本上都是按照这样）（上限50个好友），允许滑动，每个好友有个独立的
    //       长矩形子背景，从左到右分别是头像，名称，id（这个字体小一点），然后中间可以留空，右边分别是当前状态
    //       （在线/离线什么的），拉黑，删除，（仅在在线状态下）邀请……每个好友之间是有一点间隔，和底框也有间隔，
    //       右下角是以类似 23/50 小字这种形式展示好友数量」。
    // 版式（屏幕 px）与 Tools/cardframe/FriendDetailRowV1.ps1 里那组数**逐条对齐**，改一处要两边一起改。
    const string FdListName  = "Friends_List";
    const string FdCountName = "Text_Count";
    const string FdEmptyName = "Text_Empty";
    const string FdRowTex        = UiDir + "LobbyFriendRow.png";
    const string FdActBlockTex   = UiDir + "Icon_FriendActBlock.png";
    const string FdActBlockHov   = UiDir + "Icon_FriendActBlockHover.png";
    const string FdActDeleteTex  = UiDir + "Icon_FriendActDelete.png";
    const string FdActDeleteHov  = UiDir + "Icon_FriendActDeleteHover.png";
    const string FdActInviteTex  = UiDir + "Icon_FriendPlus.png";        // 邀请复用好友表头那颗「+」
    const string FdActInviteHov  = UiDir + "Icon_FriendPlusHover.png";
    const int    FdMaxFriends    = 50;      // 用户：上限 50 个好友
    const float  FdListPadX      = 32f;     // 列表左右让边（和底框的间隔）
    const float  FdListTop       = 124f;    // 列表上沿：让开标题
    const float  FdListBottom    = 60f;     // 列表下沿：给右下角那行「n/50」留位
    const float  FdRowW          = 1360f;   // 行宽（定宽，行底板按这个宽度出图，不跟着分辨率拉伸）
    const float  FdRowH          = 96f;
    const float  FdRowGap        = 12f;     // 行与行之间的间隔（用户：「一点间隔」）
    const float  FdRowPlatePad   = 8f;      // 板身之外那圈透明边（贴图 24 / 3）
    const float  FdRowRingX      = 14f;     // 头像环距行左沿
    const float  FdRowRingY      = 16f;     // 头像环距行上沿（环 64 在 96 高的行里竖直居中 → 中心 y=-48）
    const float  FdRowRingSize   = 64f;
    const float  FdRowAvatarInset = 7f;     // 井里头像比环每边小 7（块 50）—— 口径 264 里井半径 102
    const float  FdRowAvatarSize = 50f;
    const float  FdRowNameX      = 96f;
    const float  FdRowNameW      = 260f;
    const float  FdRowNameH      = 40f;    // ★ 必须 >= 字号 x 字体行高：CJK 字体 26 号实测 preferredH 37.4，
                                           //   盒子矮于它 + Ellipsis 溢出 = TMP 一个字符都不画（2026-09-27 实测踩到），
                                           //   所以名字那格不能用「刚好放下一行」的高度
    const float  FdRowNameFS     = 26f;
    const float  FdRowIdX        = 364f;    // 名称右边就是 id（用户：「从左到右……名称，id」）
    const float  FdRowIdW        = 336f;
    const float  FdRowIdH        = 40f;    // 与名字同高 -> 两列的光学中线一致
    const float  FdRowIdFS       = 20f;     // 用户：「这个字体小一点」
    const float  FdRowStatusR    = 1172f;   // 状态字右沿（再往右就是拉黑那格）
    const float  FdRowStatusW    = 260f;
    const float  FdRowStatusH    = 40f;    // 同上：三列同高
    const float  FdRowStatusFS   = 22f;
    const float  FdActSize       = 44f;     // 三格动作：与好友表头那颗「+」同尺寸
    const float  FdActY          = -26f;    // 44 高的盒在 96 高的行里竖直居中
    const float  FdActInviteX    = FdRowW - 20f - FdActSize;   // 1296（右让 20）
    const float  FdActDeleteX    = FdActInviteX - FdActSize - 12f;   // 1240
    const float  FdActBlockX     = FdActDeleteX - FdActSize - 12f;   // 1184
    const float  FdCountW        = 240f;
    const float  FdCountH        = 30f;
    const float  FdCountBottom   = 18f;     // 计数那行距底框
    const float  FdCountFS       = 22f;
    static readonly Color FdSteel = new Color32(142, 162, 180, 190);   // 钢 #8EA2B4 · 75%

    // ── 好友详情 · 「添加好友」那一格（2026-09-27）────────────────────────────────
    // 用户：「添加好友，首先是一个长的输入框（框的最右边有个类似于放大镜的ui），输入框可以之间输入 id 或者昵称
    //       （加入右键粘贴id功能），然后会在下方列出所有相关玩家（一般是输入昵称时可以有多个玩家）（也是能滑动的）」。
    //
    // 版式（自上而下）：44px 标题（与另三格共用那一行）→ 输入框那口井 1360x76（右端留 16 放一枚 44 的放大镜）
    //   → 一条金细线 → 结果列表（ScrollRect；行底板复用 LobbyFriendRow.png，1360x96 / 行距 12）
    //   → 右下角「n 位相关玩家」+ 列表正中那句空态。
    // 井与结果行**同宽同列**（都在 x32 .. x1392）：上下看过去是一条竖列，比「输入框比行窄一圈」稳。
    // 贴图由 Tools/cardframe/FriendAddV1.ps1 出（井底板 1376x92 存 1:1 / 放大镜徽章 256 按全族 x3）。
    // 搜索规则与「加好友」全在 Scripts/UI/Lobby/FriendAddSearch.cs，本类只管版式与接线。
    const string FaInputName  = "Input_Query";
    const string FaLineName   = "Line_Divider";
    const string FaListName   = "Result_List";
    const string FaCountName  = "Text_Count";
    const string FaEmptyName  = "Text_Empty";
    const string FaRowName    = "RowTemplate";
    const string FaSearchName = "Btn_Search";
    const string FaAddName    = "Act_Add";
    const string FaInputTex   = UiDir + "LobbyFriendAddInput.png";
    const string FaMagTex     = UiDir + "Icon_FriendSearch.png";
    const string FaMagHovTex  = UiDir + "Icon_FriendSearchHover.png";
    const string FaAddTex     = UiDir + "Icon_FriendPlus.png";        // 「添加」复用好友行那颗「+」
    const string FaAddHovTex  = UiDir + "Icon_FriendPlusHover.png";
    const float  FaColX       = 32f;      // 井与结果行共用的左沿（与好友列表那 32 让边同一档）
    const float  FaColW       = FdRowW;   // 井与结果行共用的宽度（一列到底）
    const float  FaInputTop   = 124f;     // 井的上沿（让开 44px 标题，与好友列表列表上沿同一档）
    const float  FaInputH     = 76f;
    const float  FaPlatePad   = 8f;       // 井底板贴图外框比井身每边多 8（贴图 24 / 3）
    const float  FaPadL       = 24f;      // 井内文字左让
    const float  FaPadV       = 14f;      // 井内文字上下让
    const float  FaMagSize    = 44f;      // 放大镜徽章（与三格动作 / 好友表头「+」同尺寸）
    const float  FaMagRight   = 16f;      // 徽章距井右沿
    const float  FaMagGap     = 16f;      // 徽章左边让给文字的距离
    const float  FaInputTextFS = 30f;
    const float  FaInputHintFS = 28f;
    const float  FaLineY      = 224f;     // 金细线：井下面 24
    const float  FaListTop    = 248f;     // 列表上沿（金细线再下 24）
    const float  FaListBottom = 60f;      // 列表下沿（给右下角那句计数留位）
    const float  FaRowH       = 96f;
    const float  FaRowGap     = 12f;
    const float  FaRowPlatePad = 8f;
    const float  FaRowRingX   = 14f;
    const float  FaRowRingSize = 64f;
    const float  FaRowAvatarInset = 7f;
    const float  FaRowAvatarSize = 50f;
    const float  FaRowNameX   = 96f;
    const float  FaRowNameW   = 340f;
    const float  FaRowNameH   = 40f;      // ★ 必须容得下整行：26 号实测 preferredHeight 37.4
    const float  FaRowNameFS  = 26f;
    const float  FaRowIdX     = 456f;
    const float  FaRowIdW     = 480f;
    const float  FaRowIdH     = 40f;
    const float  FaRowIdFS    = 20f;
    const float  FaRowStatusR = 1272f;    // 状态字右沿（再往右就是那颗「+」）
    const float  FaRowStatusW = 240f;
    const float  FaRowStatusH = 40f;
    const float  FaRowStatusFS = 22f;
    const float  FaAddSize    = 44f;
    const float  FaAddY       = -26f;
    const float  FaAddX       = FaColW - 20f - FaAddSize;   // 1296（与好友行那颗「+」同位）
    const float  FaCountW     = 300f;
    const float  FaCountH     = 30f;
    const float  FaCountBottom = 18f;
    const float  FaCountFS    = 22f;
    static readonly Color FaSteel = new Color32(142, 162, 180, 190);   // 钢 #8EA2B4 · 75%
    static readonly Color FaHint  = new Color32(142, 162, 180, 205);   // 钢 #8EA2B4 · 80%

    // ══════════════════════════════════════════════════════════════════════
    // 申请列表 / 黑名单 两格（2026-09-27）—— 用户：「申请列表和黑名单一起做，展示申请加好友的列表
    // （均在右下角类似限制50）只在右边显示不同，申请列表有一个勾和叉的ui图案用于同意和申请，
    // 黑名单则只有一个取消拉黑的」。
    // 左半（环 / 井 / 名称 / 异界号 / 状态）与好友列表逐个数一样，直接复用 FaRow* 那一组，
    // 这里只补「两格自己的」：列表盒、计数、右端两枚动作。
    // 贴图由 Tools/cardframe/FriendRequestV1.ps1 出（三枚徽章 256 按全族 x3）。
    // ══════════════════════════════════════════════════════════════════════
    const string FpListName   = "Panel_List";
    const string FpCountName  = "Text_Count";
    const string FpEmptyName  = "Text_Empty";
    const string FpRowName    = "RowTemplate";
    const string FpActAccept  = "Act_Accept";
    const string FpActRefuse  = "Act_Refuse";
    const string FpActUnblock = "Act_Unblock";
    const string FpAcceptTex  = UiDir + "Icon_FriendActAccept.png";
    const string FpAcceptHov  = UiDir + "Icon_FriendActAcceptHover.png";
    const string FpRefuseTex  = UiDir + "Icon_FriendActRefuse.png";
    const string FpRefuseHov  = UiDir + "Icon_FriendActRefuseHover.png";
    const string FpUnblockTex = UiDir + "Icon_FriendActUnblock.png";
    const string FpUnblockHov = UiDir + "Icon_FriendActUnblockHover.png";
    const float  FpColX       = 32f;      // 与好友列表 / 添加好友同一档让边
    const float  FpListTop    = 124f;     // 这两格没有输入井，列表直接顶到标题下面（与好友列表同一档）
    const float  FpListBottom = 60f;      // 给右下角「n/50」留位
    const float  FpCountW     = 300f;
    const float  FpCountH     = 30f;
    const float  FpCountBottom = 18f;
    const float  FpCountFS    = 22f;
    const float  FpActSize    = 44f;      // 与好友列表那三格同尺寸
    const float  FpActY       = -26f;
    const float  FpActMainX   = FaAddX;                       // 1296：拒绝 / 取消拉黑
    const float  FpActSubX    = FaAddX - 44f - 12f;           // 1240：同意


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
        title.raycastTarget = false;                    // 「+」落在标题盒右端，别让标题把点击吃掉

        // ── 表头行右端那颗「+」：打开好友详情全屏（2026-09-27）──
        WireFriendDetailPlus(body);

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

        // ── 滚动（2026-09-27 用户：「加滚动」）──────────────────────────────────────────
        // List 自己 = ScrollRect（只竖滚 + Clamped 不回弹），子 Viewport 用 RectMask2D 硬裁 +
        // 一张 α=0 的 Image（吃得到拖拽 —— Unity 不看 α），行都挂在 Viewport/Content 下。
        // Content 的高度由 LobbyFriendListUI 按行数改 —— 它就是 ScrollRect 的可滚范围。
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 40f;        // 滚轮灵敏度（默认 1 太肉）

        RectTransform viewport = NewRect(list, "Viewport", AnchorTL, PivotTL, Vector2.zero, Vector2.zero);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;
        scroll.viewport = viewport;

        RectTransform content = NewRect(viewport, "Content", AnchorTL, PivotTL, Vector2.zero, Vector2.zero);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 0f);           // 高度运行时按行数改
        scroll.content = content;

        // 行模板：挂在 Content 下、**存成 inactive**（运行时克隆 + Bind，真名单里一行一个）
        const float RowH = 84f;
        float rowW = FriendsPanelW - 56f;
        var rowRT = NewRect(content, "RowTemplate", AnchorTL, PivotTL, Vector2.zero, new Vector2(rowW, RowH));
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
        // 右端那格「邀请加号」（2026-09-27）——排在行内最后：画在其余件之上，点击也先命中它
        WireInvitePlus(rowRT, row, rowW);
        rowRT.gameObject.SetActive(false);                   // 模板自己藏着，只给克隆用

        // 名单 UI 挂在 Content 上（它就是「行的容器」）：高度按行数改 = ScrollRect 的可滚范围
        var listUI = content.gameObject.AddComponent<LobbyFriendListUI>();
        listUI.rowTemplate = row;
        listUI.emptyText = empty;                            // 空表时那句「暂无好友」
        listUI.scrollRect = scroll;

        // 服务挂在大厅 Canvas 上（本菜单会反复重建 Panel_Friends，服务别跟着一起没）
        if (canvas.gameObject.GetComponent<FriendListService>() == null)
            canvas.gameObject.AddComponent<FriendListService>();

        // 邀请服务同待遇：它要活过 Panel_Friends 的重建（冷却表 / 收邀请的回调都在它身上）
        if (canvas.gameObject.GetComponent<LobbyInviteService>() == null)
            canvas.gameObject.AddComponent<LobbyInviteService>();

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
        WireFriendDetailEntry(canvas);      // 「+」-> 好友详情面板（两边任一重建后都要重接）

        Debug.Log("[LobbyUI] 已生成 " + FriendsPanelName + "（好友侧边栏）：屏幕 " + FriendsPanelW + " x 1080 满高浮层" +
                  "（距上沿 " + FriendsPanelTop + " / 下沿 " + FriendsPanelBottom + "，让开量留给日后微调）+ " +
                  "整屏透明挡板（点它收回去）；好友图标 Icon_Friend 已改接它（再点一次也收回去）。" +
                  "宽 = Plate_Profile 右沿，底图 " + FriendsPanelTex + " 由 Tools/cardframe/LobbyUIv1.ps1 出。");
    }

    /// <summary>好友侧边栏表头行右端那颗「+」（2026-09-27 用户：「在好友右边靠近右边框的地方加上一个加号 ui
    /// 用于打开好友详情全屏」）。配方**逐行照抄通用关闭叉** Icon_Close（圆角方印 + 内缩金线 + 圆头金笔画，
    /// 由 Tools/cardframe/LobbyFriendPlusV1.ps1 出），只把叉那两笔改成加号；悬停 = 石面提亮 + 金 GOLD->GOLD_L。
    /// 位置：表头行右端、距右边框 FriendPlusRight，与「好友」标题同一行（y = FriendPlusY，盒 44x44）。
    /// 点击**不挂 Button**，只走 LobbyIconHover.subPanel —— 同物体上两个 IPointerClickHandler 会被各触发一次
    /// （Toggle 再 Open 正好互相抵消 = 点了没反应），所以入口一律只留一个处理器。
    /// 幂等：已经有了就只回填贴图与引用。</summary>
    static RawImage WireFriendDetailPlus(RectTransform body)
    {
        float x = FriendsPanelW - FriendPlusRight - FriendPlusSize;
        string normalPath = UiDir + FriendPlusName + ".png";
        string hoverPath  = UiDir + FriendPlusName + "Hover.png";

        Transform old = body.Find(FriendPlusName);
        RawImage icon;
        if (old != null)
        {
            icon = old.GetComponent<RawImage>();
            old.SetAsLastSibling();                  // 排在标题 / 金线之后：点击先命中它
        }
        else
        {
            icon = NewRaw(body, FriendPlusName, normalPath, AnchorTL, PivotTL,
                          new Vector2(x, FriendPlusY), new Vector2(FriendPlusSize, FriendPlusSize));
            icon.raycastTarget = true;
        }

        var hover = icon.GetComponent<LobbyIconHover>();
        if (hover == null) hover = icon.gameObject.AddComponent<LobbyIconHover>();
        hover.icon = icon;
        hover.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        hover.hoverTexture  = AssetDatabase.LoadAssetAtPath<Texture2D>(hoverPath);
        hover.popup = null;                          // 不弹占位窗
        hover.title = "";
        if (hover.hoverTexture == null) Debug.LogWarning("[LobbyUI] 找不到 " + hoverPath + " —— 好友表头那颗「+」没有悬停态");
        EditorUtility.SetDirty(hover);
        EditorUtility.SetDirty(icon);
        return icon;
    }

    /// <summary>把好友表头那颗「+」接到「好友详情」面板。两边任一重建后都要重接（重建会把旧引用变成 null）。</summary>
    static void WireFriendDetailEntry(Canvas canvas)
    {
        if (canvas == null) return;
        Transform plus = FindDeep(canvas.transform, FriendPlusName);
        if (plus == null) { Debug.LogWarning("[LobbyUI] 找不到 " + FriendPlusName + " —— 好友详情入口没接上（先跑「大厅：生成好友侧边栏」）"); return; }

        var hover = plus.GetComponent<LobbyIconHover>();
        if (hover == null) { Debug.LogWarning("[LobbyUI] " + FriendPlusName + " 上没有 LobbyIconHover —— 点击没接上"); return; }
        Transform panelT = FindDeep(canvas.transform, FriendDetailPanelName);
        if (panelT == null)
        {
            hover.subPanel = null;
            Debug.Log("[LobbyUI] 好友详情面板还没生成（" + FriendDetailPanelName + "）—— 「+」的点击等它生成后再接。");
            return;
        }

        hover.subPanel = panelT.GetComponent<LobbySubPanel>();
        EditorUtility.SetDirty(hover);
        Debug.Log("[LobbyUI] " + FriendPlusName + " -> " + FriendDetailPanelName + " 的点击引用已重接（原来指向已被销毁的旧组件时会是 null）。");
    }

    /// <summary>铺满父级、用 offsetMin / offsetMax 定边的透明框（用法同 BuildSubPanel 里的 Body_Content）。</summary>
    static RectTransform StretchRect(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
    {
        return StretchRect(parent, name, Vector2.zero, Vector2.one, offsetMin, offsetMax);
    }

    /// <summary>带锚点的版本：「一边拉伸、另一边钉死」用它 —— 两条金细线就是这么来的。</summary>
    /// <remarks>2026-09-27 实测踩到：两条线都照「四边全拉伸 + offset」写时，沿**拉伸**那条轴的边长
    /// = 父级边长 + offsetMax - offsetMin。线又是**没贴图的纯色方块**（铺多大就画多大），于是「一条 2px 的金线」
    /// 被拉成 1792x770 一整块金色大板 —— 屏幕上一大片金。横线要锚上沿（y 钉死、x 拉伸）、竖线锚左沿（x 钉死、y 拉伸）。
    /// 钉死那条轴的两个 offset 就是**屏幕上沿 / 左沿的像素距离**，换分辨率不会跟着面板一起被拉长。</remarks>
    static RectTransform StretchRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = NewRect(parent, name, Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    /// <summary>好友详情的内容骨架：横金线（叉下面） + 竖金线（从上到下） + 左侧四格竖栏 + 右侧一块独立大格子。</summary>
    /// <remarks>2026-09-27 用户：「左边有四个格子类似于大厅右下角那四个不过更扁平，分别是好友列表，添加好友，
    /// 申请列表，黑名单，初始进入位于好友列表格子，在右上角的叉下面划一道长金线横跨左右分割一下，然后左边也有
    /// 从上到下的一个金线将四个从上到下的格子与右边空间分开得到右边一个独立的大格子，四个格子每个格子的内容
    /// 都将不一样」。
    ///
    /// 四格与内容一一对应：格 i 用 <see cref="LobbyFriendTab.index"/> 自报家门，右侧 Body_Detail 里的
    /// Content_&lt;格&gt; 同一时刻只开当前那一块（<see cref="LobbyFriendDetailTabs"/>）。内容本体用户还没说，
    /// 现在每块只放「标题 + 占位 · 内容待接入」。
    /// 幂等：先把旧的四个件收掉再建（重跑本菜单不叠）。</remarks>
    static void BuildFriendDetailContent(GameObject panel)
    {
        if (panel == null) return;
        Transform bg = panel.transform.Find("Bg");
        int at = bg != null ? bg.GetSiblingIndex() + 1 : 0;

        string[] olds = { FdLineTopName, FdLineLeftName, FdRailName, FdBodyName };
        for (int i = 0; i < olds.Length; i++)
        {
            Transform o = panel.transform.Find(olds[i]);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        float lineLeftX = FdPadX + FdTabW + FdLineLeftGap;      // 428

        // ① 横金线：右上关闭叉的下面，横跨左右
        var lineTop = StretchRect(panel.transform, FdLineTopName, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                  new Vector2(FdPadX, FdLineTopY - 2f),
                                  new Vector2(-FdPadX, FdLineTopY)).gameObject.AddComponent<Image>();
        lineTop.color = FdLineColor;
        lineTop.raycastTarget = false;
        lineTop.transform.SetSiblingIndex(at);

        // ② 竖金线：从横金线一路到下沿（把左侧四格与右边那块分开）
        var lineLeft = StretchRect(panel.transform, FdLineLeftName, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                   new Vector2(lineLeftX, FdPadBottom),
                                   new Vector2(lineLeftX + 2f, FdLineTopY)).gameObject.AddComponent<Image>();
        lineLeft.color = FdLineColor;
        lineLeft.raycastTarget = false;
        lineLeft.transform.SetSiblingIndex(at + 1);

        // ③ 左侧竖栏：四个格子在竖栏里等分，板在格内居中
        RectTransform rail = StretchRect(panel.transform, FdRailName,
                                         new Vector2(FdPadX, FdPadBottom),
                                         new Vector2(FdPadX + FdTabW, FdLineTopY));
        rail.SetSiblingIndex(at + 2);

        var tabs = new LobbyFriendTab[FdTabNames.Length];
        for (int i = 0; i < FdTabNames.Length; i++)
        {
            // 格心：竖栏高度上按 1/8、3/8、5/8、7/8 锚（换分辨率不会散）
            float cy = 1f - (i + 0.5f) / FdTabNames.Length;
            RectTransform tabRT = NewRect(rail, "Tab_" + FdTabNames[i], new Vector2(0f, cy), PivotC,
                                          new Vector2(FdTabW * 0.5f, 0f), new Vector2(FdTabW, FdTabH));

            // 板身 340x104，贴图外框 356x120（每边多 8）—— 框的左上角相对板身左上角是 (-8, +8)
            RawImage plate = NewRaw(tabRT, "Plate", FdTabTex, AnchorTL, PivotTL,
                                    new Vector2(-FdPadTex, FdPadTex),
                                    new Vector2(FdTabW + FdPadTex * 2f, FdTabH + FdPadTex * 2f));
            plate.raycastTarget = true;                     // 整格的可点区就是这张板

            RawImage emblem = NewRaw(tabRT, "Emblem", UiDir + FdTabEmblems[i] + ".png", AnchorTL, PivotC,
                                     new Vector2(FdTabW * FdEmblemCX, -FdTabH * 0.5f),
                                     new Vector2(FdEmblemSize, FdEmblemSize));
            emblem.raycastTarget = false;

            TextMeshProUGUI label = NewLabel(tabRT, "Label", FdTabLabels[i],
                                             new Vector2(FdLabelX, -(FdTabH * 0.5f - 0.87f * FdFontSize)),
                                             new Vector2(FdLabelW, FdFontSize * 1.6f), FdFontSize);
            label.raycastTarget = false;                    // 落在字上也要冒泡到这一格

            var tab = tabRT.gameObject.AddComponent<LobbyFriendTab>();
            tab.index = i;
            tab.plate = plate;
            tab.emblem = emblem;
            tab.label = label;
            tab.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FdTabTex);
            tab.hoverTexture  = AssetDatabase.LoadAssetAtPath<Texture2D>(FdTabHoverTex);
            tab.onTexture     = AssetDatabase.LoadAssetAtPath<Texture2D>(FdTabOnTex);
            tab.normalColor = Cream;
            tab.goldColor = GoldBright;
            tab.normalEmblemAlpha = 0.50f;
            tab.hoverEmblemAlpha = 0.82f;
            tab.onEmblemAlpha = 1.00f;
            tabs[i] = tab;
        }

        // ④ 右侧那块独立的大格子（四块内容叠在同一处，只开当前那格）
        RectTransform body = StretchRect(panel.transform, FdBodyName,
                                         new Vector2(lineLeftX, FdPadBottom),
                                         new Vector2(-FdPadX, FdLineTopY));
        body.SetSiblingIndex(at + 3);

        var contents = new GameObject[FdTabNames.Length];
        for (int i = 0; i < FdTabNames.Length; i++)
        {
            RectTransform c = StretchRect(body, "Content_" + FdTabNames[i], Vector2.zero, Vector2.zero);
            TextMeshProUGUI head = NewLabel(c, "Text_Head", FdTabLabels[i],
                                            new Vector2(40f, -40f), new Vector2(900f, 64f), 44f);
            head.color = GoldBright;
            head.raycastTarget = false;
            // 第 1 格「好友列表」= 真名单（滚动 + 行 + 右下角 n/50）；第 2 格「添加好友」= 搜索框 + 可滚结果
            if (i == 0)
            {
                BuildFriendListInto(c);
            }
            else if (i == 1)
            {
                BuildFriendAddInto(c);
            }
            else if (i == 2)
            {
                BuildFriendPanelInto(c, false);      // 申请列表（勾 / 叉）
            }
            else
            {
                BuildFriendPanelInto(c, true);       // 黑名单（只有取消拉黑）
            }

            c.gameObject.SetActive(i == 0);
            contents[i] = c.gameObject;
        }

        // ⑤ 控制器 + 把初始态摆好（初始进入 = 好友列表那一格）
        var ctrl = panel.GetComponent<LobbyFriendDetailTabs>();
        if (ctrl == null) ctrl = panel.gameObject.AddComponent<LobbyFriendDetailTabs>();
        ctrl.tabs = tabs;
        ctrl.contents = contents;
        ctrl.startIndex = 0;

        // ★ 每一格要回指控制器 —— 漏了这条，点击就只是换了自己的皮、切不动内容
        //   （2026-09-27 实测踩到：LobbyFriendTab.tabs 为 null，点第 4 格毫无反应）
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].tabs = ctrl;
            EditorUtility.SetDirty(tabs[i]);
        }
        ctrl.Select(0);

        Transform closeBtn = panel.transform.Find("Btn_Close");
        if (closeBtn != null) closeBtn.SetAsLastSibling();   // 叉子始终压在内容之上

        Debug.Log("[LobbyUI] " + panel.name + " 内容已就位：横金线 y " + FdLineTopY + "（横跨 " + FdPadX + "..屏幕右沿-" + FdPadX + "）" +
                  " + 竖金线 x " + lineLeftX + "（从上到下到下沿 " + FdPadBottom + "）；左侧竖栏 " + FdTabW + " 宽、四格等分、板 " + FdTabW + "x" + FdTabH + "（比右下角那四块 300x120 更扁平）；" +
                  "右侧大格子 = Body_Detail（四块 Content_* 只开当前那格）；初始格 = " + FdTabLabels[0] + "。" +
                  "位置 / 尺寸改 Fd* 常量，贴图由 Tools/cardframe/FriendDetailTabsV1.ps1 出。");
    }


    /// <summary>「好友列表」那一格的内容：整块滚动名单 + 右下角「n/50」小字。</summary>
    /// <remarks>2026-09-27 用户：「好友列表（后续每个独立的玩家好友基本上都是按照这样）（上限50个好友），允许滑动，
    /// 每个好友有个独立的长矩形子背景，从左到右分别是头像，名称，id（这个字体小一点），然后中间可以留空，右边分别是
    /// 当前状态（在线/离线什么的），拉黑，删除，（仅在在线状态下）邀请……每个好友之间是有一点间隔，和底框也有间隔，
    /// 右下角是以类似 23/50 小字这种形式展示好友数量」。
    ///
    /// 滚动那一套照抄好友侧边栏（`Panel_Friends/Body/List`）：ScrollRect 只竖滚 + Clamped 不回弹，Viewport 用
    /// RectMask2D 硬裁 + 一张 α=0 的 Image 吃拖拽，行都挂在 Viewport/Content 下。与侧边栏的差别只有三条：
    /// **上限 50**、行底板是一整张贴图（`LobbyFriendRow`）、右下角有计数。
    ///
    /// 行是**定宽**（`FdRowW` 1360）不是拉伸：行底板是一张按尺寸出的图，金细线与圆角不等比拉伸就会变形；
    /// 定宽换来的是换分辨率时行不跟着拉长，与底框的间隔自然变大（用户要的「和底框也有间隔」）。
    /// 幂等：先把旧的列表 / 计数 / 空表那句收掉再建。</remarks>
    static void BuildFriendListInto(RectTransform content)
    {
        string[] olds = { FdListName, FdCountName, FdEmptyName };
        for (int i = 0; i < olds.Length; i++)
        {
            Transform o = content.Find(olds[i]);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        // 三行文字各自在自己盒里竖直居中（盒心都落在行中线 y = -FdRowH/2 上）
        float nameY   = -(FdRowH * 0.5f - FdRowNameH * 0.5f);
        float idY     = -(FdRowH * 0.5f - FdRowIdH * 0.5f);
        float statusY = -(FdRowH * 0.5f - FdRowStatusH * 0.5f);

        // ① 列表 = ScrollRect（只竖滚 + Clamped 不回弹）
        RectTransform list = StretchRect(content, FdListName,
                                         new Vector2(FdListPadX, FdListBottom),
                                         new Vector2(-FdListPadX, -FdListTop));
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 40f;        // 滚轮灵敏度（默认 1 太肉）

        RectTransform viewport = StretchRect(list, "Viewport", Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;    // 吃得到拖拽（Unity 不看 α）
        scroll.viewport = viewport;

        RectTransform listContent = NewRect(viewport, "Content", AnchorTL, PivotTL, Vector2.zero, Vector2.zero);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = new Vector2(0f, 0f);           // 高度运行时按行数改
        scroll.content = listContent;

        // ② 行模板（挂在 Content 下、存成 inactive，运行时克隆）：整行一块长矩形子背景 + 头像 + 名称 / id / 状态 + 三格动作
        RectTransform rowRT = NewRect(listContent, "RowTemplate", AnchorTL, PivotTL, Vector2.zero, new Vector2(FdRowW, FdRowH));
        var row = rowRT.gameObject.AddComponent<FriendDetailRowUI>();

        // 贴图外框比板身每边多 8（PAD 24 / 3）—— 板身正好 1360x96 盖住整行
        RawImage plate = NewRaw(rowRT, "Plate", FdRowTex, AnchorTL, PivotTL,
                                new Vector2(-FdRowPlatePad, FdRowPlatePad),
                                new Vector2(FdRowW + FdRowPlatePad * 2f, FdRowH + FdRowPlatePad * 2f));
        plate.raycastTarget = false;           // 拖拽交给 Viewport 那张 α=0 的图

        // 头环 + 井里头像（口径 = LobbyAvatarRing 264 里井半径 102 → 64 上 49.5 → 取 50）
        RawImage ring = NewRaw(rowRT, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL,
                               new Vector2(FdRowRingX, -FdRowRingY), new Vector2(FdRowRingSize, FdRowRingSize));
        ring.raycastTarget = false;
        RectTransform avatarRT = NewRect(rowRT, "Avatar_Image", AnchorTL, PivotTL,
                                         new Vector2(FdRowRingX + FdRowAvatarInset, -(FdRowRingY + FdRowAvatarInset)),
                                         new Vector2(FdRowAvatarSize, FdRowAvatarSize));
        var avatar = avatarRT.gameObject.AddComponent<RawImage>();
        avatar.texture = null;                 // 运行时填：先灰盘，Steam 头像到货自己换
        avatar.raycastTarget = false;
        avatarRT.SetSiblingIndex(ring.transform.GetSiblingIndex() + 1);

        TextMeshProUGUI name = NewLabel(rowRT, "Text_Name", "名字",
                                        new Vector2(FdRowNameX, nameY), new Vector2(FdRowNameW, FdRowNameH), FdRowNameFS);
        name.alignment = TextAlignmentOptions.Left;
        name.overflowMode = TextOverflowModes.Ellipsis;    // 名字长了截断，别糊到 id 上
        name.color = Cream;
        name.raycastTarget = false;

        TextMeshProUGUI id = NewLabel(rowRT, "Text_Id", "",
                                      new Vector2(FdRowIdX, idY), new Vector2(FdRowIdW, FdRowIdH), FdRowIdFS);
        id.alignment = TextAlignmentOptions.Left;
        id.color = FdSteel;
        id.raycastTarget = false;

        TextMeshProUGUI status = NewLabel(rowRT, "Text_Status", "离线",
                                          new Vector2(FdRowStatusR - FdRowStatusW, statusY),
                                          new Vector2(FdRowStatusW, FdRowStatusH), FdRowStatusFS);
        status.alignment = TextAlignmentOptions.Right;
        status.color = FdSteel;
        status.raycastTarget = false;

        WireRowAction(rowRT, "Act_Block", FdActBlockX, FdActBlockTex, FdActBlockHov, FriendRowActionKind.Block, row);
        WireRowAction(rowRT, "Act_Delete", FdActDeleteX, FdActDeleteTex, FdActDeleteHov, FriendRowActionKind.Delete, row);
        FriendRowAction invite = WireRowAction(rowRT, "Act_Invite", FdActInviteX, FdActInviteTex, FdActInviteHov, FriendRowActionKind.Invite, row);

        row.avatarImage = avatar;
        row.nameText = name;
        row.idText = id;
        row.statusText = status;
        row.inviteGroup = invite.gameObject;     // 只有「空闲在线」那一档才 SetActive(true)
        rowRT.gameObject.SetActive(false);       // 模板自己藏着，只给克隆用

        // ③ 右下角「实际 / 上限」小字（用户举的例子就是「23/50」）
        TextMeshProUGUI count = NewLabel(content, FdCountName, "0/" + FdMaxFriends,
                                         new Vector2(40f, -40f), new Vector2(FdCountW, FdCountH), FdCountFS);
        RectTransform countRT = count.rectTransform;
        countRT.anchorMin = new Vector2(1f, 0f);
        countRT.anchorMax = new Vector2(1f, 0f);
        countRT.pivot = new Vector2(1f, 0f);
        countRT.anchoredPosition = new Vector2(-FdListPadX, FdCountBottom);
        count.alignment = TextAlignmentOptions.Right;
        count.color = FdSteel;
        count.raycastTarget = false;

        // ④ 空表那句话（摆在大格子正中）
        TextMeshProUGUI empty = NewLabel(content, FdEmptyName, "", Vector2.zero, new Vector2(FdRowW, 40f), 24f);
        RectTransform emptyRT = empty.rectTransform;
        emptyRT.anchorMin = new Vector2(0.5f, 0.5f);
        emptyRT.anchorMax = new Vector2(0.5f, 0.5f);
        emptyRT.pivot = new Vector2(0.5f, 0.5f);
        emptyRT.anchoredPosition = Vector2.zero;
        empty.alignment = TextAlignmentOptions.Center;
        empty.color = new Color32(142, 162, 180, 170);
        empty.raycastTarget = false;

        // ⑤ 名单 UI 挂在 Content 上（它就是「行的容器」）：高度按行数改 = ScrollRect 的可滚范围
        var listUI = listContent.gameObject.AddComponent<LobbyFriendDetailListUI>();
        listUI.rowTemplate = row;
        listUI.scrollRect = scroll;
        listUI.countText = count;
        listUI.emptyText = empty;
        listUI.maxFriends = FdMaxFriends;
        listUI.rowGap = FdRowGap;

        // ⑥ 两个服务挂在大厅 Canvas 上 —— 本菜单会反复重建面板，服务别跟着一起没
        Canvas canvas = content.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (canvas.gameObject.GetComponent<FriendListService>() == null) canvas.gameObject.AddComponent<FriendListService>();
            if (canvas.gameObject.GetComponent<LobbyInviteService>() == null) canvas.gameObject.AddComponent<LobbyInviteService>();
        }

        Debug.Log("[LobbyUI] 好友列表内容已就位：上限 " + FdMaxFriends + "、行 " + FdRowW + "x" + FdRowH + "、行距 " + FdRowGap +
                  "、列表让边 " + FdListPadX + "（上 " + FdListTop + " / 下 " + FdListBottom + " —— 下留给右下角那行 n/" + FdMaxFriends + "）。");
    }

    /// <summary>行右端一格式动作：图标（常态 / 悬停两张贴图）+ <see cref="FriendRowAction"/>。</summary>
    /// <remarks>**不挂 Button** —— 同物体上两个 IPointerClickHandler 会被各触发一次（与「+」、左侧四个 tab 同一条坑）。</remarks>
    static FriendRowAction WireRowAction(RectTransform rowRT, string name, float x, string normalTex, string hoverTex,
                                         FriendRowActionKind kind, FriendDetailRowUI row)
    {
        RawImage icon = NewRaw(rowRT, name, normalTex, AnchorTL, PivotTL,
                               new Vector2(x, FdActY), new Vector2(FdActSize, FdActSize));
        icon.raycastTarget = true;
        var act = icon.gameObject.AddComponent<FriendRowAction>();
        act.kind = kind;
        act.icon = icon;
        act.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTex);
        act.hoverTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(hoverTex);
        act.row = row;
        return act;
    }


    /// <summary>「添加好友」那一格的内容：长输入框（右端放大镜）+ 金细线 + 可滚的结果名单 + 右下角计数。</summary>
    /// <remarks>2026-09-27 用户：「添加好友，首先是一个长的输入框（框的最右边有个类似于放大镜的ui），输入框可以之间
    /// 输入 id 或者昵称（加入右键粘贴id功能），然后会在下方列出所有相关玩家（一般是输入昵称时可以有多个玩家）
    /// （也是能滑动的）」。
    ///
    /// 几处与好友列表那份刻意的差别：
    ///   ① 输入框的 **ContentType = Standard**（不是 Alphanumeric）—— 昵称里有中文，Alphanumeric 会直接吃掉；
    ///   ② 井里那口 Text Area 的右沿**让开放大镜那一格**（FaMagRight + FaMagSize + FaMagGap），文字不会压到徽章上；
    ///   ③ 上 / 下两道金线只做**一道**（井与结果之间）—— 上面那道横金线是这一格的公共骨架（BuildFriendDetailContent 画的）；
    ///   ④ 结果列表与好友列表用**同一套** ScrollRect 配方（只竖滚 + Clamped + RectMask2D 硬裁 + α=0 的 Viewport 吃拖拽）；
    ///   ⑤ 空态那句挂在 **list** 下而不是 Content 下 —— 它是「列表正中」的一句话，不该跟着滚走。
    /// 幂等：先把旧的井 / 金线 / 列表 / 计数收掉再建（重跑本菜单不叠）。</remarks>
    static void BuildFriendAddInto(RectTransform content)
    {
        string[] olds = { FaInputName, FaLineName, FaListName, FaCountName };
        for (int i = 0; i < olds.Length; i++)
        {
            Transform o = content.Find(olds[i]);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        // ① 输入框那口井（长条；右端让出一枚放大镜徽章）
        RectTransform well = NewRect(content, FaInputName, AnchorTL, PivotTL,
                                     new Vector2(FaColX, -FaInputTop), new Vector2(FaColW, FaInputH));
        var wellHit = well.gameObject.AddComponent<Image>();   // 当 targetGraphic + 点击面（自己全透明，视觉全在 Plate 上）
        wellHit.color = new Color(0f, 0f, 0f, 0f);
        wellHit.raycastTarget = true;

        RawImage wellPlate = NewRaw(well, "Plate", FaInputTex, AnchorC, PivotC, Vector2.zero,
                                    new Vector2(FaColW + FaPlatePad * 2f, FaInputH + FaPlatePad * 2f));
        wellPlate.raycastTarget = false;   // 聚焦 / 定位光标都归井自己

        RectTransform area = NewRect(well, "Text Area", AnchorTL, PivotTL,
                                     new Vector2(FaPadL, -FaPadV),
                                     new Vector2(FaColW - FaPadL - (FaMagRight + FaMagSize + FaMagGap), FaInputH - FaPadV * 2f));
        area.gameObject.AddComponent<RectMask2D>();

        RectTransform textRT = StretchRect(area, "Text", Vector2.zero, Vector2.zero);
        var text = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.text = ""; text.fontSize = FaInputTextFS; text.color = Cream;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow;

        RectTransform phRT = StretchRect(area, "Placeholder", Vector2.zero, Vector2.zero);
        var ph = phRT.gameObject.AddComponent<TextMeshProUGUI>();
        ph.font = _font; ph.text = "输入异界号"; ph.fontSize = FaInputHintFS;
        ph.alignment = TextAlignmentOptions.MidlineLeft; ph.raycastTarget = false;
        ph.enableWordWrapping = false; ph.overflowMode = TextOverflowModes.Overflow;
        ph.color = FaHint;

        RectTransform magRT = NewRect(well, FaSearchName, AnchorTL, PivotTL,
                                      new Vector2(FaColW - FaMagRight - FaMagSize, -(FaInputH - FaMagSize) * 0.5f),
                                      new Vector2(FaMagSize, FaMagSize));
        var mag = magRT.gameObject.AddComponent<RawImage>();
        mag.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FaMagTex);
        if (mag.texture == null) Debug.LogWarning("[LobbyUI] 找不到贴图：" + FaMagTex);
        mag.raycastTarget = true;

        var input = well.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = wellHit;
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = ph;
        input.characterLimit = 32;                                   // 异界号 24 个字符 + 余量
        input.contentType = TMP_InputField.ContentType.Standard;     // ★ 昵称里有中文：不能是 Alphanumeric
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.restoreOriginalTextOnEscape = true;
        input.caretWidth = 2;
        input.customCaretColor = true;
        input.caretColor = Cream;
        input.selectionColor = new Color32(200, 164, 74, 90);

        // ② 金细线（井与结果列表之间的那道分割）
        var line = NewRect(content, FaLineName, AnchorTL, PivotTL,
                           new Vector2(FaColX, -FaLineY), new Vector2(FaColW, 2f)).gameObject.AddComponent<Image>();
        line.color = FdLineColor;
        line.raycastTarget = false;

        // ③ 结果列表 = ScrollRect（只竖滚 + Clamped 不回弹），配方与好友列表那份逐条一致
        RectTransform list = StretchRect(content, FaListName,
                                         new Vector2(FaColX, FaListBottom),
                                         new Vector2(-FaColX, -FaListTop));
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 40f;

        RectTransform viewport = StretchRect(list, "Viewport", Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;    // 吃得到拖拽（Unity 不看 α）
        scroll.viewport = viewport;

        RectTransform listContent = NewRect(viewport, "Content", AnchorTL, PivotTL, Vector2.zero, Vector2.zero);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = new Vector2(0f, 0f);           // 高度运行时按行数改
        scroll.content = listContent;

        // ④ 行模板（挂在 Content 下、存成 inactive，运行时克隆）
        FriendAddRowUI row = BuildFriendAddRow(listContent);

        // ⑤ 右下角「n 位相关玩家」
        TextMeshProUGUI count = NewLabel(content, FaCountName, "", new Vector2(40f, -40f),
                                         new Vector2(FaCountW, FaCountH), FaCountFS);
        RectTransform countRT = count.rectTransform;
        countRT.anchorMin = new Vector2(1f, 0f);
        countRT.anchorMax = new Vector2(1f, 0f);
        countRT.pivot = new Vector2(1f, 0f);
        countRT.anchoredPosition = new Vector2(-FaColX, FaCountBottom);
        count.alignment = TextAlignmentOptions.Right;
        count.color = FaSteel;
        count.raycastTarget = false;

        // ⑥ 列表正中那句（待输入 / 没找到）—— 挂在 list 下（不是 Content 下）：不跟着滚动走
        TextMeshProUGUI empty = NewLabel(list, FaEmptyName, "", Vector2.zero, new Vector2(FaColW, 40f), 26f);
        RectTransform emptyRT = empty.rectTransform;
        emptyRT.anchorMin = new Vector2(0.5f, 0.5f);
        emptyRT.anchorMax = new Vector2(0.5f, 0.5f);
        emptyRT.pivot = new Vector2(0.5f, 0.5f);
        emptyRT.anchoredPosition = Vector2.zero;
        empty.alignment = TextAlignmentOptions.Center;
        empty.color = new Color32(142, 162, 180, 170);
        empty.raycastTarget = false;

        // ⑦ 控制器写在**大格子自己身上**（行是它的孙子辈，GetComponentInParent 找得到）
        var ui = content.gameObject.AddComponent<FriendAddSearchUI>();
        ui.input = input;
        ui.scrollRect = scroll;
        ui.resultContent = listContent;
        ui.rowTemplate = row;
        ui.countText = count;
        ui.emptyText = empty;
        ui.rowGap = FaRowGap;
        ui.maxResults = FdMaxFriends;

        WireInputChanged(input, ui.OnQueryChanged);

        // ⑧ 右键 = 粘贴（与房间号那口井同一套，见 FriendAddInputPaste）
        var paste = well.gameObject.AddComponent<FriendAddInputPaste>();
        paste.owner = ui;

        // ⑨ 放大镜：点一下立刻搜（不等防抖）
        var magAct = magRT.gameObject.AddComponent<FriendAddAction>();
        magAct.kind = FriendAddActionKind.Search;
        magAct.icon = mag;
        magAct.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FaMagTex);
        magAct.hoverTexture  = AssetDatabase.LoadAssetAtPath<Texture2D>(FaMagHovTex);
        magAct.search = ui;

        // ⑩ 搜索要读好友表 —— 服务挂在大厅 Canvas 上（本菜单会反复重建面板，服务别跟着一起没）
        Canvas canvas = content.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.gameObject.GetComponent<FriendListService>() == null)
            canvas.gameObject.AddComponent<FriendListService>();

        row.gameObject.SetActive(false);       // 模板自己藏着，只给克隆用

        Debug.Log("[LobbyUI] 「添加好友」已就位：井 " + FaColW + "x" + FaInputH + " @x" + FaColX + "（右端放大镜 " + FaMagSize +
                  "，右让 " + FaMagRight + "）；金细线 y " + FaLineY + "；结果列表 y " + FaListTop + " .. 下沿让 " + FaListBottom +
                  "（行 " + FaColW + "x" + FaRowH + " / 行距 " + FaRowGap + "，底板复用 LobbyFriendRow）；右下角「n 位相关玩家」。" +
                  "搜索规则见 Scripts/UI/Lobby/FriendAddSearch.cs。");
    }

    /// <summary>「添加好友」结果列表的一行（模板）：底板 + 头像 + 名称 / 异界号 / 状态 + 右端那颗「+」。</summary>
    static FriendAddRowUI BuildFriendAddRow(RectTransform listContent)
    {
        RectTransform rowRT = NewRect(listContent, FaRowName, AnchorTL, PivotTL, Vector2.zero, new Vector2(FaColW, FaRowH));
        var row = rowRT.gameObject.AddComponent<FriendAddRowUI>();

        // 三行文字各自在自己盒里竖直居中（盒心都落在行中线 y = -FaRowH/2 上）
        float nameY   = -(FaRowH * 0.5f - FaRowNameH * 0.5f);
        float idY     = -(FaRowH * 0.5f - FaRowIdH * 0.5f);
        float statusY = -(FaRowH * 0.5f - FaRowStatusH * 0.5f);

        // 贴图外框比板身每边多 8（PAD 24 / 3）—— 板身正好 1360x96 盖住整行
        RawImage plate = NewRaw(rowRT, "Plate", FdRowTex, AnchorTL, PivotTL,
                                new Vector2(-FaRowPlatePad, FaRowPlatePad),
                                new Vector2(FaColW + FaRowPlatePad * 2f, FaRowH + FaRowPlatePad * 2f));
        plate.raycastTarget = false;           // 拖拽交给 Viewport 那张 α=0 的图

        RawImage ring = NewRaw(rowRT, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL,
                               new Vector2(FaRowRingX, -(FaRowH * 0.5f - FaRowRingSize * 0.5f)),
                               new Vector2(FaRowRingSize, FaRowRingSize));
        ring.raycastTarget = false;

        RectTransform avatarRT = NewRect(rowRT, "Avatar_Image", AnchorTL, PivotTL,
                                         new Vector2(FaRowRingX + FaRowAvatarInset, -(FaRowH * 0.5f - FaRowAvatarSize * 0.5f)),
                                         new Vector2(FaRowAvatarSize, FaRowAvatarSize));
        var avatar = avatarRT.gameObject.AddComponent<RawImage>();
        avatar.texture = null;                 // 运行时填：先灰盘，Steam 头像到货自己换
        avatar.raycastTarget = false;
        avatarRT.SetSiblingIndex(ring.transform.GetSiblingIndex() + 1);

        TextMeshProUGUI name = NewLabel(rowRT, "Text_Name", "名字",
                                        new Vector2(FaRowNameX, nameY), new Vector2(FaRowNameW, FaRowNameH), FaRowNameFS);
        name.alignment = TextAlignmentOptions.Left;
        name.overflowMode = TextOverflowModes.Ellipsis;   // 名字长了截断，别糊到异界号上
        name.color = Cream;
        name.raycastTarget = false;

        TextMeshProUGUI id = NewLabel(rowRT, "Text_Id", "",
                                      new Vector2(FaRowIdX, idY), new Vector2(FaRowIdW, FaRowIdH), FaRowIdFS);
        id.alignment = TextAlignmentOptions.Left;
        id.overflowMode = TextOverflowModes.Ellipsis;     // 异界号 24 个字符，盒子给的是 480（够，留着兜底）
        id.color = FdSteel;
        id.raycastTarget = false;

        TextMeshProUGUI status = NewLabel(rowRT, "Text_Status", "可添加",
                                          new Vector2(FaRowStatusR - FaRowStatusW, statusY),
                                          new Vector2(FaRowStatusW, FaRowStatusH), FaRowStatusFS);
        status.alignment = TextAlignmentOptions.Right;
        status.color = FaSteel;
        status.raycastTarget = false;

        // 右端那颗「+」：与好友行那颗「邀请 +」同尺寸同位（1296 / y-26）
        RectTransform addRT = NewRect(rowRT, FaAddName, AnchorTL, PivotTL,
                                      new Vector2(FaAddX, FaAddY), new Vector2(FaAddSize, FaAddSize));
        var addIcon = addRT.gameObject.AddComponent<RawImage>();
        addIcon.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FaAddTex);
        if (addIcon.texture == null) Debug.LogWarning("[LobbyUI] 找不到贴图：" + FaAddTex);
        addIcon.raycastTarget = true;

        var addAct = addRT.gameObject.AddComponent<FriendAddAction>();
        addAct.kind = FriendAddActionKind.Add;
        addAct.icon = addIcon;
        addAct.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FaAddTex);
        addAct.hoverTexture  = AssetDatabase.LoadAssetAtPath<Texture2D>(FaAddHovTex);
        addAct.row = row;

        row.avatarImage = avatar;
        row.nameText = name;
        row.idText = id;
        row.statusText = status;
        row.addGroup = addRT.gameObject;       // 只有「可添加」那一档才 SetActive(true)
        return row;
    }


    /// <summary>「申请列表 / 黑名单」那一格的内容：整块滚动名单 + 右下角「n/50」小字。</summary>
    /// <remarks>2026-09-27 用户：「申请列表和黑名单一起做……（均在右下角类似限制50）只在右边显示不同」。
    ///
    /// 两格**同一段代码**建（<paramref name="block"/> = false 申请列表 / true 黑名单）：左半与好友列表逐个数一样
    /// （同一张 `LobbyFriendRow` 底板、同样的列宽 / 行高 / 行距 / 让边），差的只有两处 ——
    /// **右端动作**（申请：勾 + 叉；黑名单：取消拉黑）与**数据源**，两边都在
    /// <see cref="FriendPanelRowUI"/> / <see cref="FriendPanelListUI"/> 里。
    ///
    /// 滚动配方与好友列表逐条一致：ScrollRect 只竖滚 + Clamped 不回弹，Viewport 用 RectMask2D 硬裁 +
    /// 一张 α=0 的 Image 吃拖拽，行模板挂在 Viewport/Content 下、存成 inactive，运行时克隆。
    /// 幂等：先把旧的列表 / 计数 / 空表那句收掉再建。
    /// **还要收掉早先那版留下的 `Text_Hint`（占位 · 内容待接入）** —— 第 3、4 格以前放的是它。</remarks>
    static void BuildFriendPanelInto(RectTransform content, bool block)
    {
        string[] olds = { FpListName, FpCountName, FpEmptyName, "Text_Hint" };
        for (int i = 0; i < olds.Length; i++)
        {
            Transform o = content.Find(olds[i]);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        // ① 名单 = ScrollRect（配方与好友列表 / 添加好友结果逐条一致）
        RectTransform list = StretchRect(content, FpListName,
                                         new Vector2(FpColX, FpListBottom),
                                         new Vector2(-FpColX, -FpListTop));
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 40f;

        RectTransform viewport = StretchRect(list, "Viewport", Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;    // 吃得到拖拽（Unity 不看 α）
        scroll.viewport = viewport;

        RectTransform listContent = NewRect(viewport, "Content", AnchorTL, PivotTL, Vector2.zero, Vector2.zero);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = new Vector2(0f, 0f);           // 高度运行时按行数改
        scroll.content = listContent;

        // ② 行模板
        FriendPanelMode mode = block ? FriendPanelMode.Block : FriendPanelMode.Request;
        FriendPanelRowUI row = BuildFriendPanelRow(listContent, mode);

        // ③ 右下角「n/50」（与好友列表同一口径）
        TextMeshProUGUI count = NewLabel(content, FpCountName, "", new Vector2(40f, -40f),
                                         new Vector2(FpCountW, FpCountH), FpCountFS);
        RectTransform countRT = count.rectTransform;
        countRT.anchorMin = new Vector2(1f, 0f);
        countRT.anchorMax = new Vector2(1f, 0f);
        countRT.pivot = new Vector2(1f, 0f);
        countRT.anchoredPosition = new Vector2(-FpColX, FpCountBottom);
        count.alignment = TextAlignmentOptions.Right;
        count.color = FaSteel;
        count.raycastTarget = false;

        // ④ 列表正中那句（暂无好友申请 / 黑名单是空的）—— 挂在 list 下（不是 Content 下）：不跟着滚
        TextMeshProUGUI empty = NewLabel(list, FpEmptyName, "", Vector2.zero, new Vector2(FaColW, 40f), 26f);
        RectTransform emptyRT = empty.rectTransform;
        emptyRT.anchorMin = new Vector2(0.5f, 0.5f);
        emptyRT.anchorMax = new Vector2(0.5f, 0.5f);
        emptyRT.pivot = new Vector2(0.5f, 0.5f);
        emptyRT.anchoredPosition = Vector2.zero;
        empty.alignment = TextAlignmentOptions.Center;
        empty.color = new Color32(142, 162, 180, 170);
        empty.raycastTarget = false;

        // ⑤ 名单 UI 挂在 Content 上（它就是「行的容器」）：高度按行数改 = ScrollRect 的可滚范围
        //    —— 与「好友列表」那份（BuildFriendListInto 第 ⑤ 步）同一处挂法；挂在行上比挂在大格子上少一层
        //    跨层：克隆出来的行直接在 ScrollRect.content 里，高度也直接写在它身上。
        var ui = listContent.gameObject.AddComponent<FriendPanelListUI>();
        ui.mode = mode;
        ui.rowTemplate = row;
        ui.scrollRect = scroll;
        ui.countText = count;
        ui.emptyText = empty;
        ui.maxRows = FriendRequestStore.MaxRequests;
        ui.rowGap = FaRowGap;

        // 这两格要读好友表（黑名单就是好友表里 flagged 的那些）—— 服务挂在大厅 Canvas 上
        Canvas canvas = content.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.gameObject.GetComponent<FriendListService>() == null)
            canvas.gameObject.AddComponent<FriendListService>();

        row.gameObject.SetActive(false);

        Debug.Log("[LobbyUI] 「" + (block ? "黑名单" : "申请列表") + "」已就位：列表 x" + FpColX + "..右让 " + FpColX +
                  " / 上沿 " + FpListTop + " / 下沿让 " + FpListBottom + "（行 " + FdRowW + "x" + FaRowH + " / 行距 " + FaRowGap +
                  "，底板复用 LobbyFriendRow）；右端动作 " + (block ? "取消拉黑 x" + FpActMainX : "同意 x" + FpActSubX + " + 拒绝 x" + FpActMainX) +
                  "；右下角「n/" + FriendRequestStore.MaxRequests + "」。数据与规则见 Scripts/UI/Lobby/FriendPanelListUI.cs。");
    }

    /// <summary>「申请列表 / 黑名单」的一行（模板）：底板 + 头像 + 名称 / 异界号 / 状态 + 右端一到两枚动作。</summary>
    /// <remarks>列宽 / 盒高 / 头像那几件**与「添加好友」那行用的是同一组 Fa* 常量** ——
    /// 用户要的「只在右边显示不同」在代码里就是这一句。</remarks>
    static FriendPanelRowUI BuildFriendPanelRow(RectTransform listContent, FriendPanelMode mode)
    {
        RectTransform rowRT = NewRect(listContent, FpRowName, AnchorTL, PivotTL, Vector2.zero, new Vector2(FdRowW, FaRowH));
        var row = rowRT.gameObject.AddComponent<FriendPanelRowUI>();
        row.mode = mode;

        // 三行文字各自在自己盒里竖直居中（盒心都落在行中线 y = -FaRowH/2 上）
        float nameY   = -(FaRowH * 0.5f - FaRowNameH * 0.5f);
        float idY     = -(FaRowH * 0.5f - FaRowIdH * 0.5f);
        float statusY = -(FaRowH * 0.5f - FaRowStatusH * 0.5f);

        RawImage plate = NewRaw(rowRT, "Plate", FdRowTex, AnchorTL, PivotTL,
                                new Vector2(-FaRowPlatePad, FaRowPlatePad),
                                new Vector2(FdRowW + FaRowPlatePad * 2f, FaRowH + FaRowPlatePad * 2f));
        plate.raycastTarget = false;           // 拖拽交给 Viewport 那张 α=0 的图

        RawImage ring = NewRaw(rowRT, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL,
                               new Vector2(FaRowRingX, -(FaRowH * 0.5f - FaRowRingSize * 0.5f)),
                               new Vector2(FaRowRingSize, FaRowRingSize));
        ring.raycastTarget = false;

        RectTransform avatarRT = NewRect(rowRT, "Avatar_Image", AnchorTL, PivotTL,
                                         new Vector2(FaRowRingX + FaRowAvatarInset, -(FaRowH * 0.5f - FaRowAvatarSize * 0.5f)),
                                         new Vector2(FaRowAvatarSize, FaRowAvatarSize));
        var avatar = avatarRT.gameObject.AddComponent<RawImage>();
        avatar.texture = null;                 // 运行时填：先灰盘，Steam 头像到货自己换
        avatar.raycastTarget = false;
        avatarRT.SetSiblingIndex(ring.transform.GetSiblingIndex() + 1);

        TextMeshProUGUI name = NewLabel(rowRT, "Text_Name", "名字",
                                        new Vector2(FaRowNameX, nameY), new Vector2(FaRowNameW, FaRowNameH), FaRowNameFS);
        name.alignment = TextAlignmentOptions.Left;
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.color = Cream;
        name.raycastTarget = false;

        TextMeshProUGUI id = NewLabel(rowRT, "Text_Id", "",
                                      new Vector2(FaRowIdX, idY), new Vector2(FaRowIdW, FaRowIdH), FaRowIdFS);
        id.alignment = TextAlignmentOptions.Left;
        id.overflowMode = TextOverflowModes.Ellipsis;
        id.color = FdSteel;
        id.raycastTarget = false;

        // 状态字的右沿要让开**这一格最左边那枚动作**：黑名单右边只有一颗「取消拉黑」(1296)，沿用
        // 「添加好友」那一档右沿（FaRowStatusR 1272）；申请列表右边是**两枚**（同意 1240 / 拒绝 1296），
        // 1272 会压到「同意」身上（2026-09-27 实测：状态只剩半个「离」字）—— 右沿收到 1228。
        // 让法沿用同一档间隙：右沿 + 12 = 动作左边（好友列表 1172 → 1184 就是这一档）。
        float statusR = mode == FriendPanelMode.Block ? FaRowStatusR : FpActSubX - 12f;
        TextMeshProUGUI status = NewLabel(rowRT, "Text_Status", "",
                                          new Vector2(statusR - FaRowStatusW, statusY),
                                          new Vector2(FaRowStatusW, FaRowStatusH), FaRowStatusFS);
        status.alignment = TextAlignmentOptions.Right;
        status.color = FaSteel;
        status.raycastTarget = false;

        // 右端动作：申请列表 = 勾（1240）+ 叉（1296）；黑名单 = 取消拉黑（1296）
        GameObject accept  = BuildPanelAct(rowRT, FpActAccept,  FpActSubX,  FpAcceptTex,  FpAcceptHov,  FriendRowActionKind.Accept,  row);
        GameObject refuse  = BuildPanelAct(rowRT, FpActRefuse,  FpActMainX, FpRefuseTex,  FpRefuseHov,  FriendRowActionKind.Refuse,  row);
        GameObject unblock = BuildPanelAct(rowRT, FpActUnblock, FpActMainX, FpUnblockTex, FpUnblockHov, FriendRowActionKind.Unblock, row);

        row.avatarImage = avatar;
        row.nameText = name;
        row.idText = id;
        row.statusText = status;
        row.actAccept = accept;
        row.actRefuse = refuse;
        row.actUnblock = unblock;
        return row;
    }

    /// <summary>行右端一枚小图标动作（与好友列表那三格同一套接线：不挂 Button，自己处理悬停 / 点击）。</summary>
    static GameObject BuildPanelAct(Transform rowRT, string name, float x, string tex, string hoverTex,
                                    FriendRowActionKind kind, FriendPanelRowUI row)
    {
        RectTransform rt = NewRect(rowRT, name, AnchorTL, PivotTL, new Vector2(x, FpActY),
                                   new Vector2(FpActSize, FpActSize));
        var icon = rt.gameObject.AddComponent<RawImage>();
        icon.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
        if (icon.texture == null) Debug.LogWarning("[LobbyUI] 找不到贴图：" + tex);
        icon.raycastTarget = true;

        var act = rt.gameObject.AddComponent<FriendRowAction>();
        act.kind = kind;
        act.icon = icon;
        act.normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
        act.hoverTexture  = AssetDatabase.LoadAssetAtPath<Texture2D>(hoverTex);
        act.panelRow = row;
        return rt.gameObject;
    }

    /// <summary>给 TMP_InputField.onValueChanged 接一条**持久**监听（与 WireClick 同一套口径：先清再挂）。</summary>
    static void WireInputChanged(TMP_InputField field, UnityAction<string> action)
    {
        if (field == null) return;
        while (field.onValueChanged.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(field.onValueChanged, 0);
        UnityEventTools.AddPersistentListener(field.onValueChanged, action);
    }


    /// <summary>「好友详情」全屏子弹窗的壳（内容用户还没说，先只出壳 + 右上角通用关闭叉）。
    /// 壳与 Panel_Battle 完全一致（BuildSubPanel：通用背景 CommonBack_A_clean + 关闭叉 Icon_Close）。</summary>
    [MenuItem("Tools/异界/大厅：生成「好友详情」子全屏弹窗（好友表头 + 的落点 · 占位）")]
    public static void BuildFriendDetailSubPanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);

        GameObject panel = BuildSubPanel(sub, hud.gameObject, FriendDetailPanelName, "好友详情", true, false);
        BuildFriendDetailContent(panel);            // 左侧四格 + 两条金线 + 右侧大格子（内容占位）
        WireFriendDetailEntry(canvas);              // 顺便把好友侧边栏表头那颗「+」接上
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbyUI] 已生成 " + FriendDetailPanelName + "（全屏子弹窗）：壳同 Panel_Battle（CommonBack_A_clean + 通用关闭叉 Icon_Close 右上角）；" +
                  "本面板不出标题与提示（withHeader:false），内容区 = Body_Content（只限位、不画东西）—— 详情页内容等用户后续说明；" +
                  "入口 = 好友侧边栏表头右端那颗「+」（" + FriendPlusName + "，点击走 LobbyIconHover.subPanel -> Toggle）。");
    }


    // ── 好友邀请（2026-09-27）──────────────────────────────────────────────────
    // 用户：「现在做邀请，好友栏中处于在线（非战斗状态和匹配状态）时其名字右边会出现一个加号，点击后发送邀请
    //       并且加号变成 10 秒倒计时（倒计时结束后才能继续邀请）（若自己此时不是在房间界面就进入房间并开房间），
    //       邀请是从屏幕中央上顶滑出一个小框，上面标题是收到邀请，下面一排是对应玩家头像和名称，在下面是有
    //       子背景的同意和拒绝，同意后会加入其房间，拒绝后对方也会收到：对方暂无法响应」。
    //
    // 两件东西：
    //   ① 好友行右端那一格 —— 建在 RowTemplate 里（所以名单里每一行克隆出来都带），运行时由 FriendRowUI
    //      按「在不在线 / 在不在冷却 / 悬没悬停」切三张贴图（加号 / 悬停 / 倒计时那块空板）。
    //   ② Panel_Invite —— 屏幕顶中的小窗，常驻 active 的根 + 存 inactive 的 Window（滑出 / 收窗都靠它），
    //      挂在 Layer_Hud_v1 下 ⇒ 永远压在房间面板等全屏弹窗之上（与 Panel_MatchWait 同一套层级约定）。
    //      插在 Text_LobbyToast **之前**：提示行在正中偏上、小窗也在正中偏上，提示得压得住它。
    const string InviteDir        = "Assets/_Game/Art/Sprites/Generated/invite-v1/";
    const string InvitePlusName   = "Icon_InvitePlus";
    const string InviteTimerName  = "Text_InviteTimer";
        const float  InvitePlusW      = 78f;     // 贴图 234（78x3）：横向长方形那一格（用户：「更像长方形，更扁一点」）
        const float  InvitePlusH      = 48f;     // 贴图 144（48x3）：与「踢出」子背景同高（LobbyChip_Kick）
    const float  InvitePlusRight  = 4f;      // 距行右沿
        const float  InvitePlusTop    = -18f;    // 行高 84，48 高这块上下各留 18
        const float  InviteNameTrim   = 94f;     // 名字那行要让给那一格的宽度（78 + 4 + 12 间隙）
        const float  InviteTimerFont  = 30f;

    const string InvitePanelName  = "Panel_Invite";
    const string InviteWindowName = "Window";
        const float  InviteWinW       = 420f;    // = Invite_Plate.png 贴图 1260 / 3（宽不变）
        const float  InviteWinH       = 144f;    // = 432 / 3 —— 标题栏去掉后再压一档（420:144 = 2.92:1）
    const float  InviteRestY      = -10f;    // 静止位：贴屏幕顶（与匹配小窗同档）
        const float  InviteRowY       = -12f;    // 头像行上沿（上留 12）
        const float  InviteRingSize   = 64f;     // 与好友行同口径（环 88 : 井 68 的比例）
        const float  InviteWellSize   = 50f;
        const float  InviteNameX      = 148f;
        const float  InviteRingX      = 70f;     // 头像环左沿（整行 [环 64 + 16 + 名字] 居中）
        const float  InviteNameW      = 264f;    // 148..412：正中 280 = 窗宽 420 的右 1/3 分界
        const float  InviteNameFont   = 26f;
        const float  InviteChipY      = -82f;
    const float  InviteChipW      = 96f;     // = 子背景 LobbyChip_Kick 的屏幕宽
    const float  InviteChipH      = 48f;
    const float  InviteChipFont   = 30f;
        const float  InviteChipAX     = 102f;    // 同意
        const float  InviteChipDX     = 222f;    // 拒绝

    // ── 确认删除 / 确认拉黑：长条弹窗（2026-09-27）──────────────────────────────
    // 用户：「删除好友和拉黑好友都有一个长子弹窗，上面是确认删除/拉黑（金色的好友名称），
    //        下面是有子背景的确认和取消」。
    // 长条比例：760x200 = 3.8:1 —— 比「收到邀请」小窗（420x144 = 2.92:1）再扁一档，
    //   因为这一块只有「一行标题 + 一排两个键」，没有头像行。
    // 层级：挂 Layer_Hud_v1（与 Panel_Invite / Panel_MatchWait 同级）—— 好友详情是全屏子弹窗，
    //   确认窗必须压在它之上。**不画遮罩**：用户定的「左上 / 右上在弹窗里仍显示」，
    //   一整块 Dim 会把头像与货币一起压黑。
    const string CfPanelName  = "Panel_Confirm";
    const string CfWindowName = "Window";
    const string CfTitleName  = "Text_Title";
    const string CfChipsName  = "Chips";
    const string CfPlateTex   = UiDir + "LobbyConfirmPlate.png";
        const float  CfWinW          = 760f;     // = LobbyConfirmPlate.png 贴图 760（存 1:1，不除 3）
        const float  CfWinH          = 200f;
        const float  CfTitleX        = 40f;      // 标题左让（宽 = 窗宽 - 80）
        const float  CfTitleY        = -36f;
        const float  CfTitleH        = 56f;      // ★ 必须容得下整行：34 号的实测行高 ~48.9，
                                                 //   盒子矮于它 + Ellipsis = 一个字都不画（好友行那次的坑）
        const float  CfTitleFS       = 34f;
        const float  CfChipY         = -124f;    // 两键 96x48 @ y124 -> 下留 28
    const float  CfChipW         = 96f;      // = 子背景 LobbyChip_Kick 的屏幕宽（与同意 / 拒绝同规格）
    const float  CfChipH         = 48f;
    const float  CfChipFS        = 30f;
        const float  CfChipConfirmX = 272f;      // 确认
        const float  CfChipCancelX  = 392f;      // 取消（整排 272..488 居中于 380 = 窗宽一半）

    /// <summary>
    /// 给好友行的行模板补上右端那格「邀请加号」（幂等：已经有了就只回填引用）。
    /// 顺手把名字那行收窄 —— 它原来铺到行右端，加号一进来就会压在字上。
    /// </summary>
    static GameObject WireInvitePlus(RectTransform rowRT, FriendRowUI row, float rowW)
    {
        float plusX = rowW - InvitePlusW - InvitePlusRight;
        string normalPath = UiDir + InvitePlusName + ".png";
        string hoverPath  = UiDir + InvitePlusName + "Hover.png";
        string coolPath   = UiDir + InvitePlusName + "Cool.png";

        Transform old = rowRT.Find(InvitePlusName);
        RawImage icon;
        GameObject go;
        if (old != null)
        {
            go = old.gameObject;
            icon = go.GetComponent<RawImage>();
            if (icon == null) icon = go.AddComponent<RawImage>();
            icon.rectTransform.anchorMin = AnchorTL;
            icon.rectTransform.anchorMax = AnchorTL;
            icon.rectTransform.pivot = PivotTL;
            icon.rectTransform.anchoredPosition = new Vector2(plusX, InvitePlusTop);
            icon.rectTransform.sizeDelta = new Vector2(InvitePlusW, InvitePlusH);
        }
        else
        {
            icon = NewRaw(rowRT, InvitePlusName, normalPath, AnchorTL, PivotTL,
                          new Vector2(plusX, InvitePlusTop), new Vector2(InvitePlusW, InvitePlusH));
            go = icon.gameObject;
            Undo.RegisterCreatedObjectUndo(go, "建 " + InvitePlusName);
        }
        icon.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        icon.raycastTarget = true;                 // 它自己就是 raycast 目标（同那几个压墙图标）
        go.transform.SetAsLastSibling();           // 行内最后：画在其余件之上，点击也先命中它

        // 倒计时数字：铺满那一格、居中、亮金（常态藏着，冷却那 10 秒才出来）
        Transform oldT = go.transform.Find(InviteTimerName);
        TextMeshProUGUI timer;
        if (oldT != null)
        {
            timer = oldT.GetComponent<TextMeshProUGUI>();
            if (timer == null) { Undo.DestroyObjectImmediate(oldT.gameObject); timer = null; }
        }
        else timer = null;
        if (timer == null)
        {
            timer = NewLabel(go.transform, InviteTimerName, "10", Vector2.zero, new Vector2(InvitePlusW, InvitePlusH), InviteTimerFont);
            var trt = timer.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.pivot = PivotC;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
        }
        timer.alignment = TextAlignmentOptions.Midline;   // 居中 = 正好压在空板中央
        timer.color = GoldBright;                         // 本套亮金 #E4CB84
        timer.fontSize = InviteTimerFont;
        timer.raycastTarget = false;                      // 别抢这块的点击（点击归 FriendInviteButton）

        var btn = go.GetComponent<FriendInviteButton>();
        if (btn == null) btn = go.AddComponent<FriendInviteButton>();
        btn.icon = icon;
        btn.owner = row;

        row.inviteGroup  = go;
        row.inviteIcon   = icon;
        row.inviteTimer  = timer;
        row.inviteNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        row.inviteHover  = AssetDatabase.LoadAssetAtPath<Texture2D>(hoverPath);
        row.inviteCool   = AssetDatabase.LoadAssetAtPath<Texture2D>(coolPath);
        if (row.inviteHover == null) Debug.LogWarning("[LobbyUI] 找不到 " + hoverPath + " —— 加号没有悬停态");
        if (row.inviteCool == null)  Debug.LogWarning("[LobbyUI] 找不到 " + coolPath  + " —— 倒计时没有空板");

        Transform nameT = rowRT.Find("Text_Name");
        if (nameT != null)
        {
            var nrt = nameT as RectTransform;
            if (nrt != null) nrt.sizeDelta = new Vector2(rowW - 96f - InviteNameTrim, 40f);
        }
        if (timer != null) timer.gameObject.SetActive(false);
        return go;
    }

    /// <summary>补丁式：给场景里**已经存在**的好友行模板补加号（重复执行幂等）。</summary>
    [MenuItem("Tools/异界/大厅：给好友行补「邀请加号」（好友邀请）")]
    public static void AddInvitePlusMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);

        Transform rowT = FindDeep(sub, "RowTemplate");
        if (rowT == null) { Debug.LogError("[LobbyUI] 找不到 Panel_Friends 的 RowTemplate —— 先跑一次「生成好友侧边栏」"); return; }
        var rowRT = rowT as RectTransform;
        var row = rowT.GetComponent<FriendRowUI>();
        if (rowRT == null || row == null) { Debug.LogError("[LobbyUI] RowTemplate 上没有 FriendRowUI / RectTransform"); return; }

        float rowW = rowRT.sizeDelta.x;
        WireInvitePlus(rowRT, row, rowW);

        if (canvas.gameObject.GetComponent<LobbyInviteService>() == null)
            canvas.gameObject.AddComponent<LobbyInviteService>();

        EditorUtility.SetDirty(row);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = rowT.gameObject;
        Debug.Log("[LobbyUI] 好友行已补上「邀请加号」：行宽 " + rowW + " · 那一格 " + InvitePlusW +
                  "x" + InvitePlusH + " 在 x=" + (rowW - InvitePlusW - InvitePlusRight) + " y=" + InvitePlusTop +
                  "（行内最后 = 压在其余件之上）· 名字那行已收窄到 " + (rowW - 96f - InviteNameTrim) + "。");
    }

    /// <summary>生成「收到邀请」小窗（Panel_Invite）：锚屏幕顶中，滑出 / 收窗由 LobbyInvitePanel 驱动。</summary>
    [MenuItem("Tools/异界/大厅：生成「收到邀请」小窗（好友邀请）")]
    public static void BuildInvitePanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);

        Transform old = hud.Find(InvitePanelName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // 根：常驻 active（它的 Awake 要把 Instance 立起来，服务才找得到它）；滑动的是子物体 Window
        RectTransform root = NewRect(hud, InvitePanelName, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                     Vector2.zero, new Vector2(InviteWinW, InviteWinH));
        var panel = root.gameObject.AddComponent<LobbyInvitePanel>();

        RectTransform win = NewRect(root, InviteWindowName, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                    new Vector2(0f, InviteRestY), new Vector2(InviteWinW, InviteWinH));
        panel.window = win.gameObject;
        panel.windowRect = win;
        panel.restY = InviteRestY;
        panel.hiddenY = InviteWinH + 20f;      // 整块完全在屏幕顶之上
        panel.toastDrop = InviteWinH + 10f;    // 提示行让开小窗（窗高变了这里要跟着变）

        // 底板：整块拉伸铺满（贴图 1080x720 = 屏幕 360x240，正好 1:1）；raycastTarget 开着 = 小窗自己吃点击
        RawImage plate = NewRaw(win, "Plate", InviteDir + "Invite_Plate.png", Vector2.zero, PivotC, Vector2.zero, Vector2.zero);
        plate.rectTransform.anchorMin = Vector2.zero;
        plate.rectTransform.anchorMax = Vector2.one;
        plate.rectTransform.offsetMin = Vector2.zero;
        plate.rectTransform.offsetMax = Vector2.zero;
        plate.raycastTarget = true;

        // 标题栏已取消（用户 2026-09-27：「不要收到邀请了」）—— 省下的高度直接压扁窗户，见 InviteWinH。

        // 一排：对方头像（环 72 + 井 56）+ 名称
        RawImage ring = NewRaw(win, "Avatar_Ring", UiDir + "LobbyAvatarRing.png", AnchorTL, PivotTL,
                               new Vector2(InviteRingX, InviteRowY), new Vector2(InviteRingSize, InviteRingSize));
        ring.raycastTarget = false;
        var wellRT = NewRect(win, "Avatar_Image", AnchorTL, PivotTL,
                             new Vector2(InviteRingX + (InviteRingSize - InviteWellSize) * 0.5f, InviteRowY - (InviteRingSize - InviteWellSize) * 0.5f), new Vector2(InviteWellSize, InviteWellSize));
        var well = wellRT.gameObject.AddComponent<RawImage>();
        well.texture = null;                              // 运行时填（先灰盘占位，Steam 头像到货自己换）
        well.raycastTarget = false;
        wellRT.SetSiblingIndex(ring.transform.GetSiblingIndex() + 1);
        panel.avatarImage = well;

        TextMeshProUGUI who = NewLabel(win, "Text_Name", "好友",
                                       new Vector2(InviteNameX, InviteRowY - (InviteRingSize - 40f) * 0.5f), new Vector2(InviteNameW, 40f), InviteNameFont);
        who.alignment = TextAlignmentOptions.Center;   // 用户：「文字调整至以右 1/3 处为中心对齐」= 280
        who.color = Cream;
        who.raycastTarget = false;
        panel.nameText = who;

        // 同意 / 拒绝：各带子背景（复用踢出那套 LobbyChip_Kick，同规格 96x48 / 字 30）
        RectTransform chips = NewRect(win, "Chips", AnchorTL, PivotTL,
                                      new Vector2(0f, InviteChipY), new Vector2(InviteWinW, InviteChipH));
        panel.chipsGroup = chips.gameObject;
        Button accept = BuildTextChip(chips, "Btn_Accept", "同意", AnchorTL, PivotTL,
                                      new Vector2(InviteChipAX, 0f), new Vector2(InviteChipW, InviteChipH),
                                      InviteChipFont, "LobbyChip_Kick.png", "LobbyChip_KickHover.png");
        Button decline = BuildTextChip(chips, "Btn_Decline", "拒绝", AnchorTL, PivotTL,
                                       new Vector2(InviteChipDX, 0f), new Vector2(InviteChipW, InviteChipH),
                                       InviteChipFont, "LobbyChip_Kick.png", "LobbyChip_KickHover.png");
        panel.acceptButton = accept;
        panel.declineButton = decline;
        WireClick(accept, panel.Accept);
        WireClick(decline, panel.Decline);

        // 收邀请的回调 / 冷却表都在服务上；服务挂大厅 Canvas（重跑本菜单别重建它）
        if (canvas.gameObject.GetComponent<LobbyInviteService>() == null)
            canvas.gameObject.AddComponent<LobbyInviteService>();

        // 插在提示行**之前**：提示（「对方暂无法响应」/「已加入…」）压在小窗之上
        Transform toast = FindDeep(hud, "Text_LobbyToast");
        if (toast != null) root.SetSiblingIndex(toast.GetSiblingIndex());
        else root.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[LobbyUI] 已生成 " + InvitePanelName + "（收到邀请小窗）：屏幕顶中 " + InviteWinW + "x" + InviteWinH +
"（贴图 Invite_Plate.png 1260x432 / 3）· 静止位 y=" + InviteRestY + "、起点 y=" + (InviteWinH + 20f) +
                  "（从屏幕顶滑出）· 名字居中于右 1/3（名栏 148..412）· 头像行（环 " + InviteRingSize + " / 井 " + InviteWellSize + "）+ 同意 / 拒绝（子背景 96x48）· " +
                  "挂 " + HudLayerName + "（压在房间面板等全屏弹窗之上）。");
    }

    /// <summary>「确认删除 / 确认拉黑」长条弹窗（2026-09-27）：一行标题（动词 + 金色的好友名）+ 一排
    /// 两个带子背景的键。幂等：先把旧的 Panel_Confirm 收掉再建。</summary>
    [MenuItem("Tools/异界/大厅：生成「确认删除 / 拉黑」长条弹窗（占位）")]
    public static void BuildConfirmPanelMenu()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[LobbyUI] 当前场景没有 Canvas —— 请先打开 Assets/_Game/Scenes/Lobby.unity"); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[LobbyUI] 找不到字体 {FontPath}，中文会落到 TMP 默认字体");

        Transform sub, hud;
        EnsureUiLayers(canvas, out sub, out hud);

        Transform old = hud.Find(CfPanelName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // 根：常驻 active（Awake 要把 Instance 立起来，好友行才找得到它）
        RectTransform root = NewRect(hud, CfPanelName, AnchorC, PivotC, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        var dlg = root.gameObject.AddComponent<LobbyConfirmDialog>();

        // 视觉根：**存成 active**（方便在编辑器里直接看版式 / 拖位置），运行时 Awake 第一帧自己收掉
        RectTransform win = NewRect(root, CfWindowName, AnchorC, PivotC, Vector2.zero, new Vector2(CfWinW, CfWinH));
        dlg.window = win.gameObject;

        // 底板：贴图 760x200 存成 1:1，所以 sizeDelta 直接用屏幕尺寸，不除 3
        RawImage plate = NewRaw(win, "Plate", CfPlateTex, AnchorC, PivotC, Vector2.zero, new Vector2(CfWinW, CfWinH));
        plate.raycastTarget = true;          // 窗自己吃掉点击（别点穿到底下的名单）

        // 标题：一条富文本 —— 动词奶油、好友名亮金（长度不定，交给 TMP 自己居中）
        TextMeshProUGUI title = NewLabel(win, CfTitleName, "确认删除 <color=#E4CB84>好友名</color>",
                                         new Vector2(CfTitleX, CfTitleY),
                                         new Vector2(CfWinW - CfTitleX * 2f, CfTitleH), CfTitleFS);
        title.alignment = TextAlignmentOptions.Center;
        title.overflowMode = TextOverflowModes.Ellipsis;   // 名字太长就截断（盒子 56 高，容得下整行）
        title.richText = true;
        title.color = new Color32(240, 232, 210, 236);     // 奶油 #F0E8D2
        title.raycastTarget = false;
        dlg.titleText = title;

        RectTransform chips = NewRect(win, CfChipsName, AnchorTL, PivotTL,
                                      new Vector2(0f, CfChipY), new Vector2(CfWinW, CfChipH));
        Button ok = BuildTextChip(chips, "Btn_Confirm", "确认", AnchorTL, PivotTL,
                                  new Vector2(CfChipConfirmX, 0f), new Vector2(CfChipW, CfChipH),
                                  CfChipFS, "LobbyChip_Kick.png", "LobbyChip_KickHover.png");
        Button no = BuildTextChip(chips, "Btn_Cancel", "取消", AnchorTL, PivotTL,
                                  new Vector2(CfChipCancelX, 0f), new Vector2(CfChipW, CfChipH),
                                  CfChipFS, "LobbyChip_Kick.png", "LobbyChip_KickHover.png");
        dlg.confirmButton = ok;
        dlg.cancelButton = no;
        WireClick(ok, dlg.Confirm);
        WireClick(no, dlg.Cancel);

        // 插在提示行**之前**：提示行也在屏幕中央，压得住它（与邀请小窗同一条）
        Transform toast = FindDeep(hud, "Text_LobbyToast");
        if (toast != null) root.SetSiblingIndex(toast.GetSiblingIndex());
        else root.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[LobbyUI] 已生成 " + CfPanelName + "（确认删除 / 拉黑长条弹窗）：屏幕居中 " + CfWinW + "x" + CfWinH +
                  "（" + (CfWinW / CfWinH).ToString("0.00") + ":1，贴图 " + CfPlateTex + " 存 1:1）· " +
                  "标题一行 " + CfTitleFS + " 号（动词奶油 + 好友名亮金 #E4CB84，一条富文本）· " +
                  "确认 / 取消 = LobbyChip_Kick 子背景 " + CfChipW + "x" + CfChipH + " @ x=" + CfChipConfirmX + " / " + CfChipCancelX +
                  " · 挂 " + HudLayerName + "（压在好友详情等全屏弹窗之上）· 不画遮罩。" +
                  "位置 / 尺寸改 Cf* 常量，贴图由 Tools/cardframe/LobbyConfirmPlateV1.ps1 出。");
    }
}
