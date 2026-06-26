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
        // Guard: only during your turn in online mode
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (NetworkClient.isConnected && tm != null && !tm.IsMyTurn())
            return;

        if (remainingDraws <= 0)
            return;

        NetworkPlayer player = NetworkPlayer.Local;
        if (player == null) return;

        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            // Pure client: send draw request to server
            player.CmdRequestDraw();
            // Server will send card back via TargetRpc and update energy via SyncVar
            return;
        }

        // Host/Server or offline: execute draw directly
        if (player.UseEnergy(1))
        {
            player.DrawCard();
            remainingDraws--;
            UpdateDisplay();
        }
    }

    public void UseOneDraw()
    {
        if (remainingDraws > 0)
            remainingDraws--;
    }
}
