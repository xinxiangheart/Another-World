using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// LobbyBgV2Builder —— 一键把大厅背景 v2（纯星野 + 左侧星环，三层视差）铺进 Lobby.unity，2026-09-26。
///
/// 口径 = Tools/cardframe/LobbyBgV2.ps1（唯一版式口径，那边改一个数这边也要改）与
/// Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/README.md 的「摆位 / 溢出」两节。
///
/// 节点树（挂在已有的 LobbyUI_v1 下，sibling index 1 —— 压在 Ref_Backdrop（安全网）之上、其它一切之下）：
///   Bg_v2                        (LobbyBgParallax：三层视差；全屏空容器，不画东西)
///     ├─ Far                     (RawImage Bg_Far)  全拉伸 + localScale 1.08   depth 0.25
///     ├─ Ring                    (LobbyRingNodes)   中心锚，792x792，中心 = 屏 (560,540)   depth 0.55
///     │    ├─ Base               (RawImage Ring_Base) 792x792
///     │    ├─ Glow_00..Glow_11   (RawImage Ring_NodeGlow) 112x112 —— 全部先于 Node 建，渲染在点之下
///     │    └─ Node_00..Node_11   (RawImage Ring_Node)     52x52，半径 350，屏角 -80 + 30i
///     └─ Near                    (RawImage Bg_Near)  全拉伸 + localScale 1.08   depth 1.00
///
/// 坐标换算：脚本 / README 的屏口径是「左上原点、y 向下」，Unity 的 anchoredPosition 是「中心原点、y 向上」，
/// 所以 x 取 (屏x - 960)、y 取 -(屏y - 540)；角度同理取负（屏 -80 度 -> Unity +80 度，即正上方偏右）。
///
/// 远景 / 近景 2074x1166 是屏的 1.08 倍：这里用「全拉伸 + localScale 1.08」而不是固定尺寸 ——
/// 16:9 下正好 1 贴图像素 = 1 屏像素，换比例也自动铺满；四边各 77 屏 px 余量 > depth 1 的最大位移 57.6px。
///
/// 重跑：先删同名 Bg_v2 再重建；不动 Ref_Backdrop，也不动场景里的任何其它节点。
/// Ref_Emblem（中央棋盘徽记）—— 见文件末尾的说明，本菜单会把它关掉，随时可以用下面的菜单项切回来。
/// </summary>
public static class LobbyBgV2Builder
{
    const string RootName = "Bg_v2";
    const string ParentName = "LobbyUI_v1";
    const string BgDir = "Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/";
    const string EmblemName = "Ref_Emblem";

    // 设计屏 = Canvas 参考分辨率（ScaleWithScreenSize / MatchWidthOrHeight = 1，匹配高度）
    const float DesignW = 1920f;
    const float DesignH = 1080f;

    // 远景 / 近景的出图倍率（LobbyBgV2.ps1 的 $OV）
    const float Over = 1.08f;

    // 星环 —— 与 LobbyBgV2.ps1 的 $RING_* 一一对应
    const float RingCx = 560f;          // 环心屏 x（$RING_CX）
    const float RingCy = 540f;          // 环心屏 y（$RING_CY）
    const float RingSide = 792f;        // Ring_Base 显示边长 = ($RING_R + $RING_PAD) * 2
    const float NodeRadius = 350f;      // 点所在半径（$RING_R）
    const float NodeSize = 52f;         // Ring_Node 显示尺寸
    const float GlowSize = 112f;        // Ring_NodeGlow 显示尺寸
    const int   NodeCount = 12;
    const float NodeA0 = -80f;          // 第一颗的屏角（正上方偏右 10 度）
    const float NodeStep = 30f;         // 每颗 30 度，顺时针
    // 默认不常亮 —— 12 颗全部参与「自身缓慢闪烁」；要「常亮若干颗」就调 Ring 的 LobbyRingNodes.litCount

    static readonly Vector2 AnchorC = new Vector2(0.5f, 0.5f);
    static readonly Vector2 PivotC = new Vector2(0.5f, 0.5f);

    [MenuItem("Tools/异界/生成大厅背景 v2（占位）")]
    public static void BuildBg()
    {
        GameObject parentGo = GameObject.Find(ParentName);
        if (parentGo == null)
        {
            Debug.LogError("[LobbyBg] 当前场景找不到 " + ParentName + " —— 先打开 Assets/_Game/Scenes/Lobby.unity，跑一次「生成大厅 UI v1（占位）」");
            return;
        }
        Transform parent = parentGo.transform;

        Transform previous = parent.Find(RootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);

        var root = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "生成大厅背景 v2");
        var rootRT = (RectTransform)root.transform;
        rootRT.SetParent(parent, false);
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        rootRT.pivot = PivotC;
        rootRT.anchoredPosition = Vector2.zero;
        rootRT.SetSiblingIndex(Mathf.Min(1, parent.childCount - 1));

        // ── 1 远景 ───────────────────────────────────────────────────────────
        RawImage far = NewLayer(rootRT, "Far", BgDir + "Bg_Far.png");

        // ── 2 星环（容器 + 骨架 + 12 点 + 12 辉光）─────────────────────────────
        var ringGo = new GameObject("Ring", typeof(RectTransform));
        var ringRT = (RectTransform)ringGo.transform;
        ringRT.SetParent(rootRT, false);
        ringRT.anchorMin = AnchorC;
        ringRT.anchorMax = AnchorC;
        ringRT.pivot = PivotC;
        ringRT.sizeDelta = new Vector2(RingSide, RingSide);
        ringRT.anchoredPosition = new Vector2(RingCx - DesignW * 0.5f, -(RingCy - DesignH * 0.5f));

        NewRaw(ringRT, "Base", BgDir + "Ring_Base.png", new Vector2(RingSide, RingSide));

        var nodes = new RawImage[NodeCount];
        var glows = new RawImage[NodeCount];

        // 辉光全部先建：同名下标要渲染在点之下，所以整批放在点的前面
        for (int i = 0; i < NodeCount; i++)
        {
            glows[i] = NewRaw(ringRT, "Glow_" + i.ToString("00"), BgDir + "Ring_NodeGlow.png", new Vector2(GlowSize, GlowSize));
            glows[i].rectTransform.anchoredPosition = NodePos(i);
        }
        for (int i = 0; i < NodeCount; i++)
        {
            nodes[i] = NewRaw(ringRT, "Node_" + i.ToString("00"), BgDir + "Ring_Node.png", new Vector2(NodeSize, NodeSize));
            nodes[i].rectTransform.anchoredPosition = NodePos(i);
        }

        var ring = ringGo.AddComponent<LobbyRingNodes>();
        ring.nodes = nodes;
        ring.glows = glows;
        ring.litCount = 0;                  // 常亮 0 颗 —— 12 颗全部参与闪烁
        ring.twinkleOn = true;
        ring.twinklePeriod = 6f;            // 一颗自己亮一次 6 秒（越大越慢）
        ring.twinklePhaseStep = 1f / 12f;   // 相邻错开 1/12 周期 -> 整道波 6 秒绕环一圈
        ring.twinkleMin = 0.06f;            // 最暗快熄灭 -> 最亮满亮
        ring.twinkleMax = 1f;
        ring.previewInEditMode = false;     // 要在 Scene 视图里直接看闪动就勾上（不进 Play）
        ring.Apply();       // 静态场景里是「相位 0」的一条亮度渐变带，不用进 Play

        // ── 3 近景 ───────────────────────────────────────────────────────────
        RawImage near = NewLayer(rootRT, "Near", BgDir + "Bg_Near.png");

        // ── 4 视差（只有挂上 Bg_v2 的这一层才动）──────────────────────────────
        // 2026-09-26 七次定：把「背景」和「星环」的档位拉开 —— 背景几乎不跟手、环最明显。
        // 折算到 1920×1080（parallaxMax 0.030）：Far ≈ 5.8px、Near ≈ 17.3px、Ring ≈ 49.0px。
        var parallax = root.AddComponent<LobbyBgParallax>();
        parallax.layers = new List<LobbyBgParallax.Layer>();
        parallax.layers.Add(new LobbyBgParallax.Layer { rect = far.rectTransform, depth = 0.10f });   // 背景底
        parallax.layers.Add(new LobbyBgParallax.Layer { rect = ringRT, depth = 0.85f });              // 星环（最大）
        parallax.layers.Add(new LobbyBgParallax.Layer { rect = near.rectTransform, depth = 0.30f });  // 背景浮尘

        // ── 5 旧徽记：中央棋盘徽记与星野同框是「两套圆环叠一起」，默认关掉 ──────
        HideEmblemIfActive(parent);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[LobbyBg] 已在 " + ParentName + " 下生成 " + RootName + "：Far / Ring(12 点 + 12 辉光) / Near。" +
                  "三层视差挂 Bg_v2（depth 0.25 / 0.55 / 1.00），环的闪烁改 Ring 的 LobbyRingNodes（twinklePeriod / twinklePhaseStep / twinkleMin，场景里直接看得到）；" +
                  "环心 / 大小改 ringRT 的 anchoredPosition / sizeDelta 与 NodeRadius（口径在 Tools/cardframe/LobbyBgV2.ps1 的 $RING_*）。");
    }

    [MenuItem("Tools/异界/删除大厅背景 v2")]
    public static void DeleteBg()
    {
        GameObject parentGo = GameObject.Find(ParentName);
        if (parentGo == null) { Debug.LogWarning("[LobbyBg] 当前场景找不到 " + ParentName); return; }
        Transform previous = parentGo.transform.Find(RootName);
        if (previous == null) { Debug.Log("[LobbyBg] 没有 " + ParentName + "/" + RootName + "，无需删除"); return; }
        Undo.DestroyObjectImmediate(previous.gameObject);
        EditorSceneManager.MarkSceneDirty(parentGo.scene);
        Debug.Log("[LobbyBg] 已删除 " + ParentName + "/" + RootName + "（Ref_Backdrop 还在，画面不会空）");
    }

    [MenuItem("Tools/异界/大厅：切换旧徽记 Ref_Emblem")]
    public static void ToggleEmblem()
    {
        GameObject parentGo = GameObject.Find(ParentName);
        if (parentGo == null) { Debug.LogWarning("[LobbyBg] 当前场景找不到 " + ParentName); return; }
        Transform emblem = parentGo.transform.Find(EmblemName);
        if (emblem == null) { Debug.LogWarning("[LobbyBg] 没找到 " + ParentName + "/" + EmblemName); return; }
        bool next = !emblem.gameObject.activeSelf;
        Undo.RecordObject(emblem.gameObject, "切换大厅旧徽记");
        emblem.gameObject.SetActive(next);
        EditorSceneManager.MarkSceneDirty(emblem.gameObject.scene);
        Debug.Log("[LobbyBg] " + EmblemName + (next ? " 已显示" : " 已隐藏") +
                  (next ? "（会和左侧星环叠成两套圆环 —— 要看合不合再决定）" : ""));
    }

    /// <summary>中央棋盘徽记默认关掉：它是「中央大圆环 + 六芒星」那套母题，和左侧新星环同框就是两套圆环叠加。
    /// 只改 active，不删；要回来点菜单「大厅：切换旧徽记 Ref_Emblem」。</summary>
    static void HideEmblemIfActive(Transform parent)
    {
        Transform emblem = parent.Find(EmblemName);
        if (emblem == null || !emblem.gameObject.activeSelf) return;
        Undo.RecordObject(emblem.gameObject, "隐藏大厅旧徽记");
        emblem.gameObject.SetActive(false);
        Debug.Log("[LobbyBg] 顺手关掉了 " + ParentName + "/" + EmblemName + "（棋盘徽记，与新星环同框会叠成两套圆环）；要切回来点菜单 Tools/异界/大厅：切换旧徽记 Ref_Emblem");
    }

    /// <summary>屏角 -> Unity anchoredPosition（屏口径 y 向下，Unity y 向上，所以 y 取负）。</summary>
    static Vector2 NodePos(int i)
    {
        float a = (NodeA0 + NodeStep * i) * Mathf.Deg2Rad;
        return new Vector2(NodeRadius * Mathf.Cos(a), -NodeRadius * Mathf.Sin(a));
    }

    /// <summary>全屏层：全拉伸 + localScale = 出图倍率。Raycast 一律不吃（背景不参与点击）。</summary>
    static RawImage NewLayer(Transform parent, string name, string texturePath)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = PivotC;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * Over;
        var image = go.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (image.texture == null) Debug.LogWarning("[LobbyBg] 找不到贴图：" + texturePath);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>中心锚的定尺 RawImage（环骨架 / 点 / 辉光）。</summary>
    static RawImage NewRaw(Transform parent, string name, string texturePath, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = AnchorC;
        rt.anchorMax = AnchorC;
        rt.pivot = PivotC;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var image = go.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (image.texture == null) Debug.LogWarning("[LobbyBg] 找不到贴图：" + texturePath);
        image.raycastTarget = false;
        return image;
    }
}