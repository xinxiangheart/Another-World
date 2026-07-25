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
        }

        // 抛置后强制恢复交互
        if (Test1Panel.Instance != null && cardInstance != null)
            Test1Panel.Instance.Show(cardInstance);
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

        Test1Panel.Instance?.Hide();
    }

    void OnMouseDown()
    {
        if (!CanDiscard()) return;

        BoardSlot slot = GetMySlot();
        if (slot == null) return;
        int savedSlotID = slot.slotID;

        cardInstance.isActiveExit = false;
        cardInstance.hasRevenge = false;

        bool shouldTriggerDiscard = cardInstance.hasDiscard;

        cardInstance.savedAttackForDiscard = cardInstance.currentAttack;

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
        // ── Step 5: 抛置效果分发（新 → EffectRegistry，回退 → 旧 switch）──
        var discardCtx = EffectContext.ForDiscard(deadInstance, discardSlotID);
        if (EffectDispatcher.Dispatch(Trigger.Discard, discardCtx))
            return; // handler 已处理全部逻辑

        // ── 未注册卡回退 ───────────────────────────────────
        Debug.LogWarning($"[HandleDiscardEffect] 未注册: {deadInstance.templateID}");
        // 抛置后强制恢复交互
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