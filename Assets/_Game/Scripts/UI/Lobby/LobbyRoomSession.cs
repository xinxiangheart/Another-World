using UnityEngine;
using Steamworks;

/// <summary>
/// 「房间」面板的 Steam 大厅接入：真房间号（6 位 · 查重后发布）、客人槽、踢出 / 开打两个信号。
/// </summary>
/// <remarks>2026-09-27 用户：「接入」（把 <see cref="LobbyRoomCodeTag.SetCode"/> 接到新房间面板上，含查重 +
/// Steam 未登录时的灰态）。网络那套沿用仓库里旧壳（<see cref="CreateRoomPanel"/> / <see cref="JoinRoomPanel"/>）
/// 已经在跑的口径，只是换成新面板来驱动：
///   • 关键 key 全同旧壳 —— 大厅数据 <c>game=anotherworld_room</c> / <c>room_code</c> / <c>host_data</c> /
///     <c>start</c> / <c>host_sid</c> / <c>kicked</c>，成员数据 <c>player_data</c>，值都是同一份 JSON。
///     所以旧壳的客人端（按 room_code 搜、读 host_data、看 start / kicked）和新面板**能互通**。
///   • 号还是 6 位、字母表同 <see cref="LobbyRoomCodeTag"/>（剔掉 0 O 1 I）；旧壳是 6 位纯数字（90 万种），
///     这里用 36 个字符 = 22 亿种，撞号概率可忽略 —— 但仍然**先查重再建房**。
///   • **查重必须在建房之前**：<c>RequestLobbyList</c> 不允许在「已经处在某个大厅里」时调用。
///     所以顺序是：请求列表（按 room_code 过滤）→ 没人用 → 才 CreateLobby。
///   • 查重没回调（网络卡住）时 3.5 秒超时，直接用当前候选号建房 —— 宁可极小概率撞号，也不能让房间号一直不出现。
///   • Steam 未登录 / 未连接（<c>SteamManager.Initialized &amp;&amp; SteamUser.BLoggedOn()</c> 为假）：
///     房间号那行进灰态（<see cref="LobbyRoomPanel.ApplySteamOffline"/>），建房整个跳过（沿用匹配 / 排位那条规矩）。
/// </remarks>
public class LobbyRoomSession : MonoBehaviour
{
    public static LobbyRoomSession Instance { get; private set; }

    /// <summary>号字母表：与 <see cref="LobbyRoomCodeTag"/> 同一份（剔掉 0 O 1 I）。</summary>
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    const int    CodeLen     = 6;
    const int    MaxTries    = 6;      // 查重最多换 6 次号
    const float  ListTimeout = 3.5f;   // 查重没回调的兜底
    const float  PollStep    = 0.5f;   // 房主读客人数据的步长（Steam 的 Get 依赖本地缓存）

    LobbyRoomPanel _room;
    CSteamID _lobby = CSteamID.Nil;
    string   _code;
    bool     _ready;
    bool     _hosting;
    bool     _searching;
    string   _candidate;
    int      _tries;
    float    _listT0;
    float    _poll;
    bool     _kickedPublished;

    Callback<LobbyCreated_t>   _created;
    Callback<LobbyMatchList_t> _list;

    public bool     Ready   { get { return _ready; } }
    public bool     Hosting { get { return _hosting; } }
    public string   Code    { get { return _code; } }
    public CSteamID LobbyID { get { return _lobby; } }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; DisposeCallbacks(); }

    void DisposeCallbacks()
    {
        _created?.Dispose(); _created = null;
        _list?.Dispose();    _list = null;
    }

    /// <summary>Steam 在线 = 已初始化 + 已登录后端（离线 / 无网 / 被墙都为假）。</summary>
    public static bool SteamOnline()
    {
        try { return SteamManager.Initialized && SteamUser.BLoggedOn(); }
        catch { return false; }
    }

    public static string NewCode()
    {
        var sb = new System.Text.StringBuilder(CodeLen);
        for (int i = 0; i < CodeLen; i++) sb.Append(Alphabet[Random.Range(0, Alphabet.Length)]);
        return sb.ToString();
    }

    // ===================== 房主：开面板就建房 =====================

    /// <summary>房间面板第一次打开时调（<see cref="LobbyRoomPanel.OnEnable"/>）。</summary>
    public void BeginHosting(LobbyRoomPanel room)
    {
        _room = room;
        // 面板重开：真号还在，直接回填（Tag 那边收到显式号就不再自己生成占位）
        if (_code != null && _room != null) _room.ApplyRealCode(_code);
        if (_hosting || _searching) return;      // 已经在房里——重复开面板不重来

        _ready = SteamOnline();
        if (!_ready)
        {
            Debug.LogWarning("[RoomSession] Steam 未登录 / 未连接 —— 房间号进灰态，建房跳过");
            if (_room != null) _room.ApplySteamOffline();
            return;
        }
        _tries = 0;
        PickCode();
    }

    void PickCode()
    {
        _tries++;
        _candidate = NewCode();
        _searching = true;
        _listT0 = Time.realtimeSinceStartup;
        _list?.Dispose();
        _list = Callback<LobbyMatchList_t>.Create(OnLobbyList);
        SteamMatchmaking.AddRequestLobbyListStringFilter("room_code", _candidate, ELobbyComparison.k_ELobbyComparisonEqual);
        // 显式世界范围：默认只返回同数据中心的大厅，号主在别处就查不到（旧壳同款注释）
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.RequestLobbyList();
        Debug.Log("[RoomSession] 查重候选号 " + _candidate + "（第 " + _tries + " 次）");
    }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        if (!_searching) return;
        _searching = false;
        if (cb.m_nLobbiesMatching > 0 && _tries < MaxTries)
        {
            Debug.Log("[RoomSession] 号 " + _candidate + " 已被占用 —— 换一个再查");
            PickCode();
            return;
        }
        _code = _candidate;
        CreateLobby();
    }

    void CreateLobby()
    {
        _created?.Dispose();
        _created = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (cb.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("[RoomSession] 建房失败 result=" + cb.m_eResult);
            if (cb.m_ulSteamIDLobby != 0) SteamMatchmaking.LeaveLobby(new CSteamID(cb.m_ulSteamIDLobby));
            _ready = false;
            if (_room != null) _room.ApplySteamOffline("Steam 连接失败，请检查网络 / 加速器");
            return;
        }

        _lobby = new CSteamID(cb.m_ulSteamIDLobby);
        _hosting = true;
        SteamMatchmaking.SetLobbyData(_lobby, "game", "anotherworld_room");
        SteamMatchmaking.SetLobbyData(_lobby, "room_code", _code);       // ← 号从这一刻起是「真号」
        string myJson = MyJson();
        SteamMatchmaking.SetLobbyData(_lobby, "host_data", myJson);
        SteamMatchmaking.SetLobbyMemberData(_lobby, "player_data", myJson);
        if (_room != null) _room.ApplyRealCode(_code);
        Debug.Log("[RoomSession] 房间已建：号=" + _code + " lobby=" + _lobby.m_SteamID);
    }

    // ===================== 每帧：超时兜底 + 读客人 =====================

    void Update()
    {
        if (_searching && Time.realtimeSinceStartup - _listT0 > ListTimeout)
        {
            Debug.LogWarning("[RoomSession] 查重超时 —— 直接用候选号 " + _candidate + " 建房");
            _searching = false;
            _code = _candidate;
            CreateLobby();
        }

        if (!_hosting || _lobby.m_SteamID == 0) return;

        _poll += Time.deltaTime;
        if (_poll < PollStep) return;
        _poll = 0f;
        SteamMatchmaking.RequestLobbyData(_lobby);      // 不刷新缓存会一直读到空数据（旧壳同款）
        PollGuest();
    }

    /// <summary>房主侧：大厅里除了自己还有谁 → 填 / 清客人槽。</summary>
    void PollGuest()
    {
        int count = SteamMatchmaking.GetNumLobbyMembers(_lobby);
        string guestName = null;
        ulong  guestId   = 0;

        for (int i = 0; i < count; i++)
        {
            CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i);
            if (m == SteamUser.GetSteamID()) continue;
            string js = SteamMatchmaking.GetLobbyMemberData(_lobby, m, "player_data");
            guestId = m.m_SteamID;
            if (string.IsNullOrEmpty(js)) { guestName = ""; break; }   // 人进来了、数据还没到（下一轮再填）
            var d = JsonUtility.FromJson<PlayerData>(js);
            if (d != null)
            {
                if (!string.IsNullOrEmpty(d.playerName)) guestName = d.playerName;
                if (d.steamID != 0) guestId = d.steamID;
            }
            if (string.IsNullOrEmpty(guestName)) guestName = "玩家";
            break;
        }

        if (_room == null) return;

        if (guestName == null)          // 房里只剩自己
        {
            if (_kickedPublished)
            {
                SteamMatchmaking.SetLobbyData(_lobby, "kicked", "0");   // 踢出标记用完清掉
                _kickedPublished = false;
            }
            if (_room.HasGuest) { Debug.Log("[RoomSession] 客人已离开大厅"); _room.OnGuestLeft(); }
            return;
        }

        if (!_room.HasGuest && !string.IsNullOrEmpty(guestName))
        {
            _room.SetGuest(guestName, SteamAvatarManager.GetAvatarTexture(guestId), guestId);
            Debug.Log("[RoomSession] 玩家加入房间：" + guestName + " (" + guestId + ")");
        }
    }

    // ===================== 三个信号 =====================

    /// <summary>踢出：给大厅打标记（客人端读到 <c>kicked=1</c> 自己走 —— 旧壳同款 key），客人槽由面板清。</summary>
    public void PublishKick()
    {
        if (!_hosting) return;
        SteamMatchmaking.SetLobbyData(_lobby, "kicked", "1");
        _kickedPublished = true;
        Debug.Log("[RoomSession] 已发布 kicked=1");
    }

    /// <summary>双方确认、真要开打：发布 <c>start</c> + <c>host_sid</c>，并把 LobbyConfig 填成旧壳进 Game 那套。</summary>
    public void PublishStart()
    {
        if (!_hosting) return;
        ulong myId = SteamUser.GetSteamID().m_SteamID;
        SteamMatchmaking.SetLobbyData(_lobby, "start", "1");
        SteamMatchmaking.SetLobbyData(_lobby, "host_sid", myId.ToString());

        LobbyConfig.EnterMultiplayer();
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = true;
        LobbyConfig.IsDirectIP = false;
        LobbyConfig.ServerIP = "";
        LobbyConfig.CurrentLobbyID = _lobby;
        LobbyConfig.MatchKey = "aw_" + _lobby.m_SteamID;
        LobbyConfig.HostSteamID = myId.ToString();
        foreach (var kv in GuestIds()) LobbyConfig.RemoteSteamID = kv;   // 客人 SteamID = 对手（进 Game 用头像）
        Debug.Log("[RoomSession] 已发布 start=1（客人侧读到就和匹配那条一样进 Game）");
    }

    System.Collections.Generic.IEnumerable<ulong> GuestIds()
    {
        if (_lobby.m_SteamID == 0) yield break;
        int count = SteamMatchmaking.GetNumLobbyMembers(_lobby);
        for (int i = 0; i < count; i++)
        {
            CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i);
            if (m == SteamUser.GetSteamID()) continue;
            string js = SteamMatchmaking.GetLobbyMemberData(_lobby, m, "player_data");
            var d = string.IsNullOrEmpty(js) ? null : JsonUtility.FromJson<PlayerData>(js);
            yield return (d != null && d.steamID != 0) ? d.steamID : m.m_SteamID;
        }
    }

    /// <summary>离开大厅（房主关面板 / 房间解散）。</summary>
    public void StopHosting()
    {
        if (_lobby.m_SteamID != 0)
        {
            SteamMatchmaking.LeaveLobby(_lobby);
            Debug.Log("[RoomSession] 已离开大厅 " + _lobby.m_SteamID);
        }
        _lobby = CSteamID.Nil;
        _hosting = false;
        _searching = false;
        _kickedPublished = false;
        _code = null;          // 房间解散 → 号作废，下次开面板重新查重
        DisposeCallbacks();
    }

    /// <summary>给联机侧（客人入口）留的接缝：按 6 位号搜大厅 → 命中就 JoinLobby。</summary>
    /// <remarks>客人那侧的面板还没做（「加入房间」现在只弹占位窗），所以这里只留方法不接线 ——
    /// 搜 / 进的写法与旧壳 <see cref="JoinRoomPanel"/> 完全一致（字符串过滤 room_code + 世界范围）。</remarks>
    public bool JoinByCode(string code, System.Action<bool, string> done = null)
    {
        if (!SteamOnline() || string.IsNullOrEmpty(code)) { done?.Invoke(false, "Steam 未登录 / 未连接"); return false; }
        _list?.Dispose();
        _list = Callback<LobbyMatchList_t>.Create(cb =>
        {
            if (cb.m_nLobbiesMatching == 0) { done?.Invoke(false, "没找到这个房间号"); return; }
            CSteamID lid = SteamMatchmaking.GetLobbyByIndex(0);
            if (SteamMatchmaking.GetNumLobbyMembers(lid) >= 2) { done?.Invoke(false, "房间已满"); return; }
            SteamMatchmaking.JoinLobby(lid);
            done?.Invoke(true, SteamMatchmaking.GetLobbyData(lid, "room_code"));
        });
        SteamMatchmaking.AddRequestLobbyListStringFilter("room_code", code, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.RequestLobbyList();
        return true;
    }

    // ===================== 自己的那份数据 =====================

    string MyJson()
    {
        var sd = SteamDataManager.Instance;
        var d  = sd?.playerData;
        return JsonUtility.ToJson(new PlayerData
        {
            playerName   = sd != null && !string.IsNullOrEmpty(sd.localPlayerName) ? sd.localPlayerName : "玩家",
            totalMatches = d?.totalMatches ?? 0,
            winRate      = sd?.WinRate ?? 0,
            winStreak    = d?.winStreak ?? 0,
            steamID      = sd?.localSteamID.m_SteamID ?? 0
        });
    }

    [System.Serializable] class PlayerData
    {
        public string playerName;
        public int    totalMatches;
        public double winRate;
        public int    winStreak;
        public ulong  steamID;
    }
}
