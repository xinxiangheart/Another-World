using UnityEngine;
using Mirror;
using TMPro;
using Steamworks;

/// <summary>
/// Reads LobbyConfig, starts Host or Client using FizzySteamworks,
/// shows waiting overlay, and enables TurnManager when both players are ready.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AutoConnect : MonoBehaviour
{
    private TurnManager _turnManager;
    private NetworkTurnSync _turnSync;
    private GameObject _waitingUI;
    private Callback<LobbyCreated_t> _lobbyCreatedCallback;
    private Callback<LobbyEnter_t> _lobbyEnterCallback;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
    private bool _lobbyReady;
    private ulong _myLobbyID;

    void Awake()
    {
        _turnManager = FindObjectOfType<TurnManager>();
        _turnSync = FindObjectOfType<NetworkTurnSync>();

        CreateWaitingUI();

        if (!LobbyConfig.FromLobby)
        {
            if (_waitingUI != null) _waitingUI.SetActive(false);
            Debug.Log("[AutoConnect] Standalone mode");
            return;
        }

        NetworkClient.OnConnectedEvent += OnConnected;
        NetworkClient.OnDisconnectedEvent += OnDisconnected;

        if (LobbyConfig.IsHost)
        {
            Invoke(nameof(StartAsHost), 0.1f);
        }
        else
        {
            Invoke(nameof(StartAsClient), 0.1f);
        }
    }

    void Start()
    {
        if (LobbyConfig.FromLobby && _turnManager != null)
        {
            _turnManager.enabled = false;
            Debug.Log("[AutoConnect] TurnManager disabled, waiting for both players");
        }
    }

    void OnEnable()
    {
        _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
    }

    void OnDisable()
    {
        _lobbyCreatedCallback?.Dispose();
        _lobbyEnterCallback?.Dispose();
        _gameLobbyJoinRequestedCallback?.Dispose();
    }

    // ========== Waiting UI ==========

    void CreateWaitingUI()
    {
        _waitingUI = new GameObject("NetworkWaiting");
        DontDestroyOnLoad(_waitingUI);
        Canvas c = _waitingUI.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 999;
        _waitingUI.AddComponent<UnityEngine.UI.CanvasScaler>();
        _waitingUI.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(_waitingUI.transform, false);
        var img = panel.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0.85f);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero;
        pr.offsetMax = Vector2.zero;

        GameObject txt = new GameObject("Text");
        txt.transform.SetParent(_waitingUI.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = "正在连接 Steam...";
        tmp.fontSize = 36;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF");
        if (font != null) tmp.font = font;
        var tr = txt.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0.4f);
        tr.anchorMax = new Vector2(1, 0.6f);
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
    }

    void SetText(string msg)
    {
        if (_waitingUI == null) return;
        var tmp = _waitingUI.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = msg;
    }

    // ========== Steam Lobby ==========

    void StartAsHost()
    {
        if (!SteamManager.Initialized)
        {
            SetText("等待 Steam 初始化...");
            Invoke(nameof(StartAsHost), 0.5f);
            return;
        }
        SetText("正在创建 Steam 大厅...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
    }

    void StartAsClient()
    {
        SetText("按 Shift+Tab 打开 Steam 好友列表\n右键好友 → 加入游戏\n\n或等主机邀请你");
    }

    void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult != EResult.k_EResultOK)
        {
            SetText("创建大厅失败: " + result.m_eResult);
            Debug.LogError("[AutoConnect] Lobby creation failed: " + result.m_eResult);
            return;
        }

        _myLobbyID = result.m_ulSteamIDLobby;
        CSteamID lobbyID = new CSteamID(_myLobbyID);
        Debug.Log($"[AutoConnect] Lobby created: {_myLobbyID}");
        SetText("大厅已创建！\n按 Shift+Tab 邀请好友加入");

        // Set joinable
        SteamMatchmaking.SetLobbyData(lobbyID, "game", "anotherworld");

        // Start Mirror host
        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm != null) nm.StartHost();

        _lobbyReady = true;
    }

    void OnLobbyEnter(LobbyEnter_t result)
    {
        if (LobbyConfig.IsHost) return; // host already started

        Debug.Log($"[AutoConnect] Entered lobby {result.m_ulSteamIDLobby}");
        _myLobbyID = result.m_ulSteamIDLobby;

        // Client joined lobby → start Mirror client
        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm != null)
        {
            SetText("已加入大厅！\n正在连接...");
            nm.StartClient();
        }

        _lobbyReady = true;
    }

    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
    {
        Debug.Log($"[AutoConnect] Friend invited to lobby {result.m_steamIDLobby}");
        LobbyConfig.IsHost = false;
        LobbyConfig.FromLobby = true;
        SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
    }

    // ========== Network Callbacks ==========

    void OnConnected()
    {
        if (!NetworkServer.active)
            SetText("已连接！\n等待房主开始游戏...");
        else
            SetText("对手已加入！\n即将开始游戏...");
    }

    void OnDisconnected()
    {
        SetText("连接断开\n请返回 Lobby 重试");
    }

    void OnDestroy()
    {
        NetworkClient.OnConnectedEvent -= OnConnected;
        NetworkClient.OnDisconnectedEvent -= OnDisconnected;

        if (_myLobbyID != 0 && NetworkServer.active)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(_myLobbyID));
        }
    }

    // ========== Update: hide waiting UI when TurnManager starts ==========

    void Update()
    {
        if (_waitingUI == null || !_waitingUI.activeSelf) return;
        if (_turnManager == null) return;

        if (_turnManager.enabled && NetworkTurnSync.Instance != null && NetworkTurnSync.Instance.gameStarted)
            _waitingUI.SetActive(false);
    }
}
