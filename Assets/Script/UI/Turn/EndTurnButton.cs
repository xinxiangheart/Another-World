using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class EndTurnButton : MonoBehaviour
{
    private Button button;
    private CanvasGroup canvasGroup;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(EndTurn);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void EndTurn()
    {
        Debug.Log($"[EndTurnButton] Click! Server={NetworkServer.active}, Client={NetworkClient.isConnected}");

        TurnManager turnManager = FindObjectOfType<TurnManager>();
        if (turnManager == null)
        {
            Debug.LogError("[EndTurnButton] TurnManager not found!");
            return;
        }

        // Client: send end-turn command to server
        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkTurnSync nts = FindObjectOfType<NetworkTurnSync>();
            if (nts != null)
            {
                Debug.Log("[EndTurnButton] Sending CmdRequestEndTurn via NetworkTurnSync");
                nts.CmdRequestEndTurn();
            }
            else
            {
                Debug.Log("[EndTurnButton] Sending CmdEndTurn directly");
                NetworkPlayer.Local?.CmdEndTurn();
            }
            return;
        }

        // Server/Host: execute directly
        Debug.Log($"[EndTurnButton] Direct EndCurrentTurn, currentPhase={turnManager.currentPhase}");
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