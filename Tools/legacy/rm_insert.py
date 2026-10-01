# -*- coding: utf-8 -*-
import io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
P = r"C:\Users\22589\Documents\GitHub\Another-World\Assets\_Game\Art\Old\README.md"
txt = io.open(P, "r", encoding="utf-8", newline="").read()
nl = "\r\n" if "\r\n" in txt else "\n"
marker = "| 01344 诅咒女巫 |"
i = txt.index(marker)
j = txt.index(nl, i)
row = (
 "| 01347 荣誉侍者 | 2026-09-13 | 新增正式插画（此前无卡图）；`前缀: 无`、cost 3"
 "（6/2、`copyCount: 2`、`summonType: 0 (Hero)`、trait「退场：对对方一召唤物造成 2 伤害；主动退场：+2 能量，"
 "查看对方手牌并弃掉邪恶法术」，flags `hasOnDeath + hasActiveExit`）→ 形象＝背身而立、右手抬起示意的荣誉侍者："
 "白色无袖罩袍 + 板甲护臂护胫，左手提着蓝蜡封的火漆卷轴，背心一枚盾徽；唯一点缀色＝**蓝**（盾徽、卷轴蜡封与滴蜡、剑柄缠绳），"
 "其余压在中性灰。**取舍**：候选 A（背身，盾徽上带白色跃狮纹，佩剑竖立在右侧地面；`flat 29.9 / soft 44.8 / hard 17.6`、"
 "bbox 0.61×0.90）被圈定，用户随后**只要求改两处**——①「剑斜挎在身上」②「背后的徽章只是纯色，上面不需要图案」，其余一律保留。"
 "**改法（全程本地像素级搬运，未重新生成整图）**：先试 `edit_masked.py` + `openai/gpt-image-2` 局部重绘，蒙版只盖剑与徽章仍被安全系统拒"
 "（两次 HTTP 400），`qwen/qwen-image-3.0-pro` 又不支持 `/v1/images/edits`，遂改为直接搬像素——"
 "① 从原图提起那柄立剑（`alpha>0.35` 的最大连通块 32263 px），腾空处用剑下背景色中值 `(146,55,101)` 按 4 px 膨胀抹平；"
 "再构建**整幅画布**的 RGBA 图层，以 `Image.AFFINE` 把剑绕原剑柄 `(809,633)` 平移到落点 `(390,286)`、顺时针 24°（落点选在左肩外，"
 "让盾徽基本不被压住），贴到背上——剑仍是原图自己的剑（十字护手、蓝缠绳、圆首齐全），未新画；"
 "② 盾徽：「蓝色判据 `B>R+20 且 sat>35 且 luma>78` 取蓝底 → 从裁切框边界泛洪非蓝像素 → 淹不到的即盾形内部」"
 "把跃狮整片盖成纯色 `(52,110,173)`，**不做膨胀**（膨胀 1–2 px 会啃掉盾牌自己的深色描边，实测 1 px 就把一圈描边涂成纯蓝）。"
 "**两个关键坑**：把裁切出的剑 sprite 直接 `transform` 时用了**整幅画布坐标**而 sprite 是局部坐标，剑被映射到画外、成图上完全不见"
 "（与上一版 diff 为 0），必须先贴回整幅画布的透明图层再变换；旋转会把 sprite 边缘的洋红底带进 RGB（盾徽左缘出现洋红杂边），"
 "需先把 sprite 边缘 3 px 的 RGB 用邻域均值向外扩色再旋转。抠图：暗洋红底（pink p5 45）→ `key_pink3 12 6`"
 "（T0 31 / T1 43 / T_fill 39）→ `purge_key 30 pink` → `defringe 3 0.02`。终检：洋红残留 `d<45` = 0"
 "（剑 sprite 自带洋红混色的 464 px，用「`d<45` 且 alpha>0 的像素取 5×5 干净邻居 RGB 中值」清掉）；内部空洞 10483 px "
 "全是**主体自身围出的背景口袋**（左臂与罩袍之间、两腿之间的三角），`d>30` 的**误啃 83 px**（罩袍边缘的抗锯齿）已按原图 RGB 回填 `alpha=255`；"
 "1152×1536 RGBA。实测 `flat 28.4 / soft 44.2 / hard 19.1`、bbox 0.60×0.90、填充 0.41、`luma 166.8`、平均饱和 21.6"
 "（01103 = 19.6 / 36.1 / 23.7、71.2；01124 = 40.7 / 42.4 / 7.6、bbox 0.75×0.89）。"
 "**已知偏差**：bbox 宽 0.60 低于 3 费带下限 0.80（站姿人形且剑未撑出轮廓，先例 01306 阴 0.65、01307 阳 0.67）；"
 "`luma 166.8` 明显亮于基准 71.2（白罩袍 + 浅灰板甲，先例 01343 浅灰皮肉 130.3、01336 修正者 169.0）。"
 "`Hero/3` 下新建卡图与 `.meta`（新 guid `3bd1faeadae846858ae50ab23f049bad`），"
 "归档 `Art/Sprites/Generated/01347-attendant-01.png`（guid `b12c5cb54b27412da50922d7aa50aaf7`）；"
 "**无旧稿需归档**（此前无卡图）；三份 PNG SHA256 一致 `b1749e5a…ba475eb` |"
)
assert "|" not in row.replace("| 01347 荣誉侍者 | 2026-09-13 |", "").replace(" |", "")
new = txt[:j] + nl + row + txt[j:]
io.open(P, "w", encoding="utf-8", newline="").write(new)
print("inserted after line containing:", marker)
print("readme rows now:", sum(1 for L in new.split(nl) if L.startswith("| 0")))
