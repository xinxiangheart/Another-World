using System;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;
using UnityEngine;

/// <summary>
/// 「好友申请」的网络通道 —— 申请列表那条**真来源**（Steam P2P，<c>SteamNetworkingMessages</c>）。
/// </summary>
/// <remarks>
/// **来由**：用户 2026-09-27 问「申请列表的真来源（后端收包）具体怎么改」→「改吧」。
///
/// **为什么不用后端**：异界号（<see cref="PlayerId"/>）是 A 方案 —— Steam 账号的 7 个 Base32 符号
/// **直接编进了 ID**，拿到 ID 的人在本机就能解出对方 SteamID64（<see cref="PlayerId.ResolveSteamId"/>）。
/// 所以「按 ID 发一条好友申请」是一件**点对点**的事，Steam 的 SDR 就是那条路，不需要任何服务器。
/// **办不到的那半**：按**昵称**全服搜人 —— 那要一份玩家目录（Steam Web API / 自建后端），与本类无关，
/// 分界线仍在 <see cref="FriendAddSearch.KnownPlayers"/>。
///
/// 协议：一条 UTF-8 JSON，<see cref="MaxPayload"/> 字节以内 ——
/// <code>{ "t":"fr", "v":1, "id":"P00-20260927-4-K3M79QX-8", "n":"昵称" }</code>
/// 收到之后**只做一件事**：调 <see cref="FriendRequestStore.Add(string, string, ulong)"/>。
/// 那条路自己会 Save + 通知 <c>Changed</c>，界面自动重排（申请那份从三十三次修正起订阅了它）——
/// **UI 一行都不用改**。
///
/// **收包要过五道**（顺序即 <see cref="HandlePayload"/> 里的顺序，任何一道不过就整包丢掉）：
///   ① 包本身：非空、能解 JSON、<c>t</c> 与 <c>v</c> 都对 —— 不是我们的包直接丢；
///   ② 异界号合法：<see cref="PlayerId.TryParse"/> 解得出版 SteamID64 —— 解不出的号一律不认；
///   ③ **和发件人对得上**：发件人的 SteamID64 是 **Steam 认证过的**（P2P 会话给的），包里那个 ID
///      只是他自己写的；两者不符 = 伪造 / 串号，丢；
///   ④ 不是自己；
///   ⑤ 没被拉黑（<see cref="FriendBlock.IsHidden"/>）—— 与搜索那三处、同意那一步同一口径。
///
/// **通道号 <see cref="Channel"/> = 41**：这是 SteamNetworkingMessages 的「目的端口」。它和战斗联机那套
/// （Mirror / FizzySteamworks 走 SteamSockets）**不是同一条路**，互不干扰。
/// </remarks>
public static class FriendRequestChannel
{
    /// <summary>协议版本（<c>v</c>）。改包结构就 +1，老包会被 ① 挡掉。</summary>
    public const int Protocol = 1;

    /// <summary>包类型（<c>t</c>）。</summary>
    public const string TypeRequest = "fr";

    /// <summary>通道号 —— SteamNetworkingMessages 的「目的端口」。收发两侧必须一样。</summary>
    public const int Channel = 41;

    /// <summary>一个包最大多少字节（申请就这么点东西，超了肯定是别的包走错门）。</summary>
    public const int MaxPayload = 512;

    /// <summary>一帧最多收几条（防止某一帧被刷爆）。</summary>
    const int MaxBatch = 16;

    [Serializable]
    class Packet
    {
        public string t = TypeRequest;   // 类型
        public int    v = Protocol;      // 协议号
        public string id = "";           // 申请人的异界号
        public string n  = "";           // 申请人的昵称
    }

    static readonly IntPtr[] _inbox = new IntPtr[MaxBatch];
    static bool   _relayAsked;
    static string _lastReason = "";

    /// <summary>最近一次收发是成是败（日志与自证用）。</summary>
    public static string LastReason { get { return _lastReason; } }

    /// <summary>现在能不能走这条路发（Steam 起来了，且不是直连模式）。</summary>
    public static bool CanSend { get { return SteamManager.Initialized && !LobbyConfig.IsDirectIP; } }

    /// <summary>我自己的异界号（没生成就是空串）。</summary>
    public static string MyPlayerId()
    {
        return SteamDataManager.Instance != null ? (SteamDataManager.Instance.PlayerIdText ?? "") : "";
    }

    /// <summary>我的显示名 —— 跟着对方申请列表里那行。</summary>
    public static string MyName()
    {
        if (SteamDataManager.Instance != null && !string.IsNullOrEmpty(SteamDataManager.Instance.localPlayerName))
            return SteamDataManager.Instance.localPlayerName;
        return "异界玩家";
    }

    // ── 发 ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 给「异界号 = targetPlayerId」的那个人发一条好友申请。返回 false 时 <paramref name="reason"/> 里是原因。
    /// </summary>
    /// <remarks>失败**不影响**本地已经加上的结果 —— 调用方（<see cref="FriendAddSearch.TryAdd"/>）
    /// 只记一行日志、不改返回值：这条路的定位是「顺手通知对方一声」。</remarks>
    public static bool Send(string targetPlayerId, string myName, out string reason)
    {
        reason = "";
        if (!SteamManager.Initialized) { reason = "没连上 Steam"; return false; }
        if (LobbyConfig.IsDirectIP)    { reason = "直连模式不发申请"; return false; }

        ulong target = PlayerId.ResolveSteamId(targetPlayerId);
        if (target == 0UL) { reason = "这个 ID 解不出 Steam 账号"; return false; }

        ulong self = LobbyConfig.LocalSteamID;
        if (self != 0UL && target == self) { reason = "不能加自己"; return false; }

        string me = MyPlayerId();
        if (string.IsNullOrEmpty(me)) { reason = "自己的异界号还没生成"; return false; }

        byte[] bytes = Encoding.UTF8.GetBytes(Encode(me, myName));
        if (bytes.Length > MaxPayload) { reason = "包太大（" + bytes.Length + " 字节）"; return false; }

        EnsureRelay();

        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(target);

        GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            EResult r = SteamNetworkingMessages.SendMessageToUser(
                ref identity, pinned.AddrOfPinnedObject(), (uint)bytes.Length,
                Constants.k_nSteamNetworkingSend_Reliable, Channel);
            if (r != EResult.k_EResultOK)
            {
                reason = "Steam 没收下这条（" + r + "）";
                Debug.LogWarning("[FriendRequest] 发给 SteamID " + target + " 失败：" + r);
                return false;
            }
        }
        catch (Exception e)
        {
            reason = "发送时抛异常：" + e.Message;
            Debug.LogWarning("[FriendRequest] " + reason);
            return false;
        }
        finally { pinned.Free(); }

        _lastReason = "已发出";
        Debug.Log("[FriendRequest] 已向 SteamID " + target + " 发出一条好友申请");
        return true;
    }

    /// <summary>起一次 SDR 中继（越早越好，不然第一次收发要找几秒路）。只试一次。</summary>
    static void EnsureRelay()
    {
        if (_relayAsked) return;
        _relayAsked = true;
        try { SteamNetworkingUtils.InitRelayNetworkAccess(); }
        catch (Exception e) { Debug.LogWarning("[FriendRequest] InitRelayNetworkAccess 失败：" + e.Message); }
    }

    // ── 收 ───────────────────────────────────────────────────────────────────

    /// <summary>把这一帧到的好友申请包全收下来（<see cref="FriendRequestPump"/> 每帧调一次）。</summary>
    public static void Pump()
    {
        if (!SteamManager.Initialized) return;

        int n;
        try { n = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, _inbox, MaxBatch); }
        catch (Exception e) { Debug.LogWarning("[FriendRequest] 收包失败：" + e.Message); return; }

        for (int i = 0; i < n; i++)
        {
            IntPtr raw = _inbox[i];
            _inbox[i] = IntPtr.Zero;
            if (raw == IntPtr.Zero) continue;

            try
            {
                SteamNetworkingMessage_t msg = SteamNetworkingMessage_t.FromIntPtr(raw);
                if (msg.m_cbSize <= 0 || msg.m_cbSize > MaxPayload || msg.m_pData == IntPtr.Zero) continue;

                byte[] bytes = new byte[msg.m_cbSize];
                Marshal.Copy(msg.m_pData, bytes, 0, msg.m_cbSize);

                string why;
                HandlePayload(Encoding.UTF8.GetString(bytes), msg.m_identityPeer.GetSteamID64(), out why);
            }
            catch (Exception e) { Debug.LogWarning("[FriendRequest] 解包失败：" + e.Message); }
            finally { try { SteamNetworkingMessage_t.Release(raw); } catch { } }
        }
    }

    /// <summary>拼一条申请包（测试直接调它造包）。</summary>
    public static string Encode(string playerId, string name)
    {
        var p = new Packet();
        p.t = TypeRequest;
        p.v = Protocol;
        p.id = PlayerId.Pretty(playerId);
        p.n = name ?? "";
        return JsonUtility.ToJson(p);
    }

    /// <summary>
    /// 处理一条已经到手的申请包 —— **和网络无关**，测试可以拿 <see cref="Encode"/> 造一条直接喂进来。
    /// senderSteamId = Steam 认证过的发件人（真收包时从 P2P 会话拿；测试自己给）。
    /// </summary>
    public static bool HandlePayload(string json, ulong senderSteamId, out string reason)
    {
        if (string.IsNullOrEmpty(json)) return Drop("空包", out reason);

        Packet p;
        try { p = JsonUtility.FromJson<Packet>(json); }
        catch (Exception e) { return Drop("不是合法的 JSON：" + e.Message, out reason); }

        if (p == null || p.t != TypeRequest) return Drop("不是好友申请包", out reason);
        if (p.v != Protocol)                 return Drop("协议号不符（收到 " + p.v + "）", out reason);

        int season; DateTime created; ulong bound;
        if (!PlayerId.TryParse(p.id, out season, out created, out bound))
            return Drop("异界号不合法：" + (p.id ?? ""), out reason);

        if (senderSteamId != 0UL && bound != senderSteamId)
            return Drop("ID 与发件人对不上（伪造 / 串号），丢", out reason);

        ulong self = LobbyConfig.LocalSteamID;
        if (self != 0UL && bound == self) return Drop("是自己发的，丢", out reason);

        if (FriendBlock.IsHidden(FriendStore.Load(), p.id, bound)) return Drop("加不了这个人，丢", out reason);

        bool added = FriendRequestStore.Add(PlayerId.Pretty(p.id), p.n, bound);
        reason = added ? "收到一条好友申请" : "重复的申请（只刷新了时间）";
        _lastReason = reason;
        return true;
    }

    static bool Drop(string why, out string reason)
    {
        reason = why;
        _lastReason = why;
        return false;
    }
}

/// <summary>
/// 「好友申请」的收包泵：每帧 <see cref="FriendRequestChannel.Pump"/>，并替 Steam 收下对方的 P2P 会话。
/// </summary>
/// <remarks>
/// **为什么要接受会话**：SteamNetworkingMessages 在对方第一次发消息时会先抛一个「会话请求」
/// （<c>SteamNetworkingMessagesSessionRequest_t</c>），不 <c>AcceptSessionWithUser</c> 就收不到包。
///
/// **为什么自己装自己**：<c>[RuntimeInitializeOnLoadMethod]</c> 造一个隐藏物体 —— 和 <c>SteamManager</c>
/// 一个套路。这样**不用碰 Lobby.unity**（场景是构建期生成的，手改会被覆盖）。
/// </remarks>
public class FriendRequestPump : MonoBehaviour
{
    static FriendRequestPump _instance;
    Callback<SteamNetworkingMessagesSessionRequest_t> _sessionRequest;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_instance != null) return;
        var go = new GameObject("FriendRequestPump");
        _instance = go.AddComponent<FriendRequestPump>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Update()
    {
        if (!SteamManager.Initialized) return;

        // 会话回调要等 Steam 起来才注册得上（晚注册只是晚一点，不影响收包）
        if (_sessionRequest == null)
        {
            try { _sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest); }
            catch (Exception e) { Debug.LogWarning("[FriendRequest] 会话回调注册失败：" + e.Message); }
        }

        FriendRequestChannel.Pump();
    }

    void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t req)
    {
        try { SteamNetworkingMessages.AcceptSessionWithUser(ref req.m_identityRemote); }
        catch (Exception e) { Debug.LogWarning("[FriendRequest] 接受会话失败：" + e.Message); }
    }
}
