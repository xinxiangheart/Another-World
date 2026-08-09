/// <summary>
/// 跟踪嵌套深度。>0 表示正在处理同时树，阶段推进应暂停。
/// </summary>
public static class NestingContext
{
    public static int Depth { get; private set; }
    public static bool IsNested => Depth > 0;

    public static int Snapshot() => Depth;

    public static void Enter(string tag)
    {
        Depth++;
        UnityEngine.Debug.Log($"[Nesting] Enter depth={Depth} tag={tag}");
    }

    public static void Exit()
    {
        if (Depth > 0) Depth--;
        else UnityEngine.Debug.LogWarning($"[Nesting] Exit called at depth=0 (unbalanced!)");
        UnityEngine.Debug.Log($"[Nesting] Exit depth={Depth}");
    }

    /// <summary>强制重置深度（仅在确信没有活跃嵌套时使用）。</summary>
    public static void ForceClear(string reason)
    {
        if (Depth > 0)
        {
            UnityEngine.Debug.LogWarning($"[Nesting] ForceClear depth={Depth} reason={reason}");
            Depth = 0;
        }
    }
}
