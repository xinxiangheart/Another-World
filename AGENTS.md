# Another World - 项目约定

## 卡牌立绘风格基准（Card Art Style Baseline）

**所有卡牌立绘必须沿用同一风格基准，以 01103「腐化之心」为唯一基准卡。**

基准文件：`Assets/_Game/Resources/Cards/Summon/Hero/1/SummonCard_{01103}.png`

01101「复仇者」与 01102「堕落者」已按此基准重绘（2026-09-10），两者的原始内容要求未变。

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
- 全身只允许**一个**高饱和点缀色（紫 / 暗红），其余全部压在低饱和灰黑

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

### 生成配方（已实测可用）

- 工具：`imagegen-compat` skill（`scripts/gen_image.py`），`--provider ofoxai`
- 模型：`volcengine/doubao-seedream-5.0-pro`
  - **不要用 `openai/gpt-image-2`**：它走 Azure 通道，画面里只要出现武器，一律被内容审核拒绝（HTTP 400 safety system）
  - 需要局部改图时走 `POST /v1/images/edits` + 蒙版，用 `google/gemini-3-pro-image`
- `~/.codex/imagegen-compat.json` 里 `providers.ofoxai.model` 已固定为 `volcengine/doubao-seedream-5.0-pro`，`size` 已设为 `1152x1536`，因此 `--refine` 可直接用
- 尺寸：`1152x1536`（3:4 竖版）
- 透明底：加 `--transparent` 先生成纯洋红背景，再抠图

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

### 落盘规范

- 先写入 `Assets/_Game/Art/Sprites/Generated/`，人工确认后再替换卡图
- 卡图路径：`Assets/_Game/Resources/Cards/Summon/Hero/<分组>/SummonCard_{ID}.png`
- 替换时**只覆盖 PNG，保留同名 `.meta`**，这样 Unity 里的引用（GUID）不会断
- 卡图 `.meta` 的 `maxTextureSize` 是 **2048**（`enableMipMap: 0`、`alphaIsTransparency: 1`）。出图长边不要超过 2048，否则 Unity 会静默降采样；`1152x1536` 安全
