using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

/// <summary>
/// 加入房间面板——输入房间号，搜到显示房主信息，确认加入后等房主开始。
/// </summary>
public class JoinRoomPanel : MonoBehaviour
{
    public static JoinRoomPanel Instance { get; private set; }

    [Header("面板")]
    public GameObject panelRoot;

    [Header("输入")]
    public TMP_InputField roomCodeInput;

    [Header("房主信息（搜到才显示）")]
    public GameObject hostInfoGroup;
    public RawImage hostAvatar;
    public TMP_Text hostNameText;
    public TMP_Text hostStatsText;

    [Header("提示")]
    public TMP_Text statusText;

    [Header("按钮")]
    public Button joinButton;
    public Button cancelButton;

    private bool _searching;
    private CSteamID _foundLobbyID;
    private Callback<LobbyMatchList_t> _lobbyListCB;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (hostInfoGroup != null) hostInfoGroup.SetActive(false);
        if (joinButton != null) joinButton.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
        if (joinButton != null) joinButton.onClick.AddListener(Join);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        if (roomCodeInput != null) roomCodeInput.onValueChanged.AddListener(OnInputChanged);
    }

    public void Open()
    {
        if (!SteamManager.Initialized) return;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (hostInfoGroup != null) hostInfoGroup.SetActive(false);
        if (joinButton != null) joinButton.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
        if (roomCodeInput != null) roomCodeInput.text = "";
        _foundLobbyID = default;
        _searching = false;
    }

    void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ===================== 自动搜索 =====================

    void OnInputChanged(string value)
    {
        if (value.Length != 6) return;
        if (_searching) return;
        _searching = true;

        if (hostInfoGroup != null) hostInfoGroup.SetActive(false);
        if (joinButton != null) joinButton.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);

        DisposeCallbacks();
        _lobbyListCB = Callback<LobbyMatchList_t>.Create(OnLobbyList);
        SteamMatchmaking.AddRequestLobbyListStringFilter("room_code", value, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();

        StartCoroutine(SearchTimeout());
    }

    IEnumerator SearchTimeout()
    {
        yield return new WaitForSeconds(5f);
        if (!_searching) yield break;
        ShowStatus("未找到该房间，请检查房间号");
        _searching = false;
    }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        if (!_searching) return;
        if (cb.m_nLobbiesMatching == 0) { _searching = false; return; }

        CSteamID lid = SteamMatchmaking.GetLobbyByIndex(0);

        // 检查是否满员
        if (SteamMatchmaking.GetNumLobbyMembers(lid) >= 2)
        {
            ShowStatus("该房间已满，无法加入");
            _searching = false;
            return;
        }

        _foundLobbyID = lid;
        _searching = false;

        // 读取房主数据
        string hostJson = SteamMatchmaking.GetLobbyData(lid, "host_data");
        ShowHostInfo(hostJson);
        if (joinButton != null) joinButton.gameObject.SetActive(true);
    }

    void ShowHostInfo(string json)
    {
        var data = JsonUtility.FromJson<RoomPlayerData>(json);
        if (data == null) return;

        if (hostInfoGroup != null) hostInfoGroup.SetActive(true);
        if (hostNameText != null) hostNameText.text = data.playerName;
        if (hostStatsText != null)
            hostStatsText.text = $"总场数：{data.totalMatches}  胜率：{data.winRate:F1}%  连胜数：{data.winStreak}";
        if (data.steamID != 0 && hostAvatar != null)
            LoadAvatar(hostAvatar, data.steamID);
    }

    void ShowStatus(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
            statusText.gameObject.SetActive(true);
        }
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

    // ===================== 加入 =====================

    void Join()
    {
        if (_foundLobbyID.m_SteamID == 0) return;
        if (SteamMatchmaking.GetNumLobbyMembers(_foundLobbyID) >= 2)
        {
            ShowStatus("该房间已满，无法加入");
            return;
        }

        var sd = SteamDataManager.Instance;
        var d = sd?.playerData;
        var myData = new RoomPlayerData
        {
            playerName = sd?.localPlayerName ?? "玩家",
            totalMatches = d?.totalMatches ?? 0,
            winRate = sd?.WinRate ?? 0,
            winStreak = d?.winStreak ?? 0,
            steamID = sd?.localSteamID.m_SteamID ?? 0
        };
        SteamMatchmaking.SetLobbyData(_foundLobbyID, "guest_data", JsonUtility.ToJson(myData));
        SteamMatchmaking.SetLobbyData(_foundLobbyID, "left", "");
        SteamMatchmaking.JoinLobby(_foundLobbyID);

        if (hostInfoGroup != null) hostInfoGroup.SetActive(false);
        if (joinButton != null) joinButton.gameObject.SetActive(false);
        StartCoroutine(WaitForStart());
    }

    IEnumerator WaitForStart()
    {
        ShowStatus("已加入，等待房主开始游戏...");

        while (_foundLobbyID.m_SteamID != 0)
        {
            yield return new WaitForSeconds(0.5f);

            if (SteamMatchmaking.GetLobbyData(_foundLobbyID, "kicked") == "1")
            {
                ShowStatus("你已被房主移出房间");
                _foundLobbyID = default;
                yield break;
            }

            if (SteamMatchmaking.GetNumLobbyMembers(_foundLobbyID) < 2)
            {
                ShowStatus("房主已离开");
                _foundLobbyID = default;
                yield break;
            }

            if (SteamMatchmaking.GetLobbyData(_foundLobbyID, "start") == "1")
            {
                LobbyConfig.FromLobby = true;
                LobbyConfig.IsHost = false;
                LobbyConfig.IsDirectIP = false;
                LobbyConfig.ServerIP = "";
                JoinGamePanel.Instance?.Open();
                _foundLobbyID = default;
                if (panelRoot != null) panelRoot.SetActive(false);
                yield break;
            }
        }
    }

    void OnDestroy() { DisposeCallbacks(); }

    void DisposeCallbacks() { _lobbyListCB?.Dispose(); }

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
