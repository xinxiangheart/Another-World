# 伤害流水线 — 统一伤害结算

> 创建: 2026-07-13 | 状态: 计划中 | 预估: 1.5 天

## 现状 🔴

`BattleManager.ApplyDamageLoop` (L1003-1300) 中伤害修改散落为硬编码 if-checks：

```
int actualDamage = dmg.Item2;                        // raw
if (isYinYang && !silenced)  actualDamage -= 1;      // 阴阳 — Stage4
if (poisoned)                 actualDamage *= 2;      // 中毒 — Stage2
if (tempHealthBoost > 0)      absorb...               // temp boost — Stage2
if (templateID == "01512")    clampMin(1);            // 万象镜面 — Stage4
if (lord != null)             redirect→lord;          // 领主 — Stage5 重定向
if (braveTemplateID=="01514") follower absorb...      // 勇者 — Stage2
if (hasLifePriestBlessing)    revive...                // 生命祭司 — Stage2
if (conqueror)                accumulate...            // 征服者 — Stage3后
ApplyDamageToMinionPublic is called from 20+ scattered locations, each
doing its own pre-modifications (overclock, trait checks, etc.).
```

同一个伤害计算分散在 4+ 个文件中，新增 Buff/Debuff 需要改核心循环。

## 目标架构

```csharp
public static class DamagePipeline
{
    public static DamageResult Process(DamageInput input)
    {
        var ctx = new DamageContext(input);
        // Stage1_Give: 攻击方增益 (slotTempAttackBoost, tier, aura hooks)
        ctx.damage = ApplyGiveModifiers(ctx);
        // Stage2_Receive: 防守方减益 (shield, poison, tempHealthBoost, aura hooks)
        ctx.damage = ApplyReceiveModifiers(ctx);
        // Stage3_FinalGive: 攻击方最终修正 (overclocked, conductorDouble)
        ctx.damage = ApplyFinalGiveModifiers(ctx);
        // Stage4_FinalReceive: 防守方最终修正 (yinYang, 万象镜面, cannotHeal)
        ctx.damage = ApplyFinalReceiveModifiers(ctx);
        // Stage5_Apply: 实际执行 (lordRedirect, followerAbsorb, HP subtract)
        return Apply(ctx);
    }
}
```

### AuraBase 扩展

```csharp
public class AuraBase
{
    // 现有字段不变
    public virtual int ModifyDamageOutgoing(DamageContext ctx) => ctx.damage;
    public virtual int ModifyDamageIncoming(DamageContext ctx) => ctx.damage;
}
```

### 五阶段对应现有逻辑

| Stage | 逻辑 | 当前位置 |
|-------|------|----------|
| S1 Give | slotTempAttackBoost, tier-based | `ProcessPair` L798-1019 |
| S2 Receive | shield absorb, poison*2, tempHealthBoost absorb, lifePriest revive | `ApplyDamageLoop` L1017-1021, L1028-1044, L1128-1149 |
| S3 FinalGive | overclocked*2, conductor double | `CardInstance.overclocked`, `BoardSlot.ExtractDeathData` |
| S4 FinalReceive | yinYang -1, 万象镜面 clamp(1) | `ApplyDamageLoop` L1025-1026, L1047-1048 |
| S5 Apply | lord redirect, follower absorb, actual HP subtraction | `ApplyDamageLoop` L1051-1057, L1060-1124, L1154 |

### `ApplyDamageToMinionPublic` 调用点分析

分散在 ~20 处：
- `BoardSlot` 进场 AOE (佣兵/恐怖分子/03504 等)
- `CardDrag` 法术伤害 (致命一击/血拼/箭雨等)
- `BattleManager` 战斗伤害 (ProcessPair/FirstStrike)
- `HandManager` 抛置伤害 (难民/不稳定实验品)
- `DeathHandlers` 退场 AOE (03513 断罪者)
- `Card3DHover` 抛置伤害

统一后所有调用点为：`DamagePipeline.Process(new DamageInput(attacker, defender, baseDamage))`

## ✅ D1-D5 — 五阶段全量落地 (2026-07-13)

**修改文件:** `Assets/_Game/Scripts/Damage/DamagePipeline.cs`

五阶段全部包含实际逻辑，从 ProcessPair + ApplyDamageLoop 提取：

| 阶段 | 迁移的逻辑 | 来源 |
|------|-----------|------|
| S1 Give | slotTempAttackBoost, 暴徒(01114), 破防者(01328), 猎犬(01118), 投机者(01125), 反社会(01341), 阴阳+1 | ProcessPair |
| S2 Receive | 护盾吸收, tempHP吸收, 中毒×2, 领主重定向, 追随者挡死, 祭司复活 → stopped | ApplyDamageLoop |
| S3 FinalGive | 超频(02215)×2 | ApplyDamageLoop pre-check |
| S4 FinalReceive | 阴阳-1, 万象镜面(01512) clamp(1) | ApplyDamageLoop |
| S5 Apply | X值累计, 母巢累计, 征服者累计, HP扣减, DamageSourceMarker | ApplyDamageLoop |

**零调用点改动** — `ApplyDamageToMinionPublic` / `ApplyDamageLoop` / `ProcessPair` 均未改动。
骨架+实现就绪，后续可逐步切换调用点到 `DamagePipeline.Process()`。

## ✅ D6-D7 — ApplyDamageToMinion + ApplyDamageLoop 双路径切换完成 (2026-07-13)

**修改文件:** `BattleManager.cs` (两次), `DamagePipeline.cs`

| 路径 | 旧代码 | 新代码 | 节省 |
|------|--------|--------|------|
| `ApplyDamageToMinion` (法术/特质/AOE) | 130行硬编码 | 8行 `DamagePipeline.Process()` | 122行 |
| `ApplyDamageLoop` (战斗) | 130行重复硬编码 | 5行 `DamagePipeline.Process()` | 125行 |

**所有伤害路径已统一走 DamagePipeline.Process()** — 护盾吸收/中毒×2/阴阳-1/超频×2/tempHP/领主重定向/追随者挡死/祭司复活/X值/母巢/征服者/DamageSourceMarker 全部集中处理。

**旧空壳删除:** `Assets/_Game/Scripts/Battle/DamagePipeline.cs` (之前遗留的占位文件)

## ✅ 伤害流水线 — 全部完成

| Step | 内容 | 状态 |
|------|------|------|
| D1 | 骨架 (DamageInput/Context/Result/5-stage framework) | ✅ |
| D2-D5 | 五阶段全量迁移 + AuraBase 虚方法 | ✅ |
| D6 | ApplyDamageToMinion 委托 Pipeline | ✅ |
| D7 | ApplyDamageLoop 委托 Pipeline + 残体删除 | ✅ |

剩余: BattleManager 的 `FindLordOnField`/`ApplyDamageToMinion`(public wrapper) 等辅助方法可按需标记 deprecated 或清为私有。无阻塞项。

## 与 EffectRegistry 的关系

```
用户打牌 → EffectDispatcher.Dispatch(Enter/Spell/Discard/Exit)
  → handler 计算 baseDamage
  → DamagePipeline.Process(input)    ← 新
  → ActionQueueManager 排队执行
  → CheckAndHandleDeaths
```

伤害流水线是 EffectRegistry 的下一层——效果注册解决了"哪个效果触发"，伤害流水线解决"伤害怎么算"。
