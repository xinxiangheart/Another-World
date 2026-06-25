using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class EndTurnButton : MonoBehaviour
{
    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(EndTurn);
    }

    void EndTurn()
    {
        TurnManager turnManager = FindObjectOfType<TurnManager>();

        // Client: send end-turn command to server
        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkTurnSync nts = FindObjectOfType<NetworkTurnSync>();
            if (nts != null)
                nts.CmdRequestEndTurn();
            else
                NetworkPlayer.Local?.CmdEndTurn();
            return;
        }

        // Server/Host: execute directly
        turnManager?.EndCurrentTurn();
    }

    public void SetInteractable(bool enabled)
    {
        if (button == null) button = GetComponent<Button>();
        button.interactable = enabled;
    }
}