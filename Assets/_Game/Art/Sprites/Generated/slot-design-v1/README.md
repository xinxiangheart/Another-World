# 卡槽底板 v8「凹槽」（2026-09-24）

## 这一版在做什么

棋盘换成金框 + 深蓝场之后，12 个卡位还是原来那张**淡白圆角面板**（v7），
在深色场里读成「一块贴上去的 UI 板」，和四周的金线语言不是一套。
v8 把卡位做成棋盘语言里的**凹槽**，三层结构：

1. **最外一圈细金线** —— 跟战场金框同色（214,180,104），2.8 sprite px ≈ 0.6 屏像素，不违上一轮的「收细」。
2. **金线内侧一道暗槽** —— 把金线垫起来，凹槽才有厚度（alpha 0.05，近乎透空）。
3. **槽内上暗下亮** —— 光从画面上方下来，顶边压暗、底边提亮，做出「陷进去」的体感。

## 约束（与 v6 / v7 同，本轮未破）

- 轮廓照抄原占位图，540×960 不变，`.meta` 未动 → guid 不变 → **prefab / 场景零改动**。
  实测轮廓与 v7 **逐像素完全一致**（`x=0/20/40/60/…/539` 各列 top/bot 差 0，圆角 r=55）。
- **RGB 只在金线上是暖色，槽内一律接近纯白**（240,246,255）。
  运行时 `BoardSlot.SetSlotColor()` 把 `slotImage.color` 写成状态色
  （黄 可放 / 绿 瘟疫 / 蓝 深海 / 紫 囚牢 / 黑 封锁），底板 RGB 必须接近纯白才能让状态色原样透过。
  金线乘状态色会跟着变色，**黑封锁时线自然消失**（与旧版行为一致）。
- 槽内 alpha 保持 0.18（v7 是 0.22）—— 状态色的可见度靠这一层 + 子节点 `Slot_Edge`，
  所以**不能把槽内压得太透**，否则瘟疫绿 / 深海蓝会一起变糊。这一版只压了 4 个百分点。

## 文件

| 文件 | 金线 | 槽内 | 说明 |
|---|---|---|---|
| `Plate_v8_a.png` | 2.2px @0.80 | 0.15 | 细线档：金线最轻 |
| `Plate_v8_b.png` | 2.8px @0.95 | 0.18 | **推荐档，已装** |
| `Plate_v8_c.png` | 3.4px @1.00 | 0.20 | 深槽档：金线最粗最亮、内影最深 |
| `Plate_v8_b_white.png` | 2.8px @0.95（白） | 0.18 | 不要金色时用这版 |

槽内 RGB = `(240,246,255)`，暗槽 RGB = `(150,172,200)`，金线 RGB = `(214,180,104)`。

## 已装

`Assets/_Game/Art/Sprites/Board/SlotPlate.png` ← `Plate_v8_b.png`（`.meta` 未动，guid `4e0f3b55cbb04178b74f948fe0b9014e`）

换别的档：

```
Copy-Item "Assets\_Game\Art\Sprites\Generated\slot-design-v1\Plate_v8_c.png" "Assets\_Game\Art\Sprites\Board\SlotPlate.png" -Force
```

备份（v7）：`%TEMP%\slot-design-v8-backup\SlotPlate.v7.png`

## 预览

- `Tools/cardframe/preview/slot-v8-realsize-3states.png` —— 真尺寸 × 静置/高亮/黄态 × 5 版并排
- `Tools/cardframe/preview/slot-v8b-states-withedge.png` —— 含子节点 `Slot_Edge` 的合成态（确认金线没和高亮描边挤成双线）
- `Tools/cardframe/preview/slot-v8b-fullboard.png` —— 铺满 12 格的全盘上下文
- `Tools/cardframe/preview/slot-v8-corner.png` —— 左上角 4× 放大，v7 / v7 描边 / v8b / v8c 对比
- `Tools/cardframe/preview/slot-candidates-dark.png` —— 5 版整张并排

生成脚本：`Tools/cardframe/SlotDesignV8.ps1`；预览脚本：`Tools/cardframe/SlotDesignPreviewV2.ps1`
（V2 修掉了老预览脚本的几何：老脚本的行位是旧截图上量的，新截图实测为 3 列 × 4 排，
列 435..539 / 686..790 / 938..1041，排在 y = 155/328/548/736，排高 184。）

## 未动

`SlotEdge.png`（高亮描边）本轮**没改**，仍是 v7 那张。它与新金线在放大后是两条相距约 1 sprite px 的同心线，
合成到屏幕尺寸后并成一条亮边（见 `slot-v8b-states-withedge.png`）。若要收成一条，再说。