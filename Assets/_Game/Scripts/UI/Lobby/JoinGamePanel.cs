using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 加入游戏过渡面板——双方头像/名字 + 倒计时后加载 Game。
/// 中间文字自己建一个 TMP_Text 挂在面板下，不用拖脚本。
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

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
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

        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        float remaining = 3f;
        while (remaining > 0)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(remaining).ToString();
            yield return null;
            remaining -= Time.deltaTime;
        }

        if (countdownText != null) countdownText.text = "0";
        SceneManager.LoadScene("Game");
    }
}
