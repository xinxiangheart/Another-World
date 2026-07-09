using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Step 1: IGameAction 接口 + 具体类型
// ============================================================================

/// <summary>
/// 所有游戏效果的最小执行单元。每个卡牌效果（进场/退场/法术/抛置）最终都包装为一个 IGameAction。
/// </summary>
public interface IGameAction
{
    /// <summary>启动执行。同步效果在 Execute 内直接完成；异步效果启动协程后返回。</summary>
    void Execute();

    /// <summary>是否已完成。队列处理循环会等待 IsDone == true 才出下一个。</summary>
    bool IsDone { get; }

    /// <summary>动画预留时长（秒）。当前未使用，后续可配合动画系统。</summary>
    float Duration { get; }

    /// <summary>调试用名称。开启 Debug 模式时，队列每处理一个就打印此名称。</summary>
    string DebugName { get; }
}

// ============================================================================
// 具体类型
// ============================================================================

/// <summary>
/// 同步效果 — Execute() 直接完成，IsDone 始终 true。
/// 适用于：大部分不涉及等待玩家选择或动画的效果。
/// </summary>
public class SyncAction : IGameAction
{
    readonly Action _body;
    readonly string _name;

    public SyncAction(string debugName, Action body)
    {
        _name = debugName;
        _body = body;
    }

    public void Execute() => _body?.Invoke();
    public bool IsDone => true;
    public float Duration => 0f;
    public string DebugName => _name;
}

/// <summary>
/// 协程效果 — Execute() 启动一个协程，协程结束后 IsDone 变为 true。
/// 适用于：需要 yield return 等待多帧的效果（换位、多步骤动画、选择后再伤害）。
/// </summary>
public class CoroutineAction : IGameAction
{
    readonly Func<IEnumerator> _routineFactory;
    readonly MonoBehaviour _runner;
    readonly string _name;
    Coroutine _coroutine;
    bool _done;

    public CoroutineAction(string debugName, MonoBehaviour runner, Func<IEnumerator> routineFactory)
    {
        _name = debugName;
        _runner = runner;
        _routineFactory = routineFactory;
    }

    public void Execute()
    {
        _done = false;
        _coroutine = _runner.StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return _routineFactory?.Invoke();
        _done = true;
    }

    public bool IsDone => _done;
    public float Duration => 0.5f; // 默认 0.5s，协程实际耗时通常不确定
    public string DebugName => _name;
}

/// <summary>
/// 延迟动作 — 等待指定秒数后触发 payload。用于需要视觉停留的效果。
/// </summary>
public class DelayedAction : IGameAction
{
    readonly float _delay;
    readonly Action _payload;
    readonly MonoBehaviour _runner;
    readonly string _name;
    bool _executed;

    public DelayedAction(string debugName, MonoBehaviour runner, float delaySeconds, Action payload)
    {
        _name = debugName;
        _runner = runner;
        _delay = delaySeconds;
        _payload = payload;
    }

    public void Execute()
    {
        _executed = false;
        _runner.StartCoroutine(WaitAndFire());
    }

    IEnumerator WaitAndFire()
    {
        yield return new WaitForSeconds(_delay);
        _payload?.Invoke();
        _executed = true;
    }

    public bool IsDone => _executed;
    public float Duration => _delay;
    public string DebugName => _name;
}

/// <summary>
/// 死亡检查动作 — 扫描棋盘上所有 HP≤0 的单位，为每个死亡单位依次把退场效果入队。
/// 处理完一批后，如果还有新的死亡 → 自己再入队一次，直到没有新死亡或达到上限。
///
/// 取代原来 BoardSlot.CheckAndHandleDeaths() 的同步 while 循环。
/// </summary>
public class DeathCheckAction : IGameAction
{
    readonly Func<List<DeathInfo>> _scanDeaths;  // 扫描函数：返回死亡单位列表
    readonly Action<DeathInfo> _handleDeath;      // 处理单个死亡单位
    readonly Action _onAllDeathsResolved;         // 全部死亡处理完的回调
    readonly string _name;
    bool _done;
    int _iteration;

    public DeathCheckAction(string debugName, Func<List<DeathInfo>> scanDeaths, Action<DeathInfo> handleDeath, Action onAllDeathsResolved = null)
    {
        _name = debugName;
        _scanDeaths = scanDeaths;
        _handleDeath = handleDeath;
        _onAllDeathsResolved = onAllDeathsResolved;
    }

    public void Execute()
    {
        _iteration++;
        if (_iteration > 50) // 安全阀：最多 50 轮死亡链
        {
            Debug.LogError($"[ActionQueue] DeathCheckAction 迭代超过 50 轮，强制停止！可能存在无限死亡循环。");
            _done = true;
            return;
        }

        var deadList = _scanDeaths?.Invoke();
        if (deadList == null || deadList.Count == 0)
        {
            _done = true;
            _onAllDeathsResolved?.Invoke();
            return;
        }

        // 每个死亡单位依次入队退场效果
        foreach (var death in deadList)
        {
            ActionQueueManager.Enqueue(new SyncAction(
                $"DeathEffect:{death.templateID}@slot{death.slotID}",
                () => _handleDeath?.Invoke(death)));
        }

        // 退场效果全部执行完后，可能又有新死亡 → 把自己再次入队
        ActionQueueManager.Enqueue(new SyncAction(
            $"{_name}:Recheck[{_iteration}]",
            () =>
            {
                var next = new DeathCheckAction(_name, _scanDeaths, _handleDeath, _onAllDeathsResolved)
                { _iteration = _iteration };
                ActionQueueManager.Enqueue(next);
            }));

        _done = true; // 本批次已完成，但 Recheck 会排队等待
    }

    public bool IsDone => _done;
    public float Duration => 0f;
    public string DebugName => $"{_name}[{_iteration}]";
}

/// <summary>
/// 死亡单位的简要信息。DeathCheckAction 的扫描函数返回此结构列表。
/// </summary>
public struct DeathInfo
{
    public string templateID;
    public int slotID;
    public GameObject cardObject;
    public CardInstance cardInstance;
    public bool isActiveExit; // 是否为主动退场（抛置）
    public int savedAttack;   // 退场前的攻击力
}

// ============================================================================
// ActionQueueManager — 主入口
// ============================================================================

/// <summary>入队位置。Bottom = 排队执行（默认）；Top = 插队立即执行（反制/先手插入）。</summary>
public enum QueuePosition { Bottom, Top }

/// <summary>
/// 全局游戏效果队列。所有卡牌效果通过 QueueAction / AddToBottom / AddToTop 加入队列，
/// 由 ProcessLoop 按 FIFO 顺序逐一执行（ExecuteAll 语义，出队直到清空）。
///
/// 使用方法：
///   ActionQueueManager.AddToBottom(new SyncAction("DoX", () => { ... }));
///   ActionQueueManager.AddToBottom(new CoroutineAction("DoY", this, () => MyRoutine()));
///   ActionQueueManager.AddToTop(new SyncAction("Interrupt", () => { ... }));
///
/// 场景中无需手动挂载：Bootstrap 会在场景加载前自动创建一个常驻实例。
/// </summary>
public class ActionQueueManager : MonoBehaviour
{
    public static ActionQueueManager Instance { get; private set; }

    [SerializeField] bool _enableDebugLog;

    LinkedList<IGameAction> _queue = new LinkedList<IGameAction>();
    bool _processing;
    const int MAX_ITERATIONS = 10000; // 单次处理循环的硬上限

    /// <summary>自举：若场景未手动挂载，则在场景加载前创建一个常驻实例。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("ActionQueueManager");
        go.AddComponent<ActionQueueManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ---- 用户约定 API（AddToBottom/AddToTop/QueueAction/ExecuteAll） -------------

    /// <summary>统一入队入口。position 决定排队(Bottom)还是插队(Top)。</summary>
    public static void QueueAction(IGameAction action, QueuePosition position = QueuePosition.Bottom)
    {
        if (position == QueuePosition.Top) AddToTop(action);
        else AddToBottom(action);
    }

    /// <summary>排队执行 — 添加到队尾，FIFO 顺序执行。</summary>
    public static void AddToBottom(IGameAction action)
    {
        if (Instance == null) { action.Execute(); return; } // 离线模式兜底
        Instance._queue.AddLast(action);
        if (!Instance._processing) Instance.StartCoroutine(Instance.ProcessLoop());
    }

    /// <summary>插队立即执行 — 添加到队首，下一个就执行。用于紧急响应（如反制牌触发）。</summary>
    public static void AddToTop(IGameAction action)
    {
        if (Instance == null) { action.Execute(); return; }
        Instance._queue.AddFirst(action);
        if (!Instance._processing) Instance.StartCoroutine(Instance.ProcessLoop());
    }

    /// <summary>逐一出队并执行，直到队列清空。ProcessLoop 已自动驱动，此处仅供显式启动/兜底。</summary>
    public static void ExecuteAll()
    {
        if (Instance != null && !Instance._processing && Instance._queue.Count > 0)
            Instance.StartCoroutine(Instance.ProcessLoop());
    }

    // ---- 兼容别名（旧命名 Enqueue/Interrupt，内部/DeathCheckAction 仍在用） --------

    /// <summary>[别名] 等价于 AddToBottom。</summary>
    public static void Enqueue(IGameAction action) => AddToBottom(action);

    /// <summary>[别名] 等价于 AddToTop。</summary>
    public static void Interrupt(IGameAction action) => AddToTop(action);

    /// <summary>清空队列（回合切换、游戏结束时调用）。</summary>
    public static void Clear()
    {
        if (Instance != null) Instance._queue.Clear();
    }

    public static int PendingCount => Instance?._queue.Count ?? 0;

    /// <summary>队列是否空闲（无待执行、无正在处理）。</summary>
    public static bool IsIdle => Instance == null || (!Instance._processing && Instance._queue.Count == 0);

    /// <summary>协程调用点适配：等待队列彻底排空。供原本"同步 CheckAndHandleDeaths 后立即比对棋盘"的协程使用。</summary>
    public static IEnumerator WaitForDrain()
    {
        if (Instance == null) yield break;
        // 等到不再处理且队列为空。加一帧缓冲，避免恰好在入队瞬间误判空闲。
        yield return null;
        while (Instance._processing || Instance._queue.Count > 0)
            yield return null;
    }

    IEnumerator ProcessLoop()
    {
        _processing = true;
        int safety = 0;

        while (_queue.Count > 0 && safety < MAX_ITERATIONS)
        {
            safety++;
            var action = _queue.First.Value;
            _queue.RemoveFirst();

#if UNITY_EDITOR
            if (_enableDebugLog)
                Debug.Log($"[ActionQueue] ▶ {action.DebugName}  (剩余:{_queue.Count})");
#endif
            action.Execute();

            // 等待异步动作完成
            float timeout = 30f; // 单个动作最长等 30 秒
            float waited = 0f;
            while (!action.IsDone && waited < timeout)
            {
                yield return null;
                waited += Time.deltaTime;
            }

            if (waited >= timeout)
                Debug.LogWarning($"[ActionQueue] {action.DebugName} 超时（{timeout}s），强制继续");
        }

        if (safety >= MAX_ITERATIONS)
        {
            Debug.LogError($"[ActionQueue] 单次处理循环达到 {MAX_ITERATIONS} 上限，强制停止！可能存在死循环。队列中还有 {_queue.Count} 个待执行动作。");
            _queue.Clear(); // 仅在疑似死循环时清空，避免卡死
        }

        _processing = false;

        // 处理期间可能有新动作在最后一刻入队；若队列非空则再起一轮，避免漏执行。
        if (_queue.Count > 0)
            StartCoroutine(ProcessLoop());
    }
}
