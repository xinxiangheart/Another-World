using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>「房间」面板右上角那串房间号：悬停变金、点一下复制、**下面**浮一句提示。</summary>
/// <remarks>2026-09-27 用户：「右上角的叉左边显示：房间号：xxxxxx，悬停变色点击会在下面浮现：已复制到剪切板」。
/// 与左下角那行常驻 ID（<see cref="LobbyPlayerIdTag"/>）同一路数（IPointer* 三件套、不挂 Button、
/// 提示语是子物体且 raycastTarget = false），只有三处不同：
///   ① 提示语在**下面**（ID 那行在右边）；② 悬停是**变金**（ID 那行只微亮一档，用户当时点名不要换色相）；
///   ③ 号是**本面板自己生成**的。
///
/// 号现在在本机生成（6 位，字母表剔掉 0 O 1 I 这些容易看错的），**不是真房间号** ——
/// 真正的号要等建房（Steam 大厅）接进来，那时调 <see cref="SetCode"/> 换掉即可，显示 / 复制 / 提示三件事不用改。
/// 每次面板打开换一个新号：面板是 SetActive(false)/(true) 开关的，所以生成放在 OnEnable（不是 Awake）。</remarks>
public class LobbyRoomCodeTag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("文字（留空 = 取自己身上的 TextMeshProUGUI）")] public TextMeshProUGUI label;

    [Tooltip("号前面那句")] public string prefix = "房间号：";
    [Tooltip("房间号；留空 = 每次打开面板现生成一个")] public string code = "";
    [Tooltip("每次启用（= 面板每次打开）都重新生成一个号")] public bool regenerateOnEnable = true;

    [Header("常态 / 悬停色（悬停变金）")]
    public Color normalColor = new Color32(240, 232, 210, 236);   // 奶油 #F0E8D2
    public Color hoverColor  = new Color32(228, 203, 132, 255);   // 本套亮金 #E4CB84

    [Header("复制提示：下面那句")]
    public TextMeshProUGUI toastText;
    public string toastMessage = "已复制到剪贴板";
    public Color  toastColor   = new Color32(228, 203, 132, 255);   // 同上，亮金
    [Tooltip("全亮停留多久")] public float toastHold = 1.0f;
    [Tooltip("再花多久淡出")] public float toastFade = 0.8f;
    [Tooltip("提示语停在标签左上角下方多少像素")] public float toastOffsetY = -60f;
    [Tooltip("弹出时从上边滑进来的距离")] public float toastSlide = 12f;

    /// <summary>号字母表：剔掉 0 O 1 I（读号 / 报号时最容易看错的那几个）。</summary>
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    const int CodeLen = 6;

    bool  _hovering;
    float _toastStart = -1f;      // < 0 = 提示不在显示

    /// <summary>当前号（复制用的就是它，不带「房间号：」前缀）。</summary>
    public string Code { get { return code; } }

    void Awake()
    {
        if (label == null) label = GetComponent<TextMeshProUGUI>();
        if (toastText != null)
        {
            var c = toastColor; c.a = 0f;
            toastText.color = c;
            toastText.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (regenerateOnEnable || string.IsNullOrEmpty(code)) code = NewCode();
        _toastStart = -1f;
        if (toastText != null) toastText.gameObject.SetActive(false);
        Apply();
    }

    void Update()
    {
        RefreshToast();
    }

    /// <summary>换号（真房间号接进来时调它）。</summary>
    public void SetCode(string newCode)
    {
        code = newCode;
        Apply();
    }

    void Apply()
    {
        if (label == null) return;
        label.text = prefix + code;
        label.color = _hovering ? hoverColor : normalColor;
    }

    static string NewCode()
    {
        var sb = new System.Text.StringBuilder(CodeLen);
        for (int i = 0; i < CodeLen; i++) sb.Append(Alphabet[Random.Range(0, Alphabet.Length)]);
        return sb.ToString();
    }

    // ── 悬停 / 点击 ──────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        if (label != null) label.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
        if (label != null) label.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(code)) return;

        GUIUtility.systemCopyBuffer = code;      // 复制纯号（不带前缀），粘到哪都能直接报给对面
        _toastStart = Time.unscaledTime;
        if (toastText != null) toastText.text = toastMessage;

        Debug.Log("[LobbyRoomCodeTag] 已复制到剪贴板: " + GUIUtility.systemCopyBuffer);
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
        if (e >= toastHold + toastFade)
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
        p.y = toastOffsetY + toastSlide * Mathf.Exp(-e * 18f);   // 弹出：从上边一丁点滑到位
        rt.anchoredPosition = p;
    }
}