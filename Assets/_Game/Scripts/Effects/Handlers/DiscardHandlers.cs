using System;
using UnityEngine;

// ============================================================================
// DiscardHandlers — 抛置效果注册中心（Step 5）
// ============================================================================
//
// 把 Card3DHover.HandleDiscardEffect 中 7 个 switch case 全部迁为
// EffectRegistry.Register(id, Trigger.Discard, handler)。
// ============================================================================

public static class DiscardHandlers
{
    static bool _registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        Register("01135", Handle01135);
        Register("01136", Handle01136);
        Register("01346", Handle01346);
        Register("01343", Handle01343);
        Register("01344", Handle01344);
        Register("01534", Handle01534);
        Register("03026", Handle03026);

        Debug.Log("[DiscardHandlers] 已注册 7 条抛置效果");
    }

    static void Register(string id, EffectHandler h) => EffectRegistry.Register(id, Trigger.Discard, h);

    static BoardManager BM() => UnityEngine.Object.FindObjectOfType<BoardManager>();
    static HandManager HM() => UnityEngine.Object.FindObjectOfType<HandManager>();

    // ── 通用收尾：恢复交互状态 ──────────────────────────────────────
    static void RestoreInteraction()
    {
        HM()?.SetHandAreaRaycast(true);
        HM()?.ShowAllCards();
        UnityEngine.Object.FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        BoardSlot.isTargetingMode = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 7 条抛置效果
    // ═══════════════════════════════════════════════════════════════════

    static void Handle01135(EffectContext ctx)
    {
        int discardSlotID = ctx.discardSlotID;
        int allyCount = 0;
        var bm = BM();
        if (bm != null)
            for (int i = 6; i <= 11; i++)
            {
                var s = bm.GetSlot(i);
                if (s != null && s.hasCard && s.slotID != discardSlotID) allyCount++;
            }
        if (allyCount >= 2)
        {
            Card3DHover.ignoreSlotID = discardSlotID;
            HM()?.StartCoroutine(HM().SwapTwoAllies());
            return; // HM coroutine handles cleanup
        }
        RestoreInteraction();
    }

    static void Handle01136(EffectContext ctx)
    {
        int discardSlotID = ctx.discardSlotID;
        bool hasEnemy = false;
        var bm = BM();
        for (int i = 0; i <= 5; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }
        if (hasEnemy)
        {
            BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, discardSlotID, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    var t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 1, null);
                        t3d.UpdateValues();
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
                TurnManager.SyncMyBoardToOpponent();
            });
            return;
        }
        RestoreInteraction();
    }

    static void Handle01346(EffectContext ctx)
    {
        int discardSlotID = ctx.discardSlotID;
        bool hasAlly = false;
        var bm = BM();
        for (int i = 6; i <= 11; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
        if (hasAlly)
        {
            BoardSlot.StartDiscardSelection(TargetType.SingleAlly, discardSlotID, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    var t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Minion);
                    t3d?.UpdateValues();
                }
                TurnManager.SyncMyBoardToOpponent();
            });
            return;
        }
        RestoreInteraction();
    }

    static void Handle01343(EffectContext ctx)
    {
        int mySlot = ctx.discardSlotID;
        bool hasEnemy = false;
        var bm = BM();
        for (int i = 0; i <= 5; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }
        if (hasEnemy)
        {
            BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlot, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    var t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, ctx.savedAttack, null);
                        t3d.UpdateValues();
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
                TurnManager.SyncMyBoardToOpponent();
            });
            return;
        }
        RestoreInteraction();
    }

    static void Handle01344(EffectContext ctx)
    {
        int discardSlotID = ctx.discardSlotID;
        bool hasEnemy = false;
        var bm = BM();
        for (int i = 0; i <= 5; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }
        if (hasEnemy)
        {
            BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, discardSlotID, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    var t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        t3d.cardInstance.baseAttack -= 2;
                        t3d.cardInstance.currentAttack = Mathf.Max(0, t3d.cardInstance.currentAttack - 2);
                        t3d.UpdateValues();
                    }
                }
                TurnManager.SyncMyBoardToOpponent();
            });
            return;
        }
        RestoreInteraction();
    }

    static void Handle01534(EffectContext ctx)
    {
        int discardSlotID = ctx.discardSlotID;
        int baseHP = ctx.source.totalDamageTaken;
        int baseAtk = ctx.source.currentAttack;
        Card3DHover.ignoreSlotID = discardSlotID;
        HM()?.StartCoroutine(HM().SpawnTwoHorrors(baseHP, baseAtk));
    }

    static void Handle03026(EffectContext ctx)
    {
        var ci = ctx.source;
        int lostHealth = ci.currentMaxHealth - ci.currentHealth;
        NetworkPlayer.Local.AddEnergy(lostHealth);
        Debug.Log($"投资者抛置：获得{lostHealth}能量");
        RestoreInteraction();
    }
}
