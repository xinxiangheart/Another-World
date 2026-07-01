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

        BoardSyncManager.MarkDirty();
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
        switch (deadInstance.templateID)
        {
            case "01135":
                int allyCount = 0;
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                {
                    for (int i = 6; i <= 11; i++)
                    {
                        BoardSlot s = bm.GetSlot(i);
                        if (s != null && s.hasCard && s.currentCard3D != gameObject)
                            allyCount++;
                    }
                }
                if (allyCount >= 2)
                {
                    Card3DHover.ignoreSlotID = discardSlotID;
                    HandManager hmSwap = FindObjectOfType<HandManager>();
                    if (hmSwap != null)
                        hmSwap.StartCoroutine(hmSwap.SwapTwoAllies());
                    return;
                }
                break;
            case "01136":
                bool hasEnemy = false;
                BoardManager bmRef = FindObjectOfType<BoardManager>();
                for (int i = 0; i <= 5; i++)
                    if (bmRef?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }
                if (hasEnemy)
                {
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, discardSlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 1, null);
                                t3d.UpdateValues();
                            }
                        }
                        BoardSlot.CheckAndHandleDeaths();
                        BoardSyncManager.MarkDirty();
                    });
                    return;
                }
                break;
            case "01346":
                bool hasAlly = false;
                BoardManager bmSoldier = FindObjectOfType<BoardManager>();
                for (int i = 6; i <= 11; i++)
                    if (bmSoldier?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
                if (hasAlly)
                {
                    BoardSlot.StartDiscardSelection(TargetType.SingleAlly, discardSlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Minion);
                            t3d?.UpdateValues();
                        }
                        BoardSyncManager.MarkDirty();
                    });
                    return;
                }
                break;
            case "01343":
                bool hasEnemyTarget = false;
                BoardManager bmExp = FindObjectOfType<BoardManager>();
                if (bmExp != null)
                {
                    for (int i = 0; i <= 5; i++)
                    {
                        if (bmExp.GetSlot(i)?.currentCard3D != null) { hasEnemyTarget = true; break; }
                    }
                }
                if (hasEnemyTarget)
                {
                    int mySlot = discardSlotID;
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlot, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
        // 抛置后强制恢复交互
                                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, deadInstance.savedAttackForDiscard, null);
                                t3d.UpdateValues();
                            }
                        }
                        BoardSlot.CheckAndHandleDeaths();
                        BoardSyncManager.MarkDirty();
                    });
                    return;
                }
                break;
            case "01344":
                bool hasEnemyWitch = false;
                BoardManager bmWitch = FindObjectOfType<BoardManager>();
                for (int i = 0; i <= 5; i++)
                    if (bmWitch?.GetSlot(i)?.currentCard3D != null) { hasEnemyWitch = true; break; }
                if (hasEnemyWitch)
                {
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, discardSlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                t3d.cardInstance.baseAttack -= 2;
                                t3d.cardInstance.currentAttack = Mathf.Max(0, t3d.cardInstance.currentAttack - 2);
                                t3d.UpdateValues();
                            }
                        }
                        BoardSyncManager.MarkDirty();
                    });
                    return;
                }
                break;
            case "01534":
                int baseHP = deadInstance.totalDamageTaken;
                int baseAtk = deadInstance.currentAttack;
                Card3DHover.ignoreSlotID = discardSlotID;
                HandManager hmHorror = FindObjectOfType<HandManager>();
                if (hmHorror != null)
                    hmHorror.StartCoroutine(hmHorror.SpawnTwoHorrors(baseHP, baseAtk));
                return;
            case "03026":
                int lostHealth = deadInstance.currentMaxHealth - deadInstance.currentHealth;
                NetworkPlayer.Local.AddEnergy(lostHealth);
                Debug.Log($"投资者抛置：获得{lostHealth}能量");
                break;
        }

        // 抛置后强制恢复交互
        HandManager hm = FindObjectOfType<HandManager>();
        hm?.SetHandAreaRaycast(true);
        hm?.ShowAllCards();
        FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        BoardSlot.isTargetingMode = false;
    }

    // Card3DHover.SwapTwoAllies 完整替换
  
    /// <summary>
    /// 设为己方视角：启用悬停详情、显示牌面
    /// </summary>
    public void SetEnemyView()
    {
        enabled = false;
        transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    /// <summary>
    /// 设为己方视角：启用悬停详情、显示牌面
    /// </summary>
    public void SetMyView()
    {
        enabled = true;
        transform.rotation = Quaternion.Euler(0, 180, 0);
    }
   
  
}