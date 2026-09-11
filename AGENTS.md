# Another World - 项目约定

## 卡牌立绘风格基准（Card Art Style Baseline）

**所有卡牌立绘必须沿用同一风格基准，以 01103「腐化之心」为唯一基准卡。**

基准文件：`Assets/_Game/Resources/Cards/Summon/Hero/1/SummonCard_{01103}.png`

已按此基准重绘（2026-09-10，原始内容要求未变）：01101「复仇者」、01102「堕落者」；01103「腐化之心」为首张基准稿；01104「佣兵」、01105「牌场老手」、01107「妖精」由手绘稿改为正式插画；01106「无名之辈」由灰色剪影占位稿改为正式插画（2026-09-11）；01108「搜查官」由抽象占位稿改为正式插画（2026-09-11）。01109「尖啸者」由旧占位稿（恶搞图，未作参考）改为正式插画（2026-09-11）。

被替换下来的旧卡图归档在 `Assets/_Game/Art/Old/`，索引见该目录 `README.md`。

风格特征：

- 杀戮尖塔 2（Slay the Spire 2）风格
- 干净圆润的手绘线条（clean rounded line art）
- 暗黑奇幻、低饱和度
- 精致但不繁复
- 手绘卡牌插画质感

**平涂细则（01103 的画风要点，最容易跑偏，务必守住）**

要：

- 平涂赛璐璐（flat cel）：明度阶梯只留 2–3 级，相邻色块**硬边**相接
- 线条粗细统一、闭合、干净
- 剪影优先：缩到卡面尺寸时外轮廓仍要能读出来
- 低细节密度：内部只保留必要的结构线
- 全身只允许**一个**高饱和点缀色，其余全部压在低饱和灰黑；**这个点缀色的色相由卡牌前缀决定**，见下面「前缀配色」

不要：

- 不要喷枪渐变、体积渲染、AO 辉光
- 不要交叉排线、草稿式笔触
- 不要解剖写实（肋骨、肌肉束、器官纹理这类细节要抽象掉）
- 不要为了「更精致」堆细节——**细节密度一高，就不是这个画风了**

验收：把新图和基准卡并排放在深色底上、缩到卡面尺寸看。若新图显得「更精致 / 更立体 / 更亮」，即已跑偏。

风格提示词前缀（每次生成必须带上，后续描述追加在其后，结尾统一补 `3:4 portrait, no text, no border`）：

```
refined hand-drawn card game illustration, clean rounded line art, stylized like Slay the Spire 2, dark fantasy, low saturation, flat cel shading with hard-edged two-tone shadow shapes, bold uniform outlines, no gradients, minimal hatching, no sketch texture, simple clean shapes, low detail density
```

### 前缀配色（Prefix palette）

总画风（杀戮尖塔 2 + 暗黑奇幻低饱和）**所有卡牌统一**。前缀只改变**色调、元素、气质**三项，不改变画风；差异一旦破坏统一感，就是跑偏。

| 前缀 | 主色 | 气质 | 视觉特征 |
|---|---|---|---|
| 灵能 | 蓝 | 精神、联结 | 符文、水晶、连接线 |
| 渊 | 紫 | 阴沉、深渊 | 触手、眼睛、旋涡 |
| 机械 | 棕 | 厚重、构造 | 齿轮、金属、蒸汽 |
| 血歌 | 红 | 狂热、牺牲 | 血滴、音符、燃烧 |
| 神灵画卷 | 绿 | 神秘、古卷 | 书籍、卷轴、星点 |
| 无前缀 | **不固定** | 中性、普通 | 简洁、无属性标志 |

> **`无前缀` 的「灰」说的是气质，不是色相。** 它不锁定点缀色——点缀色**按角色描述逐张决定**，可以红、可以紫、可以金绿。它和有前缀卡的区别只有一条：**不带属性母题、不套属性配色**。不要把它理解成「必须画成灰的」。

**与「唯一高饱和点缀色」的关系**：前缀不是那条规则的例外，而是**指定它的色相**。上面「全身只允许一个高饱和点缀色」照旧成立——有前缀的卡取表里的「主色」；**`无前缀` 卡不套任何属性配色，点缀色由角色描述决定**。不论哪种，其余部分仍然全部压在低饱和灰黑。

**前缀只决定两件事**：① 唯一点缀色的色相；② 一到两个属性母题。**明度结构、线条、平涂阶梯、细节密度一律不动**——差异只允许落在这两处，这就是「有差异但不破坏统一感」的界线。

生成时，在基准提示词前缀之后、角色描述之前，追加该前缀的一行：

```
灵能      psychic blue accent, glowing runes and crystal shards, thin connecting energy lines
渊        deep purple accent, writhing tentacles, watching eyes, swirling abyss
机械      rust brown accent, exposed gears and metal plating, venting steam
血歌      blood red accent, falling droplets and burning embers, faint musical notes
神灵画卷   mystic green accent, ancient scrolls and tomes, scattered star motes
无前缀    no attribute motif and no attribute palette, plain and grounded; take the single accent colour from the character description itself
```

完整提示词 =

```
<基准提示词前缀>
<该前缀的修饰行>
<角色描述>
transparent background, 3:4 portrait, no text, no border
```

**数据实况（2026-09-11 核对 `Assets/_Game/Resources/CardData`）**

- 前缀字段实际只出现 `无`（138 张）、`渊`（14）、`机械`（11）、`灵能`（11）、`神灵画卷`（3）
- **`血歌` 目前一张卡都没有**。规则先记着，等有卡再用
- 基准卡 01103「腐化之心」是 `渊`，所以它的紫色点缀与该规则一致

**`无前缀` 卡的点缀色不受本表约束**：01101 匕首的暗红、01104 卷轴/皮具的暖棕、01105 的紫色大衣、01107 的金绿翅膀，都是按各自角色描述定的，符合规则，**不需要为了「配灰」去改**。

### 生成配方（已实测可用）

- 工具：`imagegen-compat` skill（`scripts/gen_image.py`），`--provider ofoxai`
- 模型：`qwen/qwen-image-3.0-pro`（2026-09-11 起）
  - **不要用 `volcengine/doubao-seedream-*`（豆包）**：出图偏「平面卡通」——粗黑描边、明度只留两级、整体偏亮，缩到卡面尺寸和 01103/01101 并排一看就不是一个画风
  - **不要用 `openai/gpt-image-2`**：它走 Azure 通道，画面里只要出现武器，一律被内容审核拒绝（HTTP 400 safety system）
  - 需要局部改图时走 `POST /v1/images/edits` + 蒙版，用 `google/gemini-3-pro-image`
- `~/.codex/imagegen-compat.json` 里 `providers.ofoxai.model` 已固定为 `qwen/qwen-image-3.0-pro`，`size` 已设为 `1152x1536`，因此 `--refine` 可直接用
- 尺寸：`1152x1536`（3:4 竖版）
- 透明底：加 `--transparent` 先生成纯洋红背景，再抠图

#### 局部改图（换脸 / 只改某一处）

```bash
# 1) 打蒙版：白底 + 透明区，透明处 = 要改的地方（OpenAI 约定）
python Tools/imagegen/mkmask.py <原图> <mask.png> "x0,y0,x1,y1" <feather>
# 2) 调接口
python Tools/imagegen/edit_masked.py <原图> <mask.png> <输出> "<提示词>" google/gemini-3-pro-image 1152x1536
```

- **蒙版只是提示，不是硬约束：Gemini 会重绘整张图**。实测给 01105 只蒙头部改脸，结果帽子形状、衣褶、构图全都跟着变了。要做精确保留的改动，这条路不合适
- 只有 `google/gemini-3-pro-image` 这类图像模型能走这个端点。**`volcengine/doubao-seedream-5.0-pro` 不行**，直接返回 `400 endpoint_not_supported`，提示改用 `/v1/chat/completions`
- 返回 **1792x2400、无 alpha**（与 3:4 略有出入），必须重新抠图，再缩放到 `1152x1536`
- **底色不固定：喂进去带 alpha 的 PNG，回来往往是白底而不是洋红底**（01107 修手这次就是纯白 254,254,254）。别照抄上一次的键色，先取四角看一眼。白底要走白键：`key_flood.py <src> <dst> 40 20 130`
- 白底的过渡带**用宽一点**（`20 130`）比窄的（`28 70`）边缘干净：同样的浅色描边点，宽带版几乎看不出来
- 缩放回标准尺寸后要重新居中：`Tools/imagegen/resize_centre.py <src> <dst> 1152 1536`。改图后主体位置通常会变，务必核对 alpha bbox
- 提示词里要显式写「只改渲染方式，不改五官/表情/姿势」，否则模型会顺手换人设

实测 01107 修手：提示词写明「只修右手、其余原样」，出来的整张图与原件**可见差异只有 1.82%**，且全落在手臂/手部，脸和靴子逐像素级一致——所以「重绘整张」不等于「整张都变」，程度取决于提示词

#### 只想改一小块时：整张用 vs 贴回局部

```bash
# 把改图结果的某个区域贴回原图（区域交叉淡入淡出，不是叠加）
python Tools/imagegen/patch_region.py <原图.png> <改图.png> <输出.png> "x0,y0,x1,y1" <feather>
```

- **注意是交叉淡入淡出**：区域外取原图，区域内取改图。若写成「改图叠在原图上」，改图判定为背景的地方是透明的，原图那块**改坏的东西会原样留下**（实测残指没被擦掉）
- 用之前先比两张图的 alpha bbox，位置不一致要先对齐
- **代价是接缝**：两张图的线稿只要差几像素（这次翅膀比原件宽 3px），区域边界扫过翅膀叶脉时会出现**重影**，3 倍放大明显可见。小范围线稿改动、且改动处压在已有图形上时，**直接用整张改图更干净**
- 这次最终就是整张用的（`01107`），`patch_region.py` 留着备用

### 抠图注意事项（踩过的坑）

- **不要用 `--despill`**：洋红键 = 红+蓝，despill 会把画面里的红芒、红布一起洗成灰色
- **不要对洋红底直接用 `--soft-matte` 默认参数**：其内部 `_dominance_alpha` 会把红色通道偏高的像素压成半透明
- **键色必须每张图重新取样**：洋红底本身不稳定，同一批候选图实测出现过 `(238,12,202)`、`(248,28,248)`、`(253,28,248)` 三种。取四角平均色即可，但不要写死
- **过渡带阈值必须按画面调**：主体若本身是紫/红这类与洋红相近的颜色，要收紧阈值。例：01102 的紫袍与键色距离只有 98，用 60/120 会被抠成半透明（alpha 177），收紧到 `<= 35 / >= 75` 才完全实心

**首选方案：泛洪式抠图 `Tools/imagegen/key_flood.py`（带发光/紫芒的图必须用它）**

```bash
python Tools/imagegen/key_flood.py <src> <dst> <T_fill> <T0> <T1>
# 例：python Tools/imagegen/key_flood.py draft/x.jpg out.png 55 45 95
```

- 原理：只有**与画面边缘连通**的洋红才算背景。画面内部的洋红/紫色（心口发光、能量光丝、紫色描边）被主体隔开、不连通，会被强制置为不透明
- 全局距离过渡带（`d <= T0` 全透、`d >= T1` 全实）只负责边缘抗锯齿；`T_fill` 是泛洪判定阈值
- 为什么非它不可：01103「腐化之心」心口光晕有 **2.5 万+ 像素**与键色距离 < 55，纯全局阈值会把发光抠成半透明甚至抠没
- 泛洪用 numpy 扫描线自实现（Pillow 10 起 `ImageDraw.floodfill` 已被移除，不要再调用）
- 自检：抠完打印「`alpha > 200` 且 `d < 35`」的像素数，必须为 **0**，否则说明有洋红残留

**必做收尾：去洋红染色 `Tools/imagegen/defringe.py`**

```bash
python Tools/imagegen/defringe.py <cut.png> <clean.png> 3 0.02
```

- 为什么需要：洋红底与主体的抗锯齿像素是**混色**（50% 金 + 50% 洋红 = 粉），而且这些像素的 `alpha` 常常是 **255**，所以只看 alpha 的抠图完全发现不了它们，成品在深底和浅底上都会有一圈粉紫描边
- 做法：保留全部 alpha，只修 RGB。`core` = 收缩 `radius` 像素后的实心区，`rim` = 其余非全透明像素；把 RGB 从 `core` 向外铺进 `rim`；再按混色比例 `t` 过滤——`t <= t_min` 的像素（例如本来就贴在边缘的黑色描边）原样保留
- **不要用反解** `(C - t*K)/(1-t)`：`t` 大时会把 `F` 的误差放大，实测把一条 75% 洋红的边缘像素解成了亮绿色。平涂线稿不需要它，直接把该像素换成 `F` 即可
- 实测效果：01107 的粉紫边缘像素 10,020 → 90
- 参数不是定死的：01106 的发丝边缘残留的洋红描边用默认 `3 0.05 80` 只清掉一半（`pink>45` 还剩 23 px，缩到 1 倍仍在发梢泛紫），把半径放到 **6**、`t_min` 放到 **0.005**（`defringe.py <cut> <out> 6 0.005 0`）后降到 5 px，且发丝没有变粗。边缘细碎（发丝、布褶）的图先量一下 `pink>45` 的像素数再定参数
- 参数：`defringe.py <src> <dst> [radius] [t_min] [t_abs] [mode]`
  - `t_abs`：绝对兜底。`t` 是相对 `F`（最近的内部色）算的，**当 `F` 本身就是浅色**（浅灰护腕、白色高光、键色是白色）时，边缘的浅色像素算出的 `t` 很小、躲过判定，留成一串浅色「串珠」。给个 `t_abs`（白键用 `80`）就能兜住
  - `mode=all`：无条件替换所有边缘像素。**平涂线稿上实测更糟**——会把手的外轮廓外侧抹成一条深灰带。不要用

**主体内部的洋红杂线：`Tools/imagegen/purge_key.py`**

```bash
python Tools/imagegen/purge_key.py <src.png> <dst.png> 80          # 按到键色的距离
python Tools/imagegen/purge_key.py <src.png> <dst.png> 45 pink     # 按色相
```

- 原因：`key_flood.py` 会**刻意保护**不连通的内部洋红（当作发光），所以生成时留在轮廓**内部**的洋红杂线会活下来
- 只在该图**没有**刻意的紫/洋红发光时才用（01103 那种心口紫芒不能用）
- 褪色的杂线离键色可能很远（实测 d=94），用 `pink` 模式（`min(R,B) - G`）更准：同一条杂线在 `pink` 下得 100，而旁边带紫调的灰发只有 20
- `pink` 模式对**暖粉肤色是安全的**：暖粉是「红高、蓝低」，`min(R,B)` 取到蓝通道所以得分很低（实测 -5）。只有洋红那种「红高蓝也高」才会命中。反倒是 01107 要求的暖调白里透粉肤色，用 `dist` 模式才会误伤
- 顺序：`key_flood.py` → `purge_key.py` → `defringe.py`

### 落盘规范

- 先写入 `Assets/_Game/Art/Sprites/Generated/`，人工确认后再替换卡图
- 卡图路径：`Assets/_Game/Resources/Cards/Summon/Hero/<分组>/SummonCard_{ID}.png`
- 替换时**只覆盖 PNG，保留同名 `.meta`**，这样 Unity 里的引用（GUID）不会断
- 卡图 `.meta` 的 `maxTextureSize` 是 **2048**（`enableMipMap: 0`、`alphaIsTransparency: 1`）。出图长边不要超过 2048，否则 Unity 会静默降采样；`1152x1536` 安全
