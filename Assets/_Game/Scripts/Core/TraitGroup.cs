using System;
using System.Collections.Generic;

// ============================================================================
// TraitGroup — 每张卡的特性组（数据/逻辑层，先搭框架不迁移具体卡效果）。
// ============================================================================
//
// 三层粒度查询：
//   HasTrait(id)        拥有（数据存在）
//   IsTraitActive(id)   拥有 + 未被禁（单条禁 或 沉默 BlockAll 均失效）
//   CanSend(category)   发送侧：沉默(BlockAll) 或 该类被禁 → 不能发动
//   CanReceive(cat)     接收侧：BlockReceive 显式禁制 或 活跃特性自带 receiveBlocks → 不能承受
//
// 计数式多重禁制：同一特性/类别被多个来源禁 → 计数累加；一个来源解除计数-1，归零才解禁。
// BlockAll = 沉默（只禁发送侧 CanSend/IsTraitActive；CanReceive 独立，另用 BlockReceive）。
// 特性自带 receiveBlocks（如禁疗→Healed）在特性活跃时拦截；特性被禁/沉默 → IsTraitActive=false → 拦截失效。
//
// 生命周期：Phase=阶段边界自动清除；SourceAlive=来源退场/移除时主动 Unblock；Permanent=手动。
// 重结算：仅 isPersistent=true 的常驻特性，被禁调 removeEffect、恢复调 applyEffect；
//         属性重算本次留空入口（不碰 current*），迁移常驻特性后再实现。
// ============================================================================

public class TraitGroup
{
    public CardInstance owner;                          // 关联的卡实例（构建时绑定）
    public List<RuntimeTrait> traits = new List<RuntimeTrait>();

    // ── 计数式禁制 ──
    readonly Dictionary<string, int> _blockedTraits = new Dictionary<string, int>();
    readonly Dictionary<TraitCategory, int> _blockedCategories = new Dictionary<TraitCategory, int>();
    readonly Dictionary<EffectCategory, int> _blockedReceive = new Dictionary<EffectCategory, int>();
    int _blockAllCount;                                 // 沉默(BlockAll)：禁所有发送

    readonly List<TraitBlock> _blocks = new List<TraitBlock>(); // 生命周期记录（每条禁制一条）

    public enum TraitLifecycle { Phase, SourceAlive, Permanent }

    public class TraitBlock
    {
        public object source;           // 来源（卡牌/光环/格子）
        public int kind;                // 0=traitId 1=category 2=receive 3=all(沉默)
        public string traitId;
        public TraitCategory category;
        public EffectCategory effectCat;
        public TraitLifecycle lifecycle;
        public CardInstance sourceCard; // SourceAlive 判定用
    }

    // ═══════════════════ 构建 ═══════════════════

    /// <summary>从 CardInstance 构建：固有（模板 traitEntries）+ 授予（grantedTraits）+ 伪特性。</summary>
    public static TraitGroup BuildFrom(CardInstance ci)
    {
        var tg = new TraitGroup();
        if (ci == null) return tg;
        tg.owner = ci;

        CardData tpl = CardDatabase.Instance?.GetTemplate(ci.templateID);

        // 1) 固有：模板 CardData.GetTraitEntryList()（class，带 isEnter/isFirstStrike 等属性标记）
        if (tpl != null)
        {
            var entries = tpl.GetTraitEntryList();
            if (entries != null)
                foreach (var e in entries)
                {
                    string[] attrs = e.isGrant ? new[] { "赋予" } : e.GetAttributes();
                    var rt = new RuntimeTrait
                    {
                        traitId = RuntimeTrait.BuildUniqueId(attrs, e.text, tg.traits),
                        text = e.text,
                        attributes = attrs,
                        sourceTemplateID = null, // 固有
                    };
                    // 固有禁疗注册（03026/01531）：文本含"无法…恢复生命值"的常驻特性 → 拦截 Healed。
                    // 特性被沉默/单条禁（IsTraitActive=false）→ 该条拦截失效 → 恢复可治疗。
                    if (IsInherentAntiHeal(ci.templateID, e.text))
                        rt.receiveBlocks = EffectCategory.Healed;
                    // 固有反制免疫（无畏者01319）：文本含"不触发反制"的常驻特性 → 拦截 Countered。
                    // 特性被禁/沉默 → 拦截失效 → 恢复可被反制。
                    if (IsInherentCounterImmune(ci.templateID, e.text))
                        rt.receiveBlocks |= EffectCategory.Countered;
                    // 固有敌方法术免疫预留（征服者01508）：文本含"不受对方非反制" → receiveBlocks |= SpellTargeted。
                    // 数据注明"（未实现）"，当前无任何 CanReceive(SpellTargeted) 消费 → 纯预留、零行为变化。
                    // 特性被禁/沉默 → 拦截失效（免疫解除）。未来接"敌方法术不可选中"时须补 side-aware（仅对方法术）。
                    if (IsInherentEnemySpellImmune(ci.templateID, e.text))
                        rt.receiveBlocks |= EffectCategory.SpellTargeted;
                    // 5.x 持续附着效果声明（01327/03001/01129/01131/01510）：isPersistent=true 纯声明。
                    // 持续效果均为离散事件/实时查询且事件点已带 IsFullySilenced 门 → 沉默即停、解除自动恢复，
                    // applyEffect/removeEffect 留空（零行为）。如需 apply/remove 挂接再补。
                    if (IsInherentPersistentAttach(ci.templateID))
                        rt.isPersistent = true;
                    tg.traits.Add(rt);
                }
        }

        // 2) 授予：grantedTraits（text + attributes + sourceTemplateID）
        tg.RefreshGranted();

        // 3) 伪特性（无 TraitEntry 的能力标记，供 per-trait 查询；只注册不接真实门控）
        //    仅召唤物有攻击/攻击限制能力；Splash 伪特性留空（无统一文本标记，后续按卡声明）
        if (tpl != null && tpl.cardType == CardType.Summon)
        {
            tg.traits.Add(new RuntimeTrait { traitId = "攻击", text = "攻击", sourceTemplateID = null });
            if (tpl.attacksFrontRow)
                tg.traits.Add(new RuntimeTrait { traitId = "攻击前排限制", text = "攻击前排限制", sourceTemplateID = null });
            if (tpl.attacksBackRow)
                tg.traits.Add(new RuntimeTrait { traitId = "攻击后排限制", text = "攻击后排限制", sourceTemplateID = null });
        }
        return tg;
    }

    /// <summary>固有"禁疗"特性识别：仅 03026/01531 迁入特性组（receiveBlocks=Healed）。
    /// 判定 = 模板ID白名单 + 常驻文本同时含"无法"+"恢复生命值"。其他卡即使文本相似也暂不拦（未迁移）。</summary>
    static bool IsInherentAntiHeal(string templateID, string text)
    {
        if (templateID != "03026" && templateID != "01531") return false;
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("无法") && text.Contains("恢复生命值");
    }

    /// <summary>固有"反制免疫"特性识别：仅 01319（无畏者）迁入特性组。
    /// 判定 = 模板ID白名单 + 常驻文本含"不触发反制"。其他卡即使文本相似也暂不拦（未迁移）。</summary>
    static bool IsInherentCounterImmune(string templateID, string text)
    {
        if (templateID != "01319") return false;
        if (string.IsNullOrEmpty(text)) return false;
        // 无畏者 01319 常驻文本："该召唤物不会触发反制牌" / "不触发反制牌"
        return text.Contains("不会触发反制牌") || text.Contains("不触发反制牌");
    }

    /// <summary>固有"敌方法术免疫"识别（预留）：仅 01508（征服者）迁入特性组。
    /// 判定 = 模板ID白名单 + 常驻文本含"不受对方非反制"。数据注明"（未实现）" → 只注册不接行为。
    /// 预留 receiveBlocks=SpellTargeted；接入敌方法术不可选中的门控时，需侧判定（仅对方法术）再加到判断点。</summary>
    static bool IsInherentEnemySpellImmune(string templateID, string text)
    {
        if (templateID != "01508") return false;
        if (string.IsNullOrEmpty(text)) return false;
        // 征服者 01508 常驻文本："不受对方非反制卡牌法术影响（未实现）"
        return text.Contains("不受对方非反制");
    }

    /// <summary>持续附着效果模板（5.x 纯声明标记）：01327 阴影聚合体(宿主模式)/03001 追随者(每阶段+0+1)/
    /// 01129 滋养者(回合开始回宿主)/01131 未弃之人(退场转化)/01510 古老精灵(宿主退场转移)。
    /// 各持续效果已在事件点带实时沉默门 → isPersistent 仅文档化，apply/remove 留空。</summary>
    static bool IsInherentPersistentAttach(string templateID)
    {
        return templateID == "01327" || templateID == "03001" || templateID == "01129"
            || templateID == "01131" || templateID == "01510";
    }

    /// <summary>重建授予 RuntimeTrait（移除旧授予 + 从 owner.grantedTraits 重加）。</summary>
    public void RefreshGranted()
    {
        if (owner == null) return;
        traits.RemoveAll(t => t != null && t.sourceTemplateID != null); // 移除旧授予
        if (owner.grantedTraits == null) return;
        foreach (var g in owner.grantedTraits)
        {
            if (g == null) continue;
            string[] attrs = g.attributes != null ? g.attributes.ToArray()
                : CardData.ParseTraitAttributesFromText(g.text);
            traits.Add(new RuntimeTrait
            {
                traitId = RuntimeTrait.BuildUniqueId(attrs, g.text, traits),
                text = g.text,
                attributes = attrs,
                sourceTemplateID = g.sourceTemplateID ?? "",
            });
        }
    }

    // ═══════════════════ 查询（三层粒度）═══════════════════

    /// <summary>拥有某条特性（数据存在，与禁制无关）。</summary>
    public bool HasTrait(string traitId)
    {
        foreach (var t in traits) if (t != null && t.traitId == traitId) return true;
        return false;
    }

    /// <summary>拥有 + 未被禁（单条禁 或 沉默 BlockAll 都算失效）。</summary>
    public bool IsTraitActive(string traitId)
    {
        if (!HasTrait(traitId)) return false;
        if (_blockAllCount > 0) return false;  // 沉默禁所有发送
        return !(_blockedTraits.TryGetValue(traitId, out int c) && c > 0);
    }

    /// <summary>是否拥有某属性类的特性（数据存在，无视禁制/沉默）。attributes 含 attr 即算。
    /// 多属性条目（如同时 退场+主动退场）也算；文本无前缀、仅条目 isX 标记的情况也正确覆盖。</summary>
    public bool HasClass(string attr)
    {
        for (int i = 0; i < traits.Count; i++)
        {
            var t = traits[i];
            if (t == null || t.attributes == null) continue;
            for (int a = 0; a < t.attributes.Length; a++)
                if (t.attributes[a] == attr) return true;
        }
        return false;
    }

    /// <summary>是否拥有某属性类且至少一条激活（未沉默 BlockAll、未被单条禁）。
    /// 供"能触发该类行为"判断用；光环关键字禁制（如萨满禁抛置）不在特性组内，由调用方再 && !IsTraitBlocked 组合。</summary>
    public bool HasActiveClass(string attr)
    {
        if (_blockAllCount > 0) return false; // 沉默禁所有发送
        for (int i = 0; i < traits.Count; i++)
        {
            var t = traits[i];
            if (t == null || t.attributes == null) continue;
            bool match = false;
            for (int a = 0; a < t.attributes.Length; a++)
                if (t.attributes[a] == attr) { match = true; break; }
            if (!match) continue;
            if (IsTraitActive(t.traitId)) return true;
        }
        return false;
    }

    /// <summary>发送侧：此卡能否发动 c 类行为（沉默 BlockAll 或该类被禁 → 否）。</summary>
    public bool CanSend(TraitCategory c)
    {
        if (_blockAllCount > 0) return false;
        return !(_blockedCategories.TryGetValue(c, out int cc) && cc > 0);
    }

    /// <summary>接收侧：此卡能否承受 c 类效果（独立于发送禁制）。
    /// 双重拦截：① 外部 BlockReceive 显式禁制（计数式）；② 活跃特性的 receiveBlocks（如禁疗→Healed）。
    /// 特性被禁/沉默（IsTraitActive=false）则该条拦截失效 → 恢复可接收。</summary>
    public bool CanReceive(EffectCategory c)
    {
        if (_blockedReceive.TryGetValue(c, out int cc) && cc > 0) return false;
        for (int i = 0; i < traits.Count; i++)
        {
            RuntimeTrait t = traits[i];
            if (t == null || t.receiveBlocks == 0) continue;
            if ((t.receiveBlocks & c) == 0) continue;
            if (IsTraitActive(t.traitId)) return false; // 特性在生效 → 拦截该接收类别
        }
        return true;
    }

    /// <summary>是否有任一活跃禁制（状态栏 debuff 图标：多个禁制只显示一个，用此单 bool）。</summary>
    public bool HasActiveBlock()
    {
        if (_blockAllCount > 0) return true;
        foreach (var kv in _blockedTraits) if (kv.Value > 0) return true;
        foreach (var kv in _blockedCategories) if (kv.Value > 0) return true;
        foreach (var kv in _blockedReceive) if (kv.Value > 0) return true;
        return false;
    }

    // ═══════════════════ 禁制（计数式）═══════════════════

    /// <summary>沉默：禁所有发送（只禁发送侧，CanReceive 独立）。lifecycle 默认 Permanent。</summary>
    public void BlockAll(object source, TraitLifecycle life = TraitLifecycle.Permanent, CardInstance sourceCard = null)
    {
        _blockAllCount++;
        AddBlock(3, source, life, sourceCard);
        OnBlockChanged();
    }

    public void BlockCategory(TraitCategory c, object source, TraitLifecycle life = TraitLifecycle.Permanent, CardInstance sourceCard = null)
    {
        _blockedCategories[c] = GetOrZero(_blockedCategories, c) + 1;
        AddBlock(1, source, life, sourceCard, category: c);
        OnBlockChanged();
    }

    public void BlockTrait(string traitId, object source, TraitLifecycle life = TraitLifecycle.Permanent, CardInstance sourceCard = null)
    {
        _blockedTraits[traitId] = GetOrZero(_blockedTraits, traitId) + 1;
        AddBlock(0, source, life, sourceCard, traitId: traitId);
        OnBlockChanged();
    }

    public void BlockReceive(EffectCategory c, object source, TraitLifecycle life = TraitLifecycle.Permanent, CardInstance sourceCard = null)
    {
        _blockedReceive[c] = GetOrZero(_blockedReceive, c) + 1;
        AddBlock(2, source, life, sourceCard, effectCat: c);
        OnBlockChanged();
    }

    /// <summary>解除某来源的全部沉默(BlockAll)禁制（该来源几条 BlockAll 记录 → 计数相应减）。</summary>
    public void UnblockAll(object source)
    {
        int removed = RemoveBlocks(source, kind: 3);
        for (int i = 0; i < removed; i++) if (_blockAllCount > 0) _blockAllCount--;
        if (removed > 0) OnBlockChanged();
    }

    public void UnblockCategory(TraitCategory c, object source)
    {
        int removed = RemoveBlocks(source, kind: 1, category: c);
        Dec(_blockedCategories, c, removed);
        if (removed > 0) OnBlockChanged();
    }

    public void UnblockTrait(string traitId, object source)
    {
        int removed = RemoveBlocks(source, kind: 0, traitId: traitId);
        Dec(_blockedTraits, traitId, removed);
        if (removed > 0) OnBlockChanged();
    }

    public void UnblockReceive(EffectCategory c, object source)
    {
        int removed = RemoveBlocks(source, kind: 2, effectCat: c);
        Dec(_blockedReceive, c, removed);
        if (removed > 0) OnBlockChanged();
    }

    // ── 生命周期：阶段边界 / 来源在场检查（调用方在阶段边界/来源移除时驱动）──

    /// <summary>清除到期禁制：Phase 全清；SourceAlive 且来源卡已死/离场 → 清该条。返回是否有变化。</summary>
    public bool TickLifecycle()
    {
        bool changed = false;
        for (int i = _blocks.Count - 1; i >= 0; i--)
        {
            TraitBlock b = _blocks[i];
            bool expired = b.lifecycle == TraitLifecycle.Phase;
            if (!expired && b.lifecycle == TraitLifecycle.SourceAlive)
                expired = b.sourceCard == null || b.sourceCard.isDead; // 无 sourceCard 的由调用方显式 Unblock
            if (expired)
            {
                UnblockBlock(b);
                _blocks.RemoveAt(i);
                changed = true;
            }
        }
        if (changed) OnBlockChanged();
        return changed;
    }

    // ═══════════════════ 内部 ═══════════════════

    void AddBlock(int kind, object source, TraitLifecycle life, CardInstance sourceCard,
        string traitId = null, TraitCategory category = default, EffectCategory effectCat = default)
    {
        _blocks.Add(new TraitBlock
        {
            source = source, kind = kind, lifecycle = life, sourceCard = sourceCard,
            traitId = traitId, category = category, effectCat = effectCat,
        });
    }

    /// <summary>移除某来源匹配的禁制记录（kind+traitId/category 匹配），返回移除条数。</summary>
    int RemoveBlocks(object source, int kind, string traitId = null,
        TraitCategory category = default, EffectCategory effectCat = default)
    {
        int removed = 0;
        for (int i = _blocks.Count - 1; i >= 0; i--)
        {
            TraitBlock b = _blocks[i];
            if (!ReferenceEquals(b.source, source) || b.kind != kind) continue;
            if (kind == 0 && traitId != null && b.traitId != traitId) continue;
            if (kind == 1 && b.category != category) continue;
            if (kind == 2 && b.effectCat != effectCat) continue;
            _blocks.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    /// <summary>到期时对该条禁制做计数-1（UnblockBlock 不删 _blocks，调用方负责移除）。</summary>
    void UnblockBlock(TraitBlock b)
    {
        switch (b.kind)
        {
            case 0: Dec(_blockedTraits, b.traitId, 1); break;
            case 1: Dec(_blockedCategories, b.category, 1); break;
            case 2: Dec(_blockedReceive, b.effectCat, 1); break;
            case 3: if (_blockAllCount > 0) _blockAllCount--; break;
        }
    }

    static int GetOrZero<K>(Dictionary<K, int> d, K key)
        => d.TryGetValue(key, out int v) ? v : 0;

    /// <summary>计数减 amount，<=0 移除 key。</summary>
    static void Dec<K>(Dictionary<K, int> d, K key, int amount)
    {
        if (!d.TryGetValue(key, out int v)) return;
        v -= amount;
        if (v <= 0) d.Remove(key);
        else d[key] = v;
    }

    void OnBlockChanged()
    {
        RecalculatePersistentTraits();
        RecalculateAttributes();
    }

    /// <summary>重结算（只针对常驻特性 isPersistent=true）：被禁→removeEffect 还原；恢复→applyEffect 重新生效。
    /// 未迁移特性的 apply/remove 为空 → 无操作（零行为变化）。</summary>
    void RecalculatePersistentTraits()
    {
        for (int i = 0; i < traits.Count; i++)
        {
            RuntimeTrait t = traits[i];
            if (t == null || !t.isPersistent) continue;
            if (IsTraitActive(t.traitId)) t.applyEffect?.Invoke();
            else t.removeEffect?.Invoke();
        }
    }

    /// <summary>属性重算（框架入口，本次留空不碰 current* 值）。迁移常驻特性后再实现
    /// 「从基础属性(baseAttack/baseHealth/…)叠加当前生效常驻特性的净效果重算」。</summary>
    void RecalculateAttributes()
    {
        // 决策：只留空入口，不碰 current* 值。
    }
}
