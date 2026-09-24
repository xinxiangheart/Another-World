# HUD 圆形按钮 v1（抽牌 / 隐藏手牌）

> **2026-09-24 安装**：两张**原地覆盖** `Assets/_Game/Resources/UI/HUD/`（`DrawCircle.png` / `EyeCircle.png`，均 768×512）。
> 只覆盖 `.png`，**`.meta` 一字未动**（guid 不变 → 场景 / 预制体零改动，不需要动任何坐标或尺寸）。
> 覆盖前的原图备份在 `%TEMP%\hudbtn-backup-20260924\`；也在 git 里（`git checkout -- Assets/_Game/Resources/UI/HUD` 可整体回退）。
> 本目录保留为归档 / 对照稿。

## 用途与场景节点

| 贴图 | 挂在哪 | 节点 | 位置 | 备注 |
|---|---|---|---|---|
| `DrawCircle.png` | `CardCanvas/DrawCardButton` | 180×120 | `(810, -420)` | 子节点 `DrawCountText`（剩余抽牌数，TMP）**居中压在盘心** |
| `EyeCircle.png` | `CardCanvas/ToggleHandButton` | 180×120 | `(808.5, -253.5)` | `ToggleHand.cs` 不改图也不改色 → **单态图标**即可（悬停只是 Button ColorTint 相乘） |

## 尺寸口径（这一版最关键的一条）

贴图 768 贴图像素 ↔ 屏幕 180px，**1 贴图像素 = 0.234 屏幕像素**。所以线宽不能按贴图像素拍脑袋：

| 部位 | 本版尺寸（贴图像素） | 折合屏幕 |
|---|---|---|
| 整体轮廓半径 | 247（＝旧图 bbox 494 / 2，**与旧图一致**） | 57.8 px |
| 外墨边 | 14 | 3.3 px |
| 金环（外沿 230） | 9 | 2.1 px |
| 内侧淡金发丝线（r 208） | 3 | 0.7 px |
| 环上刻度 ×8 / 菱形铆钉 ×4 | 10 / 30 | 2.3 / 7 px |

> 中间试过一版「金环加粗到 21（≈5 屏幕像素）＋盘心放大卡」，观感更像奖章但中心被卡面吃掉、与旁边生命 / 能量球也不像；**已弃用**，本目录只保留当前这版。

## 两个母题

- **抽牌**：数字占了盘心（实机量得约占盘径 0.21~0.32），所以母题不抢中心 —— 盘底压一对**奶油小卡**（96×128，前亮后暗、各带一枚暗色菱形，取色自新版 `Icon_CardCount`），编号与刻度留在盘环上。语义上等于「数字印在牌上」。
- **隐藏手牌**：盘心让给**骨白眼 + 暗瞳 + 金瞳环 + 奶油高光**（巩膜沿用旧图实测的 196×110，虹膜沿用旧图的 ~70 直径），外描边奶油 `#E0D2B0`。

## 画风

与 `CardFrameV7` / `topbar-v1` / `stat-orb-v1` 同一套语言：**深蓝黑石面 + 金细线**。

- 墨 `#06090E`，金 `#C8A44A`（亮 `#E4CB84` / 暗 `#8A6F2E`），钢 `#8EA2B4`
- 盘面渐变 `#1E2938 → #0C111A`（同 `topbar-v1` 顶栏面）
- 奶油三色取自新版 `Icon_CardCount`：`#D6C298` / `#E0D2B0` / `#9A8664`
- 盘面边缘带 30% 暗角（vignette），顶部一道钢色弧光 —— 与 `SelfEnergyCircle` 的写法同源

## 复现

```
python Tools/cardframe/HudButtonV1.py          # 出图 + 原地覆盖 + 归档到本目录
python Tools/cardframe/HudButtonV1Preview.py   # 预览：preview/hud-btn-v1.png（1:1 + 3x + 老图对照）
                                               #       preview/hud-btn-v1-insitu.png（贴回实机截图原位）
```

预览里的数字是**模拟**的（Noto Serif CJK Black，字号按 180×120 节点的 41px 折算），只用来判断数字压在盘心的观感；真机数字由 `DrawCountText` 自己画。