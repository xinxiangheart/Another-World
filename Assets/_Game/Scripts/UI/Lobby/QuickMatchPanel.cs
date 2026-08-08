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
    private bool _iAccepted, _iAmHost;
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
        _state = State.Idle; _countdown = 15f; _iAccepted = false; _iAmHost = false; _oppName = "";
        if (opponentInfoGroup) opponentInfoGroup.SetActive(false);
        if (acceptButton) { acceptButton.gameObject.SetActive(false); acceptButton.interactable = true; }
        if (declineButton) { declineButton.gameObject.SetActive(false); declineButton.interactable = true; }
    }

    // =============== Search ===============

    void StartSearch()
    {
        if (!SteamManager.Initialized) { SetStatus("Steam 未初始化"); return; }
        _state = State.Searching; _iAccepted = false; _iAmHost = false; _oppName = ""; _pollTimer = 0;
        _lobbyID = default;
        SetStatus("匹配中...");
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld_quick", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
        SteamMatchmaking.RequestLobbyList();
        StartCoroutine(SearchPhase());
    }

    // =============== Steam Callbacks ===============

    Callback<LobbyMatchList_t> _listCB;
    Callback<LobbyCreated_t> _createdCB;

    void OnEnable() { _listCB = Callback<LobbyMatchList_t>.Create(cb => OnLobbyList(cb)); _createdCB = Callback<LobbyCreated_t>.Create(cb => OnLobbyCreated(cb)); }
    void OnDisable() { _listCB?.Dispose(); _createdCB?.Dispose(); }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        if (_state != State.Searching || _iAmHost) return;
        if (cb.m_nLobbiesMatching == 0) return;
        _lobbyID = SteamMatchmaking.GetLobbyByIndex(0);
        SteamMatchmaking.JoinLobby(_lobbyID);
        Debug.Log($"[QuickMatch] 找到大厅 {_lobbyID}，加入");
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (_state != State.Searching || cb.m_eResult != EResult.k_EResultOK) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_quick");
        WriteMyData("host_data");
        Debug.Log($"[QuickMatch] 自建大厅: {_lobbyID}");
    }

    // This runs via Update, not callback — simpler and more reliable
    // When JoinLobby succeeds, SteamMatchmaking.GetLobbyData starts working

    void WriteMyData(string key)
    {
        if (_lobbyID.m_SteamID == 0) return;
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        SteamMatchmaking.SetLobbyData(_lobbyID, key,
            JsonUtility.ToJson(new QMPD { playerName = sd?.localPlayerName ?? "玩家", totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 }));
    }

    // =============== Update ===============

    void Update()
    {
        if (_state == State.Idle || _lobbyID.m_SteamID == 0) return;

        // reduce poll rate
        _pollTimer += Time.deltaTime;
        if (_pollTimer < 0.3f) return;
        _pollTimer = 0;

        // Guest who just joined: write data & read host
        if (!_iAmHost && _state == State.Searching)
        {
            WriteMyData("guest_data");
        }

        // Both: try to read opponent
        if (_state == State.Searching)
        {
            string oppJson = _iAmHost
                ? SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data")
                : SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
            if (!string.IsNullOrEmpty(oppJson)) TryShowOpponent(oppJson);
        }

        // Countdown
        if (_state == State.Found)
        {
            _countdown -= Time.deltaTime;
            SetStatus($"等待确认（{_countdown:F0}s）");
            if (_countdown <= 0) { LeaveLobby(); ResetState(); StartSearch(); return; }
        }

        // Check reject / both accepted
        string hostOk = SteamMatchmaking.GetLobbyData(_lobbyID, "host_ok") ?? "";
        string guestOk = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_ok") ?? "";
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

        // Host: no guest after 8s → recreate (stale lobby)
        if (_iAmHost && _state == State.Searching && _lobbyID.m_SteamID != 0)
        {
            int members = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
            if (members >= 2 && string.IsNullOrEmpty(SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data")))
            {
                // someone joined but didn't write data; wait
            }
        }
    }

    // =============== Search timeout → become host ===============

    IEnumerator SearchPhase()
    {
        for (float t = 0; t < 2f && _state == State.Searching && _lobbyID.m_SteamID == 0; t += 0.5f)
        {
            yield return new WaitForSeconds(0.5f);
            if (_lobbyID.m_SteamID != 0) yield break; // found a lobby
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld_quick", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
            SteamMatchmaking.RequestLobbyList();
        }
        if (_state != State.Searching || _lobbyID.m_SteamID != 0) yield break;

        // No lobby found, become host
        _iAmHost = true;
        SetStatus("匹配中...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
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
