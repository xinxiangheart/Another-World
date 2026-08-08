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

    private enum State { Idle, Searching, Found, WaitingOpponent }
    private State _state;
    private float _countdown, _pollTimer;
    private bool _iAccepted, _iAmHost, _enteredLobby;
    private string _oppName;
    private CSteamID _lobbyID;

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
        _state = State.Idle; _countdown = 15f; _iAccepted = false; _iAmHost = false; _enteredLobby = false; _oppName = "";
        if (opponentInfoGroup) opponentInfoGroup.SetActive(false);
        if (acceptButton) { acceptButton.gameObject.SetActive(false); acceptButton.interactable = true; }
        if (declineButton) { declineButton.gameObject.SetActive(false); declineButton.interactable = true; }
    }

    // =============== Search ===============

    void StartSearch()
    {
        if (!SteamManager.Initialized) { SetStatus("Steam 未初始化"); return; }
        _state = State.Searching;
        SetStatus("匹配中...");
        StartCoroutine(SearchPhase());
    }

    IEnumerator SearchPhase()
    {
        // Keep searching for 4s
        for (float t = 0; t < 4f && _state == State.Searching && !_iAmHost; t += 0.5f)
        {
            yield return new WaitForSeconds(0.5f);
            if (_lobbyID.m_SteamID != 0) yield break; // already joined via callback
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld_quick", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
            SteamMatchmaking.RequestLobbyList();
        }
        if (_state != State.Searching || _lobbyID.m_SteamID != 0) yield break;
        // No lobby found — become host
        _iAmHost = true;
        _enteredLobby = true; // created lobbies count as entered immediately
        SetStatus("匹配中...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    // =============== Steam Callbacks ===============

    Callback<LobbyMatchList_t> _listCB;
    Callback<LobbyCreated_t> _createdCB;
    Callback<LobbyEnter_t> _enterCB;

    void OnEnable() { _listCB = Callback<LobbyMatchList_t>.Create(cb => OnLobbyList(cb)); _createdCB = Callback<LobbyCreated_t>.Create(cb => OnLobbyCreated(cb)); _enterCB = Callback<LobbyEnter_t>.Create(cb => OnLobbyEnter(cb)); }
    void OnDisable() { _listCB?.Dispose(); _createdCB?.Dispose(); _enterCB?.Dispose(); }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        if (_state != State.Searching || _iAmHost || cb.m_nLobbiesMatching == 0) return;
        _lobbyID = SteamMatchmaking.GetLobbyByIndex(0);
        SteamMatchmaking.JoinLobby(_lobbyID);
        SetStatus("找到对手，正在加入...");
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (_state != State.Searching || cb.m_eResult != EResult.k_EResultOK) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_quick");
        WriteMyData("host_data");
    }

    void OnLobbyEnter(LobbyEnter_t cb)
    {
        if (_state != State.Searching || _iAmHost) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        _enteredLobby = true;
        WriteMyData("guest_data"); // now I have permission to write
        // Try reading host data immediately
        string hostJson = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        if (!string.IsNullOrEmpty(hostJson)) TryShowOpponent(hostJson);
    }

    void WriteMyData(string key)
    {
        if (_lobbyID.m_SteamID == 0) return;
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        SteamMatchmaking.SetLobbyData(_lobbyID, key, JsonUtility.ToJson(new QMPD { playerName = sd?.localPlayerName ?? "玩家", totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 }));
    }

    // =============== Update ===============

    void Update()
    {
        if (_state == State.Idle || _lobbyID.m_SteamID == 0 || !_enteredLobby) return;

        _pollTimer += Time.deltaTime;
        if (_pollTimer < 0.3f) return;
        _pollTimer = 0;

        string hostOk = SteamMatchmaking.GetLobbyData(_lobbyID, "host_ok") ?? "";
        string guestOk = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_ok") ?? "";
        string oppOk = _iAmHost ? guestOk : hostOk;

        // Searching — wait for opponent data (host waits for guest, guest already read in callback)
        if (_state == State.Searching)
        {
            string oppJson = _iAmHost ? SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data") : SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
            if (!string.IsNullOrEmpty(oppJson)) TryShowOpponent(oppJson);
        }

        // Found — countdown + check reject
        if (_state == State.Found)
        {
            _countdown -= Time.deltaTime;
            SetStatus($"等待确认（{_countdown:F0}s）");
            if (_countdown <= 0) { LeaveLobby(); ResetState(); StartSearch(); return; }
        }

        // Check opponent reject or cancel
        if ((_state == State.Found || _state == State.WaitingOpponent) && oppOk == "0")
        {
            SetStatus("对方已拒绝\n重新匹配..."); LeaveLobby(); ResetState(); StartSearch(); return;
        }

        // Both accepted
        if ((_state == State.WaitingOpponent || (_iAccepted && _state == State.Found)) && hostOk == "1" && guestOk == "1")
        {
            LobbyConfig.FromLobby = true; LobbyConfig.IsHost = _iAmHost; LobbyConfig.IsDirectIP = false; LobbyConfig.ServerIP = "";
            LeaveLobby(); _state = State.Idle; if (panelRoot) panelRoot.SetActive(false);
            JoinGamePanel.Instance?.Open();
        }
    }

    // =============== UI ===============

    void TryShowOpponent(string json)
    {
        var opp = JsonUtility.FromJson<QMPD>(json);
        if (opp == null || string.IsNullOrEmpty(opp.playerName)) return;
        var sd = SteamDataManager.Instance;
        if (opp.playerName == (sd?.localPlayerName ?? "玩家")) return;
        _state = State.Found; _countdown = 15f; _oppName = opp.playerName;
        if (opponentInfoGroup) opponentInfoGroup.SetActive(true);
        if (opponentNameText) opponentNameText.text = opp.playerName;
        if (opponentStatsText) opponentStatsText.text = $"总场数：{opp.totalMatches}  胜率：{opp.winRate:F1}%  连胜数：{opp.winStreak}";
        if (acceptButton) acceptButton.gameObject.SetActive(true);
        if (declineButton) declineButton.gameObject.SetActive(true);
        if (opp.steamID != 0 && opponentAvatar) LoadAvatar(opponentAvatar, opp.steamID);
    }

    void OnAccept()
    {
        if (_state != State.Found) return;
        _iAccepted = true;
        SteamMatchmaking.SetLobbyData(_lobbyID, _iAmHost ? "host_ok" : "guest_ok", "1");
        if (acceptButton) acceptButton.interactable = false;
        if (declineButton) declineButton.gameObject.SetActive(false);
        _state = State.WaitingOpponent;
        SetStatus("已接受，等待对方确认");
    }

    void OnDecline() { SetReject(); LeaveLobby(); Close(); }
    void OnCancel() { SetReject(); LeaveLobby(); Close(); }
    void SetReject() { if (_lobbyID.m_SteamID != 0) SteamMatchmaking.SetLobbyData(_lobbyID, _iAmHost ? "host_ok" : "guest_ok", "0"); }
    void LeaveLobby() { if (_lobbyID.m_SteamID != 0) { SteamMatchmaking.LeaveLobby(_lobbyID); _lobbyID = default; } }

    void SetStatus(string msg) { Debug.Log("[QuickMatch] " + msg.Replace("\n", " ")); if (statusText) statusText.text = msg; }

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
