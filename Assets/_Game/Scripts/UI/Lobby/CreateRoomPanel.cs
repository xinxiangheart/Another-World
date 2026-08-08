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
    private bool _hasGuest;
    private float _pollTimer;
    private string _roomCode;
    private Callback<LobbyCreated_t> _lcb;

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
        _amHost = true; _hasGuest = false; _pollTimer = 0;
        _roomCode = Random.Range(100000, 999999).ToString();

        panelRoot.SetActive(true);
        guestInfoGroup.SetActive(false);
        kickButton.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        roomCodeText.text = $"房间号：{_roomCode}";
        leaveButton.gameObject.SetActive(true);

        FillMyInfo(hostAvatar, hostNameText, hostStatsText);

        _lcb?.Dispose();
        _lcb = Callback<LobbyCreated_t>.Create(cb =>
        {
            if (cb.m_eResult != EResult.k_EResultOK) return;
            _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_room");
            SteamMatchmaking.SetLobbyData(_lobbyID, "room_code", _roomCode);
        });

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    // ============== 客人入口（JoinRoomPanel 移交） ==============

    public void OpenAsGuest(CSteamID lobbyID, string roomCode)
    {
        if (!SteamManager.Initialized) return;
        _amHost = false; _hasGuest = false; _pollTimer = 0;
        _lobbyID = lobbyID; _roomCode = roomCode;

        panelRoot.SetActive(true);
        guestInfoGroup.SetActive(true);
        kickButton.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        roomCodeText.text = $"房间号：{_roomCode}";
        leaveButton.gameObject.SetActive(true);

        FillMyInfo(guestAvatar, guestNameText, guestStatsText);

        // 尝试读房主信息
        string hostData = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        if (!string.IsNullOrEmpty(hostData)) FillHostInfo(hostData);
    }

    void Update()
    {
        if (_lobbyID.m_SteamID == 0) return;

        _pollTimer += Time.deltaTime;
        if (_pollTimer < 0.3f) return;
        _pollTimer = 0;

        // Host: ensure lobby data is written
        if (_amHost)
        {
            WriteMyData("host_data");
            SteamMatchmaking.SetLobbyData(_lobbyID, "room_code", _roomCode);
            SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_room");
        }
        // Guest: keep writing my data until host sees it
        else
        {
            WriteMyData("guest_data");
        }

        // Host: detect guest
        if (_amHost && !_hasGuest)
        {
            string guestJson = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data");
            if (!string.IsNullOrEmpty(guestJson))
            {
                FillGuestInfo(guestJson); guestInfoGroup.SetActive(true);
                kickButton.gameObject.SetActive(true); startGameButton.gameObject.SetActive(true);
                _hasGuest = true;
            }
        }

        // Guest: read host data
        if (!_amHost)
        {
            string hostJson = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
            if (!string.IsNullOrEmpty(hostJson)) FillHostInfo(hostJson);

            if (SteamMatchmaking.GetLobbyData(_lobbyID, "kicked") == "1") { LeaveRoom(); return; }
            if (SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2) { LeaveRoom(); return; }
            if (SteamMatchmaking.GetLobbyData(_lobbyID, "start") == "1")
            {
                LobbyConfig.FromLobby = true; LobbyConfig.IsHost = false;
                LobbyConfig.IsDirectIP = false; LobbyConfig.ServerIP = "";
                panelRoot.SetActive(false); JoinGamePanel.Instance?.Open();
            }
        }

        // Host: guest left
        if (_amHost && _hasGuest && SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2)
        {
            _hasGuest = false; guestInfoGroup.SetActive(false);
            kickButton.gameObject.SetActive(false); startGameButton.gameObject.SetActive(false);
            SteamMatchmaking.SetLobbyData(_lobbyID, "guest_data", "");
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
        _lobbyID = default; _lcb?.Dispose(); panelRoot.SetActive(false);
    }

    void OnDestroy() { _lcb?.Dispose(); }

    [System.Serializable] class RoomPlayerData { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
