# 行动队列系统 — 三大 switch 全量改写

> **重启入口 (2026-07-09):** Step 0–1 已完成 + 3 个 bug 已修。**从 Step 2 开始** —— 统一死亡解算。

---

## Context

当前所有卡牌效果同步直行于三个巨型 switch：
- `BoardSlot.StartOnEnterEffect`（进场，switch on templateID，~50 case）
- `CardDrag.ResolveSpellEffect`（法术，**switch on 中文 effect 字符串**，~50 case）
- `Card3DHover.HandleDiscardEffect`（抛置，switch on templateID，7 case）

致命问题：顺序不可控（链式反应靠同步 do-while 硬串）、多语言被锁死（法术 key=中文）。目标：统一为 templateID + 队列。

---

## 目标架构

```
触发点 → EffectDispatcher.Dispatch(templateID, Trigger, EffectContext)
  → EffectRegistry[ (templateID, Trigger) ] = handler
  → handler 入队 1..N 个 IGameAction 到 ActionQueueManager
  → ProcessLoop FIFO 逐一 Execute，等 IsDone
```

---

## ✅ Step 0 — 队列落地 + API 对齐

**文件:** `Assets/_Game/Scripts/Battle/ActionQueue.cs`

- 自举 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`（DontDestroyOnLoad 常驻）
- API: `QueueAction(action, position)`, `AddToBottom()`, `AddToTop()`, `ExecuteAll()`, `QueuePosition{Bottom,Top}`,
  `WaitForDrain()`, `IsIdle`, 旧别名 `Enqueue`/`Interrupt` 保留
- `ProcessLoop` 修正：排空后新入队则再起一轮；仅溢出时 Clear
- `IGameAction`/`SyncAction`/`CoroutineAction`/`DelayedAction`/`DeathCheckAction`/`DeathInfo` — 全已定义，零外部调用

## ✅ Step 1 — 效果框架

**新目录:** `Assets/_Game/Scripts/Effects/`

- `Trigger.cs` — 枚举（Enter,FirstStrike,Attach,Discard,AttackPair,Revenge,Exit,ActiveExit,Spell）
- `EffectContext.cs` — 统一上下文
- `EffectRegistry.cs` — `Dictionary<(string,Trigger), EffectHandler>`
- `EffectDispatcher.cs` — `Dispatch(trigger,ctx)` 查表；未命中返回 false（调用点回退旧 switch，双轨保险）

纯附加，未接线到任何旧代码。`ActionQueueManager` 自举已启用，无外部调用。

## ✅ Bug 修复（排查期间）

1. **死亡后再进场**: `CmdReportMyBoard` 陈旧上报复活已死牌 → ci==null 拦截
2. **手牌计数挡抽**: `AddCardToHand`/`AddCardToHandFromInstance` 判满前未清 null → 判前 RemoveAll(null)
3. **独立放置重复模型**: 同步竞态附着块复制 → PlaceIndependentCard 强制 isAttached=false + 三级附着块去重

---

## 🔜 Step 2 — 统一死亡解算（下一步从这里开始）

**价值:** 最高。死亡链的顺序问题根源在此。

**关键文件:**
- `Assets/_Game/Scripts/Board/BoardSlot.cs` — `HandleDeath`(~550行, line 1088), `CheckAndHandleDeaths`(line 1045), `TriggerDeathEffect`(line 1742)
- `Assets/_Game/Scripts/Battle/ActionQueue.cs` — `DeathCheckAction`(line 134), `DeathInfo`(line 198)
- `Assets/_Game/Scripts/Manager/GlobalDeathEventHandler.cs` — 死亡事件总线（水墨深渊皇帝等）

**2a. 从 HandleDeath 拆通用死亡管线**

把 HandleDeath 中所有死亡共用的流程抽为 `DeathResolutionAction`（可用现有 SyncAction 包 lambda 的简化方式）:
1. 清 hasLifePriestBlessing
2. GlobalDeathEventHandler.Trigger(...)
3. 守墓人/未弃之人/沉默检查
4. 反注册光环 (03503/03501/01323/01335/01515/01517/01520/01528)
5. 处理附着物（AncientFairy 重附着 / 非妖精销毁）
6. SetCard(null)
7. _rebornSummon → PlaceCardToSlot 杂兵(03004)
8. 回手逻辑 (shouldReturn03504/01117/03009/01511)
9. Destroy(dyingCard)
10. X 值刷新 + 清除宿主的附着物 + SyncMistHiderDisplay
11. 网络同步 (pure client → SyncMyBoardToOpponent)

**2b. 迁 HandleDeath 每卡退场 body 为 Register(Exit)**

HandleDeath 中 ~30 个 template 分支迁为 `EffectRegistry.Register(id, isActiveExit?ActiveExit:Exit, handler)`。handler 体内把原逻辑包成 SyncAction 入队。

**2c. 改 CheckAndHandleDeaths 走 DeathCheckAction**

`CheckAndHandleDeaths()` → 入队 `DeathCheckAction`:
- `scanDeaths` = 扫 12 格 HP≤0 生成 List<DeathInfo>
- `handleDeath` = 每个死亡入队通用 DeathResolution + Dispatch(Exit)
- `onAllDeathsResolved` = 更新 X 值 + MarkDirty

**2d. 适配 ~25 个调用点**

`CheckAndHandleDeaths` 有 ~25 个调用点。部分在协程里下一行就比对棋盘状态（如 TerroristEnterEffect 的 while 循环）。改为 `yield return ActionQueueManager.WaitForDrain()`。非协程回调点就地入队。

**验证:** 恐怖分子(01509) 全链、连锁死亡(03513 死亡 AOE→再连锁)、指挥官(01311) 双重死亡。联机双端一致。

---

## 后续 Steps 概要

- **Step 3**: 迁进场效果 — StartOnEnterEffect ~50 case → Register(Enter)
- **Step 4**: 迁法术 + 切 templateID — ResolveSpellEffect 从 switch(中文) → Dispatch(id,Spell)（多语言关键步）
- **Step 5**: 迁抛置效果 — HandleDiscardEffect 7 case → Register(Discard)
- **Step 6**: 收编战斗死亡链 — BattleCoroutine 的 do-while 死亡递归走 DeathCheckAction + Trigger 优先级
- **Step 7**: 清理 — 删旧 switch 残体 + 建 CardTextTable 本地化表

---

## 当前工作区文件状态

未提交改动:
- `Assets/_Game/Scripts/Battle/ActionQueue.cs` — Step 0
- `Assets/_Game/Scripts/Effects/` (4 files) — Step 1
- `Assets/_Game/Scripts/Network/BoardSyncManager.cs` — 去重守卫
- `Assets/_Game/Scripts/Network/NetworkPlayer.cs` — CmdReportMyBoard 修复 + 去重
- `Assets/_Game/Scripts/UI/Hand/HandManager.cs` — PlaceIndependentCard 强制 isAttached=false

无诊断日志残留，自举已启用。
