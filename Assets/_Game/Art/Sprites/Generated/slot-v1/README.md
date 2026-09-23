# 卡槽底板（SlotPlate）v1 —— 2026-09-23

## 这轮解决什么

用户指：「这个槽位是不是有点不搭？」

是的，而且原因很具体：**战场 12 个卡位用的是没画过的占位图。**

- 旧素材 `Art/Sprites/Board/SlotPlate.png` = 540×960 的**纯白圆角矩形**，内部 alpha 恒为 `184/255 = 0.722`（占 99.2% 像素），无描边、无内阴影、无材质，是一张真正的占位图。
- prefab `Prefabs/Board/Slot_0.prefab` 的 Image `m_Color = (1,1,1,0.1)`，即把这张纯白满铺乘 10%。
- 工程是 **Linear 色彩空间**（`m_ActiveColorSpace: 1`），线性空间混色下这一层白会抬得很高：底板处 `(17,24,33)`、卡位处 `(78,79,81)` —— 卡位比底板亮 **3–4 倍**，且从蓝底板上「脱色」成中性灰。这就是它读起来像贴纸的原因。

## 新素材的设计约束（关键，别踩）

卡位这一层是**可运行时上色的**：`BoardSlot.SetSlotColor()` 会把 `slotImage.color` 直接写成状态色 ——
黄 `highlightColor(1,0.92,0.015,1)`、绿 `discardHighlightColor(0.2,0.9,0.2,0.7)`、紫 `(0.6,0.2,0.8)`、蓝、黑（封锁）。
`SelectionDim` 另外把 RGB 乘 `0.45` 表达「不可选」。

所以新素材：

- **RGB 一律纯白 (255,255,255)，全部结构只写在 alpha 通道** —— 状态色照旧原样相乘，C# 一行没动。
- alpha 的**形状**沿旧图的 alpha 遮罩逐像素复制（圆角半径**实测 61 sprite px ≈ 11.7 屏幕像素**，2026-09-23 由 91 个边界点最小二乘拟合，rmse 0.96；此处原写作 55 是估计值），轮廓零风险。
- 结构性只有四处：外圈细亮边（凹槽内壁）、上内影（受光反侧）、下内侧受光、左右两侧的弱版。

屏幕值换算（底板取 `(17,24,33)`，一格 alpha 0.10）：

| 位置 | 素材 alpha | 屏幕值 |
|---|---|---|
| 细亮边 | 0.80 | ≈ 80 |
| 内部 | 0.22 | ≈ 45 |
| 上内影 | ~0.10（最低点触 0） | ≈ 30 |
| 下受光 | 0.42 | ≈ 62 |

## 三个候选

| 文件 | 内部 alpha | 说明 |
|---|---|---|
| `SlotPlate_v6.png` | 0.45 | 「凹槽」：结构最明显，但仍克制。**选择期高亮填充会从 0.72 降到 0.45** |
| `SlotPlate_v6_soft.png` | 0.30 | 介于两者之间 |
| `SlotPlate_v6_line.png` | 0.22 | 「刻线」：只留细亮边 + 上内影，占面色最小。**当前已安装** |

三版的边宽参数：亮边 6、上内影 22、左 14、下 18、右 12（sprite px；540 px 宽对 104 屏幕 px ≈ 5.19 倍）。

> **已被 `Generated/slot-slim-v1` 取代（2026-09-23 同日晚）。**
> 用户看过这里的 v6 预览后只说「收细」，于是亮边从 6px@0.80 收到 3px@0.46、线层由双线改成单细线。
> 现在装在 `Art/Sprites/Board/` 的是 `Plate_v7_b.png` + `Edge_v7_b.png`，详见 `Generated/slot-slim-v1/README.md`。
> 本节以下内容保留为 v6 这轮的历史记录。

## 已安装（v6，已被 v7 取代）

`SlotPlate_v6_line.png` → `Art/Sprites/Board/SlotPlate.png`（**只覆盖 .png，`.meta` 未动**，guid `4e0f3b55cbb04178b74f948fe0b9014e` 不变 → 场景 / prefab 零改动）。实测新素材 alpha：内部 56、亮边 204。

- 备份：`%TEMP%\slot-backup-20260923-185945\SlotPlate.OLD.png`
- 回退：把备份拷回 `Art/Sprites/Board/SlotPlate.png` 即可

换成别的候选：

```powershell
Copy-Item "Assets/_Game/Art/Sprites/Generated/slot-v1/SlotPlate_v6.png" `
          "Assets/_Game/Art/Sprites/Board/SlotPlate.png" -Force
```

## 另一条路（本轮未采用）

不动素材、只把 prefab 的一格 alpha 从 `0.10` 压到 `0.06`，静置会更收。
代价：`SelectionDim` 表达「可选中 / 不可选中」靠的是同 alpha 下的白 vs 45% 灰，**静置 alpha 一降，这个明暗差同步减半**。
本轮给的 `SlotPlate_v6_line.png` 是靠「边亮、面淡」拉开层次，不吃这条明暗差，所以优先选它。

## 预览

- `preview-slot-v6-board.png` —— 4 格全屏对照（① 现状 / ② 凹槽 0.45 / ③ 刻线 0.22 / ④ 刻线+prefab 0.06）
- `preview-slot-v6-zoom.png` —— 同一批 3 倍局部

预览不是「另画的 mockup」：是拿实机截图，按 `m_ActiveColorSpace: 1` 的线性混合把**旧卡位逐像素反解回底板**（`board = (lin - oldEff) / (1 - oldEff)`，`oldEff = (a/255) × 0.10`），再叠新素材重算出来的。校验：4 格在 12 个卡位格的 8 个取样点上，① 格与源图逐值相同、②③④ 格的实测值与预测值全部对齐（差 ≤ 2）。

## 本轮没动

- `SlotEdge.png`（线层，`SlotEdgeOverlay` 每帧镜像底板色；静置 alpha 0.10 < 阈值 0.25 → 不显示，只在高亮态出现）—— 未改，高亮态行为与改前一致。
- 卡位矩形尺寸 / prefab / 场景 —— 全未动。
- 形状：仍是原轮廓（圆角 ≈10 屏幕像素）。曾试过把圆角放大到 25 屏幕像素去贴近底板的大圆角语言，判断为「改轮廓风险大于收益」而放弃，候选文件已删。

生成脚本：`Tools/cardframe/SlotPlateV6.ps1`（形状遮罩 / alpha 结构 / 预览合成）。