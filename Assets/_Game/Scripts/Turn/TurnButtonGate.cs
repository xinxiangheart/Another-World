using UnityEngine;

// ============================================================================
// TurnButtonGate — 「抽牌 / 结束回合」两个按钮的唯一写入点
// ============================================================================
//
// 背景（"偶尔自己回合点不了结束回合和抽牌"）：
//   这两个按钮原先是"谁用谁关、谁用完谁开"——TurnManager(阶段)、CardDrag(拖拽/选择/弹窗)、
//   HandManager、BoardSlot、CardDisplayPanel 各自直接写 CanvasGroup.blocksRaycasts。
//   任一处协程提前 return（yield break / 弹窗没走到收尾 / 被别处的 Show 顶掉）就会留下
//   永久禁用，而且没有任何兜底能恢复：表现为"自己回合点不了结束回合和抽牌"，
//   又因为其触发条件（手牌张数、弃牌堆内容、弹窗是否被顶掉）而偶发。
//
// 现在改成**派生状态**：
//   可交互 = 阶段允许(TurnManager 权威) 且 当前没有 UI 交互锁
//   UI 交互锁每帧从权威状态重算（拖拽 / 选择 / 放置 / 弹窗 / 战斗动画），
//   漏放的锁会在下一帧自动解除，不再依赖各协程的收尾路径是否走到。
//
// 约定：只允许 TurnButtonGate 写这两个按钮；旧的 SetButtonsInteractable / SetInteractable
// 调用点保留签名但改为"请求重算"（见 CardDrag / CardDisplayPanel）。
// ============================================================================

public static class TurnButtonGate
{
    /// <summary>阶段权威：TurnManager.SetPlayerActionsEnabled 写入（MyTurn=true；其余阶段与结束回合过渡期=false）。</summary>
    static bool _turnAllowsActions = true;

    /// <summary>缓存：DrawCardUI / EndTurnButton 会被 SetActive(false) 隐藏，FindObjectOfType 之后找不到，必须缓存。</summary>
    static EndTurnButton _endBtn;
    static DrawCardUI _drawUi;

    static bool _appliedOnce;
    static bool _lastOn;
    static EndTurnButton _lastEnd;
    static DrawCardUI _lastDraw;

    /// <summary>阶段权威写入（TurnManager.SetPlayerActionsEnabled 专用）。</summary>
    public static void SetTurnAllowsActions(bool enabled)
    {
        _turnAllowsActions = enabled;
        Apply();
    }

    /// <summary>按当前状态重算并写入两个按钮（幂等：状态与对象都没变时不重复写）。</summary>
    public static void Tick() => Apply();

    /// <summary>兼容旧调用点：请求重算。旧代码的"门"由各自状态在 IsUiLocked 中体现。</summary>
    public static void Refresh() => Apply();

    /// <summary>当前是否存在"玩家此刻不该操作"的 UI 状态。</summary>
    public static bool IsUiLocked() => IsUiLocked(out _);

    /// <summary>同上，并给出锁的出处（日志诊断用：下次若再出现"自己回合点不动按钮"，
    /// 看这条日志就知道是哪个状态还挂着）。</summary>
    public static bool IsUiLocked(out string reason)
    {
        reason = null;
        // 拖拽手牌中（CardView.IsAnyCardDragging 在 CardDrag.OnEndDrag 顶部无条件复位，不会滞留）
        if (CardView.IsAnyCardDragging) { reason = "拖拽手牌"; return true; }
        // 目标选择中（SelectionManager 层栈；含法术开选、重排、召唤选格）
        if (SelectionManager.Instance != null && SelectionManager.Instance.IsSelecting) { reason = "目标选择中"; return true; }
        // 放置召唤物 / 附着目标选择中
        if (BoardSlot.isPlacingCard) { reason = "放置召唤物中"; return true; }
        if (BoardSlot.isAttachSelectMode) { reason = "附着选择中"; return true; }
        // 卡牌列表弹窗（含其上的"确认"键）
        if (CardDisplayPanel.Instance != null && CardDisplayPanel.Instance.IsShowing) { reason = "卡牌弹窗"; return true; }
        // 是 / 否确认弹窗
        if (ConfirmPanel.Instance != null && ConfirmPanel.Instance.IsShowing) { reason = "确认弹窗"; return true; }
        // 战斗动画播放中：用 IsAnimating 派生，而不是 IsLockingUI 锁存位
        // （WaitForAll 没被 await 时 IsLockingUI 会永久停在 true）
        if (BattleAnimator.Instance != null && BattleAnimator.Instance.IsAnimating) { reason = "战斗动画"; return true; }
        return false;
    }

    public static void Apply()
    {
        bool locked = IsUiLocked(out string reason);
        bool on = _turnAllowsActions && !locked;

        if (_endBtn == null) _endBtn = Object.FindObjectOfType<EndTurnButton>(true);
        if (_drawUi == null) _drawUi = Object.FindObjectOfType<DrawCardUI>(true);

        if (_appliedOnce && on == _lastOn && _endBtn == _lastEnd && _drawUi == _lastDraw)
            return;

        _appliedOnce = true;
        _lastOn = on;
        _lastEnd = _endBtn;
        _lastDraw = _drawUi;

        // 己方回合却被 UI 锁挡住 → 留一条线索（状态变化才打，不刷屏）
        if (!on && _turnAllowsActions && reason != null)
            Debug.Log($"[TurnButtonGate] 结束回合/抽牌暂锁：{reason}");

        if (_endBtn != null) _endBtn.SetInteractable(on);
        if (_drawUi != null) _drawUi.SetInteractable(on);
    }
}
