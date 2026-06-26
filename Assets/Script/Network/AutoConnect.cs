using UnityEngine;
using Mirror;
using kcp2k;
using TMPro;

/// <summary>
/// Reads LobbyConfig, starts Host or Client using Game scene's NetworkManager,
/// shows waiting overlay, and enables TurnManager when both players are ready.
/// </summary>
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

        CreateWaitingUI();

        if (!LobbyConfig.FromLobby)
        {
            // Standalone / Editor manual mode
            if (_waitingUI != null) _waitingUI.SetActive(false);
            Debug.Log("[AutoConnect] Standalone mode");
            return;
        }

        NetworkClient.OnConnectedEvent += OnConnected;
        NetworkClient.OnDisconnectedEvent += OnDisconnected;

        if (LobbyConfig.IsHost)
        {
            Debug.Log("[AutoConnect] Starting Host...");
            Invoke(nameof(StartAsHost), 0.1f);
        }
        else
        {
            Debug.Log("[AutoConnect] Starting Client -> " + LobbyConfig.ServerIP);
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
        tmp.text = "正在连接服务器...";
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

    // ========== Start Network ==========

    void StartAsHost()
    {
        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null) { Debug.LogError("[AutoConnect] No NetworkManager!"); return; }
        FixTransport(nm);
        nm.StartHost();
        SetText("已创建房间\n等待对手加入...");
    }

    void StartAsClient()
    {
        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm == null) { Debug.LogError("[AutoConnect] No NetworkManager!"); return; }
        FixTransport(nm);
        nm.networkAddress = LobbyConfig.ServerIP;
        Debug.Log($"[AutoConnect] Client: address={nm.networkAddress}, transport={nm.transport?.GetType().Name ?? "NULL"}, port={(nm.transport as kcp2k.KcpTransport)?.Port ?? 0}");
        nm.StartClient();
        SetText("正在连接 " + LobbyConfig.ServerIP + " ...");
    }

    void FixTransport(NetworkManager nm)
    {
        KcpTransport existing = nm.gameObject.GetComponent<KcpTransport>();
        bool needsReplacement = existing != null && existing.GetType().Name.Contains("Edgegap");

        if (needsReplacement)
        {
            Debug.Log($"[AutoConnect] Replacing EdgegapKcpTransport with plain KcpTransport");
            // Mirror holds static Transport.active ref — clear before destroy
            if (Transport.active == existing)
                Transport.active = null;
            DestroyImmediate(existing);
            nm.transport = null;
        }

        if (nm.transport == null)
        {
            KcpTransport kcp = nm.gameObject.AddComponent<KcpTransport>();
            kcp.Port = 7777;
            nm.transport = kcp;
            // Restore static reference so NetworkServer/NetworkClient can find it
            Transport.active = kcp;
        }

        Debug.Log($"[AutoConnect] Transport OK: {nm.transport.GetType().Name}, port={(nm.transport as KcpTransport)?.Port ?? 7777}");
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
