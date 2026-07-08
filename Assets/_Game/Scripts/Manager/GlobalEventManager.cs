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
        int slot = GetSlotOf(ci, bm);
        if (slot < 0 || slot >= 6) return false; // 只压制对方（slot 0-5）

        // 检查对方场上是否有狂热萨满(01515)：禁止进场+抛置
        if (trait == "进场" || trait == "抛置")
        {
            for (int i = 6; i <= 11; i++)
            {
                CardInstance ally = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ally != null && ally.templateID == "01515" && !ally.silencedThisPhase && !IsFullySilenced(ally))
                    return true;
            }
        }
        // 检查对方场上是否有法官(01323)：禁止退场
        if (trait == "退场")
        {
            for (int i = 6; i <= 11; i++)
            {
                CardInstance ally = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ally != null && ally.templateID == "01323" && !ally.silencedThisPhase && !IsFullySilenced(ally))
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
        auras.RemoveAll(a => a.source == source);
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