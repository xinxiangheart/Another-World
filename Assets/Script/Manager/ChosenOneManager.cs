using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ChosenOneManager : MonoBehaviour
{
    public static ChosenOneManager Instance { get; private set; }

    private List<CardData> chosenOneDeck = new List<CardData>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Only server initializes the chosen-one deck in online mode
        if (NetworkServer.active || !NetworkClient.isConnected)
        {
            InitializeDeck();
        }
        else
        {
            Debug.Log("[ChosenOneManager] Client: skipping deck init, server owns the deck");
        }
    }

    void InitializeDeck()
    {
        CardData[] allCards = Resources.LoadAll<CardData>("ChosenOneData");
        foreach (CardData template in allCards)
        {
            chosenOneDeck.Add(template);
        }
        Shuffle(chosenOneDeck);
        Debug.Log($"ChosenOne deck: {chosenOneDeck.Count} cards");
    }

    public CardData DrawChosenOne()
    {
        if (chosenOneDeck.Count == 0) return null;
        CardData card = chosenOneDeck[0];
        chosenOneDeck.RemoveAt(0);
        return card;
    }

    public int RemainingCards => chosenOneDeck.Count;

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
