# 多语言本地化计划

> 创建: 2026-07-13 | 状态: 计划中

## 背景

所有特性描述和文字介绍目前都是中文。目标：支持中/英文随时切换，逻辑层零改动。

## 利好

Step 4 已把法术从 `switch(中文effect)` 切到 `templateID`，Step 7b 建了 `CardTextTable(230张)`。核心阻塞已清除。

## 方案：LocManager + 三文本表

```
CardTextTable.Get(id)  ─┐
TraitTextTable.Get(key) ─┼→ LocaleManager.CurrentLocale → UI刷新
PrefixTextTable.Get(key) ─┘
```

### 1. LocManager

```csharp
// Assets/_Game/Scripts/Localization/LocManager.cs
public enum Loc { Zh, En }

public static class LocManager
{
    public static Loc Current { get; private set; } = Loc.Zh;
    public static event Action OnChanged;

    public static void Switch(Loc loc) {
        Current = loc;
        PlayerPrefs.SetString("loc", loc.ToString());
        OnChanged?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod]
    static void Init() => Current = PlayerPrefs.GetString("loc") == "En" ? Loc.En : Loc.Zh;
}
```

### 2. CardTextTable 扩展

现有 `CardText( name, description )` → 扩展为 `( nameZh, descZh, nameEn, descEn )`，加 `GetName(Loc)` / `GetDesc(Loc)` 方法。

需要给 230 张卡补英文翻译。

### 3. TraitTextTable + PrefixTextTable

| 表 | 示例 key | Zh | En |
|----|----------|-----|-----|
| Trait | "进场" | 进场 | Enter |
| Trait | "退场" | 退场 | Death |
| Trait | "先手" | 先手 | First Strike |
| Trait | "反击" | 反击 | Revenge |
| Prefix | "渊" | 渊 | Abyss |
| Prefix | "机械" | 机械 | Mech |
| Prefix | "灵能" | 灵能 | Psy |
| Prefix | "血歌" | 血歌 | Bloodsong |

### 4. UI 刷新

所有显示卡牌文本的组件订阅 `LocManager.OnChanged`：

```csharp
void Start() {
    LocManager.OnChanged += Refresh;
    Refresh();
}
void Refresh() {
    var t = CardTextTable.Get(myID);
    nameLabel.text = t.GetName(LocManager.Current);
    descLabel.text = t.GetDesc(LocManager.Current);
}
```

### 5. 硬编码清理

| 问题 | 位置 | 处理 |
|------|------|------|
| `"Ԩ"` 渊前缀 | 8个文件 | 提取常量 `PREFIX_ABYSS` |
| `"进场"/"退场"` 等 | CardInstance 特性反射 | 改读 `TraitTextTable` |
| Debug.Log 中文 | 各处 | 保留中文，不做翻译 |

## 实施步骤

| Step | 内容 | 预估文件数 |
|------|------|-----------|
| L1 | LocManager + Loc 枚举 | 1 new |
| L2 | CardTextTable 扩展 + 230张英文 | 1 edit |
| L3 | TraitTextTable | 1 new |
| L4 | PrefixTextTable | 1 new |
| L5 | UI 刷新订阅 (CardDisplay2D/3D/Hover) | ~5 edits |
| L6 | `"Ԩ"` → 常量提取 | ~8 edits |
| L7 | `"进场"/"退场"` → TraitTextTable | ~3 edits |
| L8 | 切换 UI 按钮 + 测试 | 1 new |
