# 卡槽底板 v8 —— 内凹感（上内影加深 + 下内侧受光 + 亮边 1px）

## 这轮解决什么

用户 2026-10-23：「加什么增加细节和质感的光效而不过于吸引人注意」→ 选中的是**卡槽的内高光 / 内阴影**。

v7（`slot-slim-v1`）把边框收到 3px@0.46 之后，卡槽在实机里读起来是一块**平贴的淡色板**：
上内影只铺了约 40 sprite px（≈8 屏幕 px），而下内侧受光又太短，
所以「凹槽」只剩最外面那条细边在提示厚度。

v8 只动**同一条 alpha 公式的三个参数**，轮廓、圆角、内部基色、RGB 纯白全部不动：

| 项 | v7（已装） | **v8-a（本轮安装）** | v8-b（备选，更明显） |
|---|---|---|---|
| 轮廓 / 圆角 | 540×960 / 61px | 同 | 同 |
| 内部基色 alpha | 46（0.180） | 同 | 同 |
| 亮边 | 3px @ 0.46 | **4px @ 0.92** | 6px @ 0.95 |
| 上内影深度 | ~40px | **110px** | 110px |
| 下内侧受光 | ~35px，峰值 112 | **55px，峰值 ~117** | 60px，峰值 ~125 |
| 左 / 右内影 | 有，很短 | 60 / 50px | 70 / 55px |

换算到屏幕：108×192 的格子上，110 sprite px ≈ **22 屏幕 px** 的柔和上内影，
亮边从 0.6px 变成 **0.8px**（实机放大后不再被双线性吃掉）。

## 为什么是「不过于吸引人注意」

- 只改 alpha，不改颜色：静置时整格仍由 prefab 的 `m_Color.a = 0.1` 统一压住；
- 最亮处仍是原来那条边，没有新增第二种色相；
- 没有外发光、没有描边加粗到 2px 以上、没有动画。
- 自检口径：游戏视图缩到 50% 时几乎看不出变化，盯 2–3 秒才察觉「槽是凹进去的」。

## 文件

- `SlotPlate_v8_a.png` —— **已安装**（`Assets/_Game/Art/Sprites/Board/SlotPlate.png`，`.meta` 未动，guid 不变）
- `SlotPlate_v8_b.png` —— 备选，上内影与下受光都再重一档
- `preview-slot-v8.png` —— 3 倍对照（current / a / b），按 Unity 线性空间、`m_Color.a = 0.1` 合成
- `preview-slot-v8-1to1.png` —— 1:1 三格对照

## 换档 / 回退

```powershell
# 换 b
Copy-Item "Assets/_Game/Art/Sprites/Generated/slot-v8/SlotPlate_v8_b.png" `
          "Assets/_Game/Art/Sprites/Board/SlotPlate.png" -Force

# 回退到 v7
Copy-Item "$env:TEMP\slot-backup-20261023-193220\SlotPlate.before-v8.png" `
          "Assets/_Game/Art/Sprites/Board/SlotPlate.png" -Force
```

## 生成

`python Tools/cardframe/SlotPlateV8.py` —— 从当前 `SlotPlate.png` 的轮廓反推圆角矩形 SDF（r=61），
按 v6 的 `New-PlateAlpha` 同式重算 alpha，只把 `RimBand / BT / BB` 换成上表的值。
