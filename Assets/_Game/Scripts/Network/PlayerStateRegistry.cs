using System.Collections.Generic;

/// <summary>
/// 单玩家全量状态 —— 五区 + 玩家属性 + instanceID 快速索引。
/// 服务端维护 Local + Remote 两个实例；客户端持有对方镜像。
/// </summary>
public class PlayerStateRegistry
{
    // ═══════════════════ 玩家属性 ═══════════════════
    public int currentHealth;
    public int maxHealth;
    public int currentEnergy;
    public int maxEnergy;
    public int maxHandSize;

    // ═══════════════════ 五区数据 ═══════════════════

    /// <summary>牌库（仅服务端有完整数据；客户端仅有数量）</summary>
    public List<(string tid, string iid)> deck = new List<(string, string)>();

    /// <summary>手牌 instanceID 列表（联机服务端由 AddServerSideCard 维护）</summary>
    public List<string> hand = new List<string>();

    /// <summary>板面 6 槽（该玩家的半场 0-5，映射到服务端坐标后填充）</summary>
    public CardStateProto?[] board = new CardStateProto?[6];

    /// <summary>墓地入口列表</summary>
    public List<GraveEntry> graveyard = new List<GraveEntry>();

    /// <summary>放逐区 instanceID 列表</summary>
    public List<string> exile = new List<string>();

    // ═══════════════════ 快速索引 ═══════════════════
    Dictionary<string, CardStateProto> _index = new Dictionary<string, CardStateProto>();

    // ═══════════════════ 区计数 ═══════════════════
    public int DeckCount => deck.Count;
    public int HandCount => hand.Count;
    public int GraveyardCount => graveyard.Count;
    public int ExileCount => exile.Count;

    // ═══════════════════ 索引操作 ═══════════════════

    public CardStateProto? GetCard(string instanceID)
    {
        if (string.IsNullOrEmpty(instanceID)) return null;
        return _index.TryGetValue(instanceID, out var c) ? c : (CardStateProto?)null;
    }

    public void Upsert(CardStateProto card)
    {
        if (!card.IsValid) return;
        _index[card.instanceID] = card;

        // 更新区列表
        RemoveFromAllZones(card.instanceID);
        switch (card.zone)
        {
            case CardZone.Deck:
                deck.Add((card.templateID, card.instanceID)); break;
            case CardZone.Hand:
                if (!hand.Contains(card.instanceID)) hand.Add(card.instanceID); break;
            case CardZone.Board:
                if (card.slotID >= 0 && card.slotID < 6)
                    board[card.slotID] = card; break;
            case CardZone.Graveyard:
                graveyard.Add(new GraveEntry { templateID = card.templateID, instanceID = card.instanceID }); break;
            case CardZone.Exile:
                if (!exile.Contains(card.instanceID)) exile.Add(card.instanceID); break;
            default:
                UnityEngine.Debug.LogWarning($"[PlayerStateRegistry] 未知 CardZone: {card.zone} for {card.instanceID}");
                break;
        }
    }

    public void Remove(string instanceID)
    {
        if (string.IsNullOrEmpty(instanceID)) return;
        _index.Remove(instanceID);
        RemoveFromAllZones(instanceID);
    }

    void RemoveFromAllZones(string iid)
    {
        deck.RemoveAll(d => d.iid == iid);
        hand.Remove(iid);
        for (int i = 0; i < 6; i++)
            if (board[i]?.instanceID == iid) board[i] = null;
        graveyard.RemoveAll(g => g.instanceID == iid);
        exile.Remove(iid);
    }

    // ═══════════════════ 查询 API ═══════════════════

    /// <summary>按 instanceID 查询卡状态快照（含 templateID / slotID / zone 等），供来源溯源等按 ID 反查。
    /// 查不到返回 null（CardStateProto 是 struct，用可空表达"不存在"）。等价于 GetCard，语义命名更明确。</summary>
    public CardStateProto? GetCardStateByInstanceID(string instanceID)
        => GetCard(instanceID);

    public List<CardStateProto> GetHandCards()
    {
        var list = new List<CardStateProto>();
        foreach (var iid in hand)
        {
            var card = GetCard(iid);
            if (card.HasValue) list.Add(card.Value);
        }
        return list;
    }

    public List<CardStateProto> GetBoardCards()
    {
        var list = new List<CardStateProto>();
        for (int i = 0; i < 6; i++)
            if (board[i].HasValue && board[i].Value.IsValid)
                list.Add(board[i].Value);
        return list;
    }

    public List<CardStateProto> GetCardsByPrefix(string prefix)
    {
        var list = new List<CardStateProto>();
        foreach (var kv in _index)
            if (kv.Value.prefixes != null && kv.Value.prefixes.Contains(prefix))
                list.Add(kv.Value);
        return list;
    }

    public List<CardStateProto> GetCardsByTrait(string trait)
    {
        var list = new List<CardStateProto>();
        foreach (var kv in _index)
            if (kv.Value.grantedTraits != null && kv.Value.grantedTraits.Contains(trait))
                list.Add(kv.Value);
        return list;
    }

    public List<CardStateProto> GetCardsByTemplate(string templateID)
    {
        var list = new List<CardStateProto>();
        foreach (var kv in _index)
            if (kv.Value.templateID == templateID)
                list.Add(kv.Value);
        return list;
    }
}
