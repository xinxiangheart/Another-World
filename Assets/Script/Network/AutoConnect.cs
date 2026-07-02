using UnityEngine;
using Mirror;
using TMPro;
using Steamworks;
using kcp2k;

[DefaultExecutionOrder(-100)]
public class AutoConnect : MonoBehaviour
{
    private TurnManager _turnManager;
    private GameObject _waitingUI;
    private Callback<LobbyCreated_t> _lobbyCreatedCallback;
    private Callback<LobbyEnter_t> _lobbyEnterCallback;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
    private ulong _myLobbyID;
    private bool _useSteam;

    void Awake()
    {
        _turnManager = FindObjectOfType<TurnManager>();
        CreateWaitingUI();

        if (!LobbyConfig.FromLobby)
        {
            if (_waitingUI != null) _waitingUI.SetActive(false);
            Debug.Log("[AutoConnect] Standalone mode");
            return;
        }

        // IsDirectIP = manually typed IP → KCP. Otherwise → Steam matchmaking.
        _useSteam = !LobbyConfig.IsDirectIP;

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
        Callback<LobbyMatchList_t>.Create(OnLobbyList);
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
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        GameObject txt = new GameObject("Text");
        txt.transform.SetParent(_waitingUI.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 36; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF");
        if (font != null) tmp.font = font;
        var tr = txt.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0.4f); tr.anchorMax = new Vector2(1, 0.6f);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
    }

    void SetText(string msg)
    {
        if (_waitingUI == null) return;
        var tmp = _waitingUI.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = msg;
    }

    // ========== Start ==========

    void StartAsHost()
    {
        if (_useSteam && !SteamManager.Initialized)
        {
            Invoke(nameof(StartAsHost), 0.5f);
            return;
        }

        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null) return;

        if (_useSteam)
        {
            SetText("正在创建 Steam 大厅...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
        }
        else
        {
            SetText("本地主机已启动\n等待客户端连接...");
            EnsureKcpTransport(nm, 7777);
            nm.StartHost();
        }
    }

    void StartAsClient()
    {
        if (_useSteam && !SteamManager.Initialized)
        {
            Invoke(nameof(StartAsClient), 0.5f);
            return;
        }

        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null) return;

        if (_useSteam)
        {
            SetText("正在搜索可用大厅...");
            InvokeRepeating(nameof(SearchLobbies), 0f, 2f);
        }
        else
        {
            string ip = string.IsNullOrEmpty(LobbyConfig.ServerIP) ? "127.0.0.1" : LobbyConfig.ServerIP;
            SetText("正在连接 " + ip + " ...");
            EnsureKcpTransport(nm, 7777);
            nm.networkAddress = ip;
            nm.StartClient();
        }
    }

    void EnsureKcpTransport(NetworkManager nm, int port)
    {
        if (nm.transport != null && nm.transport.GetType().Name.Contains("Kcp"))
            return;

        KcpTransport kcp = nm.gameObject.AddComponent<KcpTransport>();
        kcp.Port = (ushort)port;
        nm.transport = kcp;
        Transport.active = kcp;
        Debug.Log($"[AutoConnect] KCP Transport set, port={port}");
    }

    // ========== Steam Lobby ==========

    void SearchLobbies()
    {
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
    }

    void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult != EResult.k_EResultOK)
        {
            SetText("创建大厅失败: " + result.m_eResult);
            return;
        }
        _myLobbyID = result.m_ulSteamIDLobby;
        CSteamID lobbyID = new CSteamID(_myLobbyID);
        SetText("等待对手加入...");
        SteamMatchmaking.SetLobbyData(lobbyID, "game", "anotherworld");
        FindObjectOfType<NetworkManager>()?.StartHost();
    }

    void OnLobbyList(LobbyMatchList_t result)
    {
        if (result.m_nLobbiesMatching == 0) { SetText("暂无可用房间\n正在继续搜索..."); return; }
        CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(0);
        SetText("找到房间！正在加入...");
        SteamMatchmaking.JoinLobby(lobbyID);
        CancelInvoke(nameof(SearchLobbies));
    }

    void OnLobbyEnter(LobbyEnter_t result)
    {
        if (LobbyConfig.IsHost) return;
        FindObjectOfType<NetworkManager>()?.StartClient();
    }

    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
    {
        LobbyConfig.IsHost = false; LobbyConfig.FromLobby = true;
        SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
    }

    // ========== Network Callbacks ==========

    void OnConnected()
    {
        SetText(NetworkServer.active ? "对手已加入！\n即将开始..." : "已连接！\n等待房主开始...");
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
            SteamMatchmaking.LeaveLobby(new CSteamID(_myLobbyID));
    }

    void Update()
    {
        if (_waitingUI == null || !_waitingUI.activeSelf) return;
        if (_turnManager == null) return;
        if (_turnManager.enabled && NetworkTurnSync.Instance != null && NetworkTurnSync.Instance.gameStarted)
            _waitingUI.SetActive(false);
    }
}
