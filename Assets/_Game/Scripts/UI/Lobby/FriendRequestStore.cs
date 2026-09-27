using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 收到的好友申请（别人申请加**我**）：落盘在 `Application.persistentDataPath/friend_requests.json`。
/// </summary>
/// <remarks>
/// **用户 2026-09-27**：「申请列表和黑名单一起做，展示申请加好友的列表（均在右下角类似限制50）」。
///
/// 数据就是一行 <see cref="FriendEntry"/> —— 申请人和好友是同一个形状（头像 / 昵称 / 异界号 / 在线状态），
/// 行渲染因此能和好友列表共用一套列宽。**不另立一个模型**是有意的：
/// 同意之后这一行原样进好友表（<see cref="FriendStore.AddManual"/>），少一层搬运。
///
/// **改造位**：现在没有任何网络路径能产生申请（后端 / Steam 还没接），所以这个文件平时是空的 ——
/// 界面那格会显示「暂无好友申请」。接上之后由**收包那一步**调 <see cref="Add(string, string, ulong)"/>，
/// 别的地方都不用改。要看版式可以用 `Tools/异界/调试：塞一条好友申请` 塞假数据。
///
/// 上限 <see cref="MaxRequests"/> = 50（用户：「类似限制50」）：满了**挤掉最旧的一条**（FIFO），
/// 与好友表「满了就不让加」不同 —— 申请是别人发过来的，我们没法拒收，只能滚掉最旧的。
/// </remarks>
public static class FriendRequestStore
{
    /// <summary>申请列表上限（用户：「均在右下角类似限制50」）。</summary>
    public const int MaxRequests = 50;

    [Serializable]
    class Container
    {
        public int version = 1;
        public List<FriendEntry> requests = new List<FriendEntry>();
    }

    /// <summary>申请那份名单**变了**（加了一条 / 删了一条）。UI 订阅它重排。</summary>
    /// <remarks>为什么申请这份也要事件（以前只靠切进那一格时重建）：现在有两处会**从外面**改它 ——
    /// 拉黑顺手清申请（<see cref="FriendBlock.SetBlocked"/>），以及以后**后端收包**那一步。
    /// 没有事件的话，玩家正看着申请列表时进来一条新申请是**不会显示的**。</remarks>
    public static event Action Changed;

    /// <summary>域重载关掉时静态事件会跨 play 会话残留，这里按 Unity 惯例清一遍。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Changed = null; }

    static void NotifyChanged() { if (Changed != null) Changed(); }

    static string FilePath { get { return Path.Combine(Application.persistentDataPath, "friend_requests.json"); } }

    public static List<FriendEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<FriendEntry>();
            var c = JsonUtility.FromJson<Container>(File.ReadAllText(FilePath));
            return c != null && c.requests != null ? c.requests : new List<FriendEntry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FriendRequestStore] friend_requests.json 读取失败：" + e.Message);
            return new List<FriendEntry>();
        }
    }

    public static void Save(List<FriendEntry> list)
    {
        try
        {
            var c = new Container();
            c.requests = list != null ? list : new List<FriendEntry>();
            File.WriteAllText(FilePath, JsonUtility.ToJson(c, true));
        }
        catch (Exception e)
        {
            Debug.LogError("[FriendRequestStore] friend_requests.json 保存失败：" + e.Message);
        }
    }

    /// <summary>读-改-写一条（收包那条路以后直接调这个）。重复申请只保留最新一条。</summary>
    public static bool Add(string playerId, string name, ulong steamId)
    {
        List<FriendEntry> list = Load();
        bool changed = Add(list, playerId, name, steamId);
        if (changed) { Save(list); NotifyChanged(); }
        return changed;
    }

    /// <summary>只改内存里的表，落盘由调用方做。返回 false = 同一人已经在申请里（只刷新时间戳，不算新增）。</summary>
    public static bool Add(List<FriendEntry> list, string playerId, string name, ulong steamId)
    {
        if (list == null) return false;
        if (string.IsNullOrEmpty(playerId) && steamId == 0UL) return false;

        for (int i = 0; i < list.Count; i++)
        {
            FriendEntry e = list[i];
            if (e == null) continue;
            bool same = (!string.IsNullOrEmpty(playerId) && e.playerId == playerId)
                        || (steamId != 0UL && e.SteamId == steamId);
            if (!same) continue;
            if (!string.IsNullOrEmpty(name) && e.name != name) e.name = name;
            e.lastSeenUnix = NowUnix();
            return false;
        }

        var rec = new FriendEntry();
        rec.playerId = playerId;
        rec.name = name;
        if (steamId != 0UL) { rec.SteamId = steamId; rec.steamFriend = true; }
        rec.lastSeenUnix = NowUnix();
        list.Add(rec);

        while (list.Count > MaxRequests) list.RemoveAt(0);   // 满了滚掉最旧的（FIFO）
        return true;
    }

    /// <summary>按异界号或 SteamID 文本删一条（同意 / 拒绝之后调）。**只改内存**。</summary>
    public static bool Remove(List<FriendEntry> list, string key)
    {
        if (list == null || string.IsNullOrEmpty(key)) return false;
        for (int i = 0; i < list.Count; i++)
        {
            FriendEntry e = list[i];
            if (e == null) continue;
            if (e.playerId == key || e.steamIdText == key) { list.RemoveAt(i); return true; }
        }
        return false;
    }

    /// <summary>按一条记录删（同意 / 拒绝那行拿到的就是它）。</summary>
    public static bool RemoveEntry(FriendEntry e)
    {
        if (e == null) return false;
        List<FriendEntry> list = Load();
        string key = !string.IsNullOrEmpty(e.playerId) ? e.playerId : e.steamIdText;
        bool ok = Remove(list, key);
        if (ok) { Save(list); NotifyChanged(); }
        return ok;
    }

    /// <summary>
    /// 按下手的那个人清掉他的申请 —— 拉黑时调（<see cref="FriendBlock.SetBlocked"/>）。
    /// 异界号优先、再退到 SteamID 文本：申请那条记录可能只带了其中一样。
    /// 拉黑之后这条申请**没有意义**，而且留着会出事 —— 见 FriendBlock 里那段注释。
    /// </summary>
    public static bool RemoveByPlayer(string playerId, ulong steamId)
    {
        List<FriendEntry> list = Load();
        bool ok = false;
        if (!string.IsNullOrEmpty(playerId)) ok |= Remove(list, playerId);
        if (steamId != 0UL) ok |= Remove(list, steamId.ToString());
        if (ok) { Save(list); NotifyChanged(); }
        return ok;
    }

    public static int Count() { return Load().Count; }

    static long NowUnix() { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
}
