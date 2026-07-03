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
            NetworkPlayer.Local?.CmdSurrender();
            StartCoroutine(DoReturnToLobby());
        }
        else
        {
            // Offline: just go back to lobby
            StartCoroutine(DoReturnToLobby());
        }
    }

    System.Collections.IEnumerator DoReturnToLobby()
    {
        yield return new WaitForSeconds(1.5f);
        if (NetworkServer.active)
            FindObjectOfType<Mirror.NetworkManager>()?.StopHost();
        else if (NetworkClient.isConnected)
            FindObjectOfType<Mirror.NetworkManager>()?.StopClient();
        SceneManager.LoadScene("Lobby");
    }

    // Called by server when the OTHER player surrenders
    public void OnOpponentSurrendered()
    {
        if (_surrendering) return;
        _surrendering = true;
        Debug.Log("[SettingsButton] Opponent surrendered — returning to lobby");
        StartCoroutine(DoReturnToLobby());
    }
}
