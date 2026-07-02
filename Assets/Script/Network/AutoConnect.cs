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
    private string _myPublicIP = "";

    void Awake()
    {
        _nm = FindObjectOfType<NetworkManager>();
        _turnManager = FindObjectOfType<TurnManager>();
        CreateWaitingUI();
        if (!LobbyConfig.FromLobby) { HideUI(); return; }
        NetworkClient.OnConnectedEvent += OnConnected;
        NetworkClient.OnDisconnectedEvent += OnDisconnected;
        EnsureKcpTransport();
    }

    void Start()
    {
        if (!LobbyConfig.FromLobby) return;
        if (_turnManager != null) _turnManager.enabled = false;
        _startTime = Time.time;

        if (LobbyConfig.IsHost)
        {
            StartAsHost();
        }
        else if (!string.IsNullOrEmpty(LobbyConfig.ServerIP?.Trim()))
        {
            // IP filled → direct KCP connection
            SetText("正在连接 " + LobbyConfig.ServerIP + " ...");
            _nm.networkAddress = LobbyConfig.ServerIP;
            _nm.StartClient();
        }
        else
        {
            // IP empty → search Steam lobbies
            if (!SteamManager.Initialized) { Invoke(nameof(Start), 0.5f); return; }
            SetText("正在搜索可用房间...");
            RegisterSteamCallbacks();
            InvokeRepeating(nameof(SearchLobbies), 0f, 2f);
        }
    }

    // ── Transport ──
    void EnsureKcpTransport()
    {
        if (_nm == null) return;
        // Remove ALL transports — Edgegap, Fizzy, old KCP, everything
        var all = _nm.gameObject.GetComponents<Transport>();
        foreach (var t in all)
        {
            Debug.Log($"[AutoConnect] Removing transport: {t.GetType().FullName}");
            DestroyImmediate(t);
        }
        _nm.transport = null;
        Transport.active = null;

        var kcp = _nm.gameObject.AddComponent<KcpTransport>();
        kcp.Port = 7777;
        _nm.transport = kcp;
        Transport.active = kcp;
        Debug.Log("[AutoConnect] Clean KcpTransport ready");
    }

    // ── Host ──
    void StartAsHost()
    {
        StartCoroutine(FetchPublicIP(ip =>
        {
            // If the IP looks like a LAN address, ignore it — the real public IP
            // is behind a NAT we can't read. Don't put bad IP in lobby data.
            if (ip.StartsWith("127.") || ip.StartsWith("10.") || ip.StartsWith("192.168.") || ip.StartsWith("172."))
                _myPublicIP = "";
            else
                _myPublicIP = ip;

            if (!SteamManager.Initialized)
            {
                SetText(string.IsNullOrEmpty(_myPublicIP)
                    ? "主机已启动\nSteam 未初始化\n请手动分享你的 IP 给对方"
                    : "主机已启动\nSteam 未初始化\n你的IP: " + _myPublicIP);
                _nm.StartHost();
                return;
            }
            RegisterSteamCallbacks();
            SetText("正在创建房间...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
        }));
    }

    void OnLobbyCreated(LobbyCreated_t r)
    {
        if (r.m_eResult != EResult.k_EResultOK)
        {
            SetText(string.IsNullOrEmpty(_myPublicIP)
                ? "创建房间失败\n主机已启动\n请手动分享你的 IP 给对方"
                : "主机已启动\nIP: " + _myPublicIP);
            _nm.StartHost();
            return;
        }
        var lid = new CSteamID(r.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(lid, "game", "anotherworld");
        if (!string.IsNullOrEmpty(_myPublicIP))
            SteamMatchmaking.SetLobbyData(lid, "host_ip", _myPublicIP);
        SteamMatchmaking.SetLobbyData(lid, "host_port", "7777");
        SetText("房间已创建\n等待对手加入...");
        _nm.StartHost();
    }

    // ── Client lobby search ──
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
        var lobbyID = SteamMatchmaking.GetLobbyByIndex(0);
        string hostIP = SteamMatchmaking.GetLobbyData(lobbyID, "host_ip");
        string hostPort = SteamMatchmaking.GetLobbyData(lobbyID, "host_port");
        if (string.IsNullOrEmpty(hostIP)) { SetText("房间数据异常\n请手动输入IP"); return; }
        SetText("找到房间！\n正在连接 " + hostIP + ":" + hostPort + " ...");
        SteamMatchmaking.LeaveLobby(lobbyID);
        _nm.networkAddress = hostIP;
        _nm.StartClient();
    }

    // ── IP detection ──
    System.Collections.IEnumerator FetchPublicIP(System.Action<string> cb)
    {
        var req = UnityEngine.Networking.UnityWebRequest.Get("https://api.ipify.org");
        req.timeout = 5;
        req.certificateHandler = new BypassCert(); // some networks MITM HTTPS
        yield return req.SendWebRequest();
        if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            cb(req.downloadHandler.text.Trim());
        else
            cb("127.0.0.1");
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
        var pnl = new GameObject("Panel"); pnl.transform.SetParent(_waitingUI.transform, false);
        var im = pnl.AddComponent<UnityEngine.UI.Image>(); im.color = new Color(0,0,0,0.85f);
        var pr = pnl.GetComponent<RectTransform>(); pr.anchorMin=Vector2.zero; pr.anchorMax=Vector2.one; pr.offsetMin=Vector2.zero; pr.offsetMax=Vector2.zero;
        var tgo = new GameObject("Text"); tgo.transform.SetParent(_waitingUI.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>(); tmp.fontSize=28; tmp.color=Color.white; tmp.alignment=TextAlignmentOptions.Center;
        var f = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF"); if(f!=null) tmp.font=f;
        var tr = tgo.GetComponent<RectTransform>(); tr.anchorMin=new Vector2(0.05f,0.1f); tr.anchorMax=new Vector2(0.95f,0.9f); tr.offsetMin=Vector2.zero; tr.offsetMax=Vector2.zero;
    }
    void SetText(string msg){ var t=_waitingUI?.GetComponentInChildren<TextMeshProUGUI>(); if(t!=null) t.text=msg; }
    void HideUI(){ if(_waitingUI!=null) _waitingUI.SetActive(false); }

    // ── Steam callbacks ──
    Callback<LobbyCreated_t> _lobbyCreatedCB;
    Callback<LobbyMatchList_t> _lobbyListCB;
    void RegisterSteamCallbacks()
    {
        _lobbyCreatedCB?.Dispose(); _lobbyListCB?.Dispose();
        _lobbyCreatedCB = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyListCB = Callback<LobbyMatchList_t>.Create(OnLobbyList);
    }
    void OnDestroy()
    {
        _lobbyCreatedCB?.Dispose(); _lobbyListCB?.Dispose();
        NetworkClient.OnConnectedEvent -= OnConnected;
        NetworkClient.OnDisconnectedEvent -= OnDisconnected;
    }

    void OnConnected(){ SetText(NetworkServer.active?"对手已加入！\n即将开始...":"已连接！\n等待房主开始..."); }
    void OnDisconnected(){ SetText("连接断开\n请返回 Lobby 重试"); }
    void Update(){ if(_waitingUI==null||!_waitingUI.activeSelf)return; if(_turnManager!=null&&_turnManager.enabled&&NetworkTurnSync.Instance!=null&&NetworkTurnSync.Instance.gameStarted)_waitingUI.SetActive(false); }
}
