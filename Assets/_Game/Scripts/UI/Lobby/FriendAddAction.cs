using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>「添加好友」那一格里两颗小图标动作各是干什么的。</summary>
public enum FriendAddActionKind
{
    Search = 0,   // 井右端那枚放大镜：点一下立刻去搜
    Add = 1,      // 结果行右端那颗「+」：把这个人加进游戏内好友表
}

/// <summary>小图标动作：悬停换贴图、点击转给对应的控制器。</summary>
/// <remarks>2026-09-27：与 FriendRowAction 同一套写法，只是这里只有两种动作、目标不同。
///
/// **不挂 Button** —— 同物体上再挂一个 Button 就会有两个 IPointerClickHandler 各触发一次
/// （与好友行那三格、好友表头那颗「+」同一条坑）。raycast 目标就是这张 RawImage。
/// 顺带一条：动作图标**吃掉** pointer down 是**想要的** —— 点放大镜时不该把焦点又送给输入框。
/// 悬停只有在 Play 模式下才触发（EventSystem 不进编辑模式）。
/// </remarks>
public class FriendAddAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("这一格是哪个动作")] public FriendAddActionKind kind;

    [Header("图标（256 贴图 = 屏幕 44x44）")] public RawImage icon;
    public Texture normalTexture;
    public Texture hoverTexture;

    [Header("kind = Add：点下去加谁")] public FriendAddRowUI row;
    [Header("kind = Search：点下去谁去搜")] public FriendAddSearchUI search;

    void Awake()
    {
        if (icon == null) icon = GetComponent<RawImage>();
    }

    void OnDisable()
    {
        if (icon != null && normalTexture != null) icon.texture = normalTexture;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (icon != null && hoverTexture != null) icon.texture = hoverTexture;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (icon != null && normalTexture != null) icon.texture = normalTexture;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (kind == FriendAddActionKind.Search) { if (search != null) search.Submit(); return; }
        if (row != null) row.Action();
    }
}
