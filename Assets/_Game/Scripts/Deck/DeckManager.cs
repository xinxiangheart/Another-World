using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance { get; private set; }

    public List<CardData> mainDeck = new List<CardData>();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Only server initializes the shared deck in online mode.
        // Clients get cards via TargetRpc from the server.
        if (NetworkServer.active || !NetworkClient.isConnected)
        {
            InitializeDeck();
        }
        else
        {
            Debug.Log("[DeckManager] Client: skipping deck init, server owns the deck");
        }
    }

    void InitializeDeck()
    {
        CardData[] allCards = Resources.LoadAll<CardData>("CardData");
        foreach (CardData template in allCards)
        {
            if (!template.addToMainDeck)
                continue;

            int copies = template.copyCount;
            for (int i = 0; i < copies; i++)
            {
                CardData instance = Instantiate(template);
                instance.templateID = template.templateID;
                mainDeck.Add(instance);
            }
        }
        Shuffle(mainDeck);
        Debug.Log($"Deck initialized: {mainDeck.Count} cards");
    }

    public CardData DrawFromMain()
    {
        if (mainDeck.Count == 0) return null;
        CardData card = mainDeck[0];
        mainDeck.RemoveAt(0);
        return card;
    }

    public int RemainingCards => mainDeck.Count;

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
