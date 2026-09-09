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

    private float baseX = -7.5f;
    private float baseY = 1f;
    private float baseZ = -6f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ========== 打出一张反制牌 ==========
   public void PlayCounter(GameObject cardObject, bool isMine)
{
    CardInstance inst = cardObject.GetComponent<CardInstance>();
    if (inst == null) return;

    CardData template = CardDatabase.Instance?.GetTemplate(inst.templateID);
    Debug.Log($"PlayCounter 被调用：inst.templateID={inst.templateID}, template={template?.cardName}");

    GameObject prefab = template.spellPrefab3D != null ? template.spellPrefab3D : template.prefab3D;
    Debug.Log($"PlayCounter 选择的预制体：{(prefab != null ? prefab.name : "null")}");

    if (prefab == null)
    {
        Debug.Log("PlayCounter 失败：prefab 为 null");
        return;
    }

    int count = isMine ? myCounters.Count : enemyCounters.Count;
    float xPos = isMine ? baseX : -baseX;
    Vector3 pos = new Vector3(xPos + count * 0.5f, baseY, baseZ - count * 0.1f);

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
        if (template.templateID == "02305" || template.templateID == "02306")
        {
            TurnManager tm = FindObjectOfType<TurnManager>();
            if (tm != null)
            {
                if (tm.isMyTurnFirst)
                    counter.remainingDuration = 1;
                else
                    counter.remainingDuration = 2;
            }
        }
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
        if (!isMine)
        {
            HandManager hmWatcher = FindObjectOfType<HandManager>();
            if (hmWatcher != null)
                hmWatcher.StartCoroutine(hmWatcher.WatcherDelayedCheckFor(false));
        }
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
                }

                TriggerCounter(counter, i, true);
            }
        }
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

    // ========== 己方回合开始检测 ==========
    public void CheckOnMyTurnStart()
    {
        if (!NetworkServer.active) return;

        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (!ShouldDecreaseHere(counter, CounterTriggerTiming.OnEnemyTurnEnd)) continue;

            counter.remainingDuration--;
            if (counter.remainingDuration <= 0)
            {
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

    private void RepositionCounters(bool isMine)
    {
        List<CounterCard> list = isMine ? myCounters : enemyCounters;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 pos = new Vector3(baseX + i * 0.5f, baseY, baseZ - i * 0.1f);
            list[i].model.transform.position = pos;
        }
    }
    public void CheckOnPlayerDying()
    {
        if (!NetworkServer.active) return;

        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (counter.template.counterTiming != CounterTriggerTiming.OnPlayerDying) continue;
            TriggerCounter(counter, i, true);
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
        bool aiPicks = SimpleAI.IsAIMatch && (!isMine || victim == NetworkPlayer.Remote);
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
        for (int i = victim.handCards.Count - 1; i >= 0; i--)
        {
            GameObject card = victim.handCards[i];
            if (card == null) { victim.handCards.RemoveAt(i); continue; }
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null || ci.instanceID != iid) continue;
            template = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (victim == NetworkPlayer.Local)
            {
                // 本机手牌：走 UI 安全移除（隐藏/重排）
                NetworkPlayer.RemoveCardFromLocalHand(iid);
            }
            else
            {
                victim.handCards.RemoveAt(i);
                if (victim.connectionToClient != null)
                    victim.TargetRemoveHandCard(victim.connectionToClient, iid);
                Destroy(card);
            }
            break;
        }
        if (template == null)
        {
            ExpireWithNoEffect(counter, index, isMine);
            return;
        }

        // 归属受益方：Local 走手牌 UI；AI(server-only) 走服务器手牌追踪
        if (beneficiary == NetworkPlayer.Local) NetworkPlayer.Local.AddCardToHand(template);
        else beneficiary.AddServerSideCard(template, iid);

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
            if (beneficiary == NetworkPlayer.Local) beneficiary.UpdateUI();
        }
        RemoveCounter(index, isMine);
        SyncCounterRemoved(counter, isMine);
    }
    public void TriggerEnemyCounterNoEffect(CounterCard counter)
    {
        int index = enemyCounters.IndexOf(counter);
        if (index >= 0)
            ExpireWithNoEffect(counter, index, false);
    }

    /// <summary>公开入口：供网络命令（如 CmdFearlessTriggerCounter）调用 ExpireWithNoEffect。</summary>
    public void ExpireWithNoEffectPublic(CounterCard counter, int index, bool isMine)
    {
        ExpireWithNoEffect(counter, index, isMine);
    }

    public void PlayCounterWithReducedCost(CardData template, int cost)
    {
        GameObject temp = new GameObject("TempCounter");
        CardInstance ci = temp.AddComponent<CardInstance>();
        ci.InitFromTemplate(template, 0);

        PlayCounter(temp, true);

        var counter = myCounters.LastOrDefault();
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
