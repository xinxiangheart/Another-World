using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>大厅「卡牌总览」里的一张卡：悬停把它抬起来一点，点击开这张卡的详情。</summary>
/// <remarks>2026-09-28：用户「为卡牌做悬停和点击变化，点击后……」。两处变化都在**卡本身**上做：
///   · 悬停 —— 放大一点点（2.5 → 2.62）+ 上浮 <see cref="hoverRise"/> 像素，缓动到目标后停手；
///   · 点击 —— 交给 <see cref="onClick"/>（面板去开详情），本组件只负责「按下能点到」。
///
/// ⚠ **本组件绝不许 `enabled = false`**（2026-09-28 踩过）：EventSystem 发事件前先过
///   `ExecuteEvents.ShouldSendToComponent` → `behaviour.isActiveAndEnabled`，组件一停用，
///   `GetEventHandler&lt;IPointerClickHandler&gt;()` 直接返回 null ⇒ **真鼠标点上去完全没反应**。
///   「静下来别空转」用 <see cref="_animating"/> 在 Update 里早退，不要动 enabled。
///
/// 可点面是**另铺的一张全透明 Image**（子物体 Hit，锚四边拉伸）：卡自己的 Graphic 不一定开
/// raycastTarget，且各层互相压着 —— 铺一张最上层的透明面最省事，射线不看 alpha（只有
/// alphaHitTestMinimumThreshold 才看）。
///
/// 不会跟 ScrollRect 打架：本组件不实现任何 Drag 接口，按下拖动时 EventSystem 会把
/// <c>eligibleForClick</c> 置 false 并冒泡给上层 ScrollRect —— 拖完抬手不会误开详情。
/// </remarks>
public class LobbyCardTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("悬停变化（倍数 / 屏幕 px）")]
    public float hoverScale = 2.62f;
    public float hoverRise = 7f;
    [Tooltip("追目标的速度（越大越快）；0.1 秒左右到位")]
    public float speed = 26f;

    /// <summary>点击回调（由面板填：开这张卡的详情）。</summary>
    public System.Action onClick;

    float _baseScale = 2.5f;
    RectTransform _rt;
    Vector2 _home;        // 静止态位置（悬停是相对它算的）
    float _scale;         // 当前倍数
    bool _hover;
    bool _animating;      // 正在追目标（false = Update 第一行就早退）

    void Awake()
    {
        _rt = transform as RectTransform;
        EnsureHit();
    }

    /// <summary>摆好位之后调一次：<paramref name="scale"/> 是批量铺卡时用的倍数（悬停以它为基准）。</summary>
    public void Init(float scale, System.Action act)
    {
        if (_rt == null) _rt = transform as RectTransform;
        _baseScale = scale;
        _scale = scale;
        onClick = act;
        if (_rt != null)
        {
            _home = _rt.anchoredPosition;
            _rt.localScale = Vector3.one * scale;
        }
        _animating = false;   // ⚠ 别写成 enabled = false —— 那会让点击/悬停全收不到
    }

    /// <summary>整张卡都能点到的那张透明面（铺成最后一个子物体：同层里后面的画在上面）。</summary>
    void EnsureHit()
    {
        if (transform.Find("Hit") != null) return;
        var go = new GameObject("Hit", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData) { SetHover(true); }
    public void OnPointerExit(PointerEventData eventData) { SetHover(false); }
    public void OnPointerClick(PointerEventData eventData) { if (onClick != null) onClick(); }

    void OnDisable()
    {
        // 被回收 / 面板关掉：别把中间态留在卡上（下次重建是新件，但同一件也可能只是被停用）
        _hover = false;
        if (_rt != null)
        {
            _rt.localScale = Vector3.one * _baseScale;
            _rt.anchoredPosition = _home;
        }
        _scale = _baseScale;
        _animating = false;
    }

    void SetHover(bool on)
    {
        if (_hover == on) return;
        _hover = on;
        _animating = true;   // 静下来之后 Update 自己停手（组件保持 enabled，不然收不到事件）
    }

    void Update()
    {
        if (!_animating) return;          // 静下来了：不空转（但不能停用组件，见类头备注）
        if (_rt == null) { _animating = false; return; }

        float t = speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        float wantScale = _hover ? hoverScale : _baseScale;
        float wantY = _home.y + (_hover ? hoverRise : 0f);

        _scale = Mathf.Lerp(_scale, wantScale, t);
        Vector2 p = _rt.anchoredPosition;
        p.y = Mathf.Lerp(p.y, wantY, t);

        bool done = Mathf.Abs(_scale - wantScale) < 0.0015f && Mathf.Abs(p.y - wantY) < 0.15f;
        if (done) { _scale = wantScale; p.y = wantY; }

        _rt.localScale = Vector3.one * _scale;
        _rt.anchoredPosition = p;

        if (done) _animating = false;
    }
}
