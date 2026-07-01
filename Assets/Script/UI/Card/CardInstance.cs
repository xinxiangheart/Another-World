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
   
    [Header("特性标记")]
    public bool hasOnEnter;
    public bool hasFirstStrike;
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
    public bool isWatcher; // 守望者标记
    public int consumedSpellCost; // 执行之剑消耗的法术费用
    public bool _rebornSummon;
    public List<string> enemyDamageSourceIDs = new List<string>();
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
    public List<string> mindScholarCopiedTraits; // 完整的特性文本
    public bool mindScholarEnterTriggeredThisPhase;
    public bool mindScholarDiscardTriggeredThisPhase;
    public int _conquerorTotalDamageThisBattle;
    public bool _conquerorPendingCheck;
    public GameObject _conquerorTargetEnemyCard;
    public bool immuneToEnemySpell;
    // 动态赋予的特性文本
    public List<string> grantedTraitTexts = new List<string>();
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
    public System.Action<int> _disasterWalkerHandler;
    // 受沉默控制的特性属性
    public bool HasOnEnter => hasOnEnter && !IsSilenced();
    public bool HasOnDeath => hasOnDeath && !IsSilenced();
    public bool HasActiveExit => hasActiveExit && !IsSilenced();
    public bool HasRevenge => hasRevenge && !IsSilenced();
    public bool HasDiscard => hasDiscard && !IsSilenced();
    public bool HasFirstStrike => hasFirstStrike && !IsSilenced();
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
    {
        if (poisoned) return;
        // 已有永久护盾，不能被非永久护盾顶替
        if (hasShield && shieldIsPermanent && !isPermanent)
            return;

        hasShield = true;
        shieldIsPermanent = isPermanent;
        shieldEndAtBattleStart = endAtBattleStart;
        shieldEndAtBattleEnd = endAtBattleEnd;
    }

    // 移除护盾
    public void RemoveShield()
    {
        hasShield = false;
        shieldIsPermanent = false;
        shieldEndAtBattleStart = false;
        shieldEndAtBattleEnd = false;
    }
    public void InitFromTemplate(CardData template, int copyIndex)
    {
        templateID = template.templateID;
        instanceID = templateID + (copyIndex + 1).ToString("D2");

        currentCost = template.baseCost;
        currentAttack = template.baseAttack;
        baseAttack = template.baseAttack;
        currentHealth = template.baseHealth;
        currentMaxHealth = template.baseHealth;
        baseHealth = template.baseHealth;
        baseMaxHealth = template.baseHealth;
        currentTier = template.baseTier;
        baseTier = template.baseTier;
        prefixes = template.prefix;
        summonType = template.summonType;
        CopyTraitsFromTemplate(template);

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
        }
        if (templateID == "01319")
            ignoreAllCounters = true;
        if (templateID == "01339")
            isWatcher = true;
        if (templateID == "01508")
            immuneToEnemySpell = true;
        if (templateID == "03026")
            cannotHeal = true;
        if (templateID == "01514")
            braveTemplateID = "01514";
        if (templateID == "01510")
            isAncientFairy = true;
        if (templateID == "01511")
            mindScholarCopiedTraits = new List<string>();
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
        hasOriginalFirstStrike = src.hasOriginalFirstStrike;
        hasOriginalOnEnter = src.hasOriginalOnEnter;
        hasOriginalOnDeath = src.hasOriginalOnDeath;
        hasOriginalActiveExit = src.hasOriginalActiveExit;
        hasOriginalRevenge = src.hasOriginalRevenge;
        hasOriginalDiscard = src.hasOriginalDiscard;
        hasOriginalAttach = src.hasOriginalAttach;
        hasOriginalAttacksFrontRow = src.hasOriginalAttacksFrontRow;
        hasOriginalAttacksBackRow = src.hasOriginalAttacksBackRow;
        damageSourceInstanceIDs = new List<string>(src.damageSourceInstanceIDs);
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
        mindScholarEnterTriggeredThisPhase = src.mindScholarEnterTriggeredThisPhase;
        mindScholarDiscardTriggeredThisPhase = src.mindScholarDiscardTriggeredThisPhase;
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
    public void GrantTrait(string fullTraitText)
    {
        if (grantedTraitTexts.Contains(fullTraitText)) return;
        grantedTraitTexts.Add(fullTraitText);

        if (fullTraitText.Contains("先手")) hasFirstStrike = true;
        if (fullTraitText.Contains("进场")) hasOnEnter = true;
        if (fullTraitText.Contains("退场")) hasOnDeath = true;
        if (fullTraitText.Contains("主动退场")) hasActiveExit = true;
        if (fullTraitText.Contains("反击")) hasRevenge = true;
        if (fullTraitText.Contains("抛置")) hasDiscard = true;
        if (fullTraitText.Contains("附着")) canAttach = true;
        if (fullTraitText.Contains("攻击前排")) { attacksFrontRow = true; attacksBackRow = false; }
        if (fullTraitText.Contains("攻击后排")) { attacksBackRow = true; attacksFrontRow = false; }
    }

    public void RemoveGrantedTrait(string fullTraitText)
    {
        grantedTraitTexts.Remove(fullTraitText);

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
    }
    /// <summary>
    /// 刷新该实例的2D/3D显示
    /// </summary>
    public void RefreshDisplay()
    {
        // 刷新2D手牌显示
        CardDisplay2D display2D = GetComponent<CardDisplay2D>();
        if (display2D != null) display2D.Refresh();

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
        if (cannotHeal) return;
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
        currentHealth = Mathf.Min(currentMaxHealth, currentHealth + amount);
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
    }

    public void AddTempAttack(int amount)
    {
        tempAttackBoost += amount;
        currentAttack += amount;
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
}