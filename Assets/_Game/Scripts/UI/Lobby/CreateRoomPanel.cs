using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

public class CreateRoomPanel : MonoBehaviour
{
    public static CreateRoomPanel Instance { get; private set; }

    [Header("面板")] public GameObject panelRoot;
    [Header("房间号")] public TMP_Text roomCodeText;
    [Header("房主信息")] public RawImage hostAvatar;
    public TMP_Text hostNameText, hostStatsText;
    [Header("对方信息")] public GameObject guestInfoGroup;
    public RawImage guestAvatar;
    public TMP_Text guestNameText, guestStatsText;
    [Header("按钮")] public Button kickButton, startGameButton, leaveButton;

    private bool _amHost;
    private CSteamID _lobbyID;
    private Callback<LobbyCreated_t> _lobbyCreatedCB;
    private Callback<LobbyDataUpdate_t> _lobbyDataCB;
    private bool _hasGuest;
    private string _roomCode;
    private string _hostJsonCache, _guestJsonCache;

    void Awake()
    {
        Instance = this;
        if (panelRoot) panelRoot.SetActive(false);
        if (guestInfoGroup) guestInfoGroup.SetActive(false);
        if (kickButton) kickButton.gameObject.SetActive(false);
        if (kickButton) kickButton.onClick.AddListener(KickGuest);
        if (startGameButton) startGameButton.onClick.AddListener(StartGame);
        if (leaveButton) leaveButton.onClick.AddListener(LeaveRoom);
    }

    // ============== 房主入口 ==============

    public void OpenAsHost()
    {
        if (!SteamManager.Initialized) return;
        _amHost = true; _hasGuest = false; _hostJsonCache = null; _guestJsonCache = null;
        _roomCode = Random.Range(100000, 999999).ToString();

        panelRoot.SetActive(true);
        guestInfoGroup.SetActive(false);
        kickButton.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        roomCodeText.text = $"房间号：{_roomCode}";
        leaveButton.gameObject.SetActive(true);

        FillMyInfo(hostAvatar, hostNameText, hostStatsText);
        RegisterCallbacks();
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    // ============== 客人入口（JoinRoomPanel 移交） ==============

    public void OpenAsGuest(CSteamID lobbyID, string roomCode)
    {
        if (!SteamManager.Initialized) return;
        _amHost = false; _hasGuest = true; _hostJsonCache = null; _guestJsonCache = null;
        _lobbyID = lobbyID; _roomCode = roomCode;

        panelRoot.SetActive(true);
        guestInfoGroup.SetActive(true);
        kickButton.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        roomCodeText.text = $"房间号：{_roomCode}";
        leaveButton.gameObject.SetActive(true);

        RegisterCallbacks();
        FillMyInfo(guestAvatar, guestNameText, guestStatsText);

        // Poll host data — 已在 OnLobbyEnter 写过 guest_data
        _hostJsonCache = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        if (!string.IsNullOrEmpty(_hostJsonCache)) FillHostInfo(_hostJsonCache);
    }

    // ============== Steam ==============

    void RegisterCallbacks() { DisposeCallbacks(); _lobbyCreatedCB = Callback<LobbyCreated_t>.Create(OnLobbyCreated); _lobbyDataCB = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate); }
    void DisposeCallbacks() { _lobbyCreatedCB?.Dispose(); _lobbyDataCB?.Dispose(); }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (!_amHost || cb.m_eResult != EResult.k_EResultOK) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_room");
        SteamMatchmaking.SetLobbyData(_lobbyID, "room_code", _roomCode);
        WriteMyData("host_data");
    }

    void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (_lobbyID.m_SteamID != cb.m_ulSteamIDLobby) return;
        if (_amHost) { string json = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data"); if (!string.IsNullOrEmpty(json) && !_hasGuest) { FillGuestInfo(json); guestInfoGroup.SetActive(true); kickButton.gameObject.SetActive(true); startGameButton.gameObject.SetActive(true); _hasGuest = true; } }
        else { string json = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data"); if (!string.IsNullOrEmpty(json)) FillHostInfo(json); }
    }

    void Update()
    {
        if (_lobbyID.m_SteamID == 0) return;

        // 客人侧：等待房主 start / 被踢 / 房主离开
        if (!_amHost)
        {
            if (SteamMatchmaking.GetLobbyData(_lobbyID, "kicked") == "1") { LeaveRoom(); return; }
            if (SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2) { LeaveRoom(); return; }
            if (SteamMatchmaking.GetLobbyData(_lobbyID, "start") == "1")
            {
                LobbyConfig.FromLobby = true; LobbyConfig.IsHost = false;
                LobbyConfig.IsDirectIP = false; LobbyConfig.ServerIP = "";
                panelRoot.SetActive(false); JoinGamePanel.Instance?.Open();
                return;
            }
        }
        else
        {
            // 房主侧：检测客人加入 / 客人离开
            if (!_hasGuest)
            {
                string guestJson = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data");
                if (!string.IsNullOrEmpty(guestJson))
                {
                    FillGuestInfo(guestJson);
                    guestInfoGroup.SetActive(true);
                    kickButton.gameObject.SetActive(true);
                    startGameButton.gameObject.SetActive(true);
                    _hasGuest = true;
                }
            }
            if (_hasGuest && SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2)
            {
                _hasGuest = false; guestInfoGroup.SetActive(false);
                kickButton.gameObject.SetActive(false); startGameButton.gameObject.SetActive(false);
                SteamMatchmaking.SetLobbyData(_lobbyID, "guest_data", "");
            }
        }
    }

    void WriteMyData(string key)
    {
        if (_lobbyID.m_SteamID == 0) return;
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        var json = JsonUtility.ToJson(new RoomPlayerData { playerName = sd?.localPlayerName ?? "玩家", totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 });
        SteamMatchmaking.SetLobbyData(_lobbyID, key, json);
    }

    // ============== UI ==============

    void FillMyInfo(RawImage avatar, TMP_Text nameText, TMP_Text statsText)
    {
        var sd = SteamDataManager.Instance;
        var d = sd?.playerData;
        if (avatar && sd?.localAvatar) avatar.texture = sd.localAvatar;
        if (nameText && sd) nameText.text = sd.localPlayerName;
        if (statsText && d != null) statsText.text = $"总场数：{d.totalMatches}  胜率：{sd.WinRate:F1}%  连胜数：{d.winStreak}";
    }

    void FillHostInfo(string json) { FillPlayerInfo(json, hostAvatar, hostNameText, hostStatsText); }
    void FillGuestInfo(string json) { FillPlayerInfo(json, guestAvatar, guestNameText, guestStatsText); }

    void FillPlayerInfo(string json, RawImage avatar, TMP_Text nameText, TMP_Text statsText)
    {
        var data = JsonUtility.FromJson<RoomPlayerData>(json);
        if (data == null) return;
        if (nameText) nameText.text = data.playerName;
        if (statsText) statsText.text = $"总场数：{data.totalMatches}  胜率：{data.winRate:F1}%  连胜数：{data.winStreak}";
        if (data.steamID != 0 && avatar) LoadAvatar(avatar, data.steamID);
    }

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

    // ============== 按钮 ==============

    void KickGuest() { SteamMatchmaking.SetLobbyData(_lobbyID, "kicked", "1"); }

    void StartGame()
    {
        SteamMatchmaking.SetLobbyData(_lobbyID, "start", "1");
        LobbyConfig.FromLobby = true; LobbyConfig.IsHost = _amHost;
        LobbyConfig.IsDirectIP = false; LobbyConfig.ServerIP = "";
        panelRoot.SetActive(false); JoinGamePanel.Instance?.Open();
    }

    void LeaveRoom()
    {
        if (_lobbyID.m_SteamID != 0) SteamMatchmaking.LeaveLobby(_lobbyID);
        _lobbyID = default; DisposeCallbacks(); panelRoot.SetActive(false);
    }

    void OnDestroy() { DisposeCallbacks(); }

    [System.Serializable] class RoomPlayerData { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
