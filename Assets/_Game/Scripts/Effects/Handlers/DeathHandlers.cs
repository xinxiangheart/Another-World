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
        // 03501 独有：神官退场，己方在场英雄阶位-1（还原进场/神官进场时烘焙的+1）+ UpdateValues
        var bm = BM();
        if (bm != null && BoardManager.GetSideRangeOf(ctx.source, out int s03501, out int e03501))
            for (int i = s03501; i <= e03501; i++)
            {
                var ally = bm.GetSlot(i);
                var aci = ally?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (aci != null)
                {
                    if (aci.summonType == SummonType.Hero)
                        aci.currentTier = Mathf.Max(aci.baseTier, aci.currentTier - 1);
                    ally.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
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
        if (!ctx.source.handledReturnToHand)
        {
            ctx.source.handledReturnToHand = true;
            ctx.source.isActiveExit = false;
            var owner = NP(ctx);
            // 服务端/单机：直接加回手。纯客户端：通知服务器代为处理
            if (!NetworkServer.active && NetworkClient.isConnected)
            {
                // 纯客户端序列化当前状态——服务端的 CardInstance 从未运行 MindScholarEnterEffect，state 为空
                var src = ctx.source;
                string state = $"{src.mindScholarCopyCount}|" +
                    (src.mindScholarCopiedTraits != null ? string.Join(";;", src.mindScholarCopiedTraits) : "") + "|" +
                    (src.mindScholarTriggeredKeys != null ? string.Join(";;", src.mindScholarTriggeredKeys) : "") + "|" +
                    (src.grantedTraitTexts != null ? string.Join(";;", src.grantedTraitTexts) : "");
                owner.CmdReturnScholarToHand(src.instanceID, ctx.sourceSlot?.slotID ?? -1, state);
            }
            else
            {
                owner.AddCardToHandFromInstance(CardDatabase.Instance?.GetTemplate(ctx.TemplateID), ctx.source, false);
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
            // 特性级溯源：断罪者"退场：对对方全体造成1伤害"在可见特性中的序号+文本
            int srcIdx = ctx.source != null ? ctx.source.GetTraitIndexByKeyword("对对方全体造成1伤害") : -1;
            string srcText = srcIdx > 0 ? ctx.source.GetTraitByIndex(srcIdx) : null;
            for (int i = start; i <= end; i++)
            {
                var es = bm.GetSlot(i);
                if (es?.currentCard3D != null)
                {
                    var ei = es.currentCard3D.GetComponent<Card3DInstance>();
                    if (ei?.cardInstance != null)
                    {
                        BattleManager.Instance.ApplyDamageToMinionPublic(ei.cardInstance, 1, null, srcIdx, srcText);
                        ei.UpdateValues();
                    }
                }
            }
        }
    }

    static void Handle01301(EffectContext ctx)
    {
        // 启动串行协程：前一个同伴选择面板完成后才处理下一个
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(Handle01301Routine(ctx));
    }

    static System.Collections.IEnumerator Handle01301Routine(EffectContext ctx)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        bool isActive = ctx.isActiveExit;
        Debug.Log($"[01301] 开始: isActive={isActive} slot={ctx.sourceSlot?.slotID} Nesting={NestingContext.Depth}");

        if (bm == null || !BoardManager.GetSideRangeOf(ctx.source, out int s1301, out int e1301))
        {
            Debug.Log($"[01301] 无棋盘");
            yield break;
        }

        // ── 快照：先记录所有符合条件的同伴，后续依快照触发（即使中途被其他同伴退场也照常触发）──
        var targets = new System.Collections.Generic.List<(CardInstance ci, BoardSlot slot, int slotIdx)>();

        for (int i = s1301; i <= e1301; i++)
        {
            var slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID != "01111" && ci.templateID != "01301")
                targets.Add((ci, slot, i));
        }

        Debug.Log($"[01301] 快照: {targets.Count} 个同伴待触发");
        NestingContext.Enter("01301");

        for (int t = 0; t < targets.Count; t++)
        {
            var target = targets[t];
            var ci = target.ci; var slot = target.slot; var slotIdx = target.slotIdx;
            Debug.Log($"[01301] 同伴[{t+1}/{targets.Count}] {ci.templateID} slot={slotIdx} dispatch");
            var subCtx = EffectContext.ForExit(ci, slot, isActive);
            Trigger subTrigger = isActive ? Trigger.ActiveExit : Trigger.Exit;
            EffectDispatcher.Dispatch(subTrigger, subCtx);

            if (subCtx.StartedCoroutine != null)
            {
                Debug.Log($"[01301] 同伴[{t+1}] 协程等待... Nesting={NestingContext.Depth}");
                yield return subCtx.StartedCoroutine;
                Debug.Log($"[01301] 同伴[{t+1}] 协程完成 Nesting={NestingContext.Depth}");
            }
        }

        Debug.Log($"[01301] 结束: {targets.Count}个 Nesting={NestingContext.Depth}");
        NestingContext.Exit();
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
        // 特性级溯源：荣誉侍者"退场：对对方一召唤物造成2伤害"序号+文本。
        // 伤害在选中协程落地时源卡可能已销毁 → 在入口（源卡存活）先算好捕获。
        int exitIdx = ctx.source != null ? ctx.source.GetTraitIndexByKeyword("对对方一召唤物造成2伤害") : -1;
        string exitText = exitIdx > 0 ? ctx.source.GetTraitByIndex(exitIdx) : null;
        // 按死亡卡所属玩家动态确定"敌方半场"
        BoardManager.GetEnemySideRange(ctx.sourceSlot.slotID, out int enemyStart, out int enemyEnd);
        bool hasEnemy = false;
        var bmCheck = BM();
        for (int i = enemyStart; i <= enemyEnd; i++)
            if (bmCheck?.GetSlot(i)?.currentCard3D != null) { hasEnemy = true; break; }
        if (!hasEnemy) return;

        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(Handle01347ExitCoroutine(ctx, exitIdx, exitText));
    }

    static System.Collections.IEnumerator Handle01347ExitCoroutine(EffectContext ctx, int exitIdx, string exitText)
    {
        NestingContext.Enter("01347_Exit");

        NetworkPlayer owner = BoardManager.GetOwnerPlayer(ctx.sourceSlot.slotID);

        if (owner == NetworkPlayer.Remote && Mirror.NetworkServer.active
            && NetworkPlayer.Remote.connectionToClient != null)
        {
            // 远端玩家的卡：委托远端选择目标（AI 无连接走 else 本地选择）
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
                        BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 2, null, exitIdx, exitText);
                        t3d.UpdateValues();
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
            }
        }
        else
        {
            // 主机/离线：直接选择（BeginSelection 使 IsSelecting=true → WaitForSimultaneousWindow 阻塞）
            // AI 对局中，触发者是 AI 半场的卡 → AI 自动选择，不弹给玩家
            bool isAISide = SimpleAI.IsAIMatch && owner == NetworkPlayer.Remote;
            if (isAISide) SimpleAI.IsAIEvaluating = true;
            try
            {
                bool done = false;
                SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (target) =>
                {
                    if (target?.currentCard3D != null)
                    {
                        var t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                        if (t3d?.cardInstance != null)
                        {
                            BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 2, null, exitIdx, exitText);
                            t3d.UpdateValues();
                        }
                    }
                    BoardSlot.CheckAndHandleDeaths();
                    done = true;
                });
                yield return new WaitUntil(() => done);
            }
            finally
            {
                if (isAISide) SimpleAI.IsAIEvaluating = false;
            }
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
        ctx.StartedCoroutine = ctx.sourceSlot.StartCoroutine(Handle01111Routine(ctx));
    }

    static System.Collections.IEnumerator Handle01111Routine(EffectContext ctx)
    {
        var bm = UnityEngine.Object.FindObjectOfType<BoardManager>();
        Debug.Log($"[01111] 深潜者主动退场: slot={ctx.sourceSlot?.slotID} Nesting={NestingContext.Depth}");

        if (bm == null || !BoardManager.GetSideRangeOf(ctx.source, out int s1111, out int e1111))
        { yield break; }

        var targets = new System.Collections.Generic.List<(CardInstance ci, BoardSlot slot, int slotIdx)>();
        for (int i = s1111; i <= e1111; i++)
        {
            var slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID != "01111" && ci.templateID != "01301")
                targets.Add((ci, slot, i));
        }

        Debug.Log($"[01111] 快照: {targets.Count} 个同伴");
        NestingContext.Enter("01111");

        for (int t = 0; t < targets.Count; t++)
        {
            var target = targets[t];
            var ci = target.ci; var slot = target.slot; var slotIdx = target.slotIdx;
            Debug.Log($"[01111] 同伴[{t+1}/{targets.Count}] {ci.templateID} slot={slotIdx}");
            var subCtx = EffectContext.ForExit(ci, slot, true);
            EffectDispatcher.Dispatch(Trigger.ActiveExit, subCtx);

            if (subCtx.StartedCoroutine != null)
            {
                Debug.Log($"[01111] 同伴[{t+1}] 协程等待...");
                yield return subCtx.StartedCoroutine;
            }
        }

        Debug.Log($"[01111] 结束: {targets.Count}个");
        NestingContext.Exit();
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
