# slot-prison-v1（01331 囚牢 · 雾牢）

2026-10-27 第一版，**已接入运行时**（`SlotPrisonFX01331`）。

## 文件

| 文件 | 尺寸 | 用途 |
|---|---|---|
| `SlotPrison_FogBed_01331.png` | 540×960 | 底雾；边缘厚、中心薄 |
| `SlotPrison_MistA_01331.png` | 640×640 | 横向慢雾 |
| `SlotPrison_MistB_01331.png` | 640×640 | 团状快雾，反向漂移 |
| `SlotPrison_Bar_01331.png` | 128×720 | 雾栅，四根复用 |
| `SlotPrison_Sigil_01331.png` | 384×384 | 中央封印 |
| `SlotPrison_Mote_01331.png` | 64×64 | 微光尘 |
| `SlotPrison_Vortex_01331.png` | 192×192 | 可放渊格的涡眼 |
| `preview_01331_slots_v1.png` | 1920×1200 | 双格终态 + 组件条 |
| `preview_01331_timeline_v1.png` | 1920×760 | 进场四帧 |
| `preview_01331_motion_v1.webp` | 640×940 / 8s | 动态预览（推荐） |
| `preview_01331_motion_v1_small.gif` | 400×588 / 8s | 动态预览 GIF 小样 |
| `preview_01331_motion_v1.gif` | 640×940 / 8s | 动态预览 GIF 全尺寸 |

## 色板

- 雾底 `#241536`
- 深雾 `#503374`
- 亮雾 `#9A79D0`
- 符文核心 `#E9DEFF`
- 涡眼 `#C9A7FF`

## 运行时

- 组件图在 `Assets/_Game/Resources/UI/SlotPrison01331/`；本目录只留审图、复跑脚本与动态预览。
- 接入点：`BoardSlot.Start` / `BoardSlot.SyncVisual` → `SlotPrisonFX01331.Sync(slot, prisonBlocked, prisonAllowYuan)`；`PrisonEnterEffect` 选中两格后各 `Play` 一次。
- 槽位底色走 `BoardSlot.PrisonSlotTint`（`0.26, 0.17, 0.34`，压到预览的深紫，让雾负责提亮）；雾、栅、印、涡眼、尘全部在 `SlotPrison01331` overlay 内，`raycastTarget=false`，不参与点选。
- 进场 0.62s（雾 0.10s / 栅 0.12s 起 / 印 0.28s / 涡眼 0.44s / 落定闪 0.46s），退场 0.34s。
- 状态先结算，动画纯表现；囚牢标记消失时 overlay 自己收口销毁。

## 口径

- 雾是程序化 fBm，不使用 imagegen，保证透明边与可平铺。
- 雾层边缘自带透明羽化；A/B 两层比槽位大 1.36 / 1.20 倍，外侧只留很淡的软溢出，不出现硬矩形边。
- 雾栅是“雾柱”，不是金属栏杆；亮度必须低于中央封印。
- 可放渊格只多一个中央涡眼，不改槽位缩放。
- 组件图先进本目录审；确认后复制到 `Assets/_Game/Resources/UI/SlotPrison01331/`，运行时只读 Resources 副本。

## 动态口径（2026-10-27 定）

- 雾 A：8s 周期左右漂移 ±10% 槽宽，轻微旋转，慢呼吸。
- 雾 B：4s 周期反向漂移 ±7.8% 槽宽，叠加在 A 上，形成“弥漫”而不是整块平移。
- 运行时雾层不加遮罩：纹理自身透明羽化，A/B 两层比槽位大，只在槽位边缘外留很淡的软溢出；不出现硬矩形。
- 雾栅：**底端固定**，顶端摆幅 5.5% 槽宽 + 1.8% 次级摆动；叠加 0.8% 的 S 形扭动，四根相位 0 / 0.9 / 1.8 / 2.7。
- 阵风包络：0.80–1.00 慢速起伏，四根共用包络但相位错开，避免一起“立正”。
- 运行时实现：雾用两层普通 UI Image 做 `anchoredPosition.x` 正弦漂移；雾栅用单个 `PrisonBarGraphic`（`MaskableGraphic`）生成 4 条、每条 12 段的网格，底端顶点锁死，顶端按 `sin(t + phase)` 改 X，避免用多段 Image 拼出接缝。

## 实机验证（2026-10-27）

在 `Game.unity` 进 Play，用临时脚本把雾牢挂到两个空槽实测（验证后已删）：

| 项 | 结果 |
|---|---|
| 7 张组件图 `Resources.Load` | 全部命中，无 missing 警告 |
| overlay 构建 | 两个槽位各生成 1 个 `SlotPrison01331`，层序 MistA → MistB → FogBed → Bars → Sigil → SealFlash → Vortex → Mote×10 |
| 动态 | 雾左右弥漫、雾栅底端固定顶端摆、封印呼吸、涡眼旋转、尘上升均正常 |
| 可放渊格 | 只多涡眼，槽位缩放不变 |
| 底色 | 首版 `0.45/0.32/0.60` 在实机偏亮、把雾压平；改为 `0.26/0.17/0.34` 后与预览的深底一致 |
| 异常 | 运行期无 NullReference / MissingReference，无 `error CS` |

尚未实测：联机远端、囚牢死亡/退场收尾、连续多次施加。

## 复跑

```powershell
python "Assets\_Game\Art\Sprites\Generated-01331-slot-v1\SlotPrisonFXv1.py"
python "Assets\_Game\Art\Sprites\Generated-01331-slot-v1\SlotPrisonMotionPreview.py"
```

> 说明：`Tools/cardframe/` 当前对本会话的写入被 ACL 拒绝，脚本暂放在本目录；权限允许后移到 `Tools/cardframe/`。
