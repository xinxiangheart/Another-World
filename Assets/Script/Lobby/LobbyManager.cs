using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public static class LobbyConfig
{
    public static bool IsHost { get; set; }
    public static string ServerIP { get; set; } = "127.0.0.1";
    public static bool FromLobby { get; set; }
}

/// <summary>
/// Lobby is a pure UI scene. Sets LobbyConfig, then loads Game.
/// Game scene's AutoConnect reads LobbyConfig and handles all networking.
/// </summary>
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

    void Start()
    {
        if (createRoomButton != null) createRoomButton.onClick.AddListener(CreateRoom);
        if (joinRoomButton != null) joinRoomButton.onClick.AddListener(JoinRoom);
        SetStatus("欢迎来到异界");
    }

    public void CreateRoom()
    {
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = true;
        LobbyConfig.ServerIP = "127.0.0.1";
        SetStatus("正在进入游戏房间...");
        SceneManager.LoadScene("Game");
    }

    public void JoinRoom()
    {
        string ip = "127.0.0.1";
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text.Trim()))
            ip = ipInputField.text.Trim();

        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = false;
        LobbyConfig.ServerIP = ip;
        SetStatus("正在加入游戏房间...");
        SceneManager.LoadScene("Game");
    }

    void SetStatus(string msg)
    {
        Debug.Log("[Lobby] " + msg);
        if (statusText != null) statusText.text = msg;
    }
}
