/// <summary>
/// 追踪当前"同时树"的嵌套深度。
/// 0 = 顶层（回合循环 / 玩家行动阶段）。
/// >0 = 嵌套中，阶段推进应暂停。
/// </summary>
public static class NestingContext
{
    public static int Depth { get; private set; }
    public static bool IsNested => Depth > 0;

    public static void Enter(string tag) => Depth++;

    public static void Exit()
    {
        if (Depth > 0) Depth--;
    }
}
