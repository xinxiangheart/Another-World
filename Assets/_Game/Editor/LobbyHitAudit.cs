using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>大厅 UI 命中体检：把「点了没反应」这类问题（被上层装饰图吃掉点击）直接列出来。
///
/// 来由（2026-09-28）：好友侧边栏表头那颗「+」（Icon_FriendPlus，Layer_Sub_v1）落在左上头像衬托板
/// Plate_Profile（Layer_Hud_v1，467x96，raycastTarget = 1）的矩形里 —— Unity 的命中**不看 alpha**，
/// 整块矩形都吃点击，于是 44x44 的「+」只有探出板下沿的那几 px 能点，用户原话是
/// 「好友 ui 只有鼠标在 ui 底部时才允许点击」；右上角那几个叉同样被 Plate_TopBand 吃掉上面约 29px。
///
/// 口径（**不依赖画布渲染状态**）：真 Graphic 列表 + 层级次序（画得晚的在上）+ 矩形包含 +
/// RaycastTarget + RectMask2D 裁切。**为什么不直接调 GraphicRaycaster**：它的命中里有
/// `graphic.depth == -1 ⇒ 跳过`（画布没重绘时全是 -1）与 `canvasRenderer.cull` 两条，编辑器里
/// 一旦游戏视图没重绘就整屏打空 —— 2026-09-28 实测 Stage76/Stage78 两版探针都栽在这，模型没这两条。
///
/// 判读注意：面板在场景里都存成 active（运行时由脚本自己关，见 LobbySubPanel.closeOnStart 那套约定），
/// 所以「被吃掉」里要盯的是遮它的那块**常驻**件（Layer_Hud_v1 的 Plate_Profile / Plate_TopBand /
/// Text_* / Icon_*）；遮它的是另一个子弹窗的底板（Part [Layer_Sub_v1]）多半是编辑器态叠加。</summary>
public static class LobbyHitAudit
{
    const string ScenePath = "Assets/_Game/Scenes/Lobby.unity";

    static Transform _root;

    [UnityEditor.MenuItem("Tools/异界/大厅 UI 命中体检")]
    static void MenuRun() { Debug.Log(Run()); }

    [UnityEditor.MenuItem("Tools/异界/大厅 UI 命中体检（全量，含子弹窗叠加）")]
    static void MenuRunFull() { Debug.Log(RunFull()); }

    /// <summary>只看**常驻 HUD**（Layer_Hud_v1 里不叫 Panel_* / Popup_* 的那几块：头像衬托板、
    /// 右上横栏、左下 ID 行）有没有吃掉谁 —— 这是「点了没反应」唯一会真出问题的来源。</summary>
    public static string Run() { return RunInternal(true); }

    /// <summary>全量：把子弹窗底板也算进来。面板在场景里都存成 active，所以结果噪声大，只用来排查「两个子弹窗之间」。</summary>
    public static string RunFull() { return RunInternal(false); }

    static string RunInternal(bool permanentOnly)
    {
        var sb = new StringBuilder();
        Canvas canvas = FindLobbyCanvas();
        if (canvas == null) return "[命中体检] 找不到大厅 Canvas —— 先打开 " + ScenePath;
        _root = canvas.transform;

        var all = new List<Graphic>(256);
        var gs = canvas.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < gs.Length; i++) if (gs[i] != null) all.Add(gs[i]);

        sb.AppendLine("【大厅 UI 命中体检】" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Canvas = " + canvas.name + " · renderMode = " + canvas.renderMode + " · pixelRect = " + canvas.pixelRect +
                      " · Graphic 共 " + all.Count + " 个");
        sb.AppendLine(permanentOnly
            ? "口径：只算**常驻 HUD**（Layer_Hud_v1 里不叫 Panel_* / Popup_* 的那几块）当遮挡物；取每个点击件自己 Graphic 的 5 个采样点（中心 + 四向内缩 40%）。"
            : "口径：全量（含 Layer_Hud_v1 里那些运行时会自关的子弹窗底板 —— 场景里它们都是 active，噪声大）；5 个采样点同上。");
        sb.AppendLine();

        var rows = new List<string>();
        int total = 0, bad = 0;

        var mbs = Object.FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < mbs.Length; i++)
        {
            MonoBehaviour mb = mbs[i];
            if (mb == null) continue;
            if (!(mb is IPointerClickHandler) && !(mb is IPointerDownHandler)) continue;
            GameObject go = mb.gameObject;
            Graphic g = go.GetComponent<Graphic>();
            if (g == null) g = go.GetComponentInChildren<Graphic>(true);
            if (g == null || g.rectTransform == null) continue;

            total++;
            int blocked = 0, excused = 0;
            string worst = null;
            // 本件所属的子弹窗（没有就是常驻件）：它开窗时 hideOnOpen / hideHudOnOpen 会把遮挡物藏掉，那就不算遮挡
            LobbySubPanel owner = mb.GetComponentInParent<LobbySubPanel>(true);
            for (int k = 0; k < 5; k++)
            {
                Graphic b = TopBlocker(all, g, SampleWorld(g, k), permanentOnly, owner);
                excused += _hiddenHits;
                if (b != null) { blocked++; if (worst == null) worst = LayerTag(b) + "/" + b.name; }
            }
            if (blocked > 0) bad++;
            rows.Add(string.Format("  {0,-12} {1,-24} {2,-22} 5 点中 {3} 点被盖 · 最上面那块 = {4}{5}",
                blocked == 0 ? (g.raycastTarget ? (excused > 0 ? "OK(开窗藏)" : "OK") : "自关") : "被吃掉",
                go.name, mb.GetType().Name, blocked, blocked == 0 ? "—" : worst,
                excused > 0 ? "（另有 " + excused + " 次是开窗时会被 hideOnOpen 藏掉的件）" : ""));
        }

        rows.Sort(System.StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++) sb.AppendLine(rows[i]);
        sb.AppendLine();
        sb.AppendLine("合计 " + total + " 个点击件，其中 " + bad + " 个至少有一个采样点被盖住。");

        sb.AppendLine();
        sb.AppendLine("── 采样自检（中心点必须落在自己矩形内；depth 在编辑器里可能是 -1，故只作参考）──");
        foreach (string nm in new[] { "Plate_Profile", "Plate_TopBand", "Icon_Mail", "Icon_Gear", "Icon_Friend", "Icon_FriendPlus" })
        {
            Transform t = FindDeep(_root, nm);
            if (t == null) { sb.AppendLine("  " + nm + " = 找不到"); continue; }
            Graphic g = t.GetComponent<Graphic>();
            if (g == null) g = t.GetComponentInChildren<Graphic>(true);
            if (g == null) { sb.AppendLine("  " + nm + " = 无 Graphic"); continue; }
            Vector3 w = SampleWorld(g, 0);
            sb.AppendLine(string.Format("  {0,-18} rayTarget={1} depth={2} cull={3} 中心点在自己矩形内={4}",
                nm, g.raycastTarget, g.depth, g.canvasRenderer.cull, Contains(g.rectTransform, w)));
        }
        return sb.ToString();
    }

    // ── 命中模型 ──

    static int _hiddenHits;

    /// <summary>遮挡物会不会在开窗时被所属面板藏掉（LobbySubPanel.hideOnOpen 直接点名，或 hideHudOnOpen 藏整个 HUD 层）。</summary>
    static bool HiddenOnOpen(Graphic blocker, LobbySubPanel owner)
    {
        if (owner == null) return false;
        if (owner.hideHudOnOpen && owner.hudLayer != null && blocker.transform.IsChildOf(owner.hudLayer.transform)) return true;
        if (owner.hideOnOpen == null) return false;
        for (int i = 0; i < owner.hideOnOpen.Length; i++)
        {
            GameObject h = owner.hideOnOpen[i];
            if (h == null) continue;
            if (blocker.transform == h.transform || blocker.transform.IsChildOf(h.transform)) return true;
        }
        return false;
    }

    static Graphic TopBlocker(List<Graphic> all, Graphic target, Vector3 world, bool permanentOnly, LobbySubPanel owner)
    {
        _hiddenHits = 0;
        List<int> tp = PathOf(target.transform);
        Graphic best = null;
        List<int> bp = null;
        for (int i = 0; i < all.Count; i++)
        {
            Graphic g = all[i];
            if (g == null || g == target) continue;
            if (!g.raycastTarget || !g.isActiveAndEnabled) continue;
            if (g.transform.IsChildOf(target.transform) || target.transform.IsChildOf(g.transform)) continue;
            if (!Contains(g.rectTransform, world)) continue;
            if (!InsideMasks(g, world)) continue;
            if (permanentOnly && !IsPermanentHud(g.transform)) continue;
            if (HiddenOnOpen(g, owner)) { _hiddenHits++; continue; }
            List<int> p = PathOf(g.transform);
            if (Compare(p, tp) <= 0) continue;                        // 不比目标更靠上，不算遮挡
            if (best == null || Compare(p, bp) > 0) { best = g; bp = p; }
        }
        return best;
    }

    /// <summary>挂在这个 Graphic 上的「常驻模块」是哪一个：Layer_Hud_v1 里直接子是 Panel_* / Popup_* 的一律算子弹窗（运行时自己会关），
    /// 其余（头像衬托板 / 右上横栏 / 左下 ID 行）才是常驻。其它层（Layer_Sub_v1、LobbyUI_v1…）一律不算常驻。</summary>
    static bool IsPermanentHud(Transform t)
    {
        Transform layer = null, module = null;
        while (t != null && t != _root)
        {
            if (t.parent != null && t.parent.name == "Layer_Hud_v1") module = t;
            if (t.parent == _root) layer = t;
            t = t.parent;
        }
        if (layer == null || layer.name != "Layer_Hud_v1") return false;      // 不在常驻层
        if (module == null) return false;                                     // 就是 Layer_Hud_v1 自己
        return !module.name.StartsWith("Panel_") && !module.name.StartsWith("Popup_");
    }

    static Vector3 SampleWorld(Graphic g, int k)
    {
        var c = new Vector3[4];
        g.rectTransform.GetWorldCorners(c);                            // 0=左下 1=左上 2=右上 3=右下
        Vector3 center = (c[0] + c[2]) * 0.5f;
        switch (k)
        {
            case 0: return center;
            case 1: return Vector3.Lerp(center, (c[1] + c[2]) * 0.5f, 0.6f);   // 上中
            case 2: return Vector3.Lerp(center, (c[2] + c[3]) * 0.5f, 0.6f);   // 右中
            case 3: return Vector3.Lerp(center, (c[3] + c[0]) * 0.5f, 0.6f);   // 下中
            default: return Vector3.Lerp(center, (c[0] + c[1]) * 0.5f, 0.6f);  // 左中
        }
    }

    static bool Contains(RectTransform rt, Vector3 world)
    {
        Vector3 local = rt.InverseTransformPoint(world);
        return rt.rect.Contains(new Vector2(local.x, local.y)) && Mathf.Abs(local.z) < 1.5f;
    }

    static bool InsideMasks(Graphic g, Vector3 world)
    {
        Transform t = g.transform.parent;
        while (t != null)
        {
            if (t.GetComponent<RectMask2D>() != null && !Contains((RectTransform)t, world)) return false;
            if (t.GetComponent<Mask>() != null && !Contains((RectTransform)t, world)) return false;
            t = t.parent;
        }
        return true;
    }

    static List<int> PathOf(Transform t)
    {
        var p = new List<int>(8);
        while (t != null && t != _root) { p.Add(t.GetSiblingIndex()); t = t.parent; }
        p.Reverse();
        return p;
    }

    static int Compare(List<int> a, List<int> b)
    {
        int n = Mathf.Min(a.Count, b.Count), i = 0;
        while (i < n && a[i] == b[i]) i++;
        if (i < n) return a[i] < b[i] ? -1 : 1;
        return a.Count.CompareTo(b.Count);
    }

    static string LayerTag(Graphic g)
    {
        string found = "?";
        Transform t = g.transform;
        while (t != null && t != _root)
        {
            if (t.parent == _root) found = t.name;
            t = t.parent;
        }
        return found;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static Canvas FindLobbyCanvas()
    {
        var canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null && FindDeep(canvases[i].transform, "Layer_Hud_v1") != null) return canvases[i];
        return canvases.Length > 0 ? canvases[0] : null;
    }
}
