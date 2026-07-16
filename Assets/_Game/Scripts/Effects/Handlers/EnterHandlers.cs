using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// EnterHandlers — 进场效果注册中心（Step 3）
// ============================================================================
//
// 把 BoardSlot.StartOnEnterEffect 中 ~40 个 switch case 全部迁为
// EffectRegistry.Register(id, Trigger.Enter, handler)。
// ============================================================================

public static class EnterHandlers
{
    static bool _registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        Register("03501", Handle03501);
        Register("03503", Handle03503);
        Register("03511", Handle03511);
        Register("01104", Handle01104);
        Register("01110", Handle01110);
        Register("01311", Handle01311);
        Register("01313", Handle01313);
        Register("01314", Handle01314);
        Register("01317", Handle01317);
        Register("01319", Handle01319);
        Register("01322", Handle01322);
        Register("01329", Handle01329);
        Register("01331", Handle01331);
        Register("01335", Handle01335);
        Register("01337", Handle01337);
        Register("01323", Handle01323);
        Register("01348", Handle01348);
        Register("01349", Handle01349);
        Register("01108", Handle01108);
        Register("01117", Handle01117);
        Register("01127", Handle01127);
        Register("01501", Handle01501);
        Register("01502", Handle01502);
        Register("01503", Handle01503);
        Register("01504", Handle01504);
        Register("01505", Handle01505);
        Register("01506", Handle01506);
        Register("01507", Handle01507);
        Register("01509", Handle01509);
        Register("01511", Handle01511);
        Register("01514", Handle01514);
        Register("01515", Handle01515);
        Register("01516", Handle01516);
        Register("01517", Handle01517);
        Register("01520", Handle01520);
        Register("01521", Handle01521);
        Register("01523", Handle01523);
        Register("01533", Handle01533);
        Register("01524", Handle01524);
        Register("01528", Handle01528);

        // 03504 / 03506: AOE damage from early if-checks, also migrate
        Register("03504", Handle03504);
        Register("03506", Handle03506);

        Debug.Log($"[EnterHandlers] 已注册 {EffectRegistry.Count - 32} 条进场效果");
    }

    static void Register(string id, EffectHandler h) => EffectRegistry.Register(id, Trigger.Enter, h);

    static BoardManager BM() => UnityEngine.Object.FindObjectOfType<BoardManager>();
    static SelectionManager SM() => SelectionManager.Instance;

    // ═══════════════════════════════════════════════════════════════════
    // AOE 伤害型（原 early if-check）
    // ═══════════════════════════════════════════════════════════════════

    static void AoeEnemy1(CardInstance inst, BoardSlot slot)
    {
        var bm = BM();
        for (int i = 0; i <= 5; i++)
        {
            var es = bm?.GetSlot(i);
            if (es?.currentCard3D != null)
            {
                var ei = es.currentCard3D.GetComponent<Card3DInstance>();
                if (ei?.cardInstance != null)
                {
                    BattleManager.Instance?.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                    ei.UpdateValues();
                }
            }
        }
        BoardSlot.CheckAndHandleDeaths();
        TurnManager.SyncMyBoardToOpponent();
        slot.CleanupAfterPlacement();
    }

    static void Handle03504(EffectContext ctx) => AoeEnemy1(ctx.source, ctx.sourceSlot);
    static void Handle03506(EffectContext ctx) => AoeEnemy1(ctx.source, ctx.sourceSlot);

    // ═══════════════════════════════════════════════════════════════════
    // 光环注册型
    // ═══════════════════════════════════════════════════════════════════

    static void Handle03501(EffectContext ctx)
    {
        GlobalEventManager.Instance.RegisterAura(new SuppressorAura { source = ctx.source });
        if (!ctx.sourceSlot.HasEnemyTarget()) { ctx.sourceSlot.CleanupAfterPlacement(); return; }
        SM().BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
                targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.silencedThisPhase = true;
        });
        BoardSlot.SyncMistHiderDisplay();
    }

    static void Handle03503(EffectContext ctx)
    {
        GlobalEventManager.Instance.RegisterAura(new SageAura { source = ctx.source });
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    static void Handle03511(EffectContext ctx)
    {
        GlobalEventManager.Instance.OnPlayerDamaged += ctx.sourceSlot.OnDisasterWalkerDamage;
        ctx.source._disasterWalkerHandler = ctx.sourceSlot.OnDisasterWalkerDamage;
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    static void Handle01323(EffectContext ctx)
    {
        GlobalEventManager.Instance.RegisterAura(new JudgeAura { source = ctx.source });
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    static void Handle01335(EffectContext ctx)
    {
        var bm = BM();
        int mySlot = -1;
        if (ctx.source.isAttached)
            mySlot = ctx.source.hostSlotID;
        else
            for (int i = 0; i < 12; i++)
                if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ctx.source)
                { mySlot = i; break; }
        if (mySlot >= 0)
            GlobalEventManager.Instance.RegisterAura(new EnergyHackerAura { source = ctx.source, hostSlotID = mySlot, mySlotID = mySlot });
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    static void Handle01520(EffectContext ctx)
    {
        GlobalEventManager.Instance.RegisterAura(new MerchantAura { source = ctx.source });
        foreach (var card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            var ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            var td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Summon && !ci.merchantDiscounted)
            {
                ci.merchantDiscounted = true;
                card.GetComponent<CardDisplay2D>()?.Refresh();
            }
        }
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    static void Handle01528(EffectContext ctx)
    {
        if (!ctx.source.isAttached)
            GlobalEventManager.Instance.RegisterAura(new EnergyReaperAura { source = ctx.source });
        foreach (var card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            var ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            var td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Summon && ci.prefixes.Contains("灵能") && !ci.energyReaperDiscounted)
            {
                ci.energyReaperDiscounted = true;
                card.GetComponent<CardDisplay2D>()?.Refresh();
            }
        }
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 简单同步效果
    // ═══════════════════════════════════════════════════════════════════

    static void Handle01104(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        if (!slot.HasEnemyTarget()) { slot.CleanupAfterPlacement(); BoardSlot.SyncMistHiderDisplay(); return; }
        SM().BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                var t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                if (t3d != null)
                {
                    BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 1, slot.currentCard3D);
                    t3d.UpdateValues();
                }
            }
            BoardSlot.CheckAndHandleDeaths();
            // Sync enemy damage to opponent via full 12-slot report
            TurnManager.SyncMyBoardToOpponent();
            slot.CleanupAfterPlacement();
        });
        BoardSlot.SyncMistHiderDisplay();
    }

    static void Handle01110(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        if (!slot.HasAllyTargetExceptSelf()) { slot.CleanupAfterPlacement(); BoardSlot.SyncMistHiderDisplay(); return; }
        SM().BeginSelection(TargetType.SingleAlly, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null && targetSlot != slot)
            {
                targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.isActiveExit = true;
                targetSlot.HandleDeath(targetSlot.currentCard3D);
            }
            slot.CleanupAfterPlacement();
        });
        BoardSlot.SyncMistHiderDisplay();
    }

    static void Handle01313(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        if (!slot.HasAllyTargetExceptSelf()) { slot.CleanupAfterPlacement(); BoardSlot.SyncMistHiderDisplay(); return; }
        {
            var jdLayerId = SM().BeginSelection(TargetType.SingleAlly, null);
            BoardSlot.onTargetSelected = (targetSlot) =>
            {
                if (targetSlot == slot || targetSlot == null || targetSlot.currentCard3D == null) return;
                SM().EndSelection(jdLayerId);
                var t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                if (t3d != null)
                {
                    int atk = t3d.cardInstance.currentAttack;
                    int hp = t3d.cardInstance.currentHealth;
                    var targetInst = t3d.cardInstance;
                    t3d.cardInstance.isActiveExit = true;
                    targetSlot.HandleDeath(t3d.gameObject);
                    if (!targetInst.handledReturnToHand)
                    {
                        var tt = CardDatabase.Instance?.GetTemplate(targetInst.templateID);
                        if (tt != null) NetworkPlayer.Local.AddCardToHandFromInstance(tt, targetInst);
                    }
                    var self3D = slot.currentCard3D?.GetComponent<Card3DInstance>();
                    if (self3D != null)
                    {
                        self3D.cardInstance.currentAttack += atk;
                        self3D.cardInstance.currentHealth += hp;
                        self3D.cardInstance.currentMaxHealth += hp;
                        if (atk > 0) DamagePipeline.ShowFloaterAt(self3D.cardInstance, atk, FloaterType.Buff);
                        if (hp > 0)  DamagePipeline.ShowFloaterAt(self3D.cardInstance, hp, FloaterType.Heal);
                        self3D.UpdateValues();

                        // Sync boosted stats to server (covers the HandManager path where
                        // CmdPlayCard was already sent before the enter effect).
                        NetworkPlayer.Local?.CmdUpdateCardStats(slot.slotID,
                            self3D.cardInstance.currentAttack,
                            self3D.cardInstance.currentHealth,
                            self3D.cardInstance.currentMaxHealth);
                    }
                }
                slot.CleanupAfterPlacement();
            };
        }
        BoardSlot.SyncMistHiderDisplay();
    }

    static void Handle01108(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        if (CounterManager.Instance != null && CounterManager.Instance.enemyCounters.Count > 0)
        {
            NetworkPlayer.Local.currentEnergy -= 1;
            NetworkPlayer.Local.UpdateUI();
        }
        slot.CleanupAfterPlacement();
    }

    static void Handle01117(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        var inst = ctx.source;
        if (!slot.HasEnemyTarget() || inst.giveableDeathTraits == null || inst.giveableDeathTraits.Count == 0)
        { slot.CleanupAfterPlacement(); BoardSlot.SyncMistHiderDisplay(); return; }
        SM().BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                var targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (targetCI != null)
                {
                    SufferingGiverPanel.Instance.Show(
                        new List<string>(inst.giveableDeathTraits),
                        (chosenTrait) =>
                        {
                            slot.ApplySufferingGiverEffect(inst, targetCI, chosenTrait);
                            slot.CleanupAfterPlacement();
                        }
                    );
                    return;
                }
            }
            slot.CleanupAfterPlacement();
        });
        BoardSlot.SyncMistHiderDisplay();
    }

    static void Handle01507(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        if (!slot.HasAllyTargetExceptSelf()) { slot.CleanupAfterPlacement(); return; }
        SM().BeginSelection(TargetType.SingleAlly, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null && targetSlot != slot)
            {
                var targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (targetCI != null)
                {
                    targetCI.hasLifePriestBlessing = true;
                    targetCI.lifePriestBlessingSource = ctx.source;
                }
            }
            slot.CleanupAfterPlacement();
        });
    }

    static void Handle01514(EffectContext ctx)
    {
        var follower = CardDatabase.Instance?.GetTemplate("03001");
        if (follower != null)
        {
            NetworkPlayer.Local.AddCardToHand(follower);
            NetworkPlayer.Local.AddCardToHand(follower);
        }
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    static void Handle01516(EffectContext ctx)
    {
        ctx.source.GrantShield(false, false, true);
        ctx.sourceSlot.CleanupAfterPlacement();
    }

    static void Handle01348(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        if (CounterManager.Instance == null || CounterManager.Instance.enemyCounters.Count == 0)
        { slot.CleanupAfterPlacement(); return; }
        GenericChoicePanel.Instance.Show("选择强化", new List<string> { "+3+0", "+0+3" }, (index) =>
        {
            if (index == 0) { ctx.source.currentHealth += 3; ctx.source.currentMaxHealth += 3; DamagePipeline.ShowFloaterAt(ctx.source, 3, FloaterType.Heal); }
            else { ctx.source.currentAttack += 3; DamagePipeline.ShowFloaterAt(ctx.source, 3, FloaterType.Buff); }
            var c3d = slot.FindGiver3D(ctx.source);
            c3d?.UpdateValues();
            slot.CleanupAfterPlacement();
        });
    }

    static void Handle01524(EffectContext ctx)
    {
        var slot = ctx.sourceSlot;
        int scrollCount = 0;
        var bm = BM();
        BoardManager.GetSideRange(slot.slotID, out int scS, out int scE);
        for (int i = scS; i <= scE; i++)
        {
            var s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.prefixes.Contains("神灵画卷") && ci != ctx.source)
                    scrollCount++;
            }
        }
        if (scrollCount >= 2)
        {
            for (int i = 0; i <= 5; i++)
            {
                var s = bm?.GetSlot(i);
                if (s?.currentCard3D != null)
                {
                    var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null) { ci.isActiveExit = true; s.HandleDeath(s.currentCard3D); }
                }
            }
        }
        slot.CleanupAfterPlacement();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 协程型进场效果（直接 StartCoroutine）
    // ═══════════════════════════════════════════════════════════════════

    static void Handle01311(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.ConductorEnterEffect(ctx.source));

    static void Handle01314(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.HeartthrobEnterEffect(ctx.source));

    static void Handle01317(EffectContext ctx)
    {
        var inst = ctx.source;
        var slot = ctx.sourceSlot;
        if (inst.greedySnakeEnterCount >= 3) { slot.CleanupAfterPlacement(); return; }
        if (!slot.HasEnemyTarget()) { slot.CleanupAfterPlacement(); return; }
        SM().BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                var targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (targetCI != null) { slot.StartCoroutine(slot.GreedySnakeCopyProcess(inst, targetCI)); return; }
            }
            slot.CleanupAfterPlacement();
        });
    }

    static void Handle01319(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.FearlessEnterEffect());

    static void Handle01322(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.RemnantEnterEffect(ctx.source));

    static void Handle01329(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.ApprenticeMageEnterEffect(ctx.source));

    static void Handle01331(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.PrisonEnterEffect(ctx.source));

    static void Handle01337(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.PirateEnterEffect(ctx.source));

    static void Handle01349(EffectContext ctx)
    {
        var hm = UnityEngine.Object.FindObjectOfType<HandManager>();
        hm.StartCoroutine(hm.CollectorEnterEffect(ctx.source));
    }

    static void Handle01127(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.ReformerEnterEffect(ctx.source));

    static void Handle01501(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.EmperorEnterEffect(ctx.source));

    static void Handle01502(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.ShadowMasterEnterEffect(ctx.source));

    static void Handle01503(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.LordEnterEffect(ctx.source));

    static void Handle01504(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.WolfKingEnterEffect(ctx.source));

    static void Handle01505(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.BlockerEnterEffect(ctx.source));

    static void Handle01506(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.AmplifierEnterEffect(ctx.source));

    static void Handle01509(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.TerroristEnterEffect(ctx.source));

    static void Handle01511(EffectContext ctx)
    {
        var inst = ctx.source;
        if (inst.mindScholarEnterTriggeredThisPhase) { ctx.sourceSlot.CleanupAfterPlacement(); return; }
        ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.MindScholarEnterEffect(inst));
    }

    static void Handle01515(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.FanaticShamanEnterEffect(ctx.source));

    static void Handle01517(EffectContext ctx)
    {
        var aura = new MistHiderAura { source = ctx.source };
        GlobalEventManager.Instance.RegisterAura(aura);
        aura.ApplyHide();
        ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.MistHiderEnterEffect(ctx.source));
    }

    static void Handle01521(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.BrilliantMageEnterEffect(ctx.source));

    static void Handle01523(EffectContext ctx)
        => ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.InkEnterEffect(ctx.source));

    static void Handle01533(EffectContext ctx)
    {
        // 1. 注册光环：对方召唤物进场受到己方血歌前缀召唤物数量的伤害
        var aura = new ScarletSaintAura { source = ctx.source };
        GlobalEventManager.Instance.RegisterAura(aura);

        // 2. 进场：为己方手牌或场上一召唤物附加血歌前缀
        ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.ScarletSaintEnterEffect(ctx.source));
    }
}
