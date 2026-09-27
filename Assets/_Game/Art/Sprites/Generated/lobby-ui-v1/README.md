# lobby-ui-v1 —— 大厅 UI 占位套装

**用途**：`Assets/_Game/Scenes/Lobby.unity` 的界面件。用户 2026-09-26：「好友货币商城什么的**先做占位 ui 即可**」——这一套是**占位**，不是终稿美术。系统本身（货币 / 商城 / 活动）库里都还没有。

生成脚本 `Tools/cardframe/LobbyUIv1.ps1`（可重跑）。预览：`Tools/cardframe/preview/lobby-ui-v1-sheet.png`（贴纸式对照）、`lobby-ui-v1-mockup.png`（按草图版式拼的 1920×1080 效果）、`lobby-ui-v1-icons.png`（四个「压墙」图标 1:1 + 压真背景的真播尺寸）、`lobby-ui-v1-iconhover.png`（同上的常态 / 悬停对照）。

## 右上角怎么摆（用户 2026-09-26 看图后定的）

- **横栏里 = 两个货币 + 数字**：`金币`（金币图标）与 `点券`（菱形券图标），**底下没有名字**，只有图标 + 数字。
- **栏下才是商城 / 活动 / 教程的自身图标** —— 这三个（以及左上角的**好友**）**不带底板**，图标直接显示在各自的位上。
- **设置齿轮压在横栏右端**（不是栏里的一格）。
- 横栏左端 45° 斜切、右端底边往下折一级（见下「横栏形状」）。

## 两块异形板（2026-09-26 三次修正，**逐列/逐行扫原稿量出来的**，不是目测）

用户原话：「仔细看形状好吗我把齿轮占位暂时去掉，理解形状」→「左上角形状也不对啊」。连读错两次，所以改成扫像素：原稿画布 x97..1516 / y297..1094 = **1419×797 = 16:9**，即画布就是屏幕；换算 `屏幕x = (原始x-97)×1.3531`、`屏幕y = (原始y-297)×1.3551`。

**两块是同一套语言、互为镜像：都齐屏幕角，内端一道 45° 斜切，底边带一级台阶。**

| | 左上 `LobbyProfilePlate.png` | 右上 `LobbyBandRight.png` |
|---|---|---|
| 贴图 / 屏幕 | 1401×288 / **467×96** | 1614×285 / **538×95** |
| 贴哪两个角 | 屏幕上沿 + **左沿** | 屏幕上沿 + **右沿** |
| 底边 | 左段低 `y≈95`，**只留够头像**（到 `x≈127`）→ 台阶收到 `y≈47` → 走直线 | `y≈56` 走直线到 `x≈1861` → **台阶沉下去到 `y≈95`** → 走到屏幕右沿 |
| 内端斜切 | 右端 **45° 往上收**（屏幕 `x413→467`，收完落在屏幕上沿） | 左端 **45° 往下切**（屏幕 `x1382→1433`） |
| 齿轮 | 不涉及 | **压在这段下沉台阶上**（屏幕 `x≈1830..1920`、`y0..96`）—— 去掉齿轮才看得清形状 |

**板上分区（用户 2026-09-26 确认）**：**圆框 = 头像位**（贴屏幕左沿），**圆框右侧那一段 = 名字位**。所以低段底边**只留到 `x≈127`**（头像圆框占屏幕 `x36..124`），名字位从 `x≈162` 起 —— 用户原话「左边再收紧一点」，收紧后低段正好包住圆框。好友图标**挂在底板下沿、名字右侧**（无底板）。

原稿实测关键点（屏幕坐标）：左上底边 **95 / 47**、台阶 **`x≈127→162`**、斜切 `413→467`；右上底边 56、斜切 `1382→1433`、下沉台阶 `1861→1884`。贴图顶点写在脚本 `Tools/cardframe/LobbyUIv1.ps1` 里。

### 金细线 = 外轮廓等比内缩（2026-09-26 四次修正）

`Get-InsetPoly($outer, 24)` —— 外轮廓**每条边沿自身法线内推 24 贴图 px**，再取相邻两条内推线的交点。

用户原话：「金线为什么距离边缘不一样，看着有点难受」。旧版是**手摆内顶点**：左上板右端斜边的内点 `(1368,29)` 正好落在斜边上（垂距 **0**），所以金线越往上越贴墨边、最后在右上角扎进去；直边却是 24。

实测（贴图 px，含线宽取心误差 ±0.8）：上边 **23.5**、左沿 **23**、右端斜边 **24.3–24.8**、台阶斜边 **24.5–24.7** —— 已处处一致。

**这两块不能拉伸、不能切片**，摆位时把对应角对齐屏幕的角（贴图 ÷3 = 屏幕尺寸）。

## 文件

| 文件 | 贴图 | 屏幕 | 说明 |
|---|---|---|---|
| `LobbyPanelPlate.png` | 192×192 | — | 面板底，9-slice **border 48** |
| `LobbyBtnPlate.png` | 192×192 | — | 按钮底 · 常态，9-slice border 48 |
| `LobbyBtnPlateHover.png` | 192×192 | — | 悬停（底板提亮 + 金线 α236） |
| `LobbyBtnPlatePressed.png` | 192×192 | — | 按下（底板压暗 44% + 金线 α120） |
| `LobbyPopupPlate.png` | 2700×1560 | 900×520 | **占位弹窗**面板底（十三次修正；定尺，不切） |
| `LobbyPopupBtnPlate.png` | 600×192 | 200×64 | **占位弹窗**关闭按钮底（十三次修正；定尺） |
| `LobbyBandRight.png` | 1614×285 | 538×95 | 右上横栏（固定尺寸，不切片）—— 形状见下面「两块异形板」 |
| `LobbyProfilePlate.png` | 1401×288 | 467×96 | 左上头像衬托底板（固定尺寸）—— 形状见下面「两块异形板」 |
| `LobbyAvatarRing.png` | 264×264 | 88×88 | 头像金圆框 |
| `LobbyFriendPanel.png` | 1395×3240 | **465×1080** | **好友侧边栏**底板（固定尺寸，不切）—— 齐屏幕左沿 / 上沿 / 下沿，**右沿就是滑出后的可见边界**（2026-09-27） |
| `LobbyJoinSidebar.png` | 1614×2760 | **538×920** | **「加入房间」右侧侧边栏**底板（固定尺寸，不切）—— **右上横栏（538×95）那一块的背景**：上沿 = 屏幕顶、左沿 = 横栏左沿、右沿贴屏幕右沿；板顶开口（只有下方两个圆角）（二十二次修正 · 七改） |
| `LobbyChip_Kick.png` | 288×144 | **96×48** | 「踢出」的**子背景**（平底 + 一条金细线，**定尺不能切**）—— 二十次修正 |
| `LobbyChip_KickHover.png` | 288×144 | **96×48** | 「踢出」子背景 · **悬停态**（底提亮一档 + 金线 α236，切图不跳位） |
| `LobbyChip_Start.png` | 636×192 | **212×64** | 「开始游戏」的**子背景**（同配方；四个字 + 字号更大 ⇒ 底更大） |
| `LobbyChip_StartHover.png` | 636×192 | **212×64** | 「开始游戏」子背景 · **悬停态** |
| `Icon_LobbyGear.png` | 256×256 | 92×92 | 设置 · 齿轮（与 `icons-v1` 的 `Icon_Settings` 同配方）· 压在横栏右端 |
| `Icon_LobbyFriend.png` | 256×256 | 46×46 | 好友 · 人影（**金线石印族**，十一次修正）· 无底板 |
| `Icon_LobbyShop.png` | 256×256 | 60×60 | 商城 · 货篮 + 两段金线提手 + 两只金环轮（**金线石印族**）· 无底板 |
| `Icon_LobbyEvent.png` | 256×256 | 60×60 | 活动 · 礼盒 + 竖向金带 + 菱形结扣（**金线石印族**）· 无底板 |
| `Icon_LobbyTutorial.png` | 256×256 | 60×60 | 教程 · **合起的书**（封面内缩金线 + 靠右一道书脊 + 三档金线 + 菱形铆钉，**金线石印族**）· 无底板 |
| `LobbyEntryPlate_Battle.png` | 1998×435 | **666×145** | 入口板 · 战斗（**定尺，不能切**） |
| `LobbyEntryPlate_Cards.png` | 1983×447 | **661×149** | 入口板 · 卡牌总览（**定尺**） |
| `LobbyEntryPlate_Room.png` | 1034×445 | **324×130** | 入口板 · 房间（**定尺**） |
| `LobbyEntryPlate_More.png` | 1034×445 | **324×130** | 入口板 · 其它（**定尺**） |
| `Icon_LobbyCoin.png` | 256×256 | 48×48 | **金币**（横栏内第一个货币） |
| `Icon_LobbyTicket.png` | 256×256 | 48×48 | **点券** · 菱形券（横栏内第二个货币；蓝色与金币区分） |
| `Icon_LobbyFriendHover.png` | 256×256 | 46×46 | 好友 · **悬停态**（石面提亮 + 金线 GOLD→GOLD_L） |
| `Icon_LobbyShopHover.png` | 256×256 | 60×60 | 商城 · **悬停态** |
| `Icon_LobbyEventHover.png` | 256×256 | 60×60 | 活动 · **悬停态** |
| `Icon_LobbyTutorialHover.png` | 256×256 | 60×60 | 教程 · **悬停态** |
| `Icon_LobbyJoin.png` | 256×256 | 60×60 | **加入房间**（**金线石印族**：门框 + 一支进入的箭头 + 菱形门把手）· 房间面板关闭叉左边 · 无底色（二十一次修正） |
| `Icon_LobbyJoinHover.png` | 256×256 | 60×60 | 加入房间 · **悬停态**（石面提亮 + 金线 GOLD→GOLD_L） |

> `LobbyFriendPlate.png`（好友按钮底）已于 2026-09-26 **删除** —— 好友不带底板。

## 四个入口板（2026-09-26 五次修正；用户：「重出一批用于四个框的背景，现在的太简陋视觉表现太怪了」）

**两个病灶，一个是口径错、一个是设计空。**

1. **口径错**：mockup 里这四块是拿 `LobbyBtnPlate.png`（192×192，按**贴图像素**定的线宽/圆角）**直接按屏幕像素**铺上去的 —— 于是墨边 9px、金线 3px、圆角 30px 全被当成「1 贴图 px = 1 屏幕 px」画出来，**比该有的粗 3 倍**。9-slice 又不能在 GDI+ 里切，所以四块只能用一张整图，**斜板也没法 9-slice**（切片会把斜边切坏）。
2. **设计空**：那四块只有「一块平底 + 一条金线 + 两行字」，没有任何结构 —— 所以又简陋又怪。

**改法：每块单独出一张定尺贴图（屏幕尺寸 ×3），mockup 按屏幕尺寸缩下去画。** 图层（从下到上）：

| # | 层 | 参数 |
|---|---|---|
| 1 | 平底 | `Fill-VGrad BAR_T → BAR_B`，裁在圆角矩形里 |
| 2 | 左上角一级亮楔 | `Add-Wedge` 三角 (0,0)→(0.66W,0)→(0,0.86H)，`BAR_T` **α74**（相邻色阶差 6–8 级，硬边平涂） |
| 3 | 右半徽记（水印） | 圆心 `(0.755W, 0.5H)`、半径 `0.30H`、金**单色 α46** —— **战斗 = 六芒星**（与棋盘中央 / 大厅中央徽记同源）、**卡牌总览 = 三张叠牌**、**房间 = 拱门**、**其它 = 环带刻度**（棋盘外圈同源） |
| 4 | 外墨边 | 圆角矩形 r30，`INK` α240，9px = **屏幕 3px** |
| 5 | 金细线 | 圆角矩形 **等比内缩 24**（= 同心圆角矩形，间距处处相等）、`GOLD` α150、3px = 屏幕 1px |
| 6 | 左端标题槽 | 竖线 x=108（屏幕 36）α110 + 上下两枚菱形铆钉 r11 α205 |
| 7 | 标题下短线 | x 138→(138+0.28W)、y=`H-92`、α96 |

### 板上的字（2026-09-26 六次修正）

用户：「上面文字只显示我之前给你说的，小字注释不要了，并且斜着展示，字体根据背景框大小不同，上面两个可能更大更显眼」。

| 板 | 板上文字 | 字号（屏幕 px） |
|---|---|---|
| 战斗（666×145） | **战斗** | **44** |
| 卡牌总览（661×149） | **卡牌总览** | **46** |
| 房间（324×130） | **房间** | **30** |
| 其它（324×130） | **其它** | **30** |

- **只写入入口名，副标题（「随机匹配 + AI 对战」这类小字）一律不要** —— 之前那版是占位说明，不是板上该有的字。
- **文字跟板同角度**：绕板心转（脚本里 `Put-TextRot`），不再水平 —— 现用倾角 **−5°（向上倾斜，右端高）**。
- 文字**不烘进贴图**，仍由场景画（贴图里没有字，换语言/改字号不用重出图）。
- 竖直方向按**字墨迹中心**贴板心（GDI+ 的 `DrawString` 原点在行框顶，实测墨迹重心在行框顶下方 **0.87 em**，所以偏移取 `-0.87 × 字号`）。
- 四块板各出一张定尺图，**不要再拿 `LobbyBtnPlate` 当入口板**。

预览：`Tools/cardframe/preview/lobby-ui-v1-entryplates.png`（四块 1.55 倍 + 图层说明）。
### 倾角与板间距（2026-09-26 七次修正）

用户：「倾斜程度小一点点，然后各个格子之间距离缩短，只留出部分间隔即可」；随后又补：「我忘了说是**向上倾斜**了，修正一下，另外**上面两个可以增长一些，不要让下面的露出侧边**」（原稿方向作废）。

- **倾角 6° → 5° → −5°（向上，右端高）**。四块板与板上文字同角度。画法：`RotateTransform(-5)`（屏幕 y 向下，负角即右端抬起来）。
- **板间距按法线量，不按外框量**：斜板的轴对齐外框会互相咬（净距 40px 时外框已经重叠），所以间距一律沿**板的法线**算。

| 相邻 | 旧净距 | 现净距 |
|---|---|---|
| 战斗 → 卡牌总览 | 87.5 | **40.0** |
| 卡牌总览 → 房间 | 140.4 | **39.8** |
| 房间 ↔ 其它（横向） | 21.8 | **18**（原来「纵向错开 57」已作废，见「十次修正」） |

四块板在 mockup 里的落点（屏幕 px，左上角）：战斗 `1133.5,277.5,666×145` · 卡牌总览 `1109.5,465.5,661×149` · 房间 `1133.5,668.5,324×130` · 其它 `1475.5,668.5,324×130` —— 下排两块 = **透明大框** `666×145 @ 1133.5,668.5` 的两个格子（`$ENTRY_CELL`）。

**整簇位置（2026-09-26 定）**：**卡牌总览的中心 = 屏幕右半的正中 `(1440, 540)`**（原来是 `1336.5, 487.5`，整簇平移 `+103.5 / +52.5`，四块相对关系不变）。改位置就改这四块的左上角 + `$BOX_X/$BOX_Y`。

**上面两块加长**（战斗 558→666、卡牌总览 553→661，右端不动、往左长），因为下面两块原来左侧露在外面。按旋转后的四个角算：

| 板 | 最左 x | 最右 x |
|---|---|---|
| 战斗 | **1024.9** | 1701.1 |
| 卡牌总览 | 1000.8 | 1672.2 |
| 房间 | 1028.7 | 1355.3 |
| 其它 | 1370.1 | 1698.0 |

上面两块的左右两端把下面两块整个包住 → 下面不再露侧边。（量最左/最右要按**旋转后的角点**算，不能拿轴对齐外框。）

### 每块板自己的微透视（2026-09-26 九次修正）

用户连发三张明日方舟大厅截图：「我是想做类似的**透视**效果，**不是说所有的都朝一个方向倾斜的**，其实**倾斜程度是很低**的」。

**放大实测（截图 927×576，逐条边做最小二乘）**：牌子的上/下边**接近水平**（±1~2°），四条边**互相不平行**（右端比左端矮 1~3%，是**梯形**不是矩形），而且**每块方向不同** —— 顶部大牌「作战」上边向右抬约 2°，它下面的「编队」「干员」几乎水平、略向右压，底部「基建」「仓库」又各自偏一点。整簇还随位置**递减变小**（作战牌高 ≈120px，底下小牌 ≈55px）。所以那个「透视感」来自三件事叠加：**近水平 + 一点点梯形 + 方向不统一**，不是「四块一起转 5°」。

**改法**：形体表 `Tools/cardframe/LobbyUIv1.ps1` 顶部的 `$ENTRY_SHAPE`，每块板三个数：

| 数 | 含义 | 归谁 |
|---|---|---|
| 倾斜度 | 板面倾斜（Unity 口径，+ = 右端高） | **场景**里 `Entry_*` 的 `Rotation Z`（方便手拖） |
| 端头斜切度 | 端头相对板面再斜多少 | **烤进贴图**（RectTransform 做不到） |
| 远端收缩比 | 右端比左端矮多少（0.03 = 矮 3%） | **烤进贴图**（纯旋转做不到，这条才是「透视」） |

现值：战斗 `1.6, -1.2, 0.030` · 卡牌总览 `0.0, -0.8, 0.026` · 房间 `-1.5, 1.0, 0.024` · 其它 `-1.5, 1.0, 0.024`（**下面两块三个数完全一样**）。

**方向（用户 2026-09-26 定）**：中间那块（`卡牌总览`，正好在屏幕右中）**不倾**（`0°`）；上面那块（`战斗`）**朝左下**（右端高，`+`）；下面两块在同一个大框里、**朝左上且彼此平行**（右端低，两块都用 `−1.5`）。

**画法**：板体先在板身局部坐标里照旧画好（平底渐变 / 亮楔 / 徽记 / 墨边 / 金细线 / 左端标题槽），再用**两片三角形仿射**把它映射到四角偏移后的梯形上 —— 装饰跟着形体一起走，不用逐件改坐标（`Get-EntryQuad` / `Get-EntryPlateMap` / `New-QuadWarpDraw`）。

| 板 | 板身（屏幕） | 贴图 | 外框（屏幕）= RectTransform |
|---|---|---|---|
| 战斗 | 666×145 | 2062×490 | 687.3×163.3 |
| 卡牌总览 | 661×149 | 2044×502 | 681.3×167.3 |
| 房间 | 324×130 | 1034×445 | 344.7×148.3 |
| 其它 | 324×130 | 1034×445 | 344.7×148.3 |

- 贴图把**板心放在正中**，所以场景里 `centerPos` 依旧按板心给；外框比板身大一圈（`$ENTRY_PAD = 24` 贴图 px = 8 屏幕 px，装外墨边与圆角外扩）。
- 文字落点（相对板心）：`x = 58 − 板身宽/2`、`y = 0.87×字号 − 板身高/2` —— 与旧口径完全一致。文字是子物体、只跟板一起转，**不跟着形体弯**（那点弯只有 1~3px，肉眼看不出）。
- 档位对照图：`Tools/cardframe/preview/lobby-ui-v1-shape.png`（现状 / 低幅混合 / 收缩加倍 / 近乎水平）。
- **顺手修掉两个真 bug**（都是 PowerShell 变量**大小写不敏感**害的 —— `$W` 与 `$w`、`$H` 与 `$h` 是同一个变量）：
  1. `$W = $w * $S` 赋值时把屏幕尺寸 `$w` 也改成了贴图像素，再拿它去求外框 = **乘了两次 3 倍**（贴图跑到 6158px）。现在贴图像素尺寸一律叫 `$wt` / `$ht`。
  2. 函数返回 `@($a, $b, $c, $d, $corners)` 时那层嵌套数组会被拍平，`Corners` 变成两个数 → 形体崩成碎片。改成**哈希表返回**。

### 下排两块共用一个**透明大框**（2026-09-26 十次修正）

用户：「下面两个小框实际上就是在一个整体的大框下」「这个大框是**透明的**只是用于限制位置」「大框和上面两个的大小**完全一样**」。

- **大框不画**：它在场景里就是 Canvas 下一个空的 `RectTransform`（`Entry_BottomRow`，**无 Image、无描边**），尺寸 = 上面那两块（**666×145**），把 `房间` / `其它` 钉在格子里；整排要挪就挪它。
- **竖切成两格**：`(666 − 18) / 2 = 324` 宽 × 145 高 —— `房间` = 左格（格内左上 `0,0`）、`其它` = 右格（格内左上 `342,0`）。
- **两块板跟着格子变宽、变一样**：287 / 295 → **324 / 324**（左右贴齐大框外沿，于是下排的左右沿与 `战斗` 对齐），高 129 → **130**；
- **同一大框 = 同一平面**：两块板的形体（倾角 / 斜切 / 收缩）与尺寸**逐项完全相同** —— 不一样的话两条上沿就不平行，一眼看出「不是一个框里的」。
- **原来那 57px 的纵向错落作废**：大框只有 145 高、板已经 130 高，装不下 —— 两格同一行。想要一点错落，就把板高压到 145 − 错落 以下再加回去。
- 常量：`LobbyUIv1.ps1` 的 `$BOX_X/$BOX_Y/$BOX_W/$BOX_H/$BOX_GAP` + `$ENTRY_CELL`；场景脚本 `LobbyUIBuilder.cs` 里是 `BoxSize / BoxPos / CellRoom / CellMore` —— **两处要一起改**。
- **预览**：`Tools/cardframe/preview/lobby-ui-v1-grid.png` 把那个不画的框 + 两格画成**虚线**，只为看限位对不对；正式预览 `lobby-ui-v1-mockup.png` 里不画。
- **顺手修掉一处「预览与场景左右相反」**：形体表第一个数是 **Unity 口径**（`+` = 右端高，供场景的 `Rotation Z`），而预览是 GDI+、**正角是顺时针（右端低）** —— 之前预览直接套了同一个数，于是 `战斗` 在预览里右端低、在场景里右端高。现在预览统一取相反号（`$rot = -1 * $sh[0]`），两边一致。
## 线宽折算（本套统一 **3 倍出图**：贴图 3px = 屏幕 1px）

- 外墨边 9px 贴图 = **3px 屏幕**；金细线 3px = **1px 屏幕**；9-slice 边 48px = **16px 屏幕**。
- 板角半径 30px 贴图 = 10px 屏幕。
- 图标沿用 `icons-v1` / `topbar-v1` 的墨边 **20px**（屏幕 ≈6px @80px）。
- ⚠️ 改尺寸前先算这个比 —— `hud-btn-v1` 踩过：贴图 768px ↔ 屏幕 180px，1 贴图像素只有 0.234 屏幕像素，不折算就会「线粗一圈」。

## 色值（全部取自 `Tools/cardframe/TopBarV2.ps1` 的调色板，与顶栏同一套）

| 用途 | 取值 |
|---|---|
| 板面渐变 | `BAR_T #1E2938` → `BAR_B #0C111A` |
| 墨（外描边 / 最暗） | `INK #06090E` |
| 金细线 | `GOLD #C8A44A`（亮 `#E8D18A` / 暗 `#785E28`） |
| 图标材质色 | 钢 `#9AACC2`（齿轮 / 人影）、金 `#C8A44A`（购物车 / 金币）、`#B64848`（礼盒）、`#D6C298`（书）、`#689AD6`（点券） |

## 手法（**不许违反**）

整块平底 + 一条金细线 + 平板。**不做倒角、不做内阴影、不做外发光、不做双层面板、不贴材质纹理** —— 原话是「不做出『凸起』的错觉」（`cardframe-v7`）。按钮三态只靠**底板明度**与**金线 alpha** 区分，不加阴影。图标本身有 2–3 档平涂明度 + 一条受光面（这是 `icons-v1` 既有配方，不算「体积渲染」）。

## 未定项（接线前要跟用户确认）

1. **「稍微倾斜立体」到哪一档** —— 用户 2026-09-26 定：**向上倾斜**（右端高）；九次修正后改成**每块板自己的 1~2°、方向不统一**，见「每块板自己的微透视」。草图那份是**反的**（它画成顺时针），已作废；**通用按钮底 `LobbyBtnPlate` 仍是正的 9-slice 板**（斜板无法 9-slice）。
2. **两个货币的数值口径** —— 占位用的是 `1,280` / `360`，真值不知道（库里**没有任何货币 / 钱包代码与数据**，全库搜过）。
3. **左半屏（约 55%）留给什么** —— 是否放人物立绘，**用户尚未回答**。
4. ~~草图上四块斜板的文字是水平的~~ —— **2026-09-26 已定**：文字**跟板一起斜**（现用 −5°），字号按板分档（44/46 与 30）。

## 接线注意

- 四个入口板是**定尺贴图**（不是 9-slice）：Image 直接用原生尺寸（`Preserve Aspect`），再转 −5°；**别拉长**，拉长线宽会跟着变形。
- `Generated/` 下的 PNG 是 Unity 默认导入（`textureType: 0`，**不是 Sprite**）。要当 UI Image 用，得在导入设置里改成 Sprite(2D and UI)，9-slice 那两个再把 `spriteBorder` 设成 48；或改用 RawImage 自己算。**覆盖贴图时别连 `.meta` 一起换**。
- `Lobby.unity` 里现有 57 个 TMP，字体是 NotoSerifCJKsc-Bold(45) / Black(10)，**另有 2 处 LiberationSans 残留要清**。
- 现有 10 个入口的归并（按用户草图）：战斗 = 随机匹配 + AI 对战；卡牌总览 = 已有 `CardCollectionPanel`；房间 = 创建房间 + 加入房间；其它 = 游戏介绍 / 战绩 / 设置 / 结束游戏 / 返回主界面。`Record`、`AI Battle` 两个按钮现在挂在 **x=-750（画面外）**，重组时要挪回来。
- 教程目前只有 `TutorialManager.cs`（**PLACEHOLDER**，三个方法全 `TODO: 待实现`）；商城 / 活动 / 货币**都还没有代码** —— 所以这一套接线时**只能挂占位按钮 + 预留事件**，别接不存在的系统。

## 落地到场景（2026-09-26 已做）

工具：`Assets/_Game/Editor/LobbyUIBuilder.cs` —— 菜单 **Tools → 异界 → 生成大厅 UI v1（占位）**。
为什么不是直接改 `Lobby.unity`：出这条时 `Library/LastSceneManagerSetup.txt` 显示 **Lobby.unity 正开在编辑器里**，
从外部改场景文件会让 Unity 重载场景、可能吞掉未保存的改动；走菜单还能 Ctrl+Z、贴图迭代后能重建。

生成结构（根节点固定名 `LobbyUI_v1`，挂在 Canvas 下 **Background 之后、弹窗与旧按钮之前**；重复执行先删再建）：

```
LobbyUI_v1
 ├─ Ref_Backdrop             LobbyBack.png        全屏拉伸（raycast 关）
 ├─ Ref_Emblem               LobbyBackEmblem.png  1020×1020 居中，α 0.6（raycast 关）
 ├─ Plate_Profile            LobbyProfilePlate   467×96   锚屏幕左上
 │   ├─ Avatar_Ring          LobbyAvatarRing      88×88
 │   ├─ Text_PlayerName      名字                 TMP 26
 │   └─ Icon_Friend          Icon_LobbyFriend     46×46
 ├─ Plate_TopBand            LobbyBandRight      538×95   锚屏幕右上
 │   ├─ Icon_Coin / Text_Coin(1,280) / Icon_Ticket / Text_Ticket(360) / Icon_Gear
 │   └─ Icon_Shop / Icon_Event / Icon_Tutorial（无底板，挂在栏下）
 ├─ Entry_Battle / Entry_Cards          各带一个 Label 子物体（板心锚屏幕右上角）
 └─ Entry_BottomRow                    **透明大框**：666×145 的空 RectTransform（无 Image、只为限位）
     ├─ Entry_Room                      大框左格（格内左上 0,0，板身 324×130）+ Label
     └─ Entry_More                      大框右格（格内左上 342,0，板身 324×130）+ Label
```

坐标口径（屏幕 px · 1920×1080；右半整簇锚**屏幕右上角**，所以 `anchoredPosition.x` 是负数）：

| 节点 | 锚 | 尺寸 | 位置 / 板心 | 字号 | Rotation Z |
|---|---|---|---|---|
| Plate_Profile | 左上 | 467×96 | 0, 0 | — |
| Avatar_Ring | 左上 | 88×88 | 36, −2 | — |
| Text_PlayerName | 左上 | 240×44 | 184, −12 | 26 |
| Icon_Friend | 左上 | 46×46 | 160, −57 | — |
| Plate_TopBand | 右上 | 538×95 | −538, 0 | — |
| Icon_Coin / Icon_Ticket | 左上(栏内) | 48×48 | 110 / 300, −16 | — |
| Text_Coin / Text_Ticket | 左上(栏内) | 150×46 | 164 / 354, −24 | 28 |
| Icon_Gear | 左上(栏内) | 92×92 | 446, −2 | — |
| Icon_Shop / Event / Tutorial | 左上(栏内) | 60×60 | 109 / 232 / 358, −66 | — |
| Entry_Battle | 右上（板心） | 687.3×163.3（外框；板身 666×145） | −453.5, −350 | 44 | +1.6 |
| Entry_Cards | 右上（板心） | 681.3×167.3（外框；板身 661×149） | −480, −540 | 46 | 0 |
| Entry_BottomRow | 右上（**左上角**） | 666×145（**透明大框**，无 Image、只限位） | −786.5, −668.5 | — | 0 |
| Entry_Room | 大框内（板心） | 344.7×148.3（外框；板身 324×130） | 162, −65（= 格内左上 0,0 + 半板身） | 30 | −1.5 |
| Entry_More | 大框内（板心） | 344.7×148.3（外框；板身 324×130） | 504, −65（= 格内左上 342,0 + 半板身） | 30 | −1.5 |

- **倾角换算（容易搞反）**：出图是 GDI+ 的 RotateTransform（Y 轴向下），落到 Unity（Y 轴向上）要取相反号，
  `localRotation.z = **+5**` —— 两者都是「右端高」。Unity 里写成 −5 就变成右端低。
- 入口板用 **RawImage 定尺**（贴图 = 屏幕 ×3，原始像素即原生尺寸，别拉、别 9-slice）；贴图含成形后的外框，RectTransform 按**外框**给（见上面「每块板自己的微透视」表），别按板身给；板上文字是**子物体**，
  自动继承倾角。文字口径与 mockup 的 `Put-TextRot` 同源：**距板左沿 58px、字顶落在板心上方 0.87×字号**。
- 版式核对图：`C:\Users\22589\.codex\visualizations\2026\09\26\01a0dccd-afda-7a23-a849-e45d1e52da00\lobbyui-scene-verify.png`
  （把上表换算出的矩形叠在 mockup 上，逐块贴合）。

### 这一版没做 / 遗留（用户自己调位置时要注意）

1. **不接任何逻辑**：四个入口板只是图 + 字，没有 Button、没有 onClick；旧入口按钮一个没动。
2. **旧的居中入口按钮**（QuickMatch / Creat / Join / CardData / Introduction / Setting / Leave / Return 等）仍在
   x 880–1040 一带，**与「卡牌总览」板左缘（x 1109.5）重叠约 34px**，归并时按上面「接线注意」的对照表处理。
3. **`Canvas/Background` 还在**（旧的白底 Sprite，guid `bad47bc6…`），现在被 `Ref_Backdrop` 盖住、看不见；
   要干净就删掉它，或按 `lobby-v1/README.md` 的「接线」原地覆盖 `Backgrounds/LobbyBack.png`（guid 不变）。
4. **`PlayerProfilePanel`（真实头像 RawImage + NameText）叠在左上衬托板上**，位置是 (200, −80)、300×80；
   想用真实头像 / 名字，就把它的两个子物体拖进 `Plate_Profile` 的 `Avatar_Ring` / `Text_PlayerName` 位置，删掉占位件。
5. 两个货币仍是占位数字 `1,280` / `360`；**库里没有任何货币 / 钱包代码**。
6. `LobbyBackEmblem` 只压到 α0.6；`Ref_Backdrop` 是全屏拉伸 —— 非 16:9 屏幕会被非等比拉伸（徽记是圆的，会变椭圆），
   要治就给它加 `AspectRatioFitter`。

## 十一次修正（2026-09-26）：好友 / 商城 / 活动 / 教程 重出为「金线石印」族

**来由**：用户判这四个「跟背景与风格不搭，很不满意」，要求重出、尤其后三个（商城 / 活动 / 教程）。

**旧版错在哪**：它们照搬 `icons-v1` 的**实心族**（实心彩色块面 + 20px 粗墨边 + 内阴影 + 高光楔）——
商城是蓝提手 + 金篮 + 金轮的购物车、活动是**红箱** + 金带、教程是**米黄块面**的书。三个毛病：

1. **第三色**：蓝 / 红 / 米黄都不在本套配色里（本套只有深蓝黑 + 金；红是生命色，只给生命条）。
2. **20px 粗墨边**（屏幕 60px 时 ≈ 4.7px）—— 比板件的 3px 墨边粗一半，是 App 图标的重量。
3. **内阴影 + 高光楔** —— 正是本套母规明令禁的两样（`cardframe-v7`：不做出「凸起」的错觉）。

**新版口径**（母规：整块平底 + 一条金细线 + 一处金饰）

| 项 | 取值 |
|---|---|
| 石面 | 直接用板件那对色：`BAR_T #1E2938 → BAR_B #0C111A`，**逐形状各自渐变**（走 `$path.GetBounds()`），不是整张画布渐变 |
| 金细线 | `$ICON_GOLD = 6` 贴图 px（屏幕 60px 时 ≈ 1.4px）· 相对形状边等比内缩 `$ICON_INSET = 16` |
| 墨边 | `$ICON_INK = 9`（屏幕 60px 时 ≈ 2.1px）· 细件（提手 / 轮 / 结环）另给 6~13 |
| 金饰 | 只用棋盘母题：**菱形铆钉**（礼盒结扣 / 封面徽记）、**环带**（人影胸前一道弧）、**金带**（礼盒竖带） |
| 禁 | 第三色、内阴影、外发光、高光楔、倒角 |

**四个形状**

- **好友**：人影（头 + 肩）—— 头 / 肩各一条内缩金线，胸前一档金环带。
- **商城**：货篮（梯形 + 内缩金线 + 两道金肋）+ **两段金线提手**（墨 13 打底、金 6 压上，圆头）+ 两只金环轮。
- **活动**：礼盒 —— 箱体 / 箱盖各一条内缩金线 + 竖向金带（金底 + 墨边）+ 两只结环 + **菱形结扣**。
- **教程**：合起的书 —— 封面内缩金线 + 靠右一道书脊金线 + 三档金线 + **菱形铆钉**。

**齿轮 / 金币 / 点券不动** —— 它们在右上横栏里、下面有底衬，仍走 `icons-v1` 实心族。

**场景不用重铺**：四个贴图**同名同尺寸**（256×256）覆盖，`.meta` 的 guid 没变，场景里 `Icon_Lobby*` 的引用自动指到新图。
预览 `Tools/cardframe/preview/lobby-ui-v1-icons.png` 就是「1:1 + 压在真背景上的真播尺寸」两张对照。

## 十二次修正（2026-09-26）：四个「压墙」图标补悬停态

用户要求出「一组鼠标悬停时的变化态」。**配方直接照 `LobbyBtnPlateHover`（tone 1）**，不另起一套：

| 项 | 常态 | 悬停 |
|---|---|---|
| 石面顶 | `BAR_T #1E2938` | `Mix(BAR_T, HILITE, 0.12)` |
| 石面底 | `BAR_B #0C111A` | `Mix(BAR_B, BAR_T, 0.40)` |
| 主金线 | `GOLD #C8A44A` | `GOLD_L #E8D18A` |
| 金饰（菱形铆钉 / 结扣 / 金带） | `GOLD_L` | `Mix(GOLD_L, HILITE, 0.35)` |
| 金线 α | 各件原值 | **+40**（上限 255） |
| 墨边 / 形体 / 位置 / 尺寸 | — | **一律不动** |

产物 `Icon_Lobby{Friend,Shop,Event,Tutorial}Hover.png`：与常态**同尺寸 256×256**，只差色调。
实现是 `$ICON_TONE_N` / `$ICON_TONE_H` 两张色调表 + `Set-IconTone`，`New-LobbyIcon` 多一个 `-hover` 开关。
预览 `Tools/cardframe/preview/lobby-ui-v1-iconhover.png`（常态 / 悬停上下两行 1:1 + 压真背景的 60px 对照）。

**接线见「十三次修正」**（本节只留贴图配方）。想顺便「悬停时微微放大」，就在 `LobbyIconHover.OnPointerEnter` 里改
`localScale`（贴图不用再出）。

## 十三次修正（2026-09-26）：悬停 / 点击接线 + 占位弹窗

用户口径：**先做鼠标悬停变化，点击后的弹窗只做占位即可。** 把十二次修正出的四张悬停贴图接进场景。

**接线（全在 `Assets/_Game/Editor/LobbyUIBuilder.cs`，重跑菜单不会丢）**

- `WireIconHover(icon, 常态贴图, 悬停贴图, popup, 标题)` —— 给四个「压墙」图标（好友 / 商城 / 活动 / 教程）挂
  `Assets/_Game/Scripts/UI/Lobby/LobbyIconHover.cs`，填好 `icon / normalTexture / hoverTexture / popup / title`。
- `NewPlaceholderPopup(root, 900×520)` —— `Popup_Placeholder`，**存成 inactive**（场景里看不见）。

**占位弹窗结构**（点四个图标开的是**同一个**，只换标题）

| 节点 | 是什么 |
|---|---|
| `Popup_Placeholder` | 全屏容器，`LobbyPopup` 挂这里，场景里 `activeSelf=False` |
| `Dim` | 全屏遮罩 `Image #06090E α200` + `Button`（transition None）—— 点遮罩也关 |
| `Panel` | `LobbyPopupPlate.png` 900×520 |
| `Text_Title` | 标题（点哪个图标写哪个），字号 42 |
| `Text_Hint` | 「占位 · 待接真实面板」，字号 24 α0.62 |
| `Btn_Close` | `LobbyPopupBtnPlate.png` 200×64，锚面板右下 `(−36,36)`，字 26「关闭」 |

**关窗两条路**：`Dim` 与 `Btn_Close` 的 `onClick` 都是编辑器里加的持久监听，都指向 `LobbyPopup.Hide`
（`Assets/_Game/Scripts/UI/Lobby/LobbyPopup.cs`）。`Show()` 里 `SetAsLastSibling()` —— 弹窗永远压在最上层。

**只在 Play 模式生效**（`EventSystem` 不进编辑模式）；场景里的 `EventSystem` 已带 `StandaloneInputModule`，不用另挂。
`LobbyIconHover` 走 `IPointerEnter / IPointerExit / IPointerClick` 接口、**没有 `Button`** —— 图标自己的 `RawImage` 就是 raycast 目标。

---

## 十四次修正（2026-09-26）：右下角入口条（赛季 / 公告 / 藏品 / 战绩）

> **第四块 2026-09-26 十七次修正由「战绩」改成「成就」**（键名 / 贴图 / 徽记 / 场景节点 / 板上文字全换），详见文末。本条其余口径不变。

用户口径：**在右下角加入类似于这几个形状的紧挨着的平放着的、不参与视差的四块：从右到左分别是赛季、公告、藏品、战绩**，随后「**紧贴右下角**」。

| 项 | 取值 |
|---|---|
| 块数 / 顺序 | 4 块，屏幕上从右到左 = 赛季（盾徽）· 公告（喇叭）· 藏品（展框 + 菱形）· **成就（奖章 + 绶带）** |
| 板身 | 每块 **300x120** 屏幕 px（房间那档 324x130 的略小版）；出图 3 倍 -> 贴图 **954x414** |
| 格距 | **0** —— 四块板身首尾相接成一条（占屏 x 720 -> 1920） |
| 平放 | `$ENTRY_SHAPE` 里 shear / taper 全 0，场景 `Rotation Z = 0` |
| 不参与视差 | 不挂进 `Bg_v2` 的 `LobbyBgParallax.layers`（那份只有 6 层：Far 0.10 / Ring 0.85 / Near 0.30 + 入口板三层 -0.30） |
| 出图 | `Tools/cardframe/LobbyUIv1.ps1` 的 `New-LobbyEntryPlate` + `$ENTRY_SHAPE`（与上面四块入口板同一个函数，只去掉微透视） |
| 接线 | `Assets/_Game/Editor/LobbyUIBuilder.cs` 的 `BuildCornerRow()` —— 只建自己这棵 `CornerRow_v1`，**不重建 `LobbyUI_v1`** |
| 菜单 | `Tools/异界/生成大厅右下角入口条（赛季/公告/藏品/成就）` / `Tools/异界/删除大厅右下角入口条` |

**场景结构**：`CornerRow_v1`（anchor / pivot 都是 (1,0)、`anchoredPosition (0,0)`、`sizeDelta 1200x120`）下四个子物体
`Entry_{Season,Notice,Collection,Achievement}`，每个是 **RawImage**（不是 Image —— 直接读 `Image.sprite` 得到 NULL 属正常，
不要据此判「贴图掉了」），`sizeDelta 318x138`（= 954x414 / 3，正好 1:1 贴图映射），
`anchoredPosition x = -150 / -450 / -750 / -1050`、`y = 60`（pivot 0.5 居中 -> 板身正好落在屏幕下沿）。

**注意两条**：① 重跑「生成大厅 UI v1（占位）」会把这一条一起删掉，之后点一次上面那条右下角菜单即可重建；
② 这四张 meta 显式设了 **`npotScale = None`** —— 954x414 不能被吸成 1024x512（旧那批的教训见十六次修正的待办）。
   2026-09-26 十七次修正起这条由 `TextureImportSettingsGuard.NeedsNoNpotScale()` **自动兜住**：`lobby-ui-v1/LobbyCornerPlate_*`
   新导入时直接按 `None` 处理，不用再手改 meta。

## 十五次修正（2026-09-26）：邮件图标 + 四个压墙图标重排

- 新增 `Icon_LobbyMail.png` / `Icon_LobbyMailHover.png`（256x256），与好友 / 商城 / 活动 / 教程同族：深蓝黑平底 + 一条金细线 + 一处金饰（信封 + 折口 V + 下方菱形铆钉）。
- 右上横栏下那排「压墙」图标重排成 **商城 · 活动 · 教程 · 邮件**：板心 `x = 100 / 190 / 280 / 370`（步进 90）、`y = -66`、`60x60` -> 屏幕 x = 1512 / 1602 / 1692 / 1782。
- 顺带修掉 `New-ShopGlyph` 里 `AddPolygon` 的实参展开 bug（PS 把 `PointF[]` 拆成 N 个实参，`AddPolygon` 没有 4 参重载 -> 一直静默失败）：
  商城内缩金线**从来没画出来过**，所以 `Icon_LobbyShop.png` / `Icon_LobbyShopHover.png` 在这一版变了。**这个 bug 早于十四次修正。**
- 菜单：`Tools/异界/大厅：补邮件图标并重排四个压墙图标`（补丁式，只动这四个，不重建整棵 `LobbyUI_v1`）。

## 十六次修正（2026-09-26）：右下角那条提亮一级

用户口径：**这些下面的不动 ui 可以提亮一级和上面的做对比。**

`$CORNER_LIFT = 8` —— `New-LobbyEntryPlate` 多一个 `[int]$lift` 形参：

| 件 | 原样 | 提亮 +8 |
|---|---|---|
| 石面顶 | `BAR_T #1E2938`（30,41,56） | 38,49,64 |
| 石面底 | `BAR_B #0C111A`（12,17,26） | 20,25,34 |
| 左上亮楔 | 取 `BAR_T` | 跟着抬 |
| 徽记不透明度 | 46 | 54 |
| 墨边 / 金细线 / 徽记形状与位置 | — | **一律不动** |

**+8 就是本套「相邻色阶差 6-8 级」的一级。** 只有右下角那条的调用传 `-lift`，上面四块入口板仍是 0。
贴图尺寸不变（954x414）-> 场景不用重接。

**实测（`GetPixel` 取板面同一点）**：角板 29,37,49 / 上面入口板 22,30,42 —— 差 7-8 级。
对照图 `Tools/cardframe/preview/lobby-cornerlift-ab.png`（原样 / 提亮，1:1 + 放大 2x）。

**待办（已问两次，用户未答复）**：`lobby-ui-v1` 里**旧**那批贴图仍是 `npotScale = ToNearest`，
1034x445 的「房间 / 其它」被吸成 1024x512 -> 一直纵向拉伸约 15%（战斗 +4.5% / 卡牌总览 +2%）。
要不要一并改成 `None` 属于动用户已经调好的东西，**等点头**。
**2026-09-26 十七次修正补充**：右下角这四张已由 `TextureImportSettingsGuard` 自动守住；
**上面那批（战斗 / 卡牌总览 / 房间 / 其它）仍等点头** —— 守卫里刻意没扩过去。

---

## 十七次修正（2026-09-26）：第四块 战绩 -> 成就

用户口径：**将战绩改为成就，ui 也变一下，战绩会后续做到其它地方。** 随后从候选里选定**奖章**那一版，
并补一句「**不需要重叠，就这个图案就行**」（绶带不与圆盘相交，下端停在盘顶上方留一道缝）。

| 轴 | 旧（战绩） | 新（成就） |
|---|---|---|
| 键名 | `'record'` | `'achievement'` |
| 贴图 | `LobbyCornerPlate_Record.png` | `LobbyCornerPlate_Achievement.png` |
| 场景节点 | `Entry_Record` | `Entry_Achievement` |
| 板上文字 | 战绩 | 成就 |
| 徽记 | 三根高低柱 + 一条基线 | 奖章：两条绶带（上宽下收、**不碰圆盘**）+ 圆盘 + 中央菱形 |

**几何（`New-EntryEmblem` 的 achievement 分支；`$r = $ht * 0.30 = 108` 贴图 px）**

- 圆盘：半径 `0.68r`，圆心 `(cx, cy + 0.40r)`
- 中央菱形：半径 `0.30r`，与圆盘同心
- 绶带两条：`(cx ∓ 0.26r, cy - 1.24r)` -> `(cx ∓ 0.50r, cy - 0.36r)`（上端靠里、下端靠外）
- 整枚纵向占 `cy - 1.24r .. cy + 1.08r`；板身内缩金线在 `y 29..331`，上下都留得下

**战绩那支保留备用**：`New-EntryEmblem` 的 `'record'` 分支与 `$ENTRY_SHAPE['record']` 都还在，只是不在 `$CORNER_ORDER` 里；
要用就把键加回去、重跑出图，再点一次场景菜单。旧的 `LobbyCornerPlate_Record.png`（+meta）已删。

**查出来的一个真 bug（顺手修掉）**：新贴图 `LobbyCornerPlate_Achievement.png` 第一次导入时吃了 Unity 默认设置
`nPOTScale = ToNearest` -> 954x414 被吸成 **1024x512**；`BuildCornerRow()` 又按 `tex.width / 3` 算外框，
于是 `Entry_Achievement` 的 `sizeDelta` 一度变成 **341.33x170.67**。修法两条：

1. 新增 `TextureImportSettingsGuard.NeedsNoNpotScale(path)` —— `lobby-ui-v1/LobbyCornerPlate_*` 在 `OnPreprocessTexture`
   里直接 `npotScale = None`；`NeedsFix` 也加了这一项，跑「Tools/设置/贴图导入设置体检 & 修复」能查出来。
2. 重跑 `BuildCornerRow()`，把 `sizeDelta` 退回 **318x138**。

**教训**：本套「贴图尺寸 = 屏幕 px x 3、RawImage 1:1 映射」的前提是**贴图像素尺寸一个都不能被 Unity 改**。
以后往这一族加新板，先确认 meta 是 `nPOTScale: 0`（或直接让守卫兜）。

---

## 十八次修正（2026-09-27）：通用关闭叉 + 全屏子弹窗 / 常驻 HUD

**用户原话**：「现在做战斗的子全屏弹窗，通用背景，先生成一个通用的叉ui图标，用于关闭弹窗」，
以及「左上角和右上角的显示是在那些全屏显示的弹窗界面中仍显示在屏幕上（除非明确说明隐藏这些）的」。

**新增两个文件**

| 文件 | 规格 | 说明 |
|---|---|---|
| `Icon_Close.png` | 256×256（屏幕 56px） | 通用关闭叉：圆角方印 + 内缩金框（那「一处金饰」）+ 金叉；与「合起的书 / 信封」同族 |
| `Icon_CloseHover.png` | 同上 | 悬停态：石面提亮 + 金 `GOLD`→`GOLD_L`（与 `LobbyBtnPlateHover` / 其余图标同一配方） |

名字**不带 `Lobby`**：它是跨面板复用的那一个叉（全屏子弹窗 / 占位弹窗都用它），不属右上横栏那族、也不属压墙那族。
预览 `Tools/cardframe/preview/lobby-ui-v1-close.png`（常态 / 悬停 + 真尺寸压在 `common-bg-v1` 的 A 方案上）。

**通用背景不在本目录**：全屏子弹窗铺的是 `Generated/common-bg-v1/CommonBack_A_clean.png`（A 纯净方案，用户 2026-09-26 选定）。
它自带内缩 46px 的金细框，**所以面板不要再自己加边**；保持拉伸锚（非等比拉伸正是那张图的设计前提）。

**这一轮场景侧的口径（细则在 `AGENTS.md`「大厅三层」那段，这里只记与本目录贴图有关的两条）**

1. 关闭叉在场景里的落点是 `Panel_*/Btn_Close`：屏幕**右上角**、`anchoredPosition (-64,-128)`、56×56 ——
   64 让开通用背景那圈 46 的金框、128 让开右上横栏（高 95）。`New-CloseReview` 里合成用的就是这组真坐标。
2. 左上 `Plate_Profile` 与右上 `Plate_TopBand` 被搬进了 `Layer_Hud_v1`（Canvas 最后一个子物体）——
   **贴图一个字没改，只是画在弹窗之上**；锚 / 轴 / `sizeDelta` 全部原样（`m_Father` 是唯一变过的字段）。

### 好友侧边栏底板（2026-09-27）

`LobbyFriendPanel.png` —— 屏幕上 **465×1080**（贴图 3 倍 1395×3240），生成函数 `Tools/cardframe/LobbyUIv1.ps1` 的 `New-LobbyFriendPanel`。

- **宽 465 从哪来**：左上头像衬托板 `LobbyProfilePlate.png` 的贴图右缘 `x=1396`，折屏幕 `465.3` —— 侧边栏的右沿正好接在头像板右沿上，两条边在同一条竖线上。
- **满高、贴左沿**：上沿 / 左沿 / 下沿都齐屏幕边，只有右沿是滑出后的可见边界。
- **"不遮挡左上头像板 + 左下 ID 行" 不靠躲，靠层级**（用户 2026-09-27「不是不贴边，而是在它们层级之下」）：侧边栏挂在 `Layer_Sub_v1`，那两块在 `Layer_Hud_v1`，而 `Layer_Hud_v1` 是 Canvas 最后一个子物体 ⇒ 永远画在侧边栏之上。
- **形体**与 `LobbyProfilePlate` 同源：`BAR_T → BAR_B` 平底渐变 + 外墨边 `LW_INK` + 等比内缩 24 的金细线 `LW_GOLD`。不另加装饰。
- **场景侧**由 `Assets/_Game/Editor/LobbyUIBuilder.cs` 的菜单 **`Tools/异界/大厅：生成好友侧边栏（点击好友从左侧滑出）`** 建：`Canvas/Layer_Sub_v1/Panel_Friends`（根铺满全屏 + 全透明 Image + `Button`→`Close()`；子 `Body` 是那块满高板 + 一个**不带监听**的 `Button` 只吃点击），运行时由 `LobbyFriendPanel`（`Assets/_Game/Scripts/UI/Lobby/`）滑入 / 滑出（`slideTime` 0.18s）。
- ⚠ **别用自定义的第二个 MonoBehaviour 去接点击**：同一个 `.cs` 里除文件名那个类，Unity 都序列化不了（2026-09-27 实测在场景里存成 missing script）。用内置 `Button`。
- 面板里的 `Text_Title`「好友」**不摆在面板左上角**，而是紧挨好友图标右边（用户 2026-09-27「左上角的好友字改为这个地方，ui图标右边一点」）—— 图标在 `Plate_Profile` 局部 `(160,-57)`、`46×46`，所以标题 `x` 从 `160+46+16=222` 起、竖向对齐图标中线。
- **滚动的名单**（2026-09-27 用户：「加滚动」）：`Body/List` 是 `ScrollRect`（只竖滚、`Clamped` 不回弹、`scrollSensitivity 40`），**它自己没有贴图**；子 `Viewport` 挂 `RectMask2D` + 一张 α=0 的 `Image`（吃得到拖拽 —— Unity 不看 α），行都挂在 `Viewport/Content` 下，行模板 `RowTemplate` 也在 `Content` 里存成 inactive。
  `Content` 的高度 = 行数 × 84（`RowH`），由 `LobbyFriendListUI.Rebuild()` 改 —— **那就是可滚范围**；所以**没有**做滚动条，超出部分靠 `RectMask2D` 硬裁。
  ⚠ 回顶不能只写 `ScrollRect.verticalNormalizedPosition = 1`：它拿**上一帧缓存**的 content 边界换算，刚改完高度时会停在半个位置（实测 14 行停在 0.37）—— `LobbyFriendListUI.ScrollToTop()` 里是「先设归一化位置，再把 content 的 `anchoredPosition.y` 直接写 0」。

---

## 十九次修正（2026-09-27）：「房间」子全屏弹窗 —— 唯一差别是**好友图标不藏**

**用户原话**：「现在做房间，也是类似的全屏，不过左上角的好友不再隐藏，并且能在这个界面打开好友侧边栏」；
随后追加「加入房间和创建房间现在直接合并了，默认点击房间就是创建房间」。

**场景侧**（`Assets/_Game/Editor/LobbyUIBuilder.cs`）

- 新菜单 **`Tools/异界/大厅：生成「房间」子全屏弹窗（创建房间）`** → `Canvas/Layer_Sub_v1/Panel_Room`，
  壳与 `Panel_Battle` / `Panel_Other` **同一个工厂**（`BuildSubPanel`：`common-bg-v1` 通用背景 + 右上通用关闭叉 +
  `Body_Content` 限位框），`withHeader:false` 一样不出标题。
- **与战斗 / 其它唯一的差别**：`hideOnOpen` 只藏 `Icon_Shop / Icon_Event / Icon_Tutorial / Icon_Mail`，
  **点名排除 `Icon_Friend`** —— `FindNoBackdropHudIcons(hudLayer, "Icon_Friend")`（该 helper 这次加了 `params string[] except`）。
  所以房间面板开着时，好友图标照旧在左上角，点它开 / 关左侧好友侧边栏。
- 内容 = 一张**居中**的「创建房间」模式卡（`BattleModeCardsBuilder.BuildRoomCard`，`anchorX = 0.5`；
  战斗 / 其它那两处仍是贴左 `anchorX = 0`）。卡面贴图 `Generated/battle-mode-v1/BattleModeCard_Create{,Hover}.png`，
  徽记 = **拱门 + 加号**（拱门形制取自入口板「房间」的 `New-EntryEmblem 'room'`，同源）——
  加入房间已并入创建，所以不另出一张卡。

**为此动的两处运行时逻辑**（都不改贴图）

1. `LobbySubPanel.Open()` 里 `SetActive(true)` **之后**补了 `transform.SetAsLastSibling()`：
   全屏子弹窗打开时会把自己顶到 `Layer_Sub_v1` 最高，侧边栏若不跟着抬就会被盖住。
   ⚠ 抬层只在**本层**里做，压不到 `Layer_Hud_v1` ⇒ 头像板 / 好友图标 / 左下 ID 行照旧在最上面。
2. `LobbySubPanel.Open()` 里 `SetActive(true)` **之前**补了 `LobbyFriendPanel.Instance.Close()`：
   开新弹窗先把侧边栏收回去 —— 否则它被压在下面、状态却还停在「开着」，再点好友图标只会把它关掉、看着像没反应。

**自证**（`Assets/_Game/Editor/` 里的一次性执行器，跑完已删；报告与图在 `%USERPROFILE%\.codex\visualizations\2026\09\26\01a0dccd-*`）

- `stage26_room.txt` + `stage26_shots/30_room.png`（房间面板单独开着：好友图标可见）、
  `31_room_friends.png`（房间 + 侧边栏同开：侧边栏在房间之上，头像板与左下 ID 行仍压在最上面）。
- 实测对照：`Panel_Room` 开着时 `Icon_Friend activeInHierarchy = True`；`Panel_Battle` 开着时 = `False`。
- 层级实测：`Layer_Sub_v1` 里 `Panel_Room` 同级序 3 < `Panel_Friends` 4 ⇒ 侧边栏画在上面。

---

## 十九次修正 · 二改（2026-09-27）：「房间」内容换成建房界面本身 —— 模式卡撤掉、点房间即建房

**用户原话**：「我的意思是点击房间就直接进入了创建房间的功能，不需要再次点击，加入房间的功能内嵌在这个总房间功能里，
在现在这个创建房间和好友侧边栏中间靠上边区域是显示房主头像和名称，下面那个是显示加入玩家头像和名称，
把现在这个创建房间的卡牌隐藏掉，另外右上角的叉左边显示：房间号：xxxxxx，悬停变色点击会在下面浮现：已复制到剪切板」

**场景侧**（`Assets/_Game/Editor/LobbyUIBuilder.cs`）

- **撤掉那张模式卡**：`BuildRoomSubPanelMenu` 不再调 `BattleModeCardsBuilder.BuildRoomCard(panel)`。
  `Panel_Room` 的子物体顺序实测 = `Bg, RoomPlayers, Body_Content, Text_RoomCode, Btn_Close`（叉子由 `SetAsLastSibling()` 顶在最上）。
  贴图 `BattleModeCard_Create{,Hover}.png` 与 `BuildRoomCard` 都还在，只是**不再被挂**。
- **`RoomPlayers`**（新，`BuildRoomPlayers` / `BuildRoomSlot`）：两个玩家槽，放在**建房卡原位与好友侧边栏之间靠上**——
  `RoomSlotX = 640`（好友侧边栏右沿 465 与屏幕中心 960 之间）、`RoomHostTop = -220` / `RoomGuestTop = -420`（自面板左上角量）。
  槽 = 头像环（`LobbyAvatarRing.png` 放大到 `RoomRingSize 160`，**与左上角那块同源**，环 88 : 井 68 的比例不变）
  + 井 `RoomWellSize 124`（内缩 18）+ 右侧名字（`RoomNameFont 40`，左对齐、与头像中线齐）+ 身份小字（`RoomRoleFont 26`，奶油 55%）。
  - **房主槽**挂 `PlayerProfilePanel`（`circularCrop = true`）⇒ 运行时取**本机** Steam 头像与名字（建房的人就是房主），实测出「心响」+ 真头像。
  - **加入玩家槽**是空态：`等待加入…` / `加入玩家`。⚠ 它的井必须把 alpha 压 0 —— **空 `RawImage`（texture = null）默认画一块纯白方块**
    （2026-09-27 实测踩到，Stage27 的 `40_room_build.png` 里那口井是白的）；`BuildRoomSlot` 里那句 `if (!isSelf) avatar.color = new Color(1f,1f,1f,0f)` 就是补这个。
- **`Text_RoomCode`**（新，`BuildRoomCode`）：关闭叉**左边**那行房间号。锚右上、轴 `(1,1)`，`RoomCodeX = -252`（叉左沿在 -228，再让 24）、
  `RoomCodeY = -66`（框高 60 ⇒ 中线 -96 正好落在叉的中线上）、`RoomCodeW 420`、右对齐、`RoomCodeFont 30`、奶油色。
  复制提示是它的子物体 `Text_CopyToast`（`RoomCodeToastY = -60`，字号 24，`raycastTarget = false`）——「下面浮现」因此是跟着这行走的。

**运行时逻辑**（`Assets/_Game/Scripts/UI/Lobby/LobbyRoomCodeTag.cs`，新）

- 右上那行挂 `LobbyRoomCodeTag`：`IPointerEnter/Exit/Down` 三件套，**不挂 `Button`**（不要点击音效与位移）；悬停字色 `#D6C298 → #E4CB84`
  （`normalColor` / `hoverColor`）；点击 `GUIUtility.systemCopyBuffer = code`，提示语从上方滑入（`toastSlide 12`）→ 保持 1s → 淡出 0.8s。
- **号是 `OnEnable` 现生成的**（面板由 `SetActive` 开关 ⇒ 每次打开都是一串新号）：6 位，字母表
  `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`（剔掉容易看错的 `0 O 1 I`）。已留 `SetCode()` 给未来真房间号。

**自证**（`Assets/_Game/Editor/` 里的一次性执行器，跑完已删；报告与图在 `%USERPROFILE%\.codex\visualizations\2026\09\26\01a0dccd-*`）

- `stage27_room_ui.txt` + `stage27_shots/40_room_build.png`（建房界面）、`41_room_copied.png`（复制提示浮在号下面）、`42_room_code_hover.png`（悬停变金）。
- `stage28_room_ui.txt` + `stage28_shots/43_room_fixed.png`（复验空槽白方块：`texture=<null> color=RGBA(1.000,1.000,1.000,0.000)`，井是空的深色环）。
- 实测断言：`Panel_Room` 里 `ModeCards = False`（对照 `Panel_Battle` = `True`）；房主行 = 「心响」；点号后剪贴板 = `BELWJ8`／另一轮 `RUZGEL`；
  悬停字色 `RGBA(0.894, 0.796, 0.518, 1)` = `#E4CB84`；房间面板开着时 `Icon_Friend activeInHierarchy = True`。

## 二十次修正（2026-09-27）：「房间」的踢出 / 开始游戏 —— 两个文字按钮各自带子背景

**用户原话**：「踢出和开始游戏是有个子背景的」；随后「哦开始游戏没有对齐啊」。

**贴图**（`Tools/cardframe/LobbyRoomChipV1.ps1`，产物进本目录 + 上面那张表）

- 配方 = MatchWait「取消」那套（`New-MwPlate`）：**平底竖渐变**（`#1E2938 → #0C111A`）+ 左上亮楔 + 外墨边 9 + 等比内缩金线 3
  （内缩 18 / 圆角 22）。常态金线 **α150**、悬停 **α236**；两张底只差色调 ⇒ **切图不跳位**。
- `LobbyChip_Kick{,Hover}.png` 288×144 = 屏幕 **96×48**（两个字 / 字号 30）；`LobbyChip_Start{,Hover}.png` 636×192 = 屏幕 **212×64**（四个字 / 字号 42）。
- 两条都是 **NPOT** ⇒ `Assets/_Game/Editor/TextureImportSettingsGuard.cs` 的白名单加了 `LobbyChip_` 前缀（`NeedsNopotScale` 第 60 行 / `NeedsAlphaIsTransparency` 第 68 行各一处）。

**场景侧**（`Assets/_Game/Editor/LobbyUIBuilder.cs`）

- 新件 **`BuildTextChip(...)`**：一个物体出齐「底（`Chip`，RawImage，`raycastTarget = false`）+ 字（`Text_Label`，TMP 居中）+ `Button` + `LobbyChipHover`」。
  点击归 `Button`（`targetGraphic` = 那行字；ColorTint = 常态奶油 `#F0E8D2` / 悬停与点击金 `#E4CB84` / 禁用灰 `#6E7783`）；
  悬停换底归 `LobbyChipHover`（`IPointerEnter/Exit` 只换贴图）—— **故意不复用 `LobbyIconHover`**：后者 OnPointerClick 会顺手关好友侧边栏。
- **踢出**：`RoomPlayers/Slot_Guest/Btn_Kick`，`RoomKickRight = -20`（右沿距头像环左沿 20）、`RoomKickY = -80`（与环中线同高）、96×48、字号 30。
- **开始游戏**：`RoomPlayers/Btn_StartGame`，`RoomStartX = 820`、`RoomStartY = -400`（两条槽的正中）、212×64、字号 42。
- 顶中提示 `Text_LobbyToast`（挂在 `Layer_Hud_v1` 下）由 **−240 上移到 −150** —— −240 正好叠在房主那行名字上。

**对齐**（第二句的修法）

- 原 `RoomStartX = 812` ⇒ 子背景左沿比**名字 / 身份那列**（`RoomSlotX 640 + RoomNameX 180 = 820`）还左 **8**（屏幕 ≈ 6 px）—— 看着就是「差一点点」。
- 改成 **820** 之后：子背景左沿 = 名字列左沿（实测差 **0.0 px**），同时距头像环右沿正好 **20**（与「踢出」那 20 **对称**：
  踢出右沿 − 环左沿 = −15.5 px = −20×scale）。竖直不变：子背景中线 = 两条头像环中线的中点（实测 527.6 / 527.8 相等）。

**运行时逻辑**（三个新件：`LobbyRoomPanel.cs` / `LobbyChipHover.cs` / `LobbyToast.cs`）

- `LobbyRoomPanel` = 本机房间状态机（`_isHost` / `_hasGuest` / 客人名 + 头像）+ 三个点击（`OnKickClicked` / `OnStartGameClicked` / `OnCloseClicked`）+ `Refresh()`：
  **踢出** = 房主视角 **且** 房里有人；**开始游戏** = 房里有人就显示（客人也看得见），但 `Button.interactable` 只有房主为 true ⇒ 客人那条是**灰的**。
- 「关面板不清状态」：`_established` 让房间信息在关面板后留着（用户：「只是关闭信息都在」）；面板重开时只有 `!_established` 才重置成房主。
- 收尾接缝：`MatchConfirmPanel.OpenFromRoom(room)` —— 拒绝 / 15 秒超时 / 对方拒绝三条都走 `ReturnToRoomAfterDecline()`（**回房间、不重排**）；双方确认走 `OnBothConfirmed()` → `BattleLoadingScreen.Open()`。
- `LobbyToast.Show(msg)` = 屏幕中央上方一次性提示（淡入 0.22 / 全亮 2.2 / 淡出 0.55 秒）；物体**常驻 active**、只把 α 归零（它自己的 Update 负责淡出）。
- 联机接缝（真 Steam 大厅进来时接）：`SetGuest` / `ClearGuest` / `OnGuestLeft` / `OnHostLeft`。

**自证**（`stage29` ~ `stage32` 的报告与图都在 `%USERPROFILE%\.codex\visualizations\2026\09\26\01a0dccd-*`；执行器跑完已删）

- `stage29_room_flow.txt` + `stage29_shots/50…57`：空房态两键不显 → 有客人 → 客人视角（`interactable = False`，渲染色灰 `RGBA(0.431,0.467,0.514,1)`）→
  点开始游戏（房间面板关、确认弹窗开）→ 拒绝（**回房间**）→ 踢出 → 房主离开（转交 + 提示）→ 双方确认（倒计时金 `#C8A44A` + 3）。
- `stage30_room_chip.txt` + `stage30_shots/60…64`：子背景实测 96×48 / 212×64、贴图 `LobbyChip_Kick` / `LobbyChip_Start`（悬停贴图 `…Hover`）。
- `stage31_room_hover.txt` + `stage31_shots/70…71`：常态 / 悬停对照 —— 悬停换 `…Hover` 贴图 + 字色转金。
- `stage32_room_align.txt` + `stage32_shots/80_room_align_guest.png`：**对齐修正**（`RoomStartX 812 → 820`）——
  开始游戏子背景左沿 − 名字列左沿 = **0.0 px**、− 环右沿 = 15.5 px（= 20×0.7759）、踢出右沿 − 环左沿 = −15.5 px；竖直中线 527.6 = 两条环中线中点 527.6。

## 二十一次修正（2026-09-27）：房间面板「加入房间」图标 + 真房间号（Steam 大厅）

**用户原话**：「接入，另外在叉ui左边做一个大小一样的加入房间的简单ui，button，位置和教程位置重叠即可」。

**贴图**（`Tools/cardframe/LobbyIconJoinV1.ps1`，产物进本目录 + 上面那张表）

- 与「金线石印族」（好友 / 商城 / 礼盒 / 合起的书 / 信封 / 叉）同一套配方：深蓝黑平底竖渐变（`$BAR_T → $BAR_B`）+ 外墨边 9 + 等比内缩金线 6 + 一处金饰（菱形铆钉）；
  常态 / 悬停只差色调（`Set-IconTone`），形体尺寸一致 ⇒ **切图不跳位**。
- 形体 = **门框（内缩金线 + 菱形门把手）+ 一支指向门内的金箭头**；箭头线宽 = 金细线 ×1.5 = 9 贴图 px，当这一族里「主元素」那一档（叉的 X 用的是 ×2）。
- 256×256 贴图 → 屏幕 **60×60**（与教程 / 叉同尺寸）；1 贴图 px ≈ 0.234 屏 px。
- 预览：`Tools/cardframe/preview/lobby-icon-join.png`（与教程 / 叉并排，1:1 + 3 倍）。

**场景侧**（`Assets/_Game/Editor/LobbyUIBuilder.cs`）

- 新件 `BuildRoomJoinButton(panel)` → `Panel_Room/Btn_JoinRoom`：锚右上 / 轴左上，`RoomJoinX = SubPanelCloseX - SubPanelCloseSize - 16 = -244`、`RoomJoinY = SubPanelCloseY = -66`、60×60。
- 实测：加入房间右沿 − 叉左沿 = **12.4 px** = 16 × 0.776 ✓（设计上就是紧挨着同尺寸那一个）；与 `Icon_Tutorial` **x / y 都重叠**（1300..1346 对 1289..1335）—— 用户要的就是这个，
  房间面板开着时教程本来就被 `hideOnOpen` 藏起来了。
- **不挂 Button**：悬停换贴图 + 点击弹占位弹窗都走 `LobbyIconHover`（与商城 / 活动 / 教程同款）；点击弹 `Popup_Placeholder`，标题「加入房间」。

**运行时**（新件 `Assets/_Game/Scripts/UI/Lobby/LobbyRoomSession.cs`）

- 建房时机 = **面板被用户打开**：`LobbySubPanel.Open` → `ILobbySubPanelOpen.OnSubPanelOpened` → `BeginHosting`。
  ⚠ **不能挂 OnEnable** —— 面板在场景里存成 active（方便编辑），运行时第一帧就被 `closeOnStart` 关掉，挂 OnEnable 会在玩家没点过「房间」时就先开一间大厅。
- 号：6 位、字母表同 `LobbyRoomCodeTag`（**36 个字符 = 22 亿种**；旧壳是 6 位纯数字 = 90 万种）。**先查重再建房** —— `RequestLobbyList` 不允许在「已处于某个大厅」时调用，
  所以顺序是：按 `room_code` 过滤请求列表 → 没人用 → 才 `CreateLobby` → `SetLobbyData(lobby, "room_code", 号)`；查重没回调 3.5 s 超时兜底（宁可极小概率撞号，也不能让号一直不出现）。
- 大厅 key 全同旧壳：`game=anotherworld_room` / `room_code` / `host_data` / `player_data` / `start` / `host_sid` / `kicked` —— 旧壳的客人端和新面板**能互通**。
- 客人槽：房主每 0.5 s `RequestLobbyData` + 读非自己的成员 `player_data` → 名字 / 头像（`SteamAvatarManager`）/ SteamID → `SetGuest`；人走了 → `OnGuestLeft`（提示「玩家xxxx离开」）。
- 三个信号：踢出 → `kicked=1`（用完清 0）；双方确认 → `start=1` + `host_sid` + 填 `LobbyConfig`（旧壳进 Game 那套）；房主关面板 → `LeaveLobby` + 号作废。
- **Steam 未登录 / 未连接**（`SteamManager.Initialized && SteamUser.BLoggedOn()` 为假）或建房失败 → `ApplySteamOffline()`：房间号那行换成灰字 `#6E7783` + 开始游戏 `interactable = false`（沿用匹配 / 排位那条规矩）。
- `LobbyRoomCodeTag` 加 `_explicit`：被显式给过号 / 灰态之后，面板重开**不再自己生成占位号**（实测踩到过：真号被重开时的占位号顶掉）。
- 客人那侧的面板还没做；接缝是 `LobbyRoomSession` 的 `SearchByCode(code, done)` + `JoinFound(lobby, done)`（2026-09-27 二十二次修正：旧的 `JoinByCode` 已被这两个替掉）。

**自证**（`stage34_room_join.txt` + `stage34_shots/90..92`；执行器跑完已删）

- **90**：屏幕那行读作「房间号：**AUP733**」，从 Steam 大厅数据读回 `room_code = AUP733` / `game = anotherworld_room` / lobby `109775243057510319` —— **两边一致**，
  即屏幕上这串就是别人能用来搜到的那个号（Steam 在线 `Initialized=True BLoggedOn=True`）；加入房间图标在叉左边、与教程重叠。
- **91**：点「加入房间」→ 占位弹窗（标题「加入房间」+ 一行提示）。
- **92**：`ApplySteamOffline()` 后那行 = 「Steam 未登录 / 未连接」，颜色 `RGBA(0.43,0.47,0.51,1.00)` = `#6E7783`；开始游戏 `interactable = False`。
- 顺带实测：测试里那个「假客人」（只调 `SetGuest`、不是真大厅成员）被真实大厅轮询清掉并弹了「玩家星野测试离开」—— 客人槽现在完全由大厅成员驱动。

## 二十二次修正（2026-09-27）：房间面板「加入房间」= 右侧侧边栏

**用户原话**：「加入房间是一个右侧侧边栏（到顶，但不需要完全到底），范围到右上角的左边，不遮挡房间号，ui等，再次点击（或者点击范围外）滑动回去，先是显示在右边房间号和 ui 下面的加入房间四个字，然后金线分割一下，下面是一个输入框，再下面是输入后的预览，主要是展示搜索目标的头像/名称，其下面是人数 1/2 或者红色的 2/2，然后数字右边是加入（有子背景）（根据是否满人为白色（可变金色）或者红色）」。

**贴图**：`LobbyJoinSidebar.png` 1614×2760 = 屏幕 **538×920** ×3（`Tools/cardframe/LobbyJoinSidebarV1.ps1` 出，预览 `Tools/cardframe/preview/lobby-join-sidebar.png`）—— 与 `LobbyFriendPanel` 同一套配方：平底竖渐变 `#1E2938 → #0C111A` + 外墨边 3 + 等比内缩金细线 10。**板顶开口**：左右两条线跑到画面上沿、**只有下方两个圆角** —— 上封边会被横栏压着看不见，反而会在横栏左端斜切的左侧露出一小截横线，所以不画。

**几何（画布 1080 口径；逐件实测「与板相交 = 否」）**

- **（七改 · 最终）板 = 右上横栏（`Plate_TopBand` / `LobbyBandRight`，538×95）那一块的背景** —— 与好友侧边栏 `LobbyFriendPanel`（宽 = 左上头像板宽度、齐屏幕上沿 / 左沿）**同一套口径**：上沿 = 屏幕顶（**0**）、左沿 = 横栏左沿（1920 − 538 = **1382**）、右沿贴屏幕右沿 ⇒ 板宽 **538**；下沿让开 **160**（「不需要完全到底」）⇒ 板 **538×920**。逐项实测与横栏持平：上沿 0 / 右沿 0 / 左沿距右 538（`stage44`）。用户原话：「缩到和右上角顶端一样位置不变作为其背景」「好友侧边栏不是一样的要求吗，和右上角最左侧持平」。
- **顶满靠层级、不靠躲**：本板在 `Panel_Room` 里的兄弟位次插在 `Text_RoomCode` **之前**，所以「房间号 / 加入 / 叉」压在板顶那一条上照样看得见、点得到（用户：「上顶满的意思是像好友那样作为右上角和房间号 ui」）。⚠ 运行时 `LobbyJoinSidebar.Open()` **不许再 `SetAsLastSibling`** —— 一抬到最上面这三个件就被板盖住了（stage40 实测踩过）。
- **内容位置没动**：板一路顶到屏幕顶（172 → 58 → 0），板里那层 `Content` 始终钉在**屏幕顶下来 126**（现在是 `offsetMax = (0, −126)`），标题 / 金线 / 输入框 / 预览的**屏幕坐标从五改起一个字没变**（用户：「缩到和右上角顶端一样位置不变」）。逐项实测：`Text_Title` 顶 = 166、`Input_RoomCode` 顶 = 284。
- **超出的件右移进板内**：`Text_RoomCode` 原本 420 宽、左探到 1247（压住板左沿）⇒ 收成 **238** 宽、右沿钉在距屏右 **252**（= 加入图标 244 + 8）⇒ 框 **1429..1667**，左沿正好 = 板左沿 1381 + `JoinSidePadX` 48（用户：「其它超出组件向右平移到内部」）。⚠ 这个框是**锚右上 + 轴也右上**（`pivot (1,1)`），`RoomCodeX` 量的是**右沿**不是左沿 —— 按左沿写成 −490 会把整行推出板外（stage43 实测踩过，已改 −252）。
- 板内自上而下：标题「加入房间」`y=-40` → 金细线 `-118` → 输入框 `-158`（高 76，平底凹井 `#0C111A` + 四边金细线）→ 预览 `-280`（头像环 96 / 井 74）→ 名字 + 人数 → 人数右边「加入」`96×48` → 提示行 `-400`。

**运行时**（新件 `Assets/_Game/Scripts/UI/Lobby/LobbyJoinSidebar.cs`）

- 滑动与 `LobbyFriendPanel` 同一套（progress 0..1 + `MoveTowards` + 快进慢出，方向朝右）；根节点铺满全屏 + 一张全透明 `Image`（`raycastTarget` 开着）= **点板以外滑回去**；`OnDisable` 复位，父级关掉后不留滑出态。
- 输入：只收大写字母 / 数字、≤6 位，满 6 位自动搜；搜之前先 `SuspendHosting`（`RequestLobbyList` 不允许在「已处于某个大厅」时调用），没搜到 / 关板再 `ResumeHosting` 用**同一个号**重建 —— 屏幕上的房间号不会因为来搜一次就变。
- 加入：`JoinFound`（真 `JoinLobby` + 写成员数据）→ 关板 → 房间面板切客人视角。
- 满员 = `2/2` 生命红 `#B64848` + 「加入」`interactable = false`（走 `Button` 的 `disabledColor`，与踢出 / 开始游戏同一个色）；不满 = 白、悬停金。**实测**：`Text_Count` 色 `F0E8D2EC → B64848FF`、提示行「房间已满」、子背景贴图两态不变（只有字变色）。
- 入口：`Btn_JoinRoom` 的 `LobbyIconHover` 不再弹占位窗（`popup` 已置空），改成 `joinSidebar.Toggle()`。

**右键粘贴（2026-09-27 · 八改）**

**用户原话**：「加入一个在输入栏右键自动粘贴复制的房间号功能」。TMP_InputField 自己**只认左键** —— `OnPointerClick` 第一行就把非左键 `return` 掉（实测 `com.unity.textmeshpro@3.0.7`），所以右键一直是空的；键盘那套 Ctrl+V / Shift+Insert 有是有，但没有鼠标路径。

- **新件** `Assets/_Game/Scripts/UI/Lobby/LobbyJoinInputPaste.cs`：`IPointerClickHandler`，挂在输入井（`Input_RoomCode`）**同一个物体**上（井自己那张平底 `Image` 就是 raycast 目标，整口井都能右键），只接**右键**（左键照旧留给 TMP 定位光标），转交 `LobbyJoinSidebar.PasteFromClipboard()`。以后另一口井（比方说好友 ID）挂一个本件 + 指一个 owner 就能复用。
- **`LobbyJoinSidebar.PasteFromClipboard()`**：读 `GUIUtility.systemCopyBuffer` → 走**与手输同一条** `Clean()`（只留 A-Z / 0-9 + 转大写 + 截 6 位）→ **整段替换**（不是插到光标处 —— 6 位号的井替换才是想要的）→ `input.text = code` 照常派 `onValueChanged` ⇒ `OnCodeChanged`，满 6 位自动去搜，和手打进去没有区别 → 光标收到末尾。
- **剪贴板拿不到 / 洗完是空的**：提示行显示「剪贴板里没有房间号」，**不动输入框**（免得平白把已输的号清掉）。
- 贴完 `ActivateInputField()` 把焦点留在这口井上；TMP 的 `onFocusSelectAll`（默认 true）会把整段选中、光标停在末尾 —— 与「左键点进这口井」的既有行为**一致**，不是本件引入的。

**自证**：**`stage44_join_box.txt` + `stage44_shots/`**（`r1_open` 滑出 / `r2_full` 预览填充）—— 实测 `Body offsetMin=(-538,160) offsetMax=(0,0)`、板 538×920、上沿 0 / 右沿 0 / 下沿 160 / 左沿距右 538（与 `Plate_TopBand` 三项逐项持平）、`Content` 顶 126、`Text_Title` 顶 166、`Input_RoomCode` 顶 284、`Text_RoomCode` 1429..1667（左距板 48 / 右距屏右 252，整行在板内）、运行态起点 546 → 静止位 `anchoredPosition.x = 0`、`Text_RoomCode` active 且兄弟位次 4（在板之上）。执行器跑完已删。
**自证（八改 · 右键粘贴）**：**`stage45_paste.txt` + `stage45_shots/`**（`r2_paste` 贴完 / `r3_steady` 到位）—— 编辑态：`Input_RoomCode` 上 `TMP_InputField` + `LobbyJoinInputPaste` 都在、`paste.owner == LobbyJoinSidebar`、`side.input` 就是这口井、`onValueChanged` 持久监听 1 条 = `OnCodeChanged`；几何回归：板 538×920、上沿 0 / 右沿 0 / 下沿 160 / 左沿距右 538、`Text_RoomCode` 1429..1667 整行在板内、侧边栏兄弟位次 3 在房间号（4）之前。右键四测（直接调 `OnPointerClick` —— 真实鼠标右键 EventSystem 也是进这一个方法）：`" ab-12cd "` → `AB12CD`、`"房间号：gh-77kl"` → `GH77KL`、`"房间号：！！！"` → 输入框不动 + 提示「剪贴板里没有房间号」、**左键** `"ZZZZZZ"` → 输入框不变（左键不贴）。
**注**：`stage45` 里「等了 0.9s 侧边栏还停在 546」是**执行器自己的时序 bug**（`Shot()` 设的 0.9s 等待被紧随其后的 `SetPhase()` 用 `_nextAt = Now` 冲掉，那一拍读在滑动第一帧之前），不是产品问题 —— `stage46_slide.txt` 逐帧打点（`Application.runInBackground = True`、60fps、`progress` 0 → 1、`x` 546 → 0）已证伪，`s_open.png` 是贴屏右沿的静止位。两个执行器跑完都已自删。
回退链：`stage43_join_box.txt` + `stage43_shots/`（板与横栏已三项持平、但 `RoomCodeX` 误按左沿写成 −490、整行还在板外那一版）、`stage42_*`（564×862 / 顶缝 58 / 右缩进 108）、`stage41_*`（板顶 58 / 右沿 168，叉留在板外）、`stage40_*`（顶到屏幕顶那一版）、`stage37_join_sidebar2.txt`（板顶 172）、`stage36_*`（板顶 120，与房间号那行有 6 单位矩形重叠）。

## 二十三次修正（2026-09-27）：好友邀请（加号 · 10 秒冷却 · 「收到邀请」小窗）

**用户原话**：「好友栏中处于在线（非战斗状态和匹配状态）时其名字右边会出现一个加号，点击后发送邀请并且加号变成 10 秒倒计时（倒计时结束后才能继续邀请）（若自己此时不是在房间界面就进入房间并开房间），邀请是从屏幕中央上顶滑出一个小框，上面标题是收到邀请，下面一排是对应玩家头像和名称，在下面是有子背景的同意和拒绝，同意后会加入其房间，拒绝后对方也会收到：对方暂无法响应」。

**贴图**（三张新 + 一个目录）

| 件 | 出处 | 规格 |
|---|---|---|
| `Icon_InvitePlus.png` / `…Hover.png` / `…Cool.png` | `Tools/cardframe/LobbyInvitePlusV1.ps1`（预览 `Tools/cardframe/preview/lobby-invite-plus.png`） | 256×256 = 屏幕 **60**（同批图标同档）。常态 / 悬停（石面提亮）/ **空板** |
| `invite-v1/Invite_Plate.png` | `Tools/cardframe/InvitePanelV1.ps1`（预览 `Tools/cardframe/preview/invite-panel.png`） | 1080×720 = 屏幕 **360×240**（RawImage 拉伸铺满；`New-MwPlate` 配方逐行照抄 `MatchWait_*`：平底渐变 + 外墨边 + 等比内缩金细线，**开口只在上沿**，同匹配小窗） |

- **加号是烘进贴图的**（不是场景里的字 / 图形），冷却那 10 秒藏不掉它 —— 所以另出一张**同一块空板** `Icon_InvitePlusCool.png`，冷却期间整张换掉、金数字叠在上面。三张的尺寸完全一致，换图不会跳。
- **`invite-v1` 进了 `TextureImportSettingsGuard.NoNpotScaleFolders`**（→ 同一条数组也带上了 `NeedsAlphaIsTransparency`）：1080×720 被默认的 `npotScale=ToNearest` 吸成 1024×1024 就不是 3:2 了，圆角与金线会跟着变形。

**新件 / 改动**

- `Assets/_Game/Scripts/UI/Lobby/FriendInviteButton.cs`（新）：加号那一格上的 `IPointerEnter/Exit/Click`，只转交 `FriendRowUI`；`OnDisable` 只清 hover、**不回填贴图**（回填会把它自己 `SetActive(true)` 出来，反而藏不住）。
- `Assets/_Game/Scripts/UI/Lobby/LobbyInviteService.cs`（新，挂在 Canvas 上）：冷却表 `Dictionary<ulong,float>` + 队列 + `Invite(id,name)` / `CanInvite` / **`static CooldownLeft(id)`**（行每帧读它，所以列表 20 秒重排一次也不会把倒计时洗掉）/ `EnsureRoom()` / `OnInviteReceived` / `PublishDecline` / `PollReplies`。
- `Assets/_Game/Scripts/UI/Lobby/LobbyInvitePanel.cs`（新，`Canvas/Layer_Hud_v1/Panel_Invite`）：`Show(inviter, name, lobby)` → 返回 false = 排队（一次只摆一条）。
- `FriendRowUI`：`inviteGroup / inviteIcon / inviteTimer / inviteNormal / inviteHover / inviteCool` + `InviteClicked / ApplyInviteVisual / TickInvite`（`Update` 拆成 `TickAvatar` + `TickInvite`）；`_canInvite = Presence == Online && SteamId != 0`。
- `LobbyRoomPanel.OpenAsGuest()` + `LobbyRoomSession.JoinInviteAsGuest(lobby, room, done)`：**同意 = 进对方的房** —— 先立「客人视角」再开面板（**跳过建房**，建房再 `JoinLobby` 会撞车），然后真 `JoinLobby`。
- `LobbyToast.SetExtraDrop(float)`：小窗开着时把提示行往下顶 `toastDrop = 250`（两者同在屏幕顶中，不顶就会叠在一起）。
- `LobbyUIBuilder`：常量段 + `WireInvitePlus(...)`（幂等；顺手把 `Text_Name` 收窄到 `409-96-76 = 237`）+ 两个菜单（`Tools/异界/大厅：给好友行补「邀请加号」（好友邀请）`、`…生成「收到邀请」小窗（好友邀请）`）。

**口径**

- **只有「在线」给加号** —— 匹配中 / 对局中 / 离线一律不出现（所以名字那行在场景里已经按「让出那一格」收窄过）。无 Steam 身份（`SteamId == 0`）也不给。
- **冷却 10 秒按 SteamID 记在服务里**，不在行上；冷却没走完点它**没有任何反应**（不重置冷却）。
- **点加号时若不在房间界面 → 自动进房间并开房**：`EnsureRoom()` 走的就是「点房间」那条壳（`shell.Open(null)`），用户要的那条。
- 小窗：锚 / pivot 都在屏幕顶中，`restY = -10`（贴屏幕顶，与匹配小窗同档）、`hiddenY = 260`（整块在屏幕顶之上）、滑出 `0.28s` / 收回 `0.16s`，曲线 `1-(1-p)^3`（与两个侧边栏同一条）；**收干净了才 `SetActive(false)`**，所以收窗也有动画。
- **拒绝** → `PublishDecline`：往自己的 rich presence 写 `aw_invite_reply = "<邀请者ID>|decline|<unix秒>"`，邀请方 `PollReplies` 读到 `to == 自己 && unix >= 发出时间` 才弹「对方暂无法响应」（带上 unix 是为了幂等，读到的旧回信不会重复触发）。
- **同意** → `OpenAsGuest` + `JoinInviteAsGuest` + 收窗；大厅 ID 是邀请回调直接给的（`LobbyInvite_t.m_ulSteamIDLobby`），不用按号搜。

**两个自己踩到的坑（都已在代码里堵上）**

1. **`Hide()` 在 `_p <= 0` 时收不掉** —— 同帧 `Show → Hide`（走脚本调用时会发生）时 `_p` 还是 0，`Update` 首行判断直接 return，窗口就停在「active 但看不见」，`_cur` 也不清 ⇒ 之后每条邀请都只排队。现在 `Hide()` 里 `if (_p <= 0f) Collapse();` 当场收干净。
2. **头像到货不补图** —— `Present` 那一次 `GetAvatarTexture` 只是**触发请求**（当场拿到的是灰占位 / Steam 默认像），小窗不像好友行那样每帧问缓存，于是会一直停在占位上。现在 `LobbyInvitePanel.Update` 先 `TickAvatar()`：每 `0.5s` 问一次 `PeekAvatar`，**拿到真图才重裁一次**（`_avatarSeen` 记住上次那张，避免每 0.5s `CircleCrop` 一张废图）。

**收尾（stage47b）：`Invite_Plate` 的导入设置**

stage47 唯一一条失败就是它：`npotScale=ToNearest` / `alphaIsTransparency=0` —— 贴图先落盘、守卫（`OnPreprocessTexture`）后补的那条**时序差**，守卫**不会回头重导**已有资源。`stage47b` 补跑 `TextureImportSettingsGuard.ApplyTo(imp, path)` + `SaveAndReimport()`，实测变成 `npotScale=None / alphaIsTransparency=True / filterMode=Bilinear / mipmap=True / aniso=4` —— **与 `match-wait-v1/MatchWait_Plate.png` 逐项同档**，源尺寸仍 1080×720。

**自证**

- **`stage47_invite.txt` + `stage47_shots/`**（`f1_friend_plus` / `f2_countdown` / `f3_invite_window` / `f4_after_accept`）：注入一行在线好友 → 加号 active；改「匹配中 / 对局中 / 离线」→ 加号隐藏；换回在线 → 回来；点加号 → 冷却 10.00s、贴图=空板、数字显示「10」、再点无效（10.00 → 10.00）、**`Panel_Room 开着 = True`**（自动进房间那条）；小窗 `y 260 → -10` 滑到位（标题 / 名字 / 两键 / 头像井逐项）；拒绝 → 队列空 + 收窗 + 回信实测写出 `76561198000000002|decline|1790509701`；同意 → `GuestMode False→True`、`IsHost = False`、窗口当场收掉；把剩余冷却压到 0.35s → **冷却走完加号自己回来**（贴图 = 常态加号 / 数字藏起）。失败合计 0。
- **`stage47b_invite_fix.txt` + `stage47b_shots/`**（`f1_placeholder` / `f2_avatar_arrived`）：导入设置逐项 + `Window 360×240` / `Plate` 拉伸铺满 / 底板吃的就是 `Invite_Plate 1080×720`；头像那条——摆窗时那一格是 Steam 默认像，把一张 64×64 假头像写进缓存后**小窗自己换成了新图的圆裁**（`now == CircleCrop(_fake)`，窗口没关）。失败合计 0。
- **两个执行器跑完都已自删**（`Assets/_Game/Editor/Stage47Invite.cs`、`Stage47bInviteFix.cs` 均已不在仓库）。
- **注**：`f4_after_accept` 里那行「进不去这间房（可能刚被解散 / 已满）」是**测试环境的必然** —— 假 lobby ID 在离线环境 `JoinLobby` 必失败；真机走 Steam 那条路。另外 stage47 执行器第二版第一跑（19:51）自己把报告刷到 785KB（相位没置 `_acted`，被每帧重跑）—— 那是执行器的坑，不是产品的，已在第二版堵上（`WrapUp` + 每相位自置 `_acted`）。

## 二十四次修正（2026-09-27）：邀请两块一起压扁 —— 好友行徽章 60×60 → 78×48、「收到邀请」小窗 360×240 → 420×192

**用户原话**：「要做的更像长方形，更"扁"一点」+「上方的邀请滑出也应变"扁"」。两块**一起改**（下面所有数字都**覆盖**「二十三次修正」那一节里的同名数字）。

**贴图（两块各出一条新口径，配方来源写在表里）**

| 件 | 脚本 | 旧 → 新 |
|---|---|---|
| `Icon_InvitePlus.png` / `…Hover.png` / `…Cool.png` | `Tools/cardframe/LobbyInvitePlusV1.ps1`（预览 `Tools/cardframe/preview/lobby-invite-plus.png`） | 256×256 里 **168 方** → **234×144 = 屏幕 78×48（1.625:1）** |
| `invite-v1/Invite_Plate.png` | `Tools/cardframe/InvitePanelV1.ps1`（预览 `Tools/cardframe/preview/invite-panel.png`） | 1080×720（= 360×240）→ **1260×576 = 屏幕 420×192（2.19:1）** |

- **徽章换了整套配方**：从「单件自画」并到 `Tools/cardframe/LobbyRoomChipV1.ps1` 那一套 —— 3 倍出图 / 外墨边 9 / 等比内缩金线 3+18 / 左上亮楔 / 圆角 24 / 金线 α150。于是它与**「踢出 / 开始游戏」子背景**（`LobbyChip_Kick` 96×48）**同高、同语言**，不是另一路货。加号臂 33 / 线宽 10（旧 168 方那版的等比放大）。
- **小窗底板配方没动**（还是 `match-wait-v1/MatchWait_Plate` 那一套），只换尺寸；**2.19:1 是往同族「匹配中」小窗的 2.37:1 靠**（`MatchWait_Plate` = 360×152）—— 两块都在屏幕顶中，不靠齐看着是两代东西。

**版式（`LobbyUIBuilder` 常量段；窗矮了、格子扁了，里面每一样都得跟着重排）**

| 位置 | 旧 | 新 |
|---|---|---|
| 徽章那一格 | 60×60，贴行右端 | **78×48**，贴行右端（右沿让 4），行内垂直居中（行高 84，上下各 18） |
| 名字行让位 `InviteNameTrim` | 84（60+4+20） | **94**（78+4+12）⇒ `Text_Name` 宽 **219** |
| 倒计时字号 | 26 | **30**（格子变矮反而加大：数字要顶满 48 高） |
| 小窗 | 360×240（3:2） | **420×192**（2.19:1） |
| 标题 | y −?? / 字号 30 | y **−14** / 高 **34** / 字号 **26** |
| 头像环 / 井 | 72 / 56，横写 48 | **64 / 50**，**`InviteRingX = 70`**（原来硬写 48；井按环居中套） |
| 名字 | x 136 / 字号 28 | x **150** / 字号 **26**（纵向跟环对齐） |
| 两键 | y −168，x 56 / 208 | y **−128**，x **102 / 222**（子背景仍 96×48，右沿 318 ≤ 窗宽−8） |
| 提示行让位 `toastDrop` | 写死 250 | **`InviteWinH + 10 = 202`**（原来只有字段默认值，这次改成建小窗时按窗高算；窗高再变它会自己跟） |
| `hiddenY`（收窗起点） | 260 | **`InviteWinH + 20 = 212`** |

**导入白名单**：`TextureImportSettingsGuard` 的 `lobby-ui-v1` 两处分支各加一条 `Icon_InvitePlus*` —— 234×144 **不是 2 的幂**且 >128，会走守卫的 `IsCandidate` 被吸成 256，不加白名单整块徽章会被拉坏。

**自证**

- **`stage48b_flat.txt`**（编辑态逐项，失败 2 项都在 Play 相位，见下）：三张图标 **234×144 / npot=None / alpha=1**；`Invite_Plate` **1260×576 / npot=None / alpha=1**；徽章那一格 **78×48 @(327,−18)**、右沿 405（行宽 409）、sibling 5/6、贴的就是 234×144、倒计时字号 30；`Text_Name` 219 且右沿 307 < 格左沿 327；小窗 **420×192** 静止位 y=−10、`Plate` 拉伸铺满、标题/环 64/井 50/名字/两键逐项、`toastDrop = 202`。
- **`stage48e_row_shot2.txt` + `stage48_shots/f1b_row_plus.png` / `f2c_row_countdown.png`**（运行态，失败合计 **0**）：注入两行「在线」好友 → 徽章 **78×48（1.63:1）贴 234×144**；给第一行起 10s 冷却 → **同一格换空板 + 金数字「10」**，第二行仍是加号。
- **`stage48_shots/f3_flat_window.png`**（运行态）：扁版小窗贴屏幕顶（y −10 / 420×192），标题 + 头像行 + 两键都在。
- **三个执行器跑完都已自删**（`Stage48bInviteFlat.cs` / `Stage48cInviteRowShot.cs` / `Stage48eInviteRowShot2.cs` 均已不在仓库）。

**排错记录（三支才拍成，根因值得记住）**：`stage48b` 的 Play 相位**找不到 `LobbyFriendListUI`** —— 好友侧边栏没开时那个物体整块 inactive，`FindObjectOfType` 是找不到的（编辑态几何不受影响，所以那 2 项失败纯粹是取景问题）；`stage48c` 冷却那一拍**被外部点击把侧边栏关了**（截到的是房间面板）；`stage48d` 三个 Instance 全空 —— **根因：编译触发的域重载会把 `EditorApplication.isPlaying = true` 推迟**，相位链于是**在编辑态空转**（编辑态下 `FriendListService.Instance` 这类运行时单例必然是 null）。`stage48e` 改成**条件驱动**：等到 `EditorApplication.isPlaying` **且** `FriendListService.Instance != null` 才走下一步，且 hold 计数与 `_acted` 一起存 `EditorPrefs`（域重载不丢），一次通过。


## 二十五次修正（2026-09-27）：邀请小窗 v3 —— 去掉标题栏、再压一档、名字居中到右 1/3

**用户原话**：「不要收到邀请了，把留出的空间再压更扁，同时文字调整至以右 1/3 处为中心对齐」（覆盖上一条里的同名数字）。

- **标题栏整条删掉**：`LobbyUIBuilder.BuildInvitePanelMenu` 里那段 `Text_Title「收到邀请」`（金 26 号）连同 `InviteTitleY / InviteTitleH / InviteTitleFont` 三个常量一起去掉 —— 重建是**先删旧的 `Panel_Invite` 再重造**，所以老场景里那个 `Text_Title` 子物体不会残留。`LobbyInvitePanel.titleText` 字段留着（它是 null 会走空判断，不再赋值）。
- **窗高 192 → 144**：① 标题那一栏省下 34 + 8；② 头像行上沿 −56 → **−12**（上留 12）；③ 两键 y −128 → **−82**（头像行底 −76 与两键顶 −82 之间留 6，底留 14）。合计 **12 + 64 + 6 + 48 + 14 = 144**，即 **420×144 = 2.92:1**（上一版 2.19，再上一版 3:2）。`hiddenY = 窗高 + 20 = 164`、`toastDrop = 窗高 + 10 = 154` —— 两个都已改成跟着窗高算，这次没动它们。
- **底板重出**：`Invite_Plate.png` 1260×576 → **1260×432**（`Tools/cardframe/InvitePanelV1.ps1`，配方一字未改，只换尺寸）；`TextureImportSettingsGuard`（本目录在 `NoNpotScaleFolders` 里）那条注释也跟着改成 1260×432。
- **名字（文字）居中到右 1/3**：名栏从 `x 150 宽 200 左对齐` 改成 **`x 148 宽 264 居中`** —— 148..412 的正中正好是 **280 = 420 的右 1/3 分界**（右边留 8 与窗内金线对齐）。字号 26、纵向仍与头像环（64）同心（名栏盒 40 高，中心 −44）。

**自证：`stage49_invite_flat_v3.txt` + `stage48_shots/f4_invite_flat_v3.png`**（失败合计 **0**）

- 编辑态：底板 `npotScale=None / alphaIsTransparency=True / 源尺寸 1260x432`；重建 + 存盘后 **窗 420×144（2.92:1）**、**没有 `Text_Title`**、`hiddenY=164`、`toastDrop=154`、`Plate` 吃的就是 1260×432、头像环 64 @(70,−12)、两键行 y=−82。
- 文字：名栏锚左上（不是拉伸）、`264x40 @148,−24` → **中心 (280,−44)**、`alignment=Center`、奶油色。
- 运行态（进 Play 摆一条假邀请）：**窗仍是 420×144**、滑到位 y=−10、窗口开着，名栏中心仍是 280。执行器跑完已自删。

## 二十六次修正（2026-09-27）：好友侧边栏表头那颗「+」+「好友详情」全屏子弹窗（占位壳）

**用户原话**：「在好友右边靠近右边框的地方加上一个加号 ui 用于打开好友详情全屏（类似于战斗，但不要做卡牌的，我后续给你说怎么做）」。

三件事：① 出图；② 表头加那颗「+」（悬停 / 点击）；③ 壳照抄 `Panel_Battle`，内容留空等用户后续说明。

### ① 出图 `Tools/cardframe/LobbyFriendPlusV1.ps1`

- **配方逐行照抄通用关闭叉 `Icon_Close`**（`Tools/cardframe/LobbyUIv1.ps1` 的 `New-CloseGlyph`，426 行）：圆角方印 `44,44,168,168`（圆角 20）+ 内框 `58,58,140,140`（圆角 14）+ 笔画 92..164、线宽 12、圆头。**只把叉那两笔改成加号**（横 `92,128 -> 164,128`、竖 `128,92 -> 128,164`）。
- 悬停同全族配方：石面提亮（顶 `Mix(BAR_T,HILITE,0.12)` / 底 `Mix(BAR_B,BAR_T,0.40)`）、金 `GOLD -> GOLD_L` 且 α +40。
- 产物 `Icon_FriendPlus.png` / `Icon_FriendPlusHover.png`，各 **256×256**（2 的幂，所以**不吃** npot 缩放；带硬 alpha 边，进的是守卫的 `alphaIsTransparency` 白名单）。

### ② 表头那颗「+」

| 项 | 值 |
|---|---|
| 名字 / 父节点 | `Icon_FriendPlus` / `Panel_Friends/Body` |
| 位置 | 锚左上 + 轴左上，`(397, -58)` = `FriendsPanelW 465 - FriendPlusRight 24 - FriendPlusSize 44`；与「好友」标题**同一行**（标题盒 -58..-102） |
| 尺寸 | **44×44 屏幕 px**（贴图 256；方印占 168/256 ⇒ 可见印面 **28.9**，比关闭叉的 39.4 轻一档 —— 它不是全屏弹窗的关闭件） |
| 悬停 / 点击 | `LobbyIconHover`（常态 / 悬停两张贴图）+ 新字段 `subPanel` → `Toggle()` |
| 不挂 `Button` | **同物体上两个 `IPointerClickHandler` 会被各触发一次**（`Toggle` 再 `Open` 正好互相抵消 = 点了没反应），所以入口一律只留 `LobbyIconHover` 一个处理器 |
| 标题让路 | `Text_Title.raycastTarget = false` —— 「+」的盒右端压在标题盒（200 宽）上，不关掉会被标题吃掉点击；「+」本身也 `SetAsLastSibling` 排在标题之后 |

常量都在 `BuildFriendsPanelMenu` 顶上：`FriendPlusName` / `FriendPlusSize 44` / `FriendPlusRight 24` / `FriendPlusY -58` / `FriendDetailPanelName`。

### ③ 「好友详情」全屏子弹窗（占位壳）

- `Panel_FriendDetail`，由 `LobbyUIBuilder.BuildFriendDetailSubPanelMenu`（菜单「Tools/异界/大厅：生成「好友详情」子全屏弹窗（好友表头 + 的落点 · 占位）」）出；**壳与 `Panel_Battle` 完全一致**：`BuildSubPanel(..., withHeader:false)` = 通用背景 `CommonBack_A_clean` + 右上角通用关闭叉 `Icon_Close` @ `(-168,-66)` / 60×60 + 内容限位框 `Body_Content`。
- 不出标题与提示（内容等用户后续说明）；`hideOnOpen` 自动填「无底衬 HUD 图标」那 5 个（好友 / 商城 / 活动 / 教程 / 邮件）—— 与战斗面板同口径，`Icon_Friend` 也在内，所以详情页开着时左上那颗好友图标是藏的（要它常驻就说一声，改成 `FindNoBackdropHudIcons(hud, "Icon_Friend")` 即可）。
- 打开路径：`LobbyIconHover.OnPointerClick` → `subPanel.Toggle()` → `LobbySubPanel.Open()`，**里面本来就会 `LobbyFriendPanel.Instance.Close()`** —— 正好等于「点 + 时侧边栏自己收回去」，没有额外代码。
- 接线 `WireFriendDetailEntry(canvas)`：两个菜单**各自都会调**（重建任一边都会把旧引用变成 null，与「战斗」入口同一条坑）；`+` 由 `BuildFriendsPanelMenu` 建，所以**先跑详情菜单、后跑好友侧边栏菜单**的顺序最省事（`BuildFriendDetailSubPanelMenu` 自己也会回头把 `+` 接上）。

### ④ 导入守卫

`TextureImportSettingsGuard.NeedsAlphaIsTransparency` 的 `lobby-ui-v1` 分支加 `Icon_FriendPlus`（`StartsWith` 覆盖常态 + Hover 两张）。**不加这条**，256 缩到 44（5.8 倍）时圆角与金线边缘会发黑。

**自证：`stage50_friend_detail.txt` + `stage48_shots/f5_friend_plus.png` / `f6_friend_detail.png`**（失败合计 **0**）

- 编辑态：两张贴图 **256×256 / alpha=True / Bilinear**；「+」**44×44 @(397,-58)**、锚轴都左上、有 `LobbyIconHover`、两张贴图都在、`subPanel -> Panel_FriendDetail`、**无 Button**、sibling 1 > 标题 0、标题 `raycastTarget=False`；壳：`LobbySubPanel`、背景 `CommonBack_A_clean`、关闭叉 `Icon_Close` @(-168,-66) 60×60、无 `Text_Title`/`Text_Hint`、`Body_Content` 在、`hideOnOpen` 5 个、场景里存成 active。
- 运行态：开侧边栏 → **IsOpen**、板身 x=0、「+」仍接着 `subPanel` → 截图 **f5**；调 `OnPointerClick` → **弹窗已打开**、**侧边栏已自动收回**（`IsOpen=False`）、弹窗铺的还是通用背景 → 截图 **f6**。执行器跑完已自删（`Stage50FriendDetail.cs` 不在仓库）。

**下一步（等用户）**：详情页内容（头像 / 名称 / 战绩 / 加好友 / 邀请…）—— 用户明说「不要做卡牌的，我后续给你说怎么做」。
## 二十七次修正（2026-09-27）：好友详情左侧四个 tab + 两条金线 + 右侧一块独立大格子

**用户原话**：「左边有四个格子类似于大厅右下角那四个不过更扁平，分别是好友列表，添加好友，申请列表，黑名单，初始进入位于好友列表格子，在右上角的叉下面划一道长金线横跨左右分割一下，然后左边也有从上到下的一个金线将四个从上到下的格子与右边空间分开得到右边一个独立的大格子，四个格子每个格子的内容都将不一样」。

四格自上而下 = **好友列表 / 添加好友 / 申请列表 / 黑名单**；右侧那块独立大格子里放**四块内容**，同一时刻只开当前选中那一块。

### ① 出图 `Tools/cardframe/FriendDetailTabsV1.ps1`

**配方照抄大厅右下角入口条**（`LobbyUIv1.ps1` 的 `New-LobbyEntryPlate`，`CornerBody*` 那套）：平底竖渐变 + 左上亮楔 + 外墨边 9 + 等比内缩金线（inset 24 / 线宽 3）+ 圆角 26。

与右下角那套**只有两条差别**：

| | 右下角入口条 | 本套四个 tab |
|---|---|---|
| 比例 | 300×120 = **2.5:1** | 340×104 = **3.27:1**（用户要的「更扁平」） |
| 徽记 | **烤进板里**（一个入口一张板） | **不烤进板**：徽记另出 256 图标，运行时只改 alpha |

第二条的理由：四格的**板没有格与格的区别**，区别只在徽记与文字 —— 徽记不烤进板，三态就只需要**三张板、四格共用**，而不是 3×4 = 12 张。

- 板（`1068×360` 贴图 = 屏幕 `340×104`，板身之外每边 8 透明边；`PAD 24` 贴图 px / 3）：
  `LobbyFriendTab.png`（常态）/ `LobbyFriendTabHover.png`（石面提亮 + 金 `GOLD→GOLD_L`）/ `LobbyFriendTabOn.png`（石面 +8 + 金线 α240 + **左侧一条 9px 金竖条**）。
- 徽记（`256×256` 金线稿、透明底）：`Icon_FriendTabList`（列表）/ `Icon_FriendTabAdd`（人影 + 加号）/ `Icon_FriendTabRequest`（人影 + 来向箭头）/ `Icon_FriendTabBlock`（人影 + 斜杠）。
- 预览：`Tools/cardframe/preview/lobby-friend-tabs.png`（三态板 + 四枚徽记）、`lobby-friend-detail-mock.png`（1920×1080 真坐标拼版）。

**踩过的坑**：PowerShell **变量名大小写不敏感** —— 参数 `$tone` 与脚本级 `$TONE` 撞车（`$TONE['n']` 被解析成 `$tone` 索引）；参数名改 `$toneKey` 并加 null 断言。

### ② 两个运行时脚本

| 脚本 | 管什么 |
|---|---|
| `Scripts/UI/Lobby/LobbyFriendTab.cs` | **一格自己的皮**：`index` / `plate` / `emblem` / `label` / `tabs` / 三态贴图 / 三档 `*EmblemAlpha`（0.50 / 0.82 / 1.00）/ `normalColor` 奶油 / `goldColor` #E4CB84；`SetOn` / `SetHover` / `Apply`；`IsOn`；三个 `IPointer*` 接口 |
| `Scripts/UI/Lobby/LobbyFriendDetailTabs.cs` | **控制器**：`tabs[]` / `contents[]` / `startIndex` / `Current`；`Select(index)` 切选中 + 只开对应内容；`OnEnable` 里 `Select(startIndex)` |

- 格上**不挂 `Button`** —— 同物体两个 `IPointerClickHandler` 会各触发一次（与好友表头那颗「+」同一条坑）。`raycast` 目标是子物体 `Plate` 那张 `RawImage`（文字与徽记的 `raycastTarget` 关掉），落在板 / 字 / 徽记上都会沿父级冒泡到这一格。
- **初始态每次打开都重置**：控制器挂在面板根上，面板 `closeOnStart` 关掉时整棵不激活，再 `Open` 就会走一遍 `OnEnable`。两处 `OnEnable` 都有 `if (!Application.isPlaying) return;` —— 编辑态由构建脚本摆好初始态，组件别去改场景。

### ③ 几何（`LobbyUIBuilder.BuildFriendDetailContent`）

一条轴线走通全篇：**所有边都是「屏幕像素」**，锚点 + offset，不写死 1920×1080。

| 件 | 位置 |
|---|---|
| `Line_DividerTop` 横金线 | 距屏幕上沿 **156..158**（关闭叉底沿 126 之下）、横跨 `64 .. 屏幕右沿-64` |
| `Line_DividerLeft` 竖金线 | 左沿 **428**、宽 2、从横线一路到下沿 64 |
| `Rail_Tabs` 左侧竖栏 | x `64..404` |
| 四格 | 竖栏里等分：格心锚 `y = 1-(i+0.5)/4`（1/8 · 3/8 · 5/8 · 7/8），板 **340×104**、板心 x = 170 |
| 格内 | `Plate` **356×120** @ `(-8,+8)`（贴图外框，板身正好 340×104 居中）/ `Emblem` 64 @ 板宽 75.5% / 板高 50% / `Label` 距板身左沿 58、字号 28 |
| `Body_Detail` 右侧大格子 | x `428 .. 屏幕右沿-64`、上沿 −156、下沿 64 |

贴图外框比板身每边多 8 = `PAD 24 / 3`。竖向 `/4` 等分 + 拉伸锚 = 换分辨率不会散。

### ④ ★ 排错记录（这一版真正的坑，两条）

**（a）两条金线被拉成「一整块金板」** —— 两条线原本都写成「**四边全拉伸 + offset**」。金线是**没贴图的纯色方块**（`Image.sprite == null`，铺多大就画多大，本套既有做法同 `Hairline`），而沿**拉伸**那条轴的边长 = 父级边长 + `offsetMax - offsetMin`：

- 横线 `offsetMin (64,154)` / `offsetMax (-64,-156)` → **1792×770**，屏幕上是一大块横跨左右的金色大板；
- 竖线 `offsetMin (428,64)` / `offsetMax (430,-156)` → **1921 宽**，同样是整块金板。

**改法**：`StretchRect` 加一个**带锚点**的重载，两条线改成**一边拉伸、一边钉死** —— 横线锚上沿（`anchorMin (0,1)` / `anchorMax (1,1)`，x 拉伸、y 钉死），竖线锚左沿（`(0,0)` / `(0,1)`，x 钉死、y 拉伸）。钉死那条轴的两个 offset 就是**屏幕上沿 / 左沿的像素距离**。改后横线 `2×1791.28`、竖线 `2×860`（面板 1919.28×1080）。

> 教训：**拉伸锚 + offset 的「厚度」不是 `offsetMax - offsetMin`，而是「父级边长 + 那个差」**。要一条 2px 的线，必须把那条轴钉死。`Stage53` 的门（`rect.height == 2` / `rect.width == 2`）就是为这条留的。

**（b）四格漏回指 → 点第 4 格毫无反应** —— `LobbyFriendTab.tabs` 建的时候漏了赋值：点下去只换了自己的皮，`Select` 没被调到，`Current` 仍是 0、内容也没换。**已补 `for` 循环回指 + `EditorUtility.SetDirty`**，并加了一条编辑态断言「`tabs` 回指的就是本面板的控制器」。

**（c）执行器的坑**：截图那支相位**自己要有「等落盘」的门**（`ScreenCapture.CaptureScreenshot` 是帧末落盘）—— `Stage51` 漏了这个门，报「f8 缺失」是**假失败**（其实只是还没写盘）。

### ⑤ 导入守卫

`TextureImportSettingsGuard` 两处：`NoNpotScaleFolders` 的 `lobby-ui-v1` 分支加 `LobbyFriendTab`（**1068×360 不是 2 的幂**，不加会被吸成 1024×512）；`NeedsAlphaIsTransparency` 加 `LobbyFriendTab` + `Icon_FriendTab`。

### ⑥ 自证

- **`stage52_friend_tabs.txt`**（失败合计 **0**）：七张贴图导入设置（板 `1068x360 / npot=None / alpha=True`、图标 `256` alpha=True）；横竖两条线的 offset；四格的 340×104 + 格心锚 + `Plate 356×120` + `Emblem 64 @256.7,-52` + `Label @58 字号 28`；`Body_Detail` 边界；四块内容标题；控制器 `tabs/contents=4` / `startIndex=0` / 第 0 格 = `LobbyFriendTabOn`；关闭叉压最后；`hideOnOpen` 5 个。运行态：弹窗打开 / 初始进入 = 好友列表 / 只一块内容可见 / 四格回指都在。
- **`stage53_friend_tabs_lines.txt`**（失败 **2** —— 就是上面 (a)，尺寸门抓到「2px 的金线被拉成 1082 高 / 1921 宽」）。
- **`stage54_friend_tabs_lines.txt`**（失败合计 **0**）+ **`stage48_shots/f9b_friend_tabs_lines.png`**（初始 = 好友列表）/ **`f10b_friend_tabs_block.png`**（点第 4 格黑名单后）：锚点、`rect.height == 2`、`rect.width == 屏幕宽-128`、竖线 `2 × (屏幕高-220)`、层级 `Bg < 横线 < 竖线 < 竖栏 < 右侧大格子`、运行态两条线仍是 2px。
- 三支执行器跑完**都已自删**（`Stage52FriendTabs.cs` / `Stage53FriendTabLines.cs` / `Stage54FriendTabLines.cs` 均已不在仓库）。

**下一步（等用户）**：四格各自的内容（用户：「四个格子每个格子的内容都将不一样」）—— 现在每块只有「标题 + 占位 · 内容待接入」。

## 二十八次修正（2026-09-27）：好友列表那一格的内容（行 + 三格动作 + 滚动 + n/50）

**用户原话**：「好友列表（后续每个独立的玩家好友基本上都是按照这样）（上限50个好友），允许滑动，每个好友有个独立的
长矩形子背景，从左到右分别是头像，名称，id（这个字体小一点），然后中间可以留空，右边分别是当前状态（在线/离线
什么的），拉黑，删除，（仅在在线状态下）邀请，拉黑删除和邀请都是小ui图案代替文字，每个好友之间是有一点间隔，和底框
也有间隔，右下角是以类似 23/50 小字这种形式展示好友数量」。追加一句（指令性）：**「仅在对方空闲在线才会出现邀请ui」**。

### ① 出图（`Tools/cardframe/FriendDetailRowV1.ps1`）

| 件 | 尺寸 | 说明 |
|---|---|---|
| `LobbyFriendRow.png` | **1376×112** | 行底板（可见板身 1360×96 + 每边 8 透明边） |
| `Icon_FriendActBlock.png` / `…Hover.png` | 256×256 | 拉黑 = **禁止符**（圆 + 斜杠） |
| `Icon_FriendActDelete.png` / `…Hover.png` | 256×256 | 删除 = 垃圾桶 |
| （邀请） | — | **复用已有 `Icon_FriendPlus.png`** —— 与好友表头那颗「+」同源，不另出一套 |

- 配方照抄入口板：平底竖渐变 + 左上亮楔 + 外墨边 9 + 等比内缩金线（inset 24 / 线宽 3）+ 圆角 30；徽章照抄 `Icon_FriendPlus` 的方印配方。
- **底板存成 1:1（不是全族那个 ×3）** —— 1360×3 = 4080 超过导入器默认 `maxTextureSize 2048` 会**被静默缩掉**。
  做法：脚本按 ×3 画、再高质量降采样到 1:1（`Save-BmpDown`）。**场景那边 `sizeDelta` 直接 = 贴图尺寸，不除 3。**
- 形体决策：44px 显示尺寸下「人影 + 斜杠」会糊成一团（截图实测），拉黑改成**禁止符**；并把「黑名单」tab 那枚徽记**也改成同一个形体**。

### ② 版式（`LobbyUIBuilder` 的 `Fd*` 常量段 · 屏幕 px）

一条轴线：**行定宽 1360**（不是拉伸）—— 行底板是按尺寸出的图，金细线与圆角不等比拉伸就会变形；
换分辨率时行不跟着拉长，与底框的间隔自然变大（用户要的「和底框也有间隔」）。

| 件 | 位置 / 尺寸 |
|---|---|
| `Friends_List`（ScrollRect） | 左/右各让 **32**、上让 **124**（躲开标题）、下让 **60**（给右下角计数） |
| 行 `RowTemplate` | **1360×96**，行距 **12**，存成 **inactive**（只给运行时克隆） |
| 行底板 `Plate` | **1376×112** @ `(-8, +8)`，贴 `LobbyFriendRow.png`，**不吃点击**（拖拽交给 Viewport 那张 α=0 的图） |
| 头像环 / 井 | 环 **64** @ `(14, -16)`；井里头像 **50** @ `(21, -23)`（口径 = `LobbyAvatarRing` 264 里井半径 102） |
| 名称 | x **96**、宽 **260**、字号 **26**、左对齐、**Ellipsis**（长名截断，不糊到 id 上） |
| id（小字） | x **364**、宽 **336**、字号 **20**、钢灰 `#8EA2B4` α190 |
| 状态 | 右沿 **1172**、宽 **260**、字号 **22**、**右对齐** |
| 三格动作 | **44×44** @ y **−26**，x = **1184 / 1240 / 1296**（间距 12、右让 20） |
| 右下角计数 | 锚右下、`(-32, 18)`、字号 **22**、右对齐、钢灰 |

**三列文字盒一律 40 高**（不是「刚好放下一行」）—— 见下面 ④ (a)，这是硬要求。

### ③ 运行时（三支新文件）

| 文件 | 干什么 |
|---|---|
| `Scripts/UI/Lobby/FriendRowAction.cs` | 一格动作：悬停换贴图、点击转给本行。**不挂 Button** —— 同物体两个 `IPointerClickHandler` 会各触发一次（与「+」、左侧四 tab 同一条坑） |
| `Scripts/UI/Lobby/FriendDetailRowUI.cs` | 一行的皮：头像 / 名称 / id / 状态 / 邀请格；`Bind(FriendEntry)`；头像先铺灰再等 Steam 到货（0.5s 重试 / 20s 放弃） |
| `Scripts/UI/Lobby/LobbyFriendDetailListUI.cs` | Content 上的名单：克隆行、高度、计数、超 50 截断、只在**行数变**时回顶 |

- **邀请口径**：`e.Presence == FriendPresence.Online && e.SteamId != 0` —— `Online` 就是「没在匹配、也没在对局」那一档；
  匹配中 / 对局中 / 离线都不出。三格位置**固定**，邀请那格只是 `SetActive(false)` 藏起来，所以拉黑 / 删除不会跳位。
- **删除 = 墓碑**：好友表是从「Steam 好友 + 玩过本游戏」**推**出来的、每 20 秒重扫。直接 `FriendStore.Remove`
  下一轮他就回来了 —— 所以 `FriendEntry` 加 `removed` 墓碑，`FriendListService.Refresh()` 两条路都过滤，
  `FriendStore.AddManual` 遇到已存在且带墓碑的把墓碑清掉（= 重新加回来）。
- **拉黑还是占位**（口径待用户定：只屏蔽名单，还是连匹配也不碰他）。

### ④ ★ 排错记录（这一版真正的坑，三条）

**（a）名字列一个字都不画 —— 盒子矮于一行行高 + Ellipsis 溢出。**
`FdRowNameH` 原本按「字号 26 + 一点余量」取 **34**，而 NotoSerifCJK 26 号的实测 **`preferredHeight = 37.4`**。
`overflowMode = Ellipsis` 在**连一行都放不下**时不是「截断成省略号」，而是**一个字符都不画**（实测 `textInfo.characterCount = 0`）；
同一行的 id（`Overflow` 模式、行高 28.7 < 34）反而正常 —— 所以现象是「id 有、名字没有」，很像赋值漏了。
**改法**：三列文字盒一律 **40** 高（名字 / id / 状态同高，顺带把三列的光学中线对齐）。
**教训**：`Ellipsis` 的盒子必须容得下**整行行高**，不是容得下「字号」；而且**只断言 `.text` 有值查不出这种漏**
（`.text` 是对的、`characterCount` 才是 0）—— 门要查 `characterCount`。

**（b）导入守卫的尺寸门把 npot / alpha 一起漏掉了。**
`LobbyFriendRow` 是 **1376×112**，高 112 < `MinSizeForMipmaps`（128），而 `OnPreprocessTexture` 的尺寸门
在 `ApplyTo` **之前** `return` —— 于是「不许 Unity 缩放」（npot=None）与「alpha 扩散」两条**一起没套上**
（重导后实测仍是 `ToNearest` / `alphaIsTransparency = false`，行底板会被吸成 1024×128）。
**改法**：尺寸门只管「双线性 + mipmap」那一段（小控件不做缩小采样），
把与尺寸无关的两条拆成 `ApplyFilterPart` / `ApplyScalePart`，**后者永远执行**；
`NeedsFix` 加 `sized` 参数，菜单体检与报告两条路同样改。这是**守卫本身的通用修正**，不止这一件贴图受益。

**（c）执行器的坑（复述）**：截图那支相位**自己要有「等落盘」的门**（`ScreenCapture.CaptureScreenshot` 是帧末落盘）；
另外 **`GameObject.Find` 找不到 `closeOnStart` 关掉的 `Panel_FriendDetail`** —— 要用
`Resources.FindObjectsOfTypeAll<LobbySubPanel>()` 找（含 inactive）。

### ⑤ 导入守卫

`TextureImportSettingsGuard`：`NoNpotScaleFolders` 的 `lobby-ui-v1` 分支加 `LobbyFriendRow`
（1376×112 不是 2 的幂）；`NeedsAlphaIsTransparency` 加 `LobbyFriendRow` + `Icon_FriendAct`；
并按 ④ (b) 把「与尺寸无关的两条」从尺寸门里拆出来。

### ⑥ 自证

- **`stage55_friend_row_list.txt`**（**失败 9** —— 就是 ④ (a)(b)：8 条 npot/alpha + 1 条名字列空拍，另有 3 条是我自己的断言写错）。
- **`stage57_name_probe.txt`**（探针，定位 ④ (a)）：`Text_Name` 的 `text=[在线甲]` 而 `chars=0`、`preferredH=37.37 > rect 34`；
  同一行 `Text_Id`（`overflow=Overflow`）`chars=20` —— 一眼定性。
- **`stage58_friend_row_list.txt`**（**失败合计 0**，OK 189）+ **`stage48_shots/f11_friend_row_list.png`**：
  贴图导入（行底板 `1376x112 / npot=None / alpha=True`、两枚徽章 256 / alpha=True）；
  列表让边 64 / 离底 60 / 上沿 −124；行 1360×96 / 底板 1376×112 @(−8,8) / 环 64 @(14,−16) / 井 50 @(21,−23) /
  名字 @96 字号 26 / id @364 字号 20 / 状态右沿 1172 字号 22 / 三格 44 @y−26 x=1184·1240·1296 / 计数 @(−32,18)；
  三格都挂 `FriendRowAction`、**都没多挂 Button**、都回指本行、plate 不吃点击；
  运行态注入 5 行（在线 / 对局中 / 在线 / 匹配中 / 离线）：行数 5、行距 12、Content 高 528、
  计数 `5/50`、**两行「在线」才有邀请格**、状态字与颜色逐条、
  **三列文字 `characterCount ≥ 期望` 且盒子容得下一行**（即 ④ (a) 的回归门）。
- 三支执行器跑完**都已自删**（`Stage55FriendRowList.cs` / `Stage57NameProbe.cs` / `Stage58FriendRowList.cs` 均已不在仓库）。

**下一步（等用户）**：另外三格（添加好友 / 申请列表 / 黑名单）的内容，以及「拉黑」的口径。

## 二十九次修正（2026-09-27）：确认删除 / 拉黑 —— 长条弹窗

**用户原话**：「删除好友和拉黑好友都有一个长子弹窗，上面是确认删除/拉黑（金色的好友名称），
下面是有子背景的确认和取消」。

### ① 出图（`Tools/cardframe/LobbyConfirmPlateV1.ps1`）

| 件 | 尺寸 | 说明 |
|---|---|---|
| `LobbyConfirmPlate.png` | **760×200** = 3.8:1 | 窗口底板（固定尺寸，不切片） |

- 配方与 `Invite_Plate` / `MatchWait_Plate` 同一支笔：平底竖渐变 + 左上亮楔 + 外墨边 9（屏幕 3）+ 等比内缩金线
  （inset 24 / 线宽 3 / α150）+ 圆角 30（屏幕 10）。
- **3.8:1 比「收到邀请」小窗（420×144 = 2.92:1）再扁一档** —— 这一块只有「一行标题 + 一排两个键」，没有头像行；
  用户要的就是「长条」。
- **存成 1:1**（不是全族那个 ×3）：760×3 = 2280 超过导入器默认 `maxTextureSize 2048` 会被静默缩掉。
  做法同 `LobbyFriendRow`：脚本按 ×3 画、再高质量降采样到屏幕尺寸（`Save-Down`）。
  **场景那边 `sizeDelta` 直接 = 贴图尺寸，不除 3。**
- 两个键**不另出图**：复用 `LobbyChip_Kick` / `…Hover`（与「踢出 / 开始游戏 / 加入 / 同意 / 拒绝」同规格 96×48 / 字 30）。

### ② 版式（`LobbyUIBuilder` 的 `Cf*` 常量段 · 屏幕 px）

| 件 | 位置 / 尺寸 |
|---|---|
| `Window` | **760×200**，锚屏幕正中 |
| `Plate` | 拉伸铺满窗（贴图存 1:1，所以尺寸就是 760×200），**吃点击**（别点穿到底下的名单） |
| `Text_Title` | 左让 **40**、上让 **36**、盒 **680×56**、字号 **34**、居中一行、**Ellipsis**、富文本 |
| `Chips` 行 | 上沿 **124**、宽 = 窗宽、高 **48** |
| `Btn_Confirm` / `Btn_Cancel` | **96×48** @ x = **272 / 392**（间隔 24，整排 272..488 居中于窗心 380）、字 **30** |

**标题是「一条富文本」而不是两段拼位置**：`确认删除 <color=#E4CB84>在线甲</color>` ——
动词走 `LobbyConfirmDialog.verbColor` 奶油 `#F0E8D2`，好友名走 `whoColor` **亮金 `#E4CB84`**；
名字长度不定，靠 TMP 自己居中，比摆两个文本块稳。

`CfTitleH` 取 **56**（不是「34 号看着差不多」的 48）—— 见 ④ (a)。

### ③ 运行时与层级

- 组件 `Scripts/UI/Lobby/LobbyConfirmDialog.cs`：`Ask(verb, who, onConfirm)` / `Confirm()` / `Cancel()` / `Hide()`；
  点「确认」才执行回调，点「取消」直接丢。
- 面板 `Panel_Confirm` **挂 `Layer_Hud_v1`**（与 `Panel_Invite` / `Panel_MatchWait` / `Panel_MatchConfirm` 同级）——
  好友详情是**全屏子弹窗**（在 `Layer_Sub_v1`），确认窗必须压在它之上。
  根常驻 active（`Awake` 立 `Instance`），视觉全在子物体 `Window`（**场景里存成 active**，方便在编辑器里看版式；
  运行时 `Awake` 第一帧自己收掉）。插在 `Text_LobbyToast` **之前**，提示行压得住它。
- **不画遮罩**：用户定过「左上角和右上角的显示是在那些全屏显示的弹窗界面中仍显示在屏幕上」——
  一整块暗色 Dim 会把左上头像 / 右上货币一起压黑，与那条口径冲突。代价是**点窗外不关窗**，要关就点「取消」。
- 接入点 `Scripts/UI/Lobby/FriendDetailRowUI.cs`：`Delete` / `Block` 都先过 `Ask(...)`；
  `Instance` 为空（窗没进场景）时**删除照样执行**（不把功能卡住），拉黑仍只弹「口径待定」。

### ④ ★ 排错记录（两条）

**（a）把上一轮那条坑提前避掉了**：标题 34 号的实测行高约 **48.9**，如果照「字号 + 一点余量」给 48，
配 `Ellipsis` 又会变成「一个字都不画」（见「二十八次修正」④ (a)）。所以标题盒直接取 **56**，
自证里也照旧带一条 `characterCount ≥ 5` + `盒高 ≥ preferredHeight` 的回归门。

**（b）截图相位不能「请求完就接着改状态」**：`ScreenCapture.CaptureScreenshot` 是**帧末**落盘，
第一版执行器在同一个 tick 里「请求截图 → 立刻点取消 → 再请求第二张」，结果第一张拍到的是**已经关掉的窗**
（文件根本没生成）。第二版改成「一张落盘了再换下一个动作」（`Stage61`）。

### ⑤ 导入守卫

`TextureImportSettingsGuard`：`NoNpotScaleFolders` 的 `lobby-ui-v1` 分支加 `LobbyConfirmPlate`
（760×200 不是 2 的幂，不禁止缩放会被吸成 1024×256）；`NeedsAlphaIsTransparency` 同上。

### ⑥ 自证

- **`stage60_confirm_dialog.txt`**（**失败合计 0**，OK 94）：贴图 `760x200 / npot=None / alpha=True`；
  `Panel_Confirm` 在 `Layer_Hud_v1` 下、根全屏拉伸且**没有 Image（= 没有遮罩）**；`Window` 760×200 居中；
  `Plate` 贴的就是 `LobbyConfirmPlate` 且吃点击；`Text_Title` 40/−36 / 680×56 / 字 34 / 居中 / Ellipsis /
  富文本 / 带 `<color=#E4CB84>` / 动词奶油 / `whoColor` = `E4CB84`；两个键 96×48 @272 / 392、
  各挂 Button（一条持久监听）+ 子背景 `LobbyChip_Kick`（不吃点击）+ 字 30；排在提示行之前。
  运行态：**点拉黑 → 开窗、标题「确认拉黑 在线甲」且真的画出字；点取消 → 关窗、名单还是 3 行**；
  点删除 → 标题「确认删除 在线甲」；点确认 → 关窗 + 写出 `removed=true` 墓碑；再点删除仍能开窗。
  测试用的是假好友，`friends.json` **跑前备份、跑后还原**（不污染真名单）。
- **`stage61_confirm_shots.txt`** + **`stage48_shots/f12_confirm_block.png`**（确认拉黑）/ **`f13_confirm_delete.png`**
  /**`f14_confirm_delete.png`**（确认删除，名单在）：窗压在好友列表之上、左上 / 右上 HUD 照旧可见。
- 两支执行器跑完**都已自删**（`Stage60ConfirmDialog.cs` / `Stage61ConfirmShots.cs` 均已不在仓库）。

**下一步（等用户）**：另外三格（添加好友 / 申请列表 / 黑名单）的内容，以及「拉黑」的口径。
