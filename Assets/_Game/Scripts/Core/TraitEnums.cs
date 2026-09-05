// ============================================================================
// TraitEnums — 特性组系统枚举（数据/逻辑层）
// ============================================================================
//
// TraitCategory（发送方）：这卡能做什么 → CanSend(TraitCategory) 门控 + 类别级禁制
// EffectCategory（接收方）：这卡能被怎样 → CanReceive(EffectCategory) 门控（位标志，支持组合禁）
//
// 与旧散落 bool 的关系：发送类 bool（hasOnEnter/hasFirstStrike/canAttach/attacksFrontRow…）
// 后续归入 TraitCategory 门控；接收类 bool（cannotHeal/ignoreAllCounters…）
// 归入 EffectCategory。旧 bool 并行保留、只读不写，特性组判断优先。
// ============================================================================

/// <summary>特性/行为类别（发送方）。禁制以此粒度或更细的 traitId 生效。</summary>
public enum TraitCategory
{
    Attack,          // 基础攻击
    AttackFrontRow,  // 只能攻击前排
    AttackBackRow,   // 只能攻击后排
    Splash,          // 溅射
    Trigger,         // 触发类（先手/反击/亡语/进场等）
    Damage,          // 伤害
    Heal,            // 治疗
    Buff,            // 增益
    Debuff,          // 减益
    Control,         // 控制
    Aura             // 光环
}

/// <summary>效果类别（接收方）。位标志，支持 BlockReceive 组合禁多项。</summary>
[System.Flags]
public enum EffectCategory
{
    None = 0,
    Healed = 1 << 0,          // 可被治疗
    SpellTargeted = 1 << 1,   // 可被法术选中
    TraitTargeted = 1 << 2,   // 可被特性选中
    AttackTargeted = 1 << 3,  // 可被攻击选中
    Buffed = 1 << 4,          // 可被增益
    Debuffed = 1 << 5,        // 可被减益
    Moved = 1 << 6,           // 可被移动/交换/回手
    Countered = 1 << 7        // 可被反制牌影响（拦截它=免疫反制，如无畏者01319）
}
