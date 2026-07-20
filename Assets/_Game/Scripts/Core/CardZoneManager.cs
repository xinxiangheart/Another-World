using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// ============================================================================
// CardZoneManager — 统一四区牌堆管理（Deck / Hand / Graveyard / Board / Exile）
// ============================================================================
// 替代分散的 DeckManager + GraveyardManager + handCards scattered lists。
//
// 核心原则：
//   1. 每张卡从加入牌库起就有全局唯一 instanceID（6 位时间戳 + 3 位序号）。
//   2. 所有区的数据都在 CardZoneManager 中追踪，不依赖 GameObject（GameObject 销毁
//      后数据仍保留在 Graveyard / Exile 中）。
//   3. 服务端权威：Deck/Graveyard/Exile 只在服务端存在，客户端只追踪 Hand/Board。
//   4. 牌堆为空时触发 OnDeckEmpty → 调用方可洗入弃牌堆重组牌库。
// ============================================================================

public enum CardZone { Deck, Hand, Board, Graveyard, Exile }

[Serializable]
public class DeckCard
{
    public string templateID;
    public string instanceID;         // 全局唯一
}

public class CardZoneManager : MonoBehaviour
{
    public static CardZoneManager Instance { get; private set; }

    // ===== 各区存储 =====
    List<DeckCard> _deck = new List<DeckCard>();           // 牌库（仅服务端）
    List<GraveEntry> _graveyard = new List<GraveEntry>();  // 弃牌堆（仅服务端）
    List<string> _exile = new List<string>();              // 放逐区（仅服务端，只存 instanceID）

    /// <summary>已追踪的 instanceID 集合（防碰撞）</summary>
    HashSet<string> _allInstanceIDs = new HashSet<string>();

    /// <summary>全局序号（instanceID 后缀）</summary>
    static int _globalSeq;

    // ===== 事件 =====
    /// <summary>instanceID 从 from 移到 to</summary>
    public event Action<string, CardZone, CardZone> OnCardZoneChanged;
    /// <summary>牌库为空时触发</summary>
    public event Action OnDeckEmpty;
    /// <summary>instanceID 进入弃牌堆</summary>
    public event Action<string> OnCardEnteredGraveyard;

    public int DeckCount => _deck.Count;
    public int GraveyardCount => _graveyard.Count;
    public int ExileCount => _exile.Count;
    public bool IsDeckEmpty => _deck.Count == 0;

    // ═══════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 默认行为：牌库空时自动从弃牌堆重组
        OnDeckEmpty += () =>
        {
            if (_graveyard.Count > 0)
            {
                ShuffleGraveyardIntoDeck();
                Debug.Log("[CardZoneManager] 牌库空 → 弃牌堆重组完成");
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // instanceID 生成
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>生成全局唯一 instanceID。格式：TTTTT + 6位时间戳 + 3位序号。</summary>
    public static string GenerateInstanceID(string templateID)
    {
        // 6 位时间戳（秒末 6 位，覆盖约 27 小时）
        int ts = (int)(Time.time * 10f) % 1000000;
        int seq = System.Threading.Interlocked.Increment(ref _globalSeq) % 1000;
        return $"{templateID}{ts:D6}{seq:D3}";
    }

    /// <summary>注册 instanceID 到全局追踪集。</summary>
    public void RegisterInstanceID(string iid)
    {
        if (!string.IsNullOrEmpty(iid))
            _allInstanceIDs.Add(iid);
    }

    /// <summary>instanceID 是否已存在（用于手动指定时防碰撞）。</summary>
    public bool HasInstanceID(string iid) => _allInstanceIDs.Contains(iid);

    // ═══════════════════════════════════════════════════════════════════
    // 牌库初始化
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>从 Resources/CardData 构建牌库。仅在服务端调用。</summary>
    public void InitializeDeck()
    {
        if (!NetworkServer.active && NetworkClient.isConnected) return;

        _deck.Clear();
        _graveyard.Clear();
        _exile.Clear();
        _allInstanceIDs.Clear();

        CardData[] allCards = Resources.LoadAll<CardData>("CardData");
        foreach (var template in allCards)
        {
            if (!template.addToMainDeck) continue;
            for (int i = 0; i < template.copyCount; i++)
            {
                string iid = GenerateInstanceID(template.templateID);
                _deck.Add(new DeckCard { templateID = template.templateID, instanceID = iid });
                _allInstanceIDs.Add(iid);
            }
        }

        Shuffle(_deck);
        Debug.Log($"[CardZoneManager] 牌库初始化完成: {_deck.Count} 张卡");
    }

    /// <summary>Fisher-Yates 洗牌。</summary>
    public static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 核心 API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>从牌库抽一张卡（返回 templateID + instanceID）。牌库空返回 null。</summary>
    public (string templateID, string instanceID)? DrawFromDeck()
    {
        if (_deck.Count == 0)
        {
            OnDeckEmpty?.Invoke();
            if (_deck.Count == 0) return null;
        }
        var card = _deck[0];
        _deck.RemoveAt(0);
        OnCardZoneChanged?.Invoke(card.instanceID, CardZone.Deck, CardZone.Hand);
        return (card.templateID, card.instanceID);
    }

    /// <summary>牌库顶 N 张的 templateID（查看，不移出）。</summary>
    public List<string> PeekTopN(int n)
    {
        var result = new List<string>();
        for (int i = 0; i < _deck.Count && i < n; i++)
            result.Add(_deck[i].templateID);
        return result;
    }

    /// <summary>从牌库中移除指定 instanceID（放逐等）。</summary>
    public bool RemoveFromDeck(string instanceID)
    {
        int idx = _deck.FindIndex(c => c.instanceID == instanceID);
        if (idx < 0) return false;
        _deck.RemoveAt(idx);
        return true;
    }

    /// <summary>从牌库中移除首个匹配 templateID 的卡（GetCardPanel 调试用），返回移除的 instanceID。</summary>
    public bool RemoveFromDeckByTemplateID(string templateID, out string instanceID)
    {
        instanceID = null;
        int idx = _deck.FindIndex(c => c.templateID == templateID);
        if (idx < 0) return false;
        instanceID = _deck[idx].instanceID;
        _deck.RemoveAt(idx);
        return true;
    }

    /// <summary>将一组卡洗入牌库（弃牌堆重组、牌库底回收等）。</summary>
    public void ShuffleIntoDeck(List<(string templateID, string instanceID)> cards)
    {
        foreach (var (tid, iid) in cards)
            _deck.Add(new DeckCard { templateID = tid, instanceID = iid });
        Shuffle(_deck);
    }

    /// <summary>将 GraveEntry 列表洗入牌库。</summary>
    public void ShuffleGraveyardIntoDeck()
    {
        foreach (var ge in _graveyard)
            _deck.Add(new DeckCard { templateID = ge.templateID, instanceID = ge.instanceID });
        _graveyard.Clear();
        Shuffle(_deck);
        Debug.Log($"[CardZoneManager] 弃牌堆洗入牌库: {_deck.Count} 张");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 弃牌堆
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>添加进弃牌堆。</summary>
    public void AddToGraveyard(GraveEntry entry)
    {
        if (entry == null) return;
        _graveyard.Add(entry);
        OnCardEnteredGraveyard?.Invoke(entry.instanceID);
        OnCardZoneChanged?.Invoke(entry.instanceID, CardZone.Board, CardZone.Graveyard);
        Debug.Log($"[CardZoneManager] 进入弃牌堆: {entry.templateID} ({entry.instanceID}), 共 {_graveyard.Count} 张");
    }

    /// <summary>从弃牌堆取出（移出）。</summary>
    public GraveEntry RemoveFromGraveyard(string instanceID)
    {
        for (int i = _graveyard.Count - 1; i >= 0; i--)
        {
            if (_graveyard[i].instanceID == instanceID)
            {
                var entry = _graveyard[i];
                _graveyard.RemoveAt(i);
                return entry;
            }
        }
        return null;
    }

    /// <summary>弃牌堆是否含有指定 templateID。</summary>
    public bool GraveyardContainsTemplate(string templateID)
    {
        return _graveyard.Exists(e => e.templateID == templateID);
    }

    /// <summary>获取弃牌堆中上阶段死亡的所有牌。</summary>
    public List<GraveEntry> GetGraveEntriesFromPhase(int phase)
    {
        return _graveyard.FindAll(e => e.deathPhase == phase);
    }

    public List<GraveEntry> AllGraveEntries => new List<GraveEntry>(_graveyard);

    // ═══════════════════════════════════════════════════════════════════
    // 放逐区
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>放逐一张卡（从任何区到 Exile）。</summary>
    public void ExileCard(string instanceID, CardZone from)
    {
        _exile.Add(instanceID);
        OnCardZoneChanged?.Invoke(instanceID, from, CardZone.Exile);
    }

    /// <summary>instanceID 是否已被放逐。</summary>
    public bool IsExiled(string instanceID) => _exile.Contains(instanceID);

    // ═══════════════════════════════════════════════════════════════════
    // 通用
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>按 instanceID 查弃牌堆中的 GraveEntry。</summary>
    public GraveEntry FindGraveByInstanceID(string instanceID)
    {
        return _graveyard.Find(e => e.instanceID == instanceID);
    }

    /// <summary>某区的卡数量。Deck/Graveyard/Exile 直接查，Hand/Board 需要外部注入。</summary>
    public int GetCount(CardZone zone) => zone switch
    {
        CardZone.Deck => _deck.Count,
        CardZone.Graveyard => _graveyard.Count,
        CardZone.Exile => _exile.Count,
        _ => 0
    };

    // ═══════════════════════════════════════════════════════════════════
    // 旧 API 兼容
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>兼容旧代码：只返回 templateID（不返回 instanceID）。</summary>
    [System.Obsolete]
    public CardData DrawFromMain_Legacy()
    {
        var result = DrawFromDeck();
        if (result == null) return null;
        return CardDatabase.Instance?.GetTemplate(result.Value.templateID);
    }
}
