using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 向后兼容层。所有弃牌堆操作委托给 CardZoneManager。
/// </summary>
[Serializable]
public class GraveEntry
{
    public string templateID;
    public string instanceID;
    public int currentCost;
    public int currentAttack;
    public int baseAttack;
    public int currentHealth;
    public int baseHealth;
    public int baseMaxHealth;
    public int currentMaxHealth;
    public int currentTier;
    public int baseTier;
    public string prefixes;
    public bool handledReturnToHand;
    public int deathPhase;
}

public class GraveyardManager : MonoBehaviour
{
    public static GraveyardManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>弃牌堆列表（兼容旧代码直接访问 graveyard 字段）。</summary>
    public List<GraveEntry> graveyard => CardZoneManager.Instance?.AllGraveEntries ?? new List<GraveEntry>();

    /// <summary>添加进弃牌堆。</summary>
    public void AddToGraveyard(GraveEntry entry)
    {
        CardZoneManager.Instance?.AddToGraveyard(entry);
    }

    /// <summary>按 instanceID 查找并移出。</summary>
    public GraveEntry FindByInstanceID(string instanceID)
    {
        return CardZoneManager.Instance?.RemoveFromGraveyard(instanceID);
    }
}
