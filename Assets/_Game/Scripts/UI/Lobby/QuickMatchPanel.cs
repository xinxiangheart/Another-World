using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

/// <summary>
/// 快速匹配面板——搜 Steam 大厅，搜不到就自建临时大厅等待。
/// 双方确认后设 LobbyConfig + MatchConfirmed=true，外部加载面板接管跳转。
/// </summary>
public class QuickMatchPanel : MonoBehaviour
{
    public static QuickMatchPanel Instance { get; private set; }
    public static bool MatchConfirmed { get; set; }

    /// <summary>对手名字（供 JoinGamePanel 读取）</summary>
    public string opponentName => opponentNameText != null ? opponentNameText.text : "";
    /// <summary>对手头像（供 JoinGamePanel 读取）</summary>
    public Texture opponentTexture => opponentAvatar != null ? opponentAvatar.texture : null;

    [Header("面板")]
    public GameObject panelRoot;

    [Header("状态文本")]
    public TMP_Text statusText;

    [Header("对手信息（匹配到才显示）")]
    public GameObject opponentInfoGroup;
    public RawImage opponentAvatar;
    public TMP_Text opponentNameText;
    public TMP_Text opponentStatsText;

    [Header("按钮")]
    public Button acceptButton;
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

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (opponentInfoGroup != null) opponentInfoGroup.SetActive(false);
        if (acceptButton != null) acceptButton.gameObject.SetActive(false);
        if (declineButton != null) declineButton.gameObject.SetActive(false);
        if (declineButton != null) declineButton.gameObject.SetActive(false);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAccept);
        if (declineButton != null) declineButton.onClick.AddListener(OnDecline);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        ResetState();
        StartSearch();
    }

    public void Close()
    {
        LeaveLobby();
        if (panelRoot != null) panelRoot.SetActive(false);
        _state = State.Idle;
    }

    void ResetState()
    {
        _state = State.Idle;
        _countdown = 15f;
        _iAccepted = false;
        _iAmHost = false;
        if (opponentInfoGroup != null) opponentInfoGroup.SetActive(false);
        if (acceptButton != null) acceptButton.gameObject.SetActive(false);
        if (declineButton != null) declineButton.gameObject.SetActive(false);
    }

    // ===================== 搜索 =====================

    void StartSearch()
    {
        if (!SteamManager.Initialized)
        {
            SetStatus("Steam 未初始化\n请从 Steam 启动游戏");
            return;
        }

        RegisterCallbacks();
        _state = State.Searching;
        _iAccepted = false;
        _iAmHost = false;
        SetStatus("匹配中...");

        // 搜匹配专用大厅
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld_quick", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
        SteamMatchmaking.RequestLobbyList();

        _searchCoroutine = StartCoroutine(SearchTimeout());
    }

    IEnumerator SearchTimeout()
    {
        yield return new WaitForSeconds(4f);
        if (_state != State.Searching) yield break;

        // 没搜到 → 自己建临时大厅等人
        _iAmHost = true;
        SetStatus("匹配中...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
        Debug.Log("[QuickMatch] 未搜到大厅，自建临时大厅等待");
    }

    // ===================== Steam Callbacks =====================

    void RegisterCallbacks()
    {
        DisposeCallbacks();
        _lobbyCreatedCB = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyListCB = Callback<LobbyMatchList_t>.Create(OnLobbyList);
        _lobbyEnterCB = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _lobbyDataCB = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
    }

    void DisposeCallbacks()
    {
        _lobbyCreatedCB?.Dispose();
        _lobbyListCB?.Dispose();
        _lobbyEnterCB?.Dispose();
        _lobbyDataCB?.Dispose();
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (_state != State.Searching) return;
        if (cb.m_eResult != EResult.k_EResultOK) { SetStatus("创建大厅失败"); return; }

        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobbyID, "game", "anotherworld_quick");
        SetMyLobbyData();
        Debug.Log($"[QuickMatch] 临时大厅已建: {_lobbyID}");
    }

    void OnLobbyList(LobbyMatchList_t cb)
    {
        if (_state != State.Searching) return;
        if (_iAmHost) return; // 自己是房主，不等列表回调
        if (cb.m_nLobbiesMatching == 0) return;

        var lid = SteamMatchmaking.GetLobbyByIndex(0);
        Debug.Log($"[QuickMatch] 找到大厅 {lid}，加入");
        SteamMatchmaking.JoinLobby(lid);
    }

    void OnLobbyEnter(LobbyEnter_t cb)
    {
        if (_state != State.Searching) return;

        _lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        bool wasHost = _iAmHost;

        // 如果对方建的大厅我加进来，则我不是 host
        if (!cb.m_bLocked && !_iAmHost)
        {
            Debug.Log($"[QuickMatch] 加入对方大厅: {_lobbyID}");
            SetMyLobbyData(); // 写入我的数据 → 对方 OnLobbyDataUpdate 收到
        }
        else if (_iAmHost)
        {
            // 我是房主，有人进来了
            Debug.Log($"[QuickMatch] 对手加入了我的大厅: {_lobbyID}");
            SetMyLobbyData(); // 刷新数据给对方
        }
    }

    void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (_lobbyID.m_SteamID != cb.m_ulSteamIDLobby) return;
        if (_state != State.Searching) return;

        string json = SteamMatchmaking.GetLobbyData(_lobbyID, "player_data");
        if (string.IsNullOrEmpty(json)) return;

        var oppData = JsonUtility.FromJson<QuickMatchPlayerData>(json);
        if (oppData == null || string.IsNullOrEmpty(oppData.playerName)) return;

        // 确认不是自己的数据（自己写的也会触发回调）
        var sd = SteamDataManager.Instance;
        if (oppData.playerName == (sd?.localPlayerName ?? "玩家")) return;

        Debug.Log($"[QuickMatch] 对手数据: {oppData.playerName}");

        _state = State.Found;
        _countdown = 15f;
        if (_searchCoroutine != null) { StopCoroutine(_searchCoroutine); _searchCoroutine = null; }

        if (opponentInfoGroup != null) opponentInfoGroup.SetActive(true);
        if (opponentNameText != null) opponentNameText.text = oppData.playerName;
        if (opponentStatsText != null)
            opponentStatsText.text = $"总场数：{oppData.totalMatches}  胜率：{oppData.winRate:F1}%  连胜数：{oppData.winStreak}";
        if (acceptButton != null) acceptButton.gameObject.SetActive(true);
        if (declineButton != null) declineButton.gameObject.SetActive(true);
        SetStatus($"等待确认（{_countdown:F0}s）");

        // 加载对手头像
        if (oppData.steamID != 0 && opponentAvatar != null)
        {
            int ah = SteamFriends.GetLargeFriendAvatar(new CSteamID(oppData.steamID));
            if (ah > 0 && SteamUtils.GetImageSize(ah, out uint w, out uint h))
            {
                byte[] px = new byte[w * h * 4];
                if (SteamUtils.GetImageRGBA(ah, px, (int)(w * h * 4)))
                {
                    var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(px);
                    var cols = tex.GetPixels();
                    for (int y = 0; y < h / 2; y++)
                        for (int x = 0; x < w; x++)
                        { int top = y * (int)w + x, bot = ((int)h - 1 - y) * (int)w + x; var t = cols[top]; cols[top] = cols[bot]; cols[bot] = t; }
                    tex.SetPixels(cols); tex.Apply();
                    opponentAvatar.texture = tex;
                }
            }
        }
    }

    void SetMyLobbyData()
    {
        if (_lobbyID.m_SteamID == 0) return;
        var sd = SteamDataManager.Instance;
        var d = sd?.playerData;
        var myData = new QuickMatchPlayerData
        {
            playerName = sd?.localPlayerName ?? "玩家",
            totalMatches = d?.totalMatches ?? 0,
            winRate = sd?.WinRate ?? 0,
            winStreak = d?.winStreak ?? 0,
            steamID = sd?.localSteamID.m_SteamID ?? 0
        };
        SteamMatchmaking.SetLobbyData(_lobbyID, "player_data", JsonUtility.ToJson(myData));
    }

    // ===================== 按钮 =====================

    void OnAccept()
    {
        if (_state != State.Found) return;
        _iAccepted = true;
        string key = _iAmHost ? "host_ok" : "guest_ok";
        SteamMatchmaking.SetLobbyData(_lobbyID, key, "1");
        if (acceptButton != null) acceptButton.interactable = false;
        if (declineButton != null) declineButton.gameObject.SetActive(false);
        _state = State.WaitingOpponent;
        SetStatus("已接受，等待对方确认");
    }

    void OnDecline()
    {
        if (_state != State.Found) return;
        string key = _iAmHost ? "host_ok" : "guest_ok";
        SteamMatchmaking.SetLobbyData(_lobbyID, key, "0"); // 0 = rejected
        LeaveLobby();
        Close();
    }

    void OnCancel()
    {
        // 如果已匹配到人，先通知对方拒绝
        if (_state == State.Found)
        {
            string key = _iAmHost ? "host_ok" : "guest_ok";
            SteamMatchmaking.SetLobbyData(_lobbyID, key, "0");
        }
        LeaveLobby();
        Close();
    }

    // ===================== Update =====================

    void Update()
    {
        // 倒计时
        if (_state == State.Found)
        {
            _countdown -= Time.deltaTime;
            SetStatus($"等待确认（{_countdown:F0}s）");

            // 检查对方是否已拒绝
            string oppKey = _iAmHost ? "guest_ok" : "host_ok";
            if (SteamMatchmaking.GetLobbyData(_lobbyID, oppKey) == "0")
            {
                SetStatus("对方已拒绝\n重新匹配...");
                LeaveLobby();
                ResetState();
                StartSearch();
                return;
            }

            if (_countdown <= 0)
            {
                SetStatus("超时，重新匹配...");
                LeaveLobby();
                ResetState();
                StartSearch();
            }
        }

        // 等待对方确认或双方都已接受？
        if (_state == State.WaitingOpponent)
        {
            string oppKey = _iAmHost ? "guest_ok" : "host_ok";
            string oppVal = SteamMatchmaking.GetLobbyData(_lobbyID, oppKey);
            if (oppVal == "0")
            {
                SetStatus("对方已拒绝\n重新匹配...");
                LeaveLobby();
                ResetState();
                StartSearch();
                return;
            }
        }

        if (_state == State.WaitingOpponent || (_iAccepted && _state == State.Found))
        {
            if (_lobbyID.m_SteamID != 0
                && SteamMatchmaking.GetLobbyData(_lobbyID, "host_ok") == "1"
                && SteamMatchmaking.GetLobbyData(_lobbyID, "guest_ok") == "1")
            {
                SetStatus("双方已接受！");
                MatchConfirmed = true;
                LobbyConfig.FromLobby = true;
                LobbyConfig.IsHost = _iAmHost;
                LobbyConfig.IsDirectIP = false;
                LobbyConfig.ServerIP = "";

                LeaveLobby();
                _state = State.Idle;
                if (panelRoot != null) panelRoot.SetActive(false);

                JoinGamePanel.Instance?.Open();
            }
        }
    }

    void LeaveLobby()
    {
        if (_lobbyID.m_SteamID != 0)
        {
            SteamMatchmaking.LeaveLobby(_lobbyID);
            _lobbyID = default;
        }
        DisposeCallbacks();
    }

    void SetStatus(string msg)
    {
        Debug.Log("[QuickMatch] " + msg.Replace("\n", " "));
        if (statusText != null) statusText.text = msg;
    }

    void OnDestroy() { DisposeCallbacks(); }

    [System.Serializable]
    class QuickMatchPlayerData
    {
        public string playerName;
        public int totalMatches;
        public double winRate;
        public int winStreak;
        public ulong steamID;
    }
}
