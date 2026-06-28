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

        if (self != null) UpdateOne(FindTMP("Health"), " {0}", self.currentHealth);

        if (self != null) UpdateOne(FindTMP("Energy"), " {0}/{1}", self.currentEnergy, self.maxEnergy);

        if (enemy != null) UpdateOne(FindTMP("EnemyHealthLabel"), "{0}", enemy.currentHealth);

        if (enemy != null) UpdateOne(FindTMP("EnemyEnergyLabel"), "{0}/{1}", enemy.currentEnergy, enemy.maxEnergy);
    }

    void UpdateOne(TextMeshProUGUI t, string fmt, params object[] args)
    {
        if (t != null) t.text = string.Format(fmt, args);
    }

    static TextMeshProUGUI FindTMP(string name)
    {
        var t = GameObject.Find(name)?.GetComponent<TextMeshProUGUI>();
        if (t == null) t = GameObject.Find(name + " ")?.GetComponent<TextMeshProUGUI>();
        return t;
    }
}
