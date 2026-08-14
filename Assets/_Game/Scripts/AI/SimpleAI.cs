using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SimpleAI — 离线单机模式的评分制 AI 对手。
/// AI = NetworkPlayer.Remote（server-only，connectionToClient == null），
/// 所有操作走 server 本地方法（AddServerSideCard / ServerPlayCard / UseEnergy），
/// 禁止调用 [Command]/[TargetRpc]。
///
/// 评分制：ScoreCard 按身材/光环/先手/神选者等维度打分，SelectSlot 按对位规则选槽，
/// DecideDrawCount 按能量+手牌+神选者留费决定抽牌数。
/// </summary>
public class SimpleAI : MonoBehaviour
{
    public static SimpleAI Instance { get; private set; }

    /// <summary>AI 正在评估/出牌期间为 true。选择/确认等 UI 入口检测此标志走自动分支。</summary>
    public static bool IsAIEvaluating { get; private set; }

    public enum Difficulty { Easy, Normal, Hard }
    public Difficulty difficulty = Difficulty.Normal;

    private NetworkPlayer _ai;
    private bool _playedCard;

    void Awake() => Instance = this;

    // 有光环的召唤物 templateID（进场时 RegisterAura 注册，无 CardData 字段标记，用映射表）
    static readonly HashSet<string> AuraCards = new HashSet<string>()
    {
        "03501", // 压制者：沉默对方
        "03503", // 智者：英雄+2+1
        "01323", // 法官：禁退场
        "01335", // 能量骇客：对位沉默
        "01520", // 商人：召唤物-1费
        "01528", // 能量收割者：灵能-1费
        "01515", // 狂热萨满：禁进场/抛置
        "01517", // 雾隐：隐藏
        "01533", // 猩红圣徒：血歌
    };

    /// <summary>
    /// AI 回合入口：抽牌 → 出牌 → 结束回合。
    /// 由 TurnManager.AutoEndEnemyTurn 调用（AI 在 EnemyTurn 行动）。
    /// </summary>
    public IEnumerator EvaluateAndPlay()
    {
        _ai = NetworkPlayer.Remote;
        if (_ai == null) yield break;

        yield return new WaitForSeconds(0.5f);

        IsAIEvaluating = true;
        try
        {
            // 1. 抽牌（评分制：按能量+手牌+神选者留费决定张数）
            int drawCount = DecideDrawCount();
            for (int i = 0; i < drawCount; i++)
            {
                if (!TryDraw()) break;
            }

            // 2. 循环出牌，直到能量不足或无可出的牌
            while (_ai.currentEnergy > 0)
            {
                yield return TryPlayOneCard();
                if (!_playedCard) break;
            }
        }
        finally
        {
            IsAIEvaluating = false;
        }

        // 3. 结束回合（走 ServerEndTurn 校验 currentPhase==EnemyTurn && player==Remote）
        TurnManager.Instance?.ServerEndTurn(_ai);
    }

    /// <summary>AI 抽一张牌：能量够才抽，走 server-only 手牌追踪（无 UI prefab）。</summary>
    bool TryDraw()
    {
        if (_ai == null) return false;
        if (_ai.handCards.Count >= _ai.maxHandSize) return false;

        CardData data = DeckManager.Instance?.DrawFromMain();
        if (data == null) return false;
        if (!_ai.UseEnergy(1)) return false;

        string iid = data._instanceID ?? CardZoneManager.GenerateInstanceID(data.templateID);
        _ai.AddServerSideCard(data, iid);
        return true;
    }

    /// <summary>出召唤物或法术一张。结果写入 _playedCard。</summary>
    IEnumerator TryPlayOneCard()
    {
        _playedCard = false;
        if (_ai == null) yield break;

        // 1. 优先出评分最高的召唤物（触发进场效果 + 站位决策）
        if (TryFindBestSummon(out CardInstance ci, out GameObject go, out int serverSlot))
        {
            int cost = ci.currentCost;
            if (!_ai.UseEnergy(cost)) yield break;

            Debug.Log($"[SimpleAI] 出召唤物 {ci.templateID} score={ScoreCard(ci):F1} cost={cost} slot={serverSlot}");

            _ai.ServerPlayCard(ci.templateID, serverSlot + 6, // AI 视角 6-11 → 服务器 0-5
                ci.currentAttack, ci.currentHealth, ci.currentMaxHealth, cost, ci.instanceID);

            // 触发进场效果（复用 StartOnEnterEffect，进场 handler 通过 sourceSlot 半场判断 owner）
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            BoardSlot slot = FindObjectOfType<BoardManager>()?.GetSlot(serverSlot);
            CardInstance boardInst = slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (td != null && td.hasOnEnter && boardInst != null)
                yield return slot.StartOnEnterEffect(td, boardInst);

            _ai.handCards.Remove(go);
            if (go != null) Destroy(go);
            _playedCard = true;
            yield break;
        }

        // 2. 无召唤物可出 → 出法术
        if (TryFindSpell(out CardInstance sci, out GameObject sgo, out BoardSlot spellTarget))
        {
            int cost = sci.currentCost;
            if (!_ai.UseEnergy(cost)) yield break;

            Debug.Log($"[SimpleAI] 出法术 {sci.templateID} cost={cost}");
            PlaySpell(sci, spellTarget);

            _ai.handCards.Remove(sgo);
            if (sgo != null) Destroy(sgo);
            _playedCard = true;
        }
    }

    /// <summary>选评分最高的召唤物 + 最优站位槽（服务器 0-5）。</summary>
    bool TryFindBestSummon(out CardInstance ci, out GameObject go, out int serverSlot)
    {
        ci = null; go = null; serverSlot = -1;

        // 收集所有能量够打的召唤物
        var candidates = new List<(CardInstance c, GameObject g, float score)>();
        foreach (GameObject card in _ai.handCards)
        {
            if (card == null) continue;
            CardInstance c = card.GetComponent<CardInstance>();
            if (c == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
            if (td == null || td.cardType != CardType.Summon) continue;
            if (c.currentCost > _ai.currentEnergy) continue;
            candidates.Add((c, card, ScoreCard(c)));
        }
        if (candidates.Count == 0) return false;

        // 评分降序
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        ci = candidates[0].c;
        go = candidates[0].g;
        serverSlot = SelectSlot(ci);
        if (serverSlot < 0) { ci = null; go = null; return false; }
        return true;
    }

    /// <summary>选费用最高能打的法术（非反制）+ 目标服务器槽。</summary>
    bool TryFindSpell(out CardInstance ci, out GameObject go, out BoardSlot target)
    {
        ci = null; go = null; target = null;

        foreach (GameObject card in _ai.handCards)
        {
            if (card == null) continue;
            CardInstance c = card.GetComponent<CardInstance>();
            if (c == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
            if (td == null || td.cardType != CardType.Spell) continue;
            if ((td.spellType & SpellType.Counter) != 0) continue; // 反制牌暂不处理
            if (c.currentCost > _ai.currentEnergy) continue;
            if (ci == null || c.currentCost > ci.currentCost) { ci = c; go = card; }
        }
        if (ci == null || go == null) return false;

        // 目标槽：单目标扫第一个合法；整排/全体/null 交给 handler 内部遍历
        CardData td2 = CardDatabase.Instance?.GetTemplate(ci.templateID);
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null && td2 != null)
        {
            if (td2.targetType == TargetType.SingleEnemy)
            {
                for (int i = 6; i <= 11; i++) // AI 的敌方 = 人类 = 服务器 6-11
                    if (bm.GetSlot(i)?.currentCard3D != null) { target = bm.GetSlot(i); break; }
            }
            else if (td2.targetType == TargetType.SingleAlly)
            {
                for (int i = 0; i <= 5; i++) // AI 的己方 = 服务器 0-5
                    if (bm.GetSlot(i)?.currentCard3D != null) { target = bm.GetSlot(i); break; }
            }
        }
        return true;
    }

    /// <summary>AI 施放法术：RunAsLocal 让 handler 内 Local=AI、Remote=人类（同 CmdResolveSpell 路径）。</summary>
    void PlaySpell(CardInstance sci, BoardSlot target)
    {
        CardData td = CardDatabase.Instance?.GetTemplate(sci.templateID);
        if (td == null) return;

        var capturedTarget = target;
        _ai.RunAsLocal(() =>
        {
            var ctx = EffectContext.ForSpell(td, capturedTarget);
            EffectDispatcher.Dispatch(Trigger.Spell, ctx);
            BoardSlot.CheckAndHandleDeaths();
            BoardSyncManager.MarkDirty();
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    // 评分函数
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>召唤物评分（越高越值得出）。覆盖 AI「必须知道」的机制。</summary>
    float ScoreCard(CardInstance ci)
    {
        if (ci == null) return -999f;
        CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
        if (td == null) return -999f;

        float score = 0f;

        // 1. 身材/费用比（核心价值，权重 10）
        float cost = Mathf.Max(1, ci.currentCost);
        float statValue = (ci.currentAttack + ci.currentHealth) / cost;
        score += statValue * 10f;

        // 2. 打脸能力（tier，权重 4）
        score += ci.currentTier * 4f;

        // 3. 先手（权重 6）
        if (ci.HasFirstStrike) score += 6f;

        // 4. 进场效果（权重 5）
        if (ci.HasOnEnter) score += 5f;

        // 5. 光环（权重 8）
        if (AuraCards.Contains(ci.templateID)) score += 8f;

        // 6. 护盾（权重 3）
        if (ci.hasShield) score += 3f;

        // 7. 反击（权重 2）
        if (ci.HasRevenge) score += 2f;

        // 8. 亡语/退场补偿（权重 2）
        if (ci.HasOnDeath || ci.HasActiveExit) score += 2f;

        // 9. X 数值（权重 3）
        if (ci.isXValue) score += 3f;

        // 10. 前缀协同（血歌 1.5 / 灵能 1 / 机械 1 / 渊 1）
        if (ci.prefixes.Contains("血歌")) score += 1.5f;
        if (ci.prefixes.Contains("灵能")) score += 1f;
        if (ci.prefixes.Contains("机械")) score += 1f;
        if (ci.prefixes.Contains("渊")) score += 1f;

        // 11. 神选者（权重 6）
        if (ci.summonType == SummonType.ChosenOne) score += 6f;

        // 12. 负面状态扣分
        if (ci.silencedThisPhase) score -= 5f; // 本阶段白板
        if (ci.poisoned) score -= 4f;          // 受双倍伤害 + 无法上盾

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 站位决策
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>为召唤物选最优放置槽位（服务器 0-5 视角）。返回 -1 表示无合法槽。</summary>
    int SelectSlot(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return -1;

        bool isFaceCard = ci.currentTier >= 3 && ci.currentAttack >= ci.currentHealth; // 高阶位高攻=打脸型
        bool isTank = ci.currentHealth >= ci.currentAttack * 2;                        // 高血=肉盾型

        int best = -1;
        float bestScore = float.MinValue;

        for (int s = 0; s <= 5; s++) // 遍历 AI 半场（服务器 0-5）
        {
            BoardSlot slot = bm.GetSlot(s);
            if (slot == null || slot.isBlocked || slot.hasCard) continue;

            BoardSlot enemySlot = bm.GetSlot(s + 6); // 对位人类槽
            bool enemyHasMinion = enemySlot?.currentCard3D != null;

            float sc = 0f;

            // 规则1：高攻牌优先放「对面有随从」的列（能打到东西）
            if (ci.currentAttack >= 4 && enemyHasMinion) sc += 10f;

            // 规则2：打脸牌优先放「对面空」的列（tier 直接打英雄）
            if (isFaceCard && !enemyHasMinion) sc += 8f;

            // 规则3：肉盾牌放「对面高攻」的列（挡住威胁）
            if (isTank && enemyHasMinion)
            {
                var enemyCI = enemySlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (enemyCI != null) sc += enemyCI.Attack * 2f;
            }

            // 规则4：避免把低攻牌放「对面空」列（打空浪费攻击）
            if (ci.currentAttack <= 1 && !enemyHasMinion) sc -= 3f;

            // 规则5：X数值牌放前排（尽早参与战斗）
            if (ci.isXValue && s < 3) sc += 2f;

            if (sc > bestScore) { bestScore = sc; best = s; }
        }

        return best;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 抽牌策略
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>决定本回合抽几张（0/1/2）。</summary>
    int DecideDrawCount()
    {
        if (_ai == null) return 0;
        if (_ai.currentEnergy < 1) return 0;                 // 无能量
        if (_ai.handCards.Count >= 18) return 0;             // 手牌接近上限

        int reserved = ReserveForChosenOne();                // 有神选者时留 5 费
        int spendable = _ai.currentEnergy - reserved;
        if (spendable < 1) return 0;

        int maxByEnergy = Mathf.Min(2, spendable);
        int maxByHand = _ai.maxHandSize - _ai.handCards.Count;
        return Mathf.Max(0, Mathf.Min(maxByEnergy, maxByHand));
    }

    /// <summary>手牌有神选者时预留 5 费（神选者是 5 费核心）。</summary>
    int ReserveForChosenOne()
    {
        if (_ai == null) return 0;
        foreach (GameObject card in _ai.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.summonType == SummonType.ChosenOne)
                return 5;
        }
        return 0;
    }
}
