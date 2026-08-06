using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public static class LobbyConfig
{
    public static bool IsHost { get; set; }
    public static string ServerIP { get; set; } = "";
    public static bool FromLobby { get; set; }
    public static bool IsDirectIP { get; set; }
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
    public Button returnButton;
    public Button leaveButton;

    [Header("Input")]
    public TMP_InputField ipInputField;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    private GameIntroPanel _gameIntro;

    void Start()
    {
        if (createRoomButton != null) createRoomButton.onClick.AddListener(CreateRoom);
        if (joinRoomButton != null) joinRoomButton.onClick.AddListener(JoinRoom);
        if (viewCardsButton != null) viewCardsButton.onClick.AddListener(() => SetStatus("卡牌浏览功能开发中"));
        if (gameIntroButton != null) gameIntroButton.onClick.AddListener(OpenGameIntro);
        if (returnButton != null) returnButton.onClick.AddListener(ReturnToWelcome);
        if (leaveButton != null) leaveButton.onClick.AddListener(LeaveGame);

        // Find GameIntroPanel in scene (disabled at start)
        _gameIntro = FindObjectOfType<GameIntroPanel>(true);
        SetStatus("欢迎来到异界");
    }

    public void CreateRoom()
    {
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = true;
        ApplyIP();
        SetStatus(LobbyConfig.IsDirectIP
            ? "正在创建本地房间..."
            : "正在创建 Steam 大厅...");
        SceneManager.LoadScene("Game");
    }

    public void JoinRoom()
    {
        LobbyConfig.FromLobby = true;
        LobbyConfig.IsHost = false;
        ApplyIP();
        SetStatus(LobbyConfig.IsDirectIP
            ? "正在连接 " + LobbyConfig.ServerIP + " ..."
            : "正在搜索 Steam 大厅...");
        SceneManager.LoadScene("Game");
    }

    void ApplyIP()
    {
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text.Trim()))
        {
            LobbyConfig.IsDirectIP = true;
            LobbyConfig.ServerIP = ipInputField.text.Trim();
        }
        else
        {
            LobbyConfig.IsDirectIP = false;
        }
    }

    public void ReturnToWelcome()
    {
        Debug.Log("[Lobby] ReturnToWelcome");
        SceneManager.LoadScene("Welcome");
    }

    public void LeaveGame()
    {
        Debug.Log("[Lobby] LeaveGame — quitting application");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetStatus(string msg)
    {
        Debug.Log("[Lobby] " + msg);
        if (statusText != null) statusText.text = msg;
    }

    void OpenGameIntro()
    {
        if (_gameIntro != null)
            _gameIntro.Open();
        else
            SetStatus("游戏介绍面板未找到");
    }
}
