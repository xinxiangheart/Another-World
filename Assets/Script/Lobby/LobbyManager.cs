using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public static class LobbyConfig
{
    /// <summary>True if player clicked "Create Room"</summary>
    public static bool IsHost { get; set; }
    /// <summary>Server IP (for client mode)</summary>
    public static string ServerIP { get; set; } = "127.0.0.1";
    /// <summary>True if we came from Lobby scene (always true after clicking any Lobby button)</summary>
    public static bool FromLobby { get; set; }
}

/// <summary>
/// Lobby is a pure UI scene. It sets LobbyConfig then loads Game.
/// All networking happens in Game scene via AutoConnect.
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
        SetStatus("正在进入房间...");
        SceneManager.LoadScene("Game");
    }

    public void JoinRoom()
    {
        string ip = ipInputField != null ? ipInputField.text.Trim() : "127.0.0.1";
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = false;
        LobbyConfig.ServerIP = ip;
        SetStatus("正在加入房间...");
        SceneManager.LoadScene("Game");
    }

    void SetStatus(string msg)
    {
        Debug.Log("[Lobby] " + msg);
        if (statusText != null) statusText.text = msg;
    }
}
