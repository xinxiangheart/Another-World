using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>匹配等待窗的「取消」键：悬停 → 白字变金 + 子背景换预提亮贴图；点击 → MatchWaitPanel.Cancel。</summary>
/// <remarks>2026-09-27 用户：「下面是取消 button（白字，悬停变金色），其有着子背景（只覆盖取消这两个字即可，用于提示）」。
/// 与 LobbyIconHover / LobbyPlateHover 同一路数：没有 Button，直接用 IPointer* 接口；
/// 本物体自己的 RawImage（子背景）就是 raycast 目标，字上的 raycastTarget 在接线时置 false。
/// 两张子背景贴图由 Tools/cardframe/MatchWaitV1.ps1 出，**只差色调**（同 LobbyBtnPlateHover 配方），形体一致所以不跳位。</remarks>
public class MatchWaitCancelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("子背景 / 文字")]
    public RawImage plate;
    public TextMeshProUGUI label;

    [Header("常态 / 悬停贴图")]
    public Texture normalTexture;
    public Texture hoverTexture;

    [Header("文字配色（常态色在 Awake 里从 label 现取）")]
    public Color hoverColor = new Color32(228, 203, 132, 255);   // 亮金 #E4CB84
    [HideInInspector] public Color normalColor = Color.white;

    [Header("点击")]
    public MatchWaitPanel owner;

    void Awake()
    {
        if (plate == null) plate = GetComponent<RawImage>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) normalColor = label.color;
    }

    void OnDisable() { ApplyNormal(); }

    public void OnPointerEnter(PointerEventData eventData) { ApplyHover(); }
    public void OnPointerExit(PointerEventData eventData) { ApplyNormal(); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null) owner.Cancel();
    }

    void ApplyHover()
    {
        if (label != null) label.color = hoverColor;
        if (plate != null && hoverTexture != null) plate.texture = hoverTexture;
    }

    void ApplyNormal()
    {
        if (label != null) label.color = normalColor;
        if (plate != null && normalTexture != null) plate.texture = normalTexture;
    }
}