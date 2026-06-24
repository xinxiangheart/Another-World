using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardInstance cardInstance;

    void Awake()
    {
        cardInstance = GetComponent<CardInstance>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Test1Panel.Instance != null && cardInstance != null)
            Test1Panel.Instance.Show(cardInstance);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Test1Panel.Instance?.Hide();
    }
}