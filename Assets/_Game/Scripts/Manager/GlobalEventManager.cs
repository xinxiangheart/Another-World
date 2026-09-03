using System;
using System.Collections.Generic;
using UnityEngine;

public class GlobalEventManager : MonoBehaviour
{
    public static GlobalEventManager Instance { get; private set; }

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    // ===== 事件 =====
    public event Action<CardInstance> OnMinionEntered;        // 进场完成
    public event Action<CardInstance> OnMinionDying;          // 退场前
    public event Action<CardInstance> OnMinionDied;           // 退场后
    public event Action<CardData> OnCardPlayedAndResolved;    // 卡牌结算完成
    public event Action OnBattlePhaseStart;
    public event Action OnBattlePhaseEnd;
    public event Action<CardInstance, string> OnTraitGranted;
    public event Action<CardInstance, string> OnTraitRemoved;

    public void TriggerMinionEntered(CardInstance ci) => OnMinionEntered?.Invoke(ci);
    public void TriggerMinionDying(CardInstance ci) => OnMinionDying?.Invoke(ci);
    public void TriggerMinionDied(CardInstance ci) => OnMinionDied?.Invoke(ci);
    public void TriggerCardPlayedAndResolved(CardData data) => OnCardPlayedAndResolved?.Invoke(data);
    public void TriggerBattlePhaseStart() => OnBattlePhaseStart?.Invoke();
    public void TriggerBattlePhaseEnd() => OnBattlePhaseEnd?.Invoke();
    public void TriggerTraitGranted(CardInstance ci, string t) => OnTraitGranted?.Invoke(ci, t);
    public void TriggerTraitRemoved(CardInstance ci, string t) => OnTraitRemoved?.Invoke(ci, t);

    // ===== 效果拦截 =====
    /// <summary>下一张打出的牌是否被无效</summary>
    public bool NextCardNullified;

    /// <summary>待重定向的进场效果（对方召唤物被反制后，进场由己方触发）</summary>
    public CardData PendingEnterRedirectTemplate;
    public CardInstance PendingEnterRedirectInstance;
    /// <summary>重定向目标是否为 Host（true=Host 己方6-11, false=Remote 己方0-5）</summary>
    public bool PendingEnterRedirectToHost;

    // ===== 光环管理 =====
    private List<AuraBase> auras = new List<AuraBase>();
    public void RegisterAura(AuraBase a) => auras.Add(a);
    public void UnregisterAura(AuraBase a) => auras.Remove(a);

    public bool IsTraitBlocked(CardInstance ci, string trait)
    {
        // 本地光环检查
        foreach (var a in auras)
            if (a.IsActive() && a.BlocksTrait(ci, trait)) return true;

        // 网络兜底：基于已同步的棋盘状态判断（对方客户端没有光环实例）
        if (ci.silencedThisPhase) return true;
        if (IsTraitBlockedByBoardState(ci, trait)) return true;

        return false;
    }

    public bool IsFullySilenced(CardInstance ci)
    {
        // 本地光环检查
        foreach (var a in auras)
            if (a.IsActive() && a.IsTargetFullySilenced(ci)) return true;

        // 网络兜底：基于已同步的棋盘状态判断
        if (ci.silencedThisPhase) return true;
        if (IsSilencedByEnergyHacker(ci)) return true;

        return false;
    }

    /// <summary>兜底：根据棋盘状态判断特性是否被狂热萨满(01515)/法官(01323)压制</summary>
    bool IsTraitBlockedByBoardState(CardInstance ci, string trait)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        int targetSlot = GetSlotOf(ci, bm);
        if (targetSlot < 0) return false;

        // 判断目标卡牌属于哪一方（6-11=己方, 0-5=对方）
        bool targetIsAlly = targetSlot >= 6;
        int enemySearchStart = targetIsAlly ? 0 : 6;
        int enemySearchEnd = targetIsAlly ? 5 : 11;

        // 检查对方场上是否有狂热萨满(01515)：禁止进场+抛置
        if (trait == "进场" || trait == "抛置")
        {
            for (int i = enemySearchStart; i <= enemySearchEnd; i++)
            {
                CardInstance enemy = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (enemy != null && enemy.templateID == "01515" && !enemy.silencedThisPhase && !IsFullySilenced(enemy))
                    return true;
            }
        }
        // 检查对方场上是否有法官(01323)：禁止退场
        if (trait == "退场")
        {
            for (int i = enemySearchStart; i <= enemySearchEnd; i++)
            {
                CardInstance enemy = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (enemy != null && enemy.templateID == "01323" && !enemy.silencedThisPhase && !IsFullySilenced(enemy))
                    return true;
            }
        }
        return false;
    }

    /// <summary>兜底：检查是否被能量骇客(01335)对位压制（递归安全：仅检查silencedThisPhase）</summary>
    bool IsSilencedByEnergyHacker(CardInstance ci)
    {
        if (ci.templateID == "01335") return false;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        int slot = GetSlotOf(ci, bm);
        if (slot < 0) return false;

        int oppSlot = slot < 6 ? slot + 6 : slot - 6;
        CardInstance opp = bm.GetSlot(oppSlot)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (opp == null || opp.templateID != "01335") return false;
        if (opp.silencedThisPhase) return false;
        // 能量骇客附着时检查实际槽位
        if (opp.isAttached && opp.hostSlotID != oppSlot) return false;
        return true;
    }

    int GetSlotOf(CardInstance ci, BoardManager bm)
    {
        for (int i = 0; i < 12; i++)
        {
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return i;
        }
        return -1;
    }
    public void UnregisterAuraOfSource(CardInstance source)
    {
        foreach (var a in auras)
        {
            if (a.source == source && a is ScarletSaintAura s)
            {
                OnMinionEntered -= s._handler;
            }
        }
        auras.RemoveAll(a => a.source == source);
    }

    // ===== 4.2 光环受害者状态（AddStatus activeStatuses 展示层）=====
    // 三张"封锁/沉默"光环不给受害者写字段，靠持续查询（IsTraitBlocked/IsFullySilenced）生效。
    // 受害者的 activeStatuses 标签用"中央 Refresh"按板面现算——谓词与查询同源，去重幂等，各端可跑。

    /// <summary>按当前板面重算一张卡的三个封锁/沉默光环受害者状态。</summary>
    public void RefreshAuraStatusForCard(CardInstance ci)
    {
        if (ci == null) return;
        SyncAuraStatus(ci, "01323",
            IsTraitBlockedByBoardState(ci, "退场"),
            "无法触发退场（含主动退场）"); // 法官：禁对方退场（含主动退场，与 BoardSlot 同清 hasOnDeath+hasActiveExit 一致）
        SyncAuraStatus(ci, "01515",
            IsTraitBlockedByBoardState(ci, "进场") || IsTraitBlockedByBoardState(ci, "抛置"),
            "无法触发进场和抛置特性"); // 狂热萨满：禁对方进场/抛置
        SyncAuraStatus(ci, "01335",
            IsUnderEnergyHacker(ci),
            "无法触发任何特性（能量骇客封锁）"); // 能量骇客：对位完全沉默
    }

    /// <summary>全板 12 槽重算光环受害者状态（阶段边界/光环源进场时调用）。</summary>
    public void RefreshAuraStatusesForBoard()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            var ci = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null) RefreshAuraStatusForCard(ci);
        }
    }

    static void SyncAuraStatus(CardInstance ci, string sourceID, bool active, string description)
    {
        if (active) ci.AddStatus(true, description, sourceID);
        else ci.RemoveStatusBySource(sourceID);
    }

    /// <summary>能量骇客对位判定：独立占位或附着（hostSlotID 动态）都要算。补 IsSilencedByEnergyHacker 漏附着的情况。</summary>
    bool IsUnderEnergyHacker(CardInstance ci)
    {
        if (ci == null || ci.templateID == "01335") return false;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        int slot = GetSlotOf(ci, bm);
        if (slot < 0) return false;
        int oppSlot = slot < 6 ? slot + 6 : slot - 6;
        CardInstance opp = bm.GetSlot(oppSlot)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
        if (opp != null && opp.templateID == "01335" && !opp.silencedThisPhase) return true;
        foreach (GameObject obj in bm.attachedModels)
        {
            var aci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
            if (aci != null && aci.templateID == "01335" && aci.hostSlotID == oppSlot && !aci.silencedThisPhase)
                return true;
        }
        return false;
    }
    /// <summary>己方玩家受到伤害时触发，参数为伤害量</summary>
    public event Action<int> OnPlayerDamaged;
    public void TriggerPlayerDamaged(int amount)
    {
        Debug.Log($"TriggerPlayerDamaged: amount={amount}, subscribers={OnPlayerDamaged?.GetInvocationList()?.Length}");
        OnPlayerDamaged?.Invoke(amount);
    }
    public List<AuraBase> GetAurasOfSource(CardInstance source)
    {
        return auras.FindAll(a => a.source == source);
    }
    public List<AuraBase> GetAllAuras() => auras;
}