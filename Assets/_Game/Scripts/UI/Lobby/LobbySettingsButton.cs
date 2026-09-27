using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>大厅右上角齿轮：点击开全局设置面板（SettingsPanel），悬停轻微放大。</summary>
/// <remarks>2026-09-27：用户「右上角的设置打开就是设置页面」。
/// 不依赖场景里那个 inactive 的旧 Setting 按钮（SettingsLauncher 只扫活跃对象，扫不到它），
/// 自己在运行时补一个 Button 接到 SettingsPanel.Toggle()；SettingsPanel 首次 Open 时自建画布，
/// 因此这里不需要任何场景引用。放大倍率刻意压低（1.08）—— 与「幅度要低」那条口径一致。</remarks>
[RequireComponent(typeof(RawImage))]
public class LobbySettingsButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("悬停放大")] public float hoverScale = 1.08f;
    public float lerpSpeed = 12f;

    Vector3 _base = Vector3.one;
    bool _hover;

    void Awake()
    {
        _base = transform.localScale;

        var raw = GetComponent<RawImage>();
        if (raw != null) raw.raycastTarget = true;   // 整个图标可点，不只是齿轮那几条线

        var btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = raw;
        btn.onClick.AddListener(SettingsPanel.Toggle);
    }

    void OnDisable()
    {
        _hover = false;
        transform.localScale = _base;
    }

    public void OnPointerEnter(PointerEventData eventData) { _hover = true; }
    public void OnPointerExit(PointerEventData eventData)  { _hover = false; }

    void Update()
    {
        Vector3 want = _hover ? _base * hoverScale : _base;
        transform.localScale = Vector3.Lerp(transform.localScale, want, Time.unscaledDeltaTime * lerpSpeed);
    }
}