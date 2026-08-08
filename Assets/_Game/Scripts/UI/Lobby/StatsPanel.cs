using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 战绩面板——显示总场数/胜场/败场/连胜数/连败数/胜率。
/// 挂在你自己建的 StatsPanel 上，拖入 6 个 TMP_Text 和返回按钮。
/// </summary>
public class StatsPanel : MonoBehaviour
{
    [Header("面板")]
    public GameObject panelRoot;

    [Header("按钮")]
    public Button returnButton;

    [Header("数据")]
    public TMP_Text totalMatchesText;
    public TMP_Text winsText;
    public TMP_Text lossesText;
    public TMP_Text winStreakText;
    public TMP_Text lossStreakText;
    public TMP_Text winRateText;

    void Start()
    {
        if (returnButton != null) returnButton.onClick.AddListener(Close);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Refresh()
    {
        var sd = SteamDataManager.Instance;
        var d = sd?.playerData;
        if (d == null) return;

        if (totalMatchesText != null) totalMatchesText.text = $"总场数：{d.totalMatches}";
        if (winsText != null) winsText.text = $"胜场：{d.totalWins}";
        if (lossesText != null) lossesText.text = $"败场：{d.totalLosses}";
        if (winStreakText != null) winStreakText.text = $"连胜数：{d.winStreak}";
        if (lossStreakText != null) lossStreakText.text = $"连败数：{d.lossStreak}";
        if (winRateText != null && sd != null)
            winRateText.text = $"胜率：{sd.WinRate:F1}%";
    }
}
