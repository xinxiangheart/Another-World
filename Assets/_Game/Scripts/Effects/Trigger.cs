// ============================================================================
// Trigger — 卡牌效果触发时机
// ============================================================================
//
// 用作效果注册表的 key 之一：(templateID, Trigger) → EffectHandler。
// 顺序对齐游戏既有优先级（进场 > 先手 > 附着 > 抛置 > 攻击对位 > 反击 > 退场 > 主动退场），
// 战斗阶段多触发同时命中时可据此排队。
//
// 全量改写路线中，三大 switch 分别对应：
//   Enter    ← BoardSlot.StartOnEnterEffect
//   Spell    ← CardDrag.ResolveSpellEffect
//   Discard  ← Card3DHover.HandleDiscardEffect
//   Exit / ActiveExit ← BoardSlot.HandleDeath 的每卡退场分支
// ============================================================================

/// <summary>卡牌效果触发时机。枚举顺序即优先级（值越小越先触发）。</summary>
public enum Trigger
{
    Enter = 0,        // 进场
    FirstStrike = 1,  // 先手
    Attach = 2,       // 附着
    Discard = 3,      // 抛置
    AttackPair = 4,   // 攻击对位
    Revenge = 5,      // 反击
    Exit = 6,         // 退场（被动死亡）
    ActiveExit = 7,   // 主动退场
    Spell = 8,        // 法术结算
}
