using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 确认弹窗左下「确认」/ 右下「拒绝」两个键：悬停 → 文字变金 + 换预提亮贴图；点击 → 交回 MatchConfirmPanel。
/// </summary>
/// <remarks>2026-09-27：与 LobbyPlateHover / MatchWaitCancelButton 同一路数 —— 没有 Button，直接用
/// IPointer* 接口；本物体自己的 RawImage（底板）就是 raycast 目标，字上的 raycastTarget 在接线时置 false。
/// 两张底板贴图由 Tools/cardframe/MatchConfirmV1.ps1 出，**只差色调**（同 LobbyBtnPlateHover 配方），
/// 形体一致所以悬停时不跳位。
/// <see cref="interactable"/> = false 时既不亮也不响应点击（己方确认后锁住「确认」，避免重复提交）。
/// </remarks>
public class ConfirmActionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("底板 / 文字")]
    public RawImage plate;
    public TextMeshProUGUI label;

    [Header("常态 / 悬停贴图")]
    public Texture normalTexture;
    public Texture hoverTexture;

    [Header("文字配色（常态色在 Awake 里从 label 现取）")]
    public Color hoverColor = new Color32(228, 203, 132, 255);   // 亮金 #E4CB84
    [HideInInspector] public Color normalColor = Color.white;

    [Header("归属")]
    public MatchConfirmPanel owner;
    [Tooltip("true = 确认（左下）；false = 拒绝（右下）。")]
    public bool isConfirm = true;

    [Header("可交互")]
    public bool interactable = true;

    void Awake()
    {
        if (plate == null) plate = GetComponent<RawImage>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) normalColor = label.color;
    }

    void OnDisable() { ApplyNormal(); }

    /// <summary>锁 / 解锁这个键（锁上后不亮、不响应点击）。</summary>
    public void SetInteractable(bool value)
    {
        interactable = value;
        if (!value) ApplyNormal();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interactable) return;
        if (label != null) label.color = hoverColor;
        if (plate != null && hoverTexture != null) plate.texture = hoverTexture;
    }

    public void OnPointerExit(PointerEventData eventData) { ApplyNormal(); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable || owner == null) return;
        if (isConfirm) owner.OnConfirmClicked();
        else owner.OnDeclineClicked();
    }

    void ApplyNormal()
    {
        if (label != null) label.color = normalColor;
        if (plate != null && normalTexture != null) plate.texture = normalTexture;
    }
}