using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance { get; private set; }

    // 公共大牌库（所有可抽取的牌）
    public List<CardData> mainDeck = new List<CardData>();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeDeck();
    }

    // 初始化牌库：加载模板，按 copyCount 生成副本，洗牌
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
        Debug.Log($"牌库初始化完成，共 {mainDeck.Count} 张牌");
    }

    // 从牌库顶部抽一张牌
    public CardData DrawFromMain()
    {
        if (mainDeck.Count == 0) return null;
        CardData card = mainDeck[0];
        mainDeck.RemoveAt(0);
        return card;
    }

    // Fisher-Yates 洗牌算法
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