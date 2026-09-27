# battle-mode-v1 —— 子全屏弹窗里的模式卡

出图脚本：`Tools/cardframe/BattleModeV1.ps1`（预览 `Tools/cardframe/preview/battle-mode-v1-cards.png`）。

## 产物（贴图 3x = 屏幕 300×420）

| 文件 | 用在哪 | 徽记 |
|---|---|---|
| `BattleModeCard_Match.png` / `…MatchHover.png` | `Panel_Battle`（战斗）左卡 | 两个相对的箭头 + 中央菱形铆钉 —— 撮合 / 相遇 |
| `BattleModeCard_Ranked.png` / `…RankedHover.png` | `Panel_Battle` 右卡 | 三级上升台阶 + 顶端菱形 —— 段位晋升 |
| `BattleModeCard_Offline.png` / `…OfflineHover.png` | `Panel_Other`（其它）唯一一张 | 左右各留一道缺口的圆环 + 中央菱形 —— 没连线 / 不在网里 |

- 文字不进贴图：卡上那行标题是场景里的 TMP（`Text_Title`），悬停变金由 `BattleModeCardButton` 负责。
- 悬停态只差色调（石面提亮 + 金 `GOLD→GOLD_L`），形体 / 尺寸 / 版式一律不动 —— 切贴图不跳位。
- 900×1260 是 NPOT，已在 `Assets/_Game/Editor/TextureImportSettingsGuard.cs` 的 `NoNpotScaleFolders` 白名单里（否则会被吸成 512×1024）。

## 落到场景里

- 卡组由 `Assets/_Game/Editor/BattleModeCardsBuilder.cs` 建：`Build()`（战斗：匹配 / 排位）、
  `BuildOfflineCard(panel)`（其它：离线模式）共用 `BuildCardsRoot()`；停位由运行时的 `BattleModeCards` 现算。
- 每张卡上挂 `BattleModeCardButton`：悬停换贴图 + 标题变金；点击按 `Kind` 分流
  （`QuickMatch` → `QuickMatchPanel.Open()`；`Ranked` → 占位弹窗；`Offline` → `LobbyManager.EnterOfflineBattle()`）。
- `Offline` 不受「未连接 Steam」闸门限制（它本来就是给没连 Steam 的人用的）。

<!-- 规矩来源：AGENTS.md「界面 / 场景美术方向」；色值不另起（墨 #06090E / 金 #C8A44A / 奶油 #F0E8D2）。 -->
