using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 卡牌效果层统一查询入口 —— 透明处理服务端/客户端差异。
/// 服务端直接读 Registry；客户端从本地 mirror 读；
/// 若客户端数据为空则异步请求服务器（当前版本仅日志警告）。
/// 不修改任何现有代码路径，仅提供新 API。
///
/// 状态：就绪待接入。RegistrySyncManager 自举实例化后 Instance 永远非 null，
/// 所有方法均可正常工作。当前无调用方——新卡牌效果（如窥探对手手牌）可直接使用。
/// </summary>
public static class CardQuery
{
    /// <summary>获取对手手牌（CardStateProto 列表）。</summary>
    public static List<CardStateProto> GetOpponentHand()
    {
        var mgr = RegistrySyncManager.Instance;
        if (mgr != null) return mgr.GetOpponentHand();
        Debug.LogWarning("[CardQuery] RegistrySyncManager.Instance 为空");
        return new List<CardStateProto>();
    }

    /// <summary>获取对手板面卡牌。</summary>
    public static List<CardStateProto> GetOpponentBoard()
    {
        var mgr = RegistrySyncManager.Instance;
        if (mgr != null) return mgr.GetOpponentBoard();
        Debug.LogWarning("[CardQuery] RegistrySyncManager.Instance 为空");
        return new List<CardStateProto>();
    }

    /// <summary>对手手牌数量。</summary>
    public static int GetOpponentHandCount()
    {
        var cards = GetOpponentHand();
        return cards.Count;
    }

    /// <summary>对手牌库剩余数量。</summary>
    public static int GetOpponentDeckCount()
    {
        if (NetworkPlayer.Remote != null)
        {
            var mgr = RegistrySyncManager.Instance;
            if (mgr != null && NetworkServer.active)
                return mgr.GetOpponentHand().Count >= 0 ? CardZoneManager.Instance.DeckCount : 0;
        }
        return CardZoneManager.Instance != null ? CardZoneManager.Instance.DeckCount : 0;
    }

    /// <summary>按前缀搜索对手手牌。</summary>
    public static List<CardStateProto> FindOpponentHandByPrefix(string prefix)
    {
        var cards = GetOpponentHand();
        var result = new List<CardStateProto>();
        foreach (var c in cards)
            if (c.prefixes != null && c.prefixes.Contains(prefix))
                result.Add(c);
        return result;
    }

    /// <summary>对手血量。</summary>
    public static int GetOpponentHealth()
    {
        return NetworkPlayer.Remote != null ? NetworkPlayer.Remote.currentHealth : 0;
    }

    /// <summary>对手能量。</summary>
    public static int GetOpponentEnergy()
    {
        return NetworkPlayer.Remote != null ? NetworkPlayer.Remote.currentEnergy : 0;
    }
}
