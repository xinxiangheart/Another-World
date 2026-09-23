# 图标套件 v1（暂存）

与 `CardFrameV6` / `SteelUi.ps1` 同一材质语言：**深青钢底 + 金边（受光在上 / 背光在下）+ 属性色辉光 + 硬角落影**。
底色沿用开始界面的夜色钢蓝，属性色只出现在辉光与徽记本体上，保证小尺寸下仍能靠「轮廓 + 色相」区分。

## 三种轮廓 = 三类信息

| 轮廓 | 用于 | 依据 |
|---|---|---|
| 六边形 | 属性前缀（渊 / 血歌 / 机械 / 灵能 / 神灵画卷） | 与费用宝石同族：六边形 = 属性/资源 |
| 硬角方牌 | 特性（先手 / 进场 / 退场 / 主动退场 / 反击 / 抛置 / 附着）与召唤物种类 | 与卡面画框同族：方牌 = 规则/身份 |
| 圆环 | 状态（护盾 / 增益 / 减益） | 与卡面底部数值槽同族：圆环 = 状态 |

角标（费用 / 攻击 / 生命 / 种类）**不带底座** —— 卡框 V6 已经画了金环凹槽，图标直接落在凹槽里，避免双层金框。

## 文件 → 绑定来源

目录结构即目标结构，安装 = 整目录覆盖。

| 文件 | 绑定点 |
|---|---|
| `UI/Cost.png` | `CardIcons3D.costIcon` / `CardDisplay2DNew.costIcon`（费用角标） |
| `UI/Attack.png` | `CardIcons3D.attackIcon` 攻击角标 |
| `UI/Health.png` | `CardIcons3D.healthIcon` 生命角标 |
| `UI/Hero.png` | `SummonType.Hero` 种类角标 |
| `UI/Chosen.png` | `SummonType.ChosenOne` 种类角标 |
| `UI/Special.png` | 其余 `SummonType` 种类角标 |
| `UI/First.png` | 特性 先手 `hasFirstStrike` |
| `UI/Enter.png` | 特性 进场 `hasOnEnter` |
| `UI/Leave.png` | 特性 退场 `hasOnDeath` |
| `UI/Exit.png` | 特性 主动退场 `hasActiveExit` |
| `UI/Reverge.png` | 特性 反击 `hasRevenge` |
| `UI/Discard.png` | 特性 抛置 `hasDiscard` |
| `UI/Attach.png` | 特性 附着 `canAttach` |
| `Icons/Prefixes/Abyss.png` | 前缀 渊 |
| `Icons/Prefixes/Blood.png` | 前缀 血歌 |
| `Icons/Prefixes/Mech.png` | 前缀 机械 |
| `Icons/Prefixes/Psychic.png` | 前缀 灵能 |
| `Icons/Prefixes/Scroll.png` | 前缀 神灵画卷 |
| `Icons/Buffs/Shield.png` | 状态 护盾 `hasShield` |
| `Icons/Buffs/Buff.png` | 状态 增益 |
| `Icons/Buffs/DeBuff.png` | 状态 减益（中毒 / 沉默 / 减攻 / 加费） |

尺寸一律 511×511、PPU 100（与原图一致），覆盖 .png 即可，`.meta` 不用动。
注意 `UI/Cost.png` 原为 250×250，现统一到 511×511（`SetFixedSize` 按 `bounds.x` 归一，屏上尺寸不变）。

## 语义色

| 项 | 色相 | 项 | 色相 |
|---|---|---|---|
| 渊 | 紫 `#A05AE8` | 先手 | 琥珀 `#E8C24A` |
| 血歌 | 猩红 `#D8364E` | 进场 | 天蓝 `#5FB4E8` |
| 机械 | 铜 `#D9873C` | 退场 | 冷灰 `#93A4B8` |
| 灵能 | 青 `#45C6E8` | 主动退场 | 橙 `#E8903C` |
| 神灵画卷 | 玉 `#5CC07E` | 反击 | 猩红 `#D8483C` |
| 英雄 | 金 `#C8A44A` | 抛置 | 砂黄 `#D8B878` |
| 天选 | 象牙金 `#F0DCA0` | 附着 | 藤绿 `#6FBF8A` |
| 特殊 | 钢蓝 `#9AB4D8` | 护盾 / 增益 / 减益 | 钢蓝 / 翡翠 / 猩红 |

## 待定项

- `UI/Cost.png` 现在是**中性钢金宝石**（运行时只有一个费用贴图，做不出按费用变色）。
  若要恢复「0 灰 / 1 白 / 2 绿 / 3 蓝 / 4 紫 / 5 金」，需在 `CardIcons3D` / `CardDisplay2DNew`
  里按 `cost` 取图（6 张分色宝石已存在：`Generated/CostV4_0..5.png`），或把宝石直接烘进
  `CardFrameV6_cost0..5` 左上角槽位、同时隐藏 `costIcon`。

## 生成脚本

`Tools/cardframe/IconSetV1.ps1`（`New-IconSetV1 <目标目录>`；三种底座 + 21 枚徽记的绘制都在这里）。
预览：`New-IconSheet.ps1`（全尺寸对照表）、`New-IconFarPreview.ps1`（游戏内实际尺寸 vs 旧图标）。
