# 顶栏套件 v1（暂存）

与 `board-layers-v2`（战场底板 v5）/ `CardFrameV6` 同一套语言：**深蓝黑石面 + 金细线**，金 = `#C8A44A`（与 CardFrameV6 描边金同值），面色梯度也沿用 CardFrameV6（顶 `#1E2938` → 底 `#0C111A`）。

## 关键取舍：轮廓基本照抄旧图（收起按钮除外）

顶栏 9 张贴图的轮廓是**逐列照抄旧图**的：脚本读旧图 alpha 下沿 → 用同样的下沿曲线生成 `GraphicsPath` → 沿它描边。`TopBorder` 的五段阶梯切角、`PhaseBand` 的左右尖角都与旧图对齐。**例外：收起按钮的梯形与指示三角改为现画**（见下「收起按钮缩到 0.8」），不再照抄旧图。

实测（按实心轮廓、α>200 取每列下沿）：`TopBorder` 2048 列**全部吻合**，最大偏差 1px、偏差 >1px 的列 = 0；带斜尖的 `PhaseBand` 最大 3px（25~28 列，全在斜尖那几列的抗锯齿上）。`PhaseHideBtn` 与指示三角已改为现画，不在此列。描边是**骑在**旧轮廓线上的，所以会往下多出 2~3px 的暗色描边晕（α≈80），那是描边本身、不是形状变了，压在深色底板上看不出来。

所以 **9-slice 的左右 82px 边距不用改，场景里的位置 / 尺寸 / 字号一个都不用动，直接覆盖文件即可**。

## 文件 → 目标路径

| 暂存文件 | 覆盖到 | 用途 |
|---|---|---|
| `TopBorder.png` 2048×128 | `Assets/_Game/Art/Sprites/UI/TopBorder.png` | 顶栏底板（Sliced，border 82 保持） |
| `PhaseBand.png` 853×55 | `Assets/_Game/Art/Sprites/UI/PhaseBand.png` | 阶段底衬（承载 5 个阶段圆环的那一条） |
| `PhaseHideBtn.png` 168×42 | `Assets/_Game/Art/Sprites/UI/PhaseHideBtn.png` | 收起 / 展开按钮（梯形缩到 0.8，见下） |
| `PhaseHideArrow.png` 34×26 | `Assets/_Game/Art/Sprites/UI/PhaseHideArrow.png` | 按钮上的指示三角 |
| `Icon_Health.png` | `Assets/_Game/Art/Sprites/UI/Icon_Health.png` | 生命 |
| `Icon_Energy.png` | `.../UI/Icon_Energy.png` | 能量 |
| `Icon_CardCount.png` | `.../UI/Icon_CardCount.png` | 牌数（HandRow / SelfHandRow 两处共用） |
| `Icon_TurnCount.png` | `.../UI/Icon_TurnCount.png` | 回合 |
| `Icon_Settings.png` | `.../UI/Icon_Settings.png` | 设置 |
| `ring_empty.png` 511×511 | `Assets/_Game/Resources/UI/ring_empty.png` | 阶段圆环空槽（RingSlot.ringBackground） |
| `Battle Phase.png` 511×511 | `Assets/_Game/Resources/UI/Battle Phase.png` | 战斗阶段圆环（双剑交叉） |

`.meta` 按各自原文件的设置生成（Sprite / Single / PPU 100 / pivot 0.5,0.5），只换了 guid；顺带把 5 个图标 meta 里那个错的 `spriteBorder: {x: 82, ...}` 清零（图标是 Simple，border 无意义）。

## 这一版改了什么

- **顶栏面**：深蓝黑竖向渐变，顶沿一金一钢两道细线；轮廓 = 外侧压深 + 金线 + 内侧淡金；四角阶梯处留竖刻线，两端各一枚菱形铆钉。
- **阶段底衬**：面色与顶栏**共用同一个渐变函数**（把底衬的局部 y 换算成顶栏的 dest y 取值），并且**只在顶栏下沿以下描边** —— 上端交给顶栏吃住。旧图直接照抄轮廓会出现的「两套金线交叉成 ×」由此消失。
- **五个图标**：统一配方（深墨描边 → 平涂主色 → 一块硬边暗面 → 一块硬边亮面）。34px 真机尺寸下不糊，且不再有五套互不相干的质感。
- **阶段圆环**：从「米黄实心饼 + 棕圈」改成「深蓝黑凹井 + 金圈」，空槽是暗的，头像放上去才亮。
- **收起按钮的梯形缩到 0.8（用户要求）**：不再读旧图轮廓，改为现画一个**居中梯形**并整体缩小 —— 顶边 134.4 宽（`y=0`）、底边 95.2 宽（`y=32.8`）、高 32.8（旧图是顶边占满 168、高 42）。三角形随之从 34x26 收到 **21×15**、描边 5→4，位置居中上移，`(6.5,17.5)/(27.5,17.5)/(17,2.5)`。顶边**不描边**（那一截留给顶栏 / 阶段条压住，避免又出现交叉金线），只描右 / 下 / 左三段。sprite 尺寸仍是 168×42、`.meta` 不变，场景零改动。

## 没动的东西

- `Assets/_Game/Resources/UI/Phase Wheel.png`（木拱，挂在 BasePlate 上）：**游戏里看不到**（实测被遮住 / 不渲染），因此没重画；如果哪天要让它显示，需要单独出一版。
- `PhaseWheelCircle.png`、`RingSlot.prefab`、场景层级、脚本：一律未动。

## 预览

- `Generated/preview-topbar-v1.png` —— ① 真机尺寸（1:1，含圆环 / 按钮 / 数字）② 压在底板 v5 上
- `Generated/preview-topbar-v1-compare.png` —— 旧 vs 新，同一张底板、同一套位置与字号
- `Generated/preview-topbar-v1-sheet.png` —— 素材表（含 34px 真机大小 vs 放大 3 倍）
- Generated/preview-topbar-arrow.png —— 收起按钮旧 vs 新（1:1 与放大 4×，梯形缩到 0.8）

## 生成脚本

`Tools/cardframe/TopBarV2.ps1` —— `Invoke-TopBarV2 <出图目录> <预览目录> <底板图>`。
轮廓读取 / 描边：`Get-BottomProfile` / `New-ProfilePath` / `Stroke-Profile`；面色：`Bar-FaceCol`。