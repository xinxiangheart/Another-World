using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    private Stack<string> layerStack = new Stack<string>();
    private int idCounter;
    BoardSlot _lastTargetHover;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        // 目标选择模式：3D 射线（Physics.RaycastAll）穿透卡牌，检测鼠标下的槽位 → 驱动高亮 + 点击。
        // 卡牌 Collider 保持启用（悬停弹窗/抛置正常），槽位 BoxCollider 在卡牌之后被 RaycastAll 命中。
        if (layerStack.Count == 0 || BoardSlot.currentTargetType == TargetType.None) return;
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 500f);
        BoardSlot hovered = FindSlotUnderCursor(hits);

        if (hovered != _lastTargetHover)
        {
            if (_lastTargetHover != null) _lastTargetHover.HighlightRow(false);
            _lastTargetHover = hovered;
            if (hovered != null) hovered.HighlightRow(true);
        }

        if (hovered != null && Input.GetMouseButtonDown(0) && !CardView.IsAnyCardDragging)
        {
            _lastTargetHover = null;
            BoardSlot.onTargetSelected?.Invoke(hovered);
        }
    }

    /// <summary>从射线命中中找鼠标下的槽位：命中卡牌 → 映射到其所在槽位（穿透高亮卡牌下的格子）。
    /// 不新增槽位 collider——避免干扰卡牌 OnMouseEnter（悬停弹窗）。空槽位仍由 UI OnPointerEnter 高亮。</summary>
    BoardSlot FindSlotUnderCursor(RaycastHit[] hits)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            // 直接命中槽位（若未来有槽位 collider）
            BoardSlot direct = h.collider.GetComponentInParent<BoardSlot>();
            if (direct != null && direct.IsValidTarget(BoardSlot.currentTargetType)) return direct;
            // 命中卡牌 → 找它所在槽位
            Card3DInstance c3d = h.collider.GetComponentInParent<Card3DInstance>();
            if (c3d != null)
            {
                foreach (var s in bm.GetAllSlots())
                {
                    if (s != null && s.currentCard3D != null
                        && s.currentCard3D.GetComponent<Card3DInstance>() == c3d
                        && s.IsValidTarget(BoardSlot.currentTargetType))
                        return s;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 强制退出所有选择
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
    /// 强制退出所有选择
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
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                if (card != null) card.SetActive(true);
            }
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
            Card3DHover.allowDiscard = true;
            if (_lastTargetHover != null) { _lastTargetHover.HighlightRow(false); _lastTargetHover = null; }
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
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(true);
        }
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        Card3DHover.allowDiscard = true;
        if (_lastTargetHover != null) { _lastTargetHover.HighlightRow(false); _lastTargetHover = null; }
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