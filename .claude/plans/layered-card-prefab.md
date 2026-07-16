# 分层卡牌预制体改造计划

## 目标
把当前"每张卡一个独立 prefab / 整张卡图"的临时方案改造为**底图+边框+卡面三层分离**的系统。只需少量通用美术素材即可拼出所有卡。

## 当前状态
- **2D 卡牌**: 2 个通用 prefab（`Card00_2D.prefab` / `SpellCard00_2D.prefab`），单 `Image` 组件显示整张 `cardSprite2D`
- **3D 卡牌**: 179 个独立 `.asset` 文件各引用一个 `prefab3D`，卡面用 `cardfront.mat` (Standard shader + `_MainTex`)
- **文字**: 5 个 `TextMeshPro` 子物体（攻击/生命/名称/费用/前缀），`CardDisplay3D.Refresh()` 绑定
- **实体**: 薄盒 (BoxCollider 2×2×0.01)，单面渲染

## 方案选择
**方案B: GPU 动态合成贴图** — 底图→边框→卡面三层在 shader 里 Alpha 叠加，不增加面数。

## 已完成
1. ✅ `CardComposite.shader` — 三层合成 shader，`_BgTex` → `_BorderTex` → `_ArtTex` Alpha 叠加
2. ✅ `CardArtConfig.cs` — ScriptableObject 全局配置，按费用/前缀选底图和边框
3. ✅ `CardDisplay3D` 更新 — `Awake()` 克隆材质实例，新增 `SetCompositeTextures()` / `ApplyArtFromCard()`

## 待完成

### 3D 卡牌
- [ ] 创建通用 3D 预制体 `Card3D_Template.prefab`，预挂 `CardComposite` 材质
- [ ] 所有 `CardData.asset` 的 `prefab3D` 指向通用预制体
- [ ] `CardDatabase` 或 `HandManager.PlaceCardToSlot` 中改实例化逻辑，每个模型克隆材质
- [ ] 底图/边框美术资源制作（见下方清单）

### 2D 卡牌
- [ ] `Card00_2D.prefab` 增加 `Background` / `Border` 两个子 `Image`
- [ ] `CardDisplay2D.Refresh()` 增加底图/边框 Sprite 绑定

### 美术资源清单
| 层 | 素材 | 数量 | 说明 |
|---|---|---|---|
| 底图 | 1费/3费/5费召唤物底图 | 3 | 不同底色 |
| 底图 | 法术底图 | 3 | 普通/邪恶/反制 |
| 边框 | 前缀边框 | 7 | 无/渊/机械/灵能/血歌/神灵画卷/神选者 |
| 卡面 | 每张卡卡图 | 已有 | `cardSprite2D` 的 texture 直接复用 |
| 配置 | CardArtConfig.asset | 1 | 拖入上述所有 Sprite |

### 可选的后续优化
- [ ] 清理旧的每卡独立 3D prefab（不再需要）
- [ ] `CardData.prefab3D` 字段改为可选（改为从配置读取通用预制体）
- [ ] 2D 卡牌增加费用角标显示
