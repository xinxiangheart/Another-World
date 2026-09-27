using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>大厅入口板：悬停把板上文字变金，点击开占位弹窗。</summary>
/// <remarks>2026-09-27：用户「这些方块文字在鼠标悬停时会变成金色，点击打开占位图」。
/// 服务对象 = LobbyUI_v1 里八块入口板（上排 战斗 / 卡牌总览 + 透明大框两格 房间 / 其它）
/// + 右下角入口条四块（赛季 / 公告 / 藏品 / 成就）。
/// **只改文字色，板的贴图一律不换** —— 形体不动，悬停时不会跳位。
/// 点击目标两种：绑了 subPanel（全屏子弹窗）就走它，否则退回占位弹窗 popup。
/// 没有 Button —— 直接用 IPointer* 接口；板自己的 RawImage 是 raycast 目标
/// （板上 Label 的 raycastTarget 在接线时置 false），指针落在文字上也会冒泡到板这一层。
/// 悬停只有在 Play 模式下才触发（EventSystem 不进编辑模式）。</remarks>
public class LobbyPlateHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("板上文字")] public TextMeshProUGUI label;

    [Header("点击")] public LobbyPopup popup;
    public string title = "占位";

    [Header("点击 → 子全屏弹窗（填了就走它，没填才走上面的占位弹窗）")]
    public LobbySubPanel subPanel;

    [Header("配色 —— 常态色在 Awake 里从 label 现取，这里只给悬停色")]
    public Color hoverColor = new Color32(228, 203, 132, 255);   // 本套亮金 #E4CB84

    [HideInInspector] public Color normalColor = new Color32(240, 232, 210, 236);

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) normalColor = label.color;   // 还原色永远以板上文字的实际初值为准
    }

    void OnEnable()  { ApplyNormal(); }
    void OnDisable() { ApplyNormal(); }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (label != null) label.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyNormal();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (subPanel != null) subPanel.Open(title);
        else if (popup != null) popup.Show(title);
    }

    void ApplyNormal()
    {
        if (label != null) label.color = normalColor;
    }
}