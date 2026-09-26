# 大厅背景 v1（深蓝黑石殿 + 中央棋盘徽记）

> **状态：草稿，尚未接入 `Lobby.unity`。** 本目录是归档 / 对照稿；
> 真正被场景引用的图按仓库惯例要落到 `Assets/_Game/Art/Sprites/Backgrounds/`（见下「接线」）。

生成脚本：`Tools/cardframe/LobbyBgV1.ps1`（可重跑，参数集中在文件头）。
方向依据：`AGENTS.md`「界面 / 场景美术方向」——界面美术不走酒馆风，一律与 `Game.unity` 3D 战场同源。

## 与战场的关系：色值 / 线宽 / 母题全部照抄，没有新配色

脚本 `. "$PSScriptRoot/BoardLayersV2.ps1"`，直接复用战场底板那套调色板与画笔工具（它自己带入 `CardFrameV6.ps1`）。
所以下列值不是「接近」，是**同一个变量**：

| 用途 | 量 | 来源 |
|---|---|---|
| 夜空底上下 | `SKY_T #090E19` → `SKY_B #141F31` | L1 `Board_Plate` |
| 中央冷光池 | `GLOW_C #283E5C`，alpha 118（战场 150） | L1 |
| 金 / 金亮 / 金暗 | `#C8A44A` / `#E8D18A` / `#785E28` | 全库统一 |
| 钢 / 钢暗 | `#6E93B0` / `#405670` | L1 / L3 |
| 蓝芯 | `ARC #4A84CE` | L4 `Board_Rune` |
| 最暗（描边 / 压角） | `INK0 #06090E` | 全库统一 |

母题几何是**逐行照抄** L3 `Board_Sigil` / L4 `Board_Rune` 的数值，不是「照感觉画」：

| 元素 | 半径（1200 画布 px） | 线宽 / alpha | 来源 |
|---|---|---|---|
| 金主环 | 430 | 4px / 92 | L3 |
| 金主环外圈 | 466 | 2px / 36 | L3 |
| 钢色内衬 | 414 | 2px / 24 | L3 |
| 环带刻度 24 根（偶数根更长更亮） | 430→448 / 430→468 | 3px / 5px | L3 |
| 外圈标记 12 个（每 3 个换朝向） | 500 | 3.4px / 60 | L3 |
| 主环 4 颗菱形铆钉（斜 45°） | 430 | r22 | L3 |
| 内环 + 内环刻度 | 330 / 318 / 296→322 | 3 / 2 / 3px | L4 |
| 六芒星（两个正三角） | 248 | 3px / **38** | L4 |
| 中心金菱形 / 蓝芯菱形 | 92 / 24 | 4px / 填充 | L4 |

## 为什么是两张图（底板 + 徽记）

`Lobby.unity` 的 `Canvas/Background` 是**拉伸锚**（anchorMin (0,0) / anchorMax (1,1) / sizeDelta 0），
非 16:9 屏幕上贴图会被非等比拉伸。底板里全是渐变与直线，拉伸看不出来；
但徽记全是圆 —— 21:9 上会被拉成椭圆。

所以拆成两张：

| 文件 | 尺寸 | 内容 | 场景里的用法 |
|---|---|---|---|
| `LobbyBack.png` | 2048×1152 | 夜空底 + 冷光池 + 星点 + 铺石缝 + 四角压深 + 内缩金细框（**无圆**） | `Canvas/Background`，拉伸锚铺满 |
| `LobbyBackEmblem.png` | 1200×1200 | 全部圆 / 六芒星（透明底） | 新增一个子 Image，**定尺 + `preserveAspect`**，居中 |

徽记在屏上的目标边长是脚本里的 `$EMBLEM_SCREEN`（当前 **1020**，1920×1080 口径）。

> **实测提醒**：战场上 L3 `Board_Sigil.mat` 的 `_Color` 是 **(0,0,0)**（用户当年要求「中心大圆环压黑」），
> 所以实机里那圈主环是**黑的**，只有 4 颗金珠与 8 个三角以黑形可见。
> `preview-board-v5.png` 上看到的是**贴图原色**，比实机亮。本套取的是「贴图原色」这一档 —— 
> 若要与实机完全一致，把 `$HEX_ALPHA` 与主环 alpha 一起压到 20 上下即可。

## 预览

- `Tools/cardframe/preview/lobby-bg-v1-clean.png` —— 纯背景（底板 + 徽记，无文字无面板）
- `Tools/cardframe/preview/lobby-bg-v1-mockup.png` —— 1920×1080 粗合成：文字按 `Lobby.unity` 实测坐标压上去，
  中央那个方块是**面板底板样例**（720×480，`#1E2938 → #0C111A` + 金细线），用来判断背景会不会跟面板打架

## 复现

```powershell
& Tools/cardframe/LobbyBgV1.ps1
```

**坑**：脚本是 UTF-8 无 BOM + 中文。用 `powershell -File`（Windows PowerShell 5.1）会按 ANSI 解码、直接语法报错 ——
必须在 PowerShell 7 里跑（Codex 的默认 shell 就是 pwsh 7）。

## 接线（下一步，尚未做）

1. **底板**：按 `topbar-v1` 的做法**原地覆盖** `Assets/_Game/Art/Sprites/Backgrounds/LobbyBack.png`，
   `.meta` 一个字不动 → guid 不变 → 场景零改动（`Lobby.unity:4306` 的引用照旧）。
   ⚠️ 本目录（`Generated/`）下的 PNG 是 Unity 默认导入设置（`textureType: 0` = Default，**不是 Sprite**），
   而 `Backgrounds/` 下的是 `textureType: 8` / `spriteMode: 1` / `alphaIsTransparency: 1` —— 覆盖时不要连 `.meta` 一起换。
2. **徽记**：`Lobby.unity` 里没有现成槽位，要在 `Canvas/Background` 之后插一个同级 Image
   （定尺 1020×1020、`preserveAspect`、居中、`raycastTarget` 关掉）。
3. **设置面板**：`AGENTS.md:1310` 记着「遮罩 `ColBackdrop` 黑 72% 保持深色，因为大厅背景接近纯白」。
   背景换成深色后这条前提消失，白底设置面板与遮罩比例要重新核对。