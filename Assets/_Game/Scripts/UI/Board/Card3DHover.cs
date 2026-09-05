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
    BoardSlot _targetHoverSlot; // 选择模式悬停中的槽位（高亮缓存；OnMouseOver 每帧重申）
    public bool isHidden; // 隐藏（雾隐）态：抑制悬停详情面板(Test1Panel)，但保留选择模式高亮/点击
    bool _hovering;      // 鼠标当前在本卡 collider 上（OnMouseEnter/Exit 维护）
    bool _detailShown;   // 本卡是否因"悬停+按住右键"显示了 Test1Panel（防每帧重建）
    void Start()
    {
        Card3DInstance c3d = GetComponent<Card3DInstance>();
        if (c3d != null)
            cardInstance = c3d.cardInstance;
        else
            cardInstance = GetComponent<CardInstance>();

        originalScale = transform.localScale;
        meshRenderer = GetComponentInChildren<MeshRenderer>(); // 网格在 ModelRoot 子层级

    }

    void OnMouseEnter()
    {
        Debug.Log($"OnMouseEnter 被调用：hasDiscard={cardInstance?.hasDiscard}, isMyTurn={FindObjectOfType<TurnManager>()?.IsMyTurn()}, isPlacingCard={BoardSlot.isPlacingCard}, isTargetingMode={BoardSlot.isTargetingMode}, isAttachSelectMode={BoardSlot.isAttachSelectMode}");
        _hovering = true;
        _discardHovered = false;
        _discardSlot = null;
        if (CanDiscard())
        {
        // 1. 恢复 HandArea 的射线阻挡
            HandManager hm = FindObjectOfType<HandManager>();
            if (hm != null) hm.SetHandAreaRaycast(false);

        // 2. 恢复颜色和大小
            MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
                renderer.material.color = Color.yellow;
            transform.localScale = originalScale * 1.05f;

        // 3. 所在格子绿色高亮（抛置提示）
            _discardHovered = true;
            _discardSlot = GetMySlot();
            if (_discardSlot != null) _discardSlot.SetDiscardHighlight(true);
        }

        // 选择模式：悬停卡牌 → 高亮对应格子（含整排类型走 HighlightRow）
        _targetHoverSlot = null;
        TryBeginTargetHover();

        // 抛置后强制恢复交互。Test1Panel 现改为"悬停 + 按住右键"才显示（见 UpdateDetailPanel），
        // 不再在 Enter 直接弹。选择模式高亮照常。
        // 3D 悬停标签（左=特性 右=状态，锚卡位置固定）：同一 !isHidden gate，悬停即显示（不要求右键）。
        if (!isHidden && cardInstance != null)
            HoverTagSystem.Ensure()?.Show(cardInstance, gameObject);
        UpdateDetailPanel();
    }

    void OnMouseOver()
    {
        // 悬停+按住右键 → 显示 Test1Panel；右键松开 → 隐藏（边沿检测，防每帧重建）
        UpdateDetailPanel();

        // 每帧重申抛置绿色高亮：OnMouseEnter 只在碰撞体进入时触发一次，
        // 一旦绿色被格子 OnPointerExit/SyncVisual 等路径覆盖就不会自动重画，
        // 这里保证悬停期间高亮一直存在、可重复触发。
        if (_discardHovered)
            if (_discardSlot != null) _discardSlot.SetDiscardHighlight(true);

        // 选择模式：悬停卡牌 → 高亮对应格子。
        // 选择中途开始（鼠标已停在卡上）/切到别的格子也每帧检查命中；
        // 选择已结束则丢弃缓存不再重申（高亮由 EndSelection→ClearAllHighlights 清空）。
        if (_targetHoverSlot == null)
        {
            if (BoardSlot.isTargetingMode) TryBeginTargetHover();
        }
        else
        {
            if (BoardSlot.isTargetingMode && _targetHoverSlot.IsValidTarget(BoardSlot.currentTargetType))
                _targetHoverSlot.HighlightRow(true);
            else
                _targetHoverSlot = null;
        }
    }

    void OnMouseExit()
    {
        // 1. 恢复 HandArea 的射线阻挡
        HandManager hm = FindObjectOfType<HandManager>();
        if (hm != null) hm.SetHandAreaRaycast(true);

        // 2. 恢复颜色和大小
        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
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

        // 选择模式：退出卡牌 → 取消对应格子高亮（选择已结束时不再触碰格子，避免覆盖 SyncVisual 恢复）
        if (_targetHoverSlot != null)
        {
            if (BoardSlot.isTargetingMode && _targetHoverSlot.IsValidTarget(BoardSlot.currentTargetType))
                _targetHoverSlot.HighlightRow(false);
            _targetHoverSlot = null;
        }

        _hovering = false;
        _detailShown = false;
        Test1Panel.Instance?.Hide();
        HoverTagSystem.Instance?.Hide();
    }

    /// <summary>Test1Panel 触发：悬停 + 按住鼠标右键才显示；右键松开即隐藏。
    /// OnMouseEnter/OnMouseOver 调用（OnMouseOver 每帧在 collider 上触发，边沿检测避免每帧重建）。</summary>
    void UpdateDetailPanel()
    {
        bool want = _hovering && !isHidden && cardInstance != null && Input.GetMouseButton(1);
        if (want && !_detailShown && Test1Panel.Instance != null)
        {
            Test1Panel.Instance.Show(cardInstance);
            _detailShown = true;
        }
        else if (!want && _detailShown)
        {
            Test1Panel.Instance?.Hide();
            _detailShown = false;
        }
    }

    /// <summary>选择模式悬停命中：卡牌 → 映射所在槽位 → 高亮（整排类型由 HighlightRow 处理）。
    /// 附着卡牌不可选（isAttached / GetMySlot 找不到）。返回是否进入目标高亮态。</summary>
    bool TryBeginTargetHover()
    {
        if (!BoardSlot.isTargetingMode || BoardSlot.currentTargetType == TargetType.None) return false;
        if (cardInstance != null && cardInstance.isAttached) return false; // 附着卡牌不可选
        BoardSlot slot = GetMySlot();
        if (slot == null || !slot.IsValidTarget(BoardSlot.currentTargetType)) return false;
        _targetHoverSlot = slot;
        slot.HighlightRow(true);
        return true;
    }

    /// <summary>选择模式：点击卡牌模型 → 选中对应格子（替代原 SelectionManager 3D 射线穿透选格子）。
    /// OnMouseUpAsButton=按下+松开都在同一碰撞体才算点击，避免拖离误选。
    /// 附着卡牌不可选；lastTargetClickTime 防与槽位 UI OnPointerClick 双触发。</summary>
    void OnMouseUpAsButton()
    {
        if (!BoardSlot.isTargetingMode || BoardSlot.currentTargetType == TargetType.None) return;
        if (cardInstance != null && cardInstance.isAttached) return; // 附着卡牌不可选
        BoardSlot slot = GetMySlot();
        if (slot == null || !slot.IsValidTarget(BoardSlot.currentTargetType)) return;
        if (BoardSlot.onTargetSelected == null) return;
        if (Time.time - BoardSlot.lastTargetClickTime < 0.05f) return;
        BoardSlot.lastTargetClickTime = Time.time;
        Debug.Log($"[TargetClick-card] slot={slot.slotID}, type={BoardSlot.currentTargetType}, onTargetSelected={BoardSlot.onTargetSelected != null}");
        BoardSlot.onTargetSelected?.Invoke(slot);
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

        bool shouldTriggerDiscard = cardInstance.HasDiscard; // 5.x 特性组：抛置类激活才触发抛置效果

        cardInstance.savedAttackForDiscard = cardInstance.currentAttack;
        cardInstance.savedTotalDamage = cardInstance.totalDamageTaken;

        // 抛置销毁卡牌 → 无 OnMouseExit，主动复位并隐藏悬停标签 / Test1Panel 防残留。
        _hovering = false;
        _detailShown = false;
        HoverTagSystem.Instance?.Hide();
        Test1Panel.Instance?.Hide();

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
    /// hidden=true: model flips (rotation 0°), text hides, hover detail panel suppressed
    ///              (但仍可选择模式高亮/点击选中——隐藏不阻断选择)。
    /// hidden=false: restore normal — except attachments never show text.
    /// This does NOT affect targeting, damage, traits, or stat sync.
    /// </summary>
    public static void SetHidden(GameObject model, bool hidden, bool isAttachment = false)
    {
        if (model == null) return;

        // 1. Rotation — hidden faces away (0°), visible faces toward viewer (180°)
        model.transform.rotation = hidden ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 180, 0);

        // 2. 正反面：隐藏(背面) → ShowBack 隐藏全部正面组件(卡框/前缀底图/卡图/文字/图标/三排)；
        //              附件 → 保持正面卡面但隐藏文字；显示(正面) → ShowFront
        CardDisplay3D display = model.GetComponent<CardDisplay3D>();
        if (display != null)
        {
            if (hidden)
                display.ShowBack();
            else if (isAttachment)
                display.HideAllInfo();
            else
                display.ShowFront();
        }

        // 3. 隐藏态：抑制悬停详情面板(Test1Panel)，但组件保持启用——
        //    选择模式仍可悬停高亮 / 点击选中（隐藏允许选择，仅不显示详情）
        Card3DHover hover = model.GetComponent<Card3DHover>();
        if (hover != null)
            hover.isHidden = hidden;
    }
}