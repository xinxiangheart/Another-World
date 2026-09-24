# 战场底板 v2（8 层）

同一张 2048x1152 画布拆 8 层（生成脚本 `Tools/cardframe/BoardLayersV2.ps1`，含设计口径注释）。
每层是一个 19.2x10.8 世界单位的盒子前脸，挂在 `Game.unity` 的 `Board3D_*` 节点上
（`Assets/_Game/Art/Models/Board/Back.fbx` 的 PrefabInstance，8 个实例位置都是 (0,1,-4.701)、
局部旋转 180°），每层一个材质（队列 3000–3007），材质见 `Assets/_Game/Art/Materials/Board/`。

| 层 | 贴图 | 材质 | 动态 |
|---|---|---|---|
| L1 Board_Plate | Board_Plate.png | Board_Plate.mat | 静态 |
| L2 Board_Surface | Board_Surface.png | Board_Surface.mat | `BoardTrimPulse` 明灭 |
| L3 Board_Sigil | Board_Sigil.png | Board_Sigil.mat | **`BoardRingSpin` 顺时针自转** |
| L4 Board_Rune | Board_Rune.png | Board_Rune.mat | **`BoardRingSpin` 逆时针自转** |
| L5 Board_Ornament | Board_Ornament.png | Board_Ornament.mat | `BoardTrimPulse` 明灭 |
| L6 Board_Glow | Board_Glow.png | Board_Glow.mat | Additive |
| L7 Board_Motes | Board_Motes.png | Board_Motes.mat | `BoardMotesTwinkle` 闪烁 |
| L8 Board_Foreground | Board_Foreground.png | Board_Foreground.mat | 暗角 + 底沿 |

## 环带自转（2026-09-24）

需求：金环（L3）极慢顺时针、蓝环（L4）极慢逆时针，**中央的六芒星必须一动不动**。

坑：**六芒星和蓝环画在同一张贴图 `Board_Rune.png` 上** —— 实测半径分布

| 内容 | 贴图半径（px，2048 宽） | uv 半径（/2048） |
|---|---|---|
| L4 中心菱形 | 0–31 | 0 – 0.0151 |
| L4 六芒星 | 50–252 | 0.0244 – 0.1230 |
| L4 蓝环 + 刻度 | 292–336 | 0.1426 – 0.1641 |
| L3 主环 + 刻度 | 412–418 | 0.2012 – 0.2041 |
| L3 四颗金珠 | 464–471 | 0.2266 – 0.2300 |
| L3 八个外侧空心三角 | 477–521 | 0.2329 – 0.2544 |

所以整层旋转会把六芒星一起带走。做法：`BoardRingSpin.shader` 在片元里**按半径开环带窗口**，
只让窗口内的采样坐标旋转（窗口外原样采样），两侧留 `_Soft` 宽软过渡。
蓝环层的窗口取在 270–420 px（正好落在六芒星 252 与蓝环 292 之间那道空隙里），
金环层取在 395–740 px（该层内容全在 412–521，窗口外是空的，等于整层转）。

**转向的实证（重要）**：`_Speed` 的口径是「屏幕上」的方向，为此专门验证了棋盘贴图在实机里是否左右镜像 ——
用 `Board_Motes` 的 192 颗微尘与实机截图做对位打分（局部高反差加权）：

- **未镜像**，取几何预测的 (738, 528, s=0.7785)：score **+6.34**，z=**16.0**
- 镜像假设同一变换：score +0.49，z=0.3（与随机位置的零假设无法区分）
- 拟合出的最佳 s = 0.776，与相机几何算出的 0.7785 一致

结论：**屏幕 X+ = 贴图 U+，没有镜像**，所以 uv 里的顺时针就是屏幕上的顺时针。
（`s = 屏幕px / 贴图px ≈ 0.7785`；画布 2048x1152 中心映射到 game view 中心 (738, 528)。）

### 参数（材质 Inspector 可实时调）

| 参数 | L3 Board_Sigil | L4 Board_Rune | 说明 |
|---|---|---|---|
| `_Speed` | **+1** | **−1** | 度/秒，正=屏幕上顺时针，负=逆时针；1 度/秒 → 一圈 360s |
| `_InnerR` | 0.19287109（395px） | 0.13183594（270px） | 环带内边界（u 单位） |
| `_OuterR` | 0.36132812（740px） | 0.20507812（420px） | 环带外边界 |
| `_Soft` | 0.00488281（10px） | 0.00976562（20px） | 两侧软过渡宽度 |
| `_CenterX/_CenterY` | 0.5 / 0.5 | 0.5 / 0.5 | 旋转中心 |
| `_Aspect` | 0.5625 | 0.5625 | 贴图高/宽，用来把 uv 校正成等比，不填圆会变椭圆 |

要改转速就只改 `_Speed`；要改成反向就把符号翻一下（两个材质各一个数）。
`BoardLayersV2.ps1` 头部原始设计口径写的是 L3 +3~5 度/秒 / L4 −6~−8 度/秒，
本轮按用户「极其缓慢」定为 ±1 度/秒。

预览：`Tools/cardframe/preview/board-ring-spin-frames.png`（6 帧 × 30s）、
`Tools/cardframe/preview/board-ring-spin-ab.png`（0/90/180 度对照，含六芒星零位移对照）。

### 未动的东西

- 两个材质的 `_Color` 原样保留：`Board_Sigil.mat` 仍是 **(0,0,0)**（此前用户要求「中心大圆环压黑」，
  所以那圈环在实机里是**黑的**，只有四颗金珠与八个三角以黑形可见）、`Board_Rune.mat` 仍是 0.85 灰。
- `BoardTrimPulse` 的两层（Surface / Ornament）、微尘、底板等一律未动。
