using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Mirror;
using UnityEngine.SceneManagement;

/// <summary>
/// Settings button in Game scene:
/// - Hover scale-up (never blocked by turn actions)
/// - Click toggles a settings panel with surrender button
/// - Surrender notifies both players and returns to lobby
/// </summary>
public class SettingsButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Panel")]
    public GameObject settingsPanel;

    [Header("Hover")]
    public float hoverScale = 1.15f;

    private Button _button;
    private Vector3 _originalScale;
    private bool _panelOpen;
    private bool _surrendering;

    void Awake()
    {
        _button = GetComponent<Button>();
        _originalScale = transform.localScale;
        _button.onClick.AddListener(TogglePanel);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = _originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = _originalScale;
    }

    void TogglePanel()
    {
        _panelOpen = !_panelOpen;
        if (settingsPanel != null)
            settingsPanel.SetActive(_panelOpen);
    }

    public void Surrender()
    {
        if (_surrendering) return;
        _surrendering = true;

        Debug.Log("[SettingsButton] Surrender");

        if (NetworkClient.isConnected)
        {
            // 服务端将投降方血量归零 → OnHealthChanged → GameEndPanel
            NetworkPlayer.Local?.CmdSurrender();
        }
        else
        {
            // 离线模式：直接触发GameEndPanel
            GameEndPanel.Instance?.OnPlayerDied(true);
        }

        if (settingsPanel != null) settingsPanel.SetActive(false);
        _panelOpen = false;
    }

    // 对方投降时由 GameEndPanel 统一处理，此方法删除
    public void OnOpponentSurrendered()
    {
        // No-op — 对方血量降到0时 OnHealthChanged → GameEndPanel.OnPlayerDied(false)
        Debug.Log("[SettingsButton] OnOpponentSurrendered — handled by GameEndPanel");
    }
}
