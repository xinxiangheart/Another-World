using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

public class QuickMatchPanel : MonoBehaviour
{
    public static QuickMatchPanel Instance { get; private set; }
    public static bool MatchConfirmed { get; set; }
    public string opponentName => opponentNameText != null ? opponentNameText.text : "";
    public Texture opponentTexture => opponentAvatar != null ? opponentAvatar.texture : null;

    [Header("面板")] public GameObject panelRoot;
    [Header("状态文本")] public TMP_Text statusText;
    [Header("对手信息")] public GameObject opponentInfoGroup;
    public RawImage opponentAvatar;
    public TMP_Text opponentNameText;
    public TMP_Text opponentStatsText;
    [Header("按钮")] public Button acceptButton;
    public Button declineButton;
    public Button cancelButton;

    private enum State { Idle, Searching, Found, WaitingOpponent }
    private State _state;
    private float _countdown;
    private bool _iAccepted;
    private bool _iAmHost;
    private CSteamID _lobbyID;
    private Callback<LobbyCreated_t> _lobbyCreatedCB;
    private Callback<LobbyMatchList_t> _lobbyListCB;
    private Callback<LobbyEnter_t> _lobbyEnterCB;
    private Callback<LobbyDataUpdate_t> _lobbyDataCB;
    private Coroutine _searchCoroutine;
    private bool _opponentDataReceived;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (opponentInfoGroup != null) opponentInfoGroup.SetActive(false);
        if (acceptButton != null) acceptButton.gameObject.SetActive(false);
        if (declineButton != null) declineButton.gameObject.SetActive(false);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAccept);
        if (declineButton != null) declineButton.onClick.AddListener(OnDecline);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
    }

    public void Open() { if (panelRoot != null) panelRoot.SetActive(true); ResetState(); StartSearch(); }
    public void Close() { LeaveLobby(); if (panelRoot != null) panelRoot.SetActive(false); _state = State.Idle; }

    void ResetState()
    {
        _state = State.Idle; _countdown = 15f; _iAccepted = false;
        _iAmHost = false; _opponentDataReceived = false;
        if (opponentInfoGroup != null) opponentInfoGroup.SetActive(false);
        if (acceptButton != null) acceptButton.gameObject.SetActive(false);
        if (declineButton != null) declineButton.gameObject.SetActive(false);
    }

    // ===================== 搜索 =====================

    void StartSearch()
    {
        if (!SteamManager.Initialized) { SetStatus("Steam 未初始化\n请从 Steam 启动游戏"); return; }
        RegisterCallbacks();
        _state = State.Searching; _iAccepted = false; _iAmHost = false; _opponentDataReceived = false;
        SetStatus("匹配中...");
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld_quick", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
        SteamMatchmaking.RequestLobbyList();
        _searchCoroutine = StartCoroutine(SearchTimeout());
    }

    IEnumerator SearchTimeout()
    {
        yield return new WaitForSeconds(4f);
        if (_state != State.Searching) yield break;
        _iAmHost = true;
        SetStatus("匹配中...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    // ===================== Steam Callbacks =====================

    void RegisterCallbacks() { DisposeCallbacks(); _lobbyCreatedCB = Callback<LobbyCreated_t>.Create(OnLobbyCreated); _lobbyListCB = Callback<LobbyMatchList_t>.Create(OnLobbyList); _lobbyEnterCB = Callback<LobbyEnter_t>.Create(OnLobbyEnter); _lobbyDataCB = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate); }
    void DisposeCallbacks() { _lobbyCreatedCB?.Dispose(); _lobbyListCB?.Dispose(); _lobbyEnterCB?.Dispose(); _lobbyDataCB?.Dispose(); }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (_state != State.Searching) return;
        if (cb.m_eResult != EResult.k_EResultOK) { SetStatus("创建大厅失败"); return; }
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_quick");
        WriteMyData("host_data");
    }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        if (_state != State.Searching || _iAmHost || cb.m_nLobbiesMatching == 0) return;
        SteamMatchmaking.JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
    }

    void OnLobbyEnter(LobbyEnter_t cb)
    {
        if (_state != State.Searching) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        if (!_iAmHost) { WriteMyData("guest_data"); TryReadOpponent("host_data"); }
        else SteamMatchmaking.SetLobbyData(_lobbyID, "host_data", MakeMyJson()); // refresh for the joiner
    }

    void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (_lobbyID.m_SteamID != cb.m_ulSteamIDLobby || _state != State.Searching || _opponentDataReceived) return;
        string key = _iAmHost ? "guest_data" : "host_data";
        TryReadOpponent(key);
    }

    void WriteMyData(string key)
    {
        if (_lobbyID.m_SteamID == 0) return;
        SteamMatchmaking.SetLobbyData(_lobbyID, key, MakeMyJson());
    }

    string MakeMyJson()
    {
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        return JsonUtility.ToJson(new QuickMatchPlayerData { playerName = sd?.localPlayerName ?? "玩家", totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 });
    }

    void TryReadOpponent(string key)
    {
        string json = SteamMatchmaking.GetLobbyData(_lobbyID, key);
        if (string.IsNullOrEmpty(json)) return;
        var opp = JsonUtility.FromJson<QuickMatchPlayerData>(json);
        if (opp == null || string.IsNullOrEmpty(opp.playerName)) return;
        var sd = SteamDataManager.Instance;
        if (opp.playerName == (sd?.localPlayerName ?? "玩家")) return;

        _opponentDataReceived = true;
        _state = State.Found; _countdown = 15f;
        if (_searchCoroutine != null) { StopCoroutine(_searchCoroutine); _searchCoroutine = null; }
        if (opponentInfoGroup != null) opponentInfoGroup.SetActive(true);
        if (opponentNameText != null) opponentNameText.text = opp.playerName;
        if (opponentStatsText != null) opponentStatsText.text = $"总场数：{opp.totalMatches}  胜率：{opp.winRate:F1}%  连胜数：{opp.winStreak}";
        if (acceptButton != null) acceptButton.gameObject.SetActive(true);
        if (declineButton != null) declineButton.gameObject.SetActive(true);
        SetStatus($"等待确认（{_countdown:F0}s）");
        if (opp.steamID != 0 && opponentAvatar != null) LoadAvatar(opponentAvatar, opp.steamID);
    }

    // ===================== 按钮 =====================

    void OnAccept()
    {
        if (_state != State.Found) return;
        _iAccepted = true;
        SteamMatchmaking.SetLobbyData(_lobbyID, _iAmHost ? "host_ok" : "guest_ok", "1");
        if (acceptButton != null) acceptButton.interactable = false;
        if (declineButton != null) declineButton.gameObject.SetActive(false);
        _state = State.WaitingOpponent;
        SetStatus("已接受，等待对方确认");
    }

    void OnDecline()
    {
        if (_state != State.Found) return;
        SteamMatchmaking.SetLobbyData(_lobbyID, _iAmHost ? "host_ok" : "guest_ok", "0");
        LeaveLobby(); Close();
    }

    void OnCancel()
    {
        if (_state == State.Found) SteamMatchmaking.SetLobbyData(_lobbyID, _iAmHost ? "host_ok" : "guest_ok", "0");
        LeaveLobby(); Close();
    }

    // ===================== Update =====================

    void Update()
    {
        if (_state == State.Found)
        {
            _countdown -= Time.deltaTime;
            SetStatus($"等待确认（{_countdown:F0}s）");
            string oppKey = _iAmHost ? "guest_ok" : "host_ok";
            if (SteamMatchmaking.GetLobbyData(_lobbyID, oppKey) == "0") { SetStatus("对方已拒绝\n重新匹配..."); LeaveLobby(); ResetState(); StartSearch(); return; }
            if (_countdown <= 0) { SetStatus("超时，重新匹配..."); LeaveLobby(); ResetState(); StartSearch(); }
        }
        if (_state == State.WaitingOpponent)
        {
            string oppKey = _iAmHost ? "guest_ok" : "host_ok";
            if (SteamMatchmaking.GetLobbyData(_lobbyID, oppKey) == "0") { SetStatus("对方已拒绝\n重新匹配..."); LeaveLobby(); ResetState(); StartSearch(); return; }
        }
        if ((_state == State.WaitingOpponent || (_iAccepted && _state == State.Found)) && _lobbyID.m_SteamID != 0
            && SteamMatchmaking.GetLobbyData(_lobbyID, "host_ok") == "1" && SteamMatchmaking.GetLobbyData(_lobbyID, "guest_ok") == "1")
        {
            SetStatus("双方已接受！"); MatchConfirmed = true;
            LobbyConfig.FromLobby = true; LobbyConfig.IsHost = _iAmHost; LobbyConfig.IsDirectIP = false; LobbyConfig.ServerIP = "";
            LeaveLobby(); _state = State.Idle; if (panelRoot != null) panelRoot.SetActive(false);
            JoinGamePanel.Instance?.Open();
        }
    }

    void LeaveLobby() { if (_lobbyID.m_SteamID != 0) { SteamMatchmaking.LeaveLobby(_lobbyID); _lobbyID = default; } DisposeCallbacks(); }
    void SetStatus(string msg) { Debug.Log("[QuickMatch] " + msg.Replace("\n", " ")); if (statusText != null) statusText.text = msg; }
    void OnDestroy() { DisposeCallbacks(); }

    static void LoadAvatar(RawImage target, ulong steamID)
    {
        int ah = SteamFriends.GetLargeFriendAvatar(new CSteamID(steamID));
        if (ah <= 0 || !SteamUtils.GetImageSize(ah, out uint w, out uint h)) return;
        byte[] px = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(ah, px, (int)(w * h * 4))) return;
        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(px);
        var cols = tex.GetPixels();
        for (int y = 0; y < h / 2; y++) for (int x = 0; x < w; x++) { int top = y * (int)w + x, bot = ((int)h - 1 - y) * (int)w + x; var t = cols[top]; cols[top] = cols[bot]; cols[bot] = t; }
        tex.SetPixels(cols); tex.Apply();
        target.texture = tex;
    }

    [System.Serializable] class QuickMatchPlayerData { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
