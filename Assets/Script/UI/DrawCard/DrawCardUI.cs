using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mirror;

public class DrawCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public TextMeshProUGUI drawCountText;
    public Image buttonImage;

    public float hoverScale = 1.2f;
    public float smoothSpeed = 10f;

    private int remainingDraws = 5;
    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        UpdateDisplay();
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * smoothSpeed);
    }

    public void ResetForNewPhase()
    {
        remainingDraws = 5;
        UpdateDisplay();
    }

    public int GetRemainingDraws()
    {
        return remainingDraws;
    }

    public void UpdateDisplay()
    {
        if (drawCountText != null)
            drawCountText.text = remainingDraws.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Only allow interaction during your turn
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm != null && !tm.IsMyTurn()) return;

        if (remainingDraws > 0)
            targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Guard: only during your turn (both online and offline)
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm != null && !tm.IsMyTurn())
        {
            Debug.Log($"[DrawCardUI] Click blocked: not MyTurn (phase={tm?.currentPhase})");
            return;
        }

        if (remainingDraws <= 0)
        {
            Debug.Log("[DrawCardUI] Click blocked: no remaining draws");
            return;
        }

        NetworkPlayer player = NetworkPlayer.Local;
        if (player == null)
        {
            Debug.LogError("[DrawCardUI] Click blocked: NetworkPlayer.Local is null");
            return;
        }

        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            // Pure client: send draw request to server
            Debug.Log($"[DrawCardUI] Sending CmdRequestDraw, energy={player.currentEnergy}");
            player.CmdRequestDraw();
            return;
        }

        // Host/Server or offline: execute draw directly
        Debug.Log($"[DrawCardUI] Direct draw, energy={player.currentEnergy}");
        if (player.UseEnergy(1))
        {
            player.DrawCard();
            remainingDraws--;
            UpdateDisplay();
        }
        else
        {
            Debug.LogWarning($"[DrawCardUI] UseEnergy(1) failed! currentEnergy={player.currentEnergy}");
        }
    }

    public void UseOneDraw()
    {
        if (remainingDraws > 0)
            remainingDraws--;
    }

    CanvasGroup _canvasGroup;
    public void SetInteractable(bool enabled)
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = enabled;
        _canvasGroup.blocksRaycasts = enabled;
    }
}
