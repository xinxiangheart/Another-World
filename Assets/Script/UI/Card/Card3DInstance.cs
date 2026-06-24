using UnityEngine;

public class Card3DInstance : MonoBehaviour
{
    public CardInstance cardInstance;

    public void UpdateValues()
    {
        CardDisplay3D display = GetComponent<CardDisplay3D>();
        if (display != null) display.Refresh();
    }
}