using System.Collections.Generic;
using UnityEngine;

public class CardInstance : MonoBehaviour
{
    public string instanceID;
    public string templateID;

    public int currentCost;
    public int currentAttack;
    public int baseAttack;
    public int baseHealth;
    public int baseMaxHealth;
    public int baseTier;
    public int currentHealth;
    public int currentMaxHealth;
    public int currentTier;
    public string prefixes = "";
    /// <summary>最近一次被赋予的新前缀（卡名变色规则1：以最后一次赋予的为准）。空=未赋予过。</summary>
    public string lastGivenPrefix = "";
    /// <summary>前缀 → 赋予者 templateID（4.6 来源记录）。只记来源、不加同步；空=未知/未赋予。不随区转移/模型重建复制。</summary>
    readonly Dictionary<string, string> _prefixSourceByPrefix = new Dictionary<string, string>();

    [Header("特性标记")]
    public bool hasOnEnter;
    public bool hasFirstStrike;
    /// <summary>本回合先手动作是否已消耗（5.x 拆出的瞬态字段，替代旧"先手消耗清 hasFirstStrike=false"写法）。
    /// 只本侧战斗结算写/读；不同步、不随 CopyFrom 复制（重建即默认 false=未消耗）。</summary>
    [System.NonSerialized] public bool _firstStrikeConsumed;
    public bool hasOnDeath;
    public bool hasActiveExit;
    public bool hasRevenge;
    public bool hasDiscard;
    public string revengeEffect;
    public int deathPhase; // 退场时所在的阶段数
    public bool isXValue;
    public bool xAttackReadsHighest;
    public bool xHealthReadsHighest;
    public int xAccumulatedDamage;
    public int costReduction;
    public bool buffedBySage;
    public SummonType summonType; 
    public bool enteredWithZeroCost;
    public int scrollCorePhaseCount; // 画卷之核在手牌中经过的阶段数
    public bool _conductorDoubleDeath; // 指挥家双倍退场标记
    public bool energyReaperDiscounted;
    public bool poisoned; // 本阶段无法获得护盾，受到任何伤害×2
    public int originalAttackBeforeDebuff;
    public int greedySnakeEnterCount;
    public int tempAttackBoost;   // 临时攻击力增幅（攻击回合结束清零）
    public int tempHealthBoost;   // 临时生命值增幅（攻击回合结束清零）
    public int xInitialHealth;
    public bool buffedByEmperor; 
    public bool merchantDiscounted;
    public string braveTemplateID; // 勇者标记，用于判断是否可被追随者抵挡
    public bool _justTransformed;
    public bool attacksBackRow;   // 攻击后排对位
    public bool attacksFrontRow;  // 攻击前排对位
    public int ironSmithTotalConsumedCount;   // 总共消耗召唤物数（跨阶段保留，用于触发强化）
    public int ironSmithOneCostConsumedCount; // 消耗1费召唤物数（每阶段清零，用于1费继续弹窗）
    public bool isShadow; // 影子标记
    public static int shadowLimit = 0; // 全局影子上限
    public static int shadowAtkBonus = 0; // 全局影子攻击力永久加成
    public static int shadowTierBonus = 0; // 全局影子阶位永久加成
    public static bool shadowMasterAlive = false; // 影舞者是否在场
    public string wolfKingInstanceID;
    public int totalDamageTaken; // 累计扣过的生命值（永久，只增不减）
    public bool isAncientFairy; // 古老精灵标记
    public int savedAttackForDiscard; // 不稳定实验品抛置用，保存点击时的攻击力
    public int savedTotalDamage; // 01534 活化母巢抛置用，保存 HandleDeath 前的累计受伤
    public bool isWatcher; // 守望者标记
    public int consumedSpellCost; // 执行之剑消耗的法术费用
    public bool _rebornSummon;
    public List<string> enemyDamageSourceIDs = new List<string>();
    /// <summary>反击快照：CheckAndHandleDeaths 在模型销毁前从 DamageSourceMarker 迁移到此处。
    /// 退场后同一同时窗口结束→下一个同时窗口反伤读取此快照。</summary>
    public List<string> revengeSnapshotIDs;
    public bool _outlawPlayerDamageThisTurn;
    public bool cannotHealOrGainMaxHP;
    public List<string> damageSourceInstanceIDs = new List<string>();
    public bool hasLifePriestBlessing; // 生命祭司祝福标记
    public CardInstance lifePriestBlessingSource; // 祝福来源（祭司）
    public bool _nourisherHost; // 是否是滋养者的宿主
    public string _nourisherInstanceID; // 滋养者的实例ID
    public bool _nourisherAttached; // 滋养者是否已附着
    public bool ignoreAllCounters; // 无畏者：不触发任何反制牌
    public bool _conquerorTriggered;
    public int mindScholarCopyCount;
    public List<string> mindScholarCopiedTraits; // 完整的特性文本: "{templateID}:{type}:{fullText}"
    public List<string> mindScholarTriggeredKeys; // 本阶段已触发的特性key: "{templateID}:{type}"
    public bool _mindScholarCopyPrompted; // 本次进场是否已弹出复制确认窗
    public int _conquerorTotalDamageThisBattle;
    public bool _conquerorPendingCheck;
    public GameObject _conquerorTargetEnemyCard;
    public bool immuneToEnemySpell;
    /// <summary>记录此卡被放置到槽位的时间（用于同步保护）。后续由 placementGeneration 替代。</summary>
    [System.NonSerialized] public float _placedAtTime = -999f;
    /// <summary>卡牌放置的单调递增世代号。用于替代时间窗口去重（如 EnsureCard 2s 保护）。</summary>
    [System.NonSerialized] public int placementGeneration;
    /// <summary>此卡是否已被 HandleDeath 处理过。用于替代 lastHandleDeathTime 时间窗口。</summary>
    [System.NonSerialized] public bool isDead;
    /// <summary>死亡时记录的当前世代。用于判断死亡事件是否已被同步处理。</summary>
    [System.NonSerialized] public int deathGeneration;
    /// <summary>最近一次被服务端同步确认的世代号（syncGen/attachGen）。-1=从未被服务端同步确认。
    /// EnsureEmpty 纯客户端兜底销毁守卫：只销毁已确认的残留模型，刚放置未确认的牌受保护。</summary>
    [System.NonSerialized] public int serverAckGen = -1;
    /// <summary>[Legacy] 进场效果正在执行中——死亡扫描应跳过此卡。后续由 NestingContext.IsNested 替代。</summary>
    [System.NonSerialized] public bool _enterEffectRunning;
    /// <summary>进场效果已运行过（一次性的持久标记，不清除）。用于保护玩家放置的卡不被网络同步覆盖。</summary>
    [System.NonSerialized] public bool _hadEnterEffect;
    /// <summary>[Legacy] 协程型进场效果尚未完成。后续由 EffectContext.StartedCoroutine + NestingContext 替代。</summary>
    [System.NonSerialized] public bool _hasPendingCoroutine;
    // 动态赋予的特性文本
    public List<string> grantedTraitTexts = new List<string>();
    /// <summary>结构化赋予特性（text + 属性 + 源模板ID），与 grantedTraitTexts 锁步维护（按 text 对齐）。</summary>
    public List<GrantedTrait> grantedTraits = new List<GrantedTrait>();
    /// <summary>目标侧状态来源记录：本卡被别的卡施加的 buff/debuff（来源+描述），悬停/详情按此显示。
    /// 与 grantedTraits 同等待遇参与网络序列化（";;"/"~" 分隔）。来源离场/时限到期时移除。</summary>
    public List<ActiveStatus> activeStatuses = new List<ActiveStatus>();
    /// <summary>特性组（每卡一个）：三层粒度查询（HasTrait/IsTraitActive/CanSend/CanReceive）+ 计数式多重禁制。
    /// InitFromTemplate/CopyFrom 构建（固有+授予+伪特性）；授予特性增删时 RefreshGranted 同步。旧 bool 并行保留、只读不写。</summary>
    [System.NonSerialized] public TraitGroup traits;
    /// <summary>特性组是否已按当前 silencedThisPhase 同步过（防重复 BlockAll 计数；构建时初始化对齐）。</summary>
    [System.NonSerialized] bool _silenceAppliedToTraits;
    // 苦难给予者专用
    public List<string> giveableDeathTraits = new List<string>();
    // 模板原始特性记录
    public bool hasOriginalFirstStrike;
    public bool hasOriginalOnEnter;
    public bool hasOriginalOnDeath;
    public bool hasOriginalActiveExit;
    public bool hasOriginalRevenge;
    public bool hasOriginalDiscard;
    public bool hasOriginalAttach;
    public bool hasOriginalAttacksFrontRow;
    public bool hasOriginalAttacksBackRow;
    // 退场自动回手专用：标记本次退场是否已被自动回手效果处理过
    public bool handledReturnToHand;
    public bool silencedThisPhase;
    public bool isActiveExit; // 本次退场是否为主动退场
    // 护盾
    public bool hasShield;
    public bool shieldIsPermanent;        // 永久持有（不被顶替，不受时间限制）
    public bool shieldEndAtBattleStart;   // 攻击回合开始消失
    public bool shieldEndAtBattleEnd;     // 攻击回合结束消失
    /// <summary>护盾来源 templateID（4.7 护盾 AddStatus 记录）；随 GrantShield/RemoveShield 维护。</summary>
    public string shieldSourceTemplateID = "";
    public bool isRevenge;
    // 附着系统
    public bool canAttach;          // 是否拥有附着特性（从模板读取）
    public bool isAttached;         // 当前是否附着在其他召唤物上
    public int hostSlotID = -1;     // 宿主的槽位ID（-1表示未附着）
    public int attachOrder;         // 该宿主上的第几个附着物（0开始）
    // 赋予护盾
    public enum CounterTriggerTiming
    {
        OnCardPlayed,       // 对方打出特定卡牌时
        OnPhaseEnd,         // 阶段结束时
        OnBattleEnd,        // 攻击回合结束时
        OnEnemyTurnEnd      // 对方回合结束时
    }
    /// <summary>无法恢复生命值</summary>
    public bool cannotHeal;

    /// <summary>受到的治疗量修正（正数为增强，负数为削弱，0为正常）</summary>
    public float healModifier = 1f;

    /// <summary>治疗来源类型</summary>
    public enum HealSourceType { Spell, Minion, Any }

    /// <summary>受到治疗时触发，返回实际治疗量。参数：(目标, 原始治疗量, 来源类型)</summary>
    public static event System.Func<CardInstance, int, HealSourceType, int> OnBeforeHeal;
    // 反制牌相关
    public CounterTriggerTiming counterTiming;
    public string counterTriggerCondition;  // 触发条件描述
    public string counterEffect;            // 触发效果描述
    public int counterDuration;             // 有效阶段数（-1表示永久直到触发）
    public bool isYinYang; // 阴阳标记，受到伤害-1
    public bool overclocked;
    // Buff/Debuff 持续状态（从 CardData 模板继承，可被光环等动态赋予）
    public bool hasBuff;
    public string buffText;
    public bool hasDebuff;
    public string debuffText;
    public System.Action<int> _disasterWalkerHandler;
    // 受沉默控制的特性属性
    /// <summary>进场（5.x 已迁特性组）：hasOnEnter 保留数据拥有；特性组 HasActiveClass("进场") 叠加激活（未沉默/未单条禁）。
    /// 进场效果分发门在 template（放置瞬间/召唤路径用 template.hasOnEnter + StartOnEnterEffect:1106 的 IsTraitBlocked 现查），本属性只供"按实例现态判断进场能力"的侧方判断（投机者 01125、AI 评分）使用——被沉默/被禁的进场特性不计。</summary>
    public bool HasOnEnter => hasOnEnter && (traits != null ? traits.HasActiveClass("进场") : true) && !IsSilenced();
    /// <summary>退场亡语（5.x 已迁特性组）：hasOnDeath 兼任"武装/瞬态抑制"——死亡时刻 守墓人01330/法官01323 禁退场时清 false（GlobalDeathEventHandler:26-27、BoardSlot:1480-1490），此处必须保留该判定。
    /// 特性组 HasActiveClass("退场") 叠加拥有+激活（未沉默/未单条禁）。旧 bool 并行保留。</summary>
    public bool HasOnDeath => hasOnDeath && (traits != null ? traits.HasActiveClass("退场") : true) && !IsSilenced();
    /// <summary>主动退场（5.x 已迁特性组）：同上，hasActiveExit 保留武装瞬态（守墓人/法官成对清零 + 未弃之人:1507 清 hasOnDeath 时自身不受清）。特性组查"主动退场"类。</summary>
    public bool HasActiveExit => hasActiveExit && (traits != null ? traits.HasActiveClass("主动退场") : true) && !IsSilenced();
    /// <summary>反击（5.x 已迁特性组）：hasRevenge 仍作"武装/瞬态抑制"标志——抛置/变形/战斗消耗等清零处写 false 抑制本次死亡不反伤，此处必须保留该判定。
    /// 特性组 HasActiveClass("反击") 叠加拥有+激活（未沉默 BlockAll/未单条禁），新增"被沉默/被禁的反击不触发"（与死亡类效果被沉默一致）。
    /// 旧 hasRevenge bool 并行保留（图标/数据传播/复制拥有用）。</summary>
    public bool HasRevenge => hasRevenge && (traits != null ? traits.HasActiveClass("反击") : true) && !IsSilenced();
    /// <summary>抛置（5.x 已迁特性组）：拥有抛置类特性且激活（未沉默/未单条禁）。旧 hasDiscard bool 并行保留（图标/数据传播用）。
    /// 特性组 HasActiveClass 已含 BlockAll(沉默) 与单条禁；外层叠加实时全沉默查询，与旧语义（hasDiscard && !IsSilenced）完全等价。
    /// 光环"禁抛置"（萨满01515）为持续现查，不在此属性，由抛置动作入口另判 IsTraitBlocked("抛置")。</summary>
    public bool HasDiscard => (traits != null ? traits.HasActiveClass("抛置") : hasDiscard) && !IsSilenced();
    /// <summary>先手（5.x 已迁特性组）：hasFirstStrike=数据拥有（不再被回合消耗清零）；_firstStrikeConsumed=本回合已行动瞬态（战斗清零点置 true、TurnManager 回合边界置 false）。
    /// 特性组 HasActiveClass("先手") 叠加激活（未沉默/未单条禁）。旧 bool 并行保留（图标/数据/同步用）。</summary>
    public bool HasFirstStrike => hasFirstStrike && !_firstStrikeConsumed
        && (traits != null ? traits.HasActiveClass("先手") : true) && !IsSilenced();
    /// <summary>附着动作门（5.x）：canAttach 数据 only，**不加 IsSilenced**——附着动作不受沉默影响，仍可附到宿主。
    /// 附着效果（瞬间 buff/持续行为）是否受沉默由各自效果门/事件门判定，与动作门分离。</summary>
    public bool CanAttach => canAttach;
    public bool HasShield() => hasShield;
    public int prisonMySlot = -1;
    public int prisonEnemySlot = -1;
    bool IsSilenced()
    {
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(this);
    }
    public void ClearAllTraits()
    {
        hasFirstStrike = false;
        hasOnEnter = false;
        hasOnDeath = false;
        hasActiveExit = false;
        hasRevenge = false;
        hasDiscard = false;
        canAttach = false;
        attacksFrontRow = false;
        attacksBackRow = false;
        grantedTraitTexts.Clear();
        giveableDeathTraits?.Clear();
    }
    public void GrantShield(bool isPermanent, bool endAtBattleStart, bool endAtBattleEnd)
        => GrantShield(isPermanent, endAtBattleStart, endAtBattleEnd, null);

    /// <summary>赋予护盾。sourceTemplateID = 施加者 templateID（4.7，self 盾传自身）。护盾消失（RemoveShield）时清来源状态。</summary>
    public void GrantShield(bool isPermanent, bool endAtBattleStart, bool endAtBattleEnd, string sourceTemplateID)
    {
        if (poisoned) return;
        // 已有永久护盾，不能被非永久护盾顶替
        if (hasShield && shieldIsPermanent && !isPermanent)
            return;
        // 4.7 更换护盾：先清旧来源状态
        if (!string.IsNullOrEmpty(shieldSourceTemplateID))
            RemoveStatusBySource(shieldSourceTemplateID);
        shieldSourceTemplateID = "";

        hasShield = true;
        shieldIsPermanent = isPermanent;
        shieldEndAtBattleStart = endAtBattleStart;
        shieldEndAtBattleEnd = endAtBattleEnd;

        // 4.7 护盾 AddStatus：只接 永久/攻击回合 两型（第三类 all-false 描述为空 → 不接）
        string desc = ShieldStatusDescription();
        if (!string.IsNullOrEmpty(desc) && !string.IsNullOrEmpty(sourceTemplateID))
        {
            shieldSourceTemplateID = sourceTemplateID;
            AddStatus(false, desc, sourceTemplateID);
        }
    }

    /// <summary>护盾来源状态描述：永久→「护盾」；攻击回合开始/结束消→「护盾（至本次攻击回合结束）」；其余(本阶段类)空=暂不接。</summary>
    string ShieldStatusDescription()
    {
        if (shieldIsPermanent) return "护盾";
        if (shieldEndAtBattleStart || shieldEndAtBattleEnd) return "护盾（至本次攻击回合结束）";
        return "";
    }

    // 移除护盾
    public void RemoveShield()
    {
        hasShield = false;
        shieldIsPermanent = false;
        shieldEndAtBattleStart = false;
        shieldEndAtBattleEnd = false;
        // 4.7 护盾消失（被格挡/到期/拆盾）：清来源状态
        if (!string.IsNullOrEmpty(shieldSourceTemplateID))
            RemoveStatusBySource(shieldSourceTemplateID);
        shieldSourceTemplateID = "";
    }

    // ================= Buff/Debuff 动态状态（光环/效果等授予） =================

    /// <summary>动态赋予正面持续增益。text 为空则沿用现有描述。</summary>
    public void GrantBuff(string text = null)
    {
        hasBuff = true;
        if (!string.IsNullOrEmpty(text)) buffText = text;
        RefreshDisplay();
    }

    /// <summary>清除正面持续增益，恢复模板默认（无模板则清空）。</summary>
    public void ClearBuff()
    {
        CardData t = CardDatabase.Instance != null ? CardDatabase.Instance.GetTemplate(templateID) : null;
        if (t != null) { hasBuff = t.hasBuff; buffText = t.buffText; }
        else { hasBuff = false; buffText = ""; }
        RefreshDisplay();
    }

    /// <summary>动态赋予负面持续减益。text 为空则沿用现有描述。</summary>
    public void GrantDebuff(string text = null)
    {
        hasDebuff = true;
        if (!string.IsNullOrEmpty(text)) debuffText = text;
        RefreshDisplay();
    }

    /// <summary>清除负面持续减益，恢复模板默认（无模板则清空）。</summary>
    public void ClearDebuff()
    {
        CardData t = CardDatabase.Instance != null ? CardDatabase.Instance.GetTemplate(templateID) : null;
        if (t != null) { hasDebuff = t.hasDebuff; debuffText = t.debuffText; }
        else { hasDebuff = false; debuffText = ""; }
        RefreshDisplay();
    }
    /// <summary>使用预生成 instanceID 初始化（从牌库抽取时使用）。</summary>
    public void InitFromTemplate(CardData template, int copyIndex, string overrideInstanceID = null)
    {
        templateID = template.templateID;
        // 优先使用预生成的唯一 instanceID（CardZoneManager 生成），其次旧格式
        instanceID = overrideInstanceID ?? (templateID + (copyIndex + 1).ToString("D2"));

        // 初始化所有可变集合（同步 diff 需非 null）
        grantedTraitTexts = new List<string>();
        grantedTraits = new List<GrantedTrait>();
        activeStatuses = new List<ActiveStatus>();
        giveableDeathTraits = new List<string>();
        enemyDamageSourceIDs = new List<string>();
        damageSourceInstanceIDs = new List<string>();
        revengeSnapshotIDs = new List<string>();

        currentCost = template.baseCost;
        currentAttack = Mathf.Max(0, template.baseAttack);
        baseAttack = Mathf.Max(0, template.baseAttack);
        currentHealth = Mathf.Max(0, template.baseHealth);
        currentMaxHealth = Mathf.Max(0, template.baseHealth);
        baseHealth = template.baseHealth;
        baseMaxHealth = template.baseHealth;
        currentTier = template.baseTier;
        baseTier = template.baseTier;
        prefixes = template.prefix;
        lastGivenPrefix = ""; // 模板前缀非"赋予"——不触发卡名变色
        summonType = template.summonType;
        CopyTraitsFromTemplate(template);

        // 继承模板的 Buff/Debuff 持续状态（可被光环等动态覆盖）
        hasBuff = template.hasBuff;
        buffText = template.buffText;
        hasDebuff = template.hasDebuff;
        debuffText = template.debuffText;

        if (templateID == "01117")
        {
            giveableDeathTraits = new List<string>
        {
            "退场：减一能量",
            "退场：己方全体受到一伤害",
            "退场：己方玩家扣一血"
        };
            grantedTraitTexts = new List<string>
        {
            "退场：减一能量",
            "退场：己方全体受到一伤害",
            "退场：己方玩家扣一血"
        };
            grantedTraits = new List<GrantedTrait>();
            foreach (var t in grantedTraitTexts)
                grantedTraits.Add(new GrantedTrait { text = t });
        }
        if (templateID == "01319")
            ignoreAllCounters = true;
        if (templateID == "01339")
            isWatcher = true;
        if (templateID == "01508")
            immuneToEnemySpell = true;
        if (templateID == "01514")
            braveTemplateID = "01514";
        if (templateID == "01510")
            isAncientFairy = true;
        if (templateID == "01511" && mindScholarCopiedTraits == null)
        {
            mindScholarCopiedTraits = new List<string>();
            mindScholarTriggeredKeys = new List<string>();
        }

        traits = TraitGroup.BuildFrom(this); // 特性组构建（固有 + 授予 + 伪特性）
        _silenceAppliedToTraits = silencedThisPhase; // 守卫对齐当前沉默态
        if (silencedThisPhase && traits != null) traits.BlockAll(this);
    }
    public void CopyFrom(CardInstance src)
    {
        templateID = src.templateID;
        instanceID = src.instanceID;
        currentCost = src.currentCost;
        currentAttack = src.currentAttack;
        baseAttack = src.baseAttack;
        currentHealth = src.currentHealth;
        currentMaxHealth = src.currentMaxHealth;
        baseHealth = src.baseHealth;
        baseMaxHealth = src.baseMaxHealth;
        currentTier = src.currentTier;
        baseTier = src.baseTier;
        prefixes = src.prefixes;
        lastGivenPrefix = src.lastGivenPrefix; // 卡名变色状态随实例复制
        summonType = src.summonType;
        hasOnEnter = src.hasOnEnter;
        hasOnDeath = src.hasOnDeath;
        hasActiveExit = src.hasActiveExit;
        hasRevenge = src.hasRevenge;
        hasDiscard = src.hasDiscard;
        hasFirstStrike = src.hasFirstStrike;
        canAttach = src.canAttach;
        attacksFrontRow = src.attacksFrontRow;
        attacksBackRow = src.attacksBackRow;
        isXValue = src.isXValue;
        xAttackReadsHighest = src.xAttackReadsHighest;
        xHealthReadsHighest = src.xHealthReadsHighest;
        isYinYang = src.isYinYang;
        revengeEffect = src.revengeEffect;
        buffedBySage = src.buffedBySage;
        buffedByEmperor = src.buffedByEmperor;
        costReduction = src.costReduction;
        enteredWithZeroCost = src.enteredWithZeroCost;
        handledReturnToHand = false;
        silencedThisPhase = src.silencedThisPhase;
        poisoned = src.poisoned;
        isActiveExit = src.isActiveExit;
        xAccumulatedDamage = src.xAccumulatedDamage;
        xInitialHealth = src.xInitialHealth;
        tempAttackBoost = src.tempAttackBoost;
        tempHealthBoost = src.tempHealthBoost;
        hasShield = src.hasShield;
        shieldIsPermanent = src.shieldIsPermanent;
        shieldEndAtBattleStart = src.shieldEndAtBattleStart;
        shieldEndAtBattleEnd = src.shieldEndAtBattleEnd;
        giveableDeathTraits = src.giveableDeathTraits != null ? new List<string>(src.giveableDeathTraits) : new List<string>();
        grantedTraitTexts = src.grantedTraitTexts != null ? new List<string>(src.grantedTraitTexts) : new List<string>();
        grantedTraits = src.grantedTraits != null
            ? src.grantedTraits.ConvertAll(g => new GrantedTrait
            {
                text = g?.text,
                attributes = g?.attributes != null ? new List<string>(g.attributes) : new List<string>(),
                sourceTemplateID = g?.sourceTemplateID ?? ""
            })
            : new List<GrantedTrait>();
        activeStatuses = src.activeStatuses != null
            ? src.activeStatuses.ConvertAll(a => new ActiveStatus
            {
                isDebuff = a != null && a.isDebuff,
                description = a?.description ?? "",
                sourceName = a?.sourceName ?? "",
                sourceID = a?.sourceID ?? ""
            })
            : new List<ActiveStatus>();
        hasOriginalFirstStrike = src.hasOriginalFirstStrike;
        hasOriginalOnEnter = src.hasOriginalOnEnter;
        hasOriginalOnDeath = src.hasOriginalOnDeath;
        hasOriginalActiveExit = src.hasOriginalActiveExit;
        hasOriginalRevenge = src.hasOriginalRevenge;
        hasOriginalDiscard = src.hasOriginalDiscard;
        hasOriginalAttach = src.hasOriginalAttach;
        hasOriginalAttacksFrontRow = src.hasOriginalAttacksFrontRow;
        hasOriginalAttacksBackRow = src.hasOriginalAttacksBackRow;
        damageSourceInstanceIDs = src.damageSourceInstanceIDs != null ? new List<string>(src.damageSourceInstanceIDs) : new List<string>();
        cannotHeal = src.cannotHeal;
        overclocked = src.overclocked;
        originalAttackBeforeDebuff = src.originalAttackBeforeDebuff;
        greedySnakeEnterCount = src.greedySnakeEnterCount;
        merchantDiscounted = src.merchantDiscounted;
        braveTemplateID = src.braveTemplateID;
        scrollCorePhaseCount = src.scrollCorePhaseCount;
        ironSmithTotalConsumedCount = src.ironSmithTotalConsumedCount;
        ironSmithOneCostConsumedCount = src.ironSmithOneCostConsumedCount;
        _justTransformed = src._justTransformed;
        prisonMySlot = src.prisonMySlot;
        prisonEnemySlot = src.prisonEnemySlot;
        energyReaperDiscounted = src.energyReaperDiscounted;
        _conductorDoubleDeath = src._conductorDoubleDeath;
        isShadow = src.isShadow;
        wolfKingInstanceID = src.wolfKingInstanceID;
        isAncientFairy = src.isAncientFairy;
        totalDamageTaken = src.totalDamageTaken;
        consumedSpellCost = src.consumedSpellCost;
        _outlawPlayerDamageThisTurn = src._outlawPlayerDamageThisTurn;
        enemyDamageSourceIDs = src.enemyDamageSourceIDs != null ? new List<string>(src.enemyDamageSourceIDs) : new List<string>();
        cannotHealOrGainMaxHP = src.cannotHealOrGainMaxHP;
        hasLifePriestBlessing = src.hasLifePriestBlessing;
        lifePriestBlessingSource = src.lifePriestBlessingSource;
        _conquerorTriggered = src._conquerorTriggered;
        _conquerorTotalDamageThisBattle = src._conquerorTotalDamageThisBattle;
        _conquerorPendingCheck = src._conquerorPendingCheck;
        _conquerorTargetEnemyCard = src._conquerorTargetEnemyCard;
        _nourisherHost = src._nourisherHost;
        _nourisherInstanceID = src._nourisherInstanceID;
        _nourisherAttached = src._nourisherAttached;
        isWatcher = src.isWatcher;
        ignoreAllCounters = src.ignoreAllCounters;
        mindScholarCopyCount = src.mindScholarCopyCount;
        mindScholarCopiedTraits = src.mindScholarCopiedTraits != null ? new List<string>(src.mindScholarCopiedTraits) : new List<string>();
        mindScholarTriggeredKeys = src.mindScholarTriggeredKeys != null ? new List<string>(src.mindScholarTriggeredKeys) : null;
        // _mindScholarCopyPrompted intentionally NOT copied — fresh placement gets fresh dialog
        hasBuff = src.hasBuff;
        buffText = src.buffText;
        hasDebuff = src.hasDebuff;
        debuffText = src.debuffText;

        traits = TraitGroup.BuildFrom(this); // 特性组构建（复制后按新模板 + 已复制授予重建）
        _silenceAppliedToTraits = silencedThisPhase; // 守卫对齐当前沉默态
        if (silencedThisPhase && traits != null) traits.BlockAll(this);
    }
    public void CopyTraitsFromTemplate(CardData template)
    {
        hasOnEnter = template.hasOnEnter;
        hasFirstStrike = template.hasFirstStrike;
        hasOnDeath = template.hasOnDeath;
        hasActiveExit = template.hasActiveExit;
        hasRevenge = template.hasRevenge;
        hasDiscard = template.hasDiscard;
        revengeEffect = template.revengeEffect;
        canAttach = template.canAttach;
        attacksBackRow = template.attacksBackRow;
        attacksFrontRow = template.attacksFrontRow;
        isXValue = template.isXValue;
        xAttackReadsHighest = template.xAttackReadsHighest;
        xHealthReadsHighest = template.xHealthReadsHighest;

        hasOriginalFirstStrike = template.hasFirstStrike;
        hasOriginalOnEnter = template.hasOnEnter;
        hasOriginalOnDeath = template.hasOnDeath;
        hasOriginalActiveExit = template.hasActiveExit;
        hasOriginalRevenge = template.hasRevenge;
        hasOriginalDiscard = template.hasDiscard;
        hasOriginalAttach = template.canAttach;
        hasOriginalAttacksFrontRow = template.attacksFrontRow;
        hasOriginalAttacksBackRow = template.attacksBackRow;
    }
    public void AddTrait(string trait)
    {
        switch (trait)
        {
            case "先手": hasFirstStrike = true; break;
            case "进场": hasOnEnter = true; break;
            case "退场": hasOnDeath = true; break;
            case "主动退场": hasActiveExit = true; break;
            case "反击": hasRevenge = true; break;
            case "抛置": hasDiscard = true; break;
            case "附着": canAttach = true; break;
            case "攻击前排": attacksFrontRow = true; attacksBackRow = false; break;
            case "攻击后排": attacksBackRow = true; attacksFrontRow = false; break;
        }
    }

    public void RemoveTrait(string trait)
    {
        switch (trait)
        {
            case "先手": hasFirstStrike = false; break;
            case "进场": hasOnEnter = false; break;
            case "退场": hasOnDeath = false; break;
            case "主动退场": hasActiveExit = false; break;
            case "反击": hasRevenge = false; break;
            case "抛置": hasDiscard = false; break;
            case "附着":
                canAttach = false;
                if (isAttached)
                {
                    BoardManager bm = FindObjectOfType<BoardManager>();
                    if (bm != null)
                    {
                        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
                        {
                            Card3DInstance c3d = bm.attachedModels[i]?.GetComponent<Card3DInstance>();
                            if (c3d?.cardInstance == this)
                            {
                                GameObject obj = bm.attachedModels[i];
                                bm.attachedModels.RemoveAt(i);
                                Destroy(obj);
                                break;
                            }
                        }
                    }
                    isAttached = false;
                    hostSlotID = -1;
                    attachOrder = 0;
                }
                break;
            case "攻击前排": attacksFrontRow = false; break;
            case "攻击后排": attacksBackRow = false; break;
        }
    }
    /// <summary>赋予特性（纯文本，兼容旧调用）。</summary>
    public void GrantTrait(string fullTraitText) => GrantTrait(fullTraitText, null, null);

    /// <summary>赋予特性（结构化：text + 属性 + 源模板ID），同时写 grantedTraitTexts 与 grantedTraits。</summary>
    public void GrantTrait(string text, List<string> attributes, string sourceTemplateID)
    {
        if (grantedTraitTexts == null) grantedTraitTexts = new List<string>();
        if (grantedTraits == null) grantedTraits = new List<GrantedTrait>();
        if (grantedTraitTexts.Contains(text)) return;
        grantedTraitTexts.Add(text);
        grantedTraits.Add(new GrantedTrait
        {
            text = text,
            attributes = attributes != null ? new List<string>(attributes) : new List<string>(),
            sourceTemplateID = sourceTemplateID ?? ""
        });

        if (text.Contains("先手")) hasFirstStrike = true;
        if (text.Contains("进场")) hasOnEnter = true;
        if (text.Contains("退场")) hasOnDeath = true;
        if (text.Contains("主动退场")) hasActiveExit = true;
        if (text.Contains("反击")) hasRevenge = true;
        if (text.Contains("抛置")) hasDiscard = true;
        if (text.Contains("附着")) canAttach = true;
        if (text.Contains("攻击前排")) { attacksFrontRow = true; attacksBackRow = false; }
        if (text.Contains("攻击后排")) { attacksBackRow = true; attacksFrontRow = false; }

        traits?.RefreshGranted(); // 特性组同步授予
    }

    public void RemoveGrantedTrait(string fullTraitText)
    {
        if (grantedTraitTexts == null) grantedTraitTexts = new List<string>();
        grantedTraitTexts.Remove(fullTraitText);
        if (grantedTraits != null) grantedTraits.RemoveAll(g => g != null && g.text == fullTraitText);

        bool stillHasFirstStrike = grantedTraitTexts.Exists(t => t.Contains("先手"));
        bool stillHasOnEnter = grantedTraitTexts.Exists(t => t.Contains("进场"));
        bool stillHasOnDeath = grantedTraitTexts.Exists(t => t.Contains("退场"));
        bool stillHasActiveExit = grantedTraitTexts.Exists(t => t.Contains("主动退场"));
        bool stillHasRevenge = grantedTraitTexts.Exists(t => t.Contains("反击"));
        bool stillHasDiscard = grantedTraitTexts.Exists(t => t.Contains("抛置"));
        bool stillHasAttach = grantedTraitTexts.Exists(t => t.Contains("附着"));
        bool stillHasAttackFront = grantedTraitTexts.Exists(t => t.Contains("攻击前排"));
        bool stillHasAttackBack = grantedTraitTexts.Exists(t => t.Contains("攻击后排"));

        if (!stillHasFirstStrike) hasFirstStrike = hasOriginalFirstStrike;
        if (!stillHasOnEnter) hasOnEnter = hasOriginalOnEnter;
        if (!stillHasOnDeath) hasOnDeath = hasOriginalOnDeath;
        if (!stillHasActiveExit) hasActiveExit = hasOriginalActiveExit;
        if (!stillHasRevenge) hasRevenge = hasOriginalRevenge;
        if (!stillHasDiscard) hasDiscard = hasOriginalDiscard;
        if (!stillHasAttach) canAttach = hasOriginalAttach;
        if (!stillHasAttackFront && !stillHasAttackBack) attacksFrontRow = hasOriginalAttacksFrontRow;
        if (!stillHasAttackBack && !stillHasAttackFront) attacksBackRow = hasOriginalAttacksBackRow;

        traits?.RefreshGranted(); // 特性组同步授予移除
    }

    /// <summary>按来源 templateID 移除全部由该来源授予的特性（5.x，01336 修正者附着授予用）。
    /// 附着授予只在宿主仍被该附着物附着时有意义——宿主离场回手等场景须清掉，防幻影特性重打。
    /// 逐条走 RemoveGrantedTrait：同步 grantedTraitTexts/grantedTraits、复位 hasX 到模板原始值、特性组 RefreshGranted。</summary>
    public void RemoveGrantedTraitsBySource(string sourceTemplateID)
    {
        if (string.IsNullOrEmpty(sourceTemplateID) || grantedTraits == null) return;
        var texts = new List<string>();
        foreach (var g in grantedTraits)
            if (g != null && g.sourceTemplateID == sourceTemplateID && !string.IsNullOrEmpty(g.text))
                texts.Add(g.text);
        foreach (var t in texts)
            RemoveGrantedTrait(t);
    }
    /// <summary>
    /// 刷新该实例的2D/3D显示
    /// </summary>
    public void RefreshDisplay()
    {
        // 刷新2D手牌显示
        CardDisplay2D display2D = GetComponent<CardDisplay2D>();
        if (display2D != null) display2D.Refresh();
        // 新2D手牌显示
        CardDisplay2DNew display2DNew = GetComponent<CardDisplay2DNew>();
        if (display2DNew != null) display2DNew.Refresh();

        // 刷新3D战场显示
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D != null)
                {
                    Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance == this)
                    {
                        c3d.UpdateValues();
                        break;
                    }
                }
            }
        }
    }
    /// <summary>对召唤物进行治疗（统一入口）</summary>
    public void ReceiveHeal(int amount, HealSourceType sourceType)
    {
        // 禁疗：外部状态 cannotHeal（留给外部写入） + 特性组 receiveBlocks（禁疗特性拦截 Healed）
        if (cannotHeal || (traits != null && !traits.CanReceive(EffectCategory.Healed))) return;
        if (isAttached) return;

        // 事件拦截/修正
        if (OnBeforeHeal != null)
        {
            amount = OnBeforeHeal(this, amount, sourceType);
        }
        if (amount <= 0) return;

        // 应用治疗修正
        amount = Mathf.RoundToInt(amount * healModifier);
        if (amount <= 0) return;
        if (templateID == "01512") amount = Mathf.Min(amount, 1);
        int actualHeal = Mathf.Min(currentMaxHealth - currentHealth, amount);
        currentHealth += actualHeal;
        if (actualHeal > 0) DamagePipeline.ShowFloaterAt(this, actualHeal, FloaterType.Heal);
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            for (int i = 0; i < 12; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D != null)
                {
                    Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance == this) { c3d.UpdateValues(); return; }
                }
            }
        }
    }
    public void AddTempHealth(int amount)
    {
        tempHealthBoost += amount;
        currentHealth += amount;
        if (amount > 0) DamagePipeline.ShowFloaterAt(this, amount, FloaterType.Heal);
        else if (amount < 0) DamagePipeline.ShowFloaterAt(this, -amount, FloaterType.Debuff);
    }

    public void AddTempAttack(int amount)
    {
        tempAttackBoost += amount;
        currentAttack += amount;
        if (amount > 0) DamagePipeline.ShowFloaterAt(this, amount, FloaterType.Buff);
        else if (amount < 0) DamagePipeline.ShowFloaterAt(this, -amount, FloaterType.Debuff);
    }
    public bool CanTriggerTrait(string keyword)
    {
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(this))
            return false;
        return HasTrait(keyword);
    }

    public bool HasTrait(string keyword)
    {
        switch (keyword)
        {
            case "先手": if (hasFirstStrike) return true; break;
            case "进场": if (hasOnEnter) return true; break;
            case "退场": if (hasOnDeath) return true; break;
            case "主动退场": if (hasActiveExit) return true; break;
            case "反击": if (hasRevenge) return true; break;
            case "抛置": if (hasDiscard) return true; break;
            case "附着": if (canAttach) return true; break;
            case "攻击前排": if (attacksFrontRow) return true; break;
            case "攻击后排": if (attacksBackRow) return true; break;
            case "阶段开始": if (templateID == "01525" || templateID == "01526" || templateID == "03001") return true; break;
            case "回合开始": if (templateID == "01113" || templateID == "01315" || templateID == "01302" || templateID == "01105") return true; break;
            case "战斗回合开始": if (templateID == "01308") return true; break;
        }

        if (grantedTraitTexts.Exists(t => t.Contains(keyword))) return true;

        CardData td = CardDatabase.Instance?.GetTemplate(templateID);
        if (td != null && !string.IsNullOrEmpty(td.traits) && td.traits.Contains(keyword)) return true;

        return false;
    }

    // ═══════════════════ 特性条目化（显示用） ═══════════════════

    /// <summary>一条可见特性的显示条目。</summary>
    public struct TraitEntry
    {
        public bool isGranted;      // true=获得的赋予特性（运行时从别的卡获得）
        public string text;         // 特性文本（无"数字."前缀）
        public string[] attributes; // 属性列表（进场/先手/反击/…/赋予）
    }

    /// <summary>结构化赋予特性（复制时从源卡 List&lt;TraitEntry&gt; 读取属性并携带）。</summary>
    [System.Serializable]
    public class GrantedTrait
    {
        public string text;                    // 特性文本
        public List<string> attributes;        // 属性标记（从源卡 List<TraitEntry 精确复制）
        public string sourceTemplateID;        // 源卡模板ID（溯源）
    }

    /// <summary>目标侧状态来源记录：别的卡给本卡施加的一条 buff/debuff。</summary>
    [System.Serializable]
    public class ActiveStatus
    {
        public bool isDebuff;        // 是否为减益（用于图标区分，文本不强制带前缀）
        public string description;   // 状态描述（如"攻击力临时+2"）
        public string sourceName;    // 来源卡名（显示用；查不到时回退 sourceID）
        public string sourceID;      // 来源卡模板ID（溯源/来源离场清理）
    }

    /// <summary>构建可见特性列表：固有（跳过"赋予"标记条目）+ 获得的赋予特性。编号在显示时按此顺序生成。</summary>
    public List<TraitEntry> GetVisibleTraitEntries()
    {
        var result = new List<TraitEntry>();
        CardData template = CardDatabase.Instance != null ? CardDatabase.Instance.GetTemplate(templateID) : null;

        // 固有特性（结构化 traitEntries，加载时自动迁移）——01117 迁移前用旧特殊处理
        if (templateID != "01117" && template != null)
        {
            var traitList = template.GetTraitEntryList();
            if (traitList != null)
                foreach (var te in traitList)
                {
                    if (te.isGrant) continue; // 赋予型（给予别人）：自身不显示不编号
                    result.Add(new TraitEntry { isGranted = false, text = te.text, attributes = te.GetAttributes() });
                }
        }

        // 01117 苦难给予者：数据迁移前临时保留旧显示（2 条自身特性；给予型在 grantedTraitTexts 由下方显示）
        if (templateID == "01117")
        {
            result.Insert(0, new TraitEntry { isGranted = false, text = "进场：永久给予对方一召唤物一个自己的退场（自己的退场给予后消失）", attributes = new string[0] });
            result.Insert(1, new TraitEntry { isGranted = false, text = "退场：回到手牌（该退场无法给予）", attributes = new string[0] });
        }

        // 获得的赋予特性（运行时从别的卡获得）——正常参与编号；属性优先结构化 grantedTraits，旧纯文本回退前缀解析
        if (grantedTraitTexts != null)
        {
            foreach (string g in grantedTraitTexts)
            {
                if (string.IsNullOrEmpty(g)) continue;
                string[] attrs = null;
                if (grantedTraits != null)
                {
                    var gt = grantedTraits.Find(x => x != null && x.text == g);
                    if (gt != null && gt.attributes != null && gt.attributes.Count > 0)
                        attrs = gt.attributes.ToArray();
                }
                if (attrs == null || attrs.Length == 0)
                    attrs = CardData.ParseTraitAttributesFromText(g);
                result.Add(new TraitEntry { isGranted = true, text = g, attributes = attrs });
            }
        }

        return result;
    }

    /// <summary>可见特性总数（1 基，跳过"赋予"标记条目）。</summary>
    public int GetTraitCount() => GetVisibleTraitEntries().Count;

    /// <summary>第 index 条可见特性的文本（1 基）。越界返回 null。</summary>
    public string GetTraitByIndex(int index)
    {
        var entries = GetVisibleTraitEntries();
        if (index < 1 || index > entries.Count) return null;
        return entries[index - 1].text;
    }

    /// <summary>第 index 条可见特性的属性列表（1 基）。越界返回空数组。</summary>
    public string[] GetTraitProperties(int index)
    {
        var entries = GetVisibleTraitEntries();
        if (index < 1 || index > entries.Count) return new string[0];
        return entries[index - 1].attributes;
    }

    /// <summary>效果级溯源：按关键字查特性的可见序号（1基，第N条）。找不到返回-1。
    /// 用于伤害来源记录生成"来自第N条特性（先手：xxx）"。</summary>
    public int GetTraitIndexByKeyword(string keyword)
    {
        var entries = GetVisibleTraitEntries();
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].text.Contains(keyword)) return i + 1;
        return -1;
    }

    /// <summary>特性组同步沉默：按 synced silencedThisPhase 值派生 BlockAll/UnblockAll。
    /// 双端都调用（施法端设值后 + 对端板面同步应用后）——靠同一 synced 值收敛，保证 traits 一致。
    /// 值未变不重复计数（防每帧/每次板面同步重复 BlockAll）。不改旧 silencedThisPhase/IsFullySilenced 逻辑。</summary>
    public void ApplySilenceToTraits()
    {
        if (traits == null) return;
        bool want = silencedThisPhase;
        if (want == _silenceAppliedToTraits) return;
        _silenceAppliedToTraits = want;
        if (want) traits.BlockAll(this);       // 沉默 → 禁所有发送（CanSend/IsTraitActive 失效）
        else traits.UnblockAll(this);          // 解除 → 恢复
    }

    /// <summary>格式化一条特性条目为 "N：属性1、属性2：xxx" / "N：xxx" / "N（赋予）（属性）：xxx"。</summary>
    public static string FormatTraitEntry(int n, TraitEntry e)
    {
        // 去重：text 以首个属性+"："开头时剥掉，避免 "先手：先手："
        string cleaned = e.text;
        if (e.attributes != null && e.attributes.Length > 0
            && cleaned.StartsWith(e.attributes[0] + "："))
            cleaned = cleaned.Substring(e.attributes[0].Length + 1).TrimStart();

        if (e.isGranted)
        {
            string attr = e.attributes != null && e.attributes.Length > 0 ? $"（{string.Join("、", e.attributes)}）" : "";
            return $"{n}（赋予）{attr}：{cleaned}";
        }
        string attrPart = e.attributes != null && e.attributes.Length > 0 ? string.Join("、", e.attributes) + "：" : "";
        return $"{n}：{attrPart}{cleaned}";
    }

    // ═══════════════════ granted traits 同步序列化 ═══════════════════

    /// <summary>序列化 grantedTraits → ";;" 分隔，每项 "text~属性1、属性2~源templateID"；旧纯文本 grantedTraitTexts 兜底。</summary>
    public string SerializeGrantedTraits()
    {
        if (grantedTraits != null && grantedTraits.Count > 0)
        {
            var parts = new List<string>();
            foreach (var gt in grantedTraits)
            {
                if (gt == null) continue;
                string attrs = gt.attributes != null && gt.attributes.Count > 0 ? string.Join("、", gt.attributes) : "";
                parts.Add($"{gt.text}~{attrs}~{gt.sourceTemplateID ?? ""}");
            }
            return parts.Count > 0 ? string.Join(";;", parts) : "";
        }
        // 旧纯文本兜底（grantedTraits 尚未填充）
        return grantedTraitTexts != null ? string.Join(";;", grantedTraitTexts) : "";
    }

    /// <summary>解析 ";;" 分隔的序列化 granted traits → (text, attrs, source) 列表。兼容无 "~" 的旧纯文本。</summary>
    public List<(string text, List<string> attrs, string source)> ParseGrantedTraits(string raw)
    {
        var result = new List<(string, List<string>, string)>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var entry in raw.Split(new[] { ";;" }, System.StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(entry)) continue;
            if (entry.Contains("~"))
            {
                var p = entry.Split('~');
                string text = p[0];
                var attrs = p.Length > 1 && !string.IsNullOrEmpty(p[1])
                    ? new List<string>(p[1].Split(new[] { '、', ',', '，' }, System.StringSplitOptions.RemoveEmptyEntries))
                    : new List<string>();
                string source = p.Length > 2 ? p[2] : "";
                result.Add((text, attrs, source));
            }
            else
            {
                result.Add((entry, new List<string>(), ""));
            }
        }
        return result;
    }

    /// <summary>用同步数据增量更新 grantedTraits/grantedTraitTexts（diff 式，保留 flag 恢复逻辑）。</summary>
    public void ApplySyncedGrantedTraits(string raw)
    {
        var newEntries = ParseGrantedTraits(raw);
        var newTexts = new List<string>();
        foreach (var e in newEntries) newTexts.Add(e.Item1);

        if (grantedTraitTexts == null) grantedTraitTexts = new List<string>();
        if (grantedTraits == null) grantedTraits = new List<GrantedTrait>();

        // 移除旧的不再存在的
        var oldCopy = new List<string>(grantedTraitTexts);
        foreach (var t in oldCopy)
            if (!newTexts.Contains(t)) RemoveGrantedTrait(t);

        // 添加新的 / 更新已有属性
        foreach (var e in newEntries)
        {
            int idx = grantedTraitTexts.IndexOf(e.Item1);
            if (idx >= 0)
            {
                if (idx < grantedTraits.Count)
                {
                    if (e.Item2 != null && e.Item2.Count > 0) grantedTraits[idx].attributes = e.Item2;
                    if (!string.IsNullOrEmpty(e.Item3)) grantedTraits[idx].sourceTemplateID = e.Item3;
                }
                continue;
            }
            GrantTrait(e.Item1, e.Item2, e.Item3);
        }
    }

    // ═══════════════════ 目标侧状态来源记录（activeStatuses）═══════════════════

    /// <summary>记录一条别的卡给本卡施加的状态。同 sourceID + 同 description 去重（叠加态数值合入 description，不重复加条目）。
    /// source 为 null 时按纯文本记录（无来源，仅显示，不清除/不走来源离场清理）。</summary>
    public void AddStatus(bool isDebuff, string description, CardInstance source)
    {
        AddStatus(isDebuff, description, source != null ? source.templateID : "");
    }

    /// <summary>按来源模板ID记录状态（法术/无实例来源用：源卡不在场也能记，且可按ID精准移除）。sourceID 空 = 纯文本无来源。</summary>
    public void AddStatus(bool isDebuff, string description, string sourceTemplateID)
    {
        if (activeStatuses == null) activeStatuses = new List<ActiveStatus>();
        if (string.IsNullOrEmpty(description)) return;

        string srcID = sourceTemplateID ?? "";
        string srcName = string.IsNullOrEmpty(srcID) ? "" : GetCardName(srcID);
        foreach (var a in activeStatuses)
            if (a != null && a.sourceID == srcID && a.description == description)
                return; // 同来源同描述已存在 → 不重复

        activeStatuses.Add(new ActiveStatus
        {
            isDebuff = isDebuff,
            description = description,
            sourceName = srcName,
            sourceID = srcID
        });
    }

    /// <summary>移除某来源卡施加的全部状态（来源离场/失效时调用）。sourceID 为空 → 移除所有无来源项。</summary>
    public void RemoveStatusBySource(string sourceID)
    {
        if (activeStatuses == null) return;
        activeStatuses.RemoveAll(a => a == null || a.sourceID == sourceID);
    }

    /// <summary>序列化 activeStatuses → ";;" 分隔，每项 "isDebuff~description~sourceName~sourceID"。</summary>
    public string SerializeActiveStatuses()
    {
        if (activeStatuses == null || activeStatuses.Count == 0) return "";
        var parts = new List<string>();
        foreach (var a in activeStatuses)
        {
            if (a == null) continue;
            parts.Add($"{(a.isDebuff ? "1" : "0")}~{a.description ?? ""}~{a.sourceName ?? ""}~{a.sourceID ?? ""}");
        }
        return parts.Count > 0 ? string.Join(";;", parts) : "";
    }

    /// <summary>解析 ";;" 分隔的 activeStatuses 串 → 条目列表。</summary>
    public List<ActiveStatus> ParseActiveStatuses(string raw)
    {
        var result = new List<ActiveStatus>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var entry in raw.Split(new[] { ";;" }, System.StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(entry)) continue;
            var p = entry.Split('~');
            result.Add(new ActiveStatus
            {
                isDebuff = p.Length > 0 && p[0] == "1",
                description = p.Length > 1 ? p[1] : "",
                sourceName = p.Length > 2 ? p[2] : "",
                sourceID = p.Length > 3 ? p[3] : ""
            });
        }
        return result;
    }

    /// <summary>用同步数据全量刷新 activeStatuses（diff 式：去重追加 + 移除不存在的；来源名以接收端解析为准，
    /// 保留发送端填好的 sourceName，缺失时按 sourceID 现查）。</summary>
    public void ApplySyncedActiveStatuses(string raw)
    {
        var incoming = ParseActiveStatuses(raw);
        if (activeStatuses == null) activeStatuses = new List<ActiveStatus>();

        // 移除发送端已没有的（按 description+sourceID 匹配）
        activeStatuses.RemoveAll(a =>
            a != null && !incoming.Exists(b => b != null && b.description == a.description && b.sourceID == a.sourceID));

        // 追加新的
        foreach (var b in incoming)
        {
            if (b == null || string.IsNullOrEmpty(b.description)) continue;
            if (activeStatuses.Exists(a => a != null && a.description == b.description && a.sourceID == b.sourceID)) continue;
            if (string.IsNullOrEmpty(b.sourceName)) b.sourceName = GetCardName(b.sourceID);
            activeStatuses.Add(b);
        }
    }

    /// <summary>按模板ID取卡名（来源显示用）；查不到回退 ID 本身。</summary>
    public static string GetCardName(string templateID)
        => string.IsNullOrEmpty(templateID) ? ""
         : CardDatabase.Instance?.GetTemplate(templateID)?.cardName ?? templateID;

    // ═══════════════════ 附着物反向引用（4.5，方案A：只读查询，复用 attachedModels）═══════════════════
    // 附着物关系键是 hostSlotID（宿主所在槽）；attachedModels 即板级权威附件集合。
    // 宿主反向知道"谁附着在我身上" = 扫 attachedModels 中 hostSlotID == 自己当前槽 的附着物。
    // 不加独立字段、不加同步状态——宿主换位/重建/跨端天然自一致。

    /// <summary>本卡作为宿主时，返回附着在它身上的附着物 CardInstance 列表（无宿主/附着中/不在场 → 空）。</summary>
    public List<CardInstance> GetHostedAttachments()
    {
        var result = new List<CardInstance>();
        int hostSlot = FindHostSlotIndex();
        if (hostSlot < 0) return result;
        BoardManager bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return result;
        foreach (GameObject obj in bm.attachedModels)
        {
            if (obj == null) continue;
            var aci = obj.GetComponent<Card3DInstance>()?.cardInstance;
            if (aci != null && aci.isAttached && aci.hostSlotID == hostSlot)
                result.Add(aci);
        }
        return result;
    }

    /// <summary>宿主附着物 instanceID 列表（等价于 GetHostedAttachments 取 instanceID）。</summary>
    public List<string> GetHostedAttachmentInstanceIDs()
    {
        var ids = new List<string>();
        foreach (var a in GetHostedAttachments())
            if (a != null && !string.IsNullOrEmpty(a.instanceID)) ids.Add(a.instanceID);
        return ids;
    }

    /// <summary>本卡当前占据的槽位号；附着中（无独立槽）或不在场返回 -1。</summary>
    int FindHostSlotIndex()
    {
        if (isAttached) return -1; // 附着物作为附着体无独立宿主槽
        BoardManager bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm == null) return -1;
        for (int i = 0; i < 12; i++)
        {
            var c3d = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance == this) return i;
        }
        return -1;
    }

    public int Attack
    {
        get
        {
            if (templateID == "01512" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(this)))
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        BoardSlot s = bm.GetSlot(i);
                        if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == this)
                        {
                            int opponentID = i < 6 ? i + 6 : i - 6;
                            BoardSlot os = bm.GetSlot(opponentID);
                            if (os?.currentCard3D != null)
                            {
                                CardInstance oppCI = os.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                if (oppCI != null) return oppCI.currentAttack;
                            }
                            return 0;
                        }
                    }
                }
            }
            return currentAttack;
        }
    }

    // ═══════════════════ 文字动态变色（2D/3D 通用，只作用文本） ═══════════════════

    /// <summary>五前缀对应卡名颜色：渊=紫 灵能=蓝 神灵画卷=绿 血歌=红 机械=棕。</summary>
    static readonly Dictionary<string, Color> PrefixNameColors = new Dictionary<string, Color>
    {
        { "渊",     new Color(0.70f, 0.30f, 0.90f) }, // 紫
        { "灵能",   new Color(0.30f, 0.50f, 1.00f) }, // 蓝
        { "神灵画卷", new Color(0.20f, 0.80f, 0.35f) }, // 绿
        { "血歌",   new Color(0.90f, 0.25f, 0.25f) }, // 红
        { "机械",   new Color(0.62f, 0.44f, 0.26f) }, // 棕
    };
    static readonly Color CostLowerColor  = new Color(0.20f, 0.80f, 0.30f); // 费用低于基础 → 绿
    static readonly Color CostHigherColor = new Color(0.90f, 0.25f, 0.25f); // 费用高于基础 → 红
    static readonly Color HealthLowColor  = new Color(0.90f, 0.25f, 0.25f); // 生命≤基础一半 → 红
    static readonly Color AttackHighColor = new Color(1.00f, 0.84f, 0.00f); // 攻击高于基础 → 金
    static readonly Color AttackLowColor  = new Color(0.60f, 0.60f, 0.60f); // 攻击低于基础 → 灰

    /// <summary>赋予一个新前缀（规则1）：已有则不变色；追加到 prefixes 并记录为最后一次赋予（卡名变色以它为准）。sourceID=赋予者 templateID（4.6，只记来源不接 AddStatus）；空=未知。</summary>
    public void GivePrefix(string prefix) => GivePrefix(prefix, null);

    public void GivePrefix(string prefix, string sourceTemplateID)
    {
        if (string.IsNullOrEmpty(prefix) || prefix == "无") return;
        string p = prefix.Trim();
        if (prefixes != null && prefixes.Contains(p)) return; // 重复赋予已有前缀 → 不变色
        if (string.IsNullOrEmpty(prefixes) || prefixes == "无")
            prefixes = p;
        else
            prefixes = prefixes + " " + p;
        lastGivenPrefix = p; // 新前缀 → 以最后一次赋予的为准
        if (!string.IsNullOrEmpty(sourceTemplateID))
            _prefixSourceByPrefix[p] = sourceTemplateID; // 4.6 前缀赋予者来源记录
        RefreshDisplay();
    }

    /// <summary>某前缀的赋予者来源 templateID；未赋予过/未知返回空串。</summary>
    public string GetPrefixSource(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return "";
        return _prefixSourceByPrefix.TryGetValue(prefix, out var s) ? s : "";
    }

    /// <summary>规则1：卡名颜色。新前缀赋予 → 前缀对应色；未赋予过 → 默认白。</summary>
    public Color GetNameColor()
    {
        if (!string.IsNullOrEmpty(lastGivenPrefix) && PrefixNameColors.TryGetValue(lastGivenPrefix, out var c))
            return c;
        return Color.white;
    }

    /// <summary>当前有效费用（含商户/能量收割者减费光环的显示折扣）。手牌 CostText/费用底图/费用变色共用，
    /// 保证显示与变色一致（旧 CardDisplay2D 的 displayCost 同款逻辑）。场上卡两个减费标志已清零 → 等于 currentCost。</summary>
    public int GetDisplayCost()
    {
        int cost = currentCost;
        if (merchantDiscounted && NetworkPlayer.Local != null && NetworkPlayer.Local.IsMerchantOnFieldPublic())
            cost = Mathf.Max(0, cost - 1);
        if (energyReaperDiscounted && NetworkPlayer.Local != null && NetworkPlayer.Local.IsEnergyReaperOnFieldPublic())
            cost = Mathf.Max(0, cost - 1);
        return cost;
    }

    /// <summary>规则2：费用颜色。当前有效费用低于模板基础 → 绿；高于 → 红；相等 → 默认。
    /// 实例不存 baseCost（只有 currentCost/costReduction），基础费用从模板读取。</summary>
    public Color GetCostColor()
    {
        int baseCost = CardDatabase.Instance?.GetTemplate(templateID)?.baseCost ?? currentCost;
        int displayCost = GetDisplayCost(); // 与 CostText 显示一致（含减费光环）
        if (displayCost < baseCost) return CostLowerColor;
        if (displayCost > baseCost) return CostHigherColor;
        return Color.white;
    }

    /// <summary>规则3：生命颜色。当前生命 ≤ 基础生命1/2（向下取整）→ 红；高于一半 → 默认。</summary>
    public Color GetHealthColor()
    {
        if (currentHealth <= baseHealth / 2) return HealthLowColor; // 整数除法天然向下取整
        return Color.white;
    }

    /// <summary>规则4：攻击颜色。当前攻击高于基础 → 金；低于 → 灰；相等 → 默认。
    /// 用 Attack 属性（与显示一致，01512 特殊取对位攻击）。</summary>
    public Color GetAttackColor()
    {
        int atk = Attack;
        if (atk > baseAttack) return AttackHighColor;
        if (atk < baseAttack) return AttackLowColor;
        return Color.white;
    }
}