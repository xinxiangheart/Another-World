using UnityEngine;
using Mirror;
using TMPro;
using Steamworks;

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

        if (!SteamManager.Initialized)
        {
            SetText("Steam 未就绪\n请先启动 Steam 客户端\n或在输入框填写对方 IP");
            return;
        }

        if (LobbyConfig.IsHost)
        {
            SetText("正在创建房间...");
            SetupFizzy();
            RegisterCallbacks();
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
        }
        else
        {
            SetText("正在搜索可用房间...");
            SetupFizzy();
            RegisterCallbacks();
            InvokeRepeating(nameof(SearchLobbies), 0f, 2f);
        }
    }

    void SetupFizzy()
    {
        // Remove KCP, keep FizzySteamworks
        var all = _nm.gameObject.GetComponents<Transport>();
        bool hasFizzy = false;
        foreach (var t in all)
        {
            if (t.GetType().Name.Contains("Fizzy") || t.GetType().Name.Contains("Steam")) { hasFizzy = true; continue; }
            DestroyImmediate(t);
        }
        if (!hasFizzy)
        {
            Debug.LogError("[AutoConnect] FizzySteamworks not found on NetworkManager! Add it in the Inspector.");
        }
    }

    Callback<LobbyCreated_t> _lcb;
    Callback<LobbyMatchList_t> _llcb;
    Callback<LobbyEnter_t> _leb;
    void RegisterCallbacks()
    {
        _lcb?.Dispose(); _llcb?.Dispose(); _leb?.Dispose();
        _lcb = Callback<LobbyCreated_t>.Create(r =>
        {
            if (r.m_eResult != EResult.k_EResultOK) { SetText("创建房间失败"); return; }
            var lid = new CSteamID(r.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lid, "game", "anotherworld");
            SteamMatchmaking.SetLobbyData(lid, "host_sid", SteamUser.GetSteamID().m_SteamID.ToString());
            Debug.Log($"[AutoConnect] Lobby {lid}, host SteamID64: {SteamUser.GetSteamID().m_SteamID}");
            SetText("房间已创建\n等待对手加入...");
            _nm.StartHost();
        });
        _llcb = Callback<LobbyMatchList_t>.Create(r =>
        {
            if (r.m_nLobbiesMatching == 0) return;
            CancelInvoke(nameof(SearchLobbies));
            SteamMatchmaking.JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
            SetText("找到房间！\n正在加入...");
        });
        _leb = Callback<LobbyEnter_t>.Create(r =>
        {
            if (LobbyConfig.IsHost) return;
            var lid = new CSteamID(r.m_ulSteamIDLobby);
            string hostSid = SteamMatchmaking.GetLobbyData(lid, "host_sid");
            if (string.IsNullOrEmpty(hostSid))
                hostSid = SteamMatchmaking.GetLobbyOwner(lid).m_SteamID.ToString();
            Debug.Log($"[AutoConnect] LobbyEnter — host SteamID64={hostSid}");
            _nm.networkAddress = hostSid;
            SetText("已进入大厅\n正在连接 Steam P2P ...");
            Invoke(nameof(StartMirrorClient), 1.5f);
        });
    }

    void StartMirrorClient()
    {
        Debug.Log($"[AutoConnect] StartMirrorClient — transport={_nm.transport?.GetType().Name}");
        _nm.StartClient();
    }

    void SearchLobbies()
    {
        if (NetworkClient.isConnected || NetworkServer.active) { CancelInvoke(nameof(SearchLobbies)); return; }
        if (Time.time - _startTime > 60f) { CancelInvoke(nameof(SearchLobbies)); SetText("搜索超时"); return; }
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", "anotherworld", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
    }

    void CreateWaitingUI()
    {
        _waitingUI = new GameObject("NetworkWaiting"); DontDestroyOnLoad(_waitingUI);
        var c = _waitingUI.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 999;
        _waitingUI.AddComponent<UnityEngine.UI.CanvasScaler>(); _waitingUI.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var p = new GameObject("Panel"); p.transform.SetParent(_waitingUI.transform, false);
        p.AddComponent<UnityEngine.UI.Image>().color = new Color(0,0,0,0.85f);
        var pr = p.GetComponent<RectTransform>(); pr.anchorMin=Vector2.zero; pr.anchorMax=Vector2.one; pr.offsetMin=Vector2.zero; pr.offsetMax=Vector2.zero;
        var t = new GameObject("Text"); t.transform.SetParent(_waitingUI.transform, false);
        var tmp = t.AddComponent<TextMeshProUGUI>(); tmp.fontSize=26; tmp.color=Color.white; tmp.alignment=TextAlignmentOptions.Center;
        var f = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF"); if(f!=null) tmp.font=f;
        var tr = t.GetComponent<RectTransform>(); tr.anchorMin=new Vector2(0.05f,0.1f); tr.anchorMax=new Vector2(0.95f,0.9f); tr.offsetMin=Vector2.zero; tr.offsetMax=Vector2.zero;
    }
    void SetText(string m) { var t=_waitingUI?.GetComponentInChildren<TextMeshProUGUI>(); if(t!=null) t.text=m; }
    void HideUI() { if(_waitingUI!=null) _waitingUI.SetActive(false); }
    void OnConnected(){ SetText(NetworkServer.active?"对手已加入！\n即将开始...":"已连接！\n等待房主开始..."); }
    void OnDisconnected(){ SetText("连接断开\n请返回 Lobby 重试"); }
    void OnDestroy(){ _lcb?.Dispose(); _llcb?.Dispose(); _leb?.Dispose(); NetworkClient.OnConnectedEvent-=OnConnected; NetworkClient.OnDisconnectedEvent-=OnDisconnected; }
    void Update(){ if(_waitingUI==null||!_waitingUI.activeSelf)return; if(_turnManager!=null&&_turnManager.enabled&&NetworkTurnSync.Instance!=null&&NetworkTurnSync.Instance.gameStarted)_waitingUI.SetActive(false); }
}
