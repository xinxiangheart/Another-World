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
    /// <summary>AI 对战模式（离线单机）。设 true 且 FromLobby=false，走离线 Host + AI 对手。</summary>
    public static bool IsAI { get; set; }
    /// <summary>Lobby 场景已有的 Steam 大厅 ID。</summary>
    public static Steamworks.CSteamID CurrentLobbyID { get; set; }
    public static string HostSteamID { get; set; }
    /// <summary>对手 SteamID：Host 在读取大厅成员数据时捕获（Set 写入）；Client 读取时返回 HostSteamID；AI 对战为 null。</summary>
    static string _remoteSteamID;
    public static string RemoteSteamID
    {
        get => IsAI ? null : (IsHost ? _remoteSteamID : HostSteamID);
        set => _remoteSteamID = value;
    }
    /// <summary>唯一匹配 key——基于 Lobby 大厅 ID 生成，防止多组同时进 Game 串线到别人房间。</summary>
    public static string MatchKey { get; set; }
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
    public Button aiBattleButton;
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
        // 面板互斥：打开一个匹配/房间面板时，关闭其他面板并释放其 Steam 回调，
        // 防止残留的 LobbyMatchList_t 回调收到别的面板的 RequestLobbyList 结果而错误处理。
        if (quickMatchButton != null) quickMatchButton.onClick.AddListener(() =>
        {
            CreateRoomPanel.Instance?.LeaveRoom();
            JoinRoomPanel.Instance?.Close();
            QuickMatchPanel.Instance?.Open();
        });
        if (createRoomButton != null) createRoomButton.onClick.AddListener(CreateRoom);
        if (joinRoomButton != null) joinRoomButton.onClick.AddListener(JoinRoom);
        if (aiBattleButton != null) aiBattleButton.onClick.AddListener(StartAIBattle);
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
        QuickMatchPanel.Instance?.Close();
        CreateRoomPanel.Instance?.OpenAsHost();
    }

    public void JoinRoom()
    {
        QuickMatchPanel.Instance?.Close();
        JoinRoomPanel.Instance?.Open();
    }

    /// <summary>
    /// AI 对战：离线单机模式。设 FromLobby=false（走离线 Host + AI 对手），
    /// 复用 Preloader 异步加载 Game 场景（无对手头像/倒计时）。
    /// </summary>
    public void StartAIBattle()
    {
        Debug.Log("[Lobby] StartAIBattle — 进入 AI 对战");
        LobbyConfig.FromLobby = false; // 离线 Host 模式（AutoConnect 会 StartHost）
        LobbyConfig.IsAI = true;

        // 确保 Preloader 存在（复用 JoinGamePanel 的预加载优化）
        if (Preloader.Instance == null)
        {
            var go = new GameObject("Preloader");
            go.AddComponent<Preloader>();
        }
        Preloader.Instance.StartPreload();
        Preloader.Instance.LoadGameScene();
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
