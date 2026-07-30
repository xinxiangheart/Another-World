using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

// ============================================================================
// DeathHandlers — 退场效果注册中心（Step 2b）
// ============================================================================
//
// 把 BoardSlot.HandleDeath 中 ~30 个 if (templateID == "...") 分支全部迁为
// EffectRegistry.Register。每个 handler 只做原逻辑，通过 EffectContext 读写标志位。
//
// HandleDeath 流程变为:
//   预处理 → EffectDispatcher.Dispatch(trigger, ctx) → DeathPipeline.ExecuteCommon
//
// ============================================================================

public static class DeathHandlers
{
    static bool _registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        // ── 1. 光环注销型（Exit/ActiveExit 都触发） ─────────────────────

        RegisterBoth("03503", Handle03503);
        RegisterBoth("03501", Handle03501);
        RegisterBoth("01323", Handle01323);
        RegisterBoth("01335", Handle01335);
        RegisterBoth("01515", Handle01515);
        RegisterBoth("01517", Handle01517);
        RegisterBoth("01520", Handle01520);
        RegisterBoth("01528", Handle01528);
        RegisterBoth("03511", Handle03511);

        // ── 2. 回手型（Exit/ActiveExit 都触发） ─────────────────────────

        RegisterBoth("01511", Handle01511);
        RegisterBoth("03009", Handle03009);
        RegisterBoth("03020", Handle03020);
        RegisterBoth("03021", Handle03021);
        RegisterBoth("01117", Handle01117);
        RegisterBoth("03504", Handle03504);

        // ── 3. 无条件触发的退场效果 ────────────────────────────────────

        RegisterBoth("01309", Handle01309);
        RegisterBoth("01321", Handle01321);
        RegisterBoth("01331", Handle01331);
        RegisterBoth("01502", Handle01502);
        RegisterBoth("01522", Handle01522);
        RegisterBoth("03513", Handle03513);
        RegisterBoth("01301", Handle01301);

        // ── 4. 双向效果（Exit 和 ActiveExit 行为不同） ─────────────────

        EffectRegistry.Register("01106", Trigger.Exit, Handle01106Exit);
        EffectRegistry.Register("01106", Trigger.ActiveExit, Handle01106ActiveExit);

        EffectRegistry.Register("01316", Trigger.Exit, Handle01316Exit);
        EffectRegistry.Register("01316", Trigger.ActiveExit, Handle01316ActiveExit);

        EffectRegistry.Register("01320", Trigger.Exit, Handle01320Exit);
        EffectRegistry.Register("01320", Trigger.ActiveExit, Handle01320ActiveExit);

        EffectRegistry.Register("01347", Trigger.Exit, Handle01347Exit);
        EffectRegistry.Register("01347", Trigger.ActiveExit, Handle01347ActiveExit);

        // ── 5. 仅 ActiveExit 触发 ───────────────────────────────────────

        EffectRegistry.Register("01107", Trigger.ActiveExit, Handle01107);
        EffectRegistry.Register("01111", Trigger.ActiveExit, Handle01111);
        EffectRegistry.Register("01306", Trigger.ActiveExit, Handle01306);
        EffectRegistry.Register("01307", Trigger.ActiveExit, Handle01307);
        EffectRegistry.Register("01311", Trigger.ActiveExit, Handle01311);
        EffectRegistry.Register("01325", Trigger.ActiveExit, Handle01325);
        EffectRegistry.Register("01338", Trigger.ActiveExit, Handle01338);

        Debug.Log($"[DeathHandlers] 已注册 {EffectRegistry.Count} 条退场效果");
    }

    // ── 工具 ────────────────────────────────────────────────────────────

    static void RegisterBoth(string id, EffectHandler handler)
    {
        EffectRegistry.Register(id, Trigger.Exit, handler);
        EffectRegistry.Register(id, Trigger.ActiveExit, handler);
    }

    static BoardManager BM() => UnityEngine.Object.FindObjectOfType<BoardManager>();
    /// <summary>根据 EffectContext 返回退场卡牌的所属玩家。</summary>
    static NetworkPlayer NP(EffectContext ctx) => BoardManager.GetOwnerPlayer(ctx.sourceSlot?.slotID ?? -1);
    static HandManager HM() => UnityEngine.Object.FindObjectOfType<HandManager>();

    // ═══════════════════════════════════════════════════════════════════
    // 1. 光环注销型
    // ═══════════════════════════════════════════════════════════════════

    static void Handle03503(EffectContext ctx)
    {
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
    }

    static void Handle03501(EffectContext ctx)
    {
        // 03503 也走此 handler（两个 ID 都注册），所以只注销自己的光环即可
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
        // 03501 独有：己方全体 UpdateValues
        var bm = BM();
        if (bm != null && BoardManager.GetSideRangeOf(ctx.source, out int s03501, out int e03501))
            for (int i = s03501; i <= e03501; i++)
            {
                var ally = bm.GetSlot(i);
                if (ally?.currentCard3D != null)
                    ally.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
    }

    static void Handle03511(EffectContext ctx)
    {
        if (ctx.source._disasterWalkerHandler != null)
            GlobalEventManager.Instance.OnPlayerDamaged -= ctx.source._disasterWalkerHandler;
    }

    static void Handle01323(EffectContext ctx)
    {
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
    }

    static void Handle01335(EffectContext ctx)
    {
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
    }

    static void Handle01515(EffectContext ctx)
    {
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
    }

    static void Handle01517(EffectContext ctx)
    {
        var auras = GlobalEventManager.Instance?.GetAurasOfSource(ctx.source);
        if (auras != null)
        {
            foreach (var a in auras)
            {
                if (a is MistHiderAura mistAura)
                    mistAura.RemoveHide();
            }
        }
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
    }

    static void Handle01520(EffectContext ctx)
    {
        NetworkPlayer.DrawCardForPlayer(NP(ctx));
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
        // Remote handCards are lightweight server-side tracking objects —
        // merchant discount flags live on the client; iterate is no-op on server.
        foreach (var card in NP(ctx).handCards)
        {
            if (card == null) continue;
            var ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.merchantDiscounted)
            {
                ci.merchantDiscounted = false;
                card.GetComponent<CardDisplay2D>()?.Refresh();
            }
        }
    }

    static void Handle01528(EffectContext ctx)
    {
        GlobalEventManager.Instance?.UnregisterAuraOfSource(ctx.source);
        foreach (var card in NP(ctx).handCards)
        {
            if (card == null) continue;
            var ci = card.GetComponent<CardInstance>();
            if (ci != null && ci.energyReaperDiscounted)
            {
                ci.energyReaperDiscounted = false;
                card.GetComponent<CardDisplay2D>()?.Refresh();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. 回手型
    // ═══════════════════════════════════════════════════════════════════

    static void Handle01511(EffectContext ctx)
    {
        // 仅服务端/离线执行——客户端 HandleDeath 也会触发此 handler，重复执行会导致双方手牌各获得一张
        if (!NetworkServer.active && NetworkClient.isConnected) return;
        if (!ctx.source.handledReturnToHand)
        {
            ctx.source.handledReturnToHand = true;
            var template = CardDatabase.Instance?.GetTemplate(ctx.TemplateID);
            if (template == null) return;
            var owner = NP(ctx);
            // 主机/单机：加一张到本地手牌并传递旧 CardInstance 以保留 mindScholar 已复制特性
            if (!NetworkServer.active || owner == NetworkPlayer.Local)
            {
                owner.AddCardToHandFromInstance(template, ctx.source, false);
            }
            else
            {
                string iid = template._instanceID ?? ctx.source.instanceID;
                owner.TargetReceiveCard(owner.connectionToClient, template.templateID, iid);
                owner.AddServerSideCard(template, iid);
                // 远程客户端需恢复已复制特性——单独发送同步状态
                owner.TargetSyncScholarState(owner.connectionToClient, iid,
                    ctx.source.mindScholarCopyCount,
                    ctx.source.mindScholarCopiedTraits != null
                        ? string.Join(";;", ctx.source.mindScholarCopiedTraits) : "",
                    ctx.source.mindScholarTriggeredKeys != null
                        ? string.Join(";;", ctx.source.mindScholarTriggeredKeys) : "");
            }
        }
    }

    static void Handle03009(EffectContext ctx)
    {
        if (!ctx.source.handledReturnToHand)
        {
            ctx.source.handledReturnToHand = true;
            ctx.template03009 = CardDatabase.Instance?.GetTemplate(ctx.TemplateID);
            ctx.shouldReturn03009 = true;
        }
    }

    static void Handle03020(EffectContext ctx)
    {
        // 仅服务端/离线执行（同 Handle01511 原因）
        if (!NetworkServer.active && NetworkClient.isConnected) return;
        if (!ctx.source.handledReturnToHand)
        {
            if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(ctx.source))
            {
                var next = CardDatabase.Instance?.GetTemplate("03021");
                if (next != null) NetworkPlayer.AddCardToHandForPlayer(NP(ctx), next);
            }
        }
    }

    static void Handle03021(EffectContext ctx)
    {
        // 仅服务端/离线执行（同 Handle01511 原因）
        if (!NetworkServer.active && NetworkClient.isConnected) return;
        if (!ctx.source.handledReturnToHand)
        {
            if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(ctx.source))
            {
                var next = CardDatabase.Instance?.GetTemplate("03022");
                if (next != null) NetworkPlayer.AddCardToHandForPlayer(NP(ctx), next);
            }
        }
    }

    static void Handle01117(EffectContext ctx)
    {
        bool shouldReturnToHand = false;
        if (!ctx.isActiveExit) shouldReturnToHand = true;

        foreach (string trait in ctx.source.giveableDeathTraits)
        {
            switch (trait)
            {
                case "退场：摸一张牌":
                    NP(ctx).currentEnergy -= 1;
                    NP(ctx).UpdateUI();
                    break;
                case "退场：己方全体受一点伤害":
                    var bm = BM();
                    if (bm != null && BoardManager.GetSideRangeOf(ctx.source, out int sAoe, out int eAoe))
                        for (int i = sAoe; i <= eAoe; i++)
                        {
                            var slot = bm.GetSlot(i);
                            if (slot?.currentCard3D != null)
                            {
                                var ca = slot.currentCard3D.GetComponent<Card3DInstance>();
                                if (ca?.cardInstance != null && ca.cardInstance != ctx.source)
                                {
                                    BattleManager.Instance.ApplyDamageToMinionPublic(ca.cardInstance, 1, null);
                                    ca.UpdateValues();
                                }
                            }
                        }
                    break;
                case "退场：己方玩家扣一血":
                    NP(ctx).TakeDamage(1);
                    break;
            }
        }

        if (shouldReturnToHand && !ctx.source.handledReturnToHand)
        {
            ctx.source.handledReturnToHand = true;
            ctx.template01117 = CardDatabase.Instance?.GetTemplate(ctx.TemplateID);
            ctx.shouldReturn01117 = true;
        }
    }

    static void Handle03504(EffectContext ctx)
    {
        ctx.shouldReturn03504 = ctx.source.currentCost > 0 && !ctx.source.enteredWithZeroCost;
        ctx.source.costReduction++;
        ctx.source.currentCost = Mathf.Max(0, ctx.source.currentCost - 1);
        if (ctx.shouldReturn03504)
        {
            ctx.source.handledReturnToHand = true;
            ctx.template03504 = CardDatabase.Instance?.GetTemplate(ctx.TemplateID);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. 无条件触发
    // ═══════════════════════════════════════════════════════════════════

    static void Handle01309(EffectContext ctx)
    {
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.RogueDeathEffect(ctx.source));
    }

    static void Handle01321(EffectContext ctx)
    {
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.RiddlerDeathEffect(ctx.source));
    }

    static void Handle01331(EffectContext ctx)
    {
        var bm = BM();
        if (ctx.source.prisonMySlot >= 0)
        {
            var s = bm?.GetSlot(ctx.source.prisonMySlot);
            if (s != null)
            {
                s.prisonBlocked = false;
                s.prisonAllowYuan = false;
                s.slotImage.color = s.isBlocked ? Color.gray : s.normalColor;
            }
        }
        if (ctx.source.prisonEnemySlot >= 0)
        {
            var s = bm?.GetSlot(ctx.source.prisonEnemySlot);
            if (s != null)
            {
                s.prisonBlocked = false;
                s.prisonAllowYuan = false;
                s.slotImage.color = s.isBlocked ? Color.gray : s.normalColor;
            }
        }
    }

    static void Handle01502(EffectContext ctx)
    {
        // 仅有当场上不再存在其他 01502 时才清除标记
        BoardManager bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            for (int i = 0; i < 12; i++)
            {
                var ci = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01502" && ci != ctx.source)
                {
                    Debug.Log("[Handle01502] 另一 01502 仍存活——保持 shadowMasterAlive");
                    return;
                }
            }
        }
        CardInstance.shadowMasterAlive = false;
    }

    static void Handle01522(EffectContext ctx)
    {
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.MartyrDeathEffectCoroutine(ctx.source));
    }

    static void Handle03513(EffectContext ctx)
    {
        var bm = BM();
        if (bm != null)
        {
            // 使用退场卡牌所在半场动态计算对方半场
            BoardManager.GetEnemySideRange(ctx.SourceSlotID, out int start, out int end);
            for (int i = start; i <= end; i++)
            {
                var es = bm.GetSlot(i);
                if (es?.currentCard3D != null)
                {
                    var ei = es.currentCard3D.GetComponent<Card3DInstance>();
                    if (ei?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                        ei.UpdateValues();
                    }
                }
            }
        }
    }

    static void Handle01301(EffectContext ctx)
    {
        var bm = BM();
        bool isActive = ctx.isActiveExit;
        if (bm != null && BoardManager.GetSideRangeOf(ctx.source, out int s1301, out int e1301))
            for (int i = s1301; i <= e1301; i++)
            {
                var slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID != "01111" && ci.templateID != "01301")
                    BoardSlot.TriggerDeathEffect(ci, isActive);
            }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. 双向效果（Exit / ActiveExit 不同行为）
    // ═══════════════════════════════════════════════════════════════════

    static void Handle01106Exit(EffectContext ctx)
    {
        NP(ctx).AddEnergy(1);
    }

    static void Handle01106ActiveExit(EffectContext ctx)
    {
        NP(ctx).AddEnergy(3);
    }

    static void Handle01316Exit(EffectContext ctx)
    {
        NetworkPlayer.DrawCardForPlayer(NP(ctx));
        NetworkPlayer.DrawCardForPlayer(NP(ctx));
    }

    static void Handle01316ActiveExit(EffectContext ctx)
    {
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.ThiefActiveExitEffect());
    }

    static void Handle01320Exit(EffectContext ctx)
    {
        int totalCost = 0;
        int drawnCount = 0;
        while (totalCost < 4 && drawnCount < 20)
        {
            var data = DeckManager.Instance?.DrawFromMain();
            if (data == null) break;
            NetworkPlayer.AddCardToHandForPlayer(NP(ctx), data);
            totalCost += data.baseCost;
            drawnCount++;
        }
        Debug.Log($"魔术师退场：摸{drawnCount}张，总基础费用{totalCost}");
    }

    static void Handle01320ActiveExit(EffectContext ctx)
    {
        int totalCost = 0;
        int drawnCount = 0;
        while (totalCost < 10 && drawnCount < 20)
        {
            var data = DeckManager.Instance?.DrawFromMain();
            if (data == null) break;
            NetworkPlayer.AddCardToHandForPlayer(NP(ctx), data);
            totalCost += data.baseCost;
            drawnCount++;
        }
        Debug.Log($"魔术师退场：摸{drawnCount}张，总基础费用{totalCost}");
    }

    static void Handle01347Exit(EffectContext ctx)
    {
        // 按死亡卡所属玩家动态确定"敌方半场"
        BoardManager.GetEnemySideRange(ctx.sourceSlot.slotID, out int enemyStart, out int enemyEnd);
        bool hasEnemy = false;
        var bmCheck = BM();
        for (int i = enemyStart; i <= enemyEnd; i++)
            if (bmCheck?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }
        if (!hasEnemy) return;

        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(Handle01347ExitCoroutine(ctx));
    }

    static System.Collections.IEnumerator Handle01347ExitCoroutine(EffectContext ctx)
    {
        NestingContext.Enter("01347_Exit");

        NetworkPlayer owner = BoardManager.GetOwnerPlayer(ctx.sourceSlot.slotID);

        if (owner == NetworkPlayer.Remote && Mirror.NetworkServer.active)
        {
            // 远端玩家的卡：委托远端选择目标
            BoardSlot._honorAttendantExitTarget = -1;
            BoardSlot._honorAttendantExitWaiting = true;
            NetworkPlayer.Remote.TargetHonorAttendantExitSelect(
                NetworkPlayer.Remote.connectionToClient, ctx.sourceSlot.slotID);
            float t0 = Time.time;
            while (BoardSlot._honorAttendantExitWaiting && Time.time - t0 < 30f)
                yield return null;
            BoardSlot._honorAttendantExitWaiting = false;

            int chosen = BoardSlot._honorAttendantExitTarget;
            if (chosen >= 0 && chosen < 12)
            {
                BoardManager bm = BM();
                BoardSlot target = bm?.GetSlot(chosen);
                if (target?.currentCard3D != null)
                {
                    var t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 2, null);
                        t3d.UpdateValues();
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
            }
        }
        else
        {
            // 主机/离线：直接选择（BeginSelection 使 IsSelecting=true → WaitForSimultaneousWindow 阻塞）
            bool done = false;
            SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    var t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                    if (t3d?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 2, null);
                        t3d.UpdateValues();
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
                done = true;
            });
            yield return new WaitUntil(() => done);
        }

        NestingContext.Exit();
    }

    static void Handle01347ActiveExit(EffectContext ctx)
    {
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.HonorAttendantActiveExit());
    }

    // ═══════════════════════════════════════════════════════════════════
    // 5. 仅 ActiveExit
    // ═══════════════════════════════════════════════════════════════════

    static void Handle01107(EffectContext ctx)
    {
        NP(ctx).AddEnergy(2);
        var bm = BM();
        bool hasAlly = false;
        BoardManager.GetSideRangeOf(ctx.source, out int f1007S, out int f1007E);
        for (int i = f1007S; i <= f1007E; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
        }
        if (hasAlly)
        {
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    var ti = target.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ti != null)
                    {
                        ti.GrantShield(true, false, false);
                        target.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                        TurnManager.SyncMyBoardToOpponent();
                    }
                }
            });
        }
    }

    static void Handle01111(EffectContext ctx)
    {
        var bm = BM();
        if (bm != null && BoardManager.GetSideRangeOf(ctx.source, out int s1111, out int e1111))
            for (int i = s1111; i <= e1111; i++)
            {
                var slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID != "01111" && ci.templateID != "01301")
                {
                    BoardSlot.TriggerDeathEffect(ci, true);
                }
            }
    }

    static void Handle01306(EffectContext ctx)
    {
        var bm = BM();
        if (bm != null)
        {
            BoardManager.GetEnemySideRange(ctx.SourceSlotID, out int start, out int end);
            int highestAtk = -1;
            BoardSlot targetSlot = null;
            for (int i = start; i <= end; i++)
            {
                var slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                var ce = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (ce?.cardInstance != null && ce.cardInstance.currentAttack > highestAtk)
                { highestAtk = ce.cardInstance.currentAttack; targetSlot = slot; }
            }
            if (targetSlot != null)
            {
                targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.isActiveExit = true;
                targetSlot.HandleDeath(targetSlot.currentCard3D);
            }
        }
    }

    static void Handle01307(EffectContext ctx)
    {
        var bm = BM();
        if (bm != null)
        {
            BoardManager.GetEnemySideRange(ctx.SourceSlotID, out int start, out int end);
            int highestHp = -1;
            BoardSlot targetSlot = null;
            for (int i = start; i <= end; i++)
            {
                var slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                var ce = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (ce?.cardInstance != null && ce.cardInstance.currentHealth > highestHp)
                { highestHp = ce.cardInstance.currentHealth; targetSlot = slot; }
            }
            if (targetSlot != null)
            {
                targetSlot.currentCard3D.GetComponent<Card3DInstance>().cardInstance.isActiveExit = true;
                targetSlot.HandleDeath(targetSlot.currentCard3D);
            }
        }
    }

    static void Handle01311(EffectContext ctx)
    {
        if (ctx.sourceSlot.HasAllyTargetExceptSelf())
        {
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
            {
                if (targetSlot != null && targetSlot.currentCard3D != null && targetSlot != ctx.sourceSlot)
                {
                    var targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (targetCI != null)
                    {
                        NP(ctx).AddEnergy(targetCI.currentCost);
                        targetCI.isActiveExit = true;
                        targetCI._conductorDoubleDeath = true;
                        targetSlot.HandleDeath(targetSlot.currentCard3D);
                    }
                }
            });
        }
    }

    static void Handle01325(EffectContext ctx)
    {
        int baseHP = Mathf.Max(0, ctx.source.currentHealth);
        int energyGain = baseHP * 2;
        NP(ctx)._energyCanExceedLimit = true;
        NP(ctx).AddEnergy(energyGain);
    }

    static void Handle01338(EffectContext ctx)
    {
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(ctx.sourceSlot.DeepSeaActiveExitEffect());
    }
}
