using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    private Stack<string> layerStack = new Stack<string>();
    private readonly Stack<SelectionKind> kindStack = new Stack<SelectionKind>();
    private int idCounter;

    /// <summary>当前选择层的语义类型（伤害/治愈/减益/中性）。槽位选择指示器据此决定颜色；
    /// 不在选择状态时一律为 Neutral。层栈清空时复位。</summary>
    public static SelectionKind CurrentKind { get; private set; } = SelectionKind.Neutral;

    /// <summary>本次选择的来源登记（发起选择的卡名 / 特性序号 / 是否法术）。发起方在 BeginSelection
    /// 之前用 ReportSelectionSource 登记，供提示条推出「选择「XX」特性N目标」。未登记的（战斗阶段
    /// 直连调用等）一律为 null / 0 / false —— 提示条会退回按目标类型的中性文案。</summary>
    public static string CurrentSourceName { get; private set; }
    public static int CurrentSourceTraitIndex { get; private set; }
    public static bool CurrentSourceIsSpell { get; private set; }

    struct SelectionSource
    {
        public string Name;
        public int TraitIndex;
        public bool IsSpell;
    }

    readonly Stack<SelectionSource> _sourceStack = new Stack<SelectionSource>();
    static SelectionSource _pendingSource;

    /// <summary>登记本次选择的来源（在 BeginSelection / BeginOpenSelection 之前调用）。空名 = 复位。</summary>
    public static void ReportSelectionSource(string cardName, int traitIndex = 0, bool isSpell = false)
    {
        _pendingSource = new SelectionSource
        {
            Name = string.IsNullOrEmpty(cardName) ? null : cardName,
            TraitIndex = traitIndex,
            IsSpell = isSpell
        };
    }

    /// <summary>便捷登记：按「卡 + 触发特性」自动补全卡名与特性序号。
    /// traitIndexOverride &gt; 0 时用调用方给的序号（快照/溯源路径拿到的序号比按特性名反查更准）。</summary>
    public static void ReportSelectionSource(CardInstance ci, Trigger trig, int traitIndexOverride = 0)
    {
        CardData card = (ci != null && CardDatabase.Instance != null)
            ? CardDatabase.Instance.GetTemplate(ci.templateID) : null;
        if (card == null && ci != null) ReportSelectionSource(ci.templateID, traitIndexOverride);
        else ReportSelectionSource(card, trig, traitIndexOverride);
    }

    /// <summary>便捷登记：直接给模板（死亡/快照等路径手上只有 CardData 或 templateID）。</summary>
    public static void ReportSelectionSource(CardData card, Trigger trig, int traitIndexOverride = 0)
    {
        bool isSpell = trig == Trigger.Spell || (card != null && card.cardType == CardType.Spell);
        int index = traitIndexOverride > 0 ? traitIndexOverride : TraitIndexOf(card, AttributeOf(trig));
        ReportSelectionSource(card != null ? card.cardName : null, index, isSpell);
    }

    /// <summary>特性触发时机 → 卡面特性名（与 CardData 特性条目的「特性」字段同口径）。</summary>
    public static string AttributeOf(Trigger trig)
    {
        switch (trig)
        {
            case Trigger.Enter: return "进场";
            case Trigger.FirstStrike: return "先手";
            case Trigger.Revenge: return "反击";
            case Trigger.Exit: return "退场";
            case Trigger.ActiveExit: return "主动退场";
            case Trigger.Discard: return "抛置";
            case Trigger.Attach: return "附着";
            default: return null;
        }
    }

    /// <summary>该条特性在卡面编号里的位置（1 起）。编号口径与 CardInstance.GetVisibleTraitEntries 一致：
    /// 「赋予」型条目自身不显示、不参与编号，跳过。</summary>
    public static int TraitIndexOf(CardData card, string attr)
    {
        if (card == null || string.IsNullOrEmpty(attr)) return 0;
        List<CardData.TraitEntry> list = card.GetTraitEntryList();
        if (list == null) return 0;

        int n = 0;
        for (int i = 0; i < list.Count; i++)
        {
            CardData.TraitEntry e = list[i];
            if (e == null || e.isGrant) continue;
            n++;
            if (e.MatchesAttribute(attr)) return n;
        }
        return 0;
    }

    /// <summary>把静态来源字段对齐到当前栈顶（空栈 = 复位）。</summary>
    void RefreshSourceStatics()
    {
        if (_sourceStack.Count > 0)
        {
            SelectionSource s = _sourceStack.Peek();
            CurrentSourceName = s.Name;
            CurrentSourceTraitIndex = s.TraitIndex;
            CurrentSourceIsSpell = s.IsSpell;
        }
        else
        {
            CurrentSourceName = null;
            CurrentSourceTraitIndex = 0;
            CurrentSourceIsSpell = false;
        }
    }

    /// <summary>没显式登记时，用"当前正在执行的特性"自动补全来源。
    /// 为什么必须在**开选择时**就落定、而不是每帧去问 EffectDispatcher：同步 handler（抛置那一批最典型）
    /// 在 0.5s 后就把特性出栈，而选择这时往往还开着 —— 那样文案会中途从「选择「难民」特性1目标」
    /// 退化成兜底的中性文案。登记落在**选择层栈**上，寿命与选择一致，所以文案全程稳定。</summary>
    static SelectionSource SourceFromDispatcher()
    {
        string id = EffectDispatcher.CurrentEffectTemplateID;
        Trigger trig = EffectDispatcher.CurrentEffectTrigger;
        CardData card = (CardDatabase.Instance != null && !string.IsNullOrEmpty(id))
            ? CardDatabase.Instance.GetTemplate(id) : null;
        string name = card != null && !string.IsNullOrEmpty(card.cardName) ? card.cardName : id;
        bool isSpell = trig == Trigger.Spell || (card != null && card.cardType == CardType.Spell);
        return new SelectionSource
        {
            Name = string.IsNullOrEmpty(name) ? null : name,
            TraitIndex = TraitIndexOf(card, AttributeOf(trig)),
            IsSpell = isSpell
        };
    }

    /// <summary>开选择前把来源定下来：显式登记优先，否则用当前正在执行的特性自动补全。</summary>
    void ResolvePendingSource()
    {
        if (string.IsNullOrEmpty(_pendingSource.Name) && EffectDispatcher.HasCurrentEffect)
            _pendingSource = SourceFromDispatcher();
    }

    /// <summary>待裁决选择的"取消钩子"：layerId → 把 null 结果喂给等待者（幂等，只触发一次）。
    /// 供 ForceEndAll / 清栈等强制收尾路径使用，保证不会有协程永远等一个再也不会到来的选择。</summary>
    readonly Dictionary<string, Action> _cancelHooks = new Dictionary<string, Action>();

    /// <summary>注册一次选择的回调。回调只生效一次（防 AI 自动选择 + 双路点击重复触发）；
    /// 同时登记取消钩子，强制收尾时以 null 结束等待（符合 BeginSelection 既有约定"选不到目标传 null"）。</summary>
    void RegisterSelectionCallback(string id, Action<BoardSlot> onSelected)
    {
        bool fired = false;
        BoardSlot.onTargetSelected = (slot) =>
        {
            if (fired) return;
            fired = true;
            _cancelHooks.Remove(id);
            onSelected?.Invoke(slot);
            EndSelection(id);
        };
        _cancelHooks[id] = () =>
        {
            if (fired) return;
            fired = true;
            _cancelHooks.Remove(id);
            onSelected?.Invoke(null);
        };
    }

    /// <summary>把所有待裁决选择以 null 结果收尾（幂等）。不碰层栈/高亮，清理由调用方负责。</summary>
    public void CancelPendingSelections()
    {
        if (_cancelHooks.Count == 0) return;
        var hooks = new List<Action>(_cancelHooks.Values);
        _cancelHooks.Clear();
        foreach (var hook in hooks)
        {
            try { hook(); }
            catch (Exception e) { Debug.LogError($"[SelectionManager] 取消选择回调异常: {e}"); }
        }
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>入栈一个选择层并刷新 <see cref="CurrentKind"/>。
    /// kind = Auto 时按「当前正在执行的特性是哪张卡」自动判定（见 SelectionKindRules），判不出即中性。</summary>
    void PushKind(SelectionKind kind)
    {
        SelectionKind resolved = kind != SelectionKind.Auto ? kind : ResolveAutoKind();
        kindStack.Push(resolved);
        CurrentKind = resolved;
    }

    /// <summary>Auto 判定：抛置语境（抛置特性触发的选择）一律绿色；否则按「当前正在执行的特性是哪张卡」判种类。</summary>
    static SelectionKind ResolveAutoKind()
    {
        if (EffectDispatcher.HasCurrentEffect && EffectDispatcher.CurrentEffectTrigger == Trigger.Discard)
            return SelectionKind.Discard;
        return SelectionKindRules.Classify(EffectDispatcher.CurrentEffectTemplateID);
    }

    /// <summary>层栈收尾后把 kindStack 对齐到 layerStack，空栈时复位为中性。</summary>
    void SyncCurrentKind()
    {
        while (kindStack.Count > layerStack.Count) kindStack.Pop();
        CurrentKind = kindStack.Count > 0 ? kindStack.Peek() : SelectionKind.Neutral;
    }

    void Update()
    {
        // 选择期压暗：不允许选择的格子（含其 2D 格子底与 3D 卡牌）与不可选的手牌压暗，合法目标保持原色。
        // 非选择期只需在退出时复原一次，Tick 内部自行短路。
        SelectionDim.Tick(IsSelecting);
    }

    // 目标选择模式不再在这里做 3D 射线穿透检测格子。
    // 新行为：悬停卡牌 → 高亮对应格子、点击卡牌 → 选中，全部由卡牌模型的鼠标事件驱动
    // （Card3DHover.OnMouseEnter/OnMouseOver/OnMouseUp，卡牌命中 → 映射所在槽位 → HighlightRow）。
    // 空槽位（无卡牌）仍由槽位 UI OnPointerEnter/OnPointerClick 高亮选中（无卡牌遮挡，路径不变）。

    /// <summary>
    /// 强制退出所有选择
    /// </summary>
    public string BeginSelection(TargetType targetType, Action<BoardSlot> onSelected,
        SelectionKind kind = SelectionKind.Auto)
    {
        Debug.Log($"BeginSelection 被调用: targetType={targetType}\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}");
        BoardSlot.isPlacingCard = false;
        BoardSlot.isAttachSelectMode = false;
        BoardSlot.isReplaceMode = false;
        string id = "sel_" + (++idCounter);
        layerStack.Push(id);
        PushKind(kind);
        ResolvePendingSource();
        _sourceStack.Push(_pendingSource);
        _pendingSource = default(SelectionSource);
        RefreshSourceStatics();

        BoardSlot.currentTargetType = targetType;
        RegisterSelectionCallback(id, onSelected);
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);
        Debug.Log($"BeginSelection 隐藏手牌: handCards.Count={Player.Instance.handCards.Count}");
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(false);
        }
        HandManager hm = FindObjectOfType<HandManager>();
        hm?.SetHandAreaRaycast(false);
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
        Card3DHover.allowDiscard = false;

        return id;
    }
    /// <summary>
    /// 强制退出所有选择
    /// </summary>
    public string BeginOpenSelection(TargetType targetType, Action<BoardSlot> onSelected,
        SelectionKind kind = SelectionKind.Auto)
    {
        Debug.Log($"BeginOpenSelection 被调用: targetType={targetType}");
        BoardSlot.isPlacingCard = false;
        BoardSlot.isAttachSelectMode = false;
        BoardSlot.isReplaceMode = false;
        string id = "open_" + (++idCounter);
        layerStack.Push(id);
        PushKind(kind);
        ResolvePendingSource();
        _sourceStack.Push(_pendingSource);
        _pendingSource = default(SelectionSource);
        RefreshSourceStatics();

        BoardSlot.currentTargetType = targetType;
        RegisterSelectionCallback(id, onSelected);

        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
        Card3DHover.allowDiscard = false;

        return id;
    }

    /// <summary>
    /// 强制退出所有选择
    /// </summary>
    public void EndSelection(string id)
    {
        if (layerStack.Count == 0) return;
        if (layerStack.Peek() != id)
        {
            // 非栈顶收尾 = 直接清栈：栈里其它层仍在等回调的协程必须一并解除等待，否则永久挂起
            layerStack.Clear();
            _sourceStack.Clear();
            CancelPendingSelections();
        }
        else
        {
            layerStack.Pop();
            if (_sourceStack.Count > 0) _sourceStack.Pop();
        }
        _pendingSource = default(SelectionSource);
        SyncCurrentKind();
        RefreshSourceStatics();

        if (layerStack.Count == 0)
        {
            BoardSlot.humanSelectionGuard = false; // 层栈清空 = 玩家点选（守望者 01339）已收尾：兜底放行 AI 自动选择
            BoardSlot.ClearAllHighlights();
            BoardSlot.extraTargetFilter = null;
            BoardSlot.currentTargetType = TargetType.None;
            BoardSlot.isStrengtheningSlot = false;
            BoardSlot.isPlacingCard = false;
            BoardSlot.isAttachSelectMode = false;
            BoardSlot.isReplaceMode = false;
            BoardSlot.attachCanBeIndependent = false;

            HandManager hm = FindObjectOfType<HandManager>();
            hm?.SetHandAreaRaycast(true);
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                if (card != null) card.SetActive(true);
            }
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
            Card3DHover.allowDiscard = true;
        }
    }
    /// <summary>
    /// 强制退出所有选择
    /// </summary>
    public bool IsSelecting => layerStack.Count > 0;

    /// <summary>
    /// 强制退出所有选择
    /// </summary>
    public void ForceEndAll()
    {
        // 关键兜底：强退前先把待裁决选择以 null 结果喂回等待者。
        // 只清层栈而不回调（旧行为）会让等选择的协程永久挂起——典型 01104 佣兵进场选目标：
        // Handle01104Coroutine 卡在 WaitUntil(() => done)，StartOnEnterEffect 永不收尾，
        // _enterEffectRunning 永远为 true → 该卡从此不参与死亡扫描 → 生命值 ≤0 也不退场。
        CancelPendingSelections();
        BoardSlot.humanSelectionGuard = false; // 强制收尾：放行 AI 自动选择（见 humanSelectionGuard）
        BoardSlot.ClearAllHighlights();
        layerStack.Clear();
        _sourceStack.Clear();
        _pendingSource = default(SelectionSource);
        SyncCurrentKind();
        RefreshSourceStatics();
        BoardSlot.currentTargetType = TargetType.None;
        BoardSlot.isStrengtheningSlot = false;
        BoardSlot.isPlacingCard = false;
        BoardSlot.isAttachSelectMode = false;
        BoardSlot.isReplaceMode = false;
        BoardSlot.attachCanBeIndependent = false;

        HandManager hm = FindObjectOfType<HandManager>();
        hm?.SetHandAreaRaycast(true);
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(true);
        }
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        Card3DHover.allowDiscard = true;
    }
    public void StartSafeCoroutine(IEnumerator routine)
    {
        StartCoroutine(routine);
    }
    public void RunCoroutine(IEnumerator routine)
    {
        StartCoroutine(routine);
    }
    public IEnumerator OvertimeEffect()
    {
        int currentPhase = TurnManager.Instance.phaseCount;
        List<GraveEntry> valid = new List<GraveEntry>();
        foreach (GraveEntry e in GraveyardManager.Instance.graveyard)
        {
            if (e.deathPhase == currentPhase - 1 && !e.handledReturnToHand)
            {
                CardData template = CardDatabase.Instance.GetTemplate(e.templateID);
                if (template != null && template.cardType == CardType.Summon)
                    valid.Add(e);
            }
        }

        if (valid.Count == 0) { CardDrag.CleanupSpellResources(); yield break; }

        // [AI] 02401：AI 施法 → 从上一阶段召唤物里按 {5,3,1} 挑一个，直放 AI(0-5) 空槽
        if (SimpleAI.IsAIEvaluating)
        {
            GraveEntry best2401 = null;
            int br2401 = int.MaxValue;
            int[] pref2401 = { 5, 3, 1 };
            foreach (GraveEntry e in valid)
            {
                CardData td2401 = CardDatabase.Instance?.GetTemplate(e.templateID);
                int cost = td2401 != null ? td2401.baseCost : (e.currentCost);
                int r2401 = System.Array.IndexOf(pref2401, cost);
                if (r2401 < 0) r2401 = pref2401.Length;
                if (r2401 < br2401) { br2401 = r2401; best2401 = e; }
            }
            if (best2401 != null)
            {
                GraveyardManager.Instance.graveyard.RemoveAll(x => x.instanceID == best2401.instanceID);
                CardData tdPl = CardDatabase.Instance?.GetTemplate(best2401.templateID);
                BoardManager bm2401 = FindObjectOfType<BoardManager>();
                if (tdPl?.prefab3D != null && bm2401 != null)
                    for (int s = 0; s <= 5; s++)
                    {
                        BoardSlot sl = bm2401.GetSlot(s);
                        if (sl == null || sl.hasCard || sl.isBlocked || sl.prisonBlocked || sl.permaBlocked) continue;
                        GameObject tmpAI = new GameObject("Temp");
                        CardInstance tiAI = tmpAI.AddComponent<CardInstance>();
                        tiAI.InitFromTemplate(tdPl, 0, best2401.instanceID);
                        HandManager hmAI = FindObjectOfType<HandManager>();
                        hmAI.PlaceCardToSlot(sl, tmpAI);
                        UnityEngine.Object.Destroy(tmpAI);
                        if (Mirror.NetworkClient.isConnected)
                            NetworkPlayer.Local?.CmdPlayCard(best2401.templateID, sl.slotID,
                                tiAI.baseAttack, tiAI.baseHealth, tiAI.baseMaxHealth, tiAI.currentCost, best2401.instanceID);
                        break;
                    }
            }
            CardDrag.CleanupSpellResources();
            yield break;
        }

        List<CardInstance> displayList = new List<CardInstance>();
        foreach (GraveEntry e in valid)
        {
            GameObject go = new GameObject("TempGrave");
            CardInstance ci = go.AddComponent<CardInstance>();
            ci.templateID = e.templateID;
            ci.instanceID = e.instanceID;
            ci.currentCost = e.currentCost;
            ci.currentAttack = e.currentAttack;
            ci.baseAttack = e.baseAttack;
            ci.currentHealth = e.currentHealth;
            ci.baseHealth = e.baseHealth;
            ci.baseMaxHealth = e.baseMaxHealth;
            ci.currentMaxHealth = e.currentMaxHealth;
            ci.currentTier = e.currentTier;
            ci.baseTier = e.baseTier;
            ci.prefixes = e.prefixes;
            displayList.Add(ci);
        }

        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(displayList, ci => true, () =>
        {
            confirmed = true;
        }, "召唤");
        while (!confirmed)
        {
            yield return null;
        }

        CardInstance selected = CardDisplayPanel.Instance.GetSelectedCard();
        if (selected != null && confirmed)
        {
            GraveyardManager.Instance.graveyard.RemoveAll(e => e.instanceID == selected.instanceID);
            CardData template = CardDatabase.Instance.GetTemplate(selected.templateID);
            if (template?.prefab3D != null)
            {
                HandManager hm = FindObjectOfType<HandManager>();
                BoardSlot.isPlacingCard = true;
                BoardSlot.isStrengtheningSlot = true;
                ReportSelectionSource(selected, Trigger.Enter);
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                foreach (GameObject c in NetworkPlayer.Local.handCards) if (c != null) c.SetActive(false);
                hm.SetHandAreaRaycast(false);
                FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
                Card3DHover.allowDiscard = false;

                bool placed = false;
                BoardSlot.onTargetSelected = (slot) =>
                {
                    if (slot != null && !slot.isBlocked && !slot.hasCard && slot.slotID >= 6)
                    {
                        GameObject tmp = new GameObject("Temp");
                        CardInstance ti = tmp.AddComponent<CardInstance>();
                        ti.InitFromTemplate(template, 0);
                        ti.currentCost = selected.currentCost;
                        ti.currentAttack = selected.currentAttack;
                        ti.currentHealth = selected.currentHealth;
                        ti.currentMaxHealth = selected.currentMaxHealth;
                        ti.currentTier = selected.currentTier;
                        ti.prefixes = selected.prefixes;
                        hm.PlaceCardToSlot(slot, tmp);
                        Destroy(tmp);
                        placed = true;
                        SelectionManager.Instance.ForceEndAll();
                        BoardSlot.isPlacingCard = false;
                        BoardSlot.isStrengtheningSlot = false;
                        foreach (GameObject c in NetworkPlayer.Local.handCards) if (c != null) c.SetActive(true);
                        hm.RefreshLayout(true);
                    }
                };
                yield return new WaitUntil(() => placed);
            }
        }

        foreach (CardInstance ci in displayList) if (ci != null && ci.gameObject != null) Destroy(ci.gameObject);
        CardDrag.CleanupSpellResources();
    }
}
