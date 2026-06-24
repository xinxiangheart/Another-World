using System.Collections.Generic;
using UnityEngine;

public class ChosenOneManager : MonoBehaviour
{
    public static ChosenOneManager Instance { get; private set; }

    private List<CardData> chosenOneDeck = new List<CardData>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        InitializeDeck();
    }

    void InitializeDeck()
    {
        CardData[] allCards = Resources.LoadAll<CardData>("ChosenOneData");
        foreach (CardData template in allCards)
        {
            chosenOneDeck.Add(template);
        }
        Shuffle(chosenOneDeck);
        Debug.Log($"神选者牌堆初始化完成，共 {chosenOneDeck.Count} 张");
    }

    public CardData DrawChosenOne()
    {
        if (chosenOneDeck.Count == 0) return null;
        CardData card = chosenOneDeck[0];
        chosenOneDeck.RemoveAt(0);
        return card;
    }

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