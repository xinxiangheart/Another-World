using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public static class LobbyConfig
{
    public static bool IsHost { get; set; }
    public static string ServerIP { get; set; } = "127.0.0.1";
}

public class LobbyManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button viewCardsButton;
    public Button gameIntroButton;

    [Header("Input")]
    public TMP_InputField ipInputField;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    private NetworkManager _nm;
    private bool _transitioning;
    private bool _hostStarted;

    void Start()
    {
        if (createRoomButton != null) createRoomButton.onClick.AddListener(CreateRoom);
        if (joinRoomButton != null) joinRoomButton.onClick.AddListener(JoinRoom);

        _nm = FindObjectOfType<NetworkManager>();
        SetStatus("欢迎来到异界");
    }

    void OnEnable()
    {
        NetworkClient.OnConnectedEvent += OnClientConnected;
        NetworkClient.OnDisconnectedEvent += OnClientDisconnected;
    }

    void OnDisable()
    {
        NetworkClient.OnConnectedEvent -= OnClientConnected;
        NetworkClient.OnDisconnectedEvent -= OnClientDisconnected;
    }

    public void CreateRoom()
    {
        if (NetworkServer.active || NetworkClient.isConnected) return;
        if (_nm == null)
        {
            SetStatus("NetworkManager 未找到！");
            return;
        }

        _nm.StartHost();
        _hostStarted = true;
        HideButtons();
        SetStatus("已创建房间，等待对手加入...");
    }

    public void JoinRoom()
    {
        if (NetworkServer.active || NetworkClient.isConnected) return;
        if (_nm == null)
        {
            SetStatus("NetworkManager 未找到！");
            return;
        }

        string ip = ipInputField != null ? ipInputField.text.Trim() : "127.0.0.1";
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        _nm.networkAddress = ip;
        _nm.StartClient();
        HideButtons();
        SetStatus("正在连接 " + ip + " : 7777 ...");
    }

    void Update()
    {
        if (_transitioning) return;
        if (!NetworkServer.active) return;
        if (!_hostStarted) return;

        // Host: both players connected?
        if (NetworkServer.connections.Count >= 2)
        {
            _transitioning = true;
            SetStatus("对手已加入！即将进入游戏...");
            _nm.autoCreatePlayer = true;
            _nm.ServerChangeScene("Game");
        }
    }

    void OnClientConnected()
    {
        if (!NetworkServer.active)
            SetStatus("已连接到房间！等待房主开始游戏...");
    }

    void OnClientDisconnected()
    {
        if (_transitioning) return;
        SetStatus("连接断开，请重试。");
        ShowButtons();
        _hostStarted = false;
    }

    void SetStatus(string msg)
    {
        Debug.Log("[Lobby] " + msg);
        if (statusText != null) statusText.text = msg;
    }

    void HideButtons()
    {
        if (createRoomButton != null) createRoomButton.gameObject.SetActive(false);
        if (joinRoomButton != null) joinRoomButton.gameObject.SetActive(false);
        if (ipInputField != null) ipInputField.gameObject.SetActive(false);
    }

    void ShowButtons()
    {
        if (createRoomButton != null) createRoomButton.gameObject.SetActive(true);
        if (joinRoomButton != null) joinRoomButton.gameObject.SetActive(true);
        if (ipInputField != null) ipInputField.gameObject.SetActive(true);
    }
}
