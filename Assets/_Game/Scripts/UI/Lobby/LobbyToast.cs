using UnityEngine;
using TMPro;

/// <summary>
/// 大厅屏幕**中央上方**那行一次性提示（「房主已离开，你已成为房主」/「玩家xxxx离开」）。
/// </summary>
/// <remarks>2026-09-27 用户：「同时屏幕中央上方弹出提示：房主已离开，你已成为房主」「同时弹出提示：玩家xxxx离开」。
///
/// 挂在 Canvas/Layer_Hud_v1 下 —— HUD 层是 Canvas 的最后一个子物体，所以房间面板开着时这行字照样压在它上面。
/// 物体本身**常驻 active**（只把 alpha 归零），因为它自己的 Update 要负责淡出；关掉物体就再也不会淡出了
/// （左下角那行 ID 的提示语是反过来的做法：那里提示是子物体、由父物体驱动）。
/// 时间轴：淡入 fadeInSeconds → 全亮 holdSeconds → 淡出 fadeOutSeconds，弹出时从上面一丁点滑到位。
/// </remarks>
public class LobbyToast : MonoBehaviour
{
    public static LobbyToast Instance { get; private set; }

    [Header("文字（留空 = 取自己身上的 TextMeshProUGUI）")]
    public TextMeshProUGUI text;

    [Header("时长")]
    public float fadeInSeconds = 0.22f;
    public float holdSeconds = 2.2f;
    public float fadeOutSeconds = 0.55f;

    [Header("弹出：从上边滑进来的距离")]
    public float slideFrom = -18f;

    [Header("配色（本套奶油 #F0E8D2）")]
    public Color color = new Color32(240, 232, 210, 255);

    float _t = -1f;        // < 0 = 不在显示
    float _baseY;
    float _extraDrop;      // 临时往下让出的距离（见 SetExtraDrop）

    void Awake()
    {
        Instance = this;
        if (text == null) text = GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            _baseY = text.rectTransform.anchoredPosition.y;
            var c = color; c.a = 0f;
            text.color = c;
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>弹一句（静态入口 —— 房间那边直接 LobbyToast.Show("…")）。重复调用就是换句话重头播。</summary>
    public static void Show(string msg)
    {
        if (Instance != null) Instance.ShowNow(msg);
        else Debug.Log("[LobbyToast] " + msg + "（场景里没有 LobbyToast，没能弹出来）");
    }

    /// <summary>
    /// 临时把这行提示往下顶一段（0 = 还原）。
    /// </summary>
    /// <remarks>2026-09-27：「收到邀请」小窗也占屏幕顶中那一块，两者会正面撞上（实测提示行正好压在小窗的两个键上）。
    /// 谁占着这块谁调它 —— 小窗由 <see cref="LobbyInvitePanel"/> 在滑出时顶下来、收窗时还原。
    /// 只动这一次性的提示行，不动任何常驻件的位置。</remarks>
    public static void SetExtraDrop(float drop)
    {
        if (Instance != null) Instance._extraDrop = Mathf.Max(0f, drop);
    }

    public void ShowNow(string msg)
    {
        if (text == null) return;
        text.text = msg;
        _t = 0f;
        var c = color; c.a = 0f;
        text.color = c;
        var rt = text.rectTransform;
        var p = rt.anchoredPosition;
        p.y = _baseY + _extraDrop + slideFrom;
        rt.anchoredPosition = p;
    }

    void Update()
    {
        if (text == null || _t < 0f) return;

        _t += Time.unscaledDeltaTime;
        float total = fadeInSeconds + holdSeconds + fadeOutSeconds;
        if (_t >= total)
        {
            _t = -1f;
            var c0 = color; c0.a = 0f;
            text.color = c0;
            return;
        }

        float a;
        if (_t < fadeInSeconds) a = _t / Mathf.Max(0.01f, fadeInSeconds);
        else if (_t < fadeInSeconds + holdSeconds) a = 1f;
        else a = 1f - (_t - fadeInSeconds - holdSeconds) / Mathf.Max(0.01f, fadeOutSeconds);

        var c = color; c.a = color.a * Mathf.Clamp01(a);
        text.color = c;

        var rt = text.rectTransform;
        var p = rt.anchoredPosition;
        p.y = _baseY + _extraDrop + slideFrom * Mathf.Exp(-_t * 16f);   // 弹出：一丁点滑到位
        rt.anchoredPosition = p;
    }
}
