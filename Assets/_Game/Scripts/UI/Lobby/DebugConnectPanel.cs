using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 本地测试面板——按 Ctrl+Shift+D 打开/关闭，输入 IP 直连。
/// 挂在 LobbyManager 或任意常驻 GameObject 上（不能挂 panel 自身）。
/// </summary>
public class DebugConnectPanel : MonoBehaviour
{
    [Header("面板")]
    public GameObject panelRoot;

    [Header("输入")]
    public TMP_InputField ipInput;

    [Header("按钮")]
    public Button hostButton;
    public Button clientButton;
    public Button closeButton;

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (hostButton != null) hostButton.onClick.AddListener(StartHost);
        if (clientButton != null) clientButton.onClick.AddListener(StartClient);
        if (closeButton != null) closeButton.onClick.AddListener(() => panelRoot?.SetActive(false));
        if (ipInput != null) ipInput.text = "127.0.0.1";
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.D))
        {
            if (panelRoot != null) panelRoot.SetActive(!panelRoot.activeSelf);
        }
    }

    void StartHost()
    {
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = true;
        LobbyConfig.IsDirectIP = true;
        LobbyConfig.ServerIP = "127.0.0.1";
        if (panelRoot != null) panelRoot.SetActive(false);
        JoinGamePanel.Instance?.Open();
    }

    void StartClient()
    {
        string ip = ipInput != null ? ipInput.text.Trim() : "127.0.0.1";
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = false;
        LobbyConfig.IsDirectIP = true;
        LobbyConfig.ServerIP = ip;
        if (panelRoot != null) panelRoot.SetActive(false);
        JoinGamePanel.Instance?.Open();
    }
}
