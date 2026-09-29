using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

public class QuickMatchPanel : MonoBehaviour
{
    public static QuickMatchPanel Instance { get; private set; }
    public string opponentName => _oppName;
    public Texture opponentTexture => opponentAvatar != null ? opponentAvatar.texture : null;

    // 对手战绩（2026-09-27 加：加载界面要展示对方的总场次 / 胜率 / 连胜）——
    // 由 RefreshOpponent() 解析到的那份 QMPD 存下来；opponentStatsKnown=false 时界面显示占位。
    public bool opponentStatsKnown => _oppStatsKnown;
    public int opponentTotalMatches => _oppTotal;
    public double opponentWinRate => _oppWinRate;
    public int opponentWinStreak => _oppStreak;

    [Header("面板")] public GameObject panelRoot;
    [Header("状态")] public TMP_Text statusText;
    [Header("对手")] public GameObject opponentInfoGroup;
    public RawImage opponentAvatar;
    public TMP_Text opponentNameText, opponentStatsText;
    [Header("按钮")] public Button acceptButton, declineButton, cancelButton;

    [Header("紧凑等待窗（2026-09-27）：填了就用它，本面板自带的测试 UI 不再显示")]
    [Tooltip("屏幕顶中那块「匹配中：x：xx + 取消」的小窗（MatchWaitPanel）。填上之后 Open() 只开它，不再显示 panelRoot。")]
    public MatchWaitPanel compactWait;
    [Tooltip("找到对手后「已找到对手！」停留多久再进 JoinGamePanel（留一个节拍，否则同帧就被盖住）。")]
    public float foundHoldSeconds = 1.2f;

    [Header("找到对手后的确认弹窗（2026-09-27）：填了就走它，不再自动接受")]
    [Tooltip("左右双方头像 + 中央 15 秒倒计时 + 左下确认 / 右下拒绝（MatchConfirmPanel）。\n" +
             "填上之后：找到对手 = 弹它（不自动接受）；双方确认 = 它播金色 3 秒倒计时 + 预加载；15 秒不双确认 = 它调 OnConfirmTimeout()。")]
    public MatchConfirmPanel confirmPanel;

    [Header("双方确认后的加载界面（2026-09-27）：填了就用它顶替 JoinGamePanel")]
    [Tooltip("真正全遮挡的战斗加载界面（BattleLoadingScreen）：上下两条带滑入 → 惯性漂移 + 饰纹自转 + xx% → 迅速滑出 → 切场景。")]
    public BattleLoadingScreen battleLoading;

    enum State { Idle, Searching, Found, WaitingOpponent }
    State _state;
    float _countdown;
    bool _iAccepted, _iAmHost, _joining;
    string _oppName;
    bool _oppStatsKnown;
    int _oppTotal, _oppStreak;
    double _oppWinRate;
    CSteamID _lobbyID;
    Coroutine _searchCoroutine, _bgSearchCoroutine;
    float _retryTimer, _bgSearchTimer;
    float _foundHold;      // 「已找到对手！」的停留节拍（秒）；确认弹窗路径下 = 金色 3 秒倒计时的节拍
    bool _goLatched;       // 双方确认后只触发一次 BeginGo()
    // 2026-09-29（「偶现匹配到自己」修）：Steam 的 LobbyCreated / LobbyEnter 是**全局回调** ——
    // 房间面板（LobbyRoomSession）、自动连接建的房也会打到这件上。只有我们自己发起的那一次
    // CreateLobby 才算「我建的房」；这个标志在发起时立起、回调里消掉。
    bool _pendingCreate;
    Callback<LobbyMatchList_t> _listCB, _bgListCB;
    Callback<LobbyCreated_t> _createdCB;
    Callback<LobbyEnter_t> _enterCB;
    Callback<LobbyDataUpdate_t> _dataCB;

    void Awake()
    {
        Instance = this;
        if (panelRoot) panelRoot.SetActive(false);
        if (opponentInfoGroup) opponentInfoGroup.SetActive(false);
        if (acceptButton) { acceptButton.gameObject.SetActive(false); acceptButton.onClick.AddListener(OnAccept); }
        if (declineButton) { declineButton.gameObject.SetActive(false); declineButton.onClick.AddListener(OnDecline); }
        if (cancelButton) cancelButton.onClick.AddListener(OnCancel);
    }

    /// <summary>开匹配。装了紧凑等待窗（compactWait）就走它，面板自带的测试 UI（panelRoot）不再显示 ——
    /// 用户 2026-09-27「之前那个测试匹配界面不再使用」。匹配逻辑完全不变。</summary>
    public void Open()
    {
        // 保险（2026-09-27 用户「在匹配或者排位中再次点击匹配或者排位是不会再次进入匹配或者排位进程」）：
        // 已经在跑就不要重入 —— 否则会重复 RequestLobbyList / 重复注册回调，甚至开出第二个房间。
        // 只把已经在跑的小窗抬到眼前，计时不重置。
        if (IsMatching) { SurfaceWait(); Debug.Log("[QuickMatch] 已在匹配中，忽略重复进入"); return; }

        if (compactWait != null) { if (panelRoot) panelRoot.SetActive(false); compactWait.Show(); }
        else if (panelRoot) panelRoot.SetActive(true);
        ResetState();
        StartSearch();
    }

    /// <summary>正在匹配中（搜索 / 已找到 / 等待对方确认）—— 重复点「匹配」靠它挡重入。</summary>
    public bool IsMatching
    {
        get
        {
            if (_joining) return true;
            if (_lobbyID.m_SteamID != 0) return true;
            return _state == State.Searching || _state == State.Found || _state == State.WaitingOpponent;
        }
    }

    /// <summary>把等待小窗抬到眼前（不重启流程、不重置计时）。重复点击「匹配」时用。</summary>
    public void SurfaceWait()
    {
        // 确认弹窗已经在眼前：别再抬「匹配中」小窗，否则会盖在弹窗上
        if (confirmPanel != null && confirmPanel.IsOpen) return;
        if (compactWait != null) { if (!compactWait.IsOpen) compactWait.Show(); }
        else if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (_searchCoroutine != null) { StopCoroutine(_searchCoroutine); _searchCoroutine = null; }
        LeaveLobby(); if (panelRoot) panelRoot.SetActive(false);
        if (compactWait != null) compactWait.Hide();
        if (confirmPanel != null) confirmPanel.Hide();
        _state = State.Idle;
    }

    /// <summary>取消匹配（紧凑等待窗的取消键走这里）—— 与旧的 OnCancel 同一套收尾。</summary>
    public void CancelMatch() { SetReject(); LeaveLobby(); Close(); }

    void ResetState()
    {
        _state = State.Idle; _countdown = 15f; _iAccepted = false; _iAmHost = false; _joining = false; _oppName = "";
        _oppStatsKnown = false;
        _foundHold = 0f; _goLatched = false;
        if (opponentInfoGroup) opponentInfoGroup.SetActive(false);
        if (acceptButton) { acceptButton.gameObject.SetActive(false); acceptButton.interactable = true; }
        if (declineButton) { declineButton.gameObject.SetActive(false); declineButton.interactable = true; }
    }

    // ============ Search ============

    void StartSearch()
    {
        if (!SteamManager.Initialized) { SetStatus("Steam 未初始化"); return; }
        // Steam API 已初始化但用户未登录后端（离线模式/无网/被墙）——提前报错，
        // 否则 RequestLobbyList/CreateLobby 会以 k_EResultNoConnection 静默失败，面板卡死"匹配中"
        if (!SteamUser.BLoggedOn())
        {
            Debug.LogError("[QuickMatch] Steam 未登录/未连接，取消匹配");
            ResetState();
            SetStatus("Steam 未登录/未连接\n请检查网络或加速器");
            return;
        }
        _state = State.Searching; _iAmHost = false; _lobbyID = default;
        RegisterCallbacks();
        SetStatus("匹配中...");
        _searchCoroutine = StartCoroutine(SearchRoutine());
    }

    IEnumerator SearchRoutine()
    {
        for (int i = 0; i < 10 && _state == State.Searching && !_iAmHost; i++)
        {
            yield return new WaitForSeconds(0.5f);
            // 已经找到大厅正在加入中，停止搜索
            if (_joining || _lobbyID.m_SteamID != 0) yield break;
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld_quick", ELobbyComparison.k_ELobbyComparisonEqual);
            // 显式世界范围——默认(k_ELobbyDistanceFilterDefault)只返回同数据中心的大厅，
            // 双方连不同数据中心时互相搜不到
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            // 2026-09-29：多要几个 —— 第 0 个可能是我自己那间，跳过它还能拿别人的
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(3);
            SteamMatchmaking.RequestLobbyList();
        }
        // 超时且没加入别人大厅 → 自建
        if (_state != State.Searching || _joining || _lobbyID.m_SteamID != 0) yield break;
        _iAmHost = true;
        _pendingCreate = true;      // 只有这一次 CreateLobby 的 LobbyCreated 才算「我建的」
        SetStatus("匹配中...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    // ============ Steam Callbacks — the ONLY data refresh path ============

    /// <summary>候选大厅能不能当对手：不是我现在待的那间，且**房主不是我**。</summary>
    /// <remarks>2026-09-29：自己建的房（上一局没退干净的临时大厅、房间大厅）绝不能当对手 ——
    /// 读它的 host_data 读出来就是自己。纯判断，不碰 Steam 状态。</remarks>
    static bool UsableCandidate(CSteamID found, CSteamID myLobby)
    {
        if (found.m_SteamID == 0) return false;
        if (myLobby.m_SteamID != 0 && found.m_SteamID == myLobby.m_SteamID) return false;   // 我自己那间
        if (SteamMatchmaking.GetLobbyOwner(found) == SteamUser.GetSteamID()) return false;  // 房主是我
        return true;
    }

    void RegisterCallbacks()
    {
        DisposeCallbacks();
        _listCB = Callback<LobbyMatchList_t>.Create(OnLobbyList);
        _createdCB = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _enterCB = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _dataCB = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
    }
    void DisposeCallbacks() { _listCB?.Dispose(); _createdCB?.Dispose(); _enterCB?.Dispose(); _dataCB?.Dispose(); StopBackgroundSearch(); }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        // 自建大厅后由后台回调(OnBgLobbyList)处理搜索结果，前台不再响应。
        // 双保险：即使 _listCB 残留，也不会清 _iAmHost 或误加自己的大厅。
        if (_iAmHost) return;
        if (_state != State.Searching || cb.m_nLobbiesMatching == 0) return;
        // 找到大厅 → 标为正在加入 + 立即停协程 + 重置 Host 标志
        // 2026-09-29：不能闭着眼睛拿第 0 个 —— 搜出来的可能就是我自己那间（上一局没退干净的临时大厅）。
        CSteamID pick = CSteamID.Nil;
        for (int i = 0; i < (int)cb.m_nLobbiesMatching; i++)
        {
            CSteamID c = SteamMatchmaking.GetLobbyByIndex(i);
            if (!UsableCandidate(c, _lobbyID)) { Debug.Log($"[QM] OnLobbyList 候选[{i}] {c.m_SteamID} 是我自己的房 → 跳过"); continue; }
            pick = c; break;
        }
        if (pick.m_SteamID == 0) { Debug.Log("[QM] OnLobbyList 这一批没有能用的候选 → 继续搜"); return; }
        _joining = true;
        if (_searchCoroutine != null) { StopCoroutine(_searchCoroutine); _searchCoroutine = null; }
        _iAmHost = false;
        _lobbyID = pick;
        Debug.Log($"[QM] OnLobbyList: 找到大厅 {_lobbyID}，正在加入...");
        SteamMatchmaking.JoinLobby(_lobbyID);
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        // ★ 2026-09-29：先看这间到底是不是我们自己发起的。
        // 原来的行为是 `if (!_iAmHost) { LeaveLobby(cb); return; }` —— 于是「匹配中」时房间一建房，
        // 匹配就把**房间自己的大厅**给退了（房间号还挂在屏幕上、人已经不在大厅里）。
        bool mine = _pendingCreate; _pendingCreate = false;
        Debug.Log($"[QM-Host] LobbyCreated result={cb.m_eResult}, state={_state}, iAmHost={_iAmHost}, mine={mine}");
        if (!mine)
        {
            Debug.Log("[QM-Host] 这间不是我建的（房间 / 自动连接建的）→ 忽略，一根手指都不碰它");
            return;
        }
        // 创建失败（典型 k_EResultNoConnection = 本机连不上 Steam 后端）——明确报错并复位，
        // 绝不无限"匹配中"。用户修复网络后重新打开面板即可重试。
        if (cb.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[QM-Host] 创建大厅失败 result={cb.m_eResult}");
            if (cb.m_ulSteamIDLobby != 0)
                SteamMatchmaking.LeaveLobby(new CSteamID(cb.m_ulSteamIDLobby));
            LeaveLobby();   // 清理回调 + _lobbyID
            ResetState();
            SetStatus($"创建大厅失败（{cb.m_eResult}）\n请检查网络/加速器后重试");
            return;
        }
        // 建成了但已经用不上（搜到别人并加入了 / 已退出匹配）→ 自己这间是废的，退掉
        if (!_iAmHost || _state != State.Searching)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(cb.m_ulSteamIDLobby));
            return;
        }
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_quick");
        WriteMyData("host_data");
        Debug.Log($"[QM-Host] ★ 临时大厅已建 lobbyID={_lobbyID}，等待对手加入");
        // 自建成功后启动后台搜索——防止两人同时自建永远碰不到
        StartBackgroundSearch();
    }

    void OnLobbyEnter(LobbyEnter_t cb)
    {
        Debug.Log($"[QM] OnLobbyEnter lobbyID={cb.m_ulSteamIDLobby}, state={_state}, iAmHost={_iAmHost}");
        // ★ 2026-09-29（「偶现匹配到自己」的真凶手）：LobbyEnter 也是全局回调 ——
        // 房间面板 CreateLobby（进自己的大厅）、自动连接建的房，都会打到这里。原先只挡 _state，
        // 于是「匹配中」时房间一建房，匹配就把**房间的大厅**当成自己搜到的对手：
        // 把它当 _lobbyID、读它的 host_data（房间写的也是这个 key，值就是我自己）⇒ 弹窗里「匹配到自己」。
        // 现在只认**我正在等的那一间**：前台 / 后台搜索在 JoinLobby 之前、
        // OnLobbyCreated 里都已经把 _lobbyID 定死了。
        if (_lobbyID.m_SteamID == 0 || cb.m_ulSteamIDLobby != _lobbyID.m_SteamID)
        {
            Debug.Log($"[QM] OnLobbyEnter 不是我在等的大厅（我在等 {_lobbyID.m_SteamID}）→ 忽略");
            return;
        }
        if (_state != State.Searching) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        _joining = false;
        if (_iAmHost)
        {
            // 创建者 CreateLobby 成功后 Steam 也会触发 LobbyEnter（进入自己的大厅，members==1）。
            // 此时绝不能停止后台搜索——否则双方各自自建后，兜底搜索被自己的 LobbyEnter 停掉，
            // 谁也搜不到谁，永远匹配不到。
            int membersNow = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
            if (membersNow >= 2)
            {
                // 确实有客人加入 → 停后台搜索 + 轮询客人数据
                StopBackgroundSearch();
                Debug.Log($"[QM-Host] 有人加入我的大厅 lobbyID={_lobbyID}");
                WriteMyData("host_data");
                StartCoroutine(PollGuestData());
            }
            // members==1：刚创建自己的大厅，保持后台搜索。
            // BackgroundSearchRoutine 自己会在 members>=2（有客人）或找到其他单人厅时停止。
            return;
        }
        // 已进入别人的大厅 → 停止后台搜索（作为客人不再搜索）
        StopBackgroundSearch();
        Debug.Log($"[QM-Guest] ★ 进入大厅 lobbyID={_lobbyID}，写SetLobbyMemberData");
        SteamMatchmaking.SetLobbyMemberData(_lobbyID, "player_data", MakeMyJson());
        StartCoroutine(RetryWriteGuestData());
        RefreshOpponent();
    }

    IEnumerator RetryWriteGuestData()
    {
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(0.8f);
            if (_lobbyID.m_SteamID == 0 || _state == State.Idle || _state == State.Found) yield break;
            Debug.Log($"[QM-Guest] RetryWrite round {i}: SetLobbyMemberData");
            SteamMatchmaking.SetLobbyMemberData(_lobbyID, "player_data", MakeMyJson());
        }
    }

    IEnumerator PollGuestData()
    {
        for (int i = 0; i < 8; i++)
        {
            yield return new WaitForSeconds(0.6f);
            if (_lobbyID.m_SteamID == 0 || _state == State.Idle || _state == State.Found) yield break;
            int members = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
            Debug.Log($"[QM-Host] PollGuest round {i}: members={members}");
            RefreshOpponent();
            if (_state == State.Found) yield break;
        }
        Debug.LogWarning($"[QM-Host] PollGuest exhausted");
    }

    void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (_lobbyID.m_SteamID == 0 || cb.m_ulSteamIDLobby != _lobbyID.m_SteamID) return;
        Debug.Log($"[QM-{(_iAmHost?"Host":"Guest")}] LobbyDataUpdate! lobbyID={_lobbyID}, state={_state}, iAmHost={_iAmHost}");
        RefreshOpponent();
    }

    string MakeMyJson()
    {
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        return JsonUtility.ToJson(new QMPD { playerName = sd?.localPlayerName ?? "玩家", totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 });
    }

    // 房主写 lobby data（有权限）
    void WriteMyData(string key)
    {
        if (_lobbyID.m_SteamID == 0) return;
        SteamMatchmaking.SetLobbyData(_lobbyID, key, MakeMyJson());
    }

    void RefreshOpponent()
    {
        if (_lobbyID.m_SteamID == 0) return;
        string oppJson = null;

        if (_iAmHost)
        {
            // 房主读 guest 的 member data
            int count = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
            for (int i = 0; i < count; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyID, i);
                if (member == SteamUser.GetSteamID()) continue;
                oppJson = SteamMatchmaking.GetLobbyMemberData(_lobbyID, member, "player_data");
                Debug.Log($"[QM-Host] ReadMemberData idx={i} member={member} data={(string.IsNullOrEmpty(oppJson)?"empty":"SET")}");
                if (!string.IsNullOrEmpty(oppJson)) break;
            }
        }
        else
        {
            // ★ 兜底：我以客人身份在这间大厅里，房主却是我自己 ⇒ 这不是对手。
            // （真出过的那一条：房间写的 host_data 就是本机 JSON，被当成对手读了进来）。
            if (SteamMatchmaking.GetLobbyOwner(_lobbyID) == SteamUser.GetSteamID())
            {
                Debug.LogWarning("[QM] 这间大厅的房主是我自己（我以客人身份在里面）→ 不当对手，忽略");
                return;
            }
            oppJson = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        }

        Debug.Log($"[QM-{(_iAmHost?"Host":"Guest")}] RefreshOpponent jsonEmpty={string.IsNullOrEmpty(oppJson)} state={_state}");
        if (string.IsNullOrEmpty(oppJson)) return;
        var opp = JsonUtility.FromJson<QMPD>(oppJson);
        if (opp == null || string.IsNullOrEmpty(opp.playerName)) return;
        // ★ 兜底：对面那格解析出来就是我自己的 SteamID ⇒ 绝不当对手（宁可继续等）
        if (opp.steamID != 0 && opp.steamID == SteamUser.GetSteamID().m_SteamID)
        {
            Debug.LogWarning($"[QM] 对面那格是我自己（steamID={opp.steamID}）→ 忽略，继续等真实对手");
            return;
        }
        // 捕获对手 SteamID（Host 用于加载对方头像；Client 时 opp.steamID=HostSteamID，等效）
        if (opp.steamID != 0) LobbyConfig.RemoteSteamID = opp.steamID;
        // 对手战绩（加载界面要展示）—— 放在下面那个 Found 早退之前，重复刷新也保持最新
        _oppTotal = opp.totalMatches; _oppWinRate = opp.winRate; _oppStreak = opp.winStreak;
        _oppStatsKnown = true;
        if (_state == State.Found || _state == State.WaitingOpponent) return;

        Debug.Log($"[QM] ★★★ 已找到对手: {opp.playerName} steamID={opp.steamID} matches={opp.totalMatches} ★★★");
        _state = State.Found; _countdown = 15f; _oppName = opp.playerName;
        if (opponentInfoGroup) opponentInfoGroup.SetActive(true);
        if (opponentNameText) opponentNameText.text = opp.playerName;
        if (opponentStatsText) opponentStatsText.text = $"总场数：{opp.totalMatches}  胜率：{opp.winRate:F1}%  连胜数：{opp.winStreak}";
        if (acceptButton) acceptButton.gameObject.SetActive(true);
        if (declineButton) declineButton.gameObject.SetActive(true);
        SetStatus($"等待确认（{_countdown:F0}s）");
        if (opp.steamID != 0 && opponentAvatar) LoadAvatar(opponentAvatar, opp.steamID);

        if (confirmPanel != null)
        {
            // 确认弹窗路径（2026-09-27 用户）：找到对手 = 弹窗，**不自动接受** ——
            // 双方各自点「确认」才置 host_ok / guest_ok，任一方 15 秒不确认就默认取消。
            if (compactWait != null) compactWait.Hide();
            _foundHold = 0f;
            confirmPanel.Open();
        }
        else if (compactWait != null)
        {
            // 兜底旧路径（没有确认弹窗时）：紧凑窗没有「接受 / 拒绝」两个键 —— 找到即自动接受。
            compactWait.SetFound();
            _foundHold = Mathf.Max(0f, foundHoldSeconds);   // 留一个节拍，让「已找到对手！」看得见
            OnAccept();
        }
    }

    /// <summary>捕获对手 SteamID 到 LobbyConfig.RemoteSteamID（进游戏前最终兜底）。
    /// Host 从 guest 成员数据读；Guest 从 host_data 读（同时补 HostSteamID 供兼容）。</summary>
    void CaptureRemoteSteamID()
    {
        if (_lobbyID.m_SteamID == 0) return;
        if (_iAmHost)
        {
            int count = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
            for (int i = 0; i < count; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyID, i);
                if (member == SteamUser.GetSteamID()) continue;
                string gj = SteamMatchmaking.GetLobbyMemberData(_lobbyID, member, "player_data");
                var gd = JsonUtility.FromJson<QMPD>(gj);
                if (gd != null && gd.steamID != 0) { LobbyConfig.RemoteSteamID = gd.steamID; return; }
            }
        }
        else
        {
            string hj = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
            var hd = JsonUtility.FromJson<QMPD>(hj);
            if (hd != null && hd.steamID != 0)
            {
                LobbyConfig.RemoteSteamID = hd.steamID;
                LobbyConfig.HostSteamID = hd.steamID.ToString();
            }
        }
    }

    // ============ 后台搜索 — 自建大厅后持续搜别人 ============

    void StartBackgroundSearch()
    {
        if (!_iAmHost || _lobbyID.m_SteamID == 0) return;
        Debug.Log("[QM-Bg] 启动后台搜索...");
        StopBackgroundSearch();
        _bgListCB?.Dispose();
        // 停用前台搜索回调——自建后只由后台回调(OnBgLobbyList)处理搜索结果。
        // 否则每次后台 RequestLobbyList 会同时触发前台 OnLobbyList（无 _iAmHost 守卫），
        // 把 _iAmHost 清 false 并 JoinLobby(自己的大厅)，导致永远匹配不到对手。
        _listCB?.Dispose(); _listCB = null;
        _bgListCB = Callback<LobbyMatchList_t>.Create(OnBgLobbyList);
        _bgSearchCoroutine = StartCoroutine(BackgroundSearchRoutine());
    }

    void StopBackgroundSearch()
    {
        if (_bgSearchCoroutine != null) { StopCoroutine(_bgSearchCoroutine); _bgSearchCoroutine = null; }
        _bgListCB?.Dispose(); _bgListCB = null;
    }

    IEnumerator BackgroundSearchRoutine()
    {
        while (_iAmHost && _state == State.Searching && _lobbyID.m_SteamID != 0)
        {
            // 每 3 秒搜一次——避免过于频繁调用 Steam API
            for (float t = 0; t < 3f; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                if (!_iAmHost || _state != State.Searching || _lobbyID.m_SteamID == 0) yield break;
            }
            // 确认自己的大厅还有效
            if (_lobbyID.m_SteamID == 0) yield break;
            int members = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
            // 如果已经有人加入自己的大厅，停止后台搜索
            if (members >= 2) { Debug.Log("[QM-Bg] 自己大厅已有客人，停止后台搜索"); yield break; }
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld_quick", ELobbyComparison.k_ELobbyComparisonEqual);
            // 显式世界范围——同前台搜索
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(3);
            SteamMatchmaking.RequestLobbyList();
            Debug.Log("[QM-Bg] RequestLobbyList 已发送");
        }
    }

    void OnBgLobbyList(LobbyMatchList_t cb)
    {
        if (!_iAmHost || _state != State.Searching || _lobbyID.m_SteamID == 0 || cb.m_nLobbiesMatching == 0) return;
        Debug.Log($"[QM-Bg] 搜到 {cb.m_nLobbiesMatching} 个大厅，自己={_lobbyID.m_SteamID}");

        for (int i = 0; i < (int)cb.m_nLobbiesMatching; i++)
        {
            CSteamID found = SteamMatchmaking.GetLobbyByIndex(i);
            // 诊断：打印每个大厅详情，确认对方大厅是否在结果里、game 字段是否匹配
            string fGame = SteamMatchmaking.GetLobbyData(found, "game") ?? "";
            int fMembers = SteamMatchmaking.GetNumLobbyMembers(found);
            string fHostData = SteamMatchmaking.GetLobbyData(found, "host_data") ?? "";
            Debug.Log($"[QM-Bg] 大厅[{i}] id={found.m_SteamID} game={fGame} members={fMembers} host_data={(string.IsNullOrEmpty(fHostData)?"empty":"SET")} isSelf={found == _lobbyID}");
            if (!UsableCandidate(found, _lobbyID)) { Debug.Log($"[QM-Bg] 候选 {found.m_SteamID} 是我自己的房 → 跳过"); continue; }

            // 检查对方是否一个人在等（未满员、未开始）
            int foundMembers = SteamMatchmaking.GetNumLobbyMembers(found);
            string foundHostOk = SteamMatchmaking.GetLobbyData(found, "host_ok") ?? "";
            string foundStart = SteamMatchmaking.GetLobbyData(found, "start") ?? "";
            if (foundMembers >= 2 || foundHostOk == "1" || foundStart == "1")
            {
                Debug.Log($"[QM-Bg] 大厅 {found} 已满/已确认/已开始 (members={foundMembers}, host_ok={foundHostOk}, start={foundStart})，跳过");
                continue;
            }

            Debug.Log($"[QM-Bg] ★ 发现候选大厅 {found}，放弃自己的大厅并加入");
            StopBackgroundSearch();
            CSteamID myOldLobby = _lobbyID;
            _lobbyID = default;
            SteamMatchmaking.LeaveLobby(myOldLobby);
            _iAmHost = false; _joining = true;
            _lobbyID = found;
            SteamMatchmaking.JoinLobby(found);
            return;
        }
    }

    // ============ Update ============

    void Update()
    {
        if (_state == State.Idle || _lobbyID.m_SteamID == 0) return;

        _retryTimer += Time.deltaTime;
        bool doRetry = _retryTimer >= 0.5f;
        if (doRetry) _retryTimer = 0;

        // Guest: keep retrying SetLobbyMemberData every 0.5s
        if (!_iAmHost && doRetry)
        {
            SteamMatchmaking.SetLobbyMemberData(_lobbyID, "player_data", MakeMyJson());
        }

        if (doRetry && _lobbyID.m_SteamID != 0)
        {
            SteamMatchmaking.RequestLobbyData(_lobbyID);
            // Host 读 guest 成员数据；Guest 也读 host_data（用于捕获对手 SteamID + 发现对手）
            if (_state == State.Searching)
                RefreshOpponent();
        }

        // Countdown —— 装了确认弹窗时，那 15 秒归 MatchConfirmPanel 自己走（它要驱动中央数字与两键显隐），
        // 超时回调 OnConfirmTimeout()；这里只保留没有弹窗时的旧行为（超时重开搜索）。
        if (_state == State.Found && confirmPanel == null)
        {
            _countdown -= Time.deltaTime;
            if (_countdown <= 0) { LeaveLobby(); ResetState(); StartSearch(); return; }
        }

        // Check accept/reject flags
        // Host: both flags in lobby data
        // Guest: host_ok in lobby data, guest_ok in member data (self), or just use _iAccepted
        string hostOk = SteamMatchmaking.GetLobbyData(_lobbyID, "host_ok") ?? "";
        string guestOk = _iAmHost ? ReadMemberDataKey("guest_ok") : (_iAccepted ? "1" : "");
        string oppOk = _iAmHost ? guestOk : hostOk;

        // 对方那格 = 1 → 解开弹窗里对方头像的压黑
        if (confirmPanel != null && oppOk == "1") confirmPanel.NotifyOpponentAccepted();

        if ((_state == State.Found || _state == State.WaitingOpponent) && oppOk == "0")
        {
            if (confirmPanel != null)
            {
                // 确认弹窗路径（用户 2026-09-27）：对方拒绝 = 自己这边对方压红 + 短暂停留，
                // 停留结束后弹窗自己调 OnReMatchAfterDecline() **自动重排**。
                // 这里先把本局退干净（退房 + 复位），重排那一步交给弹窗的停留收尾。
                SetStatus("对方已拒绝，重新匹配…");
                confirmPanel.OpponentDeclined();
                SetReject(); LeaveLobby(); ResetState();
                Debug.Log("[QM] 对方拒绝确认 → 退房，等弹窗停留结束后自动重排");
                return;
            }
            SetStatus("对方已拒绝\n重新匹配..."); LeaveLobby(); ResetState(); StartSearch(); return;
        }

        if ((_state == State.WaitingOpponent || (_iAccepted && _state == State.Found)) && hostOk == "1" && guestOk == "1")
        {
            if (confirmPanel != null)
            {
                // 双方确认：弹窗收掉「确认 / 拒绝」、倒计时转金色 3 秒、同时开始预加载战斗场景素材。
                // 先让这 3 秒走完再进 JoinGamePanel（否则进度条同帧就把弹窗盖住了）。
                if (!_goLatched)
                {
                    _goLatched = true;
                    _foundHold = Mathf.Max(0f, confirmPanel.GoSeconds);
                    confirmPanel.BeginGo();
                }
                if (_foundHold > 0f) { _foundHold -= Time.deltaTime; return; }
            }
            else if (_foundHold > 0f) { _foundHold -= Time.deltaTime; return; }   // 「已找到对手！」先露个面
            SetStatus("双方已接受！");
            LobbyConfig.FromLobby = true; LobbyConfig.IsHost = _iAmHost; LobbyConfig.IsDirectIP = false; LobbyConfig.ServerIP = "";
            LobbyConfig.CurrentLobbyID = _lobbyID;
            // 基于大厅ID生成唯一匹配key——防止多组同时进Game串线到别人房间
            LobbyConfig.MatchKey = $"aw_{_lobbyID.m_SteamID}";
            CaptureRemoteSteamID();   // 进游戏前最终捕获对手 SteamID（Host 读 guest 成员数据 / Guest 读 host_data）
            if (_iAmHost)
                LobbyConfig.HostSteamID = SteamUser.GetSteamID().m_SteamID.ToString();
            _lobbyID = default; _state = State.Idle;
            if (panelRoot) panelRoot.SetActive(false);
            if (compactWait != null) compactWait.Hide();
            if (confirmPanel != null) confirmPanel.Hide();
            // 双方确认后的「真正加载界面」顶替原来的 JoinGamePanel（2026-09-27 用户）；
            // 没接的时候退回 JoinGamePanel，行为不变。
            if (battleLoading != null) battleLoading.Open();
            else JoinGamePanel.Instance?.Open();
        }
    }

    // ============ Buttons ============

    /// <summary>对方拒绝、弹窗停留结束后由 MatchConfirmPanel 回调 —— **自动重排**
    /// （用户 2026-09-27：「自己拒绝的不会重排，对方拒绝的自己会再次进入匹配池子」）。</summary>
    public void OnReMatchAfterDecline()
    {
        Debug.Log("[QM] 对方拒绝 → 自动重新匹配");
        if (IsMatching) { Debug.Log("[QM] 已在匹配中，忽略重排"); return; }
        if (confirmPanel != null) confirmPanel.Hide();
        if (compactWait != null && !compactWait.IsOpen) compactWait.Show();
        StartSearch();
    }

    /// <summary>确认弹窗 15 秒到、双方没都确认 —— 用户：「倒计时结束若双方有一个不确认就默认取消」。
    /// 写拒绝标记 + 退房 + 复位，**不自动重开匹配**（玩家再点一次「匹配」）。</summary>
    public void OnConfirmTimeout()
    {
        Debug.Log("[QM] 确认超时 → 本局取消（不自动重匹配）");
        SetStatus("未确认，已取消");
        SetReject(); LeaveLobby(); ResetState();
    }

    public void OnAccept()
    {
        if (_state != State.Found) return;
        _iAccepted = true;
        // Host writes to lobby data, guest writes to member data
        if (_iAmHost) SteamMatchmaking.SetLobbyData(_lobbyID, "host_ok", "1");
        else SteamMatchmaking.SetLobbyMemberData(_lobbyID, "guest_ok", "1");
        if (acceptButton) acceptButton.interactable = false;
        if (declineButton) declineButton.gameObject.SetActive(false);
        _state = State.WaitingOpponent;
        SetStatus("已接受，等待对方确认");
    }
    public void OnDecline() { SetReject(); LeaveLobby(); Close(); }
    void OnCancel() { SetReject(); LeaveLobby(); Close(); }
    void SetReject()
    {
        if (_lobbyID.m_SteamID == 0) return;
        if (_iAmHost) SteamMatchmaking.SetLobbyData(_lobbyID, "host_ok", "0");
        else SteamMatchmaking.SetLobbyMemberData(_lobbyID, "guest_ok", "0");
    }

    string ReadMemberDataKey(string key)
    {
        int count = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyID, i);
            if (member == SteamUser.GetSteamID()) continue;
            string val = SteamMatchmaking.GetLobbyMemberData(_lobbyID, member, key);
            if (!string.IsNullOrEmpty(val)) return val;
        }
        return "";
    }

    void LeaveLobby() { _joining = false; if (_lobbyID.m_SteamID != 0) { SteamMatchmaking.LeaveLobby(_lobbyID); _lobbyID = default; } DisposeCallbacks(); }

    void SetStatus(string msg)
    {
        Debug.Log("[QuickMatch] " + msg.Replace("\n", " "));
        if (statusText) statusText.text = msg;
        if (compactWait != null) compactWait.SetNotice(msg);
    }
    void OnDestroy() { DisposeCallbacks(); }

    static void LoadAvatar(RawImage target, ulong steamID)
    {
        // 统一走 SteamAvatarManager（大→中降级 + 缓存），同时预缓存进轮盘
        var tex = SteamAvatarManager.GetAvatarTexture(steamID);
        if (tex != null && target != null) target.texture = tex;
    }

    [System.Serializable] class QMPD { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
