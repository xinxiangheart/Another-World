using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 加入游戏过渡面板——双方头像/名字 + 倒计时后加载 Game。
/// 倒计时期间后台异步预加载Game场景重资源，完成后异步切换场景。
/// </summary>
public class JoinGamePanel : MonoBehaviour
{
    public static JoinGamePanel Instance { get; private set; }

    [Header("面板")]
    public GameObject panelRoot;

    [Header("双方玩家")]
    public RawImage localAvatar;
    public RawImage opponentAvatar;
    public TMP_Text localNameText;
    public TMP_Text opponentNameText;

    [Header("倒计时")]
    public TMP_Text countdownText;
    public Slider progressBar;       // 预加载进度条（可选）
    public TMP_Text progressText;    // 预加载进度文字（可选）

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        // 确保 Preloader 在 Lobby 场景已创建
        if (Preloader.Instance == null)
        {
            var go = new GameObject("Preloader");
            go.AddComponent<Preloader>();
        }
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        var sd = SteamDataManager.Instance;

        if (localAvatar != null && sd != null && sd.localAvatar != null)
            localAvatar.texture = sd.localAvatar;
        if (localNameText != null && sd != null)
            localNameText.text = sd.localPlayerName;

        var qm = QuickMatchPanel.Instance;
        if (opponentNameText != null)
            opponentNameText.text = (qm != null && !string.IsNullOrEmpty(qm.opponentName)) ? qm.opponentName : "对手";
        if (opponentAvatar != null && qm != null && qm.opponentTexture != null)
            opponentAvatar.texture = qm.opponentTexture;

        // 启动后台预加载
        Preloader.Instance.StartPreload();
        if (progressBar != null) progressBar.gameObject.SetActive(true);
        if (progressText != null) progressText.gameObject.SetActive(true);

        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        float remaining = 3f;
        while (remaining > 0)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(remaining).ToString();

            // 更新预加载进度条
            float p = Preloader.Instance.Progress;
            if (progressBar != null) progressBar.value = p;
            if (progressText != null) progressText.text = $"加载中 {Mathf.RoundToInt(p * 100)}%";

            yield return null;
            remaining -= Time.deltaTime;
        }

        // 倒计时结束但预加载未完成→等待预加载完成
        if (!Preloader.Instance.IsDone)
        {
            if (countdownText != null) countdownText.text = "等待资源加载...";
            while (!Preloader.Instance.IsDone)
            {
                float p = Preloader.Instance.Progress;
                if (progressBar != null) progressBar.value = p;
                if (progressText != null) progressText.text = $"加载中 {Mathf.RoundToInt(p * 100)}%";
                yield return null;
            }
        }

        if (countdownText != null) countdownText.text = "进入游戏";
        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(false);

        // 异步加载场景（场景内 Awake/Start 在激活后才执行）
        Preloader.Instance.LoadGameScene();
    }
}
