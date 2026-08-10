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
    Coroutine _searchCoroutine;
    float _retryTimer;

    Callback<LobbyMatchList_t> _listCB;
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
    public void Close() { LeaveLobby(); if (panelRoot) panelRoot.SetActive(false); _state = State.Idle; }

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
    void DisposeCallbacks() { _listCB?.Dispose(); _createdCB?.Dispose(); _enterCB?.Dispose(); _dataCB?.Dispose(); }

    void OnLobbyList(LobbyMatchList_t cb)
    {
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
        if (_state != State.Searching || cb.m_eResult != EResult.k_EResultOK) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_quick");
        WriteMyData("host_data");
        Debug.Log($"[QM-Host] ★ 临时大厅已建 lobbyID={_lobbyID}，等待对手加入");
    }

    void OnLobbyEnter(LobbyEnter_t cb)
    {
        Debug.Log($"[QM] OnLobbyEnter lobbyID={cb.m_ulSteamIDLobby}, state={_state}, iAmHost={_iAmHost}");
        if (_state != State.Searching) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        _joining = false;
        if (_iAmHost)
        {
            Debug.Log($"[QM-Host] 有人加入我的大厅 lobbyID={_lobbyID}");
            // Re-write host_data for the new member, then poll guest_data for 3s
            WriteMyData("host_data");
            StartCoroutine(PollGuestData());
            return;
        }
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
            if (_iAmHost && _state == State.Searching)
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
            LeaveLobby(); _state = State.Idle; if (panelRoot) panelRoot.SetActive(false);
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
