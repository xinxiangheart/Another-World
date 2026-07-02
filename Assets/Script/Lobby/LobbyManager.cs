using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public static class LobbyConfig
{
    public static bool IsHost { get; set; }
    public static string ServerIP { get; set; } = "127.0.0.1";
    public static bool FromLobby { get; set; }
    /// <summary>Steam Lobby ID for the client to join.</summary>
    public static ulong SteamLobbyID { get; set; }
}

/// <summary>
/// Lobby UI — create/join rooms via Steam Matchmaking.
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
        if (viewCardsButton != null) viewCardsButton.onClick.AddListener(() => SetStatus("卡牌浏览功能开发中"));
        if (gameIntroButton != null) gameIntroButton.onClick.AddListener(() => SetStatus("游戏介绍功能开发中"));
        SetStatus("欢迎来到异界");
    }

    public void CreateRoom()
    {
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = true;
        SetStatus("正在创建 Steam 大厅...");
        SceneManager.LoadScene("Game");
    }

    public void JoinRoom()
    {
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = false;
        SetStatus("正在搜索 Steam 大厅...");
        SceneManager.LoadScene("Game");
    }

    void SetStatus(string msg)
    {
        Debug.Log("[Lobby] " + msg);
        if (statusText != null) statusText.text = msg;
    }
}
