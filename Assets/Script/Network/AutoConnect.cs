using UnityEngine;
using Mirror;
using kcp2k;
using TMPro;

[DefaultExecutionOrder(-100)]
public class AutoConnect : MonoBehaviour
{
    private TurnManager _turnManager;
    private NetworkTurnSync _turnSync;
    private GameObject _waitingUI;

    void Awake()
    {
        _turnManager = FindObjectOfType<TurnManager>();
        _turnSync = FindObjectOfType<NetworkTurnSync>();

        // Create waiting overlay
        CreateWaitingUI();

        // Read LobbyConfig
        if (LobbyConfig.IsHost)
        {
            Debug.Log("[AutoConnect] Starting as Host...");
            NetworkClient.OnConnectedEvent += OnConnectedToServer;
            Invoke(nameof(StartAsHost), 0.1f);
        }
        else if (LobbyConfig.FromLobby)
        {
            Debug.Log("[AutoConnect] Starting as Client, IP=" + LobbyConfig.ServerIP);
            NetworkClient.OnConnectedEvent += OnConnectedToServer;
            Invoke(nameof(StartAsClient), 0.1f);
        }
        else
        {
            // Standalone / Editor manual mode - hide waiting UI
            Debug.Log("[AutoConnect] Standalone mode");
            if (_waitingUI != null) _waitingUI.SetActive(false);
        }
    }

    void Start()
    {
        // Disable TurnManager until connected (client) or both ready (host)
        if (_turnManager != null && (_turnSync == null || !_turnSync.gameStarted))
        {
            _turnManager.enabled = false;
            Debug.Log("[AutoConnect] TurnManager disabled until game is ready");
        }
    }

    void CreateWaitingUI()
    {
        _waitingUI = new GameObject("NetworkWaiting");
        DontDestroyOnLoad(_waitingUI);
        Canvas canvas = _waitingUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _waitingUI.AddComponent<UnityEngine.UI.CanvasScaler>();
        _waitingUI.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(_waitingUI.transform, false);
        var panelImg = panel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0, 0, 0, 0.85f);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject text = new GameObject("Text");
        text.transform.SetParent(_waitingUI.transform, false);
        var tmp = text.AddComponent<TextMeshProUGUI>();
        tmp.text = "正在连接服务器...";
        tmp.fontSize = 36;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.4f);
        textRect.anchorMax = new Vector2(1, 0.6f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    void StartAsHost()
    {
        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null) { Debug.LogError("[AutoConnect] No NM!"); return; }
        FixTransport(nm);
        nm.StartHost();
        SetWaitingText("已创建房间\n等待对手加入...");
    }

    void StartAsClient()
    {
        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null) { Debug.LogError("[AutoConnect] No NM!"); return; }
        nm.networkAddress = LobbyConfig.ServerIP;
        FixTransport(nm);
        nm.StartClient();
        SetWaitingText("正在连接 " + LobbyConfig.ServerIP + " ...");
    }

    void FixTransport(NetworkManager nm)
    {
        if (nm.transport != null && nm.transport.GetType().Name.Contains("Edgegap"))
        {
            KcpTransport kcp = FindObjectOfType<KcpTransport>();
            if (kcp == null)
            {
                kcp = nm.gameObject.AddComponent<KcpTransport>();
                kcp.Port = 7777;
            }
            nm.transport = kcp;
        }
    }

    void OnConnectedToServer()
    {
        if (!NetworkServer.active)
        {
            SetWaitingText("已连接！\n等待房主开始游戏...");
        }
        else
        {
            SetWaitingText("对手已加入！\n即将开始游戏...");
        }
    }

    void SetWaitingText(string msg)
    {
        if (_waitingUI == null) return;
        var tmp = _waitingUI.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = msg;
    }

    void OnDestroy()
    {
        NetworkClient.OnConnectedEvent -= OnConnectedToServer;
    }

    void Update()
    {
        if (_waitingUI == null) return;

        // Hide waiting UI when TurnManager starts
        if (_turnManager != null && _turnManager.enabled && _turnManager.HasGameStarted())
        {
            _waitingUI.SetActive(false);
            return;
        }

        // Hide when server has 2 players and TurnManager already enabled
        if (NetworkServer.active && _turnManager != null && _turnManager.enabled)
        {
            _waitingUI.SetActive(false);
        }
    }
}
