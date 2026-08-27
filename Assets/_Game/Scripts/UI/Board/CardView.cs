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

    /// <summary>悬停上浮高度 / 放大倍率（悬停期间保持，不缩回）。</summary>
    const float HOVER_RAISE = 30f;
    const float HOVER_SCALE = 1.15f;
    bool _hovered;

    /// <summary>抽牌入场动画进行中（飞行中的牌不参与 RefreshLayout 的 snap，也不被 Update 的 lerp 覆盖）。</summary>
    [HideInInspector] public bool IsFlying = false;

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
        if (!IsAnyCardDragging && !IsFlying)
        {
            // 悬停时保持"放大+上浮"状态（不向基础位置回拉）；移开后回到 targetPos/targetRotation
            Vector3 desiredPos = _hovered ? new Vector3(targetPos.x, targetPos.y + HOVER_RAISE, 0) : targetPos;
            Quaternion desiredRot = _hovered ? Quaternion.identity : targetRotation;
            rectTransform.localPosition = Vector3.Lerp(rectTransform.localPosition, desiredPos, Time.deltaTime * 15f);
            rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, desiredRot, Time.deltaTime * 15f);
            handManager?.MarkBoundsDirty();
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (IsAnyCardDragging) return;
        _hovered = true;
        originalSibling = transform.GetSiblingIndex();
        transform.SetAsLastSibling();
        StopAllCoroutines();
        StartCoroutine(SmoothTo(new Vector3(targetPos.x, targetPos.y + HOVER_RAISE, 0), Quaternion.identity, originalScale * HOVER_SCALE, 0.12f));
        handManager?.MarkBoundsDirty();
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (IsAnyCardDragging) return;
        _hovered = false;
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

    /// <summary>设置卡牌透明度。</summary>
    public void SetAlpha(float a)
    {
        canvasGroup.alpha = a;
    }

    /// <summary>从屏幕右侧外水平飞入到目标位置的入场动画（全程可见，弹性落位）。</summary>
    /// <param name="startWorldPos">起点世界坐标（屏幕右边界外；Y/Z 会被初始目标覆盖以保持严格水平）</param>
    /// <param name="targetWorldPos">初始目标世界坐标（RefreshLayout 计算出的 targetPos 转世界，飞行中会每帧重新读取最新值）</param>
    /// <param name="targetRotation">初始目标旋转（飞行中会每帧重新读取最新值）</param>
    /// <param name="duration">动画时长</param>
    /// <param name="cfg">动画配置（弹性/旋转/缩放参数）</param>
    /// <param name="layoutTrigger">延迟让位触发点（0~1），飞行进度到该比例时回调 onLayoutTrigger</param>
    /// <param name="onLayoutTrigger">延迟让位回调（触发现有手牌滑动让位）</param>
    public System.Collections.IEnumerator FlyInFromDeck(Vector3 startWorldPos, Vector3 targetWorldPos, Quaternion targetRotation,
                                                          float duration, AnimationConfig cfg,
                                                          float layoutTrigger, System.Action onLayoutTrigger)
    {
        float overshoot = cfg != null ? cfg.flyEaseOvershoot : 1.2f;
        float zRot = cfg != null ? cfg.flyZRotation : 8f;
        float scaleMin = cfg != null ? cfg.flyScaleMin : 0.95f;

        Vector3 targetScale = transform.localScale; // Scale2DCard 已 ×3
        Quaternion startRotation = Quaternion.Euler(0f, 0f, -zRot);

        IsFlying = true;

        // 全程可见：显式激活 + alpha=1，飞行中不再有任何隐藏操作
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        // 起点：X 取屏幕外，Y/Z 锁定为初始目标值 → 纯水平飞入，无上下跳动、无深度位移
        Vector3 startPos = new Vector3(startWorldPos.x, targetWorldPos.y, targetWorldPos.z);

        transform.position = startPos;
        transform.rotation = startRotation;
        transform.localScale = targetScale * scaleMin;

        float elapsed = 0f;
        bool layoutTriggered = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t, overshoot); // 弹性缓动：到位后轻微过冲再回正

            // 飞到 trigger 进度时触发一次延迟让位
            if (!layoutTriggered && t >= layoutTrigger)
            {
                layoutTriggered = true;
                onLayoutTrigger?.Invoke();
            }

            // 每帧重新读取最新 targetPos/targetRotation（RefreshLayout 可能已因新牌加入而更新目标），
            // 从当前位置向最新目标插值 → 目标变化时无缝转向，不锁定终点、无停顿。
            Vector3 currentTargetWorld = transform.parent.TransformPoint(targetPos);

            transform.position = Vector3.Lerp(transform.position, currentTargetWorld, eased);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, eased);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, eased);

            yield return null;
        }

        // 精确落位到最终 targetPos（飞行结束，交回 Update 的 lerp 也指向同一目标）
        Vector3 finalTargetWorld = transform.parent.TransformPoint(targetPos);
        transform.position = finalTargetWorld;
        transform.rotation = targetRotation;
        transform.localScale = targetScale;
        canvasGroup.alpha = 1f;
        IsFlying = false;
        handManager?.MarkBoundsDirty();
    }

    /// <summary>ease-out back 缓动：到位后轻微过冲再回正。c1 为过冲系数（1.7≈标准，越小越轻）。</summary>
    static float EaseOutBack(float t, float c1)
    {
        float c3 = c1 + 1f;
        float t1 = t - 1f;
        return 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;
    }
}