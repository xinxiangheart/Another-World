using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大厅「卡牌总览」（Panel_Cards）打开时的入场：把一组组件照开始界面（<see cref="SceneIntro"/>）那套
/// 「从下方浮上来 + 淡入」摆进来。件与**各自起手时刻**都由面板点名
/// （见 <c>LobbyCardCollectionPanel.PlayCardsIntro</c>）—— 本组件只管把动作放出来。
/// </summary>
/// <remarks>2026-09-28 用户的四句话定下这套：
///   「优化动态进入效果…只限进入界面时展示在玩家视角里的（大概只是前两栏），其它的不会有这个动画」
///   →「右侧卡牌也应做」
///   →「不是透明，是类似于左侧栏的滑入，并且第一排和第二排不是同时滑入，第一比第二快一点点，后面排就不需要做了」
///     （第一版「整块卡牌区淡入」作废）
///   →「不再像之前那样 0.32 秒全透明」（第一版把右侧排在左栏之后起手，右侧要空 0.32 s）。
///
/// 口径与开始界面同源（<c>Assets/_Game/Scripts/UI/MainMenu/SceneIntro.cs</c>）：
///   · 缓动 <c>Ease(t) = 1 - (1-t)^3</c> —— 起步快、收尾慢；
///   · 单帧增量封顶 <c>Mathf.Min(Time.unscaledDeltaTime, 0.05f)</c> —— 编辑器进 Play 的头几帧很慢，
///     不封顶的话整段会在看得见之前一帧跑完；
///   · 位置 = <c>basePos + dir * rise * (1 - e)</c>，透明度 = <c>原始 alpha x e</c>
///     （<c>dir</c> 由 <see cref="Group.dir"/> 定：默认 <c>(0, -1)</c> = 从下方浮上来；
///      2026-09-29 大厅「卡牌详情」右栏传 <c>(-1, 0)</c> = 从左边起手，看上去是**向右**滑出来）。
///
/// **动的是「格子 / 卡」本身，不是容器**：容器（Chips_Lv0 / Chips_Lv1 / Grid_Cards）的位置是生成器与
/// ScrollRect 定的，挪容器会把整栏 / 整块一起带走（Grid_Cards 的位置还归 ScrollRect 写）。
///
/// **卡自带 CanvasGroup**（<c>Card00_New_2D.prefab</c> 上就有一个），所以这里**不写死 alpha = 1 /
/// blocksRaycasts = true**，而是记下每件被接管前的原值、收工按原值还原 —— 免得把预制体上另有用意的
/// 配置（interactable / blocksRaycasts）顺手改掉。
/// </remarks>
[DisallowMultipleComponent]
public class LobbyCardsIntro : MonoBehaviour
{
    [Header("动作参数（秒 / 屏幕 px）")]
    [Tooltip("每一件从静止位起手的距离（朝哪个方向看 Group.dir）")]
    public float rise = 24f;
    [Tooltip("单件上浮时长")]
    public float dur = 0.34f;

    /// <summary>一组入场件：<c>items</c> 里所有件在 <c>at</c> 秒（从 <see cref="PlayGroups"/> 那一刻算）**同时**起手。</summary>
    public struct Group
    {
        public List<RectTransform> items;
        public float at;
        /// <summary>起手方向（单位向量，再乘 <see cref="rise"/>）。默认 <c>(0,-1)</c> = 从下方浮上来。</summary>
        public Vector2 dir;
        public Group(float at, List<RectTransform> items) : this(at, items, new Vector2(0f, -1f)) { }
        public Group(float at, List<RectTransform> items, Vector2 dir)
        { this.at = at; this.items = items; this.dir = dir; }
    }

    class Item
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public Vector2 basePos;
        public float baseAlpha;             // 接管前它自己的 alpha（卡预制体带的那个 CanvasGroup 多半是 1）
        public bool baseBlocksRaycasts;     // 同上：收工按原值还原，不写死 true
        public Vector2 dir;                 // 起手方向（乘 rise）
        public float t0;                    // 这一件的起手时刻
    }

    readonly List<Item> _items = new List<Item>();
    float _t;
    bool _run;

    /// <summary>按 <see cref="Group.at"/> 把各件摆成「下方 + 透明」再上浮。重复调用会先还原上一次。</summary>
    /// <remarks>⚠ 传的是**件本身**不是容器：Play 里 <c>Destroy</c> 到帧末才生效，重建时清掉的那批在开面板那一帧
    /// 还挂在容器上（<c>childCount</c> 会比实际多一倍）—— 按容器取会把正在等销毁的旧件也算进去。
    /// 件由面板点名（左栏从自己那份 <c>_chips</c> 出、右侧从 <c>_cards</c> 出），天然只有活的。</remarks>
    public void PlayGroups(List<Group> groups)
    {
        Stop();
        if (groups == null) return;

        for (int g = 0; g < groups.Count; g++)
        {
            List<RectTransform> grp = groups[g].items;
            if (grp == null) continue;
            float at = groups[g].at;

            for (int i = 0; i < grp.Count; i++)
            {
                RectTransform rt = grp[i];
                if (rt == null) continue;

                CanvasGroup cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

                var it = new Item
                {
                    rt = rt,
                    cg = cg,
                    basePos = rt.anchoredPosition,
                    baseAlpha = cg.alpha,
                    baseBlocksRaycasts = cg.blocksRaycasts,
                    dir = groups[g].dir,
                    t0 = at,
                };

                // 起手压成「沿 dir 偏出去 + 全透明」。⚠ 同时关掉射线：不然滑进来的那 0.3 秒里，
                // 一格看不见的筛选格 / 一张看不见的卡压在鼠标下面，一点就切了筛选 / 起了拖拽。
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                rt.anchoredPosition = it.basePos + it.dir * rise;

                _items.Add(it);
            }
        }

        if (_items.Count == 0) return;
        _t = 0f;
        _run = true;
        enabled = true;   // 跑完自己关掉（见 Update 末尾），重播时必须自己开回来 —— Update 不跑=动画不动
    }

    /// <summary>中断并把还活着的件还原成静止态（被 Destroy 掉的那些跳过即可）。</summary>
    public void Stop()
    {
        for (int i = 0; i < _items.Count; i++) Restore(_items[i]);
        _items.Clear();
        _run = false;
    }

    void Update()
    {
        if (!_run) return;

        _t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        bool all = true;
        for (int i = 0; i < _items.Count; i++)
        {
            Item it = _items[i];
            if (it.rt == null) continue;            // 中途被重建掉了：这一件不管

            float p = Mathf.Clamp01((_t - it.t0) / Mathf.Max(0.0001f, dur));
            float e = Ease(p);
            it.cg.alpha = it.baseAlpha * e;
            it.rt.anchoredPosition = it.basePos + it.dir * (rise * (1f - e));
            it.cg.blocksRaycasts = it.baseBlocksRaycasts && e >= 0.9f;   // 快到位了才恢复可点
            if (p < 1f) all = false;
        }

        if (all) { Stop(); enabled = false; }
    }

    void OnDisable()
    {
        // 面板被关掉 / 组件被停：把件还原，别留半透明残影（下次开面板会重播一遍）。
        if (_run) Stop();
    }

    void Restore(Item it)
    {
        if (it == null || it.rt == null) return;
        it.rt.anchoredPosition = it.basePos;
        if (it.cg != null) { it.cg.alpha = it.baseAlpha; it.cg.blocksRaycasts = it.baseBlocksRaycasts; }
    }

    /// <summary>1 - (1-t)^3：起步快、收尾慢（与开始界面同一支）。</summary>
    static float Ease(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }
}
