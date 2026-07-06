using System;
using System.Collections.Generic;
using UnityEngine;

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
    public List<GraveEntry> graveyard = new List<GraveEntry>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddToGraveyard(GraveEntry entry)
    {
        if (entry != null)
            graveyard.Add(entry);
    }

    public GraveEntry FindByInstanceID(string instanceID)
    {
        for (int i = graveyard.Count - 1; i >= 0; i--)
        {
            if (graveyard[i].instanceID == instanceID)
            {
                GraveEntry target = graveyard[i];
                graveyard.RemoveAt(i);
                return target;
            }
        }
        return null;
    }
}