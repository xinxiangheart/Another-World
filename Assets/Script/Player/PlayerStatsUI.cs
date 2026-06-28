using UnityEngine;
using TMPro;

/// <summary>
/// Attach to a GameObject in the Canvas. Drag the four TextMeshProUGUI
/// references in the Inspector. Polls NetworkPlayer.Local/Remote SyncVars
/// every frame — no dependency on Mirror lifecycle callbacks.
///
/// 挂在 Canvas 下的任意 GameObject 上，拖入四个 Text。
/// </summary>
public class PlayerStatsUI : MonoBehaviour
{
    [Header("己方 (Self)")]
    public TextMeshProUGUI selfHealthText;
    public TextMeshProUGUI selfEnergyText;

    [Header("对方 (Enemy)")]
    public TextMeshProUGUI enemyHealthText;
    public TextMeshProUGUI enemyEnergyText;

    void Update()
    {
        NetworkPlayer self = NetworkPlayer.Local;
        NetworkPlayer enemy = NetworkPlayer.Remote;

        // ---------- self ----------
        if (self != null)
        {
            if (selfHealthText != null)
                selfHealthText.text = $" {self.currentHealth}";

            if (selfEnergyText != null)
                selfEnergyText.text = $" {self.currentEnergy}/{self.maxEnergy}";
        }

        // ---------- enemy ----------
        if (enemy != null)
        {
            if (enemyHealthText != null)
                enemyHealthText.text = enemy.currentHealth.ToString();

            if (enemyEnergyText != null)
                enemyEnergyText.text = $"{enemy.currentEnergy}/{enemy.maxEnergy}";
        }
    }
}
