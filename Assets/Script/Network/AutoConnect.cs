using UnityEngine;
using Mirror;
using TMPro;
using Steamworks;
using kcp2k;

public class AutoConnect : MonoBehaviour
{
    private TurnManager _turnManager;
    private GameObject _waitingUI;
    private NetworkManager _nm;
    private float _startTime;

    void Awake()
    {
        _nm = FindObjectOfType<NetworkManager>();
        _turnManager = FindObjectOfType<TurnManager>();
        CreateWaitingUI();
        if (!LobbyConfig.FromLobby) { HideUI(); return; }
        NetworkClient.OnConnectedEvent += OnConnected;
        NetworkClient.OnDisconnectedEvent += OnDisconnected;
    }

    void Start()
    {
        if (!LobbyConfig.FromLobby) return;
        if (_turnManager != null) _turnManager.enabled = false;
        _startTime = Time.time;

        bool hasSteam = SteamManager.Initialized;
        bool filledIP = !string.IsNullOrEmpty(LobbyConfig.ServerIP?.Trim());

        if (filledIP)
        {
            // ── Direct KCP ──
            SetupKcp();
            if (LobbyConfig.IsHost)
            {
                SetText("主机已启动\n等待客户端连接...\nIP: " + LobbyConfig.ServerIP);
                _nm.StartHost();
            }
            else
            {
                SetText("正在连接 " + LobbyConfig.ServerIP + " ...");
                _nm.networkAddress = LobbyConfig.ServerIP;
                _nm.StartClient();
            }
            return;
        }

        // ── Steam + KCP ──
        // Steam Lobby finds each other, then we switch to KCP with the host's IP
        SetupKcp();

        if (!hasSteam)
        {
            // Steam not available but no IP either → can't do anything
            SetText("请先启动 Steam 客户端\n或在下方输入对方 IP 地址");
            return;
        }

        if (LobbyConfig.IsHost)
        {
            SetText("正在创建房间...");
            StartCoroutine(FetchIPThenCreateLobby());
        }
        else
        {
            SetText("正在搜索可用房间...");
            RegisterCallbacks();
            InvokeRepeating(nameof(SearchLobbies), 0f, 2f);
        }
    }

    // ── Transport ──

    void SetupKcp()
    {
        var all = _nm.gameObject.GetComponents<Transport>();
        foreach (var t in all) DestroyImmediate(t);
        _nm.transport = null;
        Transport.active = null;
        var kcp = _nm.gameObject.AddComponent<KcpTransport>();
        kcp.Port = 7777;
        _nm.transport = kcp;
        Transport.active = kcp;
    }

    // ── Host: get public IP → put in lobby data → start host ──

    System.Collections.IEnumerator FetchIPThenCreateLobby()
    {
        string ip = "";
        var req = UnityEngine.Networking.UnityWebRequest.Get("https://ipv4.ip.sb");
        req.timeout = 5;
        req.certificateHandler = new BypassCert();
        yield return req.SendWebRequest();
        if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            ip = req.downloadHandler.text.Trim();
            bool isLan = ip.StartsWith("127.") || ip.StartsWith("10.") || ip.StartsWith("192.168.") || ip.StartsWith("172.");
            if (isLan) ip = "";
        }

        RegisterCallbacks();
        _lobbyIP = ip;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
        if (!string.IsNullOrEmpty(ip))
            SetText("正在获取 IP...\n你的IP: " + ip);
        else
            SetText("正在创建 Steam 房间...\n（未能获取公网IP，\n同一局域网内可直连）");
    }

    string _lobbyIP = "";

    void OnLobbyCreated(LobbyCreated_t r)
    {
        if (r.m_eResult != EResult.k_EResultOK)
        {
            SetText("创建 Steam 房间失败\n主机已启动\nIP: " + _lobbyIP);
            _nm.StartHost();
            return;
        }
        var lid = new CSteamID(r.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(lid, "game", "anotherworld");
        SteamMatchmaking.SetLobbyData(lid, "host_ip", _lobbyIP);
        SteamMatchmaking.SetLobbyData(lid, "host_port", "7777");
        SetText("房间已创建\n等待对手加入...\n你的IP: " + _lobbyIP);
        _nm.StartHost();
    }

    // ── Client: search Steam lobbies → get host IP → KCP connect ──

    void SearchLobbies()
    {
        if (NetworkClient.isConnected || NetworkServer.active) { CancelInvoke(nameof(SearchLobbies)); return; }
        if (Time.time - _startTime > 60f) { CancelInvoke(nameof(SearchLobbies)); SetText("搜索超时\n请检查网络或手动输入IP"); return; }
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
    }

    void OnLobbyList(LobbyMatchList_t r)
    {
        if (r.m_nLobbiesMatching == 0) return;
        CancelInvoke(nameof(SearchLobbies));
        var lid = SteamMatchmaking.GetLobbyByIndex(0);
        string ip = SteamMatchmaking.GetLobbyData(lid, "host_ip");
        if (string.IsNullOrEmpty(ip)) { SetText("找到房间但无 IP 数据\n请手动输入对方 IP"); return; }
        SetText("找到房间！\n正在连接 " + ip + " ...");
        SteamMatchmaking.LeaveLobby(lid);
        _nm.networkAddress = ip;
        _nm.StartClient();
    }

    class BypassCert : UnityEngine.Networking.CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] cert) => true;
    }

    // ── UI ──

    void CreateWaitingUI()
    {
        _waitingUI = new GameObject("NetworkWaiting"); DontDestroyOnLoad(_waitingUI);
        var c = _waitingUI.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 999;
        _waitingUI.AddComponent<UnityEngine.UI.CanvasScaler>(); _waitingUI.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var p = new GameObject("Panel"); p.transform.SetParent(_waitingUI.transform, false);
        p.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.85f);
        var pr = p.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        var t = new GameObject("Text"); t.transform.SetParent(_waitingUI.transform, false);
        var tmp = t.AddComponent<TextMeshProUGUI>(); tmp.fontSize = 26; tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
        var f = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF"); if (f != null) tmp.font = f;
        var tr = t.GetComponent<RectTransform>(); tr.anchorMin = new Vector2(0.05f, 0.1f); tr.anchorMax = new Vector2(0.95f, 0.9f); tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
    }
    void SetText(string msg) { var t = _waitingUI?.GetComponentInChildren<TextMeshProUGUI>(); if (t != null) t.text = msg; }
    void HideUI() { if (_waitingUI != null) _waitingUI.SetActive(false); }

    // ── Callbacks ──

    Callback<LobbyCreated_t> _lcb;
    Callback<LobbyMatchList_t> _llcb;
    void RegisterCallbacks() { _lcb?.Dispose(); _llcb?.Dispose(); _lcb = Callback<LobbyCreated_t>.Create(OnLobbyCreated); _llcb = Callback<LobbyMatchList_t>.Create(OnLobbyList); }

    void OnConnected() { SetText(NetworkServer.active ? "对手已加入！\n即将开始..." : "已连接！\n等待房主开始..."); }
    void OnDisconnected() { SetText("连接断开\n请返回 Lobby 重试"); }

    void OnDestroy()
    {
        _lcb?.Dispose(); _llcb?.Dispose();
        NetworkClient.OnConnectedEvent -= OnConnected;
        NetworkClient.OnDisconnectedEvent -= OnDisconnected;
    }

    void Update()
    {
        if (_waitingUI == null || !_waitingUI.activeSelf) return;
        if (_turnManager != null && _turnManager.enabled && NetworkTurnSync.Instance != null && NetworkTurnSync.Instance.gameStarted)
            _waitingUI.SetActive(false);
    }
}
