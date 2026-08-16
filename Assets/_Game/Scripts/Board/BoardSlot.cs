using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CardData;

public class BoardSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int slotID;
    public int opponentSlotID;
    public bool isBlocked = false;
    public bool permaBlocked = false; // 封锁者(01505)永久封锁——黑色高亮，不会被EndSelection清除
    public bool hasCard = false;
    public static System.Func<BoardSlot, bool> extraTargetFilter;
    public int spotlightTierBoost;   // 聚光灯阶位增幅
    public bool hasSpotlight;        // 是否有聚光灯效果
    private static float lastClickTime = 0f;
    public int plagueRoundCount;
    public bool hasPlague;
    public static bool isTargetingMode
    {
        get => SelectionManager.Instance != null && SelectionManager.Instance.IsSelecting;
        set { Debug.LogWarning("[BoardSlot] isTargetingMode setter is deprecated — use SelectionManager directly."); }
    }

    private static bool _isPlacingCard = false;
    private static bool _isReplaceMode = false;
    private static bool _isAttachSelectMode = false;

    public static bool isPlacingCard
    {
        get => _isPlacingCard;
        set => _isPlacingCard = value;
    }

    /// <summary>全局单调递增的卡牌放置世代号，用于替代 _placedAtTime 时间窗口去重。</summary>
    static int _globalPlacementGeneration;

    /// <summary>分配一个新的放置世代号并返回。</summary>
    public static int NextPlacementGeneration() => ++_globalPlacementGeneration;

    /// <summary>将客户端视角的 slotID 镜像映射为对端视角（0-5 ↔ 6-11）。</summary>
    public static int MirrorSlot(int slotID)
    {
        return slotID >= 6 ? slotID - 6 : slotID + 6;
    }

    // ── 超时常量 ──────────────────────────────────────────────
    public const float RPC_TIMEOUT = 30f;
    public const float PHASE_TIMEOUT = 20f;
    public const float BLOCK_WARNING = 10f;
    public static bool isReplaceMode
    {
        get => _isReplaceMode;
        set => _isReplaceMode = value;
    }
    public static bool isAttachSelectMode
    {
        get => _isAttachSelectMode;
        set => _isAttachSelectMode = value;
    }

    public static GameObject cardToPlace = null;
    public static TargetType currentTargetType = TargetType.None;
    static Action<BoardSlot> _onTargetSelected;
    /// <summary>选择回调。AI 环境下赋值时自动触发 AIResolveSelection（选第一个合法目标）。</summary>
    public static Action<BoardSlot> onTargetSelected
    {
        get => _onTargetSelected;
        set
        {
            _onTargetSelected = value;
            Debug.LogWarning($"[AIDebug] onTargetSelected setter: value={value != null}, IsAIEvaluating={SimpleAI.IsAIEvaluating}, currentTargetType={currentTargetType}");
            if (value != null && SimpleAI.IsAIEvaluating)
                AIResolveSelection();
        }
    }

    private Vector3 originalScale;
    public Image slotImage;
    public Color normalColor;
    public Color highlightColor = Color.yellow;

    public static bool attachCanBeIndependent = false;
    public int slotTempAttackBoost;
    private GameObject _currentCard;
    public static bool isStrengtheningSlot = false;

    /// <summary>退场后待处理的反击队列（同时窗口分界线）。
    /// 存储(死卡槽位ID, 反击效果文本, 伤害来源实例ID列表)。</summary>
    public static List<(int deadSlotID, string revengeEffect, List<string> sourceInstanceIDs)> pendingRevenges
        = new List<(int, string, List<string>)>();

    /// <summary>[Legacy] 无赖(01309)退场召唤阶段阻塞标记。已由 NestingContext.IsNested + WaitForSimultaneousWindow 替代外部等待链。</summary>
    public static bool _roguePhaseBlock;
    /// <summary>服务端等待远端客户端完成无赖(01309)召唤。</summary>
    public static bool _rogueRpcDone;
    public static void NotifyRogueRpcDone() { _rogueRpcDone = true; }
    /// <summary>远端客户端先手交互完成标记。服务端 FirstStrikeCoroutine 后检查此标记。</summary>
    public static bool _remoteFirstStrikeDone;
    public static void NotifyRemoteFirstStrikeDone() { _remoteFirstStrikeDone = true; }

    /// <summary>远端选择委托：等待标记 + 结果槽位。_remoteSelectionId 递增保证每次等待独立。</summary>
    public static int _remoteSelectionResultSlot = -1;
    public static int _remoteSelectionId = 0;
    public static void NotifyRemoteSelectionDone(int selectedSlot) { _remoteSelectionId++; _remoteSelectionResultSlot = selectedSlot; }

    /// <summary>
    /// AI 自动选择：离线 AI 环境中，扫描第一个合法目标并触发 onTargetSelected。
    /// 延迟一帧执行，确保 extraTargetFilter 等过滤条件已就位。
    /// </summary>
    public static void AIResolveSelection()
    {
        if (!SimpleAI.IsAIEvaluating) { Debug.LogWarning("[AIDebug] AIResolveSelection 被调用但 IsAIEvaluating=false，跳过"); return; }
        Debug.LogWarning("[AIDebug] AIResolveSelection 触发自动选择协程");
        if (SimpleAI.Instance != null)
            SimpleAI.Instance.StartCoroutine(AIResolveSelectionCoroutine());
    }

    static System.Collections.IEnumerator AIResolveSelectionCoroutine()
    {
        yield return null; // 延迟一帧
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) yield break;

        // AI 是 Remote，视角与 Host 相反。IsValidTarget 硬编码 Host 视角（0-5=敌，6-11=己），
        // 需镜像 targetType 才能选到 AI 视角的正确半场。
        TargetType aiType = MirrorTargetTypeForAI(currentTargetType);
        Debug.LogWarning($"[AIDebug] AIResolveSelectionCoroutine 执行: currentTargetType={currentTargetType}, aiType={aiType}");

        // 选 AI 己方召唤物（镜像后 SingleEnemy = AI 己方 0-5）：
        // 仅当目标是「有牌的召唤物」时用退场评分；若是选空槽（放置位置/囚牢），回退选第一个合法。
        if (aiType == TargetType.SingleEnemy)
        {
            // 先判断是否有「有牌」的合法目标
            bool hasMinionTarget = false;
            foreach (var slot in bm.GetAllSlots())
            {
                if (slot == null || !slot.IsValidTarget(aiType)) continue;
                if (slot.currentCard3D != null) { hasMinionTarget = true; break; }
            }

            if (hasMinionTarget)
            {
                // 有牌目标 → 退场评分选最优（带主动退场/可牺牲肉盾）
                float bestScore = float.MinValue;
                BoardSlot best = null;
                foreach (var slot in bm.GetAllSlots())
                {
                    if (slot == null || !slot.IsValidTarget(aiType)) continue;
                    var tci = slot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                    float sc = SimpleAI.ScoreRetreatTarget(tci, slot.slotID, true);
                    if (sc > bestScore) { bestScore = sc; best = slot; }
                }
                if (best != null) { onTargetSelected?.Invoke(best); yield break; }
            }
            else
            {
                // 空槽目标（囚牢/放置位置）→ 选第一个合法空槽
                foreach (var slot in bm.GetAllSlots())
                {
                    if (slot == null || !slot.IsValidTarget(aiType)) continue;
                    onTargetSelected?.Invoke(slot);
                    yield break;
                }
            }
            // 选不到任何合法目标 → 传 null 结束选择，防止选择状态残留（手牌隐藏/高亮残留，错误地让玩家选择）
            Debug.LogWarning($"[AIDebug] AI 选不到合法目标（aiType={aiType}），传 null 结束选择");
            onTargetSelected?.Invoke(null);
            yield break;
        }

        // 其他类型：选第一个合法目标
        foreach (var slot in bm.GetAllSlots())
        {
            if (slot == null) continue;
            if (slot.IsValidTarget(aiType))
            {
                onTargetSelected?.Invoke(slot);
                yield break;
            }
        }
        // 选不到任何合法目标 → 传 null 结束选择，防止选择状态残留
        Debug.LogWarning($"[AIDebug] AI 选不到合法目标（aiType={aiType}），传 null 结束选择");
        onTargetSelected?.Invoke(null);
    }

    /// <summary>把 Host 视角的 TargetType 镜像成 AI（Remote）视角。</summary>
    static TargetType MirrorTargetTypeForAI(TargetType t)
    {
        switch (t)
        {
            case TargetType.SingleEnemy: return TargetType.SingleAlly;
            case TargetType.SingleAlly: return TargetType.SingleEnemy;
            case TargetType.EnemyFrontRow: return TargetType.AllyFrontRow;
            case TargetType.EnemyBackRow: return TargetType.AllyBackRow;
            case TargetType.AllyFrontRow: return TargetType.EnemyFrontRow;
            case TargetType.AllyBackRow: return TargetType.EnemyBackRow;
            case TargetType.EnemyAnyRow: return TargetType.AllyAnyRow;
            case TargetType.AllyAnyRow: return TargetType.EnemyAnyRow;
            case TargetType.AllEnemies: return TargetType.AllAllies;
            case TargetType.AllAllies: return TargetType.AllEnemies;
            default: return t; // None / AllMinions 不变
        }
    }

    /// <summary>统一的目标选择辅助方法——自动根据目标拥有者决定本地/远程选择UI。</summary>
    public static IEnumerator WaitForPlayerSelection(int targetOwnerSlotID, TargetType targetType, System.Action<BoardSlot> onSelected, string reason = "")
    {
        NetworkPlayer targetOwner = BoardManager.GetOwnerPlayer(targetOwnerSlotID);
        if (targetOwner == null) yield break;

        if (!NetworkServer.active || targetOwner == NetworkPlayer.Local)
        {
            bool done = false;
            SelectionManager.Instance.BeginSelection(targetType, (s) => { onSelected?.Invoke(s); done = true; });
            yield return new WaitUntil(() => done);
        }
        else
        {
            // AI 无客户端连接 → 本地选择（SelectionManager 的 AI 自动选择分支处理）
            if (NetworkPlayer.Remote.connectionToClient == null)
            {
                bool doneLocal = false;
                SelectionManager.Instance.BeginSelection(targetType, (s) => { onSelected?.Invoke(s); doneLocal = true; });
                yield return new WaitUntil(() => doneLocal);
                yield break;
            }
            _remoteSelectionResultSlot = -1;
            int expectId = _remoteSelectionId + 1;
            NetworkPlayer.Remote.TargetRequestSelection(
                NetworkPlayer.Remote.connectionToClient, (int)targetType, targetOwnerSlotID);
            float deadline = Time.time + 30f;
            yield return new WaitUntil(() => _remoteSelectionId >= expectId || Time.time > deadline);
            if (_remoteSelectionResultSlot >= 0)
            {
                var slot = FindObjectOfType<BoardManager>()?.GetSlot(_remoteSelectionResultSlot);
                onSelected?.Invoke(slot);
            }
        }
    }

    /// <summary>远端客户端执行己方的交互式先手（槽位6-11）。完成后通知服务端解除阻塞。</summary>
    public IEnumerator RunRemoteFirstStrikes()
    {
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) { NetworkPlayer.Local?.CmdRemoteFirstStrikeDone(); yield break; }

        // ===== 第1轮：先手换位（阻塞阶段推进，全部交换完成再进行伤害）=====
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || !ci.hasFirstStrike) continue;

            // 排队——等前一个交互弹窗完成
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            yield return null;

            switch (ci.templateID)
            {
                case "01312":
                {
                    int mySlot = i;
                    int ownHalfStart = 6;
                    int col = mySlot % 3;
                    int row = (mySlot - ownHalfStart) < 3 ? 0 : 3;
                    int rowStart = ownHalfStart + row;
                    int otherRowStart = ownHalfStart + (row == 0 ? 3 : 0);
                    var slots = bm.GetAllSlots();

                    List<int> adjacent = new List<int>();
                    if (col > 0) adjacent.Add(rowStart + col - 1);
                    if (col < 2) adjacent.Add(rowStart + col + 1);
                    adjacent.Add(otherRowStart + col);
                    adjacent.RemoveAll(s => slots[s].isBlocked);
                    if (adjacent.Count == 0) continue;

                    bool confirmed = false, choseYes = false;
                    ConfirmPanel.Instance.Show("是否与相邻格子互换位置？",
                        () => { choseYes = true; confirmed = true; },
                        () => { confirmed = true; });
                    yield return new WaitUntil(() => confirmed);
                    if (!choseYes) continue;

                    bool done = false;
                    string layerId = SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                    BoardSlot.extraTargetFilter = (s) => adjacent.Contains(s.slotID);
                    BoardSlot.isStrengtheningSlot = true;
                    BoardSlot ts = null;
                    BoardSlot.onTargetSelected = (t) =>
                    {
                        if (t != null && adjacent.Contains(t.slotID))
                        { ts = t; SelectionManager.Instance.EndSelection(layerId); BoardSlot.isStrengtheningSlot = false; BoardSlot.extraTargetFilter = null; done = true; }
                    };
                    yield return new WaitUntil(() => done);
                    if (ts == null) continue;

                    int slotA = mySlot;
                    int slotB = ts.slotID;
                    BoardManager.SwapCards(slotA, slotB);
                    ci.hasFirstStrike = false;

                    // 通知服务端同步交换结果（远端 6-11 → 服务端映射为 0-5）
                    NetworkPlayer.Local?.CmdSwapCards(slotA, slotB);
                    break;
                }
                case "01513":
                {
                    var sel = SelectionManager.Instance;
                    var cb = ConfirmSelectionButton.Instance;
                    if (sel == null || cb == null) break;
                    BoardSlot.isStrengtheningSlot = true;
                    BoardSlot.extraTargetFilter = (s2) => { var c = s2?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance; return c != null && c.prefixes.Contains("机械"); };
                    sel.BeginSelection(TargetType.SingleAlly, null);
                    BoardSlot first = null; bool dd = false;
                    cb.Show(() => dd = true);
                    BoardSlot.onTargetSelected = (s2) =>
                    {
                        if (first == null) first = s2;
                        else if (s2 != first)
                        {
                            BoardManager.SwapCards(first.slotID, s2.slotID);
                            NetworkPlayer.Local?.CmdSwapCards(first.slotID, s2.slotID);
                            first = null;
                        }
                    };
                    yield return new WaitUntil(() => dd);
                    sel.ForceEndAll();
                    BoardSlot.isStrengtheningSlot = false;
                    BoardSlot.extraTargetFilter = null;
                    cb.Hide();
                    ci.hasFirstStrike = false;
                    break;
                }
                case "01516":
                {
                    var sel16 = SelectionManager.Instance;
                    var cb16 = ConfirmSelectionButton.Instance;
                    if (sel16 == null || cb16 == null) break;
                    BoardSlot.isStrengtheningSlot = true;
                    sel16.BeginSelection(TargetType.SingleAlly, null);
                    BoardSlot first = null; bool dd = false;
                    cb16.Show(() => dd = true);
                    BoardSlot.onTargetSelected = (s2) =>
                    {
                        if (first == null) first = s2;
                        else if (s2 != first)
                        {
                            BoardManager.SwapCards(first.slotID, s2.slotID);
                            NetworkPlayer.Local?.CmdSwapCards(first.slotID, s2.slotID);
                            first = null;
                        }
                    };
                    yield return new WaitUntil(() => dd);
                    sel16.ForceEndAll();
                    BoardSlot.isStrengtheningSlot = false;
                    cb16.Hide();
                    ci.hasFirstStrike = false;
                    break;
                }
                // 非交换先手 → buff/debuff/伤害由第2/3轮分别处理
                case "03012":
                case "01519":
                case "01318":
                case "03502":
                case "01310":
                case "03506":
                case "03513":
                case "03005":
                case "03003":
                case "03020":
                    break;
                default:
                    Debug.LogWarning($"[BoardSlot] RunRemoteFirstStrikes 未处理的 templateID: {ci.templateID}");
                    break;
            }
        }

        // ===== 第2轮：先手buff（换位完成后执行）=====
        for (int i2 = 6; i2 <= 11; i2++)
        {
            BoardSlot slot2 = bm.GetSlot(i2);
            if (slot2?.currentCard3D == null) continue;
            CardInstance ci2 = slot2.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci2 == null || !ci2.hasFirstStrike) continue;

            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            yield return null;

            switch (ci2.templateID)
            {
                case "03012": // 阴阳：友方攻血平衡 → Cmd 委托服务端处理
                {
                    bool selDone = false;
                    SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (t) =>
                    {
                        if (t != null && t != slot2 && t.currentCard3D != null)
                            NetworkPlayer.Local?.Cmd03012FirstStrike(t.slotID);
                        selDone = true;
                    });
                    while (!selDone) yield return null;
                    ci2.hasFirstStrike = false;
                    break;
                }
                case "01519": // 守护骑士：给友方上护盾 → Cmd 委托服务端处理
                {
                    var cdd = new List<BoardSlot>();
                    for (int j = 6; j <= 11; j++)
                    { var s3 = bm.GetSlot(j); if (s3?.currentCard3D != null && j != i2) { var bc = s3.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance; if (bc != null && !bc.hasShield && !bc.isAttached) cdd.Add(s3); } }
                    if (cdd.Count == 0) continue;
                    if (cdd.Count <= 3) { foreach (var cs in cdd) NetworkPlayer.Local?.Cmd01519FirstStrike(cs.slotID); }
                    else
                    {
                        var sel = new List<BoardSlot>();
                        var sel19 = SelectionManager.Instance;
                        string lid2 = sel19.BeginSelection(TargetType.SingleAlly, null);
                        BoardSlot.isStrengtheningSlot = true;
                        BoardSlot.onTargetSelected = (t) =>
                        {
                            if (t == null || !cdd.Contains(t)) return;
                            if (sel.Contains(t)) { sel.Remove(t); t.SetHighlightColor(t.GetNormalColor()); }
                            else if (sel.Count < 3) { sel.Add(t); t.SetHighlightColor(Color.yellow); }
                            if (sel.Count == 3) { foreach (var s3 in sel) { NetworkPlayer.Local?.Cmd01519FirstStrike(s3.slotID); s3.SetHighlightColor(s3.GetNormalColor()); } sel19.EndSelection(lid2); }
                        };
                        yield return new WaitUntil(() => !sel19.IsSelecting);
                        BoardSlot.isStrengtheningSlot = false;
                    }
                    ci2.hasFirstStrike = false;
                    break;
                }
                case "01531": // 亡命之徒：由服务端 FirstStrikeCoroutine 权威处理，远端不再重复执行
                    break;
            }
        }

        // ===== 第3轮：先手debuff（buff完成后执行）=====
        for (int i3 = 6; i3 <= 11; i3++)
        {
            BoardSlot slot3 = bm.GetSlot(i3);
            if (slot3?.currentCard3D == null) continue;
            CardInstance ci3 = slot3.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci3 == null || !ci3.hasFirstStrike) continue;

            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            yield return null;

            switch (ci3.templateID)
            {
                case "01318": // 弱化棱晶：目标攻击力→1 → Cmd 委托服务端处理
                {
                    bool dd = false;
                    SelectionManager.Instance.BeginSelection(TargetType.AllMinions, (t) =>
                    {
                        if (t?.currentCard3D != null)
                            NetworkPlayer.Local?.Cmd01318FirstStrike(t.slotID);
                        dd = true;
                    });
                    yield return new WaitUntil(() => dd);
                    ci3.hasFirstStrike = false;
                    break;
                }
                case "03502": // 毒巫：清护盾+中毒 → Cmd 委托服务端处理
                {
                    bool hasEnemy = false;
                    for (int j = 0; j <= 5; j++) if (bm.GetSlot(j)?.currentCard3D != null) { hasEnemy = true; break; }
                    if (!hasEnemy) continue;
                    bool dd = false;
                    SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (t) =>
                    {
                        if (t?.currentCard3D != null)
                            NetworkPlayer.Local?.Cmd03502FirstStrike(t.slotID);
                        dd = true;
                    });
                    while (!dd) yield return null;
                    ci3.hasFirstStrike = false;
                    break;
                }
            }
        }

        // 第4轮(伤害：01310/03005/03003/03506/03513/03020+赋予先手)——
        // 由服务端 FirstStrikeCoroutine 权威处理，结果通过 MarkDirty 同步。

        TurnManager.SyncMyBoardToOpponent();
        // 远端先手完毕后立即清零临时攻击力字段——BattleCoroutine/FinalDamage 不会在远端执行
        // 01318 可选择任意目标(AllMinions)，需覆盖全部 12 槽（含敌方 0-5）
        var bmRefresh = FindObjectOfType<BoardManager>();
        if (bmRefresh != null)
            for (int ri = 0; ri <= 11; ri++)
            {
                var rci = bmRefresh.GetSlot(ri)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (rci != null && (rci.tempAttackBoost != 0 || rci.originalAttackBeforeDebuff != 0))
                {
                    rci.tempAttackBoost = 0;
                    rci.originalAttackBeforeDebuff = 0;
                    bmRefresh.GetSlot(ri)?.currentCard3D?.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
        NetworkPlayer.Local?.CmdRemoteFirstStrikeDone();
    }
    public bool prisonBlocked;      // 囚牢封锁
    public bool prisonAllowYuan;    // 允许放置渊前缀召唤物（仅己方封锁格子）
    public int deepSeaAttackDebuff; // 格子攻击力减益
    public bool deepSeaHealthDebuff; // 格子每阶段扣血标记
    public bool deepSeaMarked;        // 深海恶物蓝色高亮标记（纯视觉）
    /// <summary>本槽位上最后一次 HandleDeath 触发时间（秒）。EnsureCard 检查此时间戳防止从过期同步数据重建已死亡模型。</summary>
    public float lastHandleDeathTime = -1f;
    public static int ignoreNextClickSlot = -1;
    public static bool _deepSeaRevengeWaiting;
    public static int _deepSeaRevengeTargetSlot = -1;
    public static void NotifyDeepSeaRevengeDone(int serverSlot) { _deepSeaRevengeTargetSlot = serverSlot; _deepSeaRevengeWaiting = false; }
    // 01527 为己方一召唤物+2+1 反击 RPC 委托
    public static bool _allyBuffRevengeWaiting;
    public static int _allyBuffRevengeTargetSlot = -1;
    public static void NotifyAllyBuffRevengeDone(int serverSlot) { _allyBuffRevengeTargetSlot = serverSlot; _allyBuffRevengeWaiting = false; }
    // 01347 荣誉侍者：退场→对敌方造成2伤害 目标选择委托
    public static bool _honorAttendantExitWaiting;
    public static int _honorAttendantExitTarget = -1;
    public static void NotifyHonorAttendantExitDone(int serverSlot) { _honorAttendantExitTarget = serverSlot; _honorAttendantExitWaiting = false; }
    // 01535 执行之剑：攻击回合开始选择目标委托
    public static bool _executionSwordWaiting;
    public static int _executionSwordTargetSlot = -1;
    public static int _executionSwordDamage;
    public static void NotifyExecutionSwordDone(int serverSlot) { _executionSwordTargetSlot = serverSlot; _executionSwordWaiting = false; }
    // 01526 忤逆者：远程消耗手牌委托
    public static bool _rebelConsumeDone;
    public static void NotifyRebelConsumeDone() { _rebelConsumeDone = true; }
    // 01522 殉难者远程委托：_martyrRpcDone + _martyrBuffSlot
    public static bool _martyrRpcDone;
    public static int _martyrBuffSlot = -1;
    public static void NotifyMartyrRpcDone(int targetServerSlot) { _martyrBuffSlot = targetServerSlot; _martyrRpcDone = true; }
    // 01347 荣誉侍者：主动退场完成标记
    public static bool _honorAttendantDone;
    void Start()
    {
        currentCard3D = null;
        slotImage = GetComponent<Image>();
        originalScale = transform.localScale;
        normalColor = slotImage.color;
    }
    // 从CardInstance提取数据包
    public class DeathEffectData
    {
      
        public int slotID;
        public string templateID;
        public string instanceID;
        public bool isActiveExit;
        public bool hasOnDeath;
        public bool hasActiveExit;
        public bool hasRevenge;
        public bool hasFirstStrike;
        public bool hasOnEnter;
        public bool hasDiscard;
        public string revengeEffect;
        public List<string> giveableDeathTraits;
        public List<string> grantedTraitTexts;
        public List<string> damageSourceInstanceIDs;
        public bool handledReturnToHand;
        public bool silencedThisPhase;
        public bool isFullySilenced;
        public bool isDeathBlocked;
        
        public int currentCost;
        public int currentAttack;
        public int currentHealth;
        public int currentMaxHealth;
        public int currentTier;
        public string prefixes;
        public SummonType summonType;
   
        public bool poisoned;
        public bool isXValue;
        public bool xAttackReadsHighest;
        public bool xHealthReadsHighest;
        public int xAccumulatedDamage;
        public int xInitialHealth;
        public int tempAttackBoost;
        public int tempHealthBoost;
        public bool hasShield;
        public bool shieldIsPermanent;
        public bool shieldEndAtBattleStart;
        public bool shieldEndAtBattleEnd;
        public bool isAttached;
        public int hostSlotID;
        public int attachOrder;
        public bool canAttach;
        public bool attacksFrontRow;
        public bool attacksBackRow;
        public bool isYinYang;
        public bool buffedBySage;
        public bool buffedByEmperor;
        public bool overclocked;
        public bool cannotHeal;
        public string braveTemplateID;
        public int greedySnakeEnterCount;
        public bool merchantDiscounted;
        public bool energyReaperDiscounted;
        public bool _justTransformed;
        public int prisonMySlot;
        public int prisonEnemySlot;
        public int ironSmithTotalConsumedCount;
        public int ironSmithOneCostConsumedCount;
        public bool _conductorDoubleDeath;
        public int scrollCorePhaseCount;
  
    }

    // 从CardInstance提取数据包
    public static DeathEffectData ExtractDeathData(CardInstance ci)
    {
        if (ci == null) return null;
        return new DeathEffectData
        {
            hasActiveExit = ci.hasActiveExit,
            hasRevenge = ci.hasRevenge,
            templateID = ci.templateID,
            isActiveExit = ci.isActiveExit,
            hasOnDeath = ci.hasOnDeath,
            revengeEffect = ci.revengeEffect,
            giveableDeathTraits = ci.giveableDeathTraits != null ? new List<string>(ci.giveableDeathTraits) : null,
            grantedTraitTexts = ci.grantedTraitTexts != null ? new List<string>(ci.grantedTraitTexts) : null,
            hasFirstStrike = ci.hasFirstStrike,
            hasOnEnter = ci.hasOnEnter,
            hasDiscard = ci.hasDiscard,
            currentCost = ci.currentCost,
            currentAttack = ci.currentAttack,
            currentHealth = ci.currentHealth,
            currentMaxHealth = ci.currentMaxHealth,
            currentTier = ci.currentTier,
            prefixes = ci.prefixes,
            summonType = ci.summonType,
            handledReturnToHand = ci.handledReturnToHand,
            silencedThisPhase = ci.silencedThisPhase,
            poisoned = ci.poisoned,
            isXValue = ci.isXValue,
            xAttackReadsHighest = ci.xAttackReadsHighest,
            xHealthReadsHighest = ci.xHealthReadsHighest,
            xAccumulatedDamage = ci.xAccumulatedDamage,
            xInitialHealth = ci.xInitialHealth,
            tempAttackBoost = ci.tempAttackBoost,
            tempHealthBoost = ci.tempHealthBoost,
            hasShield = ci.hasShield,
            shieldIsPermanent = ci.shieldIsPermanent,
            shieldEndAtBattleStart = ci.shieldEndAtBattleStart,
            shieldEndAtBattleEnd = ci.shieldEndAtBattleEnd,
            isAttached = ci.isAttached,
            hostSlotID = ci.hostSlotID,
            attachOrder = ci.attachOrder,
            canAttach = ci.canAttach,
            attacksFrontRow = ci.attacksFrontRow,
            attacksBackRow = ci.attacksBackRow,
            isYinYang = ci.isYinYang,
            buffedBySage = ci.buffedBySage,
            buffedByEmperor = ci.buffedByEmperor,
            overclocked = ci.overclocked,
            cannotHeal = ci.cannotHeal,
            braveTemplateID = ci.braveTemplateID,
            greedySnakeEnterCount = ci.greedySnakeEnterCount,
            merchantDiscounted = ci.merchantDiscounted,
            energyReaperDiscounted = ci.energyReaperDiscounted,
            _justTransformed = ci._justTransformed,
            prisonMySlot = ci.prisonMySlot,
            prisonEnemySlot = ci.prisonEnemySlot,
            ironSmithTotalConsumedCount = ci.ironSmithTotalConsumedCount,
            ironSmithOneCostConsumedCount = ci.ironSmithOneCostConsumedCount,
            _conductorDoubleDeath = ci._conductorDoubleDeath,
            scrollCorePhaseCount = ci.scrollCorePhaseCount,
            instanceID = ci.instanceID,
            damageSourceInstanceIDs = ci.damageSourceInstanceIDs != null ? new List<string>(ci.damageSourceInstanceIDs) : null,
            isFullySilenced = GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci),
            isDeathBlocked = GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(ci, "退场"),
        };
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isBlocked) return;

        if (prisonBlocked)
        {
            if (isPlacingCard && slotID >= 6 && prisonAllowYuan)
            {
                CardInstance ci = cardToPlace?.GetComponent<CardInstance>();
                if (ci != null && ci.prefixes.Contains("渊"))
                {
                    transform.localScale = originalScale * 1.15f;
                    slotImage.color = highlightColor;
                    return;
                }
            }
            transform.localScale = originalScale;
            slotImage.color = new Color(0.6f, 0.2f, 0.8f);
            return;
        }

        if (hasPlague)
        {
            slotImage.color = Color.green;
            return;
        }

        if (isPlacingCard && !isReplaceMode)
        {
            FakeEnemyPlayButton.GetSlotRange(out int minSlot, out int maxSlot);
            if (slotID >= minSlot && slotID <= maxSlot && !hasCard)
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
        }
        if (isPlacingCard && isReplaceMode)
        {
            FakeEnemyPlayButton.GetSlotRange(out int minSlot, out int maxSlot);
            if (slotID >= minSlot && slotID <= maxSlot && hasCard)
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
        }
        if (isAttachSelectMode && slotID >= 6)
        {
            if (hasCard || (attachCanBeIndependent && !hasCard))
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
        }
        if (isTargetingMode && !isAttachSelectMode && !isReplaceMode && IsValidTarget(currentTargetType))
        {
            if (currentTargetType == TargetType.SingleAlly || currentTargetType == TargetType.SingleEnemy || currentTargetType == TargetType.AllMinions)
            {
                transform.localScale = originalScale * 1.15f;
                slotImage.color = highlightColor;
            }
            else
            {
                HighlightRow(true);
            }
        }
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isBlocked)
        {
            slotImage.color = Color.black;
            return;
        }

        if (prisonBlocked)
        {
            slotImage.color = new Color(0.6f, 0.2f, 0.8f);
            return;
        }

        if (hasPlague)
        {
            slotImage.color = Color.green;
            return;
        }

        if (deepSeaMarked)
        {
            slotImage.color = Color.blue;
            return;
        }

        if (isTargetingMode && IsValidTarget(currentTargetType))
            HighlightRow(false);

        transform.localScale = originalScale;
        if (isBlocked) slotImage.color = Color.gray;
        else if (prisonBlocked) slotImage.color = new Color(0.6f, 0.2f, 0.8f);
        else if (hasPlague) slotImage.color = Color.green;
        else if (deepSeaMarked) slotImage.color = Color.blue;
        else slotImage.color = normalColor;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (CardView.IsAnyCardDragging || Time.time - lastClickTime < 0.2f)
        {
            return;
        }
        lastClickTime = Time.time;
        if (isTargetingMode && IsValidTarget(currentTargetType))
        {
            onTargetSelected?.Invoke(this);
            return;
        }

        if (isPlacingCard && isReplaceMode && slotID >= 6 && hasCard && cardToPlace != null)
        {
            CardInstance inst = cardToPlace.GetComponent<CardInstance>();
            if (inst != null && inst.canAttach && attachCanBeIndependent)
            {
                ReplaceOrAttachModal.Instance.Show(
                    onReplace: () => { ExecuteReplace(this); },
                    onAttach: () => { ExecuteAttach(this); }
                );
            }
            else
            {
                ExecuteReplace(this);
            }
            return;
        }
        FakeEnemyPlayButton.GetSlotRange(out int minSlot, out int maxSlot);
        if (isPlacingCard && slotID >= minSlot && slotID <= maxSlot && !hasCard && !isReplaceMode && cardToPlace != null)
        {
            if (isBlocked) return;
            if (prisonBlocked && slotID >= 6 && prisonAllowYuan)
            {
                CardInstance checkCI = cardToPlace?.GetComponent<CardInstance>();
                if (checkCI == null || !checkCI.prefixes.Contains("渊")) return;
            }
            else if (prisonBlocked)
            {
                return;
            }

            if (ignoreNextClickSlot >= 0 && slotID == ignoreNextClickSlot)
            {
                ignoreNextClickSlot = -1;
                return;
            }
            ignoreNextClickSlot = -1;

            string playTemplateID = "";
            CardInstance ciPre = cardToPlace?.GetComponent<CardInstance>();
            if (ciPre != null) playTemplateID = ciPre.templateID;

            bool wasAttachFlow = ciPre != null && ciPre.canAttach;

            HandManager hm = FindObjectOfType<HandManager>();
            if (hm != null)
            {
                hm.PlaceCardToSlot(this, cardToPlace);

                CardInstance inst = currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                CardData template = CardDatabase.Instance?.GetTemplate(inst?.templateID);

                // 蛊惑之音重定向：生命值降为1
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.PendingEnterRedirectTemplate != null
                    && template == GlobalEventManager.Instance.PendingEnterRedirectTemplate)
                {
                    GlobalEventManager.Instance.PendingEnterRedirectInstance = inst;
                    inst.currentHealth = 1;
                    inst.currentMaxHealth = Mathf.Max(1, inst.currentMaxHealth);
                    currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }

                HandManager hmX = FindObjectOfType<HandManager>();
                if (hmX != null)
                {
                    BoardManager bmX = FindObjectOfType<BoardManager>();
                    if (bmX != null)
                    {
                        for (int i = 6; i <= 11; i++)
                        {
                            BoardSlot slotX = bmX.GetSlot(i);
                            if (slotX?.currentCard3D == null) continue;
                            CardInstance ciX = slotX.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ciX != null && ciX.isXValue)
                                hmX.UpdateXValues(ciX);
                        }
                    }
                }

                // ❗isPlacingCard 不在此处清除——由 CleanupAfterPlacement 在 StartOnEnterEffect 完成后调用。
                // RogueDoSummon 等协程依赖 isPlacingCard 阻塞来等待进场效果子树完全结束。

                // 反制牌检查已由 CmdPlayCard 统一处理（覆盖全部 cardType，不限于 hasOnEnter）。
                // 保留此注释标记此处为进场效果前的自然时序点。

                if (template != null && template.hasOnEnter && inst != null)
                {
                    StartCoroutine(StartOnEnterEffect(template, inst));
                }

                // 清理重定向标记
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.PendingEnterRedirectTemplate == template)
                {
                    GlobalEventManager.Instance.PendingEnterRedirectTemplate = null;
                    GlobalEventManager.Instance.PendingEnterRedirectInstance = null;
                }
            }

            // 附着流程：模型尚未放置（PlaceCardToSlot 异步等待选择目标），由 HandManager.PlaceCardToSlot 回调处理同步和清理
            if (wasAttachFlow) return;

            // CmdPlayCard 已在 PlaceIndependentCard/PlaceAttachedCard 中发送——此处不再重复。
            // 仅同步 grantedTraitTexts 和 MarkDirty。
            if (NetworkClient.isConnected && !string.IsNullOrEmpty(playTemplateID))
            {
                Card3DInstance placedC3D = currentCard3D?.GetComponent<Card3DInstance>();
                if (placedC3D?.cardInstance?.grantedTraitTexts?.Count > 0)
                    NetworkPlayer.Local?.CmdSyncGrantedTraits(slotID, string.Join(";;", placedC3D.cardInstance.grantedTraitTexts));
                BoardSyncManager.MarkDirty();
            }

            // 协程型进场效果尚未完成——跳过 CleanupAfterPlacement，由协程末尾自行清理
            CardInstance ciAfter = currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ciAfter != null && ciAfter._hasPendingCoroutine) return;

            CleanupAfterPlacement();
            return;
        }
    }

    public bool IsValidTarget(TargetType type)
    {
        if (isAttachSelectMode)
        {
            if (slotID >= 6 && slotID <= 11)
            {
                if (hasCard) return true;
                if (attachCanBeIndependent) return true;
            }
            return false;
        }
        int[] ids = GetRowSlots(type);
        foreach (int id in ids)
        {
            if (id == slotID)
            {
                if (extraTargetFilter != null && !extraTargetFilter(this)) return false;
                return true;
            }
        }
        return false;
    }

    int[] GetRowSlots(TargetType type)
    {
        switch (type)
        {
            case TargetType.EnemyFrontRow: return new int[] { 0, 1, 2 };
            case TargetType.EnemyBackRow: return new int[] { 3, 4, 5 };
            case TargetType.AllyFrontRow: return new int[] { 6, 7, 8 };
            case TargetType.AllyBackRow: return new int[] { 9, 10, 11 };
            case TargetType.AllEnemies: return new int[] { 0, 1, 2, 3, 4, 5 };
            case TargetType.AllAllies: return new int[] { 6, 7, 8, 9, 10, 11 };
            case TargetType.EnemyAnyRow:
                if (slotID >= 0 && slotID <= 5) return new int[] { slotID < 3 ? 0 : 3, slotID < 3 ? 1 : 4, slotID < 3 ? 2 : 5 };
                break;
            case TargetType.AllyAnyRow:
                if (slotID >= 6 && slotID <= 11) return new int[] { slotID < 9 ? 6 : 9, slotID < 9 ? 7 : 10, slotID < 9 ? 8 : 11 };
                break;
            case TargetType.AllMinions:
                return new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            case TargetType.SingleEnemy:
                if (isStrengtheningSlot)
                {
                    // 放置模式：排除所有封锁（普通封锁/囚牢/永久封锁）
                    if (slotID >= 0 && slotID <= 5 && !isBlocked && !prisonBlocked && !permaBlocked) return new int[] { slotID };
                }
                else
                {
                    if (slotID >= 0 && slotID <= 5 && hasCard) return new int[] { slotID };
                }
                break;
            case TargetType.SingleAlly:
                if (isStrengtheningSlot)
                {
                    // 放置模式：排除所有封锁（普通封锁/囚牢/永久封锁）
                    if (slotID >= 6 && slotID <= 11 && !isBlocked && !prisonBlocked && !permaBlocked) return new int[] { slotID };
                }
                else
                {
                    if (slotID >= 6 && slotID <= 11 && hasCard) return new int[] { slotID };
                }
                break;
            default:
                Debug.LogWarning($"[BoardSlot] GetRowSlots 未处理的 TargetType: {type}");
                break;
        }
        return new int[0];
    }

    void HighlightRow(bool highlight)
    {
        if (currentTargetType == TargetType.SingleAlly || currentTargetType == TargetType.SingleEnemy)
        {
            transform.localScale = highlight ? originalScale * 1.15f : originalScale;
            slotImage.color = highlight ? highlightColor : normalColor;
            return;
        }
        int[] rowSlots = GetRowSlots(currentTargetType);
        if (rowSlots == null) return;
        foreach (int id in rowSlots)
        {
            BoardSlot slot = FindObjectOfType<BoardManager>()?.GetSlot(id);
            if (slot != null)
            {
                slot.transform.localScale = highlight ? originalScale * 1.15f : originalScale;
                slot.slotImage.color = highlight ? highlightColor : normalColor;
            }
        }
    }

    public IEnumerator StartOnEnterEffect(CardData template, CardInstance inst)
    {

        Debug.Log($"StartOnEnterEffect: template={template?.cardName}, templateID={template?.templateID}");
        if (template == null || string.IsNullOrEmpty(template.templateID)) yield break;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(inst, "进场"))
        {
            CleanupAfterPlacement();
            yield break;
        }
                // 清理重定向标记
        if (GlobalEventManager.Instance?.PendingEnterRedirectInstance == inst)
        {
            CardData redirectTemplate = GlobalEventManager.Instance.PendingEnterRedirectTemplate;
            bool redirectToHost = GlobalEventManager.Instance.PendingEnterRedirectToHost;
            GlobalEventManager.Instance.PendingEnterRedirectTemplate = null;
            GlobalEventManager.Instance.PendingEnterRedirectInstance = null;

            TargetType redirectTargetType = redirectToHost ? TargetType.SingleAlly : TargetType.SingleEnemy;

            bool hasTarget = false;
            BoardManager bmRedirect = FindObjectOfType<BoardManager>();
            if (bmRedirect != null)
            {
                int start = redirectToHost ? 6 : 0;
                int end = redirectToHost ? 11 : 5;
                for (int j = start; j <= end; j++)
                    if (bmRedirect.GetSlot(j)?.currentCard3D != null) { hasTarget = true; break; }
            }

            if (!hasTarget)
            {
                CleanupAfterPlacement();
                yield break;
            }

            SelectionManager.Instance.BeginSelection(redirectTargetType, (target) =>
            {
                if (target?.currentCard3D != null)
                {
                    CardInstance targetInst = target.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (targetInst != null && redirectTemplate != null)
                        target.StartCoroutine(target.StartOnEnterEffect(redirectTemplate, targetInst));
                }
                CleanupAfterPlacement();
            });
            yield break;
        }

        if (template.templateID == "01309") {CleanupAfterPlacement();yield break;}

        // ── Step 3: 进场效果分发（新 → EffectRegistry，回退 → 旧 switch）──
        NestingContext.Enter($"Enter_{template.templateID}");
        try
        {
        if (inst != null) { inst._enterEffectRunning = true; inst._hadEnterEffect = true; }
        var enterCtx = EffectContext.ForEnter(template, inst, this);
        if (EffectDispatcher.Dispatch(Trigger.Enter, enterCtx))
        {
            if (enterCtx.StartedCoroutine != null)
            {
                // 等待 handler 协程完成（协程末尾调用 CleanupAfterPlacement）
                yield return enterCtx.StartedCoroutine;

                // 嵌套同时树结算（法术伤害→死亡→退场→反击）
                int myDepth = NestingContext.Snapshot();
                CheckAndHandleDeaths();
                yield return ActionQueueManager.WaitForDrain();
                // 等待由本层死亡触发的所有子嵌套完成
                yield return new WaitWhile(() => NestingContext.Depth > myDepth);
                if (pendingRevenges.Count > 0 && BattleManager.Instance != null)
                    yield return BattleManager.Instance.StartCoroutine(
                        BattleManager.ResolveRevengesFromSnapshot());
            }
            // 同步型 handler 已在内部调用 CleanupAfterPlacement
            NestingContext.Exit();
            // 仅最外层 StartOnEnterEffect 清除标记——嵌套调用不碰，防止破坏外层的 _enterEffectRunning
            if (!NestingContext.IsNested)
            {
                if (inst != null) inst._enterEffectRunning = false;
                isPlacingCard = false;
                cardToPlace = null;
            }
            yield break;
        }

        // ── 未注册卡回退 ───────────────────────────────────
        Debug.LogWarning($"[StartOnEnterEffect] 未注册: {template.templateID}");
        CleanupAfterPlacement();
        NestingContext.Exit();
        }
        finally
        {
            // 安全网：若任意异常路径导致 Exit 未执行，强制复位以防 Depth 永久泄漏
            if (NestingContext.Depth > 0)
            {
                Debug.LogWarning($"[NestingContext] StartOnEnterEffect 异常退出，强制复位深度 depth={NestingContext.Depth}");
                NestingContext.ForceClear("StartOnEnterEffect leak");
            }
        }
    }

  

    public void CleanupAfterPlacement()
    {
        if (currentCard3D != null)
        {
            var crd = currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            // 嵌套内不清 _enterEffectRunning——仅最外层 StartOnEnterEffect 负责清理
            if (crd != null && !NestingContext.IsNested) crd._enterEffectRunning = false;
        }
        isPlacingCard = false;
        cardToPlace = null;

        if (!isTargetingMode && !isAttachSelectMode)
        {
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.SetHandAreaRaycast(true);
            hm?.ShowAllCards();
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(true);
        }

        // 手牌为空时强制启用按钮（防止放置/效果链路中残留禁用状态）
        NetworkPlayer.Local?.handCards.RemoveAll(c => c == null);
        if (NetworkPlayer.Local != null && NetworkPlayer.Local.handCards.Count == 0)
        {
            EndTurnButton endBtn = FindObjectOfType<EndTurnButton>();
            if (endBtn != null)
            {
                CanvasGroup endCG = endBtn.GetComponent<CanvasGroup>() ?? endBtn.gameObject.AddComponent<CanvasGroup>();
                endCG.interactable = true;
                endCG.blocksRaycasts = true;
            }
            DrawCardUI drawUI = FindObjectOfType<DrawCardUI>();
            if (drawUI != null)
            {
                CanvasGroup drawCG = drawUI.GetComponent<CanvasGroup>() ?? drawUI.gameObject.AddComponent<CanvasGroup>();
                drawCG.interactable = true;
                drawCG.blocksRaycasts = true;
            }
        }

        BoardSyncManager.MarkDirty();
    }

    public void SetBlocked(bool blocked)
    {
        isBlocked = blocked;
        slotImage.color = blocked ? Color.gray : normalColor;
    }

    public void SetCard(GameObject card3D)
    {
        currentCard3D = card3D;
        hasCard = card3D != null;
        if (card3D != null)
        {
            var ci = card3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null)
            {
                ci._placedAtTime = Time.time;
                ci.placementGeneration = BoardSlot.NextPlacementGeneration();
                ci.isDead = false;
            }
            lastHandleDeathTime = -1f; // 新卡入槽，重置死亡时间戳
            // Registry: 板面入槽
            if (ci != null && RegistrySyncManager.Instance != null)
                RegistrySyncManager.Instance.UpdateCard(ci, slotID >= 6 ? 0 : 1, CardZone.Board, slotID);
        }
    }

    /// <summary>Force-refresh slot visual after syncing flags from server.</summary>
    public void SyncVisual()
    {
        if (permaBlocked) slotImage.color = Color.black;
        else if (isBlocked) slotImage.color = Color.gray;
        else if (prisonBlocked) slotImage.color = new Color(0.6f, 0.2f, 0.8f);
        else if (hasPlague) slotImage.color = Color.green;
        else if (deepSeaMarked) slotImage.color = Color.blue;
        else slotImage.color = normalColor;
    }

    public bool HasEnemyTarget()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int id = 0; id <= 5; id++)
        {
            BoardSlot slot = bm?.GetSlot(id);
            if (slot != null && !slot.isBlocked && slot.hasCard) return true;
        }
        return false;
    }

    public bool HasAllyTargetExceptSelf()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int id = 6; id <= 11; id++)
        {
            BoardSlot slot = bm?.GetSlot(id);
            if (slot != null && !slot.isBlocked && slot.hasCard && slot != this) return true;
        }
        return false;
    }

    public static void CheckAndHandleDeaths()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        // ── 同时窗口：退场前快照伤害来源 → 存入全局待反击队列 ──
        pendingRevenges.Clear();
        for (int i = 0; i < 12; i++)
        {
            var s = bm.GetSlot(i);
            if (s?.currentCard3D == null) continue;
            var ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || ci.currentHealth > 0) continue;
            if (ci._enterEffectRunning) continue; // 进场中，不参与死亡/反击
            if (!ci.hasRevenge || string.IsNullOrEmpty(ci.revengeEffect)) continue;

            var sourceIDs = new List<string>();
            var marker = s.currentCard3D.GetComponent<DamageSourceMarker>();
            if (marker != null)
            {
                sourceIDs = marker.GetMinionDamageSources()
                    .FindAll(g => g != null && g.GetComponent<Card3DInstance>()?.cardInstance != null)
                    .ConvertAll(g => g.GetComponent<Card3DInstance>().cardInstance.instanceID);
            }
            pendingRevenges.Add((s.slotID, ci.revengeEffect, sourceIDs));
            ci.revengeSnapshotIDs = sourceIDs;
        }

        // ── Step 2c: DeathCheckAction 替代同步 do-while ─────────────────
        ActionQueueManager.Enqueue(new DeathCheckAction(
            "CheckAndHandleDeaths",
            scanDeaths: () =>
            {
                var list = new List<DeathInfo>();
                BoardManager bmScan = FindObjectOfType<BoardManager>();
                if (bmScan == null) return list;
                for (int i = 0; i < 12; i++)
                {
                    BoardSlot s = bmScan.GetSlot(i);
                    if (s?.currentCard3D == null) continue;
                    Card3DInstance c3d = s.currentCard3D.GetComponent<Card3DInstance>();
                    var sc = c3d?.cardInstance;
                    if (sc != null && sc.currentHealth <= 0)
                    {
                        // 进场效果执行中的卡跳过死亡扫描——等 CleanupAfterPlacement 后再判定
                        if (sc._enterEffectRunning) continue;
                        list.Add(new DeathInfo
                        {
                            slotID = s.slotID,
                            templateID = sc.templateID,
                            cardObject = s.currentCard3D,
                            cardInstance = sc,
                            isActiveExit = sc.isActiveExit,
                        });
                    }
                }
                return list;
            },
            handleDeath: (death) =>
            {
                BoardManager bmHandle = FindObjectOfType<BoardManager>();
                if (bmHandle == null) return;
                BoardSlot s = bmHandle.GetSlot(death.slotID);
                if (s != null && s.currentCard3D == death.cardObject)
                    s.HandleDeath(death.cardObject);
            },
            onAllDeathsResolved: () =>
            {
                HandManager hm = FindObjectOfType<HandManager>();
                BoardManager bmFinal = FindObjectOfType<BoardManager>();
                if (hm != null && bmFinal != null)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        BoardSlot s = bmFinal.GetSlot(i);
                        if (s?.currentCard3D == null) continue;
                        CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isXValue)
                            hm.UpdateXValues(ci);
                    }
                }
                BoardSyncManager.MarkDirty();
                // 纯客户端：死亡链全部处理完毕后统一同步板面（替代 DeathPipeline step 11 的逐个同步）
                if (NetworkClient.isConnected && !NetworkServer.active)
                    TurnManager.SyncMyBoardToOpponent();
            }));
    }

    public void HandleDeath(GameObject dyingCard)
    {
        if (dyingCard == null) return;
        Card3DInstance c3d = dyingCard.GetComponent<Card3DInstance>();
        if (c3d == null || c3d.cardInstance == null) return;
        // Registry: 板面退场 → 墓地
        RegistrySyncManager.Instance?.UpdateCard(c3d.cardInstance, slotID >= 6 ? 0 : 1, CardZone.Graveyard, slotID);
        lastHandleDeathTime = Time.time;
        c3d.cardInstance.isDead = true;
        c3d.cardInstance.deathGeneration = c3d.cardInstance.placementGeneration;
        c3d.cardInstance.hasLifePriestBlessing = false;
        c3d.cardInstance.lifePriestBlessingSource = null;
        string templateID = c3d.cardInstance.templateID;
        bool isActiveExit = c3d.cardInstance.isActiveExit;  
                // 清理重定向标记
        GlobalDeathEventHandler.Trigger(c3d.cardInstance, slotID, c3d.cardInstance.damageSourceInstanceIDs, isActiveExit);
                // 清理重定向标记
        if (c3d.cardInstance != null)
        {
            foreach (string sourceID in c3d.cardInstance.damageSourceInstanceIDs)
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                bool fromGravekeeper = false;
                BoardManager.GetSideRange(slotID, out int gkSideStart, out int gkSideEnd);
                for (int i = gkSideStart; i <= gkSideEnd; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s?.currentCard3D == null) continue;
                    CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.instanceID == sourceID && ci.templateID == "01330")
                    {
                        fromGravekeeper = true;
                        break;
                    }
                }
                if (fromGravekeeper)
                {
                    c3d.cardInstance.hasOnDeath = false;
                    c3d.cardInstance.hasActiveExit = false;
                    break;
                }
            }
        }
                // 清理重定向标记
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsTraitBlocked(c3d.cardInstance, "退场"))
        {
            c3d.cardInstance.hasOnDeath = false;
            c3d.cardInstance.hasActiveExit = false;
        }
                // 清理重定向标记
        Debug.Log($"未弃之人检测: templateID={templateID}, hasOnDeath={c3d.cardInstance.hasOnDeath}, hasActiveExit={c3d.cardInstance.hasActiveExit}, isActiveExit={isActiveExit}");
        if (c3d.cardInstance != null)
        {
            BoardManager bm = FindObjectOfType<BoardManager>();
            foreach (GameObject obj in bm.attachedModels)
            {
                Card3DInstance c3dAtt = obj?.GetComponent<Card3DInstance>();
                if (c3dAtt?.cardInstance != null && c3dAtt.cardInstance.templateID == "01131"
                    && c3dAtt.cardInstance.hostSlotID == slotID)
                {
                    bool canConvert = c3d.cardInstance.hasActiveExit;
                    if (canConvert && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(c3dAtt.cardInstance)))
                    {
                        c3d.cardInstance.isActiveExit = true;
                        c3d.cardInstance.hasOnDeath = false;
                        isActiveExit = true;
                        Debug.Log("未弃之人 执行替换");
                    }
                    break;
                }
            }
        }

        // ── 退场效果分发 ──────────────────────────────────────────────
        bool shouldReturn03504 = false;
        CardData template03504 = null;
        bool shouldReturn01117 = false;
        CardData template01117 = null;
        bool shouldReturn03009 = false;
        CardData template03009 = null;

        var exitCtx = EffectContext.ForExit(c3d.cardInstance, this, isActiveExit);
        Trigger exitTrigger = isActiveExit ? Trigger.ActiveExit : Trigger.Exit;
        EffectDispatcher.Dispatch(exitTrigger, exitCtx);

        // ── 动态赋予的死亡特性（01117/苦难给予者赋予的"退场：减一能量"等）──
        if (c3d.cardInstance.templateID != "01117" &&
            c3d.cardInstance.grantedTraitTexts != null &&
            c3d.cardInstance.grantedTraitTexts.Count > 0)
        {
            ProcessGrantedDeathTraits(c3d.cardInstance, slotID);
        }

        shouldReturn03504 = exitCtx.shouldReturn03504;
        template03504 = exitCtx.template03504;
        shouldReturn01117 = exitCtx.shouldReturn01117;
        template01117 = exitCtx.template01117;
        shouldReturn03009 = exitCtx.shouldReturn03009;
        template03009 = exitCtx.template03009;

        // ── 通用死亡后处理管线（Step 2a 提取） ─────────────────────────
        DeathPipeline.ExecuteCommon(new DeathPipelineParams
        {
            dyingCard = dyingCard,
            c3d = c3d,
            slot = this,
            shouldReturn03504 = shouldReturn03504,
            template03504 = template03504,
            shouldReturn01117 = shouldReturn01117,
            template01117 = template01117,
            shouldReturn03009 = shouldReturn03009,
            template03009 = template03009,
        });
    }

    /// <summary>处理动态赋予的死亡特性（苦难给予者等）。HandleDeath 中调用。</summary>
    static void ProcessGrantedDeathTraits(CardInstance ci, int slotID)
    {
        if (ci.grantedTraitTexts == null || ci.grantedTraitTexts.Count == 0) return;
        NetworkPlayer traitOwner = BoardManager.GetOwnerPlayer(slotID);

        foreach (string trait in ci.grantedTraitTexts)
        {
            // 只处理退场（死亡）相关的动态赋予特性；01511 复制的进场/抛置等非死亡特性
            // 在各自的触发时机处理（进场→MindScholarEnterEffect，抛置→TriggerScholarDiscardFromHover）
            if (!trait.Contains("退场")) continue;
            switch (trait)
            {
                case "退场：减一能量":
                    if (traitOwner != null) { traitOwner.currentEnergy -= 1; traitOwner.UpdateUI(); }
                    break;
                case "退场：己方全体受到一伤害":
                    BoardManager bmG = FindObjectOfType<BoardManager>();
                    if (bmG != null)
                    {
                        BoardManager.GetSideRange(slotID, out int gs, out int ge);
                        for (int i = gs; i <= ge; i++)
                        {
                            var si = bmG.GetSlot(i);
                            if (si?.currentCard3D != null)
                            {
                                var tci = si.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                if (tci != null)
                                {
                                    tci.currentHealth -= 1;
                                    si.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    DamagePipeline.ShowFloaterAt(tci, 1, FloaterType.Damage);
                                }
                            }
                        }
                    }
                    CheckAndHandleDeaths();
                    break;
                case "退场：己方玩家扣一血":
                    traitOwner?.TakeDamage(1);
                    break;
                default:
                    Debug.LogWarning($"[BoardSlot] ProcessGrantedDeathTraits 未处理的 trait: {trait}");
                    break;
            }
        }
    }

    static int FindSlotID(CardInstance ci)
    {
        var bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return i;
        return -1;
    }

    public static void TriggerDeathEffect(CardInstance ci, bool isActive)
    {
        if (ci == null) return;
        NetworkPlayer dp = BoardManager.GetOwnerPlayer(FindSlotID(ci));
        string id = ci.templateID;
        // dp is already set above: NetworkPlayer dp = BoardManager.GetOwnerPlayer(FindSlotID(ci));
        if (isActive)
        {
            switch (id)
            {
                case "01106": dp?.AddEnergy(3); break;
                case "01107":
                    dp?.AddEnergy(2);
                    {
                        bool hasAlly = false;
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        BoardManager.GetSideRangeOf(ci, out int fcSideStart, out int fcSideEnd);
                        for (int i = fcSideStart; i <= fcSideEnd; i++)
                        {
                            if (bm?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
                        }
                        if (hasAlly)
                        {
                            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
                            {
                                if (target?.currentCard3D != null)
                                {
                                    CardInstance ti = target.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (ti != null)
                                    {
                                        ti.GrantShield(true, false, false);
                                        target.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    }
                                }
                            });
                        }
                    }
                    break;
                default:
                    Debug.LogWarning($"[BoardSlot] TriggerDeathEffect(active) 未处理的 templateID: {id}");
                    break;
            }
        }
        else
        {
            switch (id)
            {
                case "01106": dp?.AddEnergy(1); break;
                case "03513":
                    Do03513AOE(ci);
                    break;
                default:
                    Debug.LogWarning($"[BoardSlot] TriggerDeathEffect(inactive) 未处理的 templateID: {id}");
                    break;
            }
        }
    }

    /// <summary>03513 断罪者死亡时：对对方全部随从造成 1 伤害。从有 this 和无 this 的两处调用点提取。</summary>
    static void Do03513AOE(BoardSlot mySlot)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        int enemyStart = mySlot.slotID >= 6 ? 0 : 6;
        for (int j = enemyStart; j < enemyStart + 6; j++)
        {
            BoardSlot es = bm.GetSlot(j);
            if (es?.currentCard3D != null)
            {
                Card3DInstance ei = es.currentCard3D.GetComponent<Card3DInstance>();
                if (ei?.cardInstance != null)
                {
                    BattleManager.Instance.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                    ei.UpdateValues();
                }
            }
        }
    }

    static void Do03513AOE(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            { Do03513AOE(bm.GetSlot(i)); return; }
        }
    }

    public void ApplySufferingGiverEffect(CardInstance giver, CardInstance target, string chosenTrait)
    {
        if (chosenTrait == null || giver == null || target == null) return;
        giver.giveableDeathTraits.Remove(chosenTrait);
        giver.RemoveGrantedTrait(chosenTrait);
        target.GrantTrait(chosenTrait);
        RefreshCardDisplay(target);
        // 如果详情面板正打开着，即时刷新显示
        RefreshTest1Panel(giver);
        RefreshTest1Panel(target);
        // 同步赋予的特性文本到对方视角（target 在对方半场，需要上报板面变化）
        TurnManager.SyncMyBoardToOpponent();
    }

    static void RefreshTest1Panel(CardInstance ci)
    {
        if (ci == null) return;
        var panel = Test1Panel.Instance;
        if (panel == null || !panel.panelRoot.activeSelf) return;
        panel.Show(ci);
    }
    public static void ClearAllHighlights()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot != null)
            {
                slot.transform.localScale = slot.originalScale;
                // 使用 SyncVisual 的优先级链复原颜色，而非粗暴写死 normalColor——
                // 否则 deepSeaMarked/prisonBlocked/hasPlague 的视觉状态被 EndSelection 抹除
                slot.SyncVisual();
            }
        }
    }
    void RefreshCardDisplay(CardInstance ci)
    {
        if (ci == null) return;
        BoardManager bm = FindObjectOfType<BoardManager>();
        // 1) 3D 模型刷新
        if (bm != null)
            for (int i = 0; i < 12; i++)
            {
                Card3DInstance c3d = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance == ci) { c3d.UpdateValues(); break; }
            }
        // 2) 2D 手牌刷新（如果同 instanceID 在手牌中）
        if (NetworkPlayer.Local == null) return;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            var inst = card.GetComponent<CardInstance>();
            if (inst != null && inst.instanceID == ci.instanceID)
            {
                card.GetComponent<CardDisplay2D>()?.RefreshWithInstance(inst);
                break;
            }
        }
    }

    void CleanupAfterSelection() { }

    public IEnumerator ReformerEnterEffect(CardInstance giver)
    {
        yield return null;
      
        SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        List<GameObject> handSummons = new List<GameObject>();

        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData t = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (t == null) continue;
            if (t.cardType == CardType.Spell) { card.SetActive(false); spellCards.Add(card); }
            else if (t.cardType == CardType.Summon)
            {
                handSummons.Add(card);
                CardClickHandler handler = card.GetComponent<CardClickHandler>();
                if (handler == null) handler = card.AddComponent<CardClickHandler>();
                handler.onClick = () =>
                {
                    SelectionManager.Instance.ForceEndAll();
                    CleanupReformerUI(spellCards, handSummons);
                    ApplyReformerEffect(card);
                    CleanupAfterPlacement();
                   
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.ForceEndAll();
                CleanupReformerUI(spellCards, handSummons);
                ApplyReformerEffect(targetSlot.currentCard3D);
                CleanupAfterPlacement();
                
            }
        };
    }

    void CleanupReformerUI(List<GameObject> hiddenSpells, List<GameObject> handSummons)
    {
        foreach (GameObject card in hiddenSpells) { if (card != null) card.SetActive(true); }
        foreach (GameObject card in handSummons)
        {
            if (card == null) continue;
            CardClickHandler handler = card.GetComponent<CardClickHandler>();
            if (handler != null) Destroy(handler);
        }
    }

    void ApplyReformerEffect(GameObject target)
    {
        if (target == null) return;
        CardInstance targetCI = target.GetComponent<CardInstance>();
        if (targetCI == null)
        {
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            if (c3d != null) targetCI = c3d.cardInstance;
        }
        if (targetCI != null)
        {
            if (!targetCI.prefixes.Contains("灵能"))
            {
                if (string.IsNullOrEmpty(targetCI.prefixes) || targetCI.prefixes == "无")
                    targetCI.prefixes = "灵能";
                else targetCI.prefixes += " 灵能";
            }
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();
            // 前缀修改同步到对方
            TurnManager.SyncMyBoardToOpponent();
        }
    }

    private void ExecuteReplace(BoardSlot targetSlot)
    {
        GameObject oldCard = targetSlot.currentCard3D;
        HandManager hm = FindObjectOfType<HandManager>();
        hm.PlaceCardToSlot(targetSlot, cardToPlace);
        CardInstance newInst = targetSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        CardData newTemplate = CardDatabase.Instance?.GetTemplate(newInst?.templateID);
        if (newTemplate != null && newTemplate.hasOnEnter && newInst != null)
        {
            targetSlot.StartCoroutine(targetSlot.StartOnEnterEffect(newTemplate, newInst));
        }
        if (oldCard != null)
        {
            Card3DInstance oldInst = oldCard.GetComponent<Card3DInstance>();
            if (oldInst?.cardInstance != null)
            {
                oldInst.cardInstance.isActiveExit = false;
                oldInst.cardInstance.hasRevenge = false;

                GlobalDeathEventHandler.Trigger(oldInst.cardInstance, targetSlot.slotID,
                    oldInst.cardInstance.damageSourceInstanceIDs, false);
            }

            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
                for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
                {
                    GameObject obj = bm.attachedModels[i];
                    if (obj == null) continue;
                    Card3DInstance c3d = obj.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance != null && c3d.cardInstance.hostSlotID == targetSlot.slotID)
                    { bm.attachedModels.RemoveAt(i); BoardManager.RecordAndRemoveAttach(obj); }
                }

            GraveEntry entry = new GraveEntry
            {
                templateID = oldInst.cardInstance.templateID,
                instanceID = oldInst.cardInstance.instanceID,
                deathPhase = TurnManager.Instance.phaseCount,
                handledReturnToHand = false
            };
            GraveyardManager.Instance.AddToGraveyard(entry);
            Destroy(oldCard);
        }

        // 同步赋予特性到服务器（OnPointerClick 替换路径跳过正常同步块，需在此补上）
        var placedCI2 = targetSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (placedCI2?.grantedTraitTexts?.Count > 0 && NetworkClient.isConnected)
            NetworkPlayer.Local?.CmdSyncGrantedTraits(targetSlot.slotID, string.Join(";;", placedCI2.grantedTraitTexts));

        cardToPlace = null;
        CleanupAfterPlacement();
    }

    private void ExecuteAttach(BoardSlot hostSlot)
    {
        HandManager hm = FindObjectOfType<HandManager>();
        hm.PlaceCardToSlot(null, cardToPlace);
        CleanupAfterPlacement();
    }

    public GameObject currentCard3D
    {
        get => _currentCard;
        set
        {
            // 同一个 GameObject 重复设入同一槽位 → 跳过，避免 slotTempAttackBoost / deepSeaAttackDebuff 重复加减
            if (_currentCard == value) return;

            if (_currentCard != null)
            {
                Card3DInstance oc = _currentCard.GetComponent<Card3DInstance>();
                if (oc?.cardInstance != null)
                {
                    if (!oc.cardInstance.isXValue)
                        oc.cardInstance.currentAttack -= slotTempAttackBoost;
                    oc.cardInstance.currentAttack += deepSeaAttackDebuff;
                    oc.cardInstance.currentTier -= spotlightTierBoost;
                    oc.UpdateValues();
                }
                if (hasPlague)
                {
                    hasPlague = false;
                    plagueRoundCount = 0;
                }
            }
            _currentCard = value;
            if (_currentCard != null)
            {
                Card3DInstance nc = _currentCard.GetComponent<Card3DInstance>();
                if (nc?.cardInstance != null)
                {
                    if (!nc.cardInstance.isXValue)
                        nc.cardInstance.currentAttack += slotTempAttackBoost;
                    nc.cardInstance.currentAttack = Mathf.Max(0, nc.cardInstance.currentAttack - deepSeaAttackDebuff);
                    nc.cardInstance.currentTier += spotlightTierBoost;
                    nc.UpdateValues();
                }
            }
        }
    }
    public static void CleanupAttachSelect()
    {
        isAttachSelectMode = false;
        isReplaceMode = false;
        attachCanBeIndependent = false;
    }
    public static void StartAttachSelect(bool canBeIndependent, Action<BoardSlot> onSelected)
    {
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, onSelected);
        isAttachSelectMode = true;
        attachCanBeIndependent = canBeIndependent;
    }
    public void OnDisasterWalkerDamage(int amount)
    {
        Debug.Log($"灾厄行者触发: 扣血{amount}, slotID={slotID}");
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        for (int i = 0; i < amount; i++)
        {
            owner?.DrawCardWithoutLimit();
        }
    }
    void CopyToGrave(CardInstance dest, CardInstance src)
    {
        dest.templateID = src.templateID;
        dest.instanceID = src.instanceID;
        dest.currentCost = src.currentCost;
        dest.currentAttack = src.currentAttack;
        dest.baseAttack = src.baseAttack;
        dest.currentHealth = src.currentHealth;
        dest.baseHealth = src.baseHealth;
        dest.baseMaxHealth = src.baseMaxHealth;
        dest.currentMaxHealth = src.currentMaxHealth;
        dest.currentTier = src.currentTier;
        dest.baseTier = src.baseTier;
        dest.prefixes = src.prefixes;
        dest.handledReturnToHand = src.handledReturnToHand;
        dest.hasOnDeath = src.hasOnDeath;
        dest.hasActiveExit = src.hasActiveExit;
        dest.hasOnEnter = src.hasOnEnter;
        dest.hasFirstStrike = src.hasFirstStrike;
        dest.hasRevenge = src.hasRevenge;
        dest.hasDiscard = src.hasDiscard;
        dest.canAttach = src.canAttach;
        dest.grantedTraitTexts = src.grantedTraitTexts != null ? new List<string>(src.grantedTraitTexts) : new List<string>();
        dest.giveableDeathTraits = src.giveableDeathTraits != null ? new List<string>(src.giveableDeathTraits) : new List<string>();
    }
    public IEnumerator HeartthrobEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<GameObject> heroCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero)
                heroCards.Add(card);
        }

        if (heroCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero;
        });

        GameObject selectedCard = null;
        bool cardChosen = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; cardChosen = true; };
        }
        yield return new WaitUntil(() => cardChosen);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard == null)
        {
            CleanupAfterPlacement();
            yield break;
        }

        CardInstance selInst = selectedCard.GetComponent<CardInstance>();
        bool isAttachCard = selInst != null && selInst.canAttach;

        if (isAttachCard)
        {
            // Use the standard PlaceCardToSlot flow for attach cards —
            // this handles attach/independent/replace correctly.
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.HideOtherCards(null);    // show all cards; cardToPlace gameobject stays visible
            hm?.SetHandAreaRaycast(false);
            FindObjectOfType<CardDrag>()?.SetButtonsInteractable(false);
            hm.PlaceCardToSlot(null, selectedCard);
            // PlaceCardToSlot starts an async callback flow (StartAttachSelect or direct placement).
            // Wait for it to finish.
            yield return new WaitWhile(() => BoardSlot.isPlacingCard || BoardSlot.isAttachSelectMode);
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        }
        else
        {
            // Non-attach card: standard direct placement via isPlacingCard flag
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            BoardSlot.cardToPlace = selectedCard;
            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
        }

        // Ensure cleanup
        NetworkPlayer.Local.handCards.Remove(selectedCard);
        CleanupAfterPlacement();
    }
    public IEnumerator MartyrDeathEffectCoroutine(CardInstance giver)
    {
        NestingContext.Enter("MartyrDeath");
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        if (owner == null) { NestingContext.Exit(); yield break; }

        if (NetworkServer.active && !owner.isLocalPlayer)
        {
            _martyrRpcDone = false;
            _martyrBuffSlot = -1;
            owner.TargetMartyrDeathEffect(owner.connectionToClient, slotID);
            float t = Time.time;
            while (!_martyrRpcDone && Time.time - t < 30f) yield return null;
            int targetServerSlot = _martyrBuffSlot;
            _martyrRpcDone = false;
            if (targetServerSlot >= 0)
            {
                BoardSlot targetSlot = FindObjectOfType<BoardManager>()?.GetSlot(targetServerSlot);
                if (targetSlot?.currentCard3D != null)
                {
                    CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci != giver)
                    {
                        if (!ci.cannotHealOrGainMaxHP)
                        { ci.currentHealth += 5; ci.currentMaxHealth += 5; }
                        ci.currentAttack += 4;
                        targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
            BoardSyncManager.MarkDirty();
            NestingContext.Exit();
            yield break;
        }

        yield return null;
        // 本地选择路径加 30 秒超时，防止选择永不回调导致死锁
        bool selDone = false;
        float selDeadline = Time.time + 30f;
        BoardManager bmLocal = FindObjectOfType<BoardManager>();
        bool hasAlly = false;
        for (int j = 6; j <= 11; j++)
        {
            if (bmLocal?.GetSlot(j)?.currentCard3D != null) { hasAlly = true; break; }
        }
        if (hasAlly)
        {
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
            {
                if (!selDone && targetSlot?.currentCard3D != null)
                {
                    CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci != giver)
                    {
                        if (!ci.cannotHealOrGainMaxHP)
                        {
                            ci.currentHealth += 5;
                            ci.currentMaxHealth += 5;
                        }
                        ci.currentAttack += 4;
                        targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                        TurnManager.SyncMyBoardToOpponent();
                    }
                }
                selDone = true;
            });
        }
        else { selDone = true; }
        yield return new WaitUntil(() => selDone || Time.time > selDeadline);
        if (!selDone) { SelectionManager.Instance.ForceEndAll(); Debug.LogWarning("[MartyrDeath] 本地选择超时（30s），强制结束"); }
        NestingContext.Exit();
    }
    public IEnumerator RogueDeathEffect(CardInstance giver)
    {
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        if (owner == null) yield break;

        NestingContext.Enter("RogueDeath");
        _roguePhaseBlock = true;

        if (!owner.isLocalPlayer)
        {
            owner.TargetRogueDeathEffect(owner.connectionToClient, slotID);
            float t = Time.time;
            while (!_rogueRpcDone && Time.time - t < 30f) yield return null;
            _rogueRpcDone = false;
            _roguePhaseBlock = false;
            NestingContext.Exit();
            yield break;
        }

        yield return StartCoroutine(RogueDoSummon(owner));
        _roguePhaseBlock = false;
        NestingContext.Exit();
    }

    /// <summary>远端客户端执行：走与本地完全相同的 isPlacingCard+cardToPlace 放置流程。</summary>
    public IEnumerator RogueSummonRemote()
    {
        NestingContext.Enter("RogueDeathRemote");
        yield return StartCoroutine(RogueDoSummon(NetworkPlayer.Local));
        NestingContext.Exit();
        NetworkPlayer.Local?.CmdRogueDone();
    }

    /// <summary>远端客户端：01522 殉难者退场时选择己方目标（在客户端本地执行）。</summary>
    public IEnumerator MartyrRemoteSelect()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        bool hasAlly = false;
        if (bm != null)
        {
            for (int j = 6; j <= 11; j++)
                if (bm.GetSlot(j)?.currentCard3D != null) { hasAlly = true; break; }
        }
        if (!hasAlly) { NetworkPlayer.Local?.CmdMartyrDone(-1); yield break; }

        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            // 客户端视角 6-11（己方）→ 映射为服务端 0-5（远端己方）
            int serverSlot = s != null ? (s.slotID >= 6 ? s.slotID - 6 : s.slotID + 6) : -1;
            NetworkPlayer.Local?.CmdMartyrDone(serverSlot);
            done = true;
        });
        yield return new WaitUntil(() => done);
    }

    IEnumerator RogueDoSummon(NetworkPlayer player)
    {
        player.handCards.RemoveAll(c => c == null);

        List<GameObject> heroCards = new List<GameObject>();
        foreach (GameObject card in player.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero)
                heroCards.Add(card);
        }

        if (heroCards.Count == 0) yield break;

        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Summon && td.summonType == SummonType.Hero;
        });

        GameObject selectedCard = null;
        bool done = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; done = true; };
        }
        yield return new WaitUntil(() => done);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard != null)
        {
            player.handCards.Remove(selectedCard);
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            BoardSlot.cardToPlace = selectedCard;
            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
            // 等进场效果完成（可能重新置 isPlacingCard 触发子召唤如影舞者）
            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            player.handCards.Remove(selectedCard);
        }
    }

    public IEnumerator GreedySnakeCopyProcess(CardInstance giver, CardInstance target)
    {
        List<(string key, string fullText)> traits = new List<(string, string)>();

        // 反击
        if (target.hasFirstStrike)
        {
            string text = GetTraitFullText(target, "反击");
            traits.Add(("反击", text));
        }
                // 清理重定向标记
        if (target.hasOnDeath)
        {
            string text = GetTraitFullText(target, "先手");
            traits.Add(("先手", text));
        }
                // 清理重定向标记
        if (target.hasRevenge)
        {
            string text = GetTraitFullText(target, "先手");
            traits.Add(("先手", text));
        }

        if (traits.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        if (traits.Count == 1)
        {
            ApplyGreedySnakeCopy(giver, target, traits[0].key);
            CleanupAfterPlacement();
            yield break;
        }

        foreach (var (key, fullText) in traits)
        {
            bool chosen = false;
            bool thisDone = false;
            ConfirmPanel.Instance.Show($"是否复制{fullText}？",
                () => { chosen = true; thisDone = true; },
                () => { thisDone = true; }
            );
            yield return new WaitUntil(() => thisDone);

            if (chosen)
            {
                ApplyGreedySnakeCopy(giver, target, key);
                break;
            }
        }

        CleanupAfterPlacement();
    }

    string GetTraitFullText(CardInstance ci, string traitKey)
    {
        // 1. 从赋予的特性中查找
        foreach (string gt in ci.grantedTraitTexts)
        {
            if (gt.Contains(traitKey)) return gt;
        }

        // 2. 反击从 revengeEffect
        if (traitKey == "反击" && !string.IsNullOrEmpty(ci.revengeEffect))
            return $"反击：{ci.revengeEffect}";

        // 3. 从模板特性文本中查找对应行
        CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
        if (td != null && !string.IsNullOrEmpty(td.traits))
        {
            string[] lines = td.traits.Split('\n');
            foreach (string line in lines)
            {
                if (line.Contains(traitKey)) return line.Trim();
            }
        }

        return traitKey;
    }

    void ApplyGreedySnakeCopy(CardInstance giver, CardInstance target, string key)
    {
        string fullText = GetTraitFullText(target, key);
        giver.GrantTrait(fullText);
        giver.greedySnakeEnterCount++;
        Debug.Log($"贪欲之蛇复制了{key}，进场次数={giver.greedySnakeEnterCount}");
    }
  
    public IEnumerator RemnantEnterEffect(CardInstance giver)
    {
        List<CardInstance> allyMinions = new List<CardInstance>();
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardManager.GetSideRangeOf(giver, out int rmSideStart, out int rmSideEnd);
        for (int i = rmSideStart; i <= rmSideEnd; i++)
        {
            BoardSlot slot = bm?.GetSlot(i);
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci != giver && !ci.isAttached)
                allyMinions.Add(ci);
        }

        if (allyMinions.Count < 2)
        {
            Debug.Log("残篇：己方召唤物不足2个");
            CleanupAfterPlacement();
            yield break;
        }

                // 清理重定向标记
        CardInstance firstTarget = null;
        bool firstDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (slot) =>
        {
            if (slot?.currentCard3D != null)
            {
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci != giver && !ci.isAttached)
                {
                    firstTarget = ci;
                    firstDone = true;
                }
            }
        });
        yield return new WaitUntil(() => firstDone);
        if (firstTarget == null) { CleanupAfterPlacement(); yield break; }

                // 清理重定向标记
        CardInstance secondTarget = null;
        bool secondDone = false;
        BoardSlot.extraTargetFilter = (slot) =>
        {
            if (slot?.currentCard3D == null) return false;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            return ci != null && ci != giver && ci != firstTarget && !ci.isAttached;
        };
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (slot) =>
        {
            if (slot?.currentCard3D != null)
            {
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci != giver && ci != firstTarget && !ci.isAttached)
                {
                    secondTarget = ci;
                    secondDone = true;
                }
            }
        });
        yield return new WaitUntil(() => secondDone);
        BoardSlot.extraTargetFilter = null;
        if (secondTarget == null) { CleanupAfterPlacement(); yield break; }

                // 清理重定向标记
        GenericChoicePanel.Instance.Show("选择一个返回手牌",
      new List<string>
      {
        CardDatabase.Instance?.GetTemplate(firstTarget.templateID)?.cardName ?? "召唤物1",
        CardDatabase.Instance?.GetTemplate(secondTarget.templateID)?.cardName ?? "召唤物2"
      },
      (index) =>
      {
          HandManager hm = FindObjectOfType<HandManager>();
          hm.RemnantFinalize(firstTarget, secondTarget, index == 0);
      });
    }
    public IEnumerator PirateEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        int mySlot = -1;
        for (int i = 0; i < 12; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == giver)
            { mySlot = i; break; }
        }

        if (mySlot < 0) { CleanupAfterPlacement(); yield break; }

        int rowStart = mySlot < 9 ? 0 : 3;

        // 检查目标排是否有至少2个可操作的格子
        int validCount = 0;
        for (int j = rowStart; j < rowStart + 3; j++)
        {
            BoardSlot s = bm?.GetSlot(j);
            if (s != null && !s.isBlocked) validCount++;
        }
        if (validCount < 2) { CleanupAfterPlacement(); yield break; }

        BoardSlot.isStrengtheningSlot = true;
        BoardSlot.extraTargetFilter = (slot) =>
        {
            return slot != null && slot.slotID >= rowStart && slot.slotID < rowStart + 3;
        };
        SelectionManager.Instance.BeginSelection(TargetType.EnemyAnyRow, null);

        BoardSlot firstSlot = null;
        bool confirmed = false;
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);

        System.Text.StringBuilder swapLog = null;
        if (NetworkClient.isConnected)
            swapLog = new System.Text.StringBuilder();

        BoardSlot.onTargetSelected = (slot) =>
        {
            if (slot == null || slot.isBlocked || slot.slotID < rowStart || slot.slotID >= rowStart + 3) return;
            if (firstSlot == null)
            {
                firstSlot = slot;
            }
            else if (slot != firstSlot)
            {
                BoardSlot secondSlot = slot;
                int idA = firstSlot.slotID;
                int idB = secondSlot.slotID;
                GameObject c1 = firstSlot.currentCard3D;
                GameObject c2 = secondSlot.currentCard3D;
                Vector3 p1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(firstSlot.slotID);
                Vector3 p2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(secondSlot.slotID);

                firstSlot.SetCard(null);
                secondSlot.SetCard(null);
                if (c2 != null)
                {
                    if (!firstSlot.CanPlaceCard(c2.GetComponent<Card3DInstance>()?.cardInstance)) { firstSlot = null; return; }
                    c2.transform.position = p1;
                    firstSlot.SetCard(c2);
                }
                if (c1 != null)
                {
                    if (!secondSlot.CanPlaceCard(c1.GetComponent<Card3DInstance>()?.cardInstance)) { firstSlot = null; return; }
                    c1.transform.position = p2;
                    secondSlot.SetCard(c1);
                }

                // Update attached model hostSlotIDs and reposition
                BoardManager bmSwap = FindObjectOfType<BoardManager>();
                if (bmSwap != null)
                {
                    foreach (GameObject obj in bmSwap.attachedModels)
                    {
                        CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isAttached)
                        {
                            if (ci.hostSlotID == idA) ci.hostSlotID = idB;
                            else if (ci.hostSlotID == idB) ci.hostSlotID = idA;
                        }
                    }
                    BoardManager.SyncAttachedModels(firstSlot);
                    BoardManager.SyncAttachedModels(secondSlot);
                }
                firstSlot = null;

                // Record swap pair for network sync on confirm
                if (swapLog != null)
                {
                    if (swapLog.Length > 0) swapLog.Append(';');
                    swapLog.Append(idA).Append(',').Append(idB);
                }
            }
        };

        yield return new WaitUntil(() => confirmed);
        SelectionManager.Instance.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        BoardSlot.extraTargetFilter = null;
        ConfirmSelectionButton.Instance.Hide();

        // Sync all pirate swaps to server/opponent
        if (swapLog != null && swapLog.Length > 0)
        {
            foreach (string pair in swapLog.ToString().Split(';'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                string[] ids = pair.Split(',');
                if (ids.Length == 2 && int.TryParse(ids[0], out int a) && int.TryParse(ids[1], out int b))
                    NetworkPlayer.Local?.CmdSwapCards(a, b);
            }
        }
        TurnManager.SyncMyBoardToOpponent();

        CleanupAfterPlacement();
    }
    public IEnumerator PrisonEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        bool hasMyEmpty = false;
        BoardManager.GetSideRangeOf(giver, out int prSideStart, out int prSideEnd);
        for (int i = prSideStart; i <= prSideEnd; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked) { hasMyEmpty = true; break; }
        }
        if (!hasMyEmpty) { CleanupAfterPlacement(); yield break; }

        BoardSlot myPrison = null;
        bool myDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked && s.slotID >= prSideStart && s.slotID <= prSideEnd)
            { myPrison = s; myDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => myDone);
        BoardSlot.isStrengtheningSlot = false;
        if (myPrison == null) { CleanupAfterPlacement(); yield break; }

        bool hasEnemyEmpty = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked) { hasEnemyEmpty = true; break; }
        }
        if (!hasEnemyEmpty) { CleanupAfterPlacement(); yield break; }

        BoardSlot enemyPrison = null;
        bool enemyDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked && s.slotID <= 5)
            { enemyPrison = s; enemyDone = true; }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => enemyDone);
        BoardSlot.isStrengtheningSlot = false;
        if (enemyPrison == null) { CleanupAfterPlacement(); yield break; }

        myPrison.prisonBlocked = true;
        myPrison.prisonAllowYuan = true;
        myPrison.slotImage.color = new Color(0.6f, 0.2f, 0.8f);

        enemyPrison.prisonBlocked = true;
        enemyPrison.prisonAllowYuan = false;
        enemyPrison.slotImage.color = new Color(0.6f, 0.2f, 0.8f);

        giver.prisonMySlot = myPrison.slotID;
        giver.prisonEnemySlot = enemyPrison.slotID;

        // Sync slot prison flags to opponent — must reach server & remote
        TurnManager.SyncMyBoardToOpponent();

        // 远端放置时，服务器端的 CardInstance 副本不会运行 PrisonEnterEffect，
        // 需要显式告知服务器 prisonMySlot/prisonEnemySlot，退场时才能精确解锁
        if (!NetworkServer.active)
            NetworkPlayer.Local.CmdSetPrisonSlots(giver.instanceID, myPrison.slotID, enemyPrison.slotID);

        CleanupAfterPlacement();
    }
    public bool CanPlaceCard(CardInstance ci)
    {
        if (isBlocked) return false;
        if (!prisonBlocked) return true;
        if (slotID >= 6 && prisonAllowYuan && ci != null && ci.prefixes.Contains("渊"))
            return true;
        return false;
    }
    public IEnumerator EmperorEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        HandManager hm = FindObjectOfType<HandManager>();
        hm?.ShowAllCards();

        string layerId = SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Spell)
            {
                card.SetActive(false);
                spellCards.Add(card);
            }
        }

        List<GameObject> handSummons = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Summon)
            {
                handSummons.Add(card);
                CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
                h.onClick = () =>
                {
                    SelectionManager.Instance.ForceEndAll();
                    CleanupEmperorUI(spellCards, handSummons);
                    ApplyEmperorPrefix(card);
                    CleanupAfterPlacement();
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.ForceEndAll();
                CleanupEmperorUI(spellCards, handSummons);
                ApplyEmperorPrefix(targetSlot.currentCard3D);
                CleanupAfterPlacement();
            }
        };
    }

    void CleanupEmperorUI(List<GameObject> hiddenSpells, List<GameObject> handSummons)
    {
        foreach (GameObject card in hiddenSpells) if (card != null) card.SetActive(true);
        foreach (GameObject card in handSummons)
        {
            if (card == null) continue;
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
    }

    void ApplyEmperorPrefix(GameObject target)
    {
        if (target == null) return;
        CardInstance ci = target.GetComponent<CardInstance>();
        if (ci == null) { Card3DInstance c3d = target.GetComponent<Card3DInstance>(); if (c3d != null) ci = c3d.cardInstance; }
        if (ci != null && !ci.prefixes.Contains("渊"))
        {
            if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                ci.prefixes = "渊";
            else ci.prefixes += " 渊";
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();
            // 前缀修改同步到对方
            TurnManager.SyncMyBoardToOpponent();
        }
    }
    public void SetHighlightColor(Color color)
    {
        slotImage.color = color;
    }

    public Color GetNormalColor()
    {
        return normalColor;
    }
    public IEnumerator RiddlerDeathEffect(CardInstance giver)
    {
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

                // 清理重定向标记
        List<GameObject> counterCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Spell && (td.spellType & SpellType.Counter) != 0)
                counterCards.Add(card);
        }

        if (counterCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            yield break;
        }

                // 清理重定向标记
        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Spell && (td.spellType & SpellType.Counter) != 0;
        });

        GameObject selectedCard = null;
        bool done = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; done = true; };
        }
        yield return new WaitUntil(() => done);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard != null)
        {
                // 清理重定向标记
            CounterManager.Instance?.PlayCounter(selectedCard, true);
                // 清理重定向标记
            var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
            if (counter != null) counter.noCostOnTrigger = true;
            NetworkPlayer.Local.handCards.Remove(selectedCard);
            Destroy(selectedCard);
            HandManager hm = FindObjectOfType<HandManager>();
            hm?.RefreshLayout(true);
        }
    }
    public IEnumerator BlockerEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        bool hasEnemyEmpty = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked) { hasEnemyEmpty = true; break; }
        }
        if (!hasEnemyEmpty) { CleanupAfterPlacement(); yield break; }

        BoardSlot target = null;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && !s.hasCard && !s.isBlocked && !s.prisonBlocked && s.slotID <= 5)
            {
                target = s;
                done = true;
            }
        });
        BoardSlot.isStrengtheningSlot = true;
        yield return new WaitUntil(() => done);
        BoardSlot.isStrengtheningSlot = false;

        if (target != null)
        {
            target.isBlocked = true;
            target.permaBlocked = true;
            target.SyncVisual();
            Debug.Log($"封锁者永久封锁槽位{target.slotID}");
            // Sync slot block to opponent
            if (NetworkClient.isConnected)
            {
                // 01331 模式：远程客户端需显式告知服务器锁定敌方格子——CmdReportAllSlots 不覆盖 enemy slot flags
                if (!NetworkServer.active)
                    NetworkPlayer.Local?.CmdBlockSlot(target.slotID);
                else
                    BoardSyncManager.MarkDirty();
            }
        }

        CleanupAfterPlacement();
    }
    public IEnumerator InkEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        List<CardInstance> allies = new List<CardInstance>();
        BoardManager.GetSideRangeOf(giver, out int inkSideStart, out int inkSideEnd);
        for (int i = inkSideStart; i <= inkSideEnd; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci != giver && !ci.isAttached && ci.templateID != "01523")
                    allies.Add(ci);
            }
        }

        if (allies.Count == 0) { CleanupAfterPlacement(); yield break; }

        // 使己方其它召唤物退场并回到手牌
        foreach (CardInstance ci in allies)
        {
            ci.isActiveExit = true;
            ci.handledReturnToHand = false;
            BoardSlot slot = FindSlotOf(ci);
            if (slot != null)
            {
                slot.HandleDeath(slot.currentCard3D);
                // HandleDeath 可能已通过退场特性处理回手；若未处理则手动回手
                if (!ci.handledReturnToHand)
                {
                    CardData tt = CardDatabase.Instance?.GetTemplate(ci.templateID);
                    if (tt != null)
                        NetworkPlayer.Local.AddCardToHandFromInstance(tt, ci);
                }
                yield return null;
                // 防止退场效果残留的选择状态阻塞
                if (SelectionManager.Instance.IsSelecting)
                    SelectionManager.Instance.ForceEndAll();
            }
        }

        // 每退场一个 +1+1
        int count = allies.Count;
        giver.currentHealth += count;
        giver.currentMaxHealth += count;
        giver.currentAttack += count;

        Card3DInstance giver3D = FindGiver3D(giver);
        giver3D?.UpdateValues();

        // 同步增强后的属性到服务器
        BoardSlot giverSlot = FindSlotOf(giver);
        if (giverSlot != null && NetworkClient.isConnected)
            NetworkPlayer.Local?.CmdUpdateCardStats(giverSlot.slotID,
                giver.currentAttack, giver.currentHealth, giver.currentMaxHealth);

        CleanupAfterPlacement();
    }
    BoardSlot FindSlotOf(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return s;
        }
        return null;
    }

    public Card3DInstance FindGiver3D(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return s.currentCard3D?.GetComponent<Card3DInstance>();
        }
        return null;
    }

    /// <summary>猩红圣徒(01533)：进场为己方手牌或场上一召唤物附加血歌前缀。</summary>
    public IEnumerator ScarletSaintEnterEffect(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData t = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (t?.cardType == CardType.Spell) { card.SetActive(false); spellCards.Add(card); }
        }

        bool done = false;

        // Click handler for hand summon cards
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData t = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (t?.cardType == CardType.Spell) continue;
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () =>
            {
                if (!ci.prefixes.Contains("血歌"))
                {
                    ci.prefixes = string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无"
                        ? "血歌" : ci.prefixes + " 血歌";
                    CardDisplay2D d2d = card.GetComponent<CardDisplay2D>();
                    d2d?.Refresh();
                    TurnManager.SyncMyBoardToOpponent();
                }
                SelectionManager.Instance.ForceEndAll();
                foreach (var sc in spellCards) sc?.SetActive(true);
                done = true;
            };
        }

        // Click handler for board ally slots
        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                Card3DInstance c3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance != null && !c3d.cardInstance.prefixes.Contains("血歌"))
                {
                    c3d.cardInstance.prefixes = string.IsNullOrEmpty(c3d.cardInstance.prefixes) || c3d.cardInstance.prefixes == "无"
                        ? "血歌" : c3d.cardInstance.prefixes + " 血歌";
                    c3d.UpdateValues();
                    TurnManager.SyncMyBoardToOpponent();
                }
            }
            SelectionManager.Instance.ForceEndAll();
            foreach (var sc in spellCards) sc?.SetActive(true);
            done = true;
        };

        yield return new WaitUntil(() => done);

        // Cleanup click handlers
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            var h = card?.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        foreach (var sc in spellCards) sc?.SetActive(true);

        CleanupAfterPlacement();
    }

    public IEnumerator ApprenticeMageEnterEffect(CardInstance giver)
    {
        NestingContext.Enter("Spell_01329");
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Spell)
                spellCards.Add(card);
        }

        if (spellCards.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        ConfirmQueueManager.EnterSelectionMode();
        var validCards = ConfirmQueueManager.FilterHandCards(ci =>
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            return td != null && td.cardType == CardType.Spell;
        });

        GameObject selectedCard = null;
        bool done = false;
        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
            h.onClick = () => { selectedCard = card; done = true; };
        }
        yield return new WaitUntil(() => done);

        foreach (GameObject card in validCards)
        {
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
        ConfirmQueueManager.RestoreAllHandCards();
        ConfirmQueueManager.ExitSelectionMode();

        if (selectedCard != null)
        {
            CardInstance spellInst = selectedCard.GetComponent<CardInstance>();
            CardData spellTemplate = CardDatabase.Instance?.GetTemplate(spellInst?.templateID);

            if (spellTemplate != null)
            {
                if ((spellTemplate.spellType & SpellType.Counter) != 0)
                {
                    // 有目标法术
                    CounterManager.Instance?.PlayCounter(selectedCard, true);
                    var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
                    if (counter != null) counter.noCostOnTrigger = true;
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                }
                else if (spellTemplate.targetType == TargetType.None)
                {
                // 清理重定向标记
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                    SpellEffectExecutor.Execute(spellTemplate, null);
                }
                else
                {
                // 清理重定向标记
                    NetworkPlayer.Local.handCards.Remove(selectedCard);
                    Destroy(selectedCard);
                    bool targetSelected = false;
                    SelectionManager.Instance.BeginSelection((TargetType)spellTemplate.targetType, (slot) =>
                    {
                        SpellEffectExecutor.Execute(spellTemplate, slot);
                        targetSelected = true;
                    });
                    yield return new WaitUntil(() => targetSelected);
                }
            }
        }

        // 法术可能已造成死亡 → 在同一嵌套树内结算
        int myDepth = NestingContext.Snapshot();
        CheckAndHandleDeaths();
        yield return ActionQueueManager.WaitForDrain();
        yield return new WaitWhile(() => NestingContext.Depth > myDepth);
        if (pendingRevenges.Count > 0 && BattleManager.Instance != null)
            yield return BattleManager.Instance.StartCoroutine(
                BattleManager.ResolveRevengesFromSnapshot());

        NestingContext.Exit();
        CleanupAfterPlacement();
    }
    public IEnumerator ConductorDoubleDeathEffect(DeathEffectData data)
    {
        // 第一棵退场树由 HandleDeath 触发，正在 ActionQueue 中执行。
        // 必须等它完全排空后才开始第二棵。
        int myDepth = NestingContext.Snapshot();
        yield return ActionQueueManager.WaitForDrain();
        yield return new WaitWhile(() => NestingContext.Depth > myDepth);
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);

        if (data != null)
        {
            data.isFullySilenced = false;
            if (GlobalEventManager.Instance != null)
            {
                var bmCheck = FindObjectOfType<BoardManager>();
                var checkSlot = bmCheck?.GetSlot(data.slotID);
                var checkCI = checkSlot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                data.isFullySilenced = checkCI != null && GlobalEventManager.Instance.IsFullySilenced(checkCI);
            }

            if (!data.isFullySilenced && !data.isDeathBlocked)
            {
                NestingContext.Enter("ConductorDouble");
                TriggerDeathEffectFromData(data);

                // 等待第二棵退场树完全结束
                int cdDepth = NestingContext.Snapshot();
                yield return ActionQueueManager.WaitForDrain();
                yield return new WaitWhile(() => NestingContext.Depth > cdDepth);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                NestingContext.Exit();
            }
        }
    }

                // 清理重定向标记
    void TriggerDeathEffectFromData(DeathEffectData data)
    {
        if (data == null) return;

                // 清理重定向标记
        GlobalDeathEventHandler.Trigger(null, data.slotID, data.damageSourceInstanceIDs, data.isActiveExit);

        if (data.isFullySilenced) return;
        if (data.isDeathBlocked) return;
        NetworkPlayer tOwner = BoardManager.GetOwnerPlayer(data.slotID);
        var dp = tOwner;
        string id = data.templateID;
        if (data.isActiveExit)
        {
            switch (id)
            {
                case "01106": tOwner?.AddEnergy(3); break;
                case "01107":
                    tOwner?.AddEnergy(2);
                    {
                        bool hasAlly = false;
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        BoardManager.GetSideRange(data.slotID, out int fcSideStart, out int fcSideEnd);
                        for (int i = fcSideStart; i <= fcSideEnd; i++)
                        {
                            if (bm?.GetSlot(i)?.currentCard3D != null) { hasAlly = true; break; }
                        }
                        if (hasAlly)
                        {
                            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (target) =>
                            {
                                if (target?.currentCard3D != null)
                                {
                                    CardInstance ti = target.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (ti != null)
                                    {
                                        ti.GrantShield(true, false, false);
                                        target.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    }
                                }
                            });
                        }
                    }
                    break;
                default:
                    Debug.LogWarning($"[BoardSlot] HandleDeath(active) 未处理的 templateID: {id}");
                    break;
            }
        }
        else
        {
            switch (id)
            {
                case "01106": dp?.AddEnergy(1); break;
                case "03513":
                    Do03513AOE(this);
                    break;
                default:
                    Debug.LogWarning($"[BoardSlot] HandleDeath(inactive) 未处理的 templateID: {id}");
                    break;
            }
        }

        NetworkPlayer traitOwner = BoardManager.GetOwnerPlayer(data.slotID);

        // ── 01117 自己的可给予退场列表（旧路径，保留）──
        if (id == "01117" && data.giveableDeathTraits != null)
        {
            bool shouldReturn = !data.isActiveExit;
            foreach (string trait in data.giveableDeathTraits)
            {
                switch (trait)
                {
                    case "退场：摸一张牌":
                        if (traitOwner != null) { traitOwner.currentEnergy -= 1; traitOwner.UpdateUI(); }
                        break;
                    case "退场：己方全体受到一伤害":
                        BoardManager bmDH = FindObjectOfType<BoardManager>();
                        if (bmDH != null)
                        {
                            BoardManager.GetSideRange(slotID, out int dhSideStart, out int dhSideEnd);
                            for (int i = dhSideStart; i <= dhSideEnd; i++)
                            {
                                BoardSlot slot = bmDH.GetSlot(i);
                                if (slot?.currentCard3D != null)
                                    BattleManager.Instance.ApplyDamageToMinionPublic(slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance, 1, null);
                            }
                        }
                        break;
                    case "退场：己方玩家扣一血":
                        traitOwner?.TakeDamage(1);
                        break;
                    default:
                        Debug.LogWarning($"[BoardSlot] HandleDeath(giveableDeathTraits) 未处理的 trait: {trait}");
                        break;
                }
            }
            if (shouldReturn && !data.handledReturnToHand)
            {
                CardData template = CardDatabase.Instance?.GetTemplate(data.templateID);
                if (template != null)
                {
                // 清理重定向标记
                }
            }
        }
    }
    /// <summary>碎片(01110)：进场选择己方召唤物触发主动退场。</summary>
    public IEnumerator FragmentEnterEffect(CardInstance giver, BoardSlot mySlot)
    {
        if (!HasAllyTargetExceptSelf())
        {
            giver._hasPendingCoroutine = false;
            CleanupAfterPlacement();
            yield break;
        }

        BoardSlot selectedTarget = null;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null && targetSlot != mySlot)
                selectedTarget = targetSlot;
            done = true;
        });

        yield return new WaitUntil(() => done);

        if (selectedTarget != null)
        {
            // 等一帧确保 EndSelection 完全执行完，选择栈清空
            yield return null;
            var t3d = selectedTarget.currentCard3D?.GetComponent<Card3DInstance>();
            if (t3d?.cardInstance != null)
            {
                t3d.cardInstance.isActiveExit = true;
                selectedTarget.HandleDeath(selectedTarget.currentCard3D);
                // 等主动退场内的交互完成（如妖精的护盾选择）
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                yield return null;
                // 退场完成后广播板面变化到对方
                TurnManager.SyncMyBoardToOpponent();
            }
        }

        giver._hasPendingCoroutine = false;
        CleanupAfterPlacement();
    }

    public IEnumerator ConductorEnterEffect(CardInstance giver)
    {
        if (!HasAllyTargetExceptSelf()) { CleanupAfterPlacement(); yield break; }

        CardInstance targetCI = null;
        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (slot) =>
        {
            if (slot != null && slot.currentCard3D != null && slot != this)
            {
                targetCI = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            }
            done = true;
        });
        yield return new WaitUntil(() => done);
        yield return null;

        if (targetCI != null)
        {
            targetCI.isActiveExit = true;
            targetCI._conductorDoubleDeath = true;
            BoardSlot targetSlot = FindSlotOf(targetCI);
            if (targetSlot != null)
                targetSlot.HandleDeath(targetSlot.currentCard3D);
        }

        // 等待两棵退场树完全结束：
        // 第一棵由 HandleDeath 触发，第二棵由 ConductorDoubleDeathEffect 协程在
        // 第一棵结束后自动启动。WaitForDrain + IsNested 等两棵树全部排空。
        yield return ActionQueueManager.WaitForDrain();
        yield return new WaitWhile(() => NestingContext.IsNested);
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        if (pendingRevenges.Count > 0 && BattleManager.Instance != null)
            yield return BattleManager.Instance.StartCoroutine(
                BattleManager.ResolveRevengesFromSnapshot());

        CleanupAfterPlacement();
    }
    public IEnumerator DeepSeaActiveExitEffect()
    {
        BoardSlot.isStrengtheningSlot = true;

        BoardSlot first = null;
        bool firstDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null) { first = s; firstDone = true; }
        });
        yield return new WaitUntil(() => firstDone);

        // EndSelection 会把 isStrengtheningSlot 清零——二次选择前必须恢复，
        // 否则 IsValidTarget(SingleEnemy) 走 hasCard 路径而非 !isBlocked 路径
        BoardSlot.isStrengtheningSlot = true;

        BoardSlot second = null;
        bool secondDone = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) =>
        {
            if (s != null && s != first) { second = s; secondDone = true; }
        });
        yield return new WaitUntil(() => secondDone);

        BoardSlot.isStrengtheningSlot = false;

        // 两格施加 debuff + 蓝色高亮
        if (first != null) { ApplyDeepSeaDebuffLocal(first); first.deepSeaMarked = true; first.SyncVisual(); }
        if (second != null) { ApplyDeepSeaDebuffLocal(second); second.deepSeaMarked = true; second.SyncVisual(); }

        NetworkPlayer.Local.AddEnergy(1);
        TurnManager.SyncMyBoardToOpponent();
    }

    void ApplyDeepSeaDebuffLocal(BoardSlot slot)
    {
        if (slot == null) return;
        bool alreadyDebuffed = slot.deepSeaAttackDebuff >= 1;
        slot.deepSeaAttackDebuff = 1;   // 不可叠加，始终 -1
        slot.deepSeaHealthDebuff = true;
        if (slot.currentCard3D != null && !alreadyDebuffed)
        {
            var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null)
            {
                ci.currentAttack = Mathf.Max(0, ci.currentAttack - 1);
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }
    }
    public IEnumerator FanaticShamanEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardManager.GetSideRangeOf(giver, out int fsSideStart, out int fsSideEnd);
        List<BoardSlot> allies = new List<BoardSlot>();
        for (int i = fsSideStart; i <= fsSideEnd; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null && s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance != giver)
                allies.Add(s);
        }

        GlobalEventManager.Instance.RegisterAura(new FanaticShamanAura { source = giver });

        foreach (BoardSlot s in allies)
        {
            CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            CardData td = CardDatabase.Instance?.GetTemplate(ci?.templateID);
            Debug.Log($"萨满检测: templateID={ci?.templateID}, hasOnEnter={td?.hasOnEnter}, td={td != null}");
            if (td != null && td.hasOnEnter && ci != null)
            {
                yield return StartCoroutine(s.StartOnEnterEffect(td, ci));
                yield return null;
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
        }

        CleanupAfterPlacement();
    }


    public IEnumerator SummonAllShadows()
    {
        CardData shadowTemplate = CardDatabase.Instance?.GetTemplate("03007");
        if (shadowTemplate?.prefab3D == null) yield break;

        BoardManager bm = FindObjectOfType<BoardManager>();
        int currentShadows = 0;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isShadow) currentShadows++;
            }
        }

        int toSummon = CardInstance.shadowLimit - currentShadows;
        Debug.Log($"SummonAllShadows: limit={CardInstance.shadowLimit}, current={currentShadows}, toSummon={toSummon}");

        for (int k = 0; k < toSummon; k++)
        {
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

            bool placed = false;
            BoardSlot.onTargetSelected = (selectedSlot) =>
            {
                if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.slotID < 6) return;
                string shid = CardZoneManager.GenerateInstanceID(shadowTemplate.templateID);
                GameObject temp = new GameObject("TempShadow");
                CardInstance ti = temp.AddComponent<CardInstance>();
                ti.InitFromTemplate(shadowTemplate, 0, shid);
                ti.isShadow = true;
                ti.currentAttack += CardInstance.shadowAtkBonus;
                ti.baseAttack += CardInstance.shadowAtkBonus;
                ti.currentTier += CardInstance.shadowTierBonus;
                ti.baseTier += CardInstance.shadowTierBonus;
                HandManager hm = FindObjectOfType<HandManager>();
                hm.PlaceCardToSlot(selectedSlot, temp);
                Destroy(temp);

                // Sync shadow to opponent
                if (NetworkClient.isConnected)
                    NetworkPlayer.Local?.CmdPlayCard(shadowTemplate.templateID, selectedSlot.slotID,
                        ti.currentAttack, ti.currentHealth, ti.currentMaxHealth, ti.currentCost, ti.instanceID);

                placed = true;
                SelectionManager.Instance.ForceEndAll();
                BoardSlot.isPlacingCard = false;
                BoardSlot.isStrengtheningSlot = false;
            };
            yield return new WaitUntil(() => placed);
        }
    }

    public IEnumerator ShadowMasterEnterEffect(CardInstance giver)
    {
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        Debug.Log($"影舞者进场: shadowLimit before={CardInstance.shadowLimit}");
        CardInstance.shadowLimit++;
        CardInstance.shadowMasterAlive = true;
        Debug.Log($"影舞者进场: shadowLimit after={CardInstance.shadowLimit}");
        yield return StartCoroutine(SummonAllShadows());
        CleanupAfterPlacement();
    }
    public IEnumerator LordEnterEffect(CardInstance giver)
    {
        CardData ghostTemplate = CardDatabase.Instance?.GetTemplate("03002");
        if (ghostTemplate?.prefab3D == null) { CleanupAfterPlacement(); yield break; }

        for (int k = 0; k < 2; k++)
        {
            BoardSlot.isPlacingCard = true;
            BoardSlot.isStrengtheningSlot = true;
            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

            bool placed = false;
            BoardSlot.onTargetSelected = (selectedSlot) =>
            {
                if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.slotID < 6) return;
                GameObject temp = new GameObject("TempGhost");
                CardInstance ti = temp.AddComponent<CardInstance>();
                string giid = CardZoneManager.GenerateInstanceID(ghostTemplate.templateID);
                ti.InitFromTemplate(ghostTemplate, 0, giid);
                HandManager hm = FindObjectOfType<HandManager>();
                hm.PlaceCardToSlot(selectedSlot, temp);
                Destroy(temp);

                // Sync ghost to opponent — same instanceID as placed model
                if (NetworkClient.isConnected)
                    NetworkPlayer.Local?.CmdPlayCard(ghostTemplate.templateID, selectedSlot.slotID, -1, -1, -1, -1, giid);

                placed = true;
                SelectionManager.Instance.ForceEndAll();
                BoardSlot.isPlacingCard = false;
                BoardSlot.isStrengtheningSlot = false;
            };
            yield return new WaitUntil(() => placed);
        }

        CleanupAfterPlacement();
    }
    public IEnumerator AmplifierEnterEffect(CardInstance giver)
    {
        // 2a. 召唤两名杂兵
        CardData soldierTemplate = CardDatabase.Instance?.GetTemplate("03004");
        if (soldierTemplate?.prefab3D != null)
        {
            for (int k = 0; k < 2; k++)
            {
                BoardSlot.isPlacingCard = true;
                BoardSlot.isStrengtheningSlot = true;
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                bool placed = false;
                BoardSlot.onTargetSelected = (selectedSlot) =>
                {
                    if (selectedSlot == null || selectedSlot.isBlocked || selectedSlot.slotID < 6) return;
                    string siid = CardZoneManager.GenerateInstanceID(soldierTemplate.templateID);
                    GameObject temp = new GameObject("TempSoldier");
                    CardInstance ti = temp.AddComponent<CardInstance>();
                    ti.InitFromTemplate(soldierTemplate, 0, siid);
                    HandManager hm = FindObjectOfType<HandManager>();
                    hm.PlaceCardToSlot(selectedSlot, temp);
                    Destroy(temp);

                    // Sync soldier to opponent — same instanceID as the placed model
                    if (NetworkClient.isConnected)
                        NetworkPlayer.Local?.CmdPlayCard(soldierTemplate.templateID, selectedSlot.slotID, -1, -1, -1, -1, siid);

                    placed = true;
                    SelectionManager.Instance.ForceEndAll();
                    BoardSlot.isPlacingCard = false;
                    BoardSlot.isStrengtheningSlot = false;
                };
                yield return new WaitUntil(() => placed);
            }
        }

        // 2b. 选择己方场上或手牌一召唤物附加机械前缀
        yield return StartCoroutine(AmplifierAddMechPrefix(giver));
        CleanupAfterPlacement();
    }

    IEnumerator AmplifierAddMechPrefix(CardInstance giver)
    {
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        string layerId = SelectionManager.Instance.BeginOpenSelection(TargetType.SingleAlly, null);

        List<GameObject> spellCards = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Spell)
            {
                card.SetActive(false);
                spellCards.Add(card);
            }
        }

        List<GameObject> handSummons = new List<GameObject>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            CardInstance ci = card?.GetComponent<CardInstance>();
            if (ci != null && CardDatabase.Instance?.GetTemplate(ci.templateID)?.cardType == CardType.Summon)
            {
                handSummons.Add(card);
                CardClickHandler h = card.GetComponent<CardClickHandler>() ?? card.AddComponent<CardClickHandler>();
                h.onClick = () =>
                {
                    SelectionManager.Instance.ForceEndAll();
                    CleanupPrefixUI(spellCards, handSummons);
                    ApplyMechPrefix(card);
                    CleanupAfterPlacement();
                };
            }
        }

        BoardSlot.onTargetSelected = (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                SelectionManager.Instance.ForceEndAll();
                CleanupPrefixUI(spellCards, handSummons);
                ApplyMechPrefix(targetSlot.currentCard3D);
                CleanupAfterPlacement();
            }
        };
    }

    void CleanupPrefixUI(List<GameObject> hiddenSpells, List<GameObject> handSummons)
    {
        foreach (GameObject card in hiddenSpells) if (card != null) card.SetActive(true);
        foreach (GameObject card in handSummons)
        {
            if (card == null) continue;
            CardClickHandler h = card.GetComponent<CardClickHandler>();
            if (h != null) Destroy(h);
        }
    }

    void ApplyMechPrefix(GameObject target)
    {
        if (target == null) return;
        CardInstance ci = target.GetComponent<CardInstance>();
        if (ci == null) { Card3DInstance c3d = target.GetComponent<Card3DInstance>(); if (c3d != null) ci = c3d.cardInstance; }
        if (ci != null && !ci.prefixes.Contains("机械"))
        {
            if (string.IsNullOrEmpty(ci.prefixes) || ci.prefixes == "无")
                ci.prefixes = "机械";
            else ci.prefixes += " 机械";
            Card3DInstance c3d = target.GetComponent<Card3DInstance>();
            c3d?.UpdateValues();
            CardDisplay2D d2d = target.GetComponent<CardDisplay2D>();
            d2d?.Refresh();

            // 前缀修改同步到对方
            TurnManager.SyncMyBoardToOpponent();
        }
    }
    public IEnumerator WolfKingEnterEffect(CardInstance giver)
    {
        CardData wolfTemplate = CardDatabase.Instance?.GetTemplate("03006");
        if (wolfTemplate?.prefab3D == null) { CleanupAfterPlacement(); yield break; }

        BoardManager bm = FindObjectOfType<BoardManager>();
        int mySlot = -1;
        for (int i = 0; i <= 11; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == giver)
            { mySlot = i; break; }
        }

        // 只在 Wolf King 所在半场替换
        int sideStart = (mySlot >= 6) ? 6 : 0;
        int sideEnd   = (mySlot >= 6) ? 11 : 5;
        float effectStartTime = Time.time;

        for (int i = sideStart; i <= sideEnd; i++)
        {
            // 玩家正在放置卡牌时暂停循环，防止读到中间态
            yield return new WaitWhile(() => BoardSlot.isPlacingCard);
            if (i == mySlot) continue;
            BoardSlot slot = bm?.GetSlot(i);
            if (slot == null || slot.isBlocked) continue;

            int stackAtk = 0, stackHp = 0, stackMaxHp = 0;

            if (slot.currentCard3D != null)
            {
                CardInstance oldCI = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (oldCI == null || oldCI.currentTier >= 3 || oldCI == giver)
                    continue;
                // 狼(03006)始终可替换，非狼仅替换进场前就存在的
                if (oldCI.templateID != "03006" && oldCI._placedAtTime > effectStartTime)
                    continue;
                stackAtk = oldCI.currentAttack;
                stackHp = oldCI.currentHealth;
                stackMaxHp = oldCI.currentMaxHealth;
                oldCI.isActiveExit = true;
                slot.HandleDeath(slot.currentCard3D);
                yield return null;
            }

            // 生成狼（空位或有被替换的随从）
            Vector3 pos = FindObjectOfType<HandManager>().GetSlotWorldPosition(i);
            GameObject model = Instantiate(wolfTemplate.prefab3D, pos, Quaternion.Euler(0, 180, 0));
            Card3DInstance c3d = model.GetComponent<Card3DInstance>();
            if (c3d != null)
            {
                CardInstance wolfCI = model.AddComponent<CardInstance>();
                string wid = CardZoneManager.GenerateInstanceID(wolfTemplate.templateID);
                wolfCI.InitFromTemplate(wolfTemplate, 0, wid);
                wolfCI.currentAttack += stackAtk;
                wolfCI.baseAttack += stackAtk;
                wolfCI.currentHealth += stackHp;
                wolfCI.currentMaxHealth += stackMaxHp;
                wolfCI.baseHealth += stackHp;
                wolfCI.baseMaxHealth += stackMaxHp;
                wolfCI.wolfKingInstanceID = giver.instanceID;
                wolfCI._placedAtTime = Time.time;
                wolfCI.placementGeneration = BoardSlot.NextPlacementGeneration();
                c3d.cardInstance = wolfCI;
                c3d.UpdateValues();
            }
            model.name = c3d?.cardInstance?.instanceID ?? model.name;
            slot.SetCard(model);

            // Sync wolf to server/opponent — 必须传 override 保留叠加数值
            if (NetworkClient.isConnected)
                NetworkPlayer.Local?.CmdPlayCard(wolfTemplate.templateID, i,
                    c3d?.cardInstance?.currentAttack ?? -1,
                    c3d?.cardInstance?.currentHealth ?? -1,
                    c3d?.cardInstance?.currentMaxHealth ?? -1,
                    c3d?.cardInstance?.currentCost ?? -1,
                    c3d?.cardInstance?.instanceID ?? "");
        }

        CleanupAfterPlacement();
    }
    void UpdateKingDisplay(CardInstance king)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == king)
            {
                bm.GetSlot(i).currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                break;
            }
        }
    }
    public IEnumerator TerroristEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        List<GameObject> diedThisRound = new List<GameObject>();

                // 清理重定向标记
        HashSet<string> beforeEnter = new HashSet<string>();
        BoardManager.GetEnemySideRange(slotID, out int terrEs, out int terrEe);
        for (int i = terrEs; i <= terrEe; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null) beforeEnter.Add(ci.instanceID);
            }
        }

        // 第一次AOE（基于来源槽位动态推断对方半场）
        for (int i = terrEs; i <= terrEe; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                Card3DInstance ei = s.currentCard3D.GetComponent<Card3DInstance>();
                if (ei?.cardInstance != null)
                {
                    BattleManager.Instance?.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                    ei.UpdateValues();
                    if (NetworkClient.isConnected && !NetworkServer.active)
                        NetworkPlayer.Local?.CmdApplyDamageToCard(i, 1);
                }
            }
        }
        BoardSlot.CheckAndHandleDeaths();
        yield return ActionQueueManager.WaitForDrain();
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);

                // 清理重定向标记
        bool anyDied = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D == null && beforeEnter.Count > 0)
            {
                // 清理重定向标记
                anyDied = true;
                break;
            }
        }
        // 准备确认并校验当前instanceID
        HashSet<string> afterEnter = new HashSet<string>();
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null) afterEnter.Add(ci.instanceID);
            }
        }
        anyDied = beforeEnter.Count > afterEnter.Count || !beforeEnter.SetEquals(afterEnter);

                // 清理重定向标记
        while (anyDied)
        {
            beforeEnter = new HashSet<string>(afterEnter);

            for (int i = 0; i <= 5; i++)
            {
                BoardSlot s = bm?.GetSlot(i);
                if (s?.currentCard3D != null)
                {
                    Card3DInstance ei = s.currentCard3D.GetComponent<Card3DInstance>();
                    if (ei?.cardInstance != null)
                    {
                        BattleManager.Instance?.ApplyDamageToMinionPublic(ei.cardInstance, 1, null);
                        ei.UpdateValues();
                        if (NetworkClient.isConnected && !NetworkServer.active)
                            NetworkPlayer.Local?.CmdApplyDamageToCard(i, 1);
                    }
                }
            }
            BoardSlot.CheckAndHandleDeaths();
            yield return ActionQueueManager.WaitForDrain();
            yield return null;
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);

            afterEnter.Clear();
            for (int i = 0; i <= 5; i++)
            {
                BoardSlot s = bm?.GetSlot(i);
                if (s?.currentCard3D != null)
                {
                    CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null) afterEnter.Add(ci.instanceID);
                }
            }
            anyDied = beforeEnter.Count > afterEnter.Count || !beforeEnter.SetEquals(afterEnter);
        }

        CleanupAfterPlacement();

        TurnManager.SyncMyBoardToOpponent();
    }
    public IEnumerator AncientFairyReattach(GameObject fairy, int oldHostSlotID)
    {
        yield return null;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        CardInstance fairyCI = fairy.GetComponent<Card3DInstance>()?.cardInstance;
        if (fairyCI == null) { BoardSlot.isPlacingCard = false; yield break; }

        // 远程方(0-5)委托所属玩家选择，己方(6-11)主机自选（AI 无连接走本地选择）
        if (NetworkServer.active && oldHostSlotID <= 5 && NetworkPlayer.Remote != null
            && NetworkPlayer.Remote.connectionToClient != null)
        {
            int remoteLocalOldHost = oldHostSlotID + 6;
            _fairyReattachDone = false;
            _fairyReattachNewHost = -1;
            NetworkPlayer.Remote.TargetFairyReattachSelect(
                NetworkPlayer.Remote.connectionToClient, remoteLocalOldHost);
            float deadline = Time.time + 20f;
            yield return new WaitUntil(() => _fairyReattachDone || Time.time > deadline);
            if (_fairyReattachNewHost >= 0)
                ApplyFairyReattachToSlot(fairy, fairyCI, _fairyReattachNewHost);
            else
                Destroy(fairy);
            BoardSlot.isPlacingCard = false;
            yield break;
        }

        bool done = false;
        BoardSlot newHost = null;
        isStrengtheningSlot = false;
        extraTargetFilter = (s) => s != null && s.hasCard;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            if (s != null && s.hasCard && s.slotID != oldHostSlotID)
            {
                newHost = s;
                done = true;
            }
        });

        // 与远程路径一致的 20 秒超时，防止战斗阶段卡死
        float deadlineLocal = Time.time + 20f;
        yield return new WaitUntil(() => done || Time.time > deadlineLocal);
        if (!done) Debug.LogWarning($"[AncientFairyReattach] 本地选择超时（20s），自动重附着");

        if (newHost != null)
            ApplyFairyReattachToSlot(fairy, fairyCI, newHost.slotID);
        else
            Destroy(fairy);
        BoardSlot.isPlacingCard = false;
    }

    static bool _fairyReattachDone;
    static int _fairyReattachNewHost;
    public static List<GameObject> _fairyPending = new List<GameObject>();
    public static void OnFairyReattachResult(int serverSlot)
    { _fairyReattachNewHost = serverSlot; _fairyReattachDone = true; }

    /// <summary>01510 远程客户端：选择古老精灵的新宿主。oldHostLocalSlot为远程本地视角(6-11)。</summary>
    public IEnumerator RemoteFairyReattachSelect(int oldHostLocalSlot)
    {
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) { NetworkPlayer.Local?.CmdFairyReattachResult(-1); yield break; }

        // 搜索 attachedModels 和 _fairyPending
        GameObject fairy = null;
        CardInstance fairyCI = null;
        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
        {
            var ci = bm.attachedModels[i]?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAncientFairy && ci.hostSlotID == oldHostLocalSlot)
            {
                fairy = bm.attachedModels[i]; fairyCI = ci;
                bm.attachedModels.RemoveAt(i);
                break;
            }
        }
        if (fairy == null)
        {
            for (int i = _fairyPending.Count - 1; i >= 0; i--)
            {
                var ci = _fairyPending[i]?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isAncientFairy && ci.hostSlotID == oldHostLocalSlot)
                {
                    fairy = _fairyPending[i]; fairyCI = ci;
                    _fairyPending.RemoveAt(i);
                    break;
                }
            }
        }
        if (fairy == null || fairyCI == null) { NetworkPlayer.Local?.CmdFairyReattachResult(-1); yield break; }

        bool done = false;
        BoardSlot newHost = null;
        isStrengtheningSlot = false;
        extraTargetFilter = (s) => s != null && s.hasCard;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (s) =>
        {
            if (s != null && s.hasCard && s.slotID != oldHostLocalSlot)
            {
                newHost = s;
                done = true;
            }
        });

        yield return new WaitUntil(() => done);

        if (newHost != null)
        {
            // 本地立即应用重附着——和 ApplyFairyReattachToSlot 一致
            fairyCI.hostSlotID = newHost.slotID;
            int maxOrder = -1;
            foreach (GameObject obj in bm.attachedModels)
            {
                var ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isAttached && ci.hostSlotID == newHost.slotID)
                    if (ci.attachOrder > maxOrder) maxOrder = ci.attachOrder;
            }
            fairyCI.attachOrder = maxOrder + 1;
            bm.attachedModels.Add(fairy);
            if (newHost.hasCard && newHost.currentCard3D != null)
                BoardManager.SyncAttachedModels(newHost);
            BoardSyncManager.MarkDirty();

            CardInstance nhCI = newHost.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (nhCI != null && !nhCI.cannotHealOrGainMaxHP)
            {
                nhCI.currentHealth += 5;
                nhCI.currentMaxHealth += 5;
                newHost.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
            NetworkPlayer.Local?.CmdFairyReattachResult(newHost.slotID);
        }
        else
            NetworkPlayer.Local?.CmdFairyReattachResult(-1);
    }

    void ApplyFairyReattachToSlot(GameObject fairy, CardInstance fairyCI, int hostSlotID)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardSlot newHost = bm?.GetSlot(hostSlotID);
        if (newHost == null) { Destroy(fairy); return; }

        fairyCI.hostSlotID = hostSlotID;
        int maxOrder = -1;
        foreach (GameObject obj in bm.attachedModels)
        {
            CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached && ci.hostSlotID == hostSlotID)
            {
                if (ci.attachOrder > maxOrder) maxOrder = ci.attachOrder;
            }
        }
        fairyCI.attachOrder = maxOrder + 1;
        bm.attachedModels.Add(fairy);
        BoardSyncManager.MarkDirty();

        if (newHost.hasCard && newHost.currentCard3D != null)
            BoardManager.SyncAttachedModels(newHost);

        CardInstance newHostCI = newHost.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (newHostCI != null)
        {
            if (!newHostCI.cannotHealOrGainMaxHP)
            {
                newHostCI.currentHealth += 5;
                newHostCI.currentMaxHealth += 5;
            }
            newHost.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
        }
    }

    public IEnumerator MistHiderEnterEffect(CardInstance giver)
    {
        yield return null;

        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

        BoardSlot firstSlot = null;
        bool confirmed = false;
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);

        BoardSlot.onTargetSelected = (selected) =>
        {
            if (firstSlot == null) { firstSlot = selected; }
            else if (selected != firstSlot)
            {
                BoardSlot secondSlot = selected;
                GameObject c1 = firstSlot.currentCard3D;
                GameObject c2 = secondSlot.currentCard3D;
                Debug.Log($"换位前 c1 active={c1?.activeSelf}, c2 active={c2?.activeSelf}");
                Vector3 p1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(firstSlot.slotID);
                Vector3 p2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(secondSlot.slotID);
                firstSlot.SetCard(null); secondSlot.SetCard(null);
                if (c2 != null) { c2.transform.position = p1; firstSlot.SetCard(c2); }
                if (c1 != null) { c1.transform.position = p2; secondSlot.SetCard(c1); }
                Debug.Log($"换位前 c1 active={c1?.activeSelf}, c2 active={c2?.activeSelf}");
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                    foreach (GameObject obj in bm.attachedModels)
                    {
                        CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isAttached)
                        {
                            if (ci.hostSlotID == firstSlot.slotID) ci.hostSlotID = secondSlot.slotID;
                            else if (ci.hostSlotID == secondSlot.slotID) ci.hostSlotID = firstSlot.slotID;
                        }
                    }
                BoardManager.SyncAttachedModels(firstSlot);
                BoardManager.SyncAttachedModels(secondSlot);
                // 01517 swap sync — 通知服务器交换结果，防止 SyncNow 覆盖本地交换
                if (NetworkClient.isConnected)
                {
                    NetworkPlayer.Local?.CmdSwapCards(firstSlot.slotID, secondSlot.slotID);
                    TurnManager.SyncMyBoardToOpponent();
                }
                firstSlot = null;
            }
        };

        yield return new WaitUntil(() => confirmed);
        SelectionManager.Instance.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        ConfirmSelectionButton.Instance.Hide();
        CleanupAfterPlacement();
        BoardSlot.SyncMistHiderDisplay();
    }
    public static void SyncMistHiderDisplay()
    {
        var allAuras = GlobalEventManager.Instance?.GetAllAuras();
        if (allAuras == null) return;
        foreach (var a in allAuras)
        {
            if (a is MistHiderAura mist)
                mist.IsActive(); // 触发同步
        }
    }
    public IEnumerator BrilliantMageEnterEffect(CardInstance giver)
    {
        NestingContext.Enter("Spell_01521");
        yield return null;
        NetworkPlayer.Local.handCards.RemoveAll(c => c == null);

        List<CardInstance> spellList = new List<CardInstance>();
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci == null) continue;
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null && td.cardType == CardType.Spell)
                spellList.Add(ci);
        }
        Debug.Log($"[01521] 手牌法术数量={spellList.Count}: {string.Join(",", spellList.ConvertAll(c=>c.templateID))}");

        if (spellList.Count == 0)
        {
            Debug.Log("妖精护盾选择前");
            CleanupAfterPlacement();
            yield break;
        }

        Debug.Log($"[01521] 弹窗前: 手牌法术数量={spellList.Count}, 手牌总数={NetworkPlayer.Local.handCards.Count}, handCards null数={NetworkPlayer.Local.handCards.FindAll(c => c == null).Count}");
        CardDisplayPanel.Instance.multiSelect = true;
        bool confirmed = false;
        CardDisplayPanel.Instance.ShowWithCallback(spellList, ci => true, () =>
        {
            confirmed = true;
        }, "打出");

        yield return new WaitUntil(() => confirmed);

        List<CardInstance> selected = CardDisplayPanel.Instance.GetSelectedCards();

        if (selected.Count == 0)
        {
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
            CleanupAfterPlacement();
            yield break;
        }

        int totalCost = 0;
        foreach (CardInstance ci in selected)
        {
            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td != null) totalCost += td.baseCost;
        }

        if (totalCost > 8)
        {
            Debug.Log($"辉煌法师：法术费用和={totalCost}，限制为8");
            CardDisplayPanel.Instance.Hide();
            CardDisplayPanel.Instance.multiSelect = false;
            CleanupAfterPlacement();
            yield break;
        }

        // 按 templateID 升序排列（与"卡牌编号从小到大"规则一致）
        selected.Sort((a, b) => string.Compare(a.templateID, b.templateID));

        // 复制一份迭代——法术执行中 handCards/selected 可能被修改
        var toPlay = new System.Collections.Generic.List<CardInstance>(selected);

        // Snapshot: 法术结算中的嵌套(Enter/Exit)归零后回到此深度
        int baseDepth = NestingContext.Snapshot();

        int spellIdx = 0;
        foreach (CardInstance ci in toPlay)
        {
            spellIdx++;
            // 上一法术弹窗未关闭 → 等
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            if (CardDisplayPanel.Instance != null)
                yield return new WaitWhile(() => CardDisplayPanel.Instance.panelRoot.activeSelf);
            GameObject cardObj = null;
            foreach (GameObject card in NetworkPlayer.Local.handCards)
            {
                CardInstance handCI = card?.GetComponent<CardInstance>();
                if (handCI != null && handCI.instanceID == ci.instanceID)
                {
                    cardObj = card;
                    break;
                }
            }
            if (cardObj == null)
            {
                Debug.LogWarning($"[01521] [{spellIdx}] 手牌中找不到 cardObj, instanceID={ci.instanceID}, 跳过");
                continue;
            }

            CardData td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (td == null)
            {
                Debug.LogWarning($"[01521] [{spellIdx}] CardData 为空, 跳过");
                continue;
            }

            if ((td.spellType & SpellType.Counter) != 0)
            {
                NetworkPlayer.Local.handCards.Remove(cardObj);
                CounterManager.Instance?.PlayCounter(cardObj, true);
                var counter = CounterManager.Instance?.myCounters?.LastOrDefault();
                if (counter != null) counter.noCostOnTrigger = true;
                Destroy(cardObj);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            }
            else if (td.targetType == TargetType.None)
            {
                NetworkPlayer.Local.handCards.Remove(cardObj);
                CardDrag.ExecuteSpellEffect(td, null);
                // 法术 handler 可能产生异步协程(02501等) → 等待
                if (CardDrag.SpellPending != null) { yield return CardDrag.SpellPending; CardDrag.SpellPending = null; }
                Destroy(cardObj);
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                yield return new WaitForEndOfFrame();
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                yield return new WaitWhile(() => BoardSlot.isPlacingCard);
            }
            else
            {
                if (!CardDrag.HasValidTargetStatic((TargetType)td.targetType))
                {
                    NetworkPlayer.Local.handCards.Remove(cardObj);
                    Destroy(cardObj);
                    continue;
                }
                NetworkPlayer.Local.handCards.Remove(cardObj);
                bool targetDone = false;
                SelectionManager.Instance.BeginSelection((TargetType)td.targetType, (slot) =>
                {
                    CardDrag.ExecuteSpellEffect(td, slot);
                    targetDone = true;
                });
                yield return new WaitUntil(() => targetDone);
                if (CardDrag.SpellPending != null) { yield return CardDrag.SpellPending; CardDrag.SpellPending = null; }
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                yield return new WaitForEndOfFrame();
                yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                yield return new WaitWhile(() => BoardSlot.isPlacingCard);
            }

            // 每个法术独立完成其嵌套同时树
            Debug.Log($"[01521] [{spellIdx}] 开始结算嵌套树");
            CheckAndHandleDeaths();
            yield return ActionQueueManager.WaitForDrain();
            yield return new WaitWhile(() => NestingContext.Depth > baseDepth);
            if (pendingRevenges.Count > 0 && BattleManager.Instance != null)
                yield return BattleManager.Instance.StartCoroutine(BattleManager.ResolveRevengesFromSnapshot());
            // 法术可能弹出面板(02308/02309等)——延迟一帧检测面板
            yield return null;
            if (CardDisplayPanel.Instance != null)
                yield return new WaitWhile(() => CardDisplayPanel.Instance.panelRoot.activeSelf);
            Debug.Log($"[01521] [{spellIdx}] 树结算完成");
        }

        Debug.Log($"[01521] 全部{toPlay.Count}张法术处理完毕");
        CardDisplayPanel.Instance.Hide();
        CardDisplayPanel.Instance.multiSelect = false;
        NestingContext.Exit();
        CleanupAfterPlacement();
    }
    void UpdateRebornDisplay(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            {
                s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                return;
            }
        }
    }
    CardInstance FindRebornOnField()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm?.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01513") return ci;
            }
        }
        return null;
    }
    /// <summary>
    /// 窃贼(01316)主动退场：从对手手牌选择一张获得（窃取，非复制，对方手牌消失）。
    /// </summary>
    public IEnumerator ThiefActiveExitEffect()
    {
        // ═══ 联机：客户端需服务器中转获取对手手牌 ═══
        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            // 槽位映射：客户端 6-11 → 服务器 0-5；客户端 0-5 → 服务器 6-11
            int serverSlot = slotID >= 6 ? slotID - 6 : slotID + 6;
            NetworkPlayer._thiefDone = false;
            NetworkPlayer.Local.CmdRequestThiefHand(serverSlot);
            yield return new WaitWhile(() => !NetworkPlayer._thiefDone);
            yield break;
        }

        // ═══ 服务器/单机：直接读对手手牌 ═══
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        NetworkPlayer oppNp = BoardManager.GetOpponentPlayer(slotID);

        // 联机服务器：向对手请求手牌
        if (NetworkServer.active && oppNp != null && oppNp != owner)
        {
            NetworkPlayer._handReportDone = false;
            oppNp.TargetRequestHandReport(oppNp.connectionToClient);
            yield return new WaitWhile(() => !NetworkPlayer._handReportDone);
        }

        List<GameObject> handSource = (oppNp != null && oppNp != owner && oppNp.handCards.Count > 0)
            ? oppNp.handCards : NetworkPlayer.Local.handCards;

        List<CardInstance> enemyCards = new List<CardInstance>();
        foreach (GameObject card in handSource)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null) enemyCards.Add(ci);
        }

        if (enemyCards.Count == 0) { yield break; }

        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardInstance selected = null;
        CardDisplayPanel.Instance.ShowWithCallback(enemyCards, ci => true, () =>
        {
            selected = CardDisplayPanel.Instance.GetSelectedCard();
            confirmed = true;
        }, "窃取");
        yield return new WaitUntil(() => confirmed);

        if (selected != null)
        {
            GameObject toRemove = null;
            foreach (GameObject card in handSource)
            {
                CardInstance ci = card?.GetComponent<CardInstance>();
                if (ci != null && ci.instanceID == selected.instanceID)
                { toRemove = card; break; }
            }
            if (toRemove != null)
            {
                string stolenIID = selected.instanceID;
                handSource.Remove(toRemove);
                Destroy(toRemove);
                if (oppNp != null && oppNp != owner)
                    oppNp.TargetRemoveHandCard(oppNp.connectionToClient, stolenIID);
                CardData template = CardDatabase.Instance?.GetTemplate(selected.templateID);
                if (template != null)
                    NetworkPlayer.AddCardToHandForPlayer(owner, template);
            }
        }
        CardDisplayPanel.Instance.Hide();
    }
    /// <summary>
    /// 荣誉侍者(01347)主动退场：+2能量，展示对手手牌并弃掉所有邪恶法术。
    /// 遵循 01316 窃贼的联机模式：纯客户端委托服务器执行，服务器读取对手手牌并同步。
    /// </summary>
    public IEnumerator HonorAttendantActiveExit()
    {
        // ═══ 联机：客户端需服务器中转 ═══
        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            int serverSlot = slotID >= 6 ? slotID - 6 : slotID + 6;
            BoardSlot._honorAttendantDone = false;
            NetworkPlayer.Local.CmdRequestHonorAttendantActiveExit(serverSlot);
            yield return new WaitWhile(() => !BoardSlot._honorAttendantDone);
            yield break;
        }

        // ═══ 服务器/单机：统一通过序列化 handData 处理（与 CmdRequestHonorAttendantActiveExit 同逻辑）═══
        NetworkPlayer owner = BoardManager.GetOwnerPlayer(slotID);
        owner.AddEnergy(2);

        NetworkPlayer oppNp = BoardManager.GetOpponentPlayer(slotID);

        // 构建 handData（对手手牌序列化快照）
        List<string> handData = new List<string>();
        if (NetworkServer.active && oppNp != null && oppNp != owner)
        {
            NetworkPlayer._handReportDone = false;
            oppNp.TargetRequestHandReport(oppNp.connectionToClient);
            yield return new WaitWhile(() => !NetworkPlayer._handReportDone);
            foreach (var card in oppNp.handCards)
            {
                if (card == null) continue;
                var ci = card.GetComponent<CardInstance>();
                if (ci != null && !string.IsNullOrEmpty(ci.templateID))
                    handData.Add($"{ci.templateID}|{ci.instanceID}|{ci.currentCost}|{ci.currentAttack}|{ci.currentHealth}|{ci.currentMaxHealth}|{ci.currentTier}|{ci.prefixes ?? ""}|{(ci.hasShield ? "1" : "0")}|{(ci.poisoned ? "1" : "0")}");
            }
        }
        else
        {
            foreach (var card in NetworkPlayer.Local.handCards)
            {
                if (card == null) continue;
                var ci = card.GetComponent<CardInstance>();
                if (ci != null && !string.IsNullOrEmpty(ci.templateID))
                    handData.Add($"{ci.templateID}|{ci.instanceID}|{ci.currentCost}|{ci.currentAttack}|{ci.currentHealth}|{ci.currentMaxHealth}|{ci.currentTier}|{ci.prefixes ?? ""}|{(ci.hasShield ? "1" : "0")}|{(ci.poisoned ? "1" : "0")}");
            }
        }

        if (handData.Count == 0)
        {
            Debug.Log("荣誉侍者主动退场：对手无手牌");
            yield break;
        }

        // 从 handData 构建展示用的 CardInstance 列表
        List<CardInstance> enemyCards = new List<CardInstance>();
        foreach (string entry in handData)
        {
            string[] p = entry.Split('|');
            if (p.Length < 2) continue;
            var go = new GameObject("HonorCard");
            var ci = go.AddComponent<CardInstance>();
            ci.templateID = p[0]; ci.instanceID = p[1];
            if (p.Length > 2 && int.TryParse(p[2], out int v)) ci.currentCost = v;
            if (p.Length > 3 && int.TryParse(p[3], out v)) ci.currentAttack = v;
            if (p.Length > 4 && int.TryParse(p[4], out v)) ci.currentHealth = v;
            if (p.Length > 5 && int.TryParse(p[5], out v)) ci.currentMaxHealth = v;
            if (p.Length > 6 && int.TryParse(p[6], out v)) ci.currentTier = v;
            if (p.Length > 7) ci.prefixes = p[7];
            if (p.Length > 8) ci.hasShield = p[8] == "1";
            if (p.Length > 9) ci.poisoned = p[9] == "1";
            enemyCards.Add(ci);
        }

        CardDisplayPanel.Instance.multiSelect = false;
        bool confirmed = false;
        CardDisplayPanel.Instance.Show(enemyCards, ci => true, "确认");

        ConfirmSelectionButton.Instance?.gameObject.SetActive(true);
        ConfirmSelectionButton.Instance?.Show(() => { confirmed = true; });

        yield return new WaitUntil(() => confirmed);

        // 遍历 handData 弃掉所有邪恶法术——统一使用 RemoveCardFromLocalHand / TargetRemoveHandCard（与窃贼一致）
        int discarded = 0;
        foreach (string entry in handData)
        {
            string[] parts = entry.Split('|');
            if (parts.Length < 2) continue;
            string tid = parts[0];
            string iid = parts[1];
            CardData td = CardDatabase.Instance?.GetTemplate(tid);
            if (td != null && (td.spellType & SpellType.Evil) != 0)
            {
                if (oppNp == null || oppNp == owner || oppNp == NetworkPlayer.Local)
                    NetworkPlayer.RemoveCardFromLocalHand(iid);
                else
                    oppNp.TargetRemoveHandCard(oppNp.connectionToClient, iid);
                discarded++;
            }
        }

        Debug.Log($"荣誉侍者弃掉{discarded}张邪恶法术");

        // 清理展示用的临时 GameObject
        foreach (var c in enemyCards) if (c != null) Destroy(c.gameObject);
        CardDisplayPanel.Instance.Hide();
    }
    public IEnumerator FearlessEnterEffect()
    {
        List<CounterCard> enemyCounters = CounterManager.Instance?.enemyCounters;
        if (enemyCounters == null || enemyCounters.Count == 0)
        {
            CleanupAfterPlacement();
            yield break;
        }

        // 构造临时 CardInstance 列表用于 CardDisplayPanel 弹窗
        List<CardInstance> cardList = new List<CardInstance>();
        List<GameObject> tempGOs = new List<GameObject>();
        foreach (var cc in enemyCounters)
        {
            var td = cc.template;
            if (td == null) continue;
            var go = new GameObject("TempFearlessCard");
            go.hideFlags = HideFlags.HideAndDontSave;
            var ci = go.AddComponent<CardInstance>();
            ci.InitFromTemplate(td, 0);
            cardList.Add(ci);
            tempGOs.Add(go);
        }

        CounterCard selected = null;
        bool done = false;

        var panel = CardDisplayPanel.Instance;
        panel.multiSelect = false;
        panel.showBack = true;
        panel.ShowWithCallback(cardList, ci => true, () =>
        {
            CardInstance si = panel.GetSelectedCard();
            if (si != null)
            {
                int idx = cardList.IndexOf(si);
                if (idx >= 0 && idx < enemyCounters.Count)
                    selected = enemyCounters[idx];
            }
            done = true;
        });

        yield return new WaitUntil(() => done);

        foreach (var go in tempGOs) Destroy(go);

        if (selected != null)
        {
            if (NetworkServer.active)
            {
                // Host/服务器：直接权威处理
                CounterManager.Instance.TriggerEnemyCounterNoEffect(selected);
            }
            else
            {
                // 远端客户端：委托服务器权威处理（否则远端本地修改无法同步到对手）
                NetworkPlayer.Local.CmdFearlessTriggerCounter(selected.template.templateID);
            }
        }

        CleanupAfterPlacement();
    }
    /// <summary>01511 手牌抛置：触发全部已复制抛置特性。</summary>
    public IEnumerator TriggerScholarDiscardFromHover(CardInstance scholar, int discardSlotID)
    {
        BoardSlot mySlot = FindSlotOf(scholar);
        if (mySlot == null) yield break;
        if (scholar.mindScholarCopiedTraits == null || scholar.mindScholarCopiedTraits.Count == 0) yield break;

        foreach (string trait in scholar.mindScholarCopiedTraits)
        {
            if (!trait.Contains("抛置")) continue;
            string key = ExtractTraitKey(trait);
            if (scholar.mindScholarTriggeredKeys.Contains(key)) continue;
            scholar.mindScholarTriggeredKeys.Add(key);
            NestingContext.Enter($"MS_HoverDiscard_{key}");
            TriggerDiscardEffectFromTrait(scholar, trait);
            yield return null;
            yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
            int myDepth2 = NestingContext.Snapshot();
            BoardSlot.CheckAndHandleDeaths();
            yield return ActionQueueManager.WaitForDrain();
            yield return new WaitWhile(() => NestingContext.Depth > myDepth2 || BoardSlot.isPlacingCard);
            TurnManager.SyncMyBoardToOpponent();
            NestingContext.Exit();
            yield break;
        }
    }

    public IEnumerator MindScholarEnterEffect(CardInstance giver)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();

        string newCopyRecord = null;
        string newCopyType = null;

        if (giver.mindScholarCopyCount < 4 && !giver._mindScholarCopyPrompted)
        {
            giver._mindScholarCopyPrompted = true;
            List<CardInstance> targets = new List<CardInstance>();
            BoardManager.GetEnemySideRange(slotID, out int msEs, out int msEe);
            for (int i = msEs; i <= msEe; i++) { BoardSlot s = bm?.GetSlot(i); if (s?.currentCard3D == null) continue; CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance; CardData td = CardDatabase.Instance?.GetTemplate(ci?.templateID); if (td != null && (td.baseCost == 1 || td.baseCost == 3) && (td.hasOnEnter || ci.HasDiscard)) targets.Add(ci); }

            if (targets.Count > 0)
            {
                bool shouldCopy = false, copyChoiceDone = false;
                ConfirmPanel.Instance.Show("是否复制对方特性？", () => { shouldCopy = true; copyChoiceDone = true; }, () => { copyChoiceDone = true; });
                yield return new WaitUntil(() => copyChoiceDone);

                if (shouldCopy)
                {
                    CardInstance selected = null; bool targetDone = false;
                    isStrengtheningSlot = true;
                    extraTargetFilter = (s) => { if (s?.currentCard3D == null) return false; var c = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance; var d = CardDatabase.Instance?.GetTemplate(c?.templateID); return d != null && (d.baseCost == 1 || d.baseCost == 3) && (d.hasOnEnter || (c != null && c.HasDiscard)); };
                    SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (s) => { if (s?.currentCard3D != null) { var c = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance; var d = CardDatabase.Instance?.GetTemplate(c?.templateID); if (d != null && (d.baseCost == 1 || d.baseCost == 3) && (d.hasOnEnter || (c != null && c.HasDiscard))) { selected = c; targetDone = true; } } });
                    yield return new WaitUntil(() => targetDone);
                    isStrengtheningSlot = false; extraTargetFilter = null;

                    if (selected != null)
                    {
                        List<string> copyable = new List<string>();
                        CardData selTD = CardDatabase.Instance?.GetTemplate(selected.templateID);
                        if (selTD != null && selTD.hasOnEnter) copyable.Add("进场");
                        if (selected.HasDiscard) copyable.Add("抛置");
                        string chosen = copyable.Count == 1 ? copyable[0] : null;
                        if (copyable.Count == 2) { bool traitDone = false; GenericChoicePanel.Instance.Show("选择复制特性", copyable, (i) => { chosen = copyable[i]; traitDone = true; }); yield return new WaitUntil(() => traitDone); }

                        if (chosen != null)
                        {
                            giver.mindScholarCopyCount++;
                            string text = GetTraitFullText(selected, chosen);
                            newCopyRecord = $"{selected.templateID}:{chosen}:{text}";
                            newCopyType = chosen;
                            giver.mindScholarCopiedTraits.Add(newCopyRecord);
                            giver.GrantTrait(text);
                            if (NetworkClient.isConnected && !NetworkServer.active) TurnManager.SyncMyBoardToOpponent();
                        }
                    }
                }
            }
        }

        string snapshotNew = newCopyRecord;
        string snapshotNewType = newCopyType;

        // 1. 遍历所有已复制的进场特性——每次重进场都触发
        foreach (string t in giver.mindScholarCopiedTraits)
        {
            if (t == snapshotNew) continue;   // 本次新复制的留到第2步单独处理
            if (!t.Contains("进场")) continue;
            string key = ExtractTraitKey(t);
            string tid = ExtractTemplateIDFromTrait(t);
            if (string.IsNullOrEmpty(tid)) continue;
            NestingContext.Enter($"MS_Enter_{key}");
            var td = CardDatabase.Instance?.GetTemplate(tid);
            if (td != null && td.hasOnEnter)
                yield return StartCoroutine(RunCopiedEnterEffect(giver, td));
            NestingContext.Exit();
        }

        // 2. 触发本次新复制的特性（进场或抛置，均在进场树中立即触发一次）
        if (snapshotNew != null)
        {
            string type = snapshotNewType ?? (snapshotNew.Contains("进场") ? "进场" : "抛置");
            string key = ExtractTraitKey(snapshotNew);
            string tid = ExtractTemplateIDFromTrait(snapshotNew);
            if (!string.IsNullOrEmpty(tid))
            {
                NestingContext.Enter($"MS_{type}_{key}");
                if (type == "进场")
                {
                    var td = CardDatabase.Instance?.GetTemplate(tid);
                    if (td != null && td.hasOnEnter)
                        yield return StartCoroutine(RunCopiedEnterEffect(giver, td));
                }
                else // "抛置"——进场树中立即触发，不消耗手动弃牌机会
                {
                    TriggerDiscardEffectFromTrait(giver, snapshotNew);
                    yield return null;
                    yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                    int myDepth2 = NestingContext.Snapshot();
                    BoardSlot.CheckAndHandleDeaths();
                    yield return ActionQueueManager.WaitForDrain();
                    yield return new WaitWhile(() => NestingContext.Depth > myDepth2 || BoardSlot.isPlacingCard);
                    TurnManager.SyncMyBoardToOpponent();
                }
                NestingContext.Exit();
            }
        }

        CleanupAfterPlacement();
    }

    /// <summary>运行复制的进场效果—使用 EffectDispatcher 分发到原卡 handler。</summary>
    IEnumerator RunCopiedEnterEffect(CardInstance giver, CardData originalTD)
    {
        var mySlot = FindSlotOf(giver);
        if (mySlot == null) yield break;

        // 统一走 EffectDispatcher——不传 source=giver，TemplateID 落到 originalTD.templateID
        var effectCtx = new EffectContext { template = originalTD, sourceSlot = mySlot, trigger = Trigger.Enter };
        if (EffectDispatcher.Dispatch(Trigger.Enter, effectCtx) && effectCtx.StartedCoroutine != null)
            yield return effectCtx.StartedCoroutine;
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);

        // 嵌套树结算——handler 内部可能触发的死亡/退场在当前层内完成
        int myDepth = NestingContext.Snapshot();
        BoardSlot.CheckAndHandleDeaths();
        yield return ActionQueueManager.WaitForDrain();
        yield return new WaitWhile(() => NestingContext.Depth > myDepth || BoardSlot.isPlacingCard);
        if (BoardSlot.pendingRevenges.Count > 0 && BattleManager.Instance != null)
            yield return BattleManager.Instance.StartCoroutine(BattleManager.ResolveRevengesFromSnapshot());
        TurnManager.SyncMyBoardToOpponent();
    }

    string ExtractTraitKey(string recordText)
    {
        string[] parts = recordText.Split(':');
        return parts.Length >= 2 ? $"{parts[0]}:{parts[1]}" : recordText;
    }

    string ExtractTemplateIDFromTrait(string recordText)
    {
        string[] parts = recordText.Split(':');
        return parts.Length > 0 ? parts[0] : null;
    }
    void TriggerDiscardEffectFromTrait(CardInstance ci, string recordText)
    {
        string templateID = ExtractTemplateIDFromTrait(recordText);
        if (string.IsNullOrEmpty(templateID)) return;

        // 根据原卡牌的templateID触发抛置效果
        switch (templateID)
        {
            case "01343":
                if (HasEnemyTarget())
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, ci.currentAttack, null);
                                t3d.UpdateValues();
                                if (NetworkClient.isConnected && !NetworkServer.active)
                                    NetworkPlayer.Local?.CmdApplyDamageToCard(target.slotID, ci.currentAttack);
                            }
                        }
                        BoardSlot.CheckAndHandleDeaths();
                    });
                }
                break;
            case "01136":
                if (HasEnemyTarget())
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            {
                                BattleManager.Instance.ApplyDamageToMinionPublic(t3d.cardInstance, 1, null);
                                t3d.UpdateValues();
                                if (NetworkClient.isConnected && !NetworkServer.active)
                                    NetworkPlayer.Local?.CmdApplyDamageToCard(target.slotID, 1);
                            }
                        }
                        BoardSlot.CheckAndHandleDeaths();
                    });
                }
                break;
            case "01346": // 士兵：为己方一召唤物恢复3生命值
                if (HasAllyTarget(ci))
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleAlly, mySlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            // 纯客户端委托服务器权威执行，避免本地修改被 SyncNow 覆盖
                            if (NetworkClient.isConnected && !NetworkServer.active)
                            {
                                NetworkPlayer.Local.CmdDiscardHeal01346(target.slotID);
                            }
                            else
                            {
                                Card3DInstance t3d = target.currentCard3D.GetComponent<Card3DInstance>();
                                t3d?.cardInstance?.ReceiveHeal(3, CardInstance.HealSourceType.Minion);
                                t3d?.UpdateValues();
                            }
                        }
                    });
                }
                break;
            case "01344": // 诅咒女巫：使对方攻击力永久-2
                if (HasEnemyTarget())
                {
                    BoardSlot mySlot = FindSlotOf(ci);
                    int mySlotID = mySlot?.slotID ?? -1;
                    BoardSlot.StartDiscardSelection(TargetType.SingleEnemy, mySlotID, (target) =>
                    {
                        if (target?.currentCard3D != null)
                        {
                            // 纯客户端委托服务器权威执行，避免本地修改被 SyncNow 覆盖
                            if (NetworkClient.isConnected && !NetworkServer.active)
                            {
                                NetworkPlayer.Local.CmdDiscardDebuff01344(target.slotID);
                            }
                            else
                            {
                                DiscardHandlers.Apply01344Debuff(target);
                            }
                        }
                    });
                }
                break;
            case "01135": // 杂耍大师：交换己方两召唤物
                if (HasAllyTarget(ci))
                {
                    HandManager hm = FindObjectOfType<HandManager>();
                    hm.StartCoroutine(hm.SwapTwoAllies());
                }
                break;
            default:
                Debug.LogWarning($"[BoardSlot] TriggerDiscardEffectFromTrait 未处理的 templateID: {templateID}");
                break;
        }
    }
    bool HasAllyTarget(CardInstance source)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (!BoardManager.GetSideRangeOf(source, out int s, out int e)) return false;
        for (int i = s; i <= e; i++)
            if (bm?.GetSlot(i)?.currentCard3D != null) return true;
        return false;
    }

    /// <summary>
    /// 弃牌专用选择方法，自动排除掉自己的槽位
    /// </summary>
    public static void StartDiscardSelection(TargetType targetType, int ignoreSlotID, Action<BoardSlot> onSelected)
{
    Card3DHover.ignoreSlotID = ignoreSlotID;
    SelectionManager.Instance.BeginSelection(targetType, (selectedSlot) =>
    {
        if (selectedSlot.slotID == Card3DHover.ignoreSlotID)
        {
            Card3DHover.ignoreSlotID = -1;
            return;
        }
        Card3DHover.ignoreSlotID = -1;
        onSelected?.Invoke(selectedSlot);
    });
}

}