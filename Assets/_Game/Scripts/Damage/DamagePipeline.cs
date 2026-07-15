using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// DamagePipeline — 统一五阶段伤害结算（D1-D5 全部落地）
// ============================================================================
//
// 从 ProcessPair(BattleManager) 和 ApplyDamageLoop(BattleManager) 提取所有
// 散落的硬编码伤害修改，重组成五阶段管线。
//
//   DamagePipeline.Process(input)
//     ├── Stage1_Give      (攻击方: slotTempAttack, 暴徒/猎犬/投机者/反社会)
//     ├── Stage2_Receive   (防守方: shield, attach/skip, tempHP, 领主/追随者/祭司)
//     ├── Stage3_FinalGive (攻击方: overclocked×2)
//     ├── Stage4_FinalReceive (防守方: 阴阳, 万象镜面)
//     └── Stage5_Apply     (X值, 母巢, 征服者, HP扣减, DamageSourceMarker)
//
// ProcessPair 的攻击方增益已内联在 Stage1_Give 中。ApplyDamageLoop 的防守方
// 处理已内联在 Stage2/4/5 中。
// ============================================================================

public enum DamagePhase { Battle, Spell, Trait, Discard }

public struct DamageResult
{
    public int finalDamage;
    public int absorbedDamage;
    public bool lethal;
    public bool redirectedToLord;
    public bool negatedByFollower;
    public int overkillDamage;
}

public struct DamageInput
{
    public CardInstance attacker;
    public CardInstance defender;
    public int baseDamage;
    public GameObject sourceObject;
    public DamagePhase phase;
    /// <summary>攻击方所在槽位的临时攻击加成（ProcessPair 中的 slotTempAttackBoost）。</summary>
    public int attackerSlotTempAttackBoost;
    /// <summary>攻击方槽位（用于读取 slotTempAttackBoost 等槽位数据）。</summary>
    public BoardSlot attackerSlot;

    public DamageInput(CardInstance attacker, CardInstance defender, int baseDamage,
        GameObject sourceObject = null, DamagePhase phase = DamagePhase.Battle,
        int attackerSlotTempAttackBoost = 0, BoardSlot attackerSlot = null)
    {
        this.attacker = attacker;
        this.defender = defender;
        this.baseDamage = baseDamage;
        this.sourceObject = sourceObject;
        this.phase = phase;
        this.attackerSlotTempAttackBoost = attackerSlotTempAttackBoost;
        this.attackerSlot = attackerSlot;
    }
}

public class DamageContext
{
    public DamageInput input;
    public int damage;
    public bool shieldConsumed;
    public int tempHpAbsorbed;
    public bool stopped;
    public bool redirectedToLord;    // S5 中领主重定向
    public CardInstance lordTarget;   // 领主实例
    public bool negatedByFollower;    // S5 中追随者挡死
    public bool revivedByPriest;      // S5 中生命祭司复活
    public CardInstance priestSource; // 祭司来源

    public CardInstance Attacker => input.attacker;
    public CardInstance Defender => input.defender;
    public int BaseDamage => input.baseDamage;
    public DamagePhase Phase => input.phase;

    public DamageContext(DamageInput input)
    {
        this.input = input;
        this.damage = input.baseDamage;
    }
}

public static class DamagePipeline
{
    public delegate int ModifierDelegate(int damage, DamageContext ctx);

    public static readonly List<ModifierDelegate> Stage1Modifiers = new List<ModifierDelegate>();
    public static readonly List<ModifierDelegate> Stage2Modifiers = new List<ModifierDelegate>();
    public static readonly List<ModifierDelegate> Stage3Modifiers = new List<ModifierDelegate>();
    public static readonly List<ModifierDelegate> Stage4Modifiers = new List<ModifierDelegate>();

    // ═══════════════════════════════════════════════════════════════════
    // 公共入口
    // ═══════════════════════════════════════════════════════════════════

    public static DamageResult Process(DamageInput input)
    {
        if (input.defender == null)
            return new DamageResult();

        var ctx = new DamageContext(input);

        // ── 预检 ─────────────────────────────────────────────────────
        if (ctx.Defender.isAttached) return new DamageResult();
        if (IsShadowHost(ctx.Defender)) return new DamageResult();

        // ── S1 攻击方增益 ───────────────────────────────────────────
        ctx.damage = Stage1_Give(ctx);

        // ── S2 防守方减益 ───────────────────────────────────────────
        ctx.damage = Stage2_Receive(ctx);
        if (ctx.stopped) return BuildResult(ctx);

        // ── S3 攻击方最终修正 ───────────────────────────────────────
        ctx.damage = Stage3_FinalGive(ctx);

        // ── S4 防守方最终修正 ───────────────────────────────────────
        ctx.damage = Stage4_FinalReceive(ctx);

        // ── S5 实际应用 ─────────────────────────────────────────────
        return Stage5_Apply(ctx);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stage1_Give — 攻击方增益
    // ═══════════════════════════════════════════════════════════════════
    //
    // 迁移自 ProcessPair 中的攻击方修正:
    //   - slotTempAttackBoost (槽位临时攻击加成)
    //   - 01114 暴徒: 目标有护盾 → +2 伤害并提前扣2HP
    //   - 01328 破防者光环: 己方有此卡 → 攻击护盾目标额外扣2HP
    //   - 01118 猎犬: 目标有 OnDeath/ActiveExit → +2 攻击力
    //   - 01125 投机者: 目标有 FirstStrike/OnEnter → +2 攻击力

    static int Stage1_Give(DamageContext ctx)
    {
        int d = ctx.damage;
        var atk = ctx.Attacker;
        var def = ctx.Defender;
        bool attackerSilenced = IsSilenced(atk);

        // ── slotTempAttackBoost（仅非X值单位适用）───────────────────
        if (!atk.isXValue && ctx.input.attackerSlotTempAttackBoost > 0)
            d += ctx.input.attackerSlotTempAttackBoost;

        // ── 01114 暴徒: 攻击持有护盾的目标额外扣2HP ─────────────────
        if (!attackerSilenced && atk.templateID == "01114" && def.hasShield)
        {
            def.currentHealth -= 2;
            UpdateDefenderValues(def);
        }

        // ── 01328 破防者: 己方有破防者 → 攻击护盾目标额外扣2HP ────
        if (def.hasShield && HasBreakerOnField(ctx.Attacker))
        {
            def.currentHealth -= 2;
            UpdateDefenderValues(def);
        }

        // ── 01118 猎犬: 攻击有(主动)退场特性的召唤物+2 ────────────
        if (!attackerSilenced && atk.templateID == "01118"
            && (def.HasOnDeath || def.HasActiveExit))
            d += 2;

        // ── 01125 投机者: 攻击有先手/进场特性的召唤物+2 ────────────
        if (!attackerSilenced && atk.templateID == "01125"
            && (def.HasFirstStrike || def.HasOnEnter))
            d += 2;

        // ── 01341 反社会分子: 对位无血歌前缀 → +2*前缀数 ──────────
        if (!attackerSilenced && atk.templateID == "01341"
            && !def.prefixes.Contains("血歌"))
            d += 2 * CountPrefixes(def);

        // ── 03012 阴阳: 攻击时伤害+1 ─────────────────────────────────
        if (!attackerSilenced && atk.isYinYang)
            d += 1;

        // ── 01114/01328 的提前扣HP已在上面处理。继续注册钩子 ─────
        foreach (var m in Stage1Modifiers)
            d = m(d, ctx);
        return d;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stage2_Receive — 防守方减益
    // ═══════════════════════════════════════════════════════════════════
    //
    // 迁移自 ApplyDamageLoop:
    //   - 护盾吸收 (hasShield → RemoveShield → stopped)
    //   - tempHealthBoost 吸收
    //   - 中毒 ×2
    //   - 生命祭司(01507) 祝福复活
    //   - 勇者(01514) 追随者挡死
    //   - 领主(01503) 伤害重定向

    static int Stage2_Receive(DamageContext ctx)
    {
        int d = ctx.damage;
        var def = ctx.Defender;
        bool defenderSilenced = IsSilenced(def);

        // ── 护盾吸收 ────────────────────────────────────────────────
        if (def.hasShield)
        {
            def.RemoveShield();
            ShowFloaterAt(def, 0, FloaterType.Blocked);
            ctx.shieldConsumed = true;
            ctx.stopped = true;
            return d;
        }

        // ── 阴阳(03012): 受到伤害时 -1 ────────────────────────────
        // (放在 overclocked 之前，匹配原始 ApplyDamageToMinion 顺序)
        if (!defenderSilenced && def.isYinYang)
            d = Mathf.Max(0, d - 1);

        // ── 中毒 ×2 ─────────────────────────────────────────────────
        if (def.poisoned)
            d *= 2;

        // ── tempHealthBoost 吸收 ────────────────────────────────────
        if (def.tempHealthBoost > 0)
        {
            if (d <= def.tempHealthBoost)
            {
                def.tempHealthBoost -= d;
                def.currentHealth -= d;
                ShowFloaterAt(def, 0, FloaterType.Blocked);
                ctx.tempHpAbsorbed = d;
                ctx.stopped = true;
                return 0;
            }
            else
            {
                d -= def.tempHealthBoost;
                def.currentHealth -= def.tempHealthBoost;
                ShowFloaterAt(def, 0, FloaterType.Blocked);
                ctx.tempHpAbsorbed = def.tempHealthBoost;
                def.tempHealthBoost = 0;
            }
        }

        // ── 领主(01503)重定向 ───────────────────────────────────────
        CardInstance lord = FindLordOnField(def);
        if (lord != null && def != lord && IsAlly(def)
            && !IsSilenced(lord))
        {
            lord.currentHealth -= d;
            ShowFloaterAt(def, 0, FloaterType.Blocked);
            ShowFloaterAt(lord, d, FloaterType.Damage);
            UpdateLordDisplay(lord);
            ctx.redirectedToLord = true;
            ctx.lordTarget = lord;
            ctx.stopped = true;
            return d;
        }

        // ── 勇者(01514) 追随者挡致命伤害 ─────────────────────────────
        if (!defenderSilenced && def.braveTemplateID == "01514"
            && def.currentHealth - d <= 0)
        {
            GameObject lastFollower = FindTopFollower(def);
            if (lastFollower != null)
            {
                RemoveFollower(lastFollower);
                def.currentHealth = 2;
                ShowFloaterAt(def, 0, FloaterType.Blocked);
                ReorderAttachments(GetHostSlotID(def));
                SyncAttachments(GetHostSlotID(def));
                ctx.negatedByFollower = true;
                ctx.stopped = true;
                return d;
            }
        }

        // ── 生命祭司(01507) 祝福复活 ─────────────────────────────────
        if (!defenderSilenced && def.hasLifePriestBlessing
            && def.currentHealth - d <= 0)
        {
            CardInstance priest = def.lifePriestBlessingSource;
            if (priest != null && !IsSilenced(priest))
            {
                def.hasLifePriestBlessing = false;
                def.lifePriestBlessingSource = null;
                def.currentHealth = def.currentMaxHealth;
                def.currentHealth += 2;
                def.currentMaxHealth += 2;
                def.currentAttack += 1;
                UpdateLordDisplay(def);
                CardData td = CardDatabase.Instance?.GetTemplate(def.templateID);
                if (td != null && td.hasOnEnter)
                {
                    BoardSlot targetSlot = FindSlotOf(def);
                    if (targetSlot != null)
                        targetSlot.StartOnEnterEffect(td, def);
                }
                ctx.revivedByPriest = true;
                ctx.priestSource = priest;
                ctx.stopped = true;
                return d;
            }
        }

        // ── 光环遍历 ────────────────────────────────────────────────
        var allAuras = GlobalEventManager.Instance?.GetAllAuras();
        if (allAuras != null)
            foreach (var aura in allAuras)
                if (aura != null && aura.IsActive())
                    d = aura.ModifyDamageIncoming(d, ctx);

        foreach (var m in Stage2Modifiers)
            d = m(d, ctx);
        return d;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stage3_FinalGive — 攻击方最终修正
    // ═══════════════════════════════════════════════════════════════════
    //
    //   - overclocked (02215 超频): 伤害 ×2
    //   - 阴阳攻击 (included in S1)
    //   - 反社会分子 (included in S1)

    static int Stage3_FinalGive(DamageContext ctx)
    {
        int d = ctx.damage;

        // ── 超频(02215): 攻击伤害 ×2 ────────────────────────────────
        if (ctx.Attacker != null && ctx.Attacker.overclocked
            && !IsSilenced(ctx.Attacker))
            d *= 2;

        foreach (var m in Stage3Modifiers)
            d = m(d, ctx);
        return d;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stage4_FinalReceive — 防守方最终修正
    // ═══════════════════════════════════════════════════════════════════
    //
    //   - 阴阳 03012: 受伤-1
    //   - 万象镜面 01512: clamp(1)

    static int Stage4_FinalReceive(DamageContext ctx)
    {
        int d = ctx.damage;
        var def = ctx.Defender;
        bool silenced = IsSilenced(def);

        // ── 万象镜面(01512): 单次最高为1 ────────────────────────────
        if (!silenced && def.templateID == "01512")
            d = Mathf.Min(d, 1);

        foreach (var m in Stage4Modifiers)
            d = m(d, ctx);
        return d;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stage5_Apply — 实际执行
    // ═══════════════════════════════════════════════════════════════════

    static DamageResult Stage5_Apply(DamageContext ctx)
    {
        var def = ctx.Defender;
        int actual = Mathf.Max(0, ctx.damage);

        // ── X 值累计 ────────────────────────────────────────────────
        if (def.isXValue)
            def.xAccumulatedDamage += actual;

        // ── 活化母巢(01534) 累计受伤 ────────────────────────────────
        if (def.templateID == "01534")
            def.totalDamageTaken += Mathf.Min(actual, def.currentHealth);

        // ── 征服者(01508) 本次战斗累计受伤 ──────────────────────────
        if (def.templateID == "01508" && !IsSilenced(def))
            def._conquerorTotalDamageThisBattle += actual;

        // ── 实际扣血 ─────────────────────────────────────────────────
        def.currentHealth -= actual;
        ShowFloaterAt(def, actual, FloaterType.Damage);

        // ── DamageSourceMarker ────────────────────────────────────────
        if (ctx.input.sourceObject != null)
            ctx.input.sourceObject.GetComponent<DamageSourceMarker>()
                ?.RegisterDamage(ctx.input.sourceObject, actual);

        return new DamageResult
        {
            finalDamage = actual,
            absorbedDamage = ctx.tempHpAbsorbed,
            lethal = def.currentHealth <= 0,
            redirectedToLord = ctx.redirectedToLord,
            negatedByFollower = ctx.negatedByFollower,
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════════════════════════════════

    static bool IsSilenced(CardInstance ci)
        => ci != null && GlobalEventManager.Instance != null
            && GlobalEventManager.Instance.IsFullySilenced(ci);

    static bool IsShadowHost(CardInstance ci)
        => ci != null && ci.templateID == "01502";

    static bool IsAlly(CardInstance ci)
    {
        int slot = GetSlotOf(ci);
        return slot >= 6 && slot <= 11;
    }

    static int GetSlotOf(CardInstance ci)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return -1;
        for (int i = 0; i < 12; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return i;
        }
        foreach (var obj in bm.attachedModels)
        {
            var c3d = obj?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance == ci && c3d.cardInstance.isAttached) return c3d.cardInstance.hostSlotID;
        }
        return -1;
    }

    static int GetHostSlotID(CardInstance ci)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return -1;
        for (int i = 0; i < 12; i++)
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return i;
        return -1;
    }

    static BoardSlot FindSlotOf(CardInstance ci)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        for (int i = 0; i < 12; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return s;
        }
        return null;
    }

    static bool HasBreakerOnField(CardInstance forCard)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null || forCard == null) return false;
        if (!BoardManager.GetSideRangeOf(forCard, out int brS, out int brE)) return false;
        for (int i = brS; i <= brE; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D == null) continue;
            var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01328" && !IsSilenced(ci))
                return true;
        }
        return false;
    }

    static int CountPrefixes(CardInstance ci)
    {
        if (ci == null || string.IsNullOrEmpty(ci.prefixes)) return 0;
        return ci.prefixes.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    static CardInstance FindLordOnField(CardInstance forCard)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null || forCard == null) return null;
        if (!BoardManager.GetSideRangeOf(forCard, out int ldS, out int ldE)) return null;
        for (int i = ldS; i <= ldE; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01503" && !ci.isAttached) return ci;
            }
        }
        return null;
    }

    static GameObject FindTopFollower(CardInstance host)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        int hostSlot = GetHostSlotID(host);
        GameObject last = null;
        int lastOrder = -1;
        foreach (var obj in bm.attachedModels)
        {
            var ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached && ci.hostSlotID == hostSlot && ci.templateID == "03001")
                if (ci.attachOrder > lastOrder) { lastOrder = ci.attachOrder; last = obj; }
        }
        return last;
    }

    static void RemoveFollower(GameObject follower)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        bm.attachedModels.Remove(follower);
        UnityEngine.Object.Destroy(follower);
    }

    static void ReorderAttachments(int hostSlotID)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        int order = 0;
        foreach (var obj in bm.attachedModels)
        {
            var ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached && ci.hostSlotID == hostSlotID)
                ci.attachOrder = order++;
        }
    }

    static void SyncAttachments(int hostSlotID)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        BoardManager.SyncAttachedModels(bm.GetSlot(hostSlotID));
    }

    static void UpdateDefenderValues(CardInstance def)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == def)
            {
                s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                return;
            }
        }
    }

    static void UpdateLordDisplay(CardInstance lord)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == lord)
            {
                s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                return;
            }
        }
    }

    static DamageResult BuildResult(DamageContext ctx) => new DamageResult
    {
        finalDamage = 0,
        absorbedDamage = ctx.tempHpAbsorbed > 0 ? ctx.tempHpAbsorbed : (ctx.shieldConsumed ? ctx.damage : 0),
        lethal = false,
        redirectedToLord = ctx.redirectedToLord,
        negatedByFollower = ctx.negatedByFollower,
    };

    // ═══════════════════════════════════════════════════════════════════
    // 浮动数字
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>在 CardInstance 的 3D 模型上方弹出浮动数字</summary>
    public static void ShowFloaterAt(CardInstance ci, int value, FloaterType type)
    {
        if (ci == null) return;
        Vector3 worldPos = GetWorldPosOf(ci);
        DamageFloater.Show(worldPos, value, type);
    }

    static Vector3 GetWorldPosOf(CardInstance ci)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return Vector3.zero;
        float offset = _floaterOffsetY ?? 2.5f;
        for (int i = 0; i < 12; i++)
        {
            var s = bm.GetSlot(i);
            var c3d = s?.currentCard3D?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance == ci && s.currentCard3D != null)
                return s.currentCard3D.transform.position + Vector3.up * offset;
        }
        return Vector3.zero;
    }

    static float? _floaterOffsetY;
    static float GetFloaterOffsetY()
    {
        if (!_floaterOffsetY.HasValue)
        {
            var cfg = Resources.Load<FloaterConfig>("FloaterConfig");
            _floaterOffsetY = cfg != null ? cfg.worldOffsetY : 2.5f;
        }
        return _floaterOffsetY.Value;
    }
}
