using UnityEngine;
using TMPro;

/// <summary>
/// Fallback: polls NetworkPlayer.Local/Remote every 30 frames.
/// Primary update is handled by NetworkPlayer.SyncVar hooks (OnHealthChanged/OnEnergyChanged).
/// This just catches any missed initial values.
/// </summary>
public class PlayerStatsUI : MonoBehaviour
{
    void Update()
    {
        if (Time.frameCount % 30 != 0) return;

        NetworkPlayer self = NetworkPlayer.Local;
        NetworkPlayer enemy = NetworkPlayer.Remote;

        // 己方：通过 Player.Instance 绑定（场景有两个 "Health" 同名对象，GameObject.Find 会找错）
        if (self != null && Player.Instance != null)
        {
            UpdateOne(Player.Instance.healthText, " {0}", self.currentHealth);
            UpdateOne(Player.Instance.energyText, " {0}/{1}", self.currentEnergy, self.maxEnergy);
        }

        // 对方：通过 EnemyPlayer.Instance 绑定
        if (enemy != null && EnemyPlayer.Instance != null)
        {
            UpdateOne(EnemyPlayer.Instance.healthText, "{0}", enemy.currentHealth);
            UpdateOne(EnemyPlayer.Instance.energyText, "{0}/{1}", enemy.currentEnergy, enemy.maxEnergy);
        }
    }

    void UpdateOne(TextMeshProUGUI t, string fmt, params object[] args)
    {
        if (t != null) t.text = string.Format(fmt, args);
    }
}
