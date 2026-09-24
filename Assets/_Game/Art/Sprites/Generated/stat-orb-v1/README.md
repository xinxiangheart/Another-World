# 己方生命 / 能量图案 v1

> **已于 2026-09-24 安装**：4 张**原地覆盖**到 `Assets/_Game/Art/Sprites/UI/`。
> 只覆盖 `.png`，`.meta` 一律未动（guid 不变 → 场景零改动）。
> 旧图备份在 `%TEMP%\selforb-backup-20260924\`。本目录保留为归档 / 对照稿。

**生命 = 长方形，能量 = 圆形**（用户要求）。四张均 300x300，与场景里的
`Circle` / `Ring` 节点等大（Image 是 Simple + `m_PreserveAspect: 0`，不拉伸），所以**只覆盖 PNG 就行**。

## 场景结构（`Assets/_Game/Scenes/Game.unity`，`SelfStatOrbs` 下）

```
OrbH  300x300 @ (170,205) scale 0.8   -> Circle(300x300) + Ring(300x300) + Health 文本(280x90 @ y+11)
OrbE  300x300 @ ( 65,364) scale 0.5   -> Circle(300x300) + Ring(300x300) + Energy 文本(280x90 @ y+6)
```

- `Circle` 在 `Ring` **之下**，`Ring` 在文本**之下**。所以 Circle = 底盘 + 图标，Ring = 外圈框架。
- `Ring` 上挂 `StatOrbAnimator`：**生命 = Breath**（缩放）、**能量 = Spin**（24 °/秒）。
- 上机实尺寸：生命 300x0.8 = 240px，能量 300x0.5 = 150px。

## 画风

与 `CardFrameV7` / `board-layers-v2` / `topbar-v1` 同源：**深蓝黑石面 + 金细线**。

| 用途 | 色值 |
|---|---|
| 深墨（描边 / 最暗） | `#06090E` |
| 金 | `#C8A44A`（亮 `#E4CB84` / 暗 `#8A6F2E`） |
| 生命主色 | `#B64848`（亮 `#CE979C` / 暗 `#76282E`） |
| 能量主色 | `#689AD6`（亮 `#A5C3E7` / 暗 `#3C6498`） |
| 生命隤面渐变 | `#2C1519` → `#14090C` |
| 能量隤面渐变 | `#172130` → `#090E16` |

> 生命 / 能量主色**直接取自新版 `Icon_Health` / `Icon_Energy`**，所以顶栏图标、左下显示、卡面三处同色。

## 四张图各自做什么

| 文件 | 内容 |
|---|---|
| `SelfHealthCircle.png` | 长方形血条（圆角 22，占画布 x16~284 / y88~212）：隤面渐变 + 内缩金细线 + 顶沿冷光 + **左端心形图标** + 金竖分隔线（x=104） |
| `SelfHealthRing.png` | 血条的**金外框**（深墨描边 + 金本体 + 外沿亮边），尺寸与 Circle 同一个 box |
| `SelfEnergyCircle.png` | 圆盘 r=98（渐变 + 深墨外边 + r88 金细圈 + 上半冷光弧）+ **蚀刻式闪电** |
| `SelfEnergyRing.png` | **三段不等长金弧**（-58~58° / 96~158° / 214~244°）+ 一枚菱形标记（286°） |

### 两个刻意的选择

1. **闪电做成蚀刻（低对比）而不是实心亮色。** 能量数字的 TMP 框是 280x90 居中，**正好压在盘心**——
   旧图也是这个撞法，但旧闪电实心亮蓝，数字跟它抢。现在闪电只做低对比隤刻（`#1E3A57` / 亮 `#4072A2`），数字清楚，
   闪电仍能读出形状。同理，生命的心形**放在左端**，不占数字位。
2. **能量环改成不对称。** 旧环是两条对称弧，而 `StatOrbAnimator` 对能量环跑的是 **Spin 24°/秒**
   —— 对称形状转起来看不出来。现在三段长度差很多的弧 + 一枚菱形标记，自转一眼可见。

## 同时改了一个场景数值

`Assets/_Game/Scenes/Game.unity`，生命 `Ring` 上的 `StatOrbAnimator`：

| 字段 | 旧 | 新 |
|---|---|---|
| `breathMaxScale` | `1.12` | **`1`** |

原因：血条从圆盘变成**长条**后，对称放大 12% 会读成「整个框在缩放」而不是呼吸。
想保留微脉动把它改成 `1.03` 就行；能量环的 `Spin` 未动。

## 预览

`Tools/cardframe/preview/stat-orb-v1.png` —— 1:1 实机尺寸（左旧 / 右新）+ 2x 放大 + 能量环 0/45/90/135° 自转帧。

## 生成脚本

| 文件 | 作用 |
|---|---|
| `Tools/cardframe/StatOrbV1.py` | `python Tools/cardframe/StatOrbV1.py` 全重出（同时写到 `Art/Sprites/UI/` 与本目录） |
| `Tools/cardframe/StatOrbV1Preview.py` | 出上面那张预览（需要旧图备份在 `%TEMP%\selforb-backup-20260924\`） |

可调项（`StatOrbV1.py` 顶部常量）：`BAR`（血条外框）、`DISC_R`（能量盘半径）、`ARC_RO / ARC_RI`（能量环带内外半径，
改细就把差值调小）、各个色常量、`SS = 4`（超采样倍率）。
