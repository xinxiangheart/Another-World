using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardInstance cardInstance;

    void Awake()
    {
        cardInstance = GetComponent<CardInstance>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // In online mode, don't show enemy card details
        if (NetworkClient.isConnected && !IsOwnedByLocalPlayer())
            return;

        if (Test1Panel.Instance != null && cardInstance != null)
            Test1Panel.Instance.Show(cardInstance);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Test1Panel.Instance?.Hide();
    }

    /// <summary>
    /// Check if this card belongs to the local player.
    /// Cards on slots 6-11 or in local hand are owned by local.
    /// </summary>
    bool IsOwnedByLocalPlayer()
    {
        if (NetworkPlayer.Local == null)
            return true; // Single-player mode: show all

        // Check if this card is in local player's hand
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null && card.GetComponent<CardInstance>() == cardInstance)
                return true;
        }

        // Check if this card is on local player's board (slots 6-11)
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D != null)
                {
                    Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance == cardInstance)
                        return true;
                }
            }

            // Also check attached models on local side
            foreach (GameObject obj in bm.attachedModels)
            {
                if (obj != null)
                {
                    Card3DInstance c3d = obj.GetComponent<Card3DInstance>();
                    if (c3d?.cardInstance == cardInstance)
                    {
                        // Only show if attached to a local card
                        if (c3d.cardInstance.hostSlotID >= 6)
                            return true;
                    }
                }
            }
        }

        return false;
    }
}
