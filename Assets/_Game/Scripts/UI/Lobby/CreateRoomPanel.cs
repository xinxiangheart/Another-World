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

    bool _amHost, _hasGuest;
    CSteamID _lobbyID;
    string _roomCode;
    float _writeTimer;
    Callback<LobbyCreated_t> _lcb;
    Callback<LobbyDataUpdate_t> _dataCB;

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

    // ======== 房主入口 ========

    public void OpenAsHost()
    {
        if (!SteamManager.Initialized) return;
        _amHost = true; _hasGuest = false;
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
            WriteMyData("host_data");
        });

        _dataCB?.Dispose();
        _dataCB = Callback<LobbyDataUpdate_t>.Create(OnDataUpdated);

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
        StartCoroutine(PollForGuest());
    }

    System.Collections.IEnumerator PollForGuest()
    {
        for (int i = 0; i < 40; i++)
        {
            yield return new WaitForSeconds(0.5f);
            if (_hasGuest || _lobbyID.m_SteamID == 0) yield break;
            string guestJson = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data");
            if (!string.IsNullOrEmpty(guestJson))
            {
                Debug.Log($"[Room-Host] PollForGuest: ★★★ found guest_data round {i} ★★★");
                FillGuestInfo(guestJson);
                guestInfoGroup.SetActive(true);
                kickButton.gameObject.SetActive(true);
                startGameButton.gameObject.SetActive(true);
                _hasGuest = true;
                yield break;
            }
        }
    }

    // ======== 客人入口 ========

    public void OpenAsGuest(CSteamID lobbyID, string roomCode)
    {
        if (!SteamManager.Initialized) return;
        _amHost = false; _hasGuest = false;
        _lobbyID = lobbyID; _roomCode = roomCode;
        Debug.Log($"[Room-Guest] ★ OpenAsGuest lobbyID={_lobbyID} roomCode={_roomCode}");

        panelRoot.SetActive(true);
        guestInfoGroup.SetActive(true);
        kickButton.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        roomCodeText.text = $"房间号：{_roomCode}";
        leaveButton.gameObject.SetActive(true);

        FillMyInfo(guestAvatar, guestNameText, guestStatsText);

        _dataCB?.Dispose();
        _dataCB = Callback<LobbyDataUpdate_t>.Create(OnDataUpdated);

        // 立即写入 guest_data + 启动快速重试（0.3s×3次）确保 Host 收到推送
        WriteMyData("guest_data");
        StartCoroutine(RetryWriteGuest());

        // Read host data immediately (host already wrote it)
        string hostData = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        if (!string.IsNullOrEmpty(hostData)) FillHostInfo(hostData);
    }

    // ======== Steam: the ONLY data refresh entry ========

    void OnDataUpdated(LobbyDataUpdate_t cb)
    {
        if (_lobbyID.m_SteamID == 0 || cb.m_ulSteamIDLobby != _lobbyID.m_SteamID) return;

        string guestJson = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data");
        string hostJson = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        Debug.Log($"[Room-{(_amHost?"Host":"Guest")}] LobbyDataUpdate! hasGuest={_hasGuest} guestJson={(string.IsNullOrEmpty(guestJson)?"empty":"SET")} hostJson={(string.IsNullOrEmpty(hostJson)?"empty":"SET")}");

        if (_amHost)
        {
            if (!string.IsNullOrEmpty(guestJson) && !_hasGuest)
            {
                Debug.Log($"[Room-Host] ★★★ 玩家加入房间！guestLen={guestJson.Length} ★★★");
                FillGuestInfo(guestJson);
                guestInfoGroup.SetActive(true);
                kickButton.gameObject.SetActive(true);
                startGameButton.gameObject.SetActive(true);
                _hasGuest = true;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(hostJson))
            {
                Debug.Log($"[Room-Guest] ★ 读取到房主信息");
                FillHostInfo(hostJson);
            }
        }
    }

    System.Collections.IEnumerator RetryWriteGuest()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.3f);
            if (_lobbyID.m_SteamID == 0 || _amHost) yield break;
            WriteMyData("guest_data");
        }
    }

    void WriteMyData(string key)
    {
        if (_lobbyID.m_SteamID == 0) return;
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        var name = sd?.localPlayerName ?? "玩家";
        SteamMatchmaking.SetLobbyData(_lobbyID, key,
            JsonUtility.ToJson(new RPD { playerName = name, totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 }));
        if (key == "guest_data")
            Debug.Log($"[Room-Guest] write guest_data name={name} steamID={sd?.localSteamID.m_SteamID} lobbyID={_lobbyID}");
        else if (key == "host_data")
            Debug.Log($"[Room-Host] write host_data name={name} lobbyID={_lobbyID}");
    }

    // ======== Update ========

    void Update()
    {
        if (_lobbyID.m_SteamID == 0) return;

        // Guest: write data + check start/kick/disconnect
        if (!_amHost)
        {
            // 高频写入（0.4s 一次，确保 Host 能收到）
            _writeTimer += Time.deltaTime;
            if (_writeTimer > 0.4f) { _writeTimer = 0; WriteMyData("guest_data"); }

            // 主动读房主信息（LobbyDataUpdate 回调不靠谱时兜底）
            if (!_hasGuest)
            {
                string hostJson = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
                if (!string.IsNullOrEmpty(hostJson)) FillHostInfo(hostJson);
            }

            if (SteamMatchmaking.GetLobbyData(_lobbyID, "kicked") == "1") { LeaveRoom(); return; }
            if (SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2) { LeaveRoom(); return; }
            if (SteamMatchmaking.GetLobbyData(_lobbyID, "start") == "1")
            {
                LobbyConfig.FromLobby = true; LobbyConfig.IsHost = false;
                LobbyConfig.IsDirectIP = false; LobbyConfig.ServerIP = "";
                panelRoot.SetActive(false); JoinGamePanel.Instance?.Open();
            }
        }
        // Host: poll for guest data every 0.4s (LobbyDataUpdate fallback)
        else if (!_hasGuest)
        {
            _writeTimer += Time.deltaTime;
            if (_writeTimer > 0.4f)
            {
                _writeTimer = 0;
                string guestJson = SteamMatchmaking.GetLobbyData(_lobbyID, "guest_data");
                Debug.Log($"[Room-Host] Poll guest_data: {(string.IsNullOrEmpty(guestJson)?"empty":"SET")}");
                if (!string.IsNullOrEmpty(guestJson))
                {
                    Debug.Log($"[Room-Host] ★★★ 玩家加入房间！guestLen={guestJson.Length} ★★★");
                    FillGuestInfo(guestJson);
                    guestInfoGroup.SetActive(true);
                    kickButton.gameObject.SetActive(true);
                    startGameButton.gameObject.SetActive(true);
                    _hasGuest = true;
                }
            }
        }
        // Host: guest left
        else if (_hasGuest && SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2)
        {
            _hasGuest = false; guestInfoGroup.SetActive(false);
            kickButton.gameObject.SetActive(false); startGameButton.gameObject.SetActive(false);
            SteamMatchmaking.SetLobbyData(_lobbyID, "guest_data", "");
        }
    }

    // ======== UI ========

    void FillMyInfo(RawImage avatar, TMP_Text nameText, TMP_Text statsText)
    {
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        if (avatar && sd?.localAvatar) avatar.texture = sd.localAvatar;
        if (nameText && sd) nameText.text = sd.localPlayerName;
        if (statsText && d != null) statsText.text = $"总场数：{d.totalMatches}  胜率：{sd?.WinRate ?? 0:F1}%  连胜数：{d.winStreak}";
    }

    void FillHostInfo(string json) { FillPlayerInfo(json, hostAvatar, hostNameText, hostStatsText); }
    void FillGuestInfo(string json) { FillPlayerInfo(json, guestAvatar, guestNameText, guestStatsText); }
    void FillPlayerInfo(string json, RawImage avatar, TMP_Text name, TMP_Text stats)
    {
        var d = JsonUtility.FromJson<RPD>(json);
        if (d == null) return;
        if (name) name.text = d.playerName;
        if (stats) stats.text = $"总场数：{d.totalMatches}  胜率：{d.winRate:F1}%  连胜数：{d.winStreak}";
        if (d.steamID != 0 && avatar) LoadAvatar(avatar, d.steamID);
    }

    static void LoadAvatar(RawImage target, ulong sid)
    {
        int ah = SteamFriends.GetLargeFriendAvatar(new CSteamID(sid));
        if (ah <= 0 || !SteamUtils.GetImageSize(ah, out uint w, out uint h)) return;
        byte[] px = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(ah, px, (int)(w * h * 4))) return;
        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false); tex.LoadRawTextureData(px);
        var cols = tex.GetPixels();
        for (int y = 0; y < h / 2; y++) for (int x = 0; x < w; x++) { int top = y * (int)w + x, bot = ((int)h - 1 - y) * (int)w + x; var t = cols[top]; cols[top] = cols[bot]; cols[bot] = t; }
        tex.SetPixels(cols); tex.Apply(); target.texture = tex;
    }

    // ======== 按钮 ========

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
        _lobbyID = default; _lcb?.Dispose(); _dataCB?.Dispose(); panelRoot.SetActive(false);
    }
    void OnDestroy() { _lcb?.Dispose(); _dataCB?.Dispose(); }

    [System.Serializable] class RPD { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
