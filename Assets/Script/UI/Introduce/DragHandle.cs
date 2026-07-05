using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag the target RectTransform by this UI element. Attach to a title bar / header.
/// </summary>
public class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Target")]
    public RectTransform target;

    private Vector2 _offset;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target.parent as RectTransform, eventData.position,
            eventData.pressEventCamera, out Vector2 localPoint);
        _offset = target.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target.parent as RectTransform, eventData.position,
            eventData.pressEventCamera, out Vector2 localPoint);
        target.anchoredPosition = localPoint + _offset;
    }
}
