using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡槽线层：底板（Slot_0 根节点的 Image）只负责"淡底"，本组件把线层颜色镜像成底板颜色，
/// 于是线随状态同色；底板处于常态淡底（alpha 很低）时线不显示。
/// 底板颜色一律经 BoardSlot.SetSlotColor 写（含选择期压暗），所以每帧镜像一次即可覆盖全部状态。
/// </summary>
[RequireComponent(typeof(Image))]
public class SlotEdgeOverlay : MonoBehaviour
{
    [Tooltip("底板 Image（Slot_0 根节点的 Image）")]
    public Image plate;

    [Tooltip("底板 alpha 低于该值视为常态淡底，线不显示")]
    public float edgeAlphaThreshold = 0.25f;

    Image _edge;

    void Awake()
    {
        _edge = GetComponent<Image>();
        if (plate == null && transform.parent != null) plate = transform.parent.GetComponent<Image>();
        Sync();
    }

    void LateUpdate()
    {
        Sync();
    }

    void Sync()
    {
        if (plate == null || _edge == null) return;
        Color c = plate.color;
        float a = c.a >= edgeAlphaThreshold ? c.a : 0f;
        _edge.color = new Color(c.r, c.g, c.b, a);
    }
}
