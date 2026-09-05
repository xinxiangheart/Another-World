using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// HoverTagSystem — 3D 召唤物悬停文本标签系统（单例，懒创建）。
//
// 语义：
//   - 鼠标悬停（Card3DHover.OnMouseEnter）只是开关；标签位置锚在 3D 卡牌上，
//     不跟随鼠标。卡停则标签固定（呼吸动画带来的微浮动属预期）。
//   - 所有标签生成在 2D 空间，父级 = Test1Panel 所在 Canvas（CardCanvas）。
//   - 左列 = 特性标签（从卡中心向左展开）；右列 = 状态标签（从卡中心向右展开）。
//   - 每列超 max*PerColumn 自动分新列（特性新列再向左、状态新列再向右）。
//
// 内容：
//   - 特性：CardInstance.GetVisibleTraitEntries() → "属性：文本"；被禁条目（完全沉默 BlockAll / 光环类禁）
//     行尾附禁制原因并整行置灰；完全沉默也照常列出（"置灰保留"，与卡面特性图标对齐）。
//   - 状态：仅显示"目标实际受到的、有来源记录的状态"：
//     ① ci.activeStatuses（AddStatus 登记的目标侧状态来源记录）→ 每条一个标签（描述 + 来源卡名）；
//     ② 01525 格子强化动态条 = 攻击力临时+{slot.slotTempAttackBoost}（槽位真源，目标实际受到的加成）。
//     CardData.buffText/debuffText 是"本卡给予别人状态时的描述"，不是自己身上的状态 → 自己悬停时绝不读取。
//
// 坐标（全部在 Canvas 局域单位）：锚点 = 卡牌世界中心投影到 tagLayer 的局域点；
//   每个标签存相对该锚点的局域偏移，每帧重投影锚点再摆放。
// 入场动画：Show 时标签起点 y=最终位、x=最终x×slideStartRatio（各标签竖直平行），在 slideDuration 秒内
//   整体水平滑到目标位置（smoothstep，EaseInOffset）；Hide 直接销毁，无退场动画。仅表现，不改布局/内容。
// ============================================================================

public class HoverTagSystem : MonoBehaviour
{
    public static HoverTagSystem Instance { get; private set; }

    [Header("运行时引用（懒创建自动填）")]
    public RectTransform tagLayer;      // 标签父节点（CardCanvas 全屏 stretch，pivot 中心）
    public Canvas canvas;               // 所属 Canvas（取 worldCamera）

    [Header("布局参数（Canvas 局域单位；1920×1080 参考分辨率下≈像素）")]
    [Tooltip("同列内上下标签间距")]
    public float tagSpacing = 6f;
    [Tooltip("不同列之间水平间距")]
    public float columnSpacing = 16f;
    [Tooltip("每列特性标签上限，超过自动分新列向左")]
    public int maxTraitsPerColumn = 8;
    [Tooltip("每列状态标签上限，超过自动分新列向右")]
    public int maxStatusPerColumn = 8;
    [Tooltip("最近列内边缘距卡边缘的水平空隙")]
    public float horizontalMargin = 12f;
    [Tooltip("整簇相对卡中心的垂直偏移（正=下移）")]
    public float verticalOffset = 0f;

    [Header("入场动画")]
    [Tooltip("标签从近目标处滑到目标位置的时长（秒）")]
    public float slideDuration = 0.12f;
    [Tooltip("入场起点水平位置 = 最终 x 的该比例（y 保持最终位 → 各标签平行水平滑入，不斜向汇聚）。例 0.8=从最终 x 的 80% 处开始，短距平行滑出")]
    [Range(0f, 1f)]
    public float slideStartRatio = 0.8f;

    // ── 悬停状态 ──
    Transform _anchor;                  // 悬停的 3D 卡根（世界锚点）
    readonly List<HoverTagLabel> _tags = new List<HoverTagLabel>();
    readonly Dictionary<HoverTagLabel, Vector2> _offsets = new Dictionary<HoverTagLabel, Vector2>();
    // 入场动画状态：每标签记 起始偏移(近中心) 与 开始时间，滑到 _offsets 记录的目标偏移。
    readonly Dictionary<HoverTagLabel, Vector2> _animStart = new Dictionary<HoverTagLabel, Vector2>();
    readonly Dictionary<HoverTagLabel, float> _animTime = new Dictionary<HoverTagLabel, float>();

    static GameObject _prefab;          // 来自 Resources.Load<HoverTagConfig>("Config/HoverTagConfig").tagLabelPrefab

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_anchor != null && tagLayer != null && canvas != null && _tags.Count > 0)
            ApplyPositions();
    }

    /// <summary>按当前锚点把全部标签摆到正确位置（每帧 + Show 后各调一次）。</summary>
    void ApplyPositions()
    {
        if (_anchor == null || tagLayer == null || canvas == null || _tags.Count == 0) return;
        Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        if (cam == null) return;

        // 锚点世界中心 → tagLayer 局域点（复刻 DamageFloater 换算）。
        Vector2 centerPx = RectTransformUtility.WorldToScreenPoint(cam, _anchor.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(tagLayer, centerPx, cam, out Vector2 centerLocal))
            return;

        for (int i = 0; i < _tags.Count; i++)
        {
            HoverTagLabel tag = _tags[i];
            if (tag == null) continue;
            if (!_offsets.TryGetValue(tag, out Vector2 off)) continue;
            Vector2 cur = EaseInOffset(tag, off);
            var rt = (RectTransform)tag.transform;
            rt.anchoredPosition = centerLocal + cur;
        }
    }

    /// <summary>入场动画偏移：从起点(y=最终位, x=最终x×ratio)平行水平滑到目标偏移；时长到即锁定目标并清动画态。</summary>
    Vector2 EaseInOffset(HoverTagLabel tag, Vector2 target)
    {
        if (!_animTime.TryGetValue(tag, out float t0)) return target;   // 非动画态/已完成 → 直接用目标
        float t = slideDuration > 0f ? (Time.time - t0) / slideDuration : 1f;
        if (t >= 1f)
        {
            _animTime.Remove(tag);
            _animStart.Remove(tag);
            return target;
        }
        Vector2 start = _animStart.TryGetValue(tag, out Vector2 s) ? s : target;
        float e = t * t * (3f - 2f * t); // smoothstep 平滑
        return Vector2.LerpUnclamped(start, target, e);
    }

    // ═══════════════════ 单例 & 生命周期 ═══════════════════

    /// <summary>确保单例存在（懒创建）。无 Test1Panel/Canvas 时返回 null（仅 Game 场景用）。</summary>
    public static HoverTagSystem Ensure()
    {
        if (Instance != null) return Instance;
        var t1 = Test1Panel.Instance;
        Transform parentCanvas = t1 != null ? t1.transform.parent : null;
        if (parentCanvas == null)
        {
            var cv = Object.FindObjectOfType<Canvas>();
            parentCanvas = cv != null ? cv.transform : null;
        }
        if (parentCanvas == null) return null;

        GameObject layerGo = new GameObject("HoverTagLayer", typeof(RectTransform));
        RectTransform layerRT = (RectTransform)layerGo.transform;
        layerRT.SetParent(parentCanvas, false);
        layerRT.anchorMin = Vector2.zero; layerRT.anchorMax = Vector2.one;
        layerRT.offsetMin = Vector2.zero; layerRT.offsetMax = Vector2.zero;
        layerRT.pivot = new Vector2(0.5f, 0.5f);
        layerGo.transform.SetAsLastSibling();

        HoverTagSystem sys = layerGo.AddComponent<HoverTagSystem>();
        sys.tagLayer = layerRT;
        sys.canvas = parentCanvas.GetComponent<Canvas>();
        Instance = sys;

        if (_prefab == null)
        {
            // TagLabel.prefab 在 Prefabs/UI/Panels（非 Resources）→ 由 Config 资产持引用。
            var cfg = Resources.Load<HoverTagConfig>("Config/HoverTagConfig");
            if (cfg != null) _prefab = cfg.tagLabelPrefab;
        }
        if (_prefab == null)
            Debug.LogWarning("[HoverTag] 未取得悬停标签预制体 —— 请先执行 Tools/卡牌/生成悬停标签预制体（生成 TagLabel.prefab + HoverTagConfig.asset）");
        return sys;
    }

    /// <summary>显示某张 3D 卡的悬停标签。anchor3D = 卡牌根 GameObject（世界锚点）。</summary>
    public void Show(CardInstance ci, GameObject anchor3D)
    {
        Hide();
        if (ci == null || anchor3D == null || _prefab == null) return;

        _anchor = anchor3D.transform;

        var traitTexts = BuildTraitTexts(ci);   // 左
        var statusTexts = BuildStatusTexts(ci); // 右

        if (traitTexts.Count == 0 && statusTexts.Count == 0) return;

        float cardHalfW = EstimateCardHalfWLocal(anchor3D);
        BuildSide(traitTexts, true, maxTraitsPerColumn, cardHalfW);
        BuildSide(statusTexts, false, maxStatusPerColumn, cardHalfW);
        ApplyPositions(); // 立即摆一次，避免本帧悬停在 (0,0) 闪一下
    }

    public void Hide()
    {
        _anchor = null;
        foreach (var tag in _tags)
            if (tag != null) Object.Destroy(tag.gameObject);
        _tags.Clear();
        _offsets.Clear();
        _animStart.Clear();
        _animTime.Clear();
    }

    // ═══════════════════ 单侧列布局 ═══════════════════

    void BuildSide(List<(string text, bool blocked)> items, bool isLeft, int maxPerColumn, float cardHalfW)
    {
        int perCol = Mathf.Max(1, maxPerColumn);
        int columnCount = Mathf.CeilToInt((float)items.Count / perCol);

        // ① 实例化所有标签并测量尺寸（GetSize 为局域单位）。
        var sizes = new List<Vector2>();
        foreach (var item in items)
        {
            GameObject go = Instantiate(_prefab, tagLayer, false);
            go.name = isLeft ? "TraitTag" : "StatusTag";
            var label = go.GetComponent<HoverTagLabel>();
            if (label == null) label = go.AddComponent<HoverTagLabel>();
            if (!label.SetText(item.text)) { Object.Destroy(go); continue; }
            if (item.blocked && label.labelText != null)
                label.labelText.color = TraitBanQuery.BlockedTint; // 6.3 被禁特性行置灰
            _tags.Add(label);
            sizes.Add(label.GetSize());
        }
        if (sizes.Count == 0) return;
        int n = sizes.Count;

        // ② 列统计：每列宽 = 该列最宽标签；每列高 = 纵排总高。
        var colW = new float[columnCount];
        var colH = new float[columnCount];
        var colCnt = new int[columnCount];
        for (int i = 0; i < n; i++)
        {
            int c = i / perCol;
            colW[c] = Mathf.Max(colW[c], sizes[i].x);
            colH[c] += sizes[i].y + (colCnt[c] > 0 ? tagSpacing : 0f);
            colCnt[c]++;
        }

        // ③ 每列"靠中心侧"边缘的相对局域 x（相对卡中心）：
        //    最近列内边缘 = 卡边缘(cardHalfW) + horizontalMargin；
        //    后续新列向更外侧移动 (上一列宽 + columnSpacing)。
        var colInnerX = new float[columnCount];
        for (int c = 0; c < columnCount; c++)
        {
            float edge = (isLeft ? -1f : 1f) * (cardHalfW + horizontalMargin);
            for (int k = 0; k < c; k++)
                edge += (isLeft ? -1f : 1f) * (colW[k] + columnSpacing);
            colInnerX[c] = edge;
        }

        // ④ 逐标签定位：每列纵向以卡中心为中央（整体高 colH 中点），
        //    列内自上而下排。rect 坐标 +y=上；verticalOffset 正值下移。
        //    （先算出该列第0个标签顶的 y，再逐行向下 = y 递减。）
        for (int i = 0; i < n; i++)
        {
            HoverTagLabel label = _tags[_tags.Count - n + i];
            Vector2 sz = sizes[i];
            int c = i / perCol;
            int r = i % perCol;

            // 该列垂直中点在 verticalOffset（向下为正 → rect 中 -verticalOffset）。
            float colTopY = -verticalOffset + colH[c] * 0.5f;
            for (int k = 0; k < r; k++)
            {
                int idx = c * perCol + k;
                colTopY -= sizes[idx].y + tagSpacing;
            }

            float cx = isLeft
                ? colInnerX[c] - sz.x * 0.5f                    // 左列：右边缘贴 inner
                : colInnerX[c] + sz.x * 0.5f;                   // 右列：左边缘贴 inner
            float cy = colTopY - sz.y * 0.5f;                   // 标签顶往下半高 = 标签中心

            Vector2 target = new Vector2(cx, cy);
            _offsets[label] = target;
            // 入场动画：起点 x = 最终 x × slideStartRatio（向卡中心水平收缩），y 保持最终位 →
            // 同侧各标签起点在一条竖直线上，整体平行水平滑向最终位置（非斜向汇聚）。时长 slideDuration。
            _animStart[label] = new Vector2(target.x * slideStartRatio, target.y);
            _animTime[label] = Time.time;
        }
    }

    // ═══════════════════ 工具 ═══════════════════

    /// <summary>估算卡牌半宽（局域单位）：中心 与 中心±0.75世界单位 两投影点直接取局域差。
    /// 卡模型宽约 0.9×1.4，半宽≈0.63 世界；探针取 0.75 略宽 → 标签不压卡面。</summary>
    float EstimateCardHalfWLocal(GameObject anchor3D)
    {
        Camera cam = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        if (cam == null || anchor3D == null || tagLayer == null) return 60f;
        Vector3 p = anchor3D.transform.position;
        const float probe = 0.75f;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tagLayer,
                RectTransformUtility.WorldToScreenPoint(cam, p), cam, out Vector2 cLocal)
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(tagLayer,
                RectTransformUtility.WorldToScreenPoint(cam, p + Vector3.right * probe), cam, out Vector2 rLocal))
            return Mathf.Max(8f, Mathf.Abs(rLocal.x - cLocal.x));
        return 60f;
    }

    // ═══════════════════ 内容生成 ═══════════════════

    /// <summary>左列特性文本：GetVisibleTraitEntries → "属性：文本"（无编号、无赋予标注）。
    /// 6.x 置灰保留：完全沉默不再空列；被禁（完全沉默 BlockAll / 光环类禁）条目行尾附原因并标记 blocked（置灰）。</summary>
    static List<(string, bool)> BuildTraitTexts(CardInstance ci)
    {
        var outList = new List<(string, bool)>();
        if (ci == null) return outList;
        bool fullySilenced = TraitBanQuery.IsFullySilenced(ci);

        var entries = ci.GetVisibleTraitEntries();
        foreach (var e in entries)
        {
            string cleaned = e.text;
            if (e.attributes != null && e.attributes.Length > 0
                && cleaned != null && cleaned.StartsWith(e.attributes[0] + "："))
                cleaned = cleaned.Substring(e.attributes[0].Length + 1).TrimStart();

            string line = (e.attributes != null && e.attributes.Length > 0)
                ? string.Join("、", e.attributes) + "：" + cleaned
                : cleaned;
            if (string.IsNullOrEmpty(line)) continue;

            // 被禁 → 行尾附原因：完全沉默给整卡原因；否则按属性类查光环禁制（法官/萨满）原因。
            string reason = "";
            if (fullySilenced) reason = TraitBanQuery.FullSilenceReason(ci);
            else if (e.attributes != null)
                foreach (var a in e.attributes)
                {
                    string r = TraitBanQuery.ClassBanReason(ci, a);
                    if (r.Length > 0) { reason = r; break; }
                }
            if (reason.Length > 0) line += "\n" + reason;
            outList.Add((line, reason.Length > 0));
        }
        return outList;
    }

    /// <summary>右列状态文本：目标实际受到的、有来源记录的状态（6.1/6.4：每条附来源卡名）。
    /// 数据源 = 目标自己记录的 activeStatuses（AddStatus 写入，来源卡施加时登记）+ 01525 槽位强化真源。
    /// description 内部可能含 \n 多条 → 保留在单标签内多行（来源跟在描述末尾）。
    /// 注意：CardData.buffText/debuffText 是"本卡给予别人状态时的描述"，不是自己身上的状态，故不在此读取。</summary>
    List<(string, bool)> BuildStatusTexts(CardInstance ci)
    {
        var outList = new List<(string, bool)>();
        if (ci == null) return outList;

        // ① 目标侧来源记录：本卡被施加的每条状态 = 一个标签（描述 + 来源，StatusWithSource）。
        if (ci.activeStatuses != null)
            foreach (var a in ci.activeStatuses)
            {
                if (a == null) continue;
                string text = TraitBanQuery.StatusWithSource(a);
                if (text != null) outList.Add((text, false));
            }

        // ② 01525 格子强化：反查所在槽位现读 slotTempAttackBoost——这是目标实际受到的临时攻击加成
        //    （槽位持久真源，非本卡 CardData 描述），叠加天然正确：+2 → +4 → +6 …
        BoardSlot slot = FindSlotOfAnchor();
        if (slot != null && slot.slotTempAttackBoost > 0)
            outList.Add(($"攻击力临时+{slot.slotTempAttackBoost}", false));

        return outList;
    }

    /// <summary>按悬停 3D 模型反查 BoardSlot（Card3DHover.GetMySlot 同款）。附着卡匹配不到 → null。</summary>
    BoardSlot FindSlotOfAnchor()
    {
        if (_anchor == null) return null;
        var bm = Object.FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        var slots = bm.GetAllSlots();
        if (slots == null) return null;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].currentCard3D == _anchor.gameObject)
                return slots[i];
        return null;
    }
}
