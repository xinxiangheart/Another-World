using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>好友详情左侧那一格 tab：悬停 / 选中换板贴图 + 文字变金 + 徽记提亮，点击让控制器切内容。</summary>
/// <remarks>2026-09-27：用户「左边有四个格子类似于大厅右下角那四个不过更扁平，分别是好友列表，添加好友，
/// 申请列表，黑名单，初始进入位于好友列表格子……四个格子每个格子的内容都将不一样」。
///
/// 本组件只管**这一格自己的皮**（三态板贴图 / 文字色 / 徽记透明度），「切哪块内容」归
/// <see cref="LobbyFriendDetailTabs"/>。四格共用同一张三态板（板没有格与格的区别），
/// 区别只在徽记贴图与文字 —— 所以三态只需要三张贴图。
///
/// 点击**不挂 Button**：同物体上再挂一个 Button 就会有两个 IPointerClickHandler 被各触发一次
/// （与好友表头那颗「+」同一条坑，见 LobbyIconHover.subPanel）。raycast 目标是子物体 Plate 那张
/// RawImage，指针落在板 / 文字 / 徽记上都会沿父级冒泡到本组件（文字与徽记的 raycastTarget 在建的时候关掉）。
/// 悬停只有在 Play 模式下才触发（EventSystem 不进编辑模式）。
/// </remarks>
public class LobbyFriendTab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("这一格是第几个（0 = 好友列表）")]
    public int index;

    [Header("引用")]
    public RawImage plate;
    public RawImage emblem;
    public TextMeshProUGUI label;
    public LobbyFriendDetailTabs tabs;

    [Header("三态板贴图（由 LobbyUIBuilder 填：常态 / 悬停 / 选中）")]
    public Texture normalTexture;
    public Texture hoverTexture;
    public Texture onTexture;

    [Header("配色 —— 常态色就是板上文字的实际初值口径")]
    public Color normalColor = new Color32(240, 232, 210, 236);      // 奶油 #F0E8D2
    public Color goldColor = new Color32(228, 203, 132, 255);        // 本套亮金 #E4CB84

    [Header("徽记不透明度（贴图不变，只改 alpha = 提亮）")]
    [Range(0f, 1f)] public float normalEmblemAlpha = 0.50f;
    [Range(0f, 1f)] public float hoverEmblemAlpha = 0.82f;
    [Range(0f, 1f)] public float onEmblemAlpha = 1.00f;

    bool _hover;

    /// <summary>选中的那格是 true（由 <see cref="LobbyFriendDetailTabs.Select"/> 置）。</summary>
    public bool IsOn { get; private set; }

    void Awake()
    {
        if (plate == null) plate = GetComponentInChildren<RawImage>(true);
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;    // 编辑态由构建脚本把初始态摆好，这里别去改场景
        Apply();
    }

    void OnDisable() { _hover = false; }        // 只是清指针态，别在关闭途中写贴图

    public void SetOn(bool on) { IsOn = on; Apply(); }

    public void SetHover(bool hover) { _hover = hover; Apply(); }

    void Apply()
    {
        if (plate != null)
        {
            Texture t = IsOn ? onTexture : (_hover && hoverTexture != null ? hoverTexture : normalTexture);
            if (t != null) plate.texture = t;
        }
        if (label != null) label.color = (IsOn || _hover) ? goldColor : normalColor;
        if (emblem != null)
        {
            Color c = emblem.color;
            c.a = IsOn ? onEmblemAlpha : (_hover ? hoverEmblemAlpha : normalEmblemAlpha);
            emblem.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { SetHover(true); }
    public void OnPointerExit(PointerEventData eventData) { SetHover(false); }
    public void OnPointerClick(PointerEventData eventData) { if (tabs != null) tabs.Select(index); }
}