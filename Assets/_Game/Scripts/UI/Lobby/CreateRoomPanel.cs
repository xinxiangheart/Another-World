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

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    // ======== 客人入口 ========

    string MakeMyJson()
    {
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        return JsonUtility.ToJson(new RPD { playerName = sd?.localPlayerName ?? "玩家", totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 });
    }

    public void OpenAsGuest(CSteamID lobbyID, string roomCode)
    {
        if (!SteamManager.Initialized) return;
        _amHost = false; _hasGuest = false; _writeTimer = 0;
        _lobbyID = lobbyID; _roomCode = roomCode;

        panelRoot.SetActive(true);
        guestInfoGroup.SetActive(true);
        kickButton.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        roomCodeText.text = $"房间号：{_roomCode}";
        leaveButton.gameObject.SetActive(true);

        // 清空房主区域，防止残留上轮数据
        if (hostAvatar) hostAvatar.texture = null;
        if (hostNameText) hostNameText.text = "读取中...";
        if (hostStatsText) hostStatsText.text = "";

        // 客人自己的信息填入 guest 区
        FillMyInfo(guestAvatar, guestNameText, guestStatsText);

        // Guest writes via SetLobbyMemberData (only non-owner API that works)
        SteamMatchmaking.SetLobbyMemberData(_lobbyID, "player_data", MakeMyJson());
        StartCoroutine(RetryWriteGuest());

        // 尝试立即读房主数据
        string hostData = SteamMatchmaking.GetLobbyData(_lobbyID, "host_data");
        if (!string.IsNullOrEmpty(hostData)) FillHostInfo(hostData);
    }

    System.Collections.IEnumerator RetryWriteGuest()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.8f);
            if (_lobbyID.m_SteamID == 0 || _amHost) yield break;
            SteamMatchmaking.SetLobbyMemberData(_lobbyID, "player_data", MakeMyJson());
        }
    }

    void WriteMyData(string key)
    {
        if (_lobbyID.m_SteamID == 0) return;
        SteamMatchmaking.SetLobbyData(_lobbyID, key, MakeMyJson());
    }

    // ======== Update ========

    void Update()
    {
        if (_lobbyID.m_SteamID == 0) return;

        if (!_amHost)
        {
            // Guest: keep writing member data + check start/kick/disconnect
            _writeTimer += Time.deltaTime;
            if (_writeTimer > 0.5f)
            {
                _writeTimer = 0;
                SteamMatchmaking.SetLobbyMemberData(_lobbyID, "player_data", MakeMyJson());
                SteamMatchmaking.RequestLobbyData(_lobbyID); // 刷新缓存
            }

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
        else if (!_hasGuest)
        {
            // Host: read guest's SetLobbyMemberData
            _writeTimer += Time.deltaTime;
            if (_writeTimer > 0.5f)
            {
                _writeTimer = 0;
                // 刷新 lobby 缓存——Steam 的 GetLobbyMemberData 依赖本地缓存，
                // 不调 RequestLobbyData 会一直读到空数据
                SteamMatchmaking.RequestLobbyData(_lobbyID);
                int count = SteamMatchmaking.GetNumLobbyMembers(_lobbyID);
                for (int i = 0; i < count; i++)
                {
                    CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyID, i);
                    if (member == SteamUser.GetSteamID()) continue;
                    string guestJson = SteamMatchmaking.GetLobbyMemberData(_lobbyID, member, "player_data");
                    if (!string.IsNullOrEmpty(guestJson))
                    {
                        Debug.Log($"[Room-Host] ★★★ 玩家加入房间！ ★★★");
                        FillGuestInfo(guestJson);
                        guestInfoGroup.SetActive(true);
                        kickButton.gameObject.SetActive(true);
                        startGameButton.gameObject.SetActive(true);
                        _hasGuest = true;
                        break;
                    }
                }
            }
        }
        else if (_hasGuest && SteamMatchmaking.GetNumLobbyMembers(_lobbyID) < 2)
        {
            _hasGuest = false; guestInfoGroup.SetActive(false);
            kickButton.gameObject.SetActive(false); startGameButton.gameObject.SetActive(false);
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
        _lobbyID = default; _lcb?.Dispose(); panelRoot.SetActive(false);
    }
    void OnDestroy() { _lcb?.Dispose(); }

    [System.Serializable] class RPD { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
