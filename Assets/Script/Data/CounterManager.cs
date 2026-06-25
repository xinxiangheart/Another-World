using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CounterManager : MonoBehaviour
{
    public static CounterManager Instance { get; private set; }

    // 己方打出的反制牌
    public List<CounterCard> myCounters = new List<CounterCard>();
    // 敌方打出的反制牌（己方视角看到牌背）
    public List<CounterCard> enemyCounters = new List<CounterCard>();

    private float baseX = -7.5f;
    private float baseY = 1f;
    private float baseZ = -5.5f;

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
    model.name = inst.instanceID + "_counter";
    Debug.Log($"模型生成成功：{model.name}, 位置：{model.transform.position}");

    CounterCard counter = new CounterCard();
    counter.model = model;
    counter.cardInstance = inst;
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
        Card3DHover hover = model.GetComponent<Card3DHover>();
        if (hover != null) hover.SetMyView();
    }
    else
    {
        enemyCounters.Add(counter);
        CardDisplay3D display3D = model.GetComponent<CardDisplay3D>();
        display3D?.HideAllInfo();
        Card3DHover hover = model.GetComponent<Card3DHover>();
        if (hover != null) hover.SetEnemyView();
    }

    Debug.Log($"反制牌已生成，己方数量：{myCounters.Count}");
        // 守望者：对方打出反制牌立即触发
        if (!isMine)
        {
            HandManager hmWatcher = FindObjectOfType<HandManager>();
            if (hmWatcher != null)
                hmWatcher.StartCoroutine(hmWatcher.WatcherDelayedCheck());
        }
    }

    // ========== 即时触发检测（对方打出卡牌时调用） ==========
    public void CheckOnCardPlayed(CardData playedCard)
    {
        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (counter.template.counterTiming != CounterTriggerTiming.OnCardPlayed) continue;
            if (!MatchCondition(counter, playedCard)) continue;

            // 蛊惑之音特殊处理：设置进场重定向模板，+1能量
            if (counter.template.templateID == "02304")
            {
                GlobalEventManager.Instance.PendingEnterRedirectTemplate = playedCard;
                NetworkPlayer.Local.AddEnergy(1);
            }

            TriggerCounter(counter, i, true);
        }
    }

    // ========== 阶段开始检测 ==========
    public void CheckOnPhaseStart()
    {
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
                ResolveCounterExpiry(counter, i, false);
            }
        }
    }

    // ========== 己方回合开始检测 ==========
    public void CheckOnMyTurnStart()
    {
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
        if (!counter.noCostOnTrigger)
        {
            int cost = counter.reducedTriggerCost >= 0 ? counter.reducedTriggerCost : counter.template.baseCost;
            if (isMine)
            {
                NetworkPlayer.Local.currentEnergy -= cost;
                NetworkPlayer.Local.UpdateUI();
            }
            else
            {
                NetworkPlayer.Remote.currentEnergy -= cost;
                NetworkPlayer.Remote.UpdateUI();
            }
        }

        RemoveCounter(index, isMine);
        Debug.Log($"反制牌 {counter.template.cardName} 到期无效果触发" + (counter.noCostOnTrigger ? "" : $"，扣除{counter.template.baseCost}能量"));
    }
    // ========== 触发反制牌效果 ==========
    private void TriggerCounter(CounterCard counter, int index, bool isMine)
    {
        string effect = counter.template.counterEffect;

        if (!string.IsNullOrEmpty(effect))
        {
            if (effect.Contains("摸三张牌"))
            {
                for (int j = 0; j < 3; j++)
                    NetworkPlayer.Local.DrawCard();
            }
            else if (effect.Contains("+3能量"))
            {
                NetworkPlayer.Local.AddEnergy(3);
            }
           
        }

        if (!counter.noCostOnTrigger)
        {
            int cost = counter.reducedTriggerCost >= 0 ? counter.reducedTriggerCost : counter.template.baseCost;
            if (isMine)
            {
                NetworkPlayer.Local.currentEnergy -= cost;
                NetworkPlayer.Local.UpdateUI();
            }
            else
            {
                NetworkPlayer.Remote.currentEnergy -= cost;
                NetworkPlayer.Remote.UpdateUI();
            }
        }

        RemoveCounter(index, isMine);
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
        for (int i = myCounters.Count - 1; i >= 0; i--)
        {
            CounterCard counter = myCounters[i];
            if (counter.template.counterTiming != CounterTriggerTiming.OnPlayerDying) continue;
            TriggerCounter(counter, i, true);
        }
    }
    void TriggerCounterWithCardSelect(CounterCard counter, int index, bool isMine)
    {
        List<CardInstance> enemyCards = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Remote.handCards)
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

        CardDisplayPanel.Instance.multiSelect = false;
        CardDisplayPanel.Instance.ShowWithCallback(enemyCards, ci => true, () =>
        {
            CardInstance selected = CardDisplayPanel.Instance.GetSelectedCard();
            if (selected != null)
            {
                GameObject toRemove = null;
                foreach (GameObject card in NetworkPlayer.Remote.handCards)
                {
                    CardInstance ci = card?.GetComponent<CardInstance>();
                    if (ci != null && ci.instanceID == selected.instanceID)
                    {
                        toRemove = card;
                        break;
                    }
                }
                if (toRemove != null)
                {
                    NetworkPlayer.Remote.handCards.Remove(toRemove);
                    CardData template = CardDatabase.Instance?.GetTemplate(selected.templateID);
                    if (template != null)
                        NetworkPlayer.Local.AddCardToHand(template);
                    Destroy(toRemove);
                }

                if (counter.template.templateID == "02305")
                    NetworkPlayer.Local.AddEnergy(2);
                else if (counter.template.templateID == "02306")
                {
                    NetworkPlayer.Local.DrawCardWithoutLimit();
                    NetworkPlayer.Local.DrawCardWithoutLimit();
                }
            }

            CardDisplayPanel.Instance.Hide();

            int cost = counter.template.baseCost;
            NetworkPlayer.Local.currentEnergy -= cost;
            NetworkPlayer.Local.UpdateUI();
            RemoveCounter(index, true);
        }, "获得");
    }
    public void TriggerEnemyCounterNoEffect(CounterCard counter)
    {
        int index = enemyCounters.IndexOf(counter);
        if (index >= 0)
            ExpireWithNoEffect(counter, index, false);
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
