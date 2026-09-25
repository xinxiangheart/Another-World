using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class CounterManager : MonoBehaviour
{
    public static CounterManager Instance { get; private set; }

    // 己方打出的反制牌
    public List<CounterCard> myCounters = new List<CounterCard>();
    // 敌方打出的反制牌（己方视角看到牌背）
    public List<CounterCard> enemyCounters = new List<CounterCard>();

    [Header("反制牌摆放（世界坐标；两摞都在屏幕左侧：己方在下、对方在上，沿棋盘中线 y=1 镜像）")]
    [Tooltip("己方反制牌第一张的落点（左下；y 与 enemyCounterBase 关于 y=1 镜像，两摞共用同一条竖线）")]
    [SerializeField] Vector3 myCounterBase = new Vector3(-7.1f, -0.4f, -6f);
    [Tooltip("己方每多一张的偏移（+X = 往棋盘中间走，免得挤到屏幕外）")]
    [SerializeField] Vector3 myCounterStep = new Vector3(0.5f, 0f, -0.1f);
    [Tooltip("对方反制牌第一张的落点（左上）")]
    [SerializeField] Vector3 enemyCounterBase = new Vector3(-7.1f, 2.4f, -6f);
    [Tooltip("对方每多一张的偏移（+X = 往棋盘中间走）")]
    [SerializeField] Vector3 enemyCounterStep = new Vector3(0.5f, 0f, -0.1f);

    /// <summary>
    /// 某一侧第 index 张反制牌的世界坐标。服务端与客户端共用这一份 ——
    /// 以前 TargetSpawnCounterCard 自己写死过一份坐标，两边一改就对不上。
    /// </summary>
    public Vector3 GetCounterPosition(bool isMine, int index)
    {
        return isMine ? myCounterBase + myCounterStep * index
                      : enemyCounterBase + enemyCounterStep * index;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ========== 打出一张反制牌 ==========
   public Coroutine PlayCounter(GameObject cardObject, bool isMine)
{
    CardInstance inst = cardObject.GetComponent<CardInstance>();
    if (inst == null) return null;

    CardData template = CardDatabase.Instance?.GetTemplate(inst.templateID);
    Debug.Log($"PlayCounter 被调用：inst.templateID={inst.templateID}, template={template?.cardName}");

    GameObject prefab = template.spellPrefab3D != null ? template.spellPrefab3D : template.prefab3D;
    Debug.Log($"PlayCounter 选择的预制体：{(prefab != null ? prefab.name : "null")}");

    if (prefab == null)
    {
        Debug.Log("PlayCounter 失败：prefab 为 null");
        return null;
    }

    int count = isMine ? myCounters.Count : enemyCounters.Count;
    Vector3 pos = GetCounterPosition(isMine, count);

    Quaternion rotation = isMine ? Quaternion.Euler(0, 180, 0) : Quaternion.Euler(0, 0, 0);
    GameObject model = Instantiate(prefab, pos, rotation);
    Player.Scale3DModel(model);
    model.name = inst.instanceID + "_counter";
    Debug.Log($"模型生成成功：{model.name}, 位置：{model.transform.position}");

    CounterCard counter = new CounterCard();
    counter.model = model;
    counter.template = template;
    counter.isMine = isMine;
    counter.remainingDuration = template.counterDuration;
        // 02305/02306(OnEnemyTurnEnd)：CheckOnEnemyTurnEnd 每轮被调两次（先手结束后一次、后手结束后一次），
        // 所以「持续 2」= 维持一个完整轮次，落点在**对手回合结束**那一刻 —— 正是卡面「对方下回合结束」。
        // 旧写法按 isMyTurnFirst 把先手方压成 1 → 在自己回合结束就归零，读的是对手上一轮的遗留能量；
        // 客户端镜像(TargetSpawnCounterCard)从不做这个修正，两端还会对不上。统一用模板值。
        if (template.counterTiming == CounterTriggerTiming.OnCardPlayed)
        {
            counter.decreaseTiming = CounterTriggerTiming.OnPhaseEnd;
        }
        else if (template.counterTiming == CounterTriggerTiming.OnPhaseStart)
        {
            counter.decreaseTiming = CounterTriggerTiming.OnPhaseStart;
        }
        else if (template.counterTiming == CounterTriggerTiming.OnPlayerDying)
        {
            counter.decreaseTiming = CounterTriggerTiming.OnPlayerDying;
            counter.remainingDuration = -1; // 永不到期，只等条件触发
        }
        else
        {
            counter.decreaseTiming = template.counterTiming;
        }

        if (isMine)
    {
        myCounters.Add(counter);
        // Copy CardInstance data to 3D model so CardDisplay3D shows real card info
        Card3DInstance c3d = model.GetComponent<Card3DInstance>();
        if (c3d == null) c3d = model.AddComponent<Card3DInstance>();
        CardInstance copy = model.GetComponent<CardInstance>();
        if (copy == null) copy = model.AddComponent<CardInstance>(); // 新 3D 预制体根已带 CardInstance，勿重复 Add（否则命中空模板）
        // Ensure Card3DHover exists so hover detail panel works for owner
        if (model.GetComponent<Card3DHover>() == null)
            model.AddComponent<Card3DHover>();
        copy.CopyFrom(inst);
        c3d.cardInstance = copy;
        // Force refresh display text (name/cost/effect/prefix) from the real card
        c3d.UpdateValues();
        // Owner sees counter normally (re-enables hover + visible text)
        Card3DHover.SetHidden(model, false, false);
        // Re-read cardInstance after late assignment — Start() captured stale prefab data
        Card3DHover hover = model.GetComponent<Card3DHover>();
        if (hover != null) hover.RefreshCardData();
        Debug.Log($"[PlayCounter-probe] model={model.name} c3dCI={(c3d!=null && c3d.cardInstance!=null)} copyTid={copy?.templateID} prefab={template?.spellPrefab3D?.name}");
    }
    else
    {
        enemyCounters.Add(counter);
        // Opponent's counter is hidden — flipped, no text, no panel
        Card3DHover.SetHidden(model, true, false);
    }

    // [打出展示] 反制打出 → 卡背读模型统一隐藏源（非持有方=背、持有方=正），自动跟随隐藏机制
    PlayRevealManager.Show(template, PlayRevealManager.IsHiddenBack(model));

    Debug.Log($"反制牌已生成，己方数量：{myCounters.Count}");
        // 守望者(01339)：对方打出反制牌立即触发 —— isMine=false 表示 Remote/AI 打出 → 守望者在本地(6-11)
        // 返回这次判定的协程，交给调用方（SimpleAI 反制分支）await：
        // 反制判定要等一帧才弹玩家的选目标，AI 不等就会抢在玩家点选前出下一张牌 / 结束回合。
        if (!isMine)
        {
            // 反制牌「打出后就造成伤害」→ 即时触发（不等结算静默）
            HandManager hmWatcher = FindObjectOfType<HandManager>();
            if (hmWatcher != null) return hmWatcher.StartCoroutine(hmWatcher.WatcherCounterCheckFor(false));
        }
        return null;
    }

    // ========== 即时触发检测（离线模式，FakeEnemyPlayButton 调用） ==========
    public void CheckOnCardPlayed(CardData playedCard)
    {
        // Only used in offline mode — online goes through ServerCheckOnCardPlayed in CmdPlayCard.
        if (NetworkServer.active || NetworkClient.isConnected) return;
        // Offline: FakeEnemyPlayButton simulates enemy playing → check player's own counters
        ServerCheckOnCardPlayed(playedCard, false);
    }

    /// <summary>Server-side: check counters matching a played card. hostPlayed = who played the card.
    /// playedInst 可选：能拿到打出卡 CardInstance 时传入以查特性组（反制免疫 receiveBlocks=Countered）；
    /// 拿不到（Host 分支/离线 AI）传 null → 回退旧 templateID=="01319" 硬编码。</summary>
    public void ServerCheckOnCardPlayed(CardData playedCard, bool hostPlayed, CardInstance playedInst = null)
    {
        _redirectedTemplateID = null;   // 每次判定重置：只反映「刚刚这一次」打出
        // 反制免疫：打出卡不可被反制 → 该召唤物不触发任何反制牌。
        // 特性组优先（无畏者01319 固有 receiveBlocks=Countered；被禁/沉默则失效恢复可被反制）；
        // 无实例（Host 分支/AI）回退旧 templateID 硬编码。
        if (playedInst != null && playedInst.traits != null)
        {
            if (!playedInst.traits.CanReceive(EffectCategory.Countered)) return;
        }
        else if (playedCard != null && playedCard.templateID == "01319")
        {
            return;
        }

        if (hostPlayed)
        {
            // Host played — check Remote's counters (enemyCounters). Effect benefits Remote.
            for (int i = enemyCounters.Count - 1; i >= 0; i--)
            {
                CounterCard counter = enemyCounters[i];
                if (counter.template.counterTiming != CounterTriggerTiming.OnCardPlayed) continue;
                if (!MatchCondition(counter, playedCard)) continue;

                // 蛊惑之音 special: redirect enter effect to Remote's ally
                if (counter.template.templateID == "02304")
                {
                    GlobalEventManager.Instance.PendingEnterRedirectTemplate = playedCard;
                    GlobalEventManager.Instance.PendingEnterRedirectToHost = false;
                    CounterOwner(false).AddEnergy(1);
                    _redirectedTemplateID = playedCard.templateID;
                }

                TriggerCounter(counter, i, false);
            }
        }
        else
        {
            // Remote played — check Host's counters (myCounters). Effect benefits Host.
            for (int i = myCounters.Count - 1; i >= 0; i--)
            {
                CounterCard counter = myCounters[i];
                if (counter.template.counterTiming != CounterTriggerTiming.OnCardPlayed) continue;
                if (!MatchCondition(counter, playedCard)) continue;

                // 蛊惑之音 special: redirect enter effect to Host's ally
                if (counter.template.templateID == "02304")
                {
                    GlobalEventManager.Instance.PendingEnterRedirectTemplate = playedCard;
                    GlobalEventManager.Instance.PendingEnterRedirectToHost = true;
                    CounterOwner(true).AddEnergy(1);
                    _redirectedTemplateID = playedCard.templateID;
                }

                TriggerCounter(counter, i, true);
            }
        }
    }

    /// <summary>
    /// 法术「打出」统一上报 —— AI 施法(SimpleAI.PlaySpell) / 学徒01329 / 谜语人01321 / 辉煌法师01521
    /// 这些**不经 ServerPlayCard** 的法术路径必须调用，否则 OnCardPlayed 系反制（02101/02102/02304）漏触发。
    /// 人类拖放路径由 CardDrag.ResolveSpellEffect 调本函数（HandManager.RemoveCard 对法术不再上报，防双触发）。
    /// caster：本次施法者（RunAsLocal 期间即 NetworkPlayer.Local）；null = 取 NetworkPlayer.Local。
    /// </summary>
    public static void NotifySpellPlayed(CardData template, NetworkPlayer caster = null)
    {
        if (template == null) return;
        if ((template.spellType & SpellType.Counter) != 0) return;   // 反制牌本身不触发反制

        NetworkPlayer who = caster != null ? caster : NetworkPlayer.Local;
        if (NetworkServer.active)
        {
            CounterManager.Instance?.ServerCheckOnCardPlayed(template, who == NetworkPlayer.LocalHalfPlayer);
        }
        else if (NetworkClient.isConnected && who != null && who.isLocalPlayer)
        {
            // 纯客户端本地执行的法术（needsLocalUI / 代打）服务端不知道这次打出 → 补报
            who.CmdNotifySpellPlayed(template.templateID);
        }
    }

    /// <summary>
    /// 02302 反制克星的服务端权威结算：把 caster 对手持有的 templateID 反制「无效果触发」
    /// （扣对手能量 + 移除 + 同步对手），再给 caster 打出一张复制品（触发时只扣 1 能量）。
    /// 纯客户端在本地只做视觉，权威必须走这里；Host 本地路径（CounterKillerEffect）也复用同一套。
    /// </summary>
    public void ServerCounterKiller(bool casterIsHost, string templateID)
    {
        if (!NetworkServer.active || string.IsNullOrEmpty(templateID)) return;

        List<CounterCard> list = casterIsHost ? enemyCounters : myCounters;   // 「对手持有」的那一摞
        bool victimIsMine = !casterIsHost;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i]?.template == null || list[i].template.templateID != templateID) continue;
            CardData tpl = list[i].template;
            ExpireWithNoEffect(list[i], i, victimIsMine);          // 对方反制无效果触发（权威扣费+移除+同步）
            PlayCounterWithReducedCost(tpl, 1, casterIsHost);      // 己方复制品
            // 复制品只在本机生成 → 对方客户端要补模型
            if (casterIsHost && NetworkPlayer.Remote != null && NetworkPlayer.Remote.connectionToClient != null)
                NetworkPlayer.Remote.TargetSpawnCounterCard(NetworkPlayer.Remote.connectionToClient, tpl.templateID);
            return;
        }
        Debug.LogWarning($"[02302] 服务端未找到要无效果触发的反制牌: {templateID}");
    }

    // 02304：最近一次判定里「被蛊惑之音重定向」的打出卡 templateID（一次性，供 AI 出召唤物分支消费）。
    string _redirectedTemplateID;

    /// <summary>AI 出召唤物专用：这张牌这次的进场效果是不是刚被 02304 重定向了？
    /// AI 的落位不走 BoardSlot.OnPointerClick，拿不到 PendingEnterRedirectInstance —— 不跳过的话
    /// 会「AI 自己跑一次进场 + 对面 TargetHandleEnterRedirect 又代跑一次」。消费后即清。</summary>
    public bool ConsumeEnterRedirected(string templateID)
    {
        if (string.IsNullOrEmpty(templateID) || _redirectedTemplateID != templateID) return false;
        _redirectedTemplateID = null;
        return true;
    }

    /// <summary>02304 客户端方向预判：纯客户端本地拿不到服务端的重定向标记（ServerPlayCard 在其之后才跑），
    /// 用「本地已知的对手反制」（TargetSpawnCounterCard 镜像进 enemyCounters）预判这张牌会不会被重定向。
    /// 只作预判：服务端随后用 TargetEnterRedirectVerdict 回执修正（猜错就还原本端进场效果）。</summary>
    public bool OpponentHasLiveRedirectCounter(CardData playedCard)
    {
        if (playedCard == null || enemyCounters == null) return false;
        for (int i = 0; i < enemyCounters.Count; i++)
        {
            CounterCard c = enemyCounters[i];
            if (c?.template == null) continue;
            if (c.template.templateID != "02304") continue;
            if (c.template.counterTiming != CounterTriggerTiming.OnCardPlayed) continue;
            if (MatchCondition(c, playedCard)) return true;
        }
        return false;
    }

    // ========== 阶段开始检测 ==========
    public void CheckOnPhaseStart()
    {
        if (!NetworkServer.active) return;

        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnPhaseStart)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                ResolveCounterExpiry(counter, i, true);
            }
        }

        for (int i = enemyCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = enemyCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnPhaseStart)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                ResolveCounterExpiry(counter, i, false);
            }
        }
    }

    // ========== 阶段结束检测 ==========
    public void CheckOnPhaseEnd()
    {
        if (!NetworkServer.active) return;

        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnPhaseEnd)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                ResolveCounterExpiry(counter, i, true);
            }
        }

        for (int i = enemyCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = enemyCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnPhaseEnd)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                ResolveCounterExpiry(counter, i, false);
            }
        }
    }

    // ========== 攻击回合结束检测 ==========
    public void CheckOnBattleEnd()
    {
        if (!NetworkServer.active) return;

        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnBattleEnd)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                ResolveCounterExpiry(counter, i, true);
            }
        }

        for (int i = enemyCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = enemyCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnBattleEnd)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                ResolveCounterExpiry(counter, i, false);
            }
        }
    }

    // ========== 对方回合结束检测 ==========
    public void CheckOnEnemyTurnEnd()
    {
        if (!NetworkServer.active) return;

        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnEnemyTurnEnd)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                // 片甲不留：对方能量为0触发
                if (counter.template.templateID == "02305")
                {
                    if (NetworkPlayer.Remote.currentEnergy == 0)
                        TriggerCounterWithCardSelect(counter, i, true);
                    else
                        ExpireWithNoEffect(counter, i, true);
                    continue;
                }
                // 屯能噩梦：对方能量不为0触发
                if (counter.template.templateID == "02306")
                {
                    if (NetworkPlayer.Remote.currentEnergy != 0)
                        TriggerCounterWithCardSelect(counter, i, true);
                    else
                        ExpireWithNoEffect(counter, i, true);
                    continue;
                }
                ResolveCounterExpiry(counter, i, true);
            }
        }

        for (int i = enemyCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = enemyCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnEnemyTurnEnd)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
                // 02305/02306 属 Remote/AI 时：受害方（能量判定对象）是 Local，
                // 与 myCounters 分支的 Remote 判定镜像（此前该分支不判定直接触发=空放）
                if (counter.template.templateID == "02305")
                {
                    if (NetworkPlayer.Local != null && NetworkPlayer.Local.currentEnergy == 0)
                        TriggerCounterWithCardSelect(counter, i, false);
                    else
                        ExpireWithNoEffect(counter, i, false);
                    continue;
                }
                if (counter.template.templateID == "02306")
                {
                    if (NetworkPlayer.Local != null && NetworkPlayer.Local.currentEnergy != 0)
                        TriggerCounterWithCardSelect(counter, i, false);
                    else
                        ExpireWithNoEffect(counter, i, false);
                    continue;
                }
                ResolveCounterExpiry(counter, i, false);
            }
        }
    }

    // ========== 判断反制牌是否在此时机递减期限 ==========
    private bool ShouldDecreaseHere(CounterCard counter, CounterTriggerTiming currentTiming)
    {
        return counter.decreaseTiming == currentTiming;
    }

    // ========== 匹配触发条件 ==========
    private bool MatchCondition(CounterCard counter, CardData playedCard)
    {
        string condition = counter.template.counterTriggerCondition;
        if (string.IsNullOrEmpty(condition)) return false;

        // 一费终结者：对方打出基础费用为1的牌
        if (condition.Contains("基础费用为1") && playedCard.baseCost == 1)
            return true;

        // 三费终结者：对方打出基础费用为3的牌
        if (condition.Contains("基础费用为3") && playedCard.baseCost == 3)
            return true;
        // 蛊惑之音
        if (condition.Contains("基础召唤费用为3") && condition.Contains("进场")
            && playedCard.baseCost == 3 && playedCard.hasOnEnter)
            return true;
        return false;
    }

    // ========== 到期结算（根据反制牌类型分类处理） ==========
    private void ResolveCounterExpiry(CounterCard counter, int index, bool isMine)
    {
        CounterTriggerTiming timing = counter.template.counterTiming;

        switch (timing)
        {
            case CounterTriggerTiming.OnCardPlayed:
                // 条件触发型：到期时条件从未满足，无效果触发 → 只扣费
                ExpireWithNoEffect(counter, index, isMine);
                break;

            case CounterTriggerTiming.OnPhaseStart:
            case CounterTriggerTiming.OnPhaseEnd:
            case CounterTriggerTiming.OnBattleEnd:
            case CounterTriggerTiming.OnEnemyTurnEnd:
                // 定时型：到期正常触发效果
                TriggerCounter(counter, index, isMine);
                break;
            case CounterTriggerTiming.OnPlayerDying:
                TriggerCounter(counter, index, isMine);
                break;
            default:
                // 未知类型默认只扣费
                ExpireWithNoEffect(counter, index, isMine);
                break;
        }
    }

    // ========== 无效果到期（只扣费+移除，不触发效果） ==========
    private void ExpireWithNoEffect(CounterCard counter, int index, bool isMine)
    {
        NetworkPlayer owner = CounterOwner(isMine);

        if (!counter.noCostOnTrigger)
        {
            int cost = counter.reducedTriggerCost >= 0 ? counter.reducedTriggerCost : counter.template.baseCost;
            owner.currentEnergy -= cost;
            owner.UpdateUI();
        }

        RemoveCounter(index, isMine);
        SyncCounterRemoved(counter, isMine);
        Debug.Log($"反制牌 {counter.template.cardName} 到期无效果触发" + (counter.noCostOnTrigger ? "" : $"，扣除{counter.template.baseCost}能量"));
    }
    // ========== 触发反制牌效果 ==========
    private void TriggerCounter(CounterCard counter, int index, bool isMine)
    {
        NetworkPlayer owner = CounterOwner(isMine);
        string effect = counter.template.counterEffect;

        if (!string.IsNullOrEmpty(effect))
        {
            if (effect.Contains("摸三张牌"))
            {
                for (int j = 0; j < 3; j++)
                    ServerDrawFor(owner);
            }
            else if (effect.Contains("+3能量"))
            {
                owner.AddEnergy(3);
            }
            // 02211 垂死挣扎：恢复3生命值，+1能量（己方 HP<=0 时的自救；GameEndPanel 留了 0.15s 复查窗口）
            else if (effect.Contains("恢复3生命值"))
            {
                owner.ReceiveHeal(3, CardInstance.HealSourceType.Spell);
                if (effect.Contains("+1能量")) owner.AddEnergy(1);
                Debug.Log($"[02211] 垂死挣扎触发：{owner.name} 恢复3生命值(+1能量)，当前HP={owner.currentHealth}");
            }
        }

        if (!counter.noCostOnTrigger)
        {
            int cost = counter.reducedTriggerCost >= 0 ? counter.reducedTriggerCost : counter.template.baseCost;
            owner.currentEnergy -= cost;
            owner.UpdateUI();
        }

        RemoveCounter(index, isMine);
        SyncCounterRemoved(counter, isMine);
    }

    NetworkPlayer CounterOwner(bool isMine) => isMine ? NetworkPlayer.Local : NetworkPlayer.Remote;

    /// <summary>Draw a card from deck for the given player on the server.</summary>
    void ServerDrawFor(NetworkPlayer player)
    {
        if (!NetworkServer.active || player == null) return;
        CardData data = DeckManager.Instance?.DrawFromMain();
        if (data == null) return;
        // AI(server-only) 没有连接：老写法 TargetReceiveCard(null) 每次都让 Mirror 报
        // "can't be sent because it was given a null connection"；牌本身照旧只走服务器手牌追踪。
        if (player.connectionToClient != null)
            player.TargetReceiveCard(player.connectionToClient, data.templateID, "");
        player.AddServerSideCard(data);
    }

    /// <summary>Tell Remote to remove their visual copy of a counter that the server just consumed.</summary>
    void SyncCounterRemoved(CounterCard counter, bool isMine)
    {
        // Server myCounters → Remote has it in enemyCounters → tell Remote to remove from "enemy"
        // Server enemyCounters → Remote has it in myCounters → tell Remote to remove from "mine"
        if (NetworkPlayer.Remote != null && NetworkPlayer.Remote.connectionToClient != null)
            NetworkPlayer.Remote.TargetRemoveCounter(NetworkPlayer.Remote.connectionToClient,
                counter.template.templateID, isMine ? "enemy" : "mine");
    }

    // ========== 移除反制牌 ==========
    private void RemoveCounter(int index, bool isMine)
    {
        List<CounterCard> list = isMine ? myCounters : enemyCounters;
        if (index < 0 || index >= list.Count) return;

        Destroy(list[index].model);
        list.RemoveAt(index);

        RepositionCounters(isMine);
        Debug.Log($"反制牌已移除，己方数量：{myCounters.Count}");
    }

    /// <summary>让某一侧剩下的牌重新靠拢（原来这里写死用己方的 baseX，对方那摞一重排就跑到己方这边）。</summary>
    public void Reposition(bool isMine)
    {
        RepositionCounters(isMine);
    }

    private void RepositionCounters(bool isMine)
    {
        List<CounterCard> list = isMine ? myCounters : enemyCounters;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || list[i].model == null) continue;
            list[i].model.transform.position = GetCounterPosition(isMine, i);
        }
    }
    /// <summary>玩家生命值降到 &lt;=0 时触发（02211 垂死挣扎）。dying=刚被打到 &lt;=0 的玩家，null=按本机(Host)算；
    /// 反制牌在谁手里就查谁那一摞：Host → myCounters；Remote/AI → enemyCounters（扣费与回血都归该玩家）。
    /// 调用点：NetworkPlayer.ApplyTakeDamage（扣血之后、死亡钩子判定之前）。</summary>
    public void CheckOnPlayerDying(NetworkPlayer dying = null)
    {
        if (!NetworkServer.active) return;

        NetworkPlayer d = dying != null ? dying : NetworkPlayer.LocalHalfPlayer;
        bool dyingIsHost = d == null || d == NetworkPlayer.LocalHalfPlayer;
        List<CounterCard> list = dyingIsHost ? myCounters : enemyCounters;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            CounterCard counter = list[i];
            if (counter?.template == null) continue;
            if (counter.template.counterTiming != CounterTriggerTiming.OnPlayerDying) continue;
            TriggerCounter(counter, i, dyingIsHost);
        }
    }
    /// <summary>02305/02306：受益方=反制拥有者；受害方=其对手（被挑走一张手牌）。按 isMine 定向。</summary>
    void TriggerCounterWithCardSelect(CounterCard counter, int index, bool isMine)
    {
        NetworkPlayer beneficiary = CounterOwner(isMine);
        NetworkPlayer victim = isMine ? NetworkPlayer.Remote : NetworkPlayer.Local;
        if (beneficiary == null || victim == null || victim.handCards == null)
        {
            ExpireWithNoEffect(counter, index, isMine);
            return;
        }

        List<CardInstance> enemyCards = new List<CardInstance>();
        foreach (GameObject card in victim.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null) enemyCards.Add(ci);
        }

        if (enemyCards.Count == 0)
        {
            ExpireWithNoEffect(counter, index, isMine);
            return;
        }

        // [AI] 反制属 AI（AI 挑玩家手牌）或受害方是 AI（无 UI 可弹）→ 自动挑，不弹面板
        bool aiPicks = SimpleAI.IsAIMatch && (!isMine || victim == NetworkPlayer.RemoteHalfPlayer);
        if (aiPicks)
        {
            CardInstance pick = SimpleAI.PickBestStealTarget(enemyCards) ?? enemyCards[0];
            if (pick != null)
                ResolveCardSteal(counter, index, isMine, victim, beneficiary, pick.instanceID);
            else
                ExpireWithNoEffect(counter, index, isMine);
            return;
        }

        CardDisplayPanel.Instance.multiSelect = false;
        CardDisplayPanel.Instance.ShowWithCallback(enemyCards, ci => true, () =>
        {
            CardInstance selected = CardDisplayPanel.Instance.GetSelectedCard();
            CardDisplayPanel.Instance.Hide();
            if (selected == null)
            {
                ExpireWithNoEffect(counter, index, isMine);
                return;
            }
            ResolveCardSteal(counter, index, isMine, victim, beneficiary, selected.instanceID);
        }, "获得");
    }

    /// <summary>把 victim 手牌中 iid 的牌移给 beneficiary，并结算 02305/02306 效果 + 扣费 + 移除反制。</summary>
    void ResolveCardSteal(CounterCard counter, int index, bool isMine,
        NetworkPlayer victim, NetworkPlayer beneficiary, string iid)
    {
        CardData template = null;
        CardInstance stolenCI = null;
        GameObject victimCard = null;
        for (int i = victim.handCards.Count - 1; i >= 0; i--)
        {
            GameObject card = victim.handCards[i];
            if (card == null) { victim.handCards.RemoveAt(i); continue; }
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null || ci.instanceID != iid) continue;
            template = CardDatabase.Instance?.GetTemplate(ci.templateID);
            stolenCI = ci;   // 先抓住实例，别急着销毁——受益方要按「同一张牌」重建
            victimCard = card;
            break;
        }
        if (template == null)
        {
            ExpireWithNoEffect(counter, index, isMine);
            return;
        }

        // 归属受益方：Local 走手牌 UI；AI(server-only) 走服务器手牌追踪。
        // 必须继承旧实例（减费 / 永久数值 / 前缀 / 授予特性 / 持续状态）——旧实现走 AddCardToHand(template) /
        // AddServerSideCard(template, iid) 只按模板新建，挑过来的牌会变成「全新个体」（人类侧还换了 instanceID）。
        // 顺序也必须「先建后销」：先销毁再 CopyFrom 会读到已销毁的组件。
        if (beneficiary == NetworkPlayer.LocalHalfPlayer)
        {
            if (stolenCI != null) NetworkPlayer.LocalHalfPlayer.AddCardToHandFromInstance(template, stolenCI);
            else NetworkPlayer.LocalHalfPlayer.AddCardToHand(template);
        }
        else
        {
            if (stolenCI != null) beneficiary.AddServerSideCard(template, stolenCI.instanceID, stolenCI);
            else beneficiary.AddServerSideCard(template, iid);
        }

        // 再从原主人手里移除（本机手牌走 UI 安全移除；AI/远端手牌直接移除 + 通知）
        if (victim == NetworkPlayer.LocalHalfPlayer)
        {
            NetworkPlayer.RemoveCardFromLocalHand(iid);
        }
        else if (victimCard != null)
        {
            victim.handCards.Remove(victimCard);
            if (victim.connectionToClient != null)
                victim.TargetRemoveHandCard(victim.connectionToClient, iid);
            Destroy(victimCard);
        }

        if (counter.template.templateID == "02305")
            beneficiary.AddEnergy(2);
        else if (counter.template.templateID == "02306")
        {
            beneficiary.DrawCardWithoutLimit();
            beneficiary.DrawCardWithoutLimit();
        }

        if (!counter.noCostOnTrigger)
        {
            int cost = counter.reducedTriggerCost >= 0 ? counter.reducedTriggerCost : counter.template.baseCost;
            beneficiary.currentEnergy -= cost;
            if (beneficiary == NetworkPlayer.LocalHalfPlayer) beneficiary.UpdateUI();
        }
        RemoveCounter(index, isMine);
        SyncCounterRemoved(counter, isMine);
    }
    /// <summary>指定一侧的反制牌「无效果触发」（正常扣费 + 移除 + 同步对手）。02302 反制克星用。</summary>
    public void TriggerCounterNoEffect(CounterCard counter, bool isMine)
    {
        int index = (isMine ? myCounters : enemyCounters).IndexOf(counter);
        if (index >= 0)
            ExpireWithNoEffect(counter, index, isMine);
    }

    /// <summary>旧入口：对方(enemyCounters)的反制无效果触发。</summary>
    public void TriggerEnemyCounterNoEffect(CounterCard counter) => TriggerCounterNoEffect(counter, false);

    /// <summary>公开入口：供网络命令（如 CmdFearlessTriggerCounter）调用 ExpireWithNoEffect。</summary>
    public void ExpireWithNoEffectPublic(CounterCard counter, int index, bool isMine)
    {
        ExpireWithNoEffect(counter, index, isMine);
    }

    /// <param name="isMine">true=己方(myCounters)打出；false=对方(enemyCounters)打出 —— 02302 的服务端权威结算用。</param>
    public void PlayCounterWithReducedCost(CardData template, int cost, bool isMine = true)
    {
        GameObject temp = new GameObject("TempCounter");
        CardInstance ci = temp.AddComponent<CardInstance>();
        ci.InitFromTemplate(template, 0);

        PlayCounter(temp, isMine);

        var counter = (isMine ? myCounters : enemyCounters).LastOrDefault();
        if (counter != null)
            counter.reducedTriggerCost = cost;

        Destroy(temp);
    }
}

// 反制牌数据结构
[System.Serializable]
public class CounterCard
{
    public GameObject model;
    public CardInstance cardInstance;
    public CardData template;
    public bool isMine;
    public int remainingDuration;
    public CounterTriggerTiming decreaseTiming;
    public bool noCostOnTrigger;
    public int reducedTriggerCost = -1;
}
