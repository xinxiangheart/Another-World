using System.Collections;
using UnityEngine;
using static CardData;

public class Card3DHover : MonoBehaviour
{
    private CardInstance cardInstance;
    private Vector3 originalScale;
    private MeshRenderer meshRenderer;
    private Color originalColor;
    public static bool allowDiscard = true;
    public static int ignoreSlotID = -1;
    bool _discardHovered; // 当前是否处于"可抛置"悬停（格子绿色高亮标志）
    BoardSlot _discardSlot; // 悬停中缓存的高亮槽位（OnMouseOver 每帧重申时避免 FindObjectOfType）
    void Start()
    {
        Card3DInstance c3d = GetComponent<Card3DInstance>();
        if (c3d != null)
            cardInstance = c3d.cardInstance;
        else
            cardInstance = GetComponent<CardInstance>();

        originalScale = transform.localScale;
        meshRenderer = GetComponent<MeshRenderer>();

    }

    void OnMouseEnter()
    {
        Debug.Log($"OnMouseEnter 被调用：hasDiscard={cardInstance?.hasDiscard}, isMyTurn={FindObjectOfType<TurnManager>()?.IsMyTurn()}, isPlacingCard={BoardSlot.isPlacingCard}, isTargetingMode={BoardSlot.isTargetingMode}, isAttachSelectMode={BoardSlot.isAttachSelectMode}");
        _discardHovered = false;
        _discardSlot = null;
        if (CanDiscard())
        {
        // 1. 恢复 HandArea 的射线阻挡
            HandManager hm = FindObjectOfType<HandManager>();
            if (hm != null) hm.SetHandAreaRaycast(false);

        // 2. 恢复颜色和大小
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.material.color = Color.yellow;
            transform.localScale = originalScale * 1.05f;

        // 3. 所在格子绿色高亮（抛置提示）
            _discardHovered = true;
            _discardSlot = GetMySlot();
            if (_discardSlot != null) _discardSlot.SetDiscardHighlight(true);
        }

        // 抛置后强制恢复交互
        if (Test1Panel.Instance != null && cardInstance != null)
            Test1Panel.Instance.Show(cardInstance);
    }

    void OnMouseOver()
    {
        // 每帧重申抛置绿色高亮：OnMouseEnter 只在碰撞体进入时触发一次，
        // 一旦绿色被格子 OnPointerExit/SyncVisual 等路径覆盖就不会自动重画，
        // 这里保证悬停期间高亮一直存在、可重复触发。
        if (!_discardHovered) return;
        if (_discardSlot != null) _discardSlot.SetDiscardHighlight(true);
    }

    void OnMouseExit()
    {
        // 1. 恢复 HandArea 的射线阻挡
        HandManager hm = FindObjectOfType<HandManager>();
        if (hm != null) hm.SetHandAreaRaycast(true);

        // 2. 恢复颜色和大小
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.material.color = Color.white;
        transform.localScale = originalScale;

        // 3. 抛置绿色高亮消失（仅当本次悬停是可抛置时恢复）
        if (_discardHovered)
        {
            _discardHovered = false;
            if (_discardSlot != null)
            {
                _discardSlot.SetDiscardHighlight(false);
                _discardSlot = null;
            }
            else
            {
                BoardSlot mySlot = GetMySlot();
                if (mySlot != null) mySlot.SetDiscardHighlight(false);
            }
        }

        Test1Panel.Instance?.Hide();
    }

    void OnMouseDown()
    {
        if (!CanDiscard()) return;

        BoardSlot slot = GetMySlot();
        if (slot == null) return;
        int savedSlotID = slot.slotID;

        // 抛置执行前强制清除绿色高亮——HandleDeath 会销毁卡牌，碰撞体消失后 OnMouseExit 不会触发，
        // 若不在此清除，绿色会一直残留在格子上。
        _discardHovered = false;
        _discardSlot = null;
        slot.SetDiscardHighlight(false);

        cardInstance.isActiveExit = false;
        cardInstance.hasRevenge = false;

        bool shouldTriggerDiscard = cardInstance.hasDiscard;

        cardInstance.savedAttackForDiscard = cardInstance.currentAttack;
        cardInstance.savedTotalDamage = cardInstance.totalDamageTaken;

        slot.HandleDeath(gameObject);

        if (shouldTriggerDiscard)
            HandleDiscardEffect(cardInstance, savedSlotID);

        TurnManager.SyncMyBoardToOpponent();
    }
    private bool CanDiscard()
    {
        if (!allowDiscard) return false;
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm == null || !tm.IsMyTurn()) return false;
        if (BoardSlot.isPlacingCard || BoardSlot.isTargetingMode || BoardSlot.isAttachSelectMode) return false;
        if (cardInstance == null || !cardInstance.HasDiscard) return false;
        // Only discard cards on your side (slots 6-11), never enemy cards
        BoardSlot slot = GetMySlot();
        if (slot == null || slot.slotID < 6) return false;
        if (cardInstance.templateID == "01534" && cardInstance.totalDamageTaken == 0) return false;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(cardInstance, "抛置"))
            return false;
        return true;
    }

    private BoardSlot GetMySlot()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        BoardSlot[] slots = bm.GetAllSlots();
        for (int i = 0; i < 12; i++)
        {
            if (slots[i]?.currentCard3D == gameObject)
                return slots[i];
        }
        return null;
    }

    private void HandleDiscardEffect(CardInstance deadInstance, int discardSlotID)
    {
        var discardCtx = EffectContext.ForDiscard(deadInstance, discardSlotID);
        if (EffectDispatcher.Dispatch(Trigger.Discard, discardCtx))
            return;

        // ── 01511 已复制抛置特性 → 直接在找到的槽位上启动协程 ──
        if (deadInstance.templateID == "01511" && deadInstance.mindScholarCopiedTraits?.Count > 0)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            BoardSlot found = null;
            for (int i = 0; i < 12; i++) {
                var ci = bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci == deadInstance) { found = bm.GetSlot(i); break; }
            }
            if (found != null)
                found.StartCoroutine(found.TriggerScholarDiscardFromHover(deadInstance, discardSlotID));
            return;
        }

        Debug.LogWarning($"[HandleDiscardEffect] 未注册: {deadInstance.templateID}");
        HandManager hm = FindObjectOfType<HandManager>();
        hm?.SetHandAreaRaycast(true);
        hm?.ShowAllCards();
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
    }

    // ========== Unified Hidden State ==========

    /// <summary>
    /// Set by ApplySync when the opponent has an active MistHider aura.
    /// Any new card model spawned on this client should check this flag
    /// and hide itself if targeting enemy slots.
    /// </summary>
    public static bool EnemyCardsAreHidden { get; set; }

    /// <summary>
    /// Re-read cardInstance from Card3DInstance. Call after late-assigning c3d.cardInstance
    /// (e.g. in PlayCounter, where prefab is instantiated before data is copied).
    /// </summary>
    public void RefreshCardData()
    {
        Card3DInstance c3d = GetComponent<Card3DInstance>();
        cardInstance = c3d != null ? c3d.cardInstance : GetComponent<CardInstance>();
    }

    /// <summary>
    /// Apply or remove hidden state for the OPPONENT'S perspective. Visual only.
    /// hidden=true: model flips (rotation 0°), text hides, hover panel blocked.
    /// hidden=false: restore normal — except attachments never show text.
    /// This does NOT affect targeting, damage, traits, or stat sync.
    /// </summary>
    public static void SetHidden(GameObject model, bool hidden, bool isAttachment = false)
    {
        if (model == null) return;

        // 1. Rotation — hidden faces away (0°), visible faces toward viewer (180°)
        model.transform.rotation = hidden ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 180, 0);

        // 2. Text — hidden hides all; unhiding shows except for attachments
        CardDisplay3D display = model.GetComponent<CardDisplay3D>();
        if (display != null)
        {
            if (hidden || isAttachment)
                display.HideAllInfo();
            else
                display.ShowAllInfo();
        }

        // 3. Hover panel / discard — disabled when hidden
        Card3DHover hover = model.GetComponent<Card3DHover>();
        if (hover != null)
            hover.enabled = !hidden;
    }
}