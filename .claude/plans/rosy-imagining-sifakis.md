# 行动队列系统 — 三大 switch 全量改写 ✅ 完成

> 完成日期: 2026-07-13

## 目标架构

```
触发点 → EffectDispatcher.Dispatch(templateID, Trigger, EffectContext)
  → EffectRegistry[(templateID, Trigger)] = handler
  → handler 入队 1..N 个 IGameAction 到 ActionQueueManager
  → ProcessLoop FIFO 逐一 Execute，等 IsDone
```

## Step 完成总览

| Step | 内容 | 新/改文件 |
|------|------|----------|
| 0 | ActionQueue 自举 + IGameAction 类型 | `ActionQueue.cs` |
| 1 | Effects/ 框架 (Trigger/Context/Registry/Dispatcher) | 4 files in `Effects/` |
| 2a | 通用死亡管线 DeathPipeline | +`DeathPipeline.cs`, 改 `BoardSlot.cs` |
| 2b | ~32条退场效果 → Register(Exit/ActiveExit) | +`DeathHandlers.cs`, 改 `EffectContext.cs`/`BoardSlot.cs` |
| 2c | CheckAndHandleDeaths → DeathCheckAction | 改 `BoardSlot.cs` |
| 2d | 28个调用点适配 (3个加WaitForDrain) | 改 `BoardSlot.cs`/`BattleManager.cs` |
| 3 | ~40条进场效果 → Register(Enter) | +`EnterHandlers.cs`, 改 `BoardSlot.cs` |
| 4 | ~43条法术效果 → Register(Spell) (切中文→templateID) | +`SpellHandlers.cs`, 改 `CardDrag.cs` |
| 5 | 7条抛置效果 → Register(Discard) | +`DiscardHandlers.cs`, 改 `Card3DHover.cs` |
| 6 | 战斗死亡链 → DeathCheckAction+WaitForDrain | 改 `BattleManager.cs` |
| 7a | 删旧 switch 残体 (~84KB) | 改 `BoardSlot.cs`/`CardDrag.cs`/`Card3DHover.cs` |
| 7b | CardTextTable 230张卡本地化表 | +`CardTextTable.cs` |

## 当前工作区文件状态

- `Assets/_Game/Scripts/Battle/ActionQueue.cs` — Step 0
- `Assets/_Game/Scripts/Effects/` (10 files) — Step 1–5
- `Assets/_Game/Scripts/Localization/CardTextTable.cs` — Step 7b
- `Assets/_Game/Scripts/Board/BoardSlot.cs` — Step 2–3 精简
- `Assets/_Game/Scripts/Battle/BattleManager.cs` — Step 6
- `Assets/_Game/Scripts/UI/Board/CardDrag.cs` — Step 4 精简
- `Assets/_Game/Scripts/UI/Board/Card3DHover.cs` — Step 5 精简
- `Assets/_Game/Scripts/Network/BoardSyncManager.cs` — 去重守卫
- `Assets/_Game/Scripts/Network/NetworkPlayer.cs` — CmdReportMyBoard 修复
- `Assets/_Game/Scripts/UI/Hand/HandManager.cs` — PlaceIndependentCard
