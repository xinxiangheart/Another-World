using System.Collections.Generic;

/// <summary>
/// 跟踪嵌套深度。>0 表示正在处理同时树，阶段推进应暂停。
/// 维护 tag 栈，泄漏时可精确定位是哪个 Enter 没配对 Exit。
/// </summary>
public static class NestingContext
{
    public static int Depth { get; private set; }
    public static bool IsNested => Depth > 0;

    /// <summary>当前未闭合的 Enter tag 栈（栈顶=最新 Enter）。</summary>
    static readonly List<string> _tagStack = new List<string>();

    public static int Snapshot() => Depth;

    public static void Enter(string tag)
    {
        Depth++;
        _tagStack.Add(tag);
        UnityEngine.Debug.Log($"[Nesting] Enter depth={Depth} tag={tag}");
    }

    public static void Exit()
    {
        if (Depth > 0)
        {
            Depth--;
            if (_tagStack.Count > 0) _tagStack.RemoveAt(_tagStack.Count - 1);
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Nesting] Exit called at depth=0 (unbalanced!)");
        }
        UnityEngine.Debug.Log($"[Nesting] Exit depth={Depth}");
    }

    /// <summary>返回当前泄漏（未闭合）的 tag 列表，从最早到最新。</summary>
    public static string GetLeakedTags()
        => _tagStack.Count > 0 ? string.Join(" → ", _tagStack) : "(空)";

    /// <summary>强制重置深度（仅在确信没有活跃嵌套时使用）。</summary>
    public static void ForceClear(string reason)
    {
        if (Depth > 0)
        {
            UnityEngine.Debug.LogWarning($"[Nesting] ForceClear depth={Depth} reason={reason} leakedTags=[{GetLeakedTags()}]");
            Depth = 0;
            _tagStack.Clear();
        }
    }
}
