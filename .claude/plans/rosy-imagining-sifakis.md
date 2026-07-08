# 行动队列系统 — 三大 switch 全量改写

## Context（为什么做）

当前所有卡牌效果同步直行于三个巨型 switch：
- `BoardSlot.StartOnEnterEffect`（进场，switch on `templateID`，~50 case）
- `CardDrag.ResolveSpellEffect`（法术，**switch on 中文 `template.effect` 字符串**，~50 case）
- `Card3DHover.HandleDiscardEffect`（抛置，switch on `templateID`，7 case）

两个致命问题：
1. **顺序不可控**：效果 A 触发效果 B 时，执行顺序由代码调用顺序决定。死亡链（恐怖分子进场→AOE→连锁死亡→退场效果→更多进场）靠 `CheckAndHandleDeaths()` 的同步 `do-while` + `HandleDeath`（550 行 if 链）硬串，无法插队、无法排优先级。
2. **多语言被锁死**：法术分发以中文串为 key，任何翻译都会破坏效果逻辑。

目标终态：三大 switch 全部拆成"以 `templateID` 为 key 的独立行动节点"，统一经 `ActionQueueManager` 排队执行；顺序、插队、优先级可控；显示文本降级为可替换的本地化数据，翻译 = 换数据表，零逻辑改动。

已有基础：`Assets/_Game/Scripts/Battle/ActionQueue.cs` 里 `ActionQueueManager` + `IGameAction`/`SyncAction`/`CoroutineAction`/`DelayedAction`/`DeathCheckAction`/`DeathInfo` **已定义但全项目零调用**，可直接采用。选择系统是"协程 + `WaitUntil(flag)` + 全局静态回调 `BoardSlot.onTargetSelected`"（同一时刻只允许一个选择）。

---

## 目标架构

```
卡牌触发点 (进场/法术/抛置/退场/…)
   │  统一改为
   ▼
EffectDispatcher.Dispatch(templateID, Trigger, EffectContext)
   │  查注册表
   ▼
EffectRegistry: Dictionary<(string templateID, Trigger), EffectHandler>
   │  handler 内部只做一件事：把 1..N 个 IGameAction 入队
   ▼
ActionQueueManager  (FIFO；AddToTop 用于反制/插队)
   │  ProcessLoop 逐一 Execute，等 IsDone 再出下一个
   ▼
效果落地 → 若造成伤害则入队 DeathCheckAction → 每个死亡入队"退场分发" → 可能再入队更多进场
```

四个新增核心概念（新目录 `Assets/_Game/Scripts/Effects/`）：
- **`Trigger` 枚举** — 对齐游戏既有优先级：`Enter(进场) > FirstStrike(先手) > Attach(附着) > Discard(抛置) > AttackPair(攻击对位) > Revenge(反击) > Exit(退场) > ActiveExit(主动退场) > Spell(法术)`。
- **`EffectContext`** — 承载 handler 所需一切：`source`(CardInstance)、`sourceSlot`(BoardSlot)、`template`(CardData)、`targetSlot`、`isActiveExit`、`savedAttack`、`discardSlotID` 等（即现在从参数/静态字段读的全部上下文）。
- **`EffectHandler`** = `Action<EffectContext>`；handler 体≈现有每个 case 的 body，但把"直接干活"改为"入队 IGameAction"。
- **`EffectRegistry` + `EffectDispatcher`** — Register / Dispatch 两个静态入口，取代三大 switch 的分发。

`IAction` = 现有 `IGameAction`（`Execute()`/`IsDone`/`Duration` + `DebugName`）。不重命名，避免大面积改动；文档注明二者等价。

---

## 分条实现步骤

### Step 0 — 队列落地 + API 对齐（无行为变化，先让基础可用）
- **`Assets/_Game/Scenes/Game.unity`**：新建 GameObject 挂 `ActionQueueManager`（当前场景无此组件，`Instance` 为 null 时会走"直接 Execute"兜底，所以必须先挂上）。
- **`ActionQueue.cs`**：补齐用户约定 API 别名 —— `enum QueuePosition { Bottom, Top }`；`QueueAction(IGameAction, QueuePosition)`；`AddToBottom()`（=现 `Enqueue`）、`AddToTop()`（=现 `Interrupt`）；`ExecuteAll()` 语义由 `ProcessLoop` 自动驱动，保留并暴露。
- 修 `ProcessLoop` 末尾 `_queue.Clear()`：循环自然排空后 Clear 无害，但会吞掉"排空瞬间新入队"的动作 —— 改为仅在超上限/异常时 Clear。
- 验证：离线 + 联机各跑一局，确认挂上 manager 后旧逻辑（此时仍走老 switch）行为不变。

### Step 1 — 搭效果框架（新增文件，暂不接线）
- 新增 `Effects/Trigger.cs`、`Effects/EffectContext.cs`、`Effects/EffectRegistry.cs`、`Effects/EffectDispatcher.cs`。
- `EffectDispatcher.Dispatch(id, trigger, ctx)`：查表命中则调 handler（handler 负责入队）；未命中则回退调用旧 switch（迁移期双轨并存，保证任何时刻可运行）。
- `EffectRegistry` 用一个 `static` 构造/`RuntimeInitializeOnLoadMethod` 汇总各卡注册。

### Step 2 — 统一死亡解算（最高价值，先做）
死亡链是顺序问题的根源，先把它收进队列：
- 从 `BoardSlot.HandleDeath`（~550 行）拆出**通用死亡管线** → `DeathResolutionAction`：墓地记录 `GraveEntry`、卸载附着、`SetCard(null)`、`Destroy`、X 值刷新、网络同步（`SyncMyBoardToOpponent`/`MarkDirty`）。此段对所有死亡一致执行。
- 把 `HandleDeath` 里按 `templateID` 分支的**每卡退场 body**（01106/01107/01111/01301/01306/01309/01323/01335… 约 30 段）迁为 `Register(id, Trigger.Exit / Trigger.ActiveExit, ...)`。
- `CheckAndHandleDeaths()` → 内部改为入队一个 `DeathCheckAction`（`scanDeaths`=扫 12 格 HP≤0 生成 `DeathInfo`；`handleDeath`=对每个死亡入队 `DeathResolutionAction` + `Dispatch(Exit)`）。
- **同步调用点适配**（~25 处，多在协程里"下一行就比对棋盘"）：提供协程 `ActionQueueManager.WaitForDrain()`（`yield return new WaitUntil(() => PendingCount==0)`），协程调用点改为 `yield return StartCoroutine(WaitForDrain())`；非协程回调点就地改为入队后不再假设即时结果。
- 验证：恐怖分子(01509) 全链、连锁死亡（如触发 03513 死亡 AOE 再连锁）、主动退场（指挥官 01311 双重死亡）逐一手测；联机双端一致。

### Step 3 — 迁移进场效果（`StartOnEnterEffect`）
- 每个 `case "xxxx":` → `Register("xxxx", Trigger.Enter, ctx => {…})`，body 近乎照搬，但：
  - 纯同步效果（注册光环/直接改值）包成 `SyncAction` 入队。
  - 需选择/动画的（Lord 01503、Terrorist 01509、Pirate 01337…）把现有协程包成 `CoroutineAction`（`IsDone` 随协程结束，沿用 `SelectionManager.BeginSelection`+`WaitUntil`，**不改选择系统**）。
- `StartOnEnterEffect` 瘦身为：构造 `EffectContext` → `EffectDispatcher.Dispatch(id, Trigger.Enter, ctx)`。保留开头的 `IsTraitBlocked("进场")` 与"蛊惑之声"重定向作为 dispatch 前置。
- 分批迁移（每批 ~10 张卡，迁完就手测该批），未迁的走 Step 1 的旧 switch 回退。

### Step 4 — 迁移法术效果 + **key 从中文串切到 templateID**（多语言关键步）
- `ResolveSpellEffect` 当前 `switch(template.effect)`（中文）→ 改为 `Dispatch(template.templateID, Trigger.Spell, ctx)`。
- 每个中文 case → `Register(templateID, Trigger.Spell, ctx => {…})`；templateID 从 CardDatabase 与现有中文串一一对应（迁移时逐条建立映射，写进代码注释/映射表）。
- 飞升 03005/腐化 03003 的 `CmdReportTransform`、AOE、x 费用等 body 照搬进 handler；`CheckSpellCondition` 同步改为按 templateID。
- 目标产出：法术逻辑不再引用任何中文文本。

### Step 5 — 迁移抛置效果（`HandleDiscardEffect`）
- 7 个 `case templateID` → `Register(id, Trigger.Discard, ctx => {…})`，`discardSlotID`/`savedAttackForDiscard` 进 `EffectContext`；沿用 `BoardSlot.StartDiscardSelection`。
- `HandleDiscardEffect` 瘦身为 dispatch。

### Step 6 — 收编其余死亡/战斗调用点
- `BattleManager.BattleCoroutine`（先手/攻击/最终伤害阶段的 `do-while` 死亡递归、`ApplyDamageLoop`）、`HandManager`、`TurnManager`、`GetCardPanel` 等剩余 `CheckAndHandleDeaths`/`HandleDeath` 调用点，统一改走队列 + `WaitForDrain()`。
- 战斗阶段多触发同时命中时，按 `Trigger` 优先级入队（进场>先手>…>退场），坐实"优先级可控"。

### Step 7 — 清理 + 多语言脚手架
- 删除三大 switch 残体与 Step 1 的旧 switch 回退分支（确认注册表全覆盖后）。
- 新增以 `templateID` 为 key 的本地化文本表（`CardTextTable`：名称/费用/效果描述/前缀），运行时按语言取；`CardData` 的中文 `effect`/`cardName` 显示改为查表。至此翻译 = 换一张表。

---

## 关键文件
- 改写核心：`Assets/_Game/Scripts/Battle/ActionQueue.cs`（API 别名 + ProcessLoop 修正）
- 三大 switch：`Assets/_Game/Scripts/Board/BoardSlot.cs`、`Assets/_Game/Scripts/UI/Board/CardDrag.cs`、`Assets/_Game/Scripts/UI/Board/Card3DHover.cs`
- 死亡/战斗调用点：`BattleManager.cs`、`HandManager.cs`、`Turn/TurnManager.cs`、`GetCardPanel.cs`
- 复用（不改）：`Manager/SelectionManager.cs`（`BeginSelection`/`WaitUntil` 模式）、`Manager/GlobalEventManager.cs`（`IsTraitBlocked`/光环）
- 新增目录：`Assets/_Game/Scripts/Effects/`（`Trigger.cs`、`EffectContext.cs`、`EffectRegistry.cs`、`EffectDispatcher.cs`、按卡分文件的 handler 注册）
- 场景：`Assets/_Game/Scenes/Game.unity`（挂 `ActionQueueManager`）

## 风险控制
- **全量改写，分批执行，每批后游戏可运行**：Step 1 的"未命中回退旧 switch"是双轨保险 —— 迁一张删一个 case，任意中途都能跑。
- 网络同步先原样保留在各 handler 内（`CmdSyncEnemyDamage`/`SyncMyBoardToOpponent`/`MarkDirty`）；顺序确定后，Step 6 可再收敛为"队列排空时统一同步"。
- 每批迁移必做联机双端手测（host + client），确认对位/死亡/退场链两端一致。

## 验证（Unity，手动为主）
1. 每批迁移后进 Play 模式跑该批卡；重点回归恐怖分子(01509) 长链、连锁死亡、指挥官(01311) 双重死亡、飞升/腐化、抛置 7 卡。
2. 联机：host + client 各一局，验证进场/法术/抛置/战斗死亡两端棋盘一致、能量/手牌一致。
3. 多语言冒烟：Step 7 后切换文本表，确认显示切换而效果逻辑不受影响。
4. 无自动化测试框架，验证以对局手测 + `_enableDebugLog` 打印队列执行序列为准。
