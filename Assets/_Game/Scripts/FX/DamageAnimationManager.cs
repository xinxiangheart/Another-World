using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// DamageAnimationManager — 伤害粒子串行队列管理器（表现层，不参与伤害计算）
// ============================================================================
//
// 接收 DamageFX.Request 的事件入队，串行播放（前一个粒子到达才播下一个）。
// 一方（sourceSide）全部完成后，另一方再开始——按来源方分组，一轮一轮处理。
// 播放期间新入队的事件缓冲到下一轮。空闲时自动处理。
// ============================================================================

public enum DamageFxSource { Player, Spell, Grid, Self, Attacker }

public struct DamageFxEvent
{
    public Vector3 targetWorldPos;  // 目标世界坐标（粒子终点/弹数字位置）
    public int value;               // 伤害数值
    public FloaterType type;        // 浮动数字类型（Damage/Heal 等，通常 Damage）
    public DamageFxSource source;   // 来源类型（决定起点规则）
    public int sourceSide;          // 来源方 0=己方 1=对方（串行分组用）
    public int sourceSlotID;        // 格子来源的槽位ID（Grid 用）；其它 -1
    public bool selfDamage;         // 自伤 → 自身中心特殊轨迹
}

public class DamageAnimationManager : MonoBehaviour
{
    public static DamageAnimationManager Instance { get; private set; }

    readonly List<DamageFxEvent> _pending = new List<DamageFxEvent>();
    bool _processing;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>确保管理器存在（懒加载；无则创建 DontDestroyOnLoad 实例）。</summary>
    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("DamageAnimationManager");
        go.AddComponent<DamageAnimationManager>();
        DontDestroyOnLoad(go);
    }

    /// <summary>入队一个伤害粒子事件。</summary>
    public void Enqueue(DamageFxEvent evt)
    {
        if (_processing) { _pending.Add(evt); return; }
        _pending.Add(evt);
        StartCoroutine(ProcessBurst());
    }

    /// <summary>等待所有伤害粒子播放完成（含当前缓冲 + 处理中）。阶段推进前调用，动画阻塞推进（与攻击动画一致）。</summary>
    public IEnumerator WaitForAll()
    {
        // 逻辑结算已先行（伤害值算好）；这里只等粒子演出串行播完，再让阶段推进
        float timeout = 30f;
        float start = Time.time;
        while ((_pending.Count > 0 || _processing) && Time.time - start < timeout)
            yield return null;
    }

    /// <summary>一轮处理：把当前缓冲按 sourceSide 分组，先播完一组再播另一组（串行）。</summary>
    IEnumerator ProcessBurst()
    {
        _processing = true;
        while (_pending.Count > 0)
        {
            // 快照当前缓冲，按来源方分组
            var side0 = new List<DamageFxEvent>();
            var side1 = new List<DamageFxEvent>();
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].sourceSide == 0) side0.Add(_pending[i]);
                else side1.Add(_pending[i]);
            }
            _pending.Clear();

            // 先播完一方，再另一方（先0后1）
            yield return PlayGroup(side0);
            yield return PlayGroup(side1);
        }
        _processing = false;
    }

    /// <summary>串行播放一组事件（每个粒子到达后播下一个）。</summary>
    IEnumerator PlayGroup(List<DamageFxEvent> group)
    {
        for (int i = 0; i < group.Count; i++)
        {
            var evt = group[i];
            bool completed = false;
            PlayOne(evt, () => completed = true);
            // 等待该粒子到达（协程 yield，不阻塞外部逻辑——本管理器只串行粒子演出）
            float timeout = 5f;
            float start = Time.time;
            while (!completed && Time.time - start < timeout)
                yield return null;
        }
    }

    /// <summary>播放单个粒子：计算起点 → 选轨迹 → 到达时弹数字。</summary>
    void PlayOne(DamageFxEvent evt, Action onArrive)
    {
        Vector3 target = evt.targetWorldPos;
        bool isGrid = evt.source == DamageFxSource.Grid || evt.selfDamage;

        // 规则：起点和终点都有效才播粒子。格子/召唤物来源必须有槽位（起点=该格子位置）
        bool needSlot = evt.source == DamageFxSource.Grid || evt.source == DamageFxSource.Attacker;
        if ((needSlot && evt.sourceSlotID < 0) || target == Vector3.zero)
        {
            // 缺起点或缺终点 → 直接弹伤害数字，不播粒子
            DamageFloater.Show(target, evt.value, evt.type);
            onArrive?.Invoke();
            return;
        }

        // 起点：格子/来源召唤物 → 其格子中心；自伤 → 自身中心；玩家/法术 → 屏幕中心
        Vector3 from;
        if (needSlot)
            from = GetSlotWorldPos(evt.sourceSlotID);          // 格子中心 / 来源召唤物格子位置
        else if (evt.selfDamage)
            from = target;                                     // 自身中心
        else
            from = GetScreenEdge(evt.sourceSide);              // 屏幕中心下/上侧

        if (from == Vector3.zero)
        {
            // 起点位置无效 → 直接弹数字
            DamageFloater.Show(target, evt.value, evt.type);
            onArrive?.Invoke();
            return;
        }

        var particle = DamageParticlePlayer.Get();
        float dur = isGrid ? 0.6f : 0.35f;
        Action arrive = () =>
        {
            DamageFloater.Show(target, evt.value, evt.type);  // 粒子到达 → 弹伤害数字
            onArrive?.Invoke();
        };
        if (isGrid)
            particle.PlaySpecial(from, target, dur, arrive);
        else
            particle.PlayStraight(from, target, dur, arrive);
    }

    /// <summary>屏幕中心最下侧/最上侧的世界坐标（视口底部/顶部中心，距相机一定深度）。</summary>
    Vector3 GetScreenEdge(int side)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        // 用场景中轴深度（卡牌 z≈-5.7 附近）投影，保证粒子与卡牌同深度平面
        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, side == 0 ? 0.0f : 1.0f, 9f));
        return center;
    }

    Vector3 GetSlotWorldPos(int slotID)
    {
        HandManager hm = FindObjectOfType<HandManager>();
        return hm != null ? hm.GetSlotWorldPosition(slotID) : Vector3.zero;
    }
}
