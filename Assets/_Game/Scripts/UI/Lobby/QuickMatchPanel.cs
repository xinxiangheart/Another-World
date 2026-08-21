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

    [Header("面板")] public GameObject panelRoot;
    [Header("状态")] public TMP_Text statusText;
    [Header("对手")] public GameObject opponentInfoGroup;
    public RawImage opponentAvatar;
    public TMP_Text opponentNameText, opponentStatsText;
    [Header("按钮")] public Button acceptButton, declineButton, cancelButton;

    enum State { Idle, Searching, Found, WaitingOpponent }
    State _state;
    float _countdown;
    bool _iAccepted, _iAmHost, _joining;
    string _oppName;
    CSteamID _lobbyID;
    Coroutine _searchCoroutine, _bgSearchCoroutine;
    float _retryTimer, _bgSearchTimer;
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

    public void Open() { if (panelRoot) panelRoot.SetActive(true); ResetState(); StartSearch(); }
    public void Close()
    {
        if (_searchCoroutine != null) { StopCoroutine(_searchCoroutine); _searchCoroutine = null; }
        LeaveLobby(); if (panelRoot) panelRoot.SetActive(false); _state = State.Idle;
    }

    void ResetState()
    {
        _state = State.Idle; _countdown = 15f; _iAccepted = false; _iAmHost = false; _joining = false; _oppName = "";
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
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
            SteamMatchmaking.RequestLobbyList();
        }
        // 超时且没加入别人大厅 → 自建
        if (_state != State.Searching || _joining || _lobbyID.m_SteamID != 0) yield break;
        _iAmHost = true;
        SetStatus("匹配中...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    // ============ Steam Callbacks — the ONLY data refresh path ============

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
        _joining = true;
        if (_searchCoroutine != null) { StopCoroutine(_searchCoroutine); _searchCoroutine = null; }
        _iAmHost = false;
        _lobbyID = SteamMatchmaking.GetLobbyByIndex(0);
        Debug.Log($"[QM] OnLobbyList: 找到大厅 {_lobbyID}，正在加入...");
        SteamMatchmaking.JoinLobby(_lobbyID);
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        Debug.Log($"[QM-Host] LobbyCreated result={cb.m_eResult}, state={_state}, iAmHost={_iAmHost}");
        // 如果已经加入别人大厅（_iAmHost 被 OnLobbyList 重置），销毁自己建的这个废弃大厅
        if (!_iAmHost) { SteamMatchmaking.LeaveLobby(new CSteamID(cb.m_ulSteamIDLobby)); return; }
        if (_state != State.Searching) return;
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
            oppJson = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        }

        Debug.Log($"[QM-{(_iAmHost?"Host":"Guest")}] RefreshOpponent jsonEmpty={string.IsNullOrEmpty(oppJson)} state={_state}");
        if (string.IsNullOrEmpty(oppJson)) return;
        var opp = JsonUtility.FromJson<QMPD>(oppJson);
        if (opp == null || string.IsNullOrEmpty(opp.playerName)) return;
        // 捕获对手 SteamID（Host 用于加载对方头像；Client 时 opp.steamID=HostSteamID，等效）
        if (opp.steamID != 0) LobbyConfig.RemoteSteamID = opp.steamID.ToString();
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
                if (gd != null && gd.steamID != 0) { LobbyConfig.RemoteSteamID = gd.steamID.ToString(); return; }
            }
        }
        else
        {
            string hj = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
            var hd = JsonUtility.FromJson<QMPD>(hj);
            if (hd != null && hd.steamID != 0)
            {
                LobbyConfig.RemoteSteamID = hd.steamID.ToString();
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
            if (found == _lobbyID) continue; // 忽略自己的大厅

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

        // Countdown
        if (_state == State.Found)
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

        if ((_state == State.Found || _state == State.WaitingOpponent) && oppOk == "0")
        {
            SetStatus("对方已拒绝\n重新匹配..."); LeaveLobby(); ResetState(); StartSearch(); return;
        }

        if ((_state == State.WaitingOpponent || (_iAccepted && _state == State.Found)) && hostOk == "1" && guestOk == "1")
        {
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
            JoinGamePanel.Instance?.Open();
        }
    }

    // ============ Buttons ============

    void OnAccept()
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
    void OnDecline() { SetReject(); LeaveLobby(); Close(); }
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

    void SetStatus(string msg) { Debug.Log("[QuickMatch] " + msg.Replace("\n", " ")); if (statusText) statusText.text = msg; }
    void OnDestroy() { DisposeCallbacks(); }

    static void LoadAvatar(RawImage target, ulong steamID)
    {
        int ah = SteamFriends.GetLargeFriendAvatar(new CSteamID(steamID));
        if (ah <= 0 || !SteamUtils.GetImageSize(ah, out uint w, out uint h)) return;
        byte[] px = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(ah, px, (int)(w * h * 4))) return;
        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false); tex.LoadRawTextureData(px);
        var cols = tex.GetPixels();
        for (int y = 0; y < h / 2; y++) for (int x = 0; x < w; x++) { int top = y * (int)w + x, bot = ((int)h - 1 - y) * (int)w + x; var t = cols[top]; cols[top] = cols[bot]; cols[bot] = t; }
        tex.SetPixels(cols); tex.Apply(); target.texture = tex;
    }

    [System.Serializable] class QMPD { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
