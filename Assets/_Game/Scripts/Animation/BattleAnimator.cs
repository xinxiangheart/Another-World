using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// BattleAnimator — 战斗动画播放管理器（单例）
// ============================================================================
//
// 负责按顺序播放 BattleEvent，支持"重叠窗口"：
//   前一个攻击的击中时刻（onImpact 触发后），下一个攻击立即启动"飞向"，
//   前一个攻击的"返回"在后台继续，不必等它完全返回。
//
// UI 锁定：从第一个动画开始锁定，到所有动画（含返回）结束才解锁。
// ============================================================================

public class BattleAnimator : MonoBehaviour
{
    public static BattleAnimator Instance { get; private set; }

    /// <summary>动画播放期间锁定 UI（禁止拖牌/点击手牌/操作 UI）。</summary>
    public static bool IsLockingUI { get; private set; }

    private Queue<BattleEvent> _queue = new Queue<BattleEvent>();
    private bool _playing = false;
    private int _returningCount = 0; // 后台返回动画计数

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>加入播放队列。</summary>
    public void Play(BattleEvent evt)
    {
        if (evt == null) return;
        _queue.Enqueue(evt);
        TryPlayNext();
    }

    /// <summary>等待所有事件（含返回动画）完成。结束时解锁 UI。</summary>
    public IEnumerator WaitForAll()
    {
        while (_queue.Count > 0 || _playing || _returningCount > 0)
            yield return null;

        IsLockingUI = false;
        var hm = FindObjectOfType<HandManager>();
        if (hm != null) hm.SetHandAreaRaycast(true);
    }

    /// <summary>是否有动画在播（含后台返回）。</summary>
    public bool IsAnimating => _queue.Count > 0 || _playing || _returningCount > 0;

    void TryPlayNext()
    {
        if (_playing || _queue.Count == 0) return;
        StartCoroutine(PlayRoutine(_queue.Dequeue()));
    }

    IEnumerator PlayRoutine(BattleEvent evt)
    {
        _playing = true;
        IsLockingUI = true;
        var hm = FindObjectOfType<HandManager>();
        if (hm != null) hm.SetHandAreaRaycast(false);

        // 飞向 + 击中（阻塞到 onImpact）
        yield return evt.Play();

        _playing = false;

        // 返回动画后台执行（不阻塞，下一个攻击可立即启动 → 重叠窗口）
        _returningCount++;
        StartCoroutine(ReturnRoutine(evt));

        TryPlayNext();
    }

    IEnumerator ReturnRoutine(BattleEvent evt)
    {
        yield return evt.Return();
        _returningCount--;
    }
}
