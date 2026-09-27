using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>大厅左下角那行常驻小字：显示自己的「异界号」；点一下复制到剪贴板，右侧弹一句提示；悬停微亮。</summary>
/// <remarks>2026-09-27 用户：「左下角会常态以小字显示自己id」→ 追加「可以加[复制]，另外鼠标悬停稍微变亮一点点，
/// 同时右侧弹出：已复制到剪贴板 的提示」。
///
/// 数据永远从 <c>SteamDataManager.PlayerIdText</c> 现取 —— ID 是在 Steam 就绪之后才生成的（SteamDataManager.Start），
/// 比本组件的 Awake 晚，所以不能只在 Start 刷一次。每帧比一次字符串、变了才写 text（每帧赋 text 会触发 TMP 重排）。
///
/// 本组件挂在**文字对象自己身上**（不是父物体）—— 没有 ID 时只把 text 清空，不 SetActive(false)，否则把自己也关了。
/// 提示语是它的**子物体** <c>Text_CopyToast</c>：跟着 ID 行一起走；raycastTarget = false，免得把父物体的
/// 悬停 / 点击抢走（Unity 的 pointer enter/exit 是看命中的那个 raycast 目标）。
///
/// 场景位置：Canvas/Layer_Hud_v1/Text_PlayerId（HUD 是 Canvas 最后一层 ⇒ 全屏子弹窗压不住它）。
/// 由 Tools/异界/大厅：加左下角常驻玩家 ID 生成 / 更新。</remarks>
public class LobbyPlayerIdTag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("文字（留空 = 取自己身上的 TextMeshProUGUI）")] public TextMeshProUGUI label;

    [Tooltip("ID 前面那句小字")]
    public string prefix = "ID  ";

    [Tooltip("还没拿到 ID 时显示的内容；留空 = 整行留白")]
    public string pending = "";

    [Header("常态 / 悬停色（悬停只是微亮一档，不换色相）")]
    public Color normalColor = new Color32(240, 232, 210, 140);
    public Color hoverColor  = new Color32(252, 246, 228, 205);

    [Header("复制提示：右侧那句")]
    public TextMeshProUGUI toastText;
    public string toastMessage = "已复制到剪贴板";
    public Color  toastColor   = new Color32(228, 203, 132, 255);   // 本套亮金 #E4CB84
    [Tooltip("全亮停留多久")]
    public float toastHold = 1.0f;
    [Tooltip("再花多久淡出")]
    public float toastFade = 0.8f;
    [Tooltip("提示语停在距 ID 行左端多少像素处")]
    public float toastOffsetX = 380f;
    [Tooltip("弹出时从左边滑进来的距离")]
    public float toastSlide = 14f;

    string _shown;
    bool   _hovering;
    float  _toastStart = -1f;      // < 0 = 提示不在显示

    void Awake()
    {
        if (label == null) label = GetComponent<TextMeshProUGUI>();
        if (label != null) label.color = normalColor;
        if (toastText != null)
        {
            var c = toastColor; c.a = 0f;
            toastText.color = c;
            toastText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        RefreshId();
        RefreshToast();
    }

    // ── ID 那一行 ────────────────────────────────────────────────────────────

    void RefreshId()
    {
        if (label == null) return;
        var sd = SteamDataManager.Instance;
        string id = sd != null ? sd.PlayerIdText : "";
        string text = string.IsNullOrEmpty(id) ? (pending ?? "") : (prefix + PlayerId.Pretty(id));
        if (text == _shown) return;
        _shown = text;
        label.text = text;
        label.color = _hovering ? hoverColor : normalColor;   // 悬停中号才出来 → 颜色也要跟上
    }

    /// <summary>当前要复制的号（空 = 还没有号）。</summary>
    string CurrentId()
    {
        var sd = SteamDataManager.Instance;
        return sd != null ? sd.PlayerIdText : "";
    }

    // ── 悬停 / 点击 ──────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        if (string.IsNullOrEmpty(CurrentId())) return;      // 还没号就没什么可亮 / 可点的
        if (label != null) label.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
        if (label != null) label.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        string id = CurrentId();
        if (string.IsNullOrEmpty(id)) return;

        GUIUtility.systemCopyBuffer = PlayerId.Pretty(id);   // 复制的是纯 ID（不带「ID  」前缀），粘到哪都能解
        _toastStart = Time.unscaledTime;
        if (toastText != null) toastText.text = toastMessage;

        Debug.Log("[LobbyPlayerIdTag] 已复制到剪贴板: " + GUIUtility.systemCopyBuffer);
    }

    // ── 提示语 ───────────────────────────────────────────────────────────────

    void RefreshToast()
    {
        if (toastText == null) return;

        if (_toastStart < 0f)
        {
            if (toastText.gameObject.activeSelf) toastText.gameObject.SetActive(false);
            return;
        }

        float e = Time.unscaledTime - _toastStart;
        float total = toastHold + toastFade;
        if (e >= total)
        {
            _toastStart = -1f;
            toastText.gameObject.SetActive(false);
            return;
        }

        if (!toastText.gameObject.activeSelf) toastText.gameObject.SetActive(true);
        float a = e < toastHold ? 1f : 1f - (e - toastHold) / Mathf.Max(0.01f, toastFade);
        var c = toastColor; c.a *= a;
        toastText.color = c;

        var rt = toastText.rectTransform;
        var p = rt.anchoredPosition;
        p.x = toastOffsetX - toastSlide * Mathf.Exp(-e * 18f);   // 弹出：从左边一丁点滑到位
        rt.anchoredPosition = p;
    }
}
