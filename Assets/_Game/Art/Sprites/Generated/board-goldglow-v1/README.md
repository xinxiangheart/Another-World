# 金色细线的二次辉光（v1）

## 这轮解决什么

用户 2026-10-23：「把金色细线单独做一张模糊副本，压在线下面，加色混合、alpha 4–8%……
金环会从『画上去的线』变成『会反光的金属』。」

做法：从金色层里**只抠金色像素**，做两档模糊（近核 + 远晕），再压回棋盘。

## 取哪些层

| 层 | 取什么 | 说明 |
|---|---|---|
| `Board_Surface.png` | 内圈跑道金环 + 铆钉 | 最显眼的一条 |
| `Board_Ornament.png` | 外框金轨 + 铆钉 | 屏幕四边 |
| `Board_Rune.png` | 六芒星的淡金线 | 蓝环 / 中心菱形被色相筛掉，不会跟着转 |

筛选条件：色相 25°–70°、饱和 > 0.12、明度 > 0.25、alpha > 0.03。
`Board_Sigil` 不取 —— 它的材质色是纯黑（中心大圆环本来就是黑的）。

## 两档模糊

| 档 | 半径（2048 画布 px） | 权重 | 作用 |
|---|---|---|---|
| 近核 | 3 | 0.45 | 贴住线，补金属的高光 |
| 远晕 | 22 | 0.55 | 光晕，约 17 屏幕 px |

合计后再乘 **0.30**（等于「加色层 alpha 4–8%」的等效强度；纯加色在 Linear 空间里比数值看起来更强）。

## 为什么装进了 `Board_Glow.png`，而不是新加第 9 层

- 8 层材质的渲染队列是写死的 3000–3007，插一层要重排 3 个材质 + 往 `Game.unity` 里复制一整个 PrefabInstance；
- 用户当时 Unity 正开着 Game 场景，外部改 `.unity` 有被编辑器回写覆盖的风险；
- `Board_Glow` 正好在队列 **3005**（金线 3004 之上、微尘 3006 之下），本来就是「叠在所有东西上的光」，把金晕并进去**观感等价、零场景风险**。

独立的 `Board_GoldGlow.png` 保留着：以后要改成真正的 `Blend SrcAlpha One` 独立层，
新建材质 + 复制 `Board3D_Glow` 节点即可，纹理不用重做。

## 文件

- `Board_GoldGlow.png` —— 纯金晕（2048×1152，峰值 alpha 0.365）
- `Board_Glow_base.png` —— 原 `Board_Glow.png` 的存档（脚本读它，避免重复叠加）
- `Board_Glow_v2_with_gold.png` —— 与原 `Board_Glow.png` 合成后的成品（**已安装**）
- `preview-corner-tl.png` / `preview-ring-top.png` / `preview-center.png` —— before / after 对照
- 生成：`python Tools/cardframe/BoardGoldGlowV1.py`（一次跑完抠金、模糊、合成、落盘）

合成（Linear 空间）：

```
a  = a_glow + a_gold * 0.30 * (1 - a_glow)
rgb = (rgb_glow*a_glow + rgb_gold*a_gold*0.30*(1-a_glow)) / a
```

## 回退

```powershell
Copy-Item "$env:TEMP\boardglow-backup-20261023-205812\Board_Glow.before-gold.png" `
          "Assets/_Game/Art/Sprites/Generated/board-layers-v2/Board_Glow.png" -Force
```
