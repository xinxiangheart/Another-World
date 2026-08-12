using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image), typeof(CanvasGroup))]
public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [HideInInspector] public RectTransform rectTransform;
    [HideInInspector] public Vector3 targetPos;
    [HideInInspector] public Quaternion targetRotation = Quaternion.identity;
    [HideInInspector] public HandManager handManager;

    public static bool IsAnyCardDragging = false;
    public System.Action<CardInstance> OnCardClicked;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private int originalSibling;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        originalScale = transform.localScale;
    }

    /// <summary>重新记录当前的 localScale 作为基准（Scale2DCard 调用后）</summary>
    public void RefreshOriginalScale()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {
        if (!IsAnyCardDragging)
        {
            rectTransform.localPosition = Vector3.Lerp(rectTransform.localPosition, targetPos, Time.deltaTime * 15f);
            rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, targetRotation, Time.deltaTime * 15f);
            handManager?.MarkBoundsDirty();
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (IsAnyCardDragging) return;
        originalSibling = transform.GetSiblingIndex();
        transform.SetAsLastSibling();
        StopAllCoroutines();
        StartCoroutine(SmoothTo(new Vector3(targetPos.x, targetPos.y + 30, 0), Quaternion.identity, originalScale * 1.15f, 0.12f));
        handManager?.MarkBoundsDirty();
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (IsAnyCardDragging) return;
        transform.SetSiblingIndex(originalSibling);
        StopAllCoroutines();
        StartCoroutine(SmoothTo(targetPos, targetRotation, originalScale, 0.15f));
        handManager?.MarkBoundsDirty();
    }

    System.Collections.IEnumerator SmoothTo(Vector3 pos, Quaternion rot, Vector3 scale, float dur)
    {
        Vector3 sp = rectTransform.localPosition;
        Quaternion sr = rectTransform.localRotation;
        Vector3 ss = rectTransform.localScale;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            p = p < 0.5f ? 2 * p * p : -1 + (4 - 2 * p) * p;
            rectTransform.localPosition = Vector3.Lerp(sp, pos, p);
            rectTransform.localRotation = Quaternion.Slerp(sr, rot, p);
            rectTransform.localScale = Vector3.Lerp(ss, scale, p);
            handManager?.MarkBoundsDirty();
            yield return null;
        }
        rectTransform.localPosition = pos;
        rectTransform.localRotation = rot;
        rectTransform.localScale = scale;
        handManager?.MarkBoundsDirty();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsAnyCardDragging && OnCardClicked != null)
        {
            CardInstance ci = GetComponent<CardInstance>();
            OnCardClicked?.Invoke(ci);
        }
    }

    /// <summary>返回卡牌在屏幕空间的包围矩形（含缩放/位移/旋转）。</summary>
    public Rect GetWorldRect()
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        // corners: [0]=左下, [1]=左上, [2]=右上, [3]=右下
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}