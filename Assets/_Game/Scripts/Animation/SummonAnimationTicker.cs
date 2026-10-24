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
    }

    public static void Register(Card3DInstance instance)
    {
        if (instance == null) return;
        Ensure();
        if (_instance == null)
        {
            instance.MarkSummonTickerRegistered(false);
            return;
        }

        if (!_instance._targets.Contains(instance))
            _instance._targets.Add(instance);
        instance.MarkSummonTickerRegistered(true);
    }

    public static void Unregister(Card3DInstance instance)
    {
        if (_instance != null)
            _instance._targets.Remove(instance);
        if (instance != null)
            instance.MarkSummonTickerRegistered(false);
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
                _targets.RemoveAt(i);
                continue;
            }
            if (!target.IsSummonAnimating)
            {
                target.MarkSummonTickerRegistered(false);
                _targets.RemoveAt(i);
                continue;
            }

            target.TickSummon(dt);
        }
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        for (int i = _targets.Count - 1; i >= 0; i--)
            if (_targets[i] != null) _targets[i].MarkSummonTickerRegistered(false);
        _targets.Clear();
        _instance = null;
    }
}
