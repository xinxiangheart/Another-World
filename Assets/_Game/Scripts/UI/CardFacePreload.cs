using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡面那一坨资源的**过场预加载 + 常驻持有者**（2026-09-28）。
/// </summary>
/// <remarks>来由：用户「要预加载，即在开始界面到大厅的转场就直接预加载所有卡面资源，令点开卡牌总览不卡顿」。
///
/// **病在哪**：卡牌总览一打开就 Instantiate 全库 186 张预览，而每张的卡面是**点开那一刻**才 Resources.Load
/// 的（<c>CardDisplay2DNew.LoadSprite</c>：卡面 / 卡框 / 前缀底图 / 特性图标 / 能量·攻击·生命图标都走它）——
/// 136 张卡面加几百个小图标全挤在一次点击里，就是那一下的卡顿。
///
/// **做法**：Welcome -> Lobby 的黑条扫屏 + 全黑期间（见 <see cref="SceneTransition"/>）把这几个目录整读一遍，
/// 并且**用静态字段一直持有**，理由两条：
///   1 **静态引用跨场景存活**。交给 SceneTransition 自己持有等于白读 —— 它渐入完就 Destroy 了，
///     引用一断，切场景时 Unity 跑的那次 UnloadUnusedAssets 会把刚读进来的卡面当场收走；
///   2 静态字段算「被引用」，那一次 UnloadUnusedAssets 收不到它们（这条是 Resources 预加载最容易翻车的地方）。
///
/// **只读不实例化**，读的是**和运行时同一批 Resources 路径**，所以之后 <c>Resources.Load</c> 全部命中内存缓存。
/// 模板上那两张 2D 预制体引用也一并摸一遍 —— build 里 ScriptableObject 上的资源引用是**懒加载**的，
/// 光读模板不摸引用等于没读（`Resources.LoadAll<CardData>` 只把 SO 本体读进来）。
///
/// 调用方只有两处：<see cref="SceneTransition"/> 过场（正常路径）、执行器 / 调试（量数据用）。
/// </remarks>
public static class CardFacePreload
{
    /// <summary>整读的目录（Resources 相对路径）。**改这里就能改「过场里先读什么」**。</summary>
    static readonly string[] Dirs =
    {
        "Cards/Summon",           // 136 张召唤物卡面（Hero/0,1,3,5 · ChosenOne · Special）—— 最重的一坨
        "Cards/Spell",            // 法术卡面（目录现在还是空的，先留着 —— 有图那天不用改代码）
        "Cards/Back And Front",   // 卡框 SummonCard_0..5 / SpellCard_0..5 + 卡背 Card000 / CardSpell000
        "Cards/Frame",            // v7 分层卡框的零件
        "Cards/Materials",        // 卡框材质
        "Cards/PrefixArtBG",      // 前缀底图（卡面缺图时的兜底 —— 法术卡现在全靠它）
        "Icons",                  // 前缀 / 特性 / 状态图标
        "UI",                     // 能量 · 攻击 · 生命 等
    };

    static readonly List<Object> _held = new List<Object>();
    static readonly HashSet<Object> _seen = new HashSet<Object>();
    static readonly List<string> _doneDirs = new List<string>();

    /// <summary>整目录 + 模板预制体都读完了（超时提前收的跑法不置这个）。</summary>
    public static bool IsDone { get; private set; }
    /// <summary>当前持有的资源个数（去重后）。</summary>
    public static int HeldCount { get { return _held.Count; } }
    /// <summary>已经整读完的目录个数。</summary>
    public static int DoneDirCount { get { return _doneDirs.Count; } }
    /// <summary>一共要读几个目录。</summary>
    public static int DirCount { get { return Dirs.Length; } }
    /// <summary>摸过的模板 2D 预制体个数（去重）。</summary>
    public static int WarmedPrefabs { get; private set; }
    /// <summary>上一次 Run 是不是因为超时提前收的。</summary>
    public static bool LastRunTimedOut { get; private set; }

    /// <summary>哪些场景要预加载这一坨（卡牌总览在大厅，所以只有 Lobby）。</summary>
    public static bool WantedFor(string sceneName) { return sceneName == "Lobby"; }

    /// <summary>跑一趟：一个目录读一帧，读到的全部静态持住。
    /// <paramref name="deadline"/> 是 <c>Time.realtimeSinceStartup</c> 口径的截止时刻（&lt;= 0 表示不限时）。</summary>
    public static IEnumerator Run(float deadline)
    {
        if (IsDone) yield break;
        LastRunTimedOut = false;

        for (int i = 0; i < Dirs.Length; i++)
        {
            if (!_doneDirs.Contains(Dirs[i])) LoadDir(Dirs[i]);
            yield return null;                  // 一个目录一帧：整段过场别被挤成一帧卡死
            if (deadline > 0f && Time.realtimeSinceStartup > deadline && i < Dirs.Length - 1)
            {
                LastRunTimedOut = true;
                Debug.LogWarning($"[CardFacePreload] 超时：读完 {_doneDirs.Count}/{Dirs.Length} 个目录、持有 {_held.Count} 个资源，" +
                                 "剩下的交给运行时按需读（下次过场还会接着读完）");
                yield break;                    // IsDone 保持 false：没读完的留着下次接着读
            }
        }

        yield return WarmTemplatePrefabs();
        IsDone = true;
        Debug.Log($"[CardFacePreload] 卡面预加载完成：{_doneDirs.Count} 个目录 / 持有 {_held.Count} 个资源 / 模板预制体 {WarmedPrefabs} 个");
    }

    static void LoadDir(string dir)
    {
        Object[] objs = Resources.LoadAll(dir);     // 目录只能同步枚举（含子目录）—— 反正整段都在黑幕里
        int n = 0;
        foreach (Object o in objs) if (Hold(o)) n++;
        _doneDirs.Add(dir);
        Debug.Log($"[CardFacePreload]   {dir}  {n} 个");
    }

    /// <summary>把模板上那两张 2D 预制体引用摸一遍 —— build 里它们是懒加载的。</summary>
    static IEnumerator WarmTemplatePrefabs()
    {
        var list = new List<CardData>();
        foreach (var d in Resources.LoadAll<CardData>("CardData")) if (d != null) list.Add(d);
        foreach (var d in Resources.LoadAll<CardData>("ChosenOneData")) if (d != null) list.Add(d);

        int n = 0;
        for (int i = 0; i < list.Count; i++)
        {
            CardData d = list[i];
            if (Hold(d.card2DPrefab)) n++;
            if (Hold(d.spell2DPrefab)) n++;
            if ((i & 31) == 31) yield return null;  // 每 32 张让一帧
        }
        WarmedPrefabs = n;
    }

    static bool Hold(Object o)
    {
        if (o == null) return false;
        if (!_seen.Add(o)) return false;
        _held.Add(o);
        return true;
    }

    /// <summary>只给执行器 / 调试用：清掉持有（下次 Run 会重读）。</summary>
    public static void Release()
    {
        _held.Clear(); _seen.Clear(); _doneDirs.Clear();
        IsDone = false; WarmedPrefabs = 0; LastRunTimedOut = false;
    }

    /// <summary>只给执行器 / 调试用：现在持有哪些资源。</summary>
    public static Object[] Snapshot() { return _held.ToArray(); }
}
