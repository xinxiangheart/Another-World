using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        turnManager?.EndCurrentTurn();
    }

    public void SetInteractable(bool enabled)
    {
        if (button == null) button = GetComponent<Button>();
        button.interactable = enabled;
    }
}