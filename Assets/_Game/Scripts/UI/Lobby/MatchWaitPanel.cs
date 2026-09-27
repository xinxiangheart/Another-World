using UnityEngine;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// 匹配等待小视窗：屏幕顶中一块小窗，上一行「匹配中：x：xx」（金字 + 白字计时），
/// 下一行「取消」键（白字，悬停变金，自带只覆盖两个字的子背景）；匹配到人时取消整组隐藏、文字变「已找到对手！」。
/// </summary>
/// <remarks>2026-09-27 用户定：
/// 「屏幕中央顶侧出现小视窗，上面一栏是：匹配中（金字）：x：xx，x 是匹配时间（白字），下面是取消 button
///   （白字，悬停变金色），其有着子背景（只覆盖取消这两个字即可，用于提示），当匹配到人时取消文字和背景隐藏，
///   匹配中变成：已找到对手！」。
///
/// 本组件只做「显示 / 文字 / 计时 / 换状态」，**不认识 QuickMatchPanel** —— 取消键的点击走
/// <see cref="onCancel"/>（编辑器里连到 QuickMatchPanel.CancelMatch）。状态由 QuickMatchPanel 通过
/// <see cref="SetNotice"/> / <see cref="SetFound"/> 推过来。
///
/// 一行两色用 TMP 富文本（<c>&lt;color=#RRGGBB&gt;</c>）—— 这样计时从 0：09 走到 0：10 时整行仍然自动居中，
/// 不用手算两段宽度。文字里用的全角冒号是用户原话写法。
///
/// 层级：挂在 Canvas/Layer_Hud_v1 下（常驻 HUD 层，永远画在所有弹窗之上）。组件挂**常驻 active** 的
/// Panel_MatchWait 上，视觉部分在子物体 <see cref="window"/> 里 —— 这样 Show/Hide 只切 window，
/// 本组件的 Update（计时）与协程不受影响。
/// </remarks>
public class MatchWaitPanel : MonoBehaviour
{
    [Header("视觉根（Show / Hide 切这个）")]
    public GameObject window;

    [Header("第一行：状态 + 计时")]
    public TextMeshProUGUI stateText;
    [Tooltip("状态行的 RectTransform —— 找到对手 / 报错时移到窗口正中。")]
    public RectTransform stateRect;
    [Tooltip("搜索中：状态行中心相对窗口顶的距离（负值向下）。")]
    public float searchingY = -46f;
    [Tooltip("找到对手 / 报错：状态行中心相对窗口顶的距离（= 窗口高一半 = 竖直居中）。")]
    public float centeredY = -76f;
    [Tooltip("状态行在两个位置之间挪动的时长（秒）。")]
    public float moveTime = 0.18f;

    [Header("第二行：取消（子背景 + 两个白字）")]
    public GameObject cancelGroup;

    [Header("点击取消")]
    public UnityEvent onCancel;

    [Header("配色")]
    public string goldHex = "#C8A44A";     // 匹配中 / 已找到对手（本套金）
    public string whiteHex = "#FFFFFF";    // 计时 / 取消（用户点名白字）
    public string warnHex = "#CE979C";     // 出错提示（本套生命的亮档，不另起颜色）

    [Header("文案")]
    public string searchingLabel = "匹配中：";
    public string foundLabel = "已找到对手！";
    [Tooltip("搜索 / 找到态的字号。")]
    public float stateFontSize = 30f;
    [Tooltip("提示态（Steam 未登录一类）的字号 —— 那行字更长，要缩小并允许折行，否则会横向溢出窗口。")]
    public float noticeFontSize = 22f;

    float _t;
    bool _running;
    bool _found;
    bool _warn;        // 报错态（停表 + 藏取消 + 居中）
    float _y;          // 状态行当前 y（自己 Lerp，不做成动画曲线）

    public bool IsOpen { get { return window != null && window.activeSelf; } }

    void Awake()
    {
        if (window != null && window.activeSelf) window.SetActive(false);
        _y = searchingY;
    }

    void Update()
    {
        if (!IsOpen) return;

        if (_running)
        {
            _t += Time.unscaledDeltaTime;
            stateText.text = Col(goldHex, searchingLabel) + Col(whiteHex, Fmt(_t));
        }

        if (stateRect != null)
        {
            float want = (_found || _warn) ? centeredY : searchingY;
            if (Mathf.Abs(_y - want) > 0.05f)
            {
                _y = moveTime <= 0f ? want : Mathf.Lerp(_y, want, Time.unscaledDeltaTime / moveTime);
                stateRect.anchoredPosition = new Vector2(stateRect.anchoredPosition.x, _y);
            }
        }
    }

    /// <summary>显示小窗，计时从 0 起。</summary>
    public void Show()
    {
        SteamPresence.Matching();   // 搜索中 → 好友列表里那行「匹配中」（金）
        _t = 0f; _found = false; _warn = false; _running = true;
        _y = searchingY;
        if (stateRect != null) stateRect.anchoredPosition = new Vector2(stateRect.anchoredPosition.x, _y);
        if (cancelGroup != null) cancelGroup.SetActive(true);
        SetNoticeFont(false);
        if (stateText != null) stateText.text = Col(goldHex, searchingLabel) + Col(whiteHex, Fmt(0f));
        if (window != null) window.SetActive(true);
    }

    public void Hide()
    {
        SteamPresence.Idle();       // 不在搜索了 → 好友看到「在线」（绿）
        _running = false;
        if (window != null) window.SetActive(false);
    }

    /// <summary>外部（QuickMatchPanel.SetStatus）推过来的状态文字。
    /// 含「匹配中」= 回到搜索态（计时 + 取消）；其余按提示处理（停表 + 藏取消 + 居中）。</summary>
    public void SetNotice(string msg)
    {
        if (string.IsNullOrEmpty(msg) || !IsOpen) return;
        if (_found) return;                       // 已经找到对手，后续状态（「已接受…」）不再改这一行

        if (msg.Contains("匹配中"))
        {
            _warn = false; _running = true;
            if (cancelGroup != null) cancelGroup.SetActive(true);
            SetNoticeFont(false);
            stateText.text = Col(goldHex, searchingLabel) + Col(whiteHex, Fmt(_t));
        }
        else
        {
            _warn = true; _running = false;
            if (cancelGroup != null) cancelGroup.SetActive(false);
            SetNoticeFont(true);          // 提示更长：缩小 + 折行，别横向溢出窗口
            stateText.text = Col(warnHex, msg);
        }
    }

    /// <summary>匹配到人：藏掉「取消」整组，文字改「已找到对手！」并居中。</summary>
    public void SetFound()
    {
        _found = true; _warn = false; _running = false;
        if (cancelGroup != null) cancelGroup.SetActive(false);
        SetNoticeFont(false);
        if (stateText != null) stateText.text = Col(goldHex, foundLabel);
        if (window != null && !window.activeSelf) window.SetActive(true);
    }

    /// <summary>取消键的点击（场景里由 MatchWaitCancelButton 调；也可直接挂 Button）。</summary>
    public void Cancel()
    {
        Hide();
        if (onCancel != null) onCancel.Invoke();
    }

    /// <summary>计时文案：用户原话是「x：xx」—— 分钟不补零、秒补两位、全角冒号。</summary>
    public static string Fmt(float sec)
    {
        if (sec < 0f) sec = 0f;
        int s = (int)sec;
        return string.Format("{0}：{1:00}", s / 60, s % 60);
    }

    /// <summary>提示态用小字号 + 允许折行（那两行字比窗口宽，30 号会顶出板子）。</summary>
    void SetNoticeFont(bool small)
    {
        if (stateText == null) return;
        stateText.fontSize = small ? noticeFontSize : stateFontSize;
        stateText.enableWordWrapping = small;
    }

    static string Col(string hex, string s) { return "<color=" + hex + ">" + s + "</color>"; }
}
