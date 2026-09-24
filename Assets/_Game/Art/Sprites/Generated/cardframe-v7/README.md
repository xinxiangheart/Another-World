# 卡牌预制体 v7（组件分离 / 贴合棋盘 / 扁平）

**已装进 `Resources/`（2026-09-24）** —— 34 个文件原地覆盖，安装脚本 `Tools/cardframe/InstallCardFrameV7.py`。
覆盖前的原件整目录备份在 `%TEMP%\cardframe-v7-backup-20260924-141239\`（把它覆盖回 `Assets/_Game/Resources/` 即回滚）。
本目录的 PNG 是**组件图层**（未烘焙的源素材）；装进 `Resources/` 的那批是烘焙后的整框，见下面「安装产物」一节。
生成脚本：`Tools/cardframe/CardFrameV7.py`（仓库根目录下 `python Tools/cardframe/CardFrameV7.py` 可整套重出）。

## 这一版在回应什么

| 要求 | 做法 |
|---|---|
| 组件分离 | 卡框从**一张烤死的图**拆成 5 张独立 PNG，各挂一个 Image 节点，可以单独换色 / 单独挪位 / 单独关掉 |
| 更适配背景 | 底色直接取棋盘 `board-layers-v2` 的夜色钢蓝（`Board_Plate` 平均 `#0D141F`），金线取 `Board_Ornament` 的最亮金 `#C9AF65`；卡背的六芒星母题也和棋盘中央同源 |
| 不要太多视觉分层 | **没有**倒角、内阴影、外发光、双层面板、材质贴图。全套只有：一块平底 + 一条金线 + 三块平板（名牌 / 画窗 / 数值条）。相邻色阶差 6–8 级，不做出「凸起」的错觉 |
| 名字靠左 / 种类移到名字右边 | 名牌左侧留 120px 给费用徽，名字从 x=160 起**左对齐**（可用宽 440px），种类徽固定在**名牌右端** x=666 |

## 文件 → 绑定点（对照 `Card00_New_2D.prefab`）

画布 768×1344、PPU 100，与现役 `SummonCard_*.png` 同规格，**同尺寸整层替换**。

| 文件 | 挂在 | 说明 |
|---|---|---|
| `Card_Body.png` | 卡身底板 | 圆角 44、竖直渐变 `#121B27 → #090E16` |
| `Card_Edge.png` | 外金线（**费用档**） | 22px 实色边带 + 1.5px 暗分隔 + 内侧 1.6px 细线 + 四角钉。现役靠 34px 实色带区分数值，这里保留「一条实色带」但压到 22px、不倒角 |
| `Card_NamePlate.png` | 名牌 | 平板 + 金线 + 两端菱钉；文字区 x 160–600 |
| `Card_ArtWindow.png` | 画窗 | 金线 2.2px + 内侧 1px 暗线；窗口中间是**透明洞**，原画从下层透出；召唤 x 76–692 / y 268–1043，法术 x 76–692 / y 268–856（短窗，给底部说明留位） |
| `Card_StatPlate.png` | 数值条 | 平板 + 金线 + 中缝；生命槽心 x=176、攻击槽心 x=592、y=1174 |
| `Card_Back.png` | 卡背 | 同底板 + 金线 + 中央六芒星 / 菱形 / 16 颗外圈铆钉 |
| `Card_Edge_0..5.png` | 同上，六档分色 | 组件分离后**只换这一层**就能按费用换色，不用再出 6 张整框 |
| `Card_Edge_Tint.png` | 同上，纯白版 | 挂上去用 `Image.color` 乘色 —— 一张图管六档，且能**运行时**随费用变（减费 3→2 当场从蓝跳绿） |
| `Badge_Cost.png` / `Badge_Cost_0..5.png` | `costIcon` | 六角凹槽，中性金 / 按费用分色 |
| `Badge_Attack.png` | `attackIcon` | 圆槽 + 交叉双剑（钢蓝双色块 + 金护手） |
| `Badge_Health.png` | `healthIcon` | 圆槽 + 血滴（两档硬边 + 一枚硬边高光） |
| `Badge_Type_Hero/Chosen/Special.png` | `typeIcon` | 菱形槽 + 牛角盔 / 十字 / 四芒星 |
| `Icon_Prefix_*.png` | `prefixIconPath` → `Icons/Prefixes/` | 渊 / 血歌 / 机械 / 灵能 / 神灵画卷，六角 + 属性色 |
| `Icon_Trait_*.png` | `traitIconPath` → `Resources/UI/` | 先手 / 进场 / 退场 / 主动退场 / 反击 / 抛置 / 附着，方牌 |
| `Icon_Status_*.png` | `statusIconPath` → `Icons/Buffs/` | 护盾 / 增益 / 减益，圆环 |

> 换名对不上时按「语义」绑定，别按文件名硬套：`Icon_Trait_Revenge.png` 对应现役的 `Reverge.png`（拼写不同）。

## 尺寸 / 锚点速查（768×1344 画布，左上为原点）

```
卡身       inset 6   圆角 44
边带       inset 6–28  22px  暗分隔 inset 29.75  内侧细线 inset 40  1.6px
名牌       x 40–728  y 40–186    圆角 18
费用徽     (82,113)  128px
名字       x 160 起，竖直中心 y=113
种类徽     (666,113) 100px
画窗       x 76–692  y 268–1043 / 268–856  圆角 14  （中间透明）
数值条     x 40–728  y 1042–1306 圆角 18
生命槽     (176,1174) 118px      攻击槽 (592,1174) 118px
```

## 怎么调

| 想要 | 改哪 |
|---|---|
| 金线更亮 / 更暗 | `Card_Edge.png` 的 `GOLD`（`CardFrameV7.py` 顶部常量），或直接在 Unity 里用 `Image.color` 调 |
| 边带更宽 / 更窄 | `CardFrameV7.py` 的 `BAND`（现 22.0，现役是 34） |
| 按费用换色 | 用 `Card_Edge_0..5.png`，或对同一个 `Card_Edge.png` 乘色（组件分离后这条才成立） |
| 卡身更亮 / 更暗 | `BODY_T` / `BODY_B` |
| 名字区更宽 | `NAME_X0/NAME_X1`（占位预览用的自动缩放区间） |
| 徽标大小 | `assemble()` 里的 `sz`；真机改预制体节点 sizeDelta |
| 再扁一点 | 把 `Card_NamePlate` / `Card_StatPlate` 的 `PLATE_T/PLATE_B` 调成与 `BODY_T/BODY_B` 相同 —— 两块板就「消失」成纯金线分区 |

## 费用档怎么区分（2026-09-24 补）

现役 `SummonCard_0..5.png` 是**六张整框**，靠外圈一条 **34px 实心色带**区分档位（实测左边框 x 37–70）：

| 费 | 现役实测 | 新方案（提亮，暗底上要够亮） |
|---|---|---|
| 0 | `#8C8C8C` | `#929AA4` |
| 1 | `#F5F0E4` | `#E2DFD4` |
| 2 | `#3C8C3C` | `#56B068` |
| 3 | `#325AA0` | `#568CD6` |
| 4 | `#643C96` | `#986CD0` |
| 5 | `#E8C850` | `#E2BA56` |

拆开之后，档位色只活在 **`Card_Edge*` 这一层**：

- 最省事：只放 `Card_Edge_Tint.png`（纯白），运行时 `Image.color` 给色 —— **一张图管六档**，还能随减费实时变色
- 不想写代码：六个 `Card_Edge_0..5.png` 直接按费取图
- 费用徽同步用 `Badge_Cost_0..5.png`，颜色与边带同源

## 预览

- `Tools/cardframe/preview/cardframe-v7-cost-tiers.png` —— 现役六整框 vs 新方案六边带 + 色板对照
- `Tools/cardframe/preview/cardframe-v7-components.png` —— 12 个组件分层表
- `Tools/cardframe/preview/cardframe-v7-card.png` —— 组装后的正面 / 带原画 / 卡背
- `Tools/cardframe/preview/cardframe-v7-onscreen.png` —— 棋盘底上**实机尺寸**（104×182）三态 + 旧卡对照，带 ×2.5 放大
- `Tools/cardframe/preview/cardframe-v7-icons.png` —— 前缀 / 特性 / 状态图标全套

## 安装产物（2026-09-24）

`Tools/cardframe/InstallCardFrameV7.py` 已执行，**34 个文件原地覆盖**。预制体没改——因此装进去的是**烘焙成一张的整框**（组件分离要到改预制体那一步才真正生效）：

| 覆盖位置 | 数量 | 由哪几层烘成 |
|---|---|---|
| `Cards/Back And Front/Summon/SummonCard_0..5.png` | 6 | `Card_Body` + `Card_Edge_i` + `Card_NamePlate` + `Card_ArtWindow`（挖洞）+ `Card_StatPlate` |
| `Cards/Back And Front/Spell/SpellCard_0..5.png` | 6 | `Card_Body_Spell` + `Card_Edge_i` + `Card_NamePlate` + `Card_ArtWindow_Spell`（短洞）+ `Card_StatPlate_Spell` |
| `Cards/Back And Front/Back.png` | 1 | `Card_Back` |
| `UI/`（Cost / Attack / Health / Hero / Chosen / Special / First / Enter / Leave / Exit / Reverge / Discard / Attach） | 13 | `Badge_*` / `Icon_Trait_*` |
| `Icons/Prefixes/`（Abyss / Blood / Mech / Psychic / Scroll） | 5 | `Icon_Prefix_*` |
| `Icons/Buffs/`（Shield / Buff / DeBuff） | 3 | `Icon_Status_*` |

覆盖前全量备份：`C:\Users\22589\AppData\Local\Temp\cardframe-v7-backup-20260924-141239\`（含 `MANIFEST.txt`，34 条 old→new 尺寸记录）。回滚 = 把该目录整体覆盖回 `Assets/_Game/Resources/`。

两个既实约束（改图时必须守）：

- **图标尺寸不能变**：`CardIcons3D.SetFixedSize` 按 `sprite.bounds.size.x` 归一，新图像素尺寸与原图不一致就会变大变小。图标一律 **511×511**，`Cost.png` 是 **250×250**。
- **画窗必须是透明洞**：卡框节点在预制体里盖在原画之上，不挖洞就把原画盖死。实测洞：召唤 y 268–1043，法术 y 268–856。

## 分层卡框装进预制体（2026-09-24，第二步）

`Resources/Cards/Frame/` 下新增 **14 张可当 Sprite 用的分层图**（`textureType: 8` + FullRect）：

| 文件 | 用途 |
|---|---|
| `Frame_Body.png` / `Frame_Body_Spell.png` | 卡身（含原画透明洞） |
| `Frame_NamePlate.png` | 名牌底板 |
| `Frame_ArtWindow.png` / `Frame_ArtWindow_Spell.png` | 画窗金线（中间透明） |
| `Frame_StatPlate.png` / `Frame_StatPlate_Spell.png` | 数值条 / 说明条底板 |
| `Frame_Edge_0..5.png` | 费用边带（index = 费用） |
| `Frame_Edge_Tint.png` | 纯白边带（乘色用） |

预制体里原来那一个整框节点已拆成 **5 个独立节点**：

| 预制体 | 拆掉的旧节点 | 新节点（层序 = 渲染序） |
|---|---|---|
| `Prefabs/Cards/Summon/Card00_New_2D.prefab` | `FrontFace/CostFrameBase` | `Frame_Body` → `Frame_NamePlate` → `Frame_ArtWindow` → `Frame_StatPlate` → `Frame_Edge` |
| `Prefabs/Cards/Spell/SpellCard00_New_2D.prefab` | 同上 | 同上（用法术版贴图） |
| `Prefabs/Cards/Summon/Card00_New_3D.prefab` | `UIComponents/CardFrame` | 同上 5 层 SpriteRenderer（z 0.140 / 0.142 / 0.144 / 0.146 / 0.148） |
| `Prefabs/Cards/Spell/SpellCard00_New_3D.prefab` | 同上 | 同上（z 0.110 起） |

五层都是**整框铺满卡面**（anchor 0,0–1,1 / sizeDelta 0），因此单独换一层图不会跑位。

### 他们靠什么联系：`CardFrameLayers.cs`

`Assets/_Game/Scripts/UI/Board/CardFrameLayers.cs`（新）挂在卡牌根节点，只干一件事：**按费用档刷新「边带」这一层**。

- 默认：按 `edgePath`（`Cards/Frame/Frame_Edge_{0}`）或 `edgeSprites[]` 换贴图
- 勾 `useTint`：用 `Frame_Edge_Tint` 乘 `edgeColors[费用]`，费用变了可实时变色
- 没挂本组件的旧预制体不受影响：仍走原来的「整框贴图」路径

接线点（都是一行）：

| 脚本 | 位置 |
|---|---|
| `CardDisplay2DNew.Refresh()` | 费用底图块之后 |
| `CardDisplay2DSpell.Refresh()` | 同上 |
| `CardDisplay3D.ApplyArtFromCard()` | 卡框块之后 |

生成器也同步改了（重跑不会把分层冲掉）：`Editor/Card2DNewPrefabBuilder.cs`、
`Editor/Card3DNewPrefabBuilder.cs`、`Editor/Card3DSpellNewPrefabBuilder.cs`。

### 回滚

预制体原件备份：`%TEMP%\cardframe-v7-prefab-backup-20260924-v7split\`（4 个 .prefab）。

## 没动的东西

- `Card000_Back.png` / `CardSpell000_Back.png`（496×880）没换 —— 它们只被旧预制体 `Card00_3D` / `SpellCard00_3D` 和 `Art/Materials/cardback.mat` 引用，那些预制体不在任何场景里（实测：`Game.unity` / `Lobby.unity` 都没引用），改了也看不到
- `Card000_Front.png` / `CardSpell000_Front.png` 没换 —— 代码把它们当「旧占位图」识别（`IsLegacyPlaceholder`），换了会破坏“未分配卡面”的判定
- 卡牌立绘（`Resources/Cards/Summon|Spell/**`）不属于本批