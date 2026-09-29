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
    bool     _oppDeclineSeen;               // 房间确认弹窗：对面那格的「拒绝」只处理一次

    // ── 客人侧（「加入房间」侧边栏，2026-09-27）──────────────────────────────
    bool     _suspended;                    // 搜号期间把自己的大厅让出去了（号还留着）
    CSteamID _guestLobby = CSteamID.Nil;    // 已经进了别人的房 = 客人
    float    _guestT;                       // 客人侧轮询计时
    bool     _startSeen;                    // 读到 start=1 只放行一次
    bool     _confirmSeen;                  // 读到 confirm=1（房主点「开始游戏」）只放行一次
    System.Action<LobbySearchResult> _findDone;
    Callback<LobbyMatchList_t> _find;
    Callback<LobbyEnter_t>     _enter;

    Callback<LobbyCreated_t>   _created;
    Callback<LobbyMatchList_t> _list;

    public bool     Ready   { get { return _ready; } }
    public bool     Hosting { get { return _hosting; } }
    public string   Code    { get { return _code; } }
    public CSteamID LobbyID { get { return _lobby; } }
    public bool     IsGuest { get { return _guestLobby.m_SteamID != 0; } }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; DisposeCallbacks(); }

    void DisposeCallbacks()
    {
        _created?.Dispose(); _created = null;
        _list?.Dispose();    _list = null;
        _find?.Dispose();    _find = null;
        _enter?.Dispose();   _enter = null;
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

        PollTick();
    }

    /// <summary>房间壳关着时替这一拍接着跑（确认弹窗调它）。</summary>
    /// <remarks>2026-09-29 用户报「即使对方点了确认，在自己视角里对方仍是待确认状态」的根因：
    /// 房主点「开始游戏」/ 客人读到 <c>confirm=1</c> 都会先 <c>LobbySubPanel.Close()</c>，而那个壳和本组件挂在
    /// <b>同一个物体</b>上 ⇒ 整个物体 SetActive(false) ⇒ 下面那个 <see cref="Update"/> 不再跑
    /// ⇒ 对面那格（<c>confirm_host_ok</c> / <c>confirm_guest_ok</c>）永远读不回来，两边各自 15 秒超时。
    /// 现在由确认弹窗（它自己常驻 active）每帧调这个接手。</remarks>
    public void PollDetached()
    {
        if (isActiveAndEnabled) return;     // 自己那份 Update 还在跑就别抢（免得一拍跑两次）
        PollTick();
    }

    /// <summary>一拍：房主读客人那格 + 客人读房主那几条，两边都在这儿。</summary>
    void PollTick()
    {
        if (_hosting && _lobby.m_SteamID != 0)
        {
            _poll += Time.deltaTime;
            if (_poll >= PollStep)
            {
                _poll = 0f;
                SteamMatchmaking.RequestLobbyData(_lobby);      // 不刷新缓存会一直读到空数据（旧壳同款）
                PollGuest();
                PollRoomConfirm();                              // 确认弹窗开着时读客人那格（confirm_guest_ok）
            }
        }

        PollGuestSide();        // 客人侧：读房主那几条信号（kicked / confirm / start）
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

    /// <summary>房间确认弹窗：把对面那格读回弹窗 —— 对面确认就解压黑，对面拒绝就一起回房间。</summary>
    /// <remarks>2026-09-27 二次修：匹配那条路这套是 <see cref="QuickMatchPanel"/> 在轮询（它只在 <c>State.Found</c>
    /// 里跑，房间那条不经过它）—— 房间这条得自己读。不读的后果就是「房主点了确认，客人那边一直压着黑」，
    /// 两边各自 15 秒超时回房间。
    /// 三值口径见 <see cref="ConfirmValue"/>：<c>0</c> 未表态 / <c>1</c> 确认 / <c>2</c> 拒绝。</remarks>
    void PollRoomConfirm()
    {
        var cp = MatchConfirmPanel.Instance;
        if (cp == null || !cp.IsOpen || _room == null || cp.roomSource != _room) return;

        string opp;
        if (_hosting && _lobby.m_SteamID != 0)          // 房主：客人那格在**成员数据**里
        {
            opp = "";
            int count = SteamMatchmaking.GetNumLobbyMembers(_lobby);
            for (int i = 0; i < count; i++)
            {
                CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i);
                if (m == SteamUser.GetSteamID()) continue;
                opp = SteamMatchmaking.GetLobbyMemberData(_lobby, m, "confirm_guest_ok");
                break;
            }
        }
        else if (IsGuest)                               // 客人：房主那格在**大厅数据**里
        {
            opp = SteamMatchmaking.GetLobbyData(_guestLobby, "confirm_host_ok");
        }
        else return;

        int d = ConfirmDecision(opp);
        if (d == 1) cp.NotifyOpponentAccepted();
        else if (d == 2 && !_oppDeclineSeen) { _oppDeclineSeen = true; cp.OpponentDeclined(); }
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

    // ── 房间「开始游戏」那条：一个通知 + 一张三值的确认表 ────────────────────────────
    //    2026-09-27 二次修（用户报「房主点击开始后客人不会进入确认界面而是仍卡在房间界面」）：
    //    这套原来一个都没有 —— 房主点了开始，客人侧读不到任何东西，就永远卡在房间界面；
    //    而房主自己那一下「确认」也没写给对面（房间这条路不经过 QuickMatchPanel）⇒ 两边各自 15 秒超时回房间。

    /// <summary>房主点了「开始游戏」→ 通知客人也进确认弹窗（大厅数据 <c>confirm=1</c>）。</summary>
    /// <remarks>⚠ 不是 <c>start</c> —— 那个是「双方已确认、真要开打」（<see cref="PublishStart"/>），
    /// 提前发客人会跳过确认直接进加载。</remarks>
    public void PublishConfirm()
    {
        if (!_hosting) return;
        SteamMatchmaking.SetLobbyData(_lobby, "confirm", "1");
        SteamMatchmaking.SetLobbyData(_lobby, "confirm_host_ok", "0");   // 本轮从「未表态」开始（上一轮的 1 / 2 不能留着）
        _oppDeclineSeen = false;
        Debug.Log("[RoomSession] 已发布 confirm=1（客人侧读到就进确认弹窗）");
    }

    /// <summary>房间确认格的三值：<c>0</c> 未表态 / <c>1</c> 确认 / <c>2</c> 拒绝。</summary>
    /// <remarks>为什么不用 <c>host_ok</c> / <c>guest_ok</c> 那对：那对是**匹配**大厅的口径，只有 0 / 1 两值，
    /// 「没写过」和「写 0」分不开（见 <see cref="ConfirmDecision"/> 的注释）。抽成纯函数是为了能直接喂参数自证。</remarks>
    public static string ConfirmValue(bool ok) { return ok ? "1" : "2"; }

    /// <summary>对面那格读到什么 → 确认弹窗该怎么动：<c>0</c> 什么都不做 / <c>1</c> 解压黑 / <c>2</c> 一起回房间。</summary>
    /// <remarks>空的（键还没写过）= 什么都不做 —— 这是「未表态」和「拒绝」必须分开的原因：
    /// 一开始双方都是「没写过」，要是把空当成拒绝，房主刚点开始就把两个人踢回房间。</remarks>
    public static int ConfirmDecision(string opp)
    {
        if (opp == "1") return 1;
        if (opp == "2") return 2;
        return 0;
    }

    /// <summary>确认弹窗里自己那一下（确认 / 拒绝）→ 写给对面看的那一格。</summary>
    /// <remarks>房主写大厅数据、客人写成员数据（跟 <see cref="PollRoomConfirm"/> 的读法一一对应）。</remarks>
    public void PublishConfirmAccept(bool ok)
    {
        string v = ConfirmValue(ok);
        if (_hosting) SteamMatchmaking.SetLobbyData(_lobby, "confirm_host_ok", v);
        else if (IsGuest) SteamMatchmaking.SetLobbyMemberData(_guestLobby, "confirm_guest_ok", v);
        Debug.Log("[RoomSession] 确认弹窗：自己那格写成 " + v);
    }

    /// <summary>回到房间等下一轮（拒绝 / 超时）→ 把「读到过什么」清掉，否则下一轮同样的信号会被当成旧的忽略掉。</summary>
    public void ResetConfirmWatch()
    {
        _confirmSeen = false;
        _oppDeclineSeen = false;
    }

    /// <summary>双方确认后进战斗加载界面那一步 —— 把 <c>LobbyConfig</c> 按自己这一侧填好，房主额外发布 <c>start=1</c>。</summary>
    /// <remarks>2026-09-27 二次修：原来只有房主在 <see cref="PublishStart"/> 里填配置，客人侧是读到 <c>start</c> 才填 ——
    /// 而客人的确认弹窗金色 3 秒走完会**先**开加载界面（<c>BattleLoadingScreen.FillContent()</c> 读的就是这份配置），
    /// 客人那半区可能读到上一局的对手。现在两侧都在这里填。</remarks>
    public void ConfirmBattleEntry()
    {
        if (_hosting) { PublishStart(); return; }
        if (IsGuest) FillGuestConfig();
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
        _suspended = false;
        _confirmSeen = false;
        _oppDeclineSeen = false;
        DisposeCallbacks();
    }

    // ===================== 客人：「加入房间」侧边栏那套 =====================

    /// <summary>按 6 位号搜一间房（**不进去**）—— 只把房主 / 人数读回来给侧边栏出预览。</summary>
    /// <remarks>搜之前先 <see cref="SuspendHosting"/>：RequestLobbyList 不允许在「已经处于某个大厅」时调用
    /// （自己开着房时搜号必然搜不到）。</remarks>
    public bool SearchByCode(string code, System.Action<LobbySearchResult> done)
    {
        code = string.IsNullOrEmpty(code) ? "" : code.Trim().ToUpperInvariant();
        if (!SteamOnline()) { done?.Invoke(Fail("Steam 未登录 / 未连接")); return false; }
        if (code.Length != CodeLen) { done?.Invoke(Fail("房间号是 " + CodeLen + " 位")); return false; }
        if (IsGuest) { done?.Invoke(Fail("你已经在别人的房间里了")); return false; }

        SuspendHosting();
        _find?.Dispose();
        _findDone = done;
        _find = Callback<LobbyMatchList_t>.Create(OnFindList);
        SteamMatchmaking.AddRequestLobbyListStringFilter("room_code", code, ELobbyComparison.k_ELobbyComparisonEqual);
        // 显式世界范围：默认只返回同数据中心的大厅，号主在别处就搜不到（旧壳同款注释）
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.RequestLobbyList();
        Debug.Log("[RoomSession] 搜房间号 " + code);
        return true;
    }

    void OnFindList(LobbyMatchList_t cb)
    {
        var done = _findDone;
        _findDone = null;
        if (cb.m_nLobbiesMatching == 0) { done?.Invoke(Fail("没找到这个房间号")); return; }

        CSteamID lid = SteamMatchmaking.GetLobbyByIndex(0);
        var r = new LobbySearchResult
        {
            found = true,
            lobby = lid,
            members = SteamMatchmaking.GetNumLobbyMembers(lid),
            capacity = 2,
            hostId = SteamMatchmaking.GetLobbyOwner(lid).m_SteamID,
        };
        string js = SteamMatchmaking.GetLobbyData(lid, "host_data");
        if (!string.IsNullOrEmpty(js))
        {
            var d = JsonUtility.FromJson<PlayerData>(js);
            if (d != null)
            {
                if (!string.IsNullOrEmpty(d.playerName)) r.hostName = d.playerName;
                if (d.steamID != 0) r.hostId = d.steamID;
            }
        }
        if (string.IsNullOrEmpty(r.hostName) && r.hostId != 0)
        {
            string n = SteamFriends.GetFriendPersonaName(new CSteamID(r.hostId));
            if (!string.IsNullOrEmpty(n)) r.hostName = n;
        }
        Debug.Log("[RoomSession] 搜到房间 " + lid.m_SteamID + "  房主=" + r.hostName + "(" + r.hostId + ")  人数=" + r.members + "/" + r.capacity);
        done?.Invoke(r);
    }

    static LobbySearchResult Fail(string msg)
    {
        return new LobbySearchResult { found = false, message = msg };
    }

    /// <summary>真进那间房：JoinLobby → 写自己的成员数据（客人只能写 member data）→ 房间面板切客人视角。</summary>
    public bool JoinFound(CSteamID lobby, System.Action<bool, string> done = null)
    {
        if (!SteamOnline()) { done?.Invoke(false, "Steam 未登录 / 未连接"); return false; }
        if (lobby.m_SteamID == 0) { done?.Invoke(false, "没找到这个房间号"); return false; }

        _enter?.Dispose();
        _enter = Callback<LobbyEnter_t>.Create(cb =>
        {
            if (cb.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.LogWarning("[RoomSession] 进大厅失败 response=" + cb.m_EChatRoomEnterResponse);
                done?.Invoke(false, "进不去这间房（可能刚被解散 / 已满）");
                return;
            }
            _guestLobby = new CSteamID(cb.m_ulSteamIDLobby);
            _suspended = false;
            _startSeen = false;
            _confirmSeen = false;
            _oppDeclineSeen = false;
            _guestT = 0f;
            SteamMatchmaking.SetLobbyMemberData(_guestLobby, "player_data", MyJson());
            SteamMatchmaking.RequestLobbyData(_guestLobby);

            ulong hostId = SteamMatchmaking.GetLobbyOwner(_guestLobby).m_SteamID;
            string hostName = HostNameOf(_guestLobby, hostId);
            string code = SteamMatchmaking.GetLobbyData(_guestLobby, "room_code");
            if (_room != null) _room.ApplyGuestLobby(hostName, SteamAvatarManager.GetAvatarTexture(hostId), hostId, code);
            Debug.Log("[RoomSession] 已进别人的房 lobby=" + _guestLobby.m_SteamID + "  房主=" + hostName + "  号=" + code);
            done?.Invoke(true, "");
        });
        SteamMatchmaking.JoinLobby(lobby);
        return true;
    }

    /// <summary>
    /// 客人视角：从「收到邀请」点同意进来 —— 直接进对方那间大厅（大厅 ID 是邀请回调给的，不用再按号搜）。
    /// </summary>
    /// <remarks>2026-09-27：与 <see cref="JoinFound"/> 的两点不同 ——
    /// ① 面板可能一次都没开过（从好友栏直接点进来），所以 <c>_room</c> 得由调用方显式交进来
    ///   （<see cref="ApplyGuestLobby"/> 要它才把两个槽换成对方的房）；
    /// ② 自己可能正开着房（点过房间 / 刚邀过别人），Steam 同时只能在一个大厅 ⇒ 先让位再进
    ///   （<see cref="StopHosting"/> 会 LeaveLobby 并清掉自己的号）。</remarks>
    public bool JoinInviteAsGuest(CSteamID lobby, LobbyRoomPanel room, System.Action<bool, string> done = null)
    {
        if (room != null) _room = room;
        if (!SteamOnline()) { done?.Invoke(false, "Steam 未登录 / 未连接"); return false; }
        if (lobby.m_SteamID == 0) { done?.Invoke(false, "这张邀请已经失效了"); return false; }

        if (IsGuest) LeaveGuestLobby();     // 已经在别人的房里换一间
        if (_hosting) StopHosting();        // 自己开着房 → 让位（同时只能在一个大厅里）

        Debug.Log("[RoomSession] 收到邀请 → 进对方的大厅 " + lobby.m_SteamID);
        return JoinFound(lobby, done);
    }

    static string HostNameOf(CSteamID lobby, ulong hostId)
    {
        string js = SteamMatchmaking.GetLobbyData(lobby, "host_data");
        if (!string.IsNullOrEmpty(js))
        {
            var d = JsonUtility.FromJson<PlayerData>(js);
            if (d != null && !string.IsNullOrEmpty(d.playerName)) return d.playerName;
        }
        if (hostId != 0)
        {
            string n = SteamFriends.GetFriendPersonaName(new CSteamID(hostId));
            if (!string.IsNullOrEmpty(n)) return n;
        }
        return "玩家";
    }

    /// <summary>搜号期间先把自己的大厅让出来（号留着）。关掉侧边栏用同一个号重建 —— 屏幕上的号不会变。</summary>
    public void SuspendHosting()
    {
        if (!_hosting || _lobby.m_SteamID == 0) return;
        SteamMatchmaking.LeaveLobby(_lobby);
        _hosting = false;
        _suspended = true;
        _kickedPublished = false;
        Debug.Log("[RoomSession] 搜号期间先离开自己的大厅（号 " + _code + " 留着，收板再重建）");
    }

    /// <summary>侧边栏收起来了 → 用**同一个号**把大厅重建回来（不重查重：那个号刚由自己释放）。</summary>
    public void ResumeHosting()
    {
        if (!_suspended) return;
        _suspended = false;
        if (IsGuest || string.IsNullOrEmpty(_code) || _room == null || !_room.gameObject.activeInHierarchy) return;
        CreateLobby();
        Debug.Log("[RoomSession] 侧边栏收起 —— 用同一个号 " + _code + " 重建大厅");
    }

    /// <summary>客人侧：离开别人的房，然后重新开自己的房（新号：查重 + 建房）。</summary>
    public void RestartHosting()
    {
        LeaveGuestLobby();
        _code = null;
        _hosting = false;
        _suspended = false;
        if (_room != null && _room.gameObject.activeInHierarchy) BeginHosting(_room);
    }

    /// <summary>离开别人的房（自己被踢 / 客人点叉）。</summary>
    public void LeaveGuestLobby()
    {
        if (_guestLobby.m_SteamID != 0)
        {
            SteamMatchmaking.LeaveLobby(_guestLobby);
            Debug.Log("[RoomSession] 已离开别人的房 " + _guestLobby.m_SteamID);
        }
        _guestLobby = CSteamID.Nil;
        _startSeen = false;
        _confirmSeen = false;
        _oppDeclineSeen = false;
        _code = null;          // 刚才是别人的房：自己那个旧号（搜号前 SuspendHosting 留下的）也一并作废
        _suspended = false;
        _enter?.Dispose();
        _enter = null;
    }

    /// <summary>客人侧每帧：读房主那三条信号（kicked / confirm / start）。</summary>
    void PollGuestSide()
    {
        if (_hosting) return;
        if (_guestLobby.m_SteamID == 0) return;

        _guestT += Time.deltaTime;
        if (_guestT < PollStep) return;
        _guestT = 0f;
        SteamMatchmaking.RequestLobbyData(_guestLobby);      // 不刷新缓存会一直读到空数据（旧壳同款）

        if (SteamMatchmaking.GetLobbyData(_guestLobby, "kicked") == "1")
        {
            if (_room != null) _room.OnKickedByHost();
            return;
        }
        if (!_confirmSeen && SteamMatchmaking.GetLobbyData(_guestLobby, "confirm") == "1")
        {
            _confirmSeen = true;
            _oppDeclineSeen = false;
            SteamMatchmaking.SetLobbyMemberData(_guestLobby, "confirm_guest_ok", "0");   // 本轮从「未表态」开始
            if (_room != null) _room.OnRemoteConfirm();
        }
        if (!_startSeen && SteamMatchmaking.GetLobbyData(_guestLobby, "start") == "1")
        {
            _startSeen = true;
            FillGuestConfig();
            if (_room != null) _room.OnRemoteStart();
        }

        PollRoomConfirm();          // 确认弹窗开着时读房主那格（confirm_host_ok）
    }

    /// <summary>客人进 Game 那套配置（旧壳 CreateRoomPanel 的客人分支同款：IsHost=false、对手 = 房主）。</summary>
    void FillGuestConfig()
    {
        ulong hostId = SteamMatchmaking.GetLobbyOwner(_guestLobby).m_SteamID;
        LobbyConfig.EnterMultiplayer();          // 清掉上一次 AI 对战留下的 IsAI
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = false;
        LobbyConfig.IsDirectIP = false;
        LobbyConfig.ServerIP = "";
        LobbyConfig.CurrentLobbyID = _guestLobby;
        LobbyConfig.MatchKey = "aw_" + _guestLobby.m_SteamID;
        LobbyConfig.HostSteamID = hostId.ToString();
        LobbyConfig.RemoteSteamID = hostId;      // 房主 = 对手（进 Game 用头像）
        Debug.Log("[RoomSession] 客人侧看到 start=1 → 已填进 Game 那套配置（对手 = 房主 " + hostId + "）");
    }

    /// <summary>搜号结果（侧边栏那份预览数据）。</summary>
    public class LobbySearchResult
    {
        public bool      found;
        public string    message;
        public CSteamID  lobby = CSteamID.Nil;
        public ulong     hostId;
        public string    hostName;
        public int       members;
        public int       capacity = 2;
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
