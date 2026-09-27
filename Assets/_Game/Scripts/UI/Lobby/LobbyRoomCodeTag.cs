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
/// 2026-09-27「接入」：号先由本面板现生成一个占位（6 位，字母表剔掉 0 O 1 I 这些容易看错的），
/// 房间面板一打开就由 <see cref="LobbyRoomSession"/> 去 Steam 建房，建好后用**真号**（发布到大厅数据
/// <c>room_code</c> 的那一串）调 <see cref="SetCode"/> 覆盖掉；Steam 未登录 / 未连接时改走
/// <see cref="SetUnavailable"/>（整行进灰、不给复制）。显示 / 复制 / 提示三件事不变。
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

    [Header("Steam 未登录 / 未连接时的灰态（这行换成一句话，且不给复制）")]
    public Color unavailableColor = new Color32(110, 119, 131, 255);   // 禁用灰 #6E7783

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

    bool  _explicit;              // 有人（LobbyRoomSession）显式给过号 / 灰态 → 面板重开不再重新生成
    bool  _locked;                // Steam 未连接 → 灰态：不复制、不悬停变金
    string _lockedText;
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
        // 显式给过号（真房间号 / 灰态）就不重新生成 —— 否则面板每次打开都会把真号顶掉（2026-09-27 实测踩到）
        if (!_explicit && (regenerateOnEnable || string.IsNullOrEmpty(code))) code = NewCode();
        _toastStart = -1f;
        if (toastText != null) toastText.gameObject.SetActive(false);
        Apply();
    }

    void Update()
    {
        RefreshToast();
    }

    /// <summary>换号 —— <see cref="LobbyRoomSession"/> 建房成功后拿 Steam 大厅里的真号调它。</summary>
    public void SetCode(string newCode)
    {
        _explicit = true;
        _locked = false;
        code = newCode;
        Apply();
    }

    /// <summary>Steam 未登录 / 未连接：这行进灰态（<see cref="LobbyRoomSession"/> 判定为假时调）。</summary>
    public void SetUnavailable(string msg = "Steam 未登录 / 未连接")
    {
        _explicit = true;
        _locked = true;
        _lockedText = msg;
        code = "";
        Apply();
    }

    void Apply()
    {
        if (label == null) return;
        if (_locked)
        {
            label.text = string.IsNullOrEmpty(_lockedText) ? "Steam 未登录 / 未连接" : _lockedText;
            label.color = unavailableColor;
            return;
        }
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
        if (_locked) return;
        _hovering = true;
        if (label != null) label.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_locked) return;
        _hovering = false;
        if (label != null) label.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_locked || string.IsNullOrEmpty(code)) return;

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