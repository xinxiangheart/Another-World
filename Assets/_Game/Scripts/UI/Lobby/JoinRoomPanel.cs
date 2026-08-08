using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

public class JoinRoomPanel : MonoBehaviour
{
    public static JoinRoomPanel Instance { get; private set; }

    [Header("面板")] public GameObject panelRoot;
    [Header("输入")] public TMP_InputField roomCodeInput;
    [Header("房主信息")] public GameObject hostInfoGroup;
    public RawImage hostAvatar;
    public TMP_Text hostNameText, hostStatsText;
    [Header("提示")] public TMP_Text statusText;
    [Header("按钮")] public Button joinButton, cancelButton;

    private bool _searching;
    private CSteamID _foundLobbyID;
    private string _foundRoomCode;
    private Callback<LobbyMatchList_t> _lobbyListCB;

    void Awake()
    {
        Instance = this;
        if (panelRoot) panelRoot.SetActive(false);
        if (hostInfoGroup) hostInfoGroup.SetActive(false);
        if (joinButton) joinButton.gameObject.SetActive(false);
        if (statusText) statusText.gameObject.SetActive(false);
        if (joinButton) joinButton.onClick.AddListener(Join);
        if (cancelButton) cancelButton.onClick.AddListener(Close);
        if (roomCodeInput) roomCodeInput.onValueChanged.AddListener(OnInputChanged);
    }

    public void Open()
    {
        if (!SteamManager.Initialized) return;
        panelRoot.SetActive(true);
        hostInfoGroup.SetActive(false);
        joinButton.gameObject.SetActive(false);
        statusText.gameObject.SetActive(false);
        if (roomCodeInput) roomCodeInput.text = "";
        _foundLobbyID = default;
        _foundRoomCode = "";
        _searching = false;
    }

    void Close() { panelRoot.SetActive(false); }

    void OnInputChanged(string value)
    {
        if (value.Length != 6 || _searching) return;
        _searching = true;
        hostInfoGroup.SetActive(false);
        joinButton.gameObject.SetActive(false);
        statusText.gameObject.SetActive(false);

        DisposeCallbacks();
        _lobbyListCB = Callback<LobbyMatchList_t>.Create(OnLobbyList);
        SteamMatchmaking.AddRequestLobbyListStringFilter("room_code", value, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
        StartCoroutine(SearchTimeout());
    }

    IEnumerator SearchTimeout() { yield return new WaitForSeconds(5f); if (_searching) { ShowStatus("未找到该房间，请检查房间号"); _searching = false; } }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        if (!_searching || cb.m_nLobbiesMatching == 0) { _searching = false; return; }
        CSteamID lid = SteamMatchmaking.GetLobbyByIndex(0);
        if (SteamMatchmaking.GetNumLobbyMembers(lid) >= 2) { ShowStatus("该房间已满，无法加入"); _searching = false; return; }
        _foundLobbyID = lid;
        _foundRoomCode = SteamMatchmaking.GetLobbyData(lid, "room_code");
        _searching = false;

        string hostJson = SteamMatchmaking.GetLobbyData(lid, "host_data");
        if (!string.IsNullOrEmpty(hostJson)) FillHost(hostJson);
        joinButton.gameObject.SetActive(true);
    }

    void FillHost(string json)
    {
        var data = JsonUtility.FromJson<RoomPlayerData>(json);
        if (data == null) return;
        hostInfoGroup.SetActive(true);
        if (hostNameText) hostNameText.text = data.playerName;
        if (hostStatsText) hostStatsText.text = $"总场数：{data.totalMatches}  胜率：{data.winRate:F1}%  连胜数：{data.winStreak}";
        if (data.steamID != 0 && hostAvatar) LoadAvatar(hostAvatar, data.steamID);
    }

    void ShowStatus(string msg) { if (statusText) { statusText.text = msg; statusText.gameObject.SetActive(true); } }

    void Join()
    {
        if (_foundLobbyID.m_SteamID == 0) return;
        if (SteamMatchmaking.GetNumLobbyMembers(_foundLobbyID) >= 2) { ShowStatus("该房间已满"); return; }

        panelRoot.SetActive(false);
        StartCoroutine(JoinDelayed());
    }

    IEnumerator JoinDelayed()
    {
        // 先加入大厅（成为成员后才有 SetLobbyData 权限）
        SteamMatchmaking.JoinLobby(_foundLobbyID);
        yield return new WaitForSeconds(0.3f);

        // 写入 guest data
        var sd = SteamDataManager.Instance; var d = sd?.playerData;
        var myData = new RoomPlayerData { playerName = sd?.localPlayerName ?? "玩家", totalMatches = d?.totalMatches ?? 0, winRate = sd?.WinRate ?? 0, winStreak = d?.winStreak ?? 0, steamID = sd?.localSteamID.m_SteamID ?? 0 };
        SteamMatchmaking.SetLobbyData(_foundLobbyID, "guest_data", JsonUtility.ToJson(myData));

        // 移交 CreateRoomPanel
        CreateRoomPanel.Instance?.OpenAsGuest(_foundLobbyID, _foundRoomCode);
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

    void OnDestroy() { DisposeCallbacks(); }
    void DisposeCallbacks() { _lobbyListCB?.Dispose(); }

    [System.Serializable] class RoomPlayerData { public string playerName; public int totalMatches; public double winRate; public int winStreak; public ulong steamID; }
}
