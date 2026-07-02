using UnityEngine;
using Mirror;
using TMPro;
using kcp2k;

public class AutoConnect : MonoBehaviour
{
    private TurnManager _turnManager;
    private GameObject _waitingUI;
    private NetworkManager _nm;

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
        if (LobbyConfig.IsHost)
        {
            SetText("主机已启动\n等待客户端连接...");
            _nm.StartHost();
        }
        else
        {
            string ip = string.IsNullOrEmpty(LobbyConfig.ServerIP) ? "127.0.0.1" : LobbyConfig.ServerIP;
            SetText("正在连接 " + ip + " ...");
            _nm.networkAddress = ip;
            _nm.StartClient();
        }
    }

    void EnsureKcpTransport()
    {
        if (_nm == null) return;

        // Strip ALL existing transport components — they may be crashy or incompatible
        var all = _nm.gameObject.GetComponents<Transport>();
        foreach (var t in all)
        {
            Debug.Log($"[AutoConnect] Removing transport: {t.GetType().Name}");
            DestroyImmediate(t as Object);
        }
        _nm.transport = null;
        Transport.active = null;

        var kcp = _nm.gameObject.AddComponent<KcpTransport>();
        kcp.Port = 7777;
        _nm.transport = kcp;
        Transport.active = kcp;
        Debug.Log("[AutoConnect] KcpTransport ready");
    }

    void CreateWaitingUI()
    {
        _waitingUI = new GameObject("NetworkWaiting");
        DontDestroyOnLoad(_waitingUI);
        var c = _waitingUI.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 999;
        _waitingUI.AddComponent<UnityEngine.UI.CanvasScaler>();
        _waitingUI.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var pnl = new GameObject("Panel"); pnl.transform.SetParent(_waitingUI.transform, false);
        var im = pnl.AddComponent<UnityEngine.UI.Image>(); im.color = new Color(0, 0, 0, 0.85f);
        var pr = pnl.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        var tgo = new GameObject("Text"); tgo.transform.SetParent(_waitingUI.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>(); tmp.fontSize = 28; tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
        var f = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF"); if (f != null) tmp.font = f;
        var tr = tgo.GetComponent<RectTransform>(); tr.anchorMin = new Vector2(0.05f, 0.1f); tr.anchorMax = new Vector2(0.95f, 0.9f); tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
    }

    void SetText(string msg) { var t = _waitingUI?.GetComponentInChildren<TextMeshProUGUI>(); if (t != null) t.text = msg; }
    void HideUI() { if (_waitingUI != null) _waitingUI.SetActive(false); }

    void OnConnected() { SetText(NetworkServer.active ? "对手已加入！\n即将开始..." : "已连接！\n等待房主开始..."); }
    void OnDisconnected() { SetText("连接断开\n请返回 Lobby 重试"); }
    void OnDestroy() { NetworkClient.OnConnectedEvent -= OnConnected; NetworkClient.OnDisconnectedEvent -= OnDisconnected; }

    void Update()
    {
        if (_waitingUI == null || !_waitingUI.activeSelf) return;
        if (_turnManager != null && _turnManager.enabled && NetworkTurnSync.Instance != null && NetworkTurnSync.Instance.gameStarted)
            _waitingUI.SetActive(false);
    }
}
