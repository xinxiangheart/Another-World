using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// ============================================================================
// SpellHandlers — 法术效果注册中心（Step 4）
// ============================================================================
//
// 把 CardDrag.ResolveSpellEffect 中 ~43 个 switch case（key=中文 effect 字符串）
// 全量迁为 EffectRegistry.Register(templateID, Trigger.Spell, handler)。
// 这是多语言解耦的关键步：effect 显示文本可本地化，逻辑走稳定 templateID。
// ============================================================================

public static class SpellHandlers
{
    static bool _registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        // 0费/特殊费
        Register("02002", Handle02002);
        Register("02004", Handle02004);
        Register("02006", Handle02006);
        Register("02009", Handle02009);
        Register("02010", Handle02010);

        // 1费
        Register("02103", Handle02103);
        Register("02104", Handle02104);
        Register("02105", Handle02105);
        Register("02106", Handle02106);
        Register("02107", Handle02107);
        Register("02109", Handle02109);
        Register("02110", Handle02110);
        Register("02111", Handle02111);

        // 2费
        Register("02201", Handle02201);
        Register("02202", Handle02202);
        Register("02203", Handle02203);
        Register("02204", Handle02204);
        Register("02205", Handle02205);
        Register("02206", Handle02206);
        Register("02207", Handle02207);
        Register("02209", Handle02209);
        Register("02212", Handle02212);
        Register("02213", Handle02213);
        Register("02214", Handle02214);
        Register("02215", Handle02215);

        // 3费
        Register("02301", Handle02301);
        Register("02302", Handle02302);
        Register("02303", Handle02303);
        Register("02307", Handle02307);
        Register("02308", Handle02308);
        Register("02309", Handle02309);
        Register("02310", Handle02310);
        Register("02311", Handle02311);

        // 4费
        Register("02402", Handle02402);
        Register("02403", Handle02403);
        Register("02404", Handle02404);
        Register("02407", Handle02407);
        Register("02408", Handle02408);

        // 5费
        Register("02501", Handle02501);
        Register("02508", Handle02508);

        // 特殊 early-if 检查型（在 switch 之前）
        Register("02005", Handle02005);

        Debug.Log($"[SpellHandlers] 已注册 {EffectRegistry.Count} 条效果（含进场/退场/法术）");
    }

    static void Register(string id, EffectHandler h) => EffectRegistry.Register(id, Trigger.Spell, h);

    // ── 便捷 ──────────────────────────────────────────────────────────
    static BoardManager BM() => UnityEngine.Object.FindObjectOfType<BoardManager>();
    static HandManager HM() => UnityEngine.Object.FindObjectOfType<HandManager>();
    static void Cleanup() => CardDrag.CleanupSpellResources();

    // ═══════════════════════════════════════════════════════════════════
    // 0费/特殊费
    // ═══════════════════════════════════════════════════════════════════

    static void Handle02002(EffectContext ctx)
    {
        NetworkPlayer.Local?.DrawCard();
        NetworkPlayer.Local?.DrawCard();
        Cleanup();
    }

    static void Handle02004(EffectContext ctx)
    {
        var cd = UnityEngine.Object.FindObjectOfType<CardDrag>();
        SelectionManager.Instance.StartSafeCoroutine(cd.EmperorsApprovalEffectCoroutine());
    }

    static void Handle02006(EffectContext ctx)
    {
        NetworkPlayer.Remote?.TakeDamage(1);
        Cleanup();
    }

    static void Handle02009(EffectContext ctx)
    {
        string[] ids = { "03015", "03016", "03017", "03018", "03019" };
        var data = CardDatabase.Instance?.GetTemplate(ids[Random.Range(0, ids.Length)]);
        if (data != null) NetworkPlayer.Local.AddCardToHand(data);
        Cleanup();
    }

    static void Handle02010(EffectContext ctx)
    {
        HM().StartCoroutine(HM().BetrayalEffect());
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1费
    // ═══════════════════════════════════════════════════════════════════

    static void Handle02103(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Spell);
            t3d?.UpdateValues();
        }
        Cleanup();
    }

    static void Handle02104(EffectContext ctx)
    {
        var data = CardDatabase.Instance?.GetTemplate("03001");
        if (data != null) NetworkPlayer.Local.AddCardToHand(data);
        Cleanup();
    }

    static void Handle02105(EffectContext ctx)
    {
        NetworkPlayer.Local.ReceiveHeal(2, CardInstance.HealSourceType.Spell);
        Cleanup();
    }

    static void Handle02106(EffectContext ctx)
    {
        var cd = UnityEngine.Object.FindObjectOfType<CardDrag>();
        HM().StartCoroutine(HM().ReformFormationEffect(cd));
    }

    static void Handle02107(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            if (t3d?.cardInstance != null)
            {
                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 3, null);
                t3d.UpdateValues();
            }
        }
        BoardSlot.CheckAndHandleDeaths();
        Cleanup();
    }

    static void Handle02109(EffectContext ctx)
    {
        HM().StartCoroutine(HM().SummonTwoMinions());
    }

    static void Handle02110(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            if (t3d?.cardInstance != null && t3d.cardInstance.currentHealth >= 4)
            {
                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 4, null);
                t3d.UpdateValues();
                NetworkPlayer.Local.AddEnergy(4);
                BoardSlot.CheckAndHandleDeaths();
            }
        }
        BoardSlot.extraTargetFilter = null;
        Cleanup();
    }

    static void Handle02111(EffectContext ctx)
    {
        HM().StartCoroutine(HM().HandCleanseEffect());
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2费
    // ═══════════════════════════════════════════════════════════════════

    static void Handle02201(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            var ci = t3d?.cardInstance;
            var td = CardDatabase.Instance?.GetTemplate(ci?.templateID);
            if (td != null && td.hasOnEnter)
                ts.StartOnEnterEffect(td, ci);
        }
        Cleanup();
    }

    static void Handle02202(EffectContext ctx)
    {
        var bm = BM();
        if (bm == null) { Cleanup(); return; }
        int totalRefund = 0;
        var toRemove = new List<BoardSlot>();
        for (int i = 6; i <= 11; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && !ci.isAttached)
                { totalRefund += ci.currentCost; toRemove.Add(s); }
            }
        }
        foreach (var s in toRemove)
        {
            var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null)
            {
                ci.hasOnDeath = false; ci.hasActiveExit = false; ci.hasRevenge = false;
                ci.handledReturnToHand = true;
                ci.giveableDeathTraits?.Clear(); ci.grantedTraitTexts?.Clear();
            }
        }
        foreach (var s in toRemove) s.HandleDeath(s.currentCard3D);
        NetworkPlayer.Local.AddEnergy(totalRefund);
        Cleanup();
    }

    static void Handle02203(EffectContext ctx)
    {
        HM().StartCoroutine(HM().GreatEvolutionEffect());
    }

    static void Handle02204(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            var ci = t3d?.cardInstance;
            if (ci != null)
            {
                int oldAtk = ci.currentAttack, oldHp = ci.currentHealth;
                ci.currentAttack = oldHp; ci.currentMaxHealth = oldAtk; ci.currentHealth = oldAtk;
                t3d.UpdateValues();
                if (ci.currentHealth <= 0) BoardSlot.CheckAndHandleDeaths();
            }
        }
        Cleanup();
    }

    static void Handle02205(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            if (t3d?.cardInstance != null)
            {
                t3d.cardInstance.isActiveExit = false;
                t3d.cardInstance.handledReturnToHand = true;
                var rt = CardDatabase.Instance?.GetTemplate(t3d.cardInstance.templateID);
                ts.HandleDeath(ts.currentCard3D);
                if (rt != null) NetworkPlayer.Local.AddCardToHandFromInstance(rt, t3d.cardInstance);
                NetworkPlayer.Local.DrawCardWithoutLimit();
            }
        }
        Cleanup();
    }

    static void Handle02206(EffectContext ctx) => TransformTo("03005", ctx, "机械飞升");
    static void Handle02207(EffectContext ctx) => TransformTo("03003", ctx, "深渊之息");

    static void TransformTo(string targetID, EffectContext ctx, string log)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D == null) { Cleanup(); return; }
        var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
        if (t3d?.cardInstance == null || t3d.cardInstance.isAttached) { Cleanup(); return; }
        var newTD = CardDatabase.Instance?.GetTemplate(targetID);
        if (newTD?.prefab3D == null) { Cleanup(); return; }

        UnityEngine.Object.Destroy(ts.currentCard3D);
        ts.SetCard(null);
        var pos = HM().GetSlotWorldPosition(ts.slotID);
        var model = UnityEngine.Object.Instantiate(newTD.prefab3D, pos, Quaternion.Euler(0, 180, 0));
        var new3D = model.GetComponent<Card3DInstance>();
        if (new3D != null)
        {
            var newInst = model.AddComponent<CardInstance>();
            newInst.InitFromTemplate(newTD, 0);
            new3D.cardInstance = newInst; new3D.UpdateValues();
        }
        ts.SetCard(model);
        TurnManager.SyncMyBoardToOpponent();
        Cleanup();
    }

    static void Handle02209(EffectContext ctx)
    {
        NetworkPlayer.Local.TakeDamage(3);
        NetworkPlayer.Local.AddEnergy(5);
        Cleanup();
    }

    static void Handle02212(EffectContext ctx)
    {
        HM().StartCoroutine(HM().SummonCoreEffect());
    }

    static void Handle02213(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            var ci = t3d?.cardInstance;
            if (ci != null && ci.currentAttack > 1)
            {
                int r = ci.currentAttack - 1;
                ci.currentAttack = 1; ci.currentHealth += r;
                if (ci.currentHealth > ci.currentMaxHealth) ci.currentMaxHealth = ci.currentHealth;
                DamagePipeline.ShowFloaterAt(ci, r, FloaterType.Debuff);
                DamagePipeline.ShowFloaterAt(ci, r, FloaterType.Heal);
                t3d.UpdateValues();
            }
        }
        Cleanup();
    }

    static void Handle02214(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts != null)
        {
            int rs = ts.slotID < 3 ? 0 : 3;
            var bm = BM();
            for (int c = 0; c < 3; c++)
            {
                var s = bm?.GetSlot(rs + c);
                if (s?.currentCard3D != null)
                {
                    var t3d = s.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        int dmg = c == 1 ? 3 : 2;
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, dmg, null);
                        t3d.UpdateValues();
                    }
                }
            }
            BoardSlot.CheckAndHandleDeaths();
        }
        Cleanup();
    }

    static void Handle02215(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            if (t3d?.cardInstance != null) t3d.cardInstance.overclocked = true;
        }
        Cleanup();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3费
    // ═══════════════════════════════════════════════════════════════════

    static void Handle02301(EffectContext ctx)
    {
        NetworkPlayer.Local.ReceiveHeal(4, CardInstance.HealSourceType.Spell);
        Cleanup();
    }

    static void Handle02302(EffectContext ctx)
    {
        HM().StartCoroutine(HM().CounterKillerEffect());
    }

    static void Handle02303(EffectContext ctx)
    {
        var bm = BM();
        if (bm != null)
        {
            for (int i = 0; i <= 5; i++)
            {
                var s = bm.GetSlot(i);
                if (s?.currentCard3D != null)
                {
                    var c3d = s.currentCard3D.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance != null)
                    {
                        if (c3d.cardInstance.hasShield) c3d.cardInstance.RemoveShield();
                        BattleManager.Instance.ApplyDamageToMinionPublic(c3d.cardInstance, 2, null);
                        c3d.UpdateValues();
                    }
                }
            }
        }
        BoardSlot.CheckAndHandleDeaths();
        Cleanup();
    }

    static void Handle02307(EffectContext ctx)
    {
        HM().StartCoroutine(HM().ManyCardsEffect());
    }

    static void Handle02308(EffectContext ctx)
    {
        var d = CardDatabase.Instance?.GetTemplate("03014");
        if (d != null) NetworkPlayer.Local.AddCardToHand(d);
        Cleanup();
    }

    static void Handle02309(EffectContext ctx)
    {
        var d = CardDatabase.Instance?.GetTemplate("03026");
        if (d != null) NetworkPlayer.Local.AddCardToHand(d);
        Cleanup();
    }

    static void Handle02310(EffectContext ctx)
    {
        HM().StartCoroutine(HM().SpotlightEffect());
    }

    static void Handle02311(EffectContext ctx)
    {
        HM().StartCoroutine(HM().ChargeHornEffect());
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4费
    // ═══════════════════════════════════════════════════════════════════

    static void Handle02402(EffectContext ctx)
    {
        var bm = BM();
        if (bm != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                var s = bm.GetSlot(i);
                if (s?.currentCard3D != null)
                {
                    var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && !ci.isAttached)
                    {
                        ci.GrantShield(true, false, false);
                        if (!ci.cannotHealOrGainMaxHP)
                        { ci.currentHealth += 1; ci.currentMaxHealth += 1; DamagePipeline.ShowFloaterAt(ci, 1, FloaterType.Heal); }
                        s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
        }
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            t3d?.cardInstance?.ReceiveHeal(2, CardInstance.HealSourceType.Spell);
            t3d?.UpdateValues();
        }
        Cleanup();
    }

    static void Handle02403(EffectContext ctx)
    {
        HM().StartCoroutine(HM().SummonSmallEvilEffect());
    }

    static void Handle02404(EffectContext ctx)
    {
        var d = CardDatabase.Instance?.GetTemplate("03009");
        if (d != null) NetworkPlayer.Local.AddCardToHand(d);
        Cleanup();
    }

    static void Handle02407(EffectContext ctx)
    {
        var d = CardDatabase.Instance?.GetTemplate("03020");
        if (d != null) NetworkPlayer.Local.AddCardToHand(d);
        Cleanup();
    }

    static void Handle02408(EffectContext ctx)
    {
        HM().StartCoroutine(HM().PlagueEffect());
    }

    // ═══════════════════════════════════════════════════════════════════
    // 5费
    // ═══════════════════════════════════════════════════════════════════

    static void Handle02501(EffectContext ctx)
    {
        HM().StartCoroutine(HM().DoorEffect());
    }

    static void Handle02508(EffectContext ctx)
    {
        NetworkPlayer.Local.TakeDamage(2);
        TimeWarpManager.Instance.Activate();
        Cleanup();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 特殊 early-if（02005 爬！: 使己方一召唤物退场+摸1张牌）
    // ═══════════════════════════════════════════════════════════════════

    static void Handle02005(EffectContext ctx)
    {
        var ts = ctx.targetSlot;
        if (ts?.currentCard3D != null)
        {
            var t3d = ts.currentCard3D.GetComponent<Card3DInstance>();
            if (t3d?.cardInstance != null)
            {
                t3d.cardInstance.isActiveExit = false;
                ts.HandleDeath(ts.currentCard3D);
            }
        }
        NetworkPlayer.Local.DrawCard();
        Cleanup();
    }
}
