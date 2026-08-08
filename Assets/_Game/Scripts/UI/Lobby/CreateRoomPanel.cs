using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

/// <summary>
/// 创建房间面板（纯房主视角）。
/// 加入房间有单独的 JoinRoomPanel。
/// </summary>
public class CreateRoomPanel : MonoBehaviour
{
    public static CreateRoomPanel Instance { get; private set; }

    [Header("面板")]
    public GameObject panelRoot;

    [Header("房间号")]
    public TMP_Text roomCodeText;

    [Header("房主信息")]
    public RawImage hostAvatar;
    public TMP_Text hostNameText;
    public TMP_Text hostStatsText;

    [Header("对方信息")]
    public GameObject guestInfoGroup;
    public RawImage guestAvatar;
    public TMP_Text guestNameText;
    public TMP_Text guestStatsText;

    [Header("按钮")]
    public Button kickButton;
    public Button startGameButton;
    public Button leaveButton;

    private CSteamID _lobbyID;
    private Callback<LobbyCreated_t> _lobbyCreatedCB;
    private Callback<LobbyDataUpdate_t> _lobbyDataCB;
    private bool _hasGuest;
    private string _roomCode;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (guestInfoGroup != null) guestInfoGroup.SetActive(false);
        if (kickButton != null) kickButton.gameObject.SetActive(false);
        if (kickButton != null) kickButton.onClick.AddListener(KickGuest);
        if (startGameButton != null) startGameButton.onClick.AddListener(StartGame);
        if (leaveButton != null) leaveButton.onClick.AddListener(LeaveRoom);
    }

    public void Open()
    {
        if (!SteamManager.Initialized) return;

        _hasGuest = false;
        _roomCode = Random.Range(100000, 999999).ToString();

        if (panelRoot != null) panelRoot.SetActive(true);
        if (guestInfoGroup != null) guestInfoGroup.SetActive(false);
        if (kickButton != null) kickButton.gameObject.SetActive(false);
        if (startGameButton != null) startGameButton.gameObject.SetActive(false);
        if (roomCodeText != null) roomCodeText.text = $"房间号：{_roomCode}";

        FillHostInfo();
        RegisterCallbacks();
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    void RegisterCallbacks()
    {
        DisposeCallbacks();
        _lobbyCreatedCB = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyDataCB = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
    }

    void DisposeCallbacks()
    {
        _lobbyCreatedCB?.Dispose();
        _lobbyDataCB?.Dispose();
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (cb.m_eResult != EResult.k_EResultOK) return;
        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_room");
        SteamMatchmaking.SetLobbyData(_lobbyID, "room_code", _roomCode);
        SetMyData();
    }

    void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (_lobbyID.m_SteamID != cb.m_ulSteamIDLobby) return;
        if (_hasGuest) return;

        string guestJson = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data");
        if (string.IsNullOrEmpty(guestJson)) return;

        FillGuestInfo(guestJson);
        if (guestInfoGroup != null) guestInfoGroup.SetActive(true);
        if (kickButton != null) kickButton.gameObject.SetActive(true);
        if (startGameButton != null) startGameButton.gameObject.SetActive(true);
        _hasGuest = true;
    }

    void Update()
    {
        if (_lobbyID.m_SteamID == 0) return;

        // 对方离开
        if (_hasGuest && SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2)
        {
            _hasGuest = false;
            if (guestInfoGroup != null) guestInfoGroup.SetActive(false);
            if (kickButton != null) kickButton.gameObject.SetActive(false);
            SteamMatchmaking.SetLobbyData(_lobbyID, "guest_data", "");
        }

        // 对方被踢后重新设标志
        if (SteamMatchmaking.GetLobbyData(_lobbyID, "left") == "1")
        {
            _hasGuest = false;
            if (guestInfoGroup != null) guestInfoGroup.SetActive(false);
            if (kickButton != null) kickButton.gameObject.SetActive(false);
            SteamMatchmaking.SetLobbyData(_lobbyID, "left", "");
            SteamMatchmaking.SetLobbyData(_lobbyID, "guest_data", "");
        }
    }

    void SetMyData()
    {
        var sd = SteamDataManager.Instance;
        var d = sd?.playerData;
        var data = new RoomPlayerData
        {
            playerName = sd?.localPlayerName ?? "玩家",
            totalMatches = d?.totalMatches ?? 0,
            winRate = sd?.WinRate ?? 0,
            winStreak = d?.winStreak ?? 0,
            steamID = sd?.localSteamID.m_SteamID ?? 0
        };
        SteamMatchmaking.SetLobbyData(_lobbyID, "host_data", JsonUtility.ToJson(data));
    }

    void FillHostInfo()
    {
        var sd = SteamDataManager.Instance;
        if (hostAvatar != null && sd != null && sd.localAvatar != null)
            hostAvatar.texture = sd.localAvatar;
        if (hostNameText != null && sd != null)
            hostNameText.text = sd.localPlayerName;
        var d = sd?.playerData;
        if (hostStatsText != null && d != null)
            hostStatsText.text = $"总场数：{d.totalMatches}  胜率：{sd.WinRate:F1}%  连胜数：{d.winStreak}";
    }

    void FillGuestInfo(string json)
    {
        var data = JsonUtility.FromJson<RoomPlayerData>(json);
        if (data == null) return;
        if (guestNameText != null) guestNameText.text = data.playerName;
        if (guestStatsText != null)
            guestStatsText.text = $"总场数：{data.totalMatches}  胜率：{data.winRate:F1}%  连胜数：{data.winStreak}";
        if (data.steamID != 0 && guestAvatar != null)
            LoadAvatar(guestAvatar, data.steamID);
    }

    static void LoadAvatar(RawImage target, ulong steamID)
    {
        int ah = SteamFriends.GetLargeFriendAvatar(new CSteamID(steamID));
        if (ah <= 0 || !SteamUtils.GetImageSize(ah, out uint w, out uint h)) return;
        byte[] px = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(ah, px, (int)(w * h * 4))) return;
        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(px);
        var cols = tex.GetPixels();
        for (int y = 0; y < h / 2; y++)
            for (int x = 0; x < w; x++)
            { int top = y * (int)w + x, bot = ((int)h - 1 - y) * (int)w + x; var t = cols[top]; cols[top] = cols[bot]; cols[bot] = t; }
        tex.SetPixels(cols); tex.Apply();
        target.texture = tex;
    }

    void KickGuest() { SteamMatchmaking.SetLobbyData(_lobbyID, "kicked", "1"); }

    void StartGame()
    {
        SteamMatchmaking.SetLobbyData(_lobbyID, "start", "1");
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = true;
        LobbyConfig.IsDirectIP = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        JoinGamePanel.Instance?.Open();
    }

    void LeaveRoom()
    {
        if (_lobbyID.m_SteamID != 0)
            SteamMatchmaking.LeaveLobby(_lobbyID);
        _lobbyID = default;
        DisposeCallbacks();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnDestroy() { DisposeCallbacks(); }

    [System.Serializable]
    class RoomPlayerData
    {
        public string playerName;
        public int totalMatches;
        public double winRate;
        public int winStreak;
        public ulong steamID;
    }
}
