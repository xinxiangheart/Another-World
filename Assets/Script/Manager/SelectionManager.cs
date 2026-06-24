using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    private Stack<string> layerStack = new Stack<string>();
    private int idCounter;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 标准选择：隐藏手牌、禁用按钮、禁用抛置
    /// </summary>
    public string BeginSelection(TargetType targetType, Action<BoardSlot> onSelected)
    {
        Debug.Log($"BeginSelection 被调用: targetType={targetType}");
        BoardSlot.isPlacingCard = false;
        BoardSlot.isAttachSelectMode = false;
        BoardSlot.isReplaceMode = false;
        string id = "sel_" + (++idCounter);
        layerStack.Push(id);

        BoardSlot.currentTargetType = targetType;
        BoardSlot.onTargetSelected = (slot) =>
        {
            onSelected?.Invoke(slot);
            EndSelection(id);
        };
        Player.Instance.handCards.RemoveAll(c => c == null);
        Debug.Log($"BeginSelection 隐藏手牌: handCards.Count={Player.Instance.handCards.Count}");
        foreach (GameObject card in Player.Instance.handCards)
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
    /// 不隐藏手牌的选择：只禁用按钮和抛置
    /// </summary>
    public string BeginOpenSelection(TargetType targetType, Action<BoardSlot> onSelected)
    {
        BoardSlot.isPlacingCard = false;
        BoardSlot.isAttachSelectMode = false;
        BoardSlot.isReplaceMode = false;
        string id = "open_" + (++idCounter);
        layerStack.Push(id);

        BoardSlot.currentTargetType = targetType;
        BoardSlot.onTargetSelected = (slot) =>
        {
            onSelected?.Invoke(slot);
            EndSelection(id);
        };

        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
        Card3DHover.allowDiscard = false;

        return id;
    }

    /// <summary>
    /// 结束选择，栈空时恢复一切
    /// </summary>
    public void EndSelection(string id)
    {
        if (layerStack.Count == 0) return;
        if (layerStack.Peek() != id) layerStack.Clear();
        else layerStack.Pop();

        if (layerStack.Count == 0)
        {
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
            foreach (GameObject card in Player.Instance.handCards)
            {
                if (card != null) card.SetActive(true);
            }
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
            Card3DHover.allowDiscard = true;
        }
    }
    /// <summary>
    /// 当前是否在选择中
    /// </summary>
    public bool IsSelecting => layerStack.Count > 0;

    /// <summary>
    /// 强制退出所有选择
    /// </summary>
    public void ForceEndAll()
    {
        BoardSlot.ClearAllHighlights();
        layerStack.Clear();
        BoardSlot.currentTargetType = TargetType.None;
        BoardSlot.isStrengtheningSlot = false;
        BoardSlot.isPlacingCard = false;
        BoardSlot.isAttachSelectMode = false;
        BoardSlot.isReplaceMode = false;
        BoardSlot.attachCanBeIndependent = false;

        HandManager hm = FindObjectOfType<HandManager>();
        hm?.SetHandAreaRaycast(true);
        foreach (GameObject card in Player.Instance.handCards)
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
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                foreach (GameObject c in Player.Instance.handCards) if (c != null) c.SetActive(false);
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
                        foreach (GameObject c in Player.Instance.handCards) if (c != null) c.SetActive(true);
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