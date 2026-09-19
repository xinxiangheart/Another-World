using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选择期「压暗」：进入选择阶段（<see cref="SelectionManager.IsSelecting"/>）或召唤放置窗口（<see cref="IsPlacementChoosing"/>）后，
/// 不被允许选择的槽位（格子底 + 格上 3D 卡牌 + 挂在该格上的附着物）与不可点的手牌压成暗色，合法目标保持原色。
/// 它取代了旧的「合法目标全部黄框高亮」这一表达方式（旧的槽位高亮逐步退场，见 BoardSlot.HighlightRow）。
///
/// 实现是「本体压暗」：槽位把**格子底色（线层由 SlotEdgeOverlay 镜像，跟着走）**乘一个系数，
/// 格上的 3D 卡牌与**挂在该格上的附着物**把**卡面本体**各图形（卡面三层 / 角标 / 三排图标 = SpriteRenderer，攻防文字 = TMP）
/// 各乘同一个系数；2D 手牌仍是铺一层黑幕 Image（卡面是 UGUI，改色会与悬停 / 图标的 tint 打架）。
/// 三处共用同一个系数 <see cref="DimMul"/>，压暗程度天然一致。
/// 压暗只乘 RGB、不动 alpha：封锁 / 囚牢 / 瘟疫 / 渊印记这些状态色照旧参与（黑仍黑、紫仍紫，只是更暗），
/// 退出选择即整体还原。
/// </summary>
public static class SelectionDim
{
    /// <summary>手牌黑幕的不透明度（0 = 不压暗，1 = 全黑）。</summary>
    public const float Alpha = 0.55f;

    /// <summary>
    /// 本体压暗的亮度系数：各图形自己的 RGB 乘它，alpha 不动。
    /// 0.45 = 旧黑幕（0.55 不透明度叠黑）的等效乘数（lerp(c, 黑, 0.55) = c × 0.45），
    /// 所以换成「本体压暗」后整体明暗与改前一致，只是形状 / 描边不再被黑框吃掉。三处共用这一条。
    /// </summary>
    public const float DimMul = 0.45f;

    /// <summary>把一个颜色压暗（RGB × DimMul，alpha 原样）。</summary>
    public static Color Dim(Color c) => new Color(c.r * DimMul, c.g * DimMul, c.b * DimMul, c.a);

    static bool _applied;      // 上一帧是否处于压暗状态（退出时还要再跑一遍复原）
    static BoardManager _bm;   // 缓存棋盘（选择期每帧要用，退出时用来复原）

    /// <summary>本次召唤放置窗口是否已经落子。落子后 isPlacingCard 仍要为 true（等进场效果子树收尾），
    /// 那段窗口若继续压暗会把刚上场的卡一起压黑，所以落子即收（见 NotifyPlacementResolved）。</summary>
    static bool _placementResolved;

    /// <summary>落子成功时由 <see cref="BoardSlot"/> 调用：本次召唤放置的压暗立即收掉。</summary>
    public static void NotifyPlacementResolved() => _placementResolved = true;

    /// <summary>是否处于「召唤放置」选择窗口：拖拽出牌放置（有待放的牌 + 非目标选择）。
    /// 把压暗从法术选目标扩到召唤落点选择——判据与 <see cref="BoardSlot.CanBeSelected"/> 的放置分支同源，
    /// 因此「场上满 → 走顶替」时己方有召唤物的格子会正确保持原色（不压黑）。</summary>
    public static bool IsPlacementChoosing =>
        BoardSlot.isPlacingCard && BoardSlot.cardToPlace != null && !BoardSlot.isTargetingMode;

    /// <summary>每帧由 <see cref="SelectionManager"/> 驱动。selecting = 是否处于选择阶段（层栈里有选择层）。</summary>
    public static void Tick(bool selecting)
    {
        bool placing = IsPlacementChoosing;
        if (!placing) _placementResolved = false;   // 窗口结束 → 下一次放置重新开始压暗
        bool active = selecting || (placing && !_placementResolved);
        if (!active && !_applied) return; // 非选择期且已复原：什么都不做

        if (_bm == null) _bm = Object.FindObjectOfType<BoardManager>();
        if (_bm != null)
        {
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = _bm.GetSlot(i);
                if (slot == null) continue;
                // 合法目标（含抛置提示格）保持原色，其余压暗
                slot.SetSelectionDim(active && !slot.CanBeSelected());
            }
        }

        ApplyHandDim(active);
        _applied = active;
    }

    /// <summary>手牌压暗：本次选择里「可点」的手牌保持原色，其余压暗。
    /// 判定沿用流程自身的挂法——可选手牌会被挂上 CardClickHandler.onClick。</summary>
    static void ApplyHandDim(bool active)
    {
        List<GameObject> hand = NetworkPlayer.Local != null ? NetworkPlayer.Local.handCards : null;
        if (hand == null) return;
        for (int i = 0; i < hand.Count; i++)
        {
            GameObject card = hand[i];
            if (card == null) continue;
            bool dim = active && !IsHandCardSelectable(card);
            if (dim && !card.activeInHierarchy) continue; // 收起中的手牌（选目标期整手隐藏）不必压暗
            SetCardDim(card, dim);
        }
    }

    /// <summary>本次选择里这张手牌能不能点（流程会给可选手牌挂 CardClickHandler.onClick）。</summary>
    public static bool IsHandCardSelectable(GameObject card)
    {
        if (card == null) return false;
        CardClickHandler handler = card.GetComponent<CardClickHandler>();
        return handler != null && handler.onClick != null;
    }

    // ── 2D 卡（手牌）：在卡面上铺一层黑幕，不碰卡本身的任何颜色与透明度 ──
    const string OverlayName = "SelectionDim";

    static readonly Color DimColor = new Color(0f, 0f, 0f, Alpha);

    /// <summary>压暗 / 复原一张 2D 手牌（非 UI 卡无 RectTransform 时跳过，场上 3D 牌走槽位黑幕）。</summary>
    public static void SetCardDim(GameObject card, bool dim)
    {
        if (card == null) return;
        RectTransform cardRt = card.transform as RectTransform;
        if (cardRt == null) return;

        Transform existing = cardRt.Find(OverlayName);
        if (existing == null)
        {
            if (!dim) return; // 没黑幕又不需要压暗：不创建
            GameObject go = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(cardRt, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            Image imgNew = go.GetComponent<Image>();
            imgNew.sprite = null;
            imgNew.color = DimColor;
            imgNew.raycastTarget = false;
            imgNew.maskable = false;
            imgNew.useSpriteMesh = false;
            existing = go.transform;
        }

        Image img = existing.GetComponent<Image>();
        if (img == null) return;

        bool needShow = dim && !existing.gameObject.activeSelf;
        if (needShow)
        {
            existing.SetAsLastSibling(); // 压在卡面各图层之上（只在开关那一下调，别每帧动层级）
            existing.gameObject.SetActive(true);
        }
        else if (!dim && existing.gameObject.activeSelf)
        {
            existing.gameObject.SetActive(false);
        }
    }
}
