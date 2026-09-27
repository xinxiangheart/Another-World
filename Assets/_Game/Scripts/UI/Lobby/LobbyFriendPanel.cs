using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>大厅「好友」侧边栏：点好友图标从屏幕左侧滑出，再点一次、或点面板以外的任何地方滑回去。</summary>
/// <remarks>2026-09-27 用户：「现在做好友侧边栏展示，点击好友后从屏幕左侧滑出（速度较快）一个侧边栏，
/// 大概到左上角那个图案的右边缘，再次点击好友或者点击侧边栏之外的区域会滑动回去」。
/// 「左上角那个图案」= 左上头像衬托板 Plate_Profile —— 它的贴图右缘 1396 折屏幕 465.3，所以板宽 <see cref="width"/> 取 465。
///
/// 几何：根节点铺满全屏（只用于接点击），板身 <see cref="body"/> 是贴屏幕左沿的**满高**浮层（465x1080），
/// 只有横向参与滑动 —— 关着时停在 x = -width（整块在屏幕外），开着时 x = 0。
///
/// 层级：本面板挂 Layer_Sub_v1，而 Layer_Hud_v1（左上头像板 + 右上横栏 + 五个压墙图标 + 左下那行常驻
/// ID）是 Canvas 最后一个子物体 ⇒ 永远画在本面板之上。这一条同时管两件事：
///   ① 「不遮挡左上角组件 + 左下 ID 行」—— 用户 2026-09-27「不是不贴边，而是在它们层级之下」，
///      所以板是满高的、不靠躲，靠这一层；
///   ② 好友图标压在本面板之上 ⇒ 侧边栏开着时还能再点一次好友图标把它关掉（LobbyIconHover.friendsPanel → Toggle）。
///
/// 挡点击：根节点铺满全屏 + 一张**全透明 Image**（raycastTarget 开着 = 吃得到点击），Image 上挂一个
/// transition = None 的 **Button**，onClick 的持久监听指向 <see cref="Close"/> —— 点它就是「点面板以外」= 关。
/// 板身 Body 上也挂一个 transition = None 的 Button 但**不带任何监听**，它只是把点击吃掉：Unity 的
/// StandaloneInputModule 在 pointerDown 没找到 IPointerDownHandler 时会 GetEventHandler&lt;IPointerClickHandler&gt;
/// **沿父级向上找**，不挡的话点板身会冒泡到根节点那个 Button，一点就把面板关了。
///
/// 根节点在场景里**存成 active**（方便在编辑器里直接看版式），运行时由 Start 的 closeOnStart 自己关掉 ——
/// 与 LobbySubPanel 同一套约定。</remarks>
public class LobbyFriendPanel : MonoBehaviour
{
    [Header("滑动的那块板（留空 = 取第一个子物体）")]
    public RectTransform body;

    [Tooltip("板宽（屏幕 px）= 左上头像板的右沿 —— 关着时板停在一板宽之外的左侧")]
    public float width = 465f;

    [Tooltip("滑入 / 滑出耗时（秒）——「速度较快」")]
    public float slideTime = 0.18f;

    [Header("场景里默认可见（只在编辑器里看和调）")]
    public bool closeOnStart = true;

    /// <summary>唯一实例（LobbyIconHover 点别的图标前用它把侧边栏收回去）。</summary>
    public static LobbyFriendPanel Instance { get; private set; }

    float _progress;   // 0 = 全关（板在屏幕外），1 = 全开
    float _target;

    /// <summary>开着（含正在开）= true。</summary>
    public bool IsOpen { get { return _target > 0f; } }

    void Awake()
    {
        Instance = this;
        if (body == null && transform.childCount > 0) body = transform.GetChild(0) as RectTransform;
        _progress = 0f;
        _target = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (!closeOnStart) return;
        _progress = 0f; _target = 0f;
        Apply();
        gameObject.SetActive(false);
    }

    public void Open()
    {
        HidePlaceholderPopups();          // 别的图标开的占位弹窗先收掉，免得两块糊在一起
        gameObject.SetActive(true);
        if (_progress <= 0f) Apply();     // 从屏幕外起步
        _target = 1f;
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        _target = 0f;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    void Update()
    {
        if (Mathf.Approximately(_progress, _target)) return;

        float step = slideTime > 0.0001f ? Time.unscaledDeltaTime / slideTime : 1f;
        _progress = Mathf.MoveTowards(_progress, _target, step);
        Apply();

        if (_target <= 0f && _progress <= 0f) gameObject.SetActive(false);
    }

    void Apply()
    {
        if (body == null) return;
        float e = 1f - Mathf.Pow(1f - _progress, 3f);   // 快进慢出：起手快、贴边稳
        Vector2 p = body.anchoredPosition;
        p.x = -width * (1f - e);
        body.anchoredPosition = p;
    }

    /// <summary>把已经打开的占位弹窗收掉 —— 好友侧边栏和它抢同一块屏幕。</summary>
    static void HidePlaceholderPopups()
    {
        var all = UnityEngine.Object.FindObjectsOfType<LobbyPopup>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].gameObject.activeSelf) all[i].Hide();
    }
}

// 点「面板以外」怎么关：整个根节点铺满全屏、带一张**全透明 Image**（raycastTarget 开着 = 吃得到点击），
// 上面挂一个 transition = None 的 Button，onClick 的持久监听指向 Close() —— 与 LobbyPopup 的 Dim 遮罩同一套写法。
// 板身 Body 上也挂一个 transition = None、**不带任何监听**的 Button：它只是把点击吃掉，
// 不这么做的话 Unity 的 StandaloneInputModule 会 GetEventHandler<IPointerClickHandler> **沿父级向上找**，
// 一点板身就冒泡到根节点的 Button，把面板关了。
// （别用自定义的第二个 MonoBehaviour 去接：同一个 .cs 里除了文件名那个类，Unity 都序列化不了 ——
//   2026-09-27 实测，那两个组件在场景里存成了 missing script。Button 是内置组件，没这个毛病。）
