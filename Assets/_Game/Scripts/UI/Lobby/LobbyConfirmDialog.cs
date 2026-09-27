using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「确认删除 / 确认拉黑」长条弹窗（2026-09-27）：一行标题（动词 + **金色的好友名**）+ 一排两个
/// 带子背景的键（确认 / 取消）。
/// </summary>
/// <remarks>
/// 用户 2026-09-27：「删除好友和拉黑好友都有一个长子弹窗，上面是确认删除/拉黑（金色的好友名称），
/// 下面是有子背景的确认和取消」。
///
/// 标题是**一条富文本**（不是两段拼位置）：<c>确认删除 &lt;color=#E4CB84&gt;在线甲&lt;/color&gt;</c> ——
/// 好友名长度不定，靠 TMP 自己居中，比摆两个文本块稳。
///
/// 层级：挂在 Canvas/Layer_Hud_v1 下的 Panel_Confirm（常驻 active），视觉全在子物体
/// <see cref="window"/>（**场景里存成 active**，方便在编辑器里看版式；运行时 Awake 第一帧自己收掉）——
/// 与 MatchConfirmPanel / LobbyInvitePanel 同一套写法，Ask / Hide 只切 window。
/// 好友详情是**全屏子弹窗**，确认窗必须压在它之上，所以走 HUD 层。
///
/// **不画遮罩**：用户定过「左上角和右上角的显示是在那些全屏显示的弹窗界面中仍显示在屏幕上」——
/// 一整块暗色 Dim 会把左上头像 / 右上货币一起压黑，与那条口径冲突。代价是点窗外不关窗，
/// 要关就点「取消」。
///
/// 贴图：LobbyConfirmPlate.png（Tools/cardframe/LobbyConfirmPlateV1.ps1）。
/// </remarks>
public class LobbyConfirmDialog : MonoBehaviour
{
    public static LobbyConfirmDialog Instance { get; private set; }

    [Header("视觉根（Ask / Hide 切这个）")]
    public GameObject window;

    [Header("标题（一条富文本：动词 + 金色的好友名）")]
    public TextMeshProUGUI titleText;

    [Header("有子背景的确认 / 取消")]
    public Button confirmButton;
    public Button cancelButton;

    [Header("配色：动词用奶油、好友名用亮金")]
    public Color verbColor = new Color32(240, 232, 210, 255);   // 奶油 #F0E8D2
    public Color whoColor  = new Color32(228, 203, 132, 255);   // 亮金 #E4CB84

    [Header("默认文案（Ask 不带参数时用）")]
    public string defaultVerb = "确认";

    Action _onConfirm;

    public bool IsOpen { get { return window != null && window.activeSelf; } }

    void Awake()
    {
        Instance = this;
        if (window != null) window.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>问一句：<paramref name="verb"/> 例「确认删除」，<paramref name="who"/> = 好友名（金色）。
    /// 点「确认」才执行 <paramref name="onConfirm"/>，点「取消」直接丢。</summary>
    public void Ask(string verb, string who, Action onConfirm)
    {
        _onConfirm = onConfirm;

        if (titleText != null)
        {
            string name = string.IsNullOrEmpty(who) ? "" : who;
            titleText.text = (string.IsNullOrEmpty(verb) ? defaultVerb : verb)
                             + " <color=#" + ColorUtility.ToHtmlStringRGB(whoColor) + ">" + name + "</color>";
            titleText.color = verbColor;
        }

        if (window != null)
        {
            window.SetActive(true);
            window.transform.SetAsLastSibling();
        }
        else
        {
            // 窗没接上就别把动作吞掉（调用方会看 Instance / IsOpen 判断）
            Debug.LogWarning("[LobbyConfirm] window 没连上 —— 这次确认窗开不出来");
            _onConfirm = null;
        }
    }

    /// <summary>确认键：先收窗，再执行（执行里可能又要开别的窗 / 弹提示）。</summary>
    public void Confirm()
    {
        Action act = _onConfirm;
        _onConfirm = null;
        Hide();
        if (act != null) act();
    }

    /// <summary>取消键：丢掉这一次。</summary>
    public void Cancel()
    {
        _onConfirm = null;
        Hide();
    }

    public void Hide()
    {
        _onConfirm = null;
        if (window != null) window.SetActive(false);
    }
}
