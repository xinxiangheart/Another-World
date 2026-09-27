using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 找到对手后的确认弹窗：左右两个头像（己方 / 对方）、中央 15 秒倒计时、左下「确认」、右下「拒绝」。
/// </summary>
/// <remarks>2026-09-27 用户：「现在做找到敌人后的确认弹窗左右两个图案分别是己方和对方头像，中间是一个
/// 15 秒倒计时（倒计时结束若双方有一个不确认就默认取消），左下角是确认，右下角是拒绝，非确认的头像
/// （一开始双方默认不确认）会有类似于战斗场景里的卡牌压黑机制（边框（本身白色）压黑后是灰色），确认后
/// 恢复原色，若双方点击确认，确认和拒绝会隐藏，中间的倒计时会变成金色 3 秒倒计时同时开始预加载战斗场景素材」。
///
/// 压黑机制：**不动贴图**，靠 <see cref="RawImage.color"/> 乘色 —— <c>Confirm_Frame.png</c> 本身是纯白，
/// 乘 <see cref="frameLocked"/>（钢灰 #6E7783）就是用户要的「灰边框」；确认后乘回纯白即恢复原色。
/// 头像图同乘一层灰（同战斗里卡牌压黑的手感）。
///
/// 2026-09-27 二次修（用户）：① 自己确认 = 收掉两个键、**不留任何字**，中央倒计时继续走；
/// ② 对方拒绝 = 对方那一侧压红 + 中央倒计时**停表并变红**（不显示「对方已拒绝」文字），短暂停留后自动重排；
/// ③ 超时/取消 = 直接关窗，不给提示。
///
/// 计时归本组件自己管（<see cref="acceptSeconds"/> / <see cref="goSeconds"/>），因为它要同时驱动
/// 中央那行数字的颜色与两键的显隐；超时 / 对方拒绝 / 己方拒绝的 Steam 收尾仍归 <see cref="QuickMatchPanel"/>
/// （大厅匹配状态机在那里），通过 <see cref="owner"/> 回调过去。
///
/// 层级：挂在 Canvas/Layer_Hud_v1 下的 Panel_MatchConfirm（常驻 active），视觉全在子物体
/// <see cref="window"/>（存 inactive）—— 与 MatchWaitPanel 同一套写法，Show/Hide 只切 window。
/// 贴图出图脚本：Tools/cardframe/MatchConfirmV1.ps1。
/// </remarks>
public class MatchConfirmPanel : MonoBehaviour
{
    public static MatchConfirmPanel Instance { get; private set; }

    [Header("视觉根（Show / Hide 切这个）")]
    public GameObject window;

    [Header("归属 —— 确认 / 拒绝 / 超时的 Steam 收尾都回调给它")]
    public QuickMatchPanel owner;

    [Header("己方")]
    public RawImage localAvatar;
    public RawImage localFrame;
    public TMP_Text localNameText;

    [Header("对方")]
    public RawImage opponentAvatar;
    public RawImage opponentFrame;
    public TMP_Text opponentNameText;

    [Header("中央倒计时")]
    public TextMeshProUGUI timerText;

    [Header("左下确认 / 右下拒绝（整组一起隐藏）")]
    public GameObject confirmGroup;
    public ConfirmActionButton confirmButton;
    public ConfirmActionButton declineButton;

    [Header("压黑 / 恢复配色 —— 色值抄 AGENTS「界面 / 场景美术方向」")]
    public Color frameLocked = new Color32(110, 119, 131, 255);   // 钢 #8EA2B4 压一档 → #6E7783（灰度读数明确是「灰」）
    public Color frameNormal = new Color32(255, 255, 255, 255);   // 边框贴图本身纯白
    public Color avatarLocked = new Color32(115, 115, 115, 255);  // 头像同乘一层灰（约 0.45 亮度）
    public Color avatarNormal = new Color32(255, 255, 255, 255);
    public Color timerNormal = new Color32(255, 255, 255, 255);   // 15 秒：白字（用户点名）
    public Color timerGold = new Color32(200, 164, 74, 255);      // 3 秒：金字 #C8A44A
    public Color timerWarn = new Color32(206, 151, 156, 255);     // 对方拒绝：倒计时停表变红用 #CE979C（生命亮档，不另起颜色）
    [Tooltip("对方拒绝时对方那一侧压红 —— 框用生命的 #B64848，头像乘一层红。")]
    public Color frameDeclined = new Color32(182, 72, 72, 255);   // 生命 #B64848
    public Color avatarDeclined = new Color32(255, 115, 115, 255); // 乘色压红（同压黑的机制，只换颜色）

    [Header("时长")]
    public float acceptSeconds = 15f;
    public float goSeconds = 3f;
    [Tooltip("对方拒绝时「停表 + 变红」停留多久再关窗（停留结束后自动重排）。")]
    public float noticeSeconds = 1.6f;


    enum State { Idle, Accept, Go, Notice }
    State _st = State.Idle;
    float _t;
    bool _localOk, _oppOk, _goStarted;
    bool _reMatchPending;   // 对方拒绝 → 停留结束后自动重排（自己拒绝不置这个）

    public bool IsOpen { get { return window != null && window.activeSelf; } }
    public float GoSeconds { get { return goSeconds; } }

    void Awake()
    {
        Instance = this;
        if (window != null) window.SetActive(false);
    }

    // ===================== 开关 =====================

    /// <summary>弹窗（找到对手时由 QuickMatchPanel 调）。两边默认「未确认」→ 都压黑。</summary>
    public void Open()
    {
        var sd = SteamDataManager.Instance;

        // 己方：Steam 头像 + 昵称（未就绪给灰圆盘占位，不留黑洞）
        if (localNameText != null)
            localNameText.text = (sd != null && !string.IsNullOrEmpty(sd.localPlayerName)) ? sd.localPlayerName : "我";
        ApplyAvatar(localAvatar, sd != null ? sd.localAvatar : null);

        // 对方：QuickMatchPanel 已经按对手 SteamID 取过图了
        string oppName = owner != null ? owner.opponentName : null;
        if (opponentNameText != null)
            opponentNameText.text = string.IsNullOrEmpty(oppName) ? "对手" : oppName;
        ApplyAvatar(opponentAvatar, owner != null ? owner.opponentTexture as Texture2D : null);

        // 双方默认未确认
        _localOk = false; _oppOk = false; _goStarted = false; _reMatchPending = false;
        SetSideAccepted(true, false);
        SetSideAccepted(false, false);

        _st = State.Accept;
        _t = Mathf.Max(1f, acceptSeconds);
        if (confirmGroup != null) confirmGroup.SetActive(true);
        if (confirmButton != null) confirmButton.SetInteractable(true);
        if (declineButton != null) declineButton.SetInteractable(true);
        if (timerText != null) timerText.color = timerNormal;
        Tick();

        if (window != null) window.SetActive(true);
    }

    /// <summary>关窗（进游戏 / 取消 / 拒绝之后都走这里）。</summary>
    public void Hide()
    {
        _st = State.Idle;
        if (window != null) window.SetActive(false);
    }

    // ===================== 双方确认 =====================

    /// <summary>己方点了「确认」（ConfirmActionButton 回调）。只认一次。</summary>
    public void OnConfirmClicked()
    {
        if (_st != State.Accept || _localOk) return;
        _localOk = true;
        SetSideAccepted(true, true);
        if (confirmButton != null) confirmButton.SetInteractable(false);   // 防重复点击
        // 用户 2026-09-27：「自己确认也会隐藏确认和拒绝 button」→ 两个键整组收掉；
        // 同日二次修：「等待对方确认也不显示字」→ 不留任何字，只留中央那行 15 秒倒计时继续走（它决定超时取消）。
        if (confirmGroup != null) confirmGroup.SetActive(false);
        if (owner != null) owner.OnAccept();                              // 写 host_ok / guest_ok，转 WaitingOpponent
        TryGo();
    }

    /// <summary>己方点了「拒绝」（ConfirmActionButton 回调）—— 收尾在 QuickMatchPanel。</summary>
    public void OnDeclineClicked()
    {
        if (_st != State.Accept && _st != State.Go) return;
        Debug.Log("[MatchConfirm] 己方点「拒绝」→ 本局取消（自己拒绝**不重排**）");
        _reMatchPending = false;
        if (owner != null) owner.OnDecline();
        else Hide();
    }

    /// <summary>对手确认了（QuickMatchPanel 读到大厅数据里对方那格 = 1）→ 解开对方头像的压黑。</summary>
    public void NotifyOpponentAccepted()
    {
        if (_oppOk) return;
        _oppOk = true;
        SetSideAccepted(false, true);
        TryGo();
    }

    void TryGo() { if (_localOk && _oppOk) BeginGo(); }

    /// <summary>双方都确认：收掉「确认 / 拒绝」两个键，倒计时转金色 3 秒，同时开始预加载战斗场景素材。</summary>
    public void BeginGo()
    {
        if (_goStarted) return;
        _goStarted = true;
        _st = State.Go;
        _t = Mathf.Max(0f, goSeconds);
        if (confirmGroup != null) confirmGroup.SetActive(false);
        if (timerText != null) timerText.color = timerGold;
        Tick();
        EnsurePreloader();
        if (Preloader.Instance != null) Preloader.Instance.StartPreload();
        Debug.Log("[MatchConfirm] 双方已确认 → 金色 3 秒倒计时 + 开始预加载战斗素材");
    }

    // ===================== 超时 / 对方拒绝 =====================

    /// <summary>15 秒到还没双确认 —— 用户：「倒计时结束若双方有一个不确认就默认取消」。
    /// 2026-09-27 用户补：「取消就直接关闭这个确认弹窗即可，不需要停留」→ 关窗走人，不留提示。
    /// Steam 收尾仍交回 QuickMatchPanel（OnConfirmTimeout）。</summary>
    public void Timeout()
    {
        if (!IsOpen) return;
        Hide();
        if (owner != null) owner.OnConfirmTimeout();
    }

    /// <summary>对方拒了（用户 2026-09-27）：自己这边**对方那一侧压红**，短暂停留后自动关窗并**重排**。
    /// 「自己拒绝的不会重排，对方拒绝的自己会再次进入匹配池子」—— 自己拒绝走 OnDeclineClicked，不经过这里。</summary>
    public void OpponentDeclined()
    {
        if (!IsOpen) return;
        _reMatchPending = true;
        SetSideDeclined(false);        // 只有对方那一侧压红
        NoticeStopRed();               // 中央倒计时停表 + 变红（用户：不显示「对方已拒绝」几个字）
    }

    /// <summary>对方那一侧的框 + 头像压红（同压黑的乘色机制，只换颜色）。</summary>
    void SetSideDeclined(bool isLocal)
    {
        var frame = isLocal ? localFrame : opponentFrame;
        var avatar = isLocal ? localAvatar : opponentAvatar;
        if (frame != null) frame.color = frameDeclined;
        if (avatar != null) avatar.color = avatarDeclined;
    }

    /// <summary>对方拒绝（用户 2026-09-27）：「不显示『对方已拒绝』，而是倒计时停止并变红以示对方拒绝」。
    /// 数字停在当前值、颜色换成红，短暂停留后由 Update 收尾（关窗 + 自动重排）。</summary>
    void NoticeStopRed()
    {
        _st = State.Notice;
        _t = Mathf.Max(0.4f, noticeSeconds);
        if (confirmGroup != null) confirmGroup.SetActive(false);
        if (timerText != null) timerText.color = timerWarn;   // 停表 + 变红；文字仍是那个数字，不换文案
    }

    // ===================== 计时 =====================

    void Update()
    {
        if (!IsOpen) return;

        switch (_st)
        {
            case State.Accept:
                _t -= Time.unscaledDeltaTime;
                Tick();
                if (_t <= 0f) Timeout();
                break;

            case State.Go:
                _t -= Time.unscaledDeltaTime;
                Tick();
                break;

            case State.Notice:
                _t -= Time.unscaledDeltaTime;
                if (_t <= 0f)
                {
                    Hide();
                    if (_reMatchPending)
                    {
                        _reMatchPending = false;
                        if (owner != null) owner.OnReMatchAfterDecline();   // 对方拒绝 → 自己再次进匹配池子
                    }
                }
                break;
        }
    }

    void Tick()
    {
        if (timerText == null) return;
        timerText.text = Mathf.Max(0, Mathf.CeilToInt(_t)).ToString();
    }

    // ===================== 小工具 =====================

    /// <summary>框 / 头像一起切：未确认 = 压黑，已确认 = 恢复原色。框贴图本身纯白，乘灰就是「灰边框」。</summary>
    void SetSideAccepted(bool isLocal, bool ok)
    {
        var frame = isLocal ? localFrame : opponentFrame;
        var avatar = isLocal ? localAvatar : opponentAvatar;
        if (frame != null) frame.color = ok ? frameNormal : frameLocked;
        if (avatar != null) avatar.color = ok ? avatarNormal : avatarLocked;
    }

    /// <summary>铺头像（圆形裁切复用 PlayerProfilePanel 那套，源图不可读时退回方形）。</summary>
    static void ApplyAvatar(RawImage img, Texture2D src)
    {
        if (img == null) return;
        if (src == null) { img.texture = PlayerProfilePanel.Placeholder(); return; }
        var tex = PlayerProfilePanel.CircleCrop(src);
        img.texture = tex != null ? tex : src;
    }

    static void EnsurePreloader()
    {
        if (Preloader.Instance == null)
        {
            var go = new GameObject("Preloader");
            go.AddComponent<Preloader>();
        }
    }
}