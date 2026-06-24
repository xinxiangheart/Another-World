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
        foreach (var a in auras)
            if (a.IsActive() && a.BlocksTrait(ci, trait)) return true;
        return false;
    }

    public bool IsFullySilenced(CardInstance ci)
    {
        foreach (var a in auras)
            if (a.IsActive() && a.IsTargetFullySilenced(ci)) return true;
        return false;
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