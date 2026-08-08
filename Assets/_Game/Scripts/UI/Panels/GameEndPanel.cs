using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;
using TMPro;

/// <summary>
/// 游戏结束弹窗。挂在 Panel 上，panelRoot 也填这个 Panel 本身。
/// </summary>
public class GameEndPanel : MonoBehaviour
{
    public static GameEndPanel Instance { get; private set; }

    [Header("UI 引用")]
    public GameObject panelRoot;
    public TMP_Text resultText;
    public Button returnButton;

    private CanvasGroup _canvasGroup;
    private bool _gameEnded;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // 用 CanvasGroup 隐藏，不用 SetActive(false) —— 那样会让协程无法启动
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    void Start()
    {
        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnToLobby);
    }

    public void OnPlayerDied(bool isLocalPlayer)
    {
        if (_gameEnded) return;
        StartCoroutine(CheckAndShow(isLocalPlayer));
    }

    IEnumerator CheckAndShow(bool isLocalPlayer)
    {
        yield return new WaitForSeconds(0.15f);

        NetworkPlayer deadPlayer = isLocalPlayer ? NetworkPlayer.Local : NetworkPlayer.Remote;
        if (deadPlayer != null)
        {
            if (deadPlayer.currentHealth > 0) yield break;
        }
        else
        {
            Player p = FindObjectOfType<Player>();
            EnemyPlayer ep = FindObjectOfType<EnemyPlayer>();
            if (isLocalPlayer && p != null && p.currentHealth > 0) yield break;
            if (!isLocalPlayer && ep != null && ep.currentHealth > 0) yield break;
        }

        Show(isLocalPlayer);
    }

    void Show(bool isLocalPlayer)
    {
        if (_gameEnded) return;
        _gameEnded = true;

        _canvasGroup.alpha = 1;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        if (resultText != null)
            resultText.text = isLocalPlayer ? "你输了！" : "你赢了！";

        Debug.Log($"[GameEndPanel] Game over, isLocal={isLocalPlayer}");

        // 记录胜/负
        if (isLocalPlayer)
            SteamDataManager.Instance?.RecordLoss();
        else
            SteamDataManager.Instance?.RecordWin();
    }

    void ReturnToLobby()
    {
        if (NetworkServer.active) FindObjectOfType<NetworkManager>()?.StopHost();
        else if (NetworkClient.isConnected) FindObjectOfType<NetworkManager>()?.StopClient();

        var nm = FindObjectOfType<NetworkManager>();
        if (nm != null) Destroy(nm.gameObject);

        SceneManager.LoadScene("Lobby");
    }
}
