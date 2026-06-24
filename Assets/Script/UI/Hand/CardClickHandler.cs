using UnityEngine;
using UnityEngine.EventSystems;

public class CardClickHandler : MonoBehaviour, IPointerClickHandler
{
    public System.Action onClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke();
    }
}