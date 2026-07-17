using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 高仿兼容层：所有牌库逻辑已迁至 CardZoneManager。
/// DrawFromMain() 仍返回 CardData（兼容旧代码），但每张卡携带唯一 _instanceID。
/// </summary>
public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance { get; private set; }

    /// <summary>兼容 GetCardPanel 等直接遍历牌库的旧代码。</summary>
    public List<CardData> mainDeck = new List<CardData>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Ensure CardZoneManager exists and deck is initialized
        var czm = CardZoneManager.Instance;
        if (czm == null)
        {
            var go = new GameObject("CardZoneManager");
            go.AddComponent<CardZoneManager>();
            czm = CardZoneManager.Instance;
        }
        if (NetworkServer.active || !NetworkClient.isConnected)
        {
            czm?.InitializeDeck();
        }
        else
        {
            Debug.Log("[DeckManager] Client: skipping deck init, server owns the deck");
        }
    }

    /// <summary>从牌库抽一张（兼容旧调用）。返回的 CardData._instanceID 为全局唯一。</summary>
    public CardData DrawFromMain()
    {
        var czm = CardZoneManager.Instance;
        if (czm == null) return null;

        var result = czm.DrawFromDeck();
        if (result == null) return null;

        CardData template = CardDatabase.Instance?.GetTemplate(result.Value.templateID);
        if (template == null) return null;

        CardData clone = Instantiate(template);
        clone.templateID = template.templateID;
        clone._instanceID = result.Value.instanceID;
        return clone;
    }

    /// <summary>兼容旧调用：剩余卡数。</summary>
    public int RemainingCards => CardZoneManager.Instance?.DeckCount ?? 0;
}
