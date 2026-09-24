# 前缀底图 v1（画窗底图）

> **已被 v2 取代（2026-09-24）**：见 `Assets/_Game/Art/Sprites/Generated/prefixbg-v2/README.md`。
> 用户实机反馈 v1「太暗 + 区分点全在中央被立绘挡掉」，本目录只作历史对照。

> **已于 2026-09-24 安装**：7 张**原地覆盖**到 `Assets/_Game/Resources/Cards/PrefixArtBG/`。
> 只覆盖 `.png`，`.meta` 一律未动 —— **guid 不变 → 预制体 / 场景零改动**。
> 覆盖前的 6 张旧图备份在 `%TEMP%\prefixbg-backup-20260924\`；也在 git 里
> （`git checkout -- Assets/_Game/Resources/Cards/PrefixArtBG` 可整体回退）。
> 本目录保留为归档 / 对照稿。

这 6 张图（+ 新增的第 7 张）是**卡面画窗的底纹**，画在原画背后：

| 场景 | 节点 | 谁在用它 |
|---|---|---|
| 2D | `ArtworkArea/PrefixArtBG`（rect 64x84） | `Card00_New_2D.prefab` / `SpellCard00_New_2D.prefab` 的 `prefixArtSprites[]` + `defaultPrefixArtSprite` |
| 3D | `PrefixBg`（SpriteRenderer，PPU 100） | `Card00_New_3D.prefab` / `SpellCard00_New_3D.prefab` 的同一组字段 |

**guid 已被四个预制体硬引用**，所以覆盖 PNG 即生效：

| 文件 | guid |
|---|---|
| `Psychic.png` | `03f7426fabfe62344b15e5381a36d784` |
| `Abyss.png` | `fb81b43c3f16e894f90fb8b11fb14441` |
| `Mech.png` | `5d8e080f8e8792049a049d343f80ac0a` |
| `Blood.png` | `833d5a95703bd754685dbed99d980ecf` |
| `Scroll.png` | `145199175b8ee374fbcd65030f622c35` |
| `Common.png` | `b7e7d2b8f09570042b3d43a492a159ac` |
| `Spell.png` | `364b48539f8b49ab97694f59e468d0db`（**本轮新建**，从 `Common.png.meta` 复制改 guid） |

数组下标顺序（与 `CardDisplay2D` / `CardDisplay3D` 的 `PrefixToIndex` 一致）：
`0 = 灵能  1 = 渊  2 = 机械  3 = 血歌  4 = 神灵画卷`，未知 / 无前缀 → `-1` → 走 `defaultPrefixArtSprite`。

## 为什么是 768x1024

三处实测反推出来的，两个显示路径都要同时铺满：

- **2D**：`PrefixArtBG` rect `64x84`（父 `ArtworkArea` 也是 `64x84`，中点对齐）→ 画窗比例 0.762。
- **3D**：`PrefixBg` 是 `SpriteRenderer` + `spritePixelsToUnits = 100`，目标世界尺寸 = `CardDisplay3D.ArtAreaSize = (0.69, 0.92)`。
  旧图 736px 宽 x scale 0.095 = 0.699 ≈ 0.69 —— 旧值就是这么来的。
  `768x1024 @ PPU100` = `7.68 x 10.24` 世界单位 x **uniform scale 0.09** = **`0.6912 x 0.9216`**，正好铺满。
- **对照**：卡图 `SummonCard_*.png` 是 `1152x1536`（3:4）+ PPU 100。1152x1536 贴进 593x775 的画窗横向压 0.15%，
  而 `768x1024` 是拉宽 1.6% —— 同一个数量级，看不出来。

**所以两个 3D 预制体的 `PrefixBg` `localScale` 本轮改成了 `(0.09, 0.09, 1)`**（原来分别是 `(0.095, 0.07, 1)`
与 `(0.09261745, 0.070823714, 1)` —— 非等比，旧图各是各的尺寸才凑得出来）。新图统一尺寸后不再需要非等比缩放。

## 旧图的问题

尺寸各不相同（`Abyss 745x1299` / `Psychic 746x1301` / 其余 `736x1312`），画面是**高饱和整色板**
（紫 / 红 / 灰 / 棕 / 亮蓝 / 绿）+ 一层淡菱形，跟 v7 卡框的夜色钢蓝**完全不搭** —— 底纹比原画还亮，
而且六张彼此不像一套。

## 这一版的构图词汇表

写在 `Tools/cardframe/PrefixBgV1.py` 头部 docstring 里，改口径直接改那儿：

- **底** `#101A26 → #080D15` 竖直渐变（比卡身 `#121B27 → #090E16` **再深一档**，读成「凹进卡里的画窗」）
- **边** 径向压暗 vignette（强度 `0.40`、下限 `0.58`）；**不做**倒角 / 内阴影 / 辉光
- **母题** 大、居中、**低对比**，只当纹理，绝不抢原画
- **明度常量** `MAIN, FINE, DOTS, FAINT = 56, 38, 78, 16`（首轮是 64/46/96/20，压过一次）
- **色相**同 `Icon_Prefix_*`：渊 `#A05AE8` / 血歌 `#D8364E` / 机械 `#D9873C` / 灵能 `#45C6E8` / 神灵画卷 `#5CC07E`；
  **通用 = 中性金 `#C9AF65`**（无属性）；**法术通用 = 中性钢蓝 `#9EB8D4`**
- **家族感** 七张共用**同一个外环（r=372）+ 四角刻线**，保证摆在一起成套

## 七个母题

| 键 | 中文 | 画面 |
|---|---|---|
| `Psychic` | 灵能 | 双层符文环 + 刻度 + 六棱晶核 + 上下能量脊 + 漂浮碎晶 |
| `Abyss` | 渊 | 3 层同心椭圆旋涡 + 大眼（上下睑弧 + 虹膜 + 竖瞳）+ 四角触手弧 + 气泡 |
| `Mech` | 机械 | 正齿轮（20 点真齿轮）+ 2 小齿轮 + 左右铆钉列 + 蒸汽弧 + 底沿齿条 + 压力表 |
| `Blood` | 血歌 | 大血滴（切线算的 teardrop）+ 左右声波弧 + 3 个音符 + 底部谱线 |
| `Scroll` | 神灵画卷 | 卷纸 + 上下实心卷杆 + 折线星图 + 8 节点 + 60 颗星点 |
| `Common` | 通用（无前缀） | 4 层同心环 + 刻度 + 凹角四芒星 + 四角菱形钉 |
| `Spell` | 法术通用 | 竖长符文柱 + 内框 + 柱内符文块 + 两侧刻度 + 上下导引 + 漂浮符片 |

## 法术通用底图（本轮新增的第 7 张）

法术**无前缀**，所以单独给一张中性的、不指属性的底图，而不是落回召唤的「通用」。

- 新建 `Spell.png` + `Spell.png.meta`（guid `364b48539f8b49ab97694f59e468d0db`）。
  **没有加进任何预制体的 `prefixArtSprites[]`** —— 走 `Resources.Load` 路径取，所以也不用动预制体。
- `CardDisplay2DSpell.cs`：新增字段 `spellPrefixBgPath = "Cards/PrefixArtBG/Spell"`，
  `GetPrefixArtBGSprite()` 开头改为**法术一律先取法术底图**。
- `CardDisplay3D.cs`：新增字段 `spellPrefixBgSprite`（拖了就用拖的，没拖走 `LoadSprite`）+ 方法 `ResolveSpellPrefixBgSprite()`；
  `Refresh()` 里改判 `template.cardType == CardType.Spell ? ResolveSpellPrefixBgSprite() : ResolvePrefixBgSprite(template.prefix)`。

## 预览

- `Tools/cardframe/preview/prefixbg-v1-sheet.png` —— 七张并排（含中文标注）
- `Tools/cardframe/preview/prefixbg-v1-onscreen.png` —— **装进 v7 卡框 + 带原画**，验「底纹会不会抢主体」

## 生成脚本

| 文件 | 作用 |
|---|---|
| `Tools/cardframe/PrefixBgV1.py` | 主脚本。仓库根目录 `python Tools/cardframe/PrefixBgV1.py` 整套重出（出图 + 装到 `Resources/`） |
| `Tools/cardframe/PrefixBgMotifs.py` | 7 个母题的绘制函数（被主脚本 import，**改几何改这里**） |
| `Tools/cardframe/PrefixBgV1Preview.py` | 出上面两张预览 |

可调项（脚本顶部）：`MAIN / FINE / DOTS / FAINT` 明度、`FIELD_T / FIELD_B` 底色、`TINT` 色相、
`field()` 里 vignette 的 `0.40` 强度与 `0.58` 下限、`SS = 3` 超采样倍率。

> 注意：`PrefixBgV1.py` 直接往 `Resources/` 写，重跑即重装；跑之前想留底就先备份 `%TEMP%`。
