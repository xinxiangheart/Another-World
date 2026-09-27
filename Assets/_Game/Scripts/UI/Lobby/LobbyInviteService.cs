using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>
/// 好友邀请的收发（2026-09-27）。
/// </summary>
/// <remarks>
/// 用户 2026-09-27：「好友栏中处于在线（非战斗状态和匹配状态）时其名字右边会出现一个加号，点击后发送邀请
/// 并且加号变成 10 秒倒计时（倒计时结束后才能继续邀请）（若自己此时不是在房间界面就进入房间并开房间）」。
///
/// **发送**：走 Steam 大厅那套 —— 先确保自己在房间里（<see cref="EnsureRoom"/>：不在就打开房间面板 ⇒
/// 面板的 OnSubPanelOpened 会建房），大厅一建好就 <c>SteamMatchmaking.InviteUserToLobby</c>。
/// 人群等在大厅建好之前（建房是异步的：查重 → CreateLobby → LobbyCreated），所以邀请先排进
/// <see cref="_queue"/>，由 Update 每帧试发一次。**冷却从点下加号那一刻起算**（用户原话：「点击后发送邀请
/// 并且加号变成 10 秒倒计时」）—— 顺带也挡住了建房那零点几秒里的连点。
///
/// **接收**：对面发来的是 <c>LobbyInvite_t</c>（对面点加号发出、Steam 投递到我们），转给
/// <see cref="LobbyInvitePanel"/> 出「收到邀请」小窗。
///
/// **回信（「对方暂无法响应」）**：仓库里没有 P2P 通道（SteamNetworking* 只有 autogen 壳、没有业务代码），
/// 而 Steam 的大厅数据**只有成员写得进去**（被邀的人不是成员，写不了邀请方的大厅）。所以这条回信走
/// **rich presence**：拒绝方把 <c>aw_invite_reply = "&lt;邀请者ID&gt;|decline|&lt;unix秒&gt;"</c> 写在自己的
/// presence 上（自己写自己，永远合法），邀请方按 <see cref="replyPollStep"/> 去读那几位好友的
/// <c>GetFriendRichPresence</c>（互为好友才读得到）。带上 unix 秒是为了幂等：读到一条比自己这次发出时间
/// 更早的、或发给别人的，一律不算。**这一条是占位级通道**，日后真接 P2P（SteamNetworkingMessages）时
/// 换掉 <see cref="PublishDecline"/> / <see cref="PollReplies"/> 两处即可，别处不用动。
/// </remarks>
public class LobbyInviteService : MonoBehaviour
{
    public static LobbyInviteService Instance { get; private set; }

    /// <summary>回信用的 rich presence 键（与 <see cref="SteamPresence"/> 的 status 键分开，互不覆盖）。</summary>
    public const string ReplyKey = "aw_invite_reply";
    public const string ReplyDecline = "decline";

    [Header("每个好友的邀请冷却（秒）—— 用户：「加号变成 10 秒倒计时」")]
    public float cooldownSeconds = 10f;

    [Header("等回信的轮询步长 / 最长等多久（秒）")]
    public float replyPollStep = 1.5f;
    public float replyGiveUpSeconds = 180f;

    // 冷却：key = 好友 SteamID，value = 到期时刻（Time.unscaledTime）
    readonly Dictionary<ulong, float> _coolUntil = new Dictionary<ulong, float>();
    // 已经排上、等大厅建好就发的邀请
    readonly List<ulong> _queue = new List<ulong>();
    // 已经发出去的邀请：key = 好友 SteamID，value = 发出时刻（unscaledTime / unix 秒各一份）
    readonly Dictionary<ulong, float> _sentAt = new Dictionary<ulong, float>();
    readonly Dictionary<ulong, long> _sentUnix = new Dictionary<ulong, long>();
    readonly List<ulong> _scratch = new List<ulong>();

    Callback<LobbyInvite_t> _inviteCb;
    float _poll;

    void Awake()
    {
        Instance = this;
        if (SteamManager.Initialized)
        {
            try { _inviteCb = Callback<LobbyInvite_t>.Create(OnInviteReceived); }
            catch (System.Exception e) { Debug.LogWarning("[Invite] 订阅 LobbyInvite_t 失败：" + e.Message); }
        }
        else
        {
            Debug.Log("[Invite] Steam 未初始化 —— 收不到大厅邀请（发送侧也会被 SteamOnline 挡住）");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_inviteCb != null) { _inviteCb.Dispose(); _inviteCb = null; }
    }

    void Update()
    {
        FlushQueue();
        PollReplies();
    }

    // ===================== 冷却（好友行那个 10 秒倒计时读它） =====================

    /// <summary>还能邀吗（冷却走完 = true）。</summary>
    public bool CanInvite(ulong steamId)
    {
        return steamId != 0UL && CooldownLeft(steamId) <= 0f;
    }

    /// <summary>冷却还剩几秒（0 = 可以邀）。静态入口：好友行每帧读，不用先拿到服务实例。</summary>
    public static float CooldownLeft(ulong steamId)
    {
        LobbyInviteService s = Instance;
        if (s == null || steamId == 0UL) return 0f;

        float until;
        if (!s._coolUntil.TryGetValue(steamId, out until)) return 0f;
        float left = until - Time.unscaledTime;
        if (left <= 0f) { s._coolUntil.Remove(steamId); return 0f; }
        return left;
    }

    // ===================== 发邀请 =====================

    /// <summary>点加号。返回 true = 真的开始发（冷却已起算）。</summary>
    public bool Invite(ulong steamId, string name)
    {
        if (steamId == 0UL)
        {
            LobbyToast.Show("没有对方的 Steam 身份，暂时邀不了");
            return false;
        }
        if (!LobbyRoomSession.SteamOnline())
        {
            LobbyToast.Show("Steam 未登录 / 未连接，暂时邀不了");
            return false;
        }
        if (CooldownLeft(steamId) > 0f) return false;      // 保险：冷却没走完不再发（连点 / 脚本调用都挡）

        _coolUntil[steamId] = Time.unscaledTime + Mathf.Max(1f, cooldownSeconds);
        EnsureRoom();
        _queue.Add(steamId);
        Debug.Log("[Invite] 邀请「" + name + "」(" + steamId + ") —— 冷却 " + cooldownSeconds + "s");
        return true;
    }

    /// <summary>「若自己此时不是在房间界面就进入房间并开房间」—— 打开房间面板，建房由面板那条 OnSubPanelOpened 走。</summary>
    void EnsureRoom()
    {
        LobbyRoomPanel room = LobbyRoomPanel.Instance;
        if (room == null) { LobbyToast.Show("场景里没有房间面板，开不了房"); return; }

        LobbySubPanel shell = room.shell;
        if (shell == null) shell = room.GetComponent<LobbySubPanel>();
        if (shell == null) { LobbyToast.Show("房间面板没有壳，开不了房"); return; }

        if (!shell.IsOpen) shell.Open(null);               // Open → ILobbySubPanelOpen → BeginHosting（真建房）
    }

    /// <summary>大厅建好之前先排队；一旦自己是房主且有真大厅，就把队里的邀请发出去。</summary>
    void FlushQueue()
    {
        if (_queue.Count == 0) return;

        LobbyRoomSession s = LobbyRoomSession.Instance;
        if (s == null || !s.Hosting || s.LobbyID.m_SteamID == 0UL) return;    // 还在建房，等下一帧
        if (!LobbyRoomSession.SteamOnline()) { _queue.Clear(); return; }

        long nowUnix = UnixNow();
        for (int i = 0; i < _queue.Count; i++)
        {
            ulong id = _queue[i];
            bool ok = false;
            try { ok = SteamMatchmaking.InviteUserToLobby(s.LobbyID, new CSteamID(id)); }
            catch (System.Exception e) { Debug.LogWarning("[Invite] InviteUserToLobby 抛了：" + e.Message); }

            if (ok)
            {
                _sentAt[id] = Time.unscaledTime;
                _sentUnix[id] = nowUnix;
                LobbyToast.Show("已发出邀请，等对方响应");
            }
            else
            {
                LobbyToast.Show("邀请发送失败（对方可能不在线）");
            }
            Debug.Log("[Invite] InviteUserToLobby -> " + ok + "  lobby=" + s.LobbyID.m_SteamID + "  to=" + id);
        }
        _queue.Clear();
    }

    // ===================== 收邀请 =====================

    void OnInviteReceived(LobbyInvite_t cb)
    {
        ulong from = cb.m_ulSteamIDUser;
        if (from == 0UL || cb.m_ulSteamIDLobby == 0UL) return;

        string name = null;
        try { name = SteamFriends.GetFriendPersonaName(new CSteamID(from)); } catch { }

        Debug.Log("[Invite] 收到邀请：from=" + from + "(" + name + ")  lobby=" + cb.m_ulSteamIDLobby);

        LobbyInvitePanel panel = LobbyInvitePanel.Instance;
        if (panel == null) { Debug.LogWarning("[Invite] 场景里没有 LobbyInvitePanel —— 邀请没能弹出来"); return; }
        panel.Show(from, name, cb.m_ulSteamIDLobby);
    }

    // ===================== 回信 =====================

    /// <summary>拒绝方回一句「暂无法响应」（写在自己的 rich presence 上）。</summary>
    public void PublishDecline(ulong inviterId)
    {
        if (inviterId == 0UL) return;
        if (!SteamManager.Initialized) { Debug.Log("[Invite] Steam 未初始化 —— 回信发不出去"); return; }
        try
        {
            string v = inviterId + "|" + ReplyDecline + "|" + UnixNow();
            SteamFriends.SetRichPresence(ReplyKey, v);
            Debug.Log("[Invite] 已回信给 " + inviterId + "：" + ReplyDecline);
        }
        catch (System.Exception e) { Debug.LogWarning("[Invite] 写回信失败：" + e.Message); }
    }

    /// <summary>邀请方：读那几位被邀好友的 presence，读到给自己的拒绝就弹「对方暂无法响应」。</summary>
    void PollReplies()
    {
        if (_sentAt.Count == 0 || !LobbyRoomSession.SteamOnline()) return;

        _poll += Time.unscaledDeltaTime;
        if (_poll < Mathf.Max(0.5f, replyPollStep)) return;
        _poll = 0f;

        ulong me = SteamUser.GetSteamID().m_SteamID;
        _scratch.Clear();
        foreach (KeyValuePair<ulong, float> kv in _sentAt) _scratch.Add(kv.Key);

        for (int i = 0; i < _scratch.Count; i++)
        {
            ulong id = _scratch[i];

            float sentAt;
            if (!_sentAt.TryGetValue(id, out sentAt)) continue;
            if (Time.unscaledTime - sentAt > Mathf.Max(10f, replyGiveUpSeconds))
            {
                _sentAt.Remove(id); _sentUnix.Remove(id);
                continue;
            }

            string raw = null;
            try
            {
                CSteamID fid = new CSteamID(id);
                SteamFriends.RequestFriendRichPresence(fid);       // 不 request 可能读到的是旧缓存
                raw = SteamFriends.GetFriendRichPresence(fid, ReplyKey);
            }
            catch (System.Exception e) { Debug.LogWarning("[Invite] 读回信失败：" + e.Message); continue; }
            if (string.IsNullOrEmpty(raw)) continue;

            string[] parts = raw.Split('|');
            if (parts.Length < 3) continue;

            ulong to;
            if (!ulong.TryParse(parts[0], out to) || to != me) continue;      // 不是回给我的
            if (parts[1] != ReplyDecline) continue;
            long unix;
            if (!long.TryParse(parts[2], out unix)) continue;

            long mine;
            if (_sentUnix.TryGetValue(id, out mine) && unix < mine) continue;  // 比这次邀请还早 → 上一次的旧回信

            _sentAt.Remove(id); _sentUnix.Remove(id);
            Debug.Log("[Invite] " + id + " 回了「暂无法响应」");
            LobbyToast.Show("对方暂无法响应");
        }
    }

    static long UnixNow()
    {
        return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
