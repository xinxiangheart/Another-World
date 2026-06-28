using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class EndTurnButton : MonoBehaviour
{
    private Button button;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        button.onClick.AddListener(EndTurn);
    }

    void EndTurn()
    {
        Debug.Log($"[EndTurnButton] Click! Server={NetworkServer.active}, Client={NetworkClient.isConnected}");

        TurnManager turnManager = FindObjectOfType<TurnManager>();
        if (turnManager == null) { Debug.LogError("[EndTurnButton] TurnManager not found!"); return; }

        // Client: send end-turn command to server
        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkPlayer.Local?.CmdEndTurn();
            return;
        }

        // Server/Host: execute directly
        Debug.Log($"[EndTurnButton] Direct EndCurrentTurn, phase={turnManager.currentPhase}");
        turnManager.EndCurrentTurn();
    }

    public void SetInteractable(bool enabled)
    {
        if (button == null) button = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (button != null) button.interactable = enabled;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }
        Debug.Log($"[EndTurnButton] SetInteractable({enabled})");
    }
}