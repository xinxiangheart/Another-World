using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召唤演出的独立推进器。卡牌自己的 Update 会被失活 / 禁用 / 域重载打断，
/// 演出状态一旦停住就会表现为「卡直接出现在槽位」。这里用一个常驻对象统一推进，
/// 卡牌即使暂时不在激活层级里也能把动画走完；重新激活时只需套用当前姿态。
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class SummonAnimationTicker : MonoBehaviour
{
    static SummonAnimationTicker _instance;
    readonly List<Card3DInstance> _targets = new List<Card3DInstance>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
    }

    static void Ensure()
    {
        if (!Application.isPlaying || _instance != null) return;

        GameObject host = new GameObject("[SummonAnimationTicker]");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<SummonAnimationTicker>();
        Debug.Log($"{Card3DInstance.SummonTracePrefix}[ticker] CREATE instance={_instance.GetInstanceID()}");
    }

    public static void Register(Card3DInstance instance)
    {
        if (instance == null) return;
        Ensure();
        if (_instance == null)
        {
            Debug.LogWarning($"{Card3DInstance.SummonTracePrefix}[{instance.name}#{instance.GetInstanceID()}] TICKER_REGISTER_FAILED");
            instance.MarkSummonTickerRegistered(false);
            return;
        }

        if (!_instance._targets.Contains(instance))
            _instance._targets.Add(instance);
        instance.MarkSummonTickerRegistered(true);
        Debug.Log($"{instance.SummonTraceLabel} TICKER_REGISTER count={_instance._targets.Count} ticker={_instance.GetInstanceID()}");
    }

    public static void Unregister(Card3DInstance instance)
    {
        if (_instance != null)
            _instance._targets.Remove(instance);
        if (instance != null)
        {
            Debug.Log($"{instance.SummonTraceLabel} TICKER_UNREGISTER count={(_instance != null ? _instance._targets.Count : -1)}");
            instance.MarkSummonTickerRegistered(false);
        }
    }

    void Update()
    {
        // 与卡牌动画时长使用同一套防长帧口径；演出本身只受 unscaled 时间影响。
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (dt <= 0f) return;

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            Card3DInstance target = _targets[i];
            if (target == null)
            {
                Debug.LogWarning($"{Card3DInstance.SummonTracePrefix}[ticker] DROP_DESTROYED index={i} countBefore={_targets.Count}");
                _targets.RemoveAt(i);
                continue;
            }
            if (!target.IsSummonAnimating)
            {
                Debug.Log($"{target.SummonTraceLabel} TICKER_DROP_NOT_ANIMATING");
                target.MarkSummonTickerRegistered(false);
                _targets.RemoveAt(i);
                continue;
            }

            target.TickSummon(dt);
        }
    }

    void LateUpdate()
    {
        // 普通脚本 / 手牌 / 同步链都可能在同一帧把卡关掉。推进器执行顺序在最后，
        // 这里再压一次可见态，保证进入渲染前卡一定处于激活、渲染器开启的状态。
        for (int i = 0; i < _targets.Count; i++)
        {
            Card3DInstance target = _targets[i];
            if (target != null && target.IsSummonAnimating)
                target.EnforceSummonVisibleNow();
        }
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        Debug.Log($"{Card3DInstance.SummonTracePrefix}[ticker] DESTROY targets={_targets.Count}");
        for (int i = _targets.Count - 1; i >= 0; i--)
            if (_targets[i] != null) _targets[i].MarkSummonTickerRegistered(false);
        _targets.Clear();
        _instance = null;
    }
}
