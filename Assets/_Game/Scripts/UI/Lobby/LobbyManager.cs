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
    /// <summary>Lobby 场景已有的 Steam 大厅 ID——Game 场景 AutoConnect 直接复用，省去重新创建/搜索。</summary>
    public static Steamworks.CSteamID CurrentLobbyID { get; set; }
    public static string HostSteamID { get; set; }
}

/// <summary>
/// Lobby UI — create/join rooms via Steam Matchmaking.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button quickMatchButton;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button viewCardsButton;
    public Button gameIntroButton;
    public Button returnButton;
    public Button leaveButton;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    private GameIntroPanel _gameIntro;

    void Awake()
    {
        // 确保 SteamDataManager 存在
        if (SteamDataManager.Instance == null)
        {
            var go = new GameObject("SteamDataManager");
            go.AddComponent<SteamDataManager>();
        }
    }

    void Start()
    {
        if (quickMatchButton != null) quickMatchButton.onClick.AddListener(() => QuickMatchPanel.Instance?.Open());
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
        CreateRoomPanel.Instance?.OpenAsHost();
    }

    public void JoinRoom()
    {
        JoinRoomPanel.Instance?.Open();
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
