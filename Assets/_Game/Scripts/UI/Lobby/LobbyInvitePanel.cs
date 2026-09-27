using System.Collections.Generic;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「收到邀请」小窗（2026-09-27）：从屏幕中央顶侧滑出，标题「收到邀请」+ 一排对方头像 / 名称 +
/// 「同意 / 拒绝」两个带子背景的键。
/// </summary>
/// <remarks>
/// 用户 2026-09-27：「邀请是从屏幕中央上顶滑出一个小框，上面标题是收到邀请，下面一排是对应玩家头像和名称，
/// 在下面是有子背景的同意和拒绝，同意后会加入其房间，拒绝后对方也会收到：对方暂无法响应」。
///
/// **同意** = 进对方的房：<see cref="LobbyRoomPanel.OpenAsGuest"/>（开面板但**不**建自己的房 ——
/// 建房再 JoinLobby 会撞车）→ <see cref="LobbyRoomSession.JoinInviteAsGuest"/>（真 JoinLobby）。
/// 大厅 ID 是邀请回调直接给的（<c>LobbyInvite_t.m_ulSteamIDLobby</c>），所以不用按号搜。
/// **拒绝** = 关上窗 + <see cref="LobbyInviteService.PublishDecline"/> 回一句（对方弹「对方暂无法响应」）。
///
/// 一次只摆一条：开着的时候又来一条就排队，等当前这条收窗（滑回屏幕顶之上）再摆下一条 ——
/// 所以 <see cref="Show"/> 可以是队列入口。
///
/// 滑动：锚屏幕顶中、pivot 顶中，anchoredPosition.y 从 <see cref="hiddenY"/>（整块在屏幕顶之上）
/// 收到 <see cref="restY"/>，曲线与两个侧边栏同一条（1-(1-p)^3，快进慢出）。窗口的显隐由 _p 自己管，
/// 到位之后 window 才 SetActive(false)，所以收窗也有动画。
///
/// 层级：挂在 Canvas/Layer_Hud_v1 下的 Panel_Invite（存 inactive 的 window），与 Panel_MatchWait /
/// Panel_MatchConfirm 同级 —— HUD 层是 Canvas 的最后一个子物体，所以它永远压在房间面板等全屏弹窗之上。
/// 贴图：Invite_Plate.png（Tools/cardframe/InvitePanelV1.ps1）。
/// </remarks>
public class LobbyInvitePanel : MonoBehaviour
{
    public static LobbyInvitePanel Instance { get; private set; }

    [Header("视觉根（滑动期间才 active）")]
    public GameObject window;
    [Tooltip("滑动的那块（留空 = 取 window 的 RectTransform）")]
    public RectTransform windowRect;

    [Header("标题")]
    public TextMeshProUGUI titleText;
    public string title = "收到邀请";

    [Header("一排：对方头像 + 名称")]
    public RawImage avatarImage;
    public TMP_Text nameText;

    [Header("同意 / 拒绝（各带子背景）")]
    public GameObject chipsGroup;
    public Button acceptButton;
    public Button declineButton;

    [Header("滑动（画布 1080 口径；锚屏幕顶中、pivot 顶中 —— 正 y = 屏幕顶之上）")]
    public float restY = -10f;        // 静止位：贴着屏幕顶（与匹配小窗同档）
    public float hiddenY = 260f;      // 起点：整块（高 240）完全在屏幕顶之上
    public float showTime = 0.28f;    // 滑出（用户：「迅速滑出」那条手感留给侧边栏，这里小窗慢一档更好认）
    public float hideTime = 0.16f;

    [Header("顶开提示行（提示行也在屏幕顶中，会撞上小窗）")]
    [Tooltip("小窗开着时把 LobbyToast 往下顶多少（≈ 小窗底沿 240 + 10）")]
    public float toastDrop = 250f;

    [Header("配色（与房间面板 / 确认弹窗同一套）")]
    public Color cream = new Color32(240, 232, 210, 255);   // 奶油 #F0E8D2

    class Pending
    {
        public ulong    inviter;
        public string   name;
        public CSteamID lobby = CSteamID.Nil;
    }

    readonly List<Pending> _queue = new List<Pending>();
    Pending _cur;
    float _p;             // 0 = 全收（在屏幕顶之上），1 = 到位
    float _target;
    bool _busy;           // 已经按过同意 / 拒绝，或正在收窗 —— 期间不再接受点击
    Texture2D _avatarSeen;  // 上次拿去重裁的 Steam 头像原图（没到货时是 null）
    float _avatarRetryAt;   // 下次问缓存的时间（0.5s 一轮，与 FriendRowUI.TickAvatar 同一条）

    /// <summary>开着（含正在开）= true。</summary>
    public bool IsOpen { get { return _target > 0f; } }
    /// <summary>window 真的 active（滑到位 / 滑动中）。</summary>
    public bool Visible { get { return window != null && window.activeSelf; } }
    public ulong CurrentInviter { get { return _cur != null ? _cur.inviter : 0UL; } }
    public int QueueCount { get { return _queue.Count; } }
    /// <summary>当前这块离屏幕顶多远（诊断用；静止时应等于 restY）。</summary>
    public float WindowY { get { return windowRect != null ? windowRect.anchoredPosition.y : 0f; } }

    void Awake()
    {
        Instance = this;
        if (windowRect == null && window != null) windowRect = window.transform as RectTransform;
        _p = 0f; _target = 0f;
        if (window != null) window.SetActive(false);
        if (windowRect != null) windowRect.anchoredPosition = new Vector2(windowRect.anchoredPosition.x, hiddenY);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ===================== 开关 =====================

    /// <summary>收到一条邀请。返回 true = 立刻摆出来了（false = 排在队里）。</summary>
    public bool Show(ulong inviterId, string inviterName, ulong lobbyId)
    {
        var p = new Pending
        {
            inviter = inviterId,
            name    = string.IsNullOrEmpty(inviterName) ? "好友" : inviterName,
            lobby   = new CSteamID(lobbyId)
        };

        if (_cur != null || _p > 0f) { _queue.Add(p); Debug.Log("[Invite] 已有一条在先，这条排队（队 " + _queue.Count + "）"); return false; }

        Present(p);
        return true;
    }

    /// <summary>收窗（滑回屏幕顶之上；到位后 window 自己关）。</summary>
    public void Hide()
    {
        _busy = true;
        _target = 0f;
        // 还没滑出来就收（Present 之后同帧被打回 / 走脚本调用）：_p 已经是 0，Update 的首行判断会直接 return，
        // 那就永远停在「active 但看不见」—— _cur 也不清，后面每一条邀请都只排队。这里当场收干净。
        if (_p <= 0f) Collapse();
    }

    /// <summary>真正收干净：藏起 window、还原提示行、清当前这条、接着摆下一条。</summary>
    void Collapse()
    {
        _p = 0f;
        _target = 0f;
        if (window != null) window.SetActive(false);
        if (windowRect != null) windowRect.anchoredPosition = new Vector2(windowRect.anchoredPosition.x, hiddenY);
        LobbyToast.SetExtraDrop(0f);        // 收窗 → 提示行回原位

        _cur = null;
        if (_queue.Count > 0)               // 队里还有 → 接着摆下一条
        {
            Pending n = _queue[0];
            _queue.RemoveAt(0);
            Present(n);
        }
    }

    void Present(Pending p)
    {
        _cur = p;
        _busy = false;

        if (titleText != null) titleText.text = title;
        if (nameText != null) { nameText.text = p.name; nameText.color = cream; }
        ApplyAvatar(p.inviter, true);
        _avatarSeen = SteamAvatarManager.PeekAvatar(p.inviter);

        if (chipsGroup != null) chipsGroup.SetActive(true);
        if (acceptButton != null) acceptButton.interactable = true;
        if (declineButton != null) declineButton.interactable = true;

        if (window != null) window.SetActive(true);
        _target = 1f;
        _p = 0f;                     // 从屏幕顶之上滑入
        Apply();
        LobbyToast.SetExtraDrop(toastDrop);   // 提示行让开小窗那一条（它也在屏幕顶中）
        Debug.Log("[Invite] 弹出「收到邀请」：" + p.name + "(" + p.inviter + ")  lobby=" + p.lobby.m_SteamID);
    }

    void Update()
    {
        TickAvatar();
        if (_target > 0f ? _p >= 1f : _p <= 0f) return;

        float dur = _target > 0f ? showTime : hideTime;
        _p = Mathf.MoveTowards(_p, _target, dur <= 0f ? 1f : Time.unscaledDeltaTime / dur);
        Apply();

        if (_p <= 0f) Collapse();
    }

    void Apply()
    {
        if (windowRect == null) return;
        float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(_p), 3f);     // 快进慢出（与两个侧边栏同一条曲线）
        windowRect.anchoredPosition = new Vector2(windowRect.anchoredPosition.x, Mathf.Lerp(hiddenY, restY, e));
    }

    /// <summary>到货之后补上：Present 那一刻 Steam 往往还没把图给出来（GetAvatarTexture 只是触发请求，
    /// 当场拿到的是灰占位）—— 所以开着的时候每 0.5s 问一次缓存，拿到真图才重裁一次。</summary>
    void TickAvatar()
    {
        if (_cur == null || avatarImage == null || _cur.inviter == 0UL) return;
        if (Time.unscaledTime < _avatarRetryAt) return;
        _avatarRetryAt = Time.unscaledTime + 0.5f;

        Texture2D tex = SteamAvatarManager.PeekAvatar(_cur.inviter);
        if (tex == null || tex == _avatarSeen) return;      // 没到货 / 还是同一张：不动
        _avatarSeen = tex;
        ApplyAvatar(_cur.inviter, false);
    }

    /// <summary>request = true 时真的去问 Steam（顺带触发下载）；之后只查缓存。</summary>
    void ApplyAvatar(ulong steamId, bool request)
    {
        if (avatarImage == null) return;
        Texture2D tex = steamId != 0UL
            ? (request ? SteamAvatarManager.GetAvatarTexture(steamId) : SteamAvatarManager.PeekAvatar(steamId))
            : null;
        avatarImage.texture = tex != null ? PlayerProfilePanel.CircleCrop(tex) : PlayerProfilePanel.Placeholder();
        avatarImage.color = Color.white;
    }

    // ===================== 两个键 =====================

    /// <summary>同意 → 进对方的房间（开面板但不建自己的房，再真 JoinLobby）。</summary>
    public void Accept()
    {
        if (_busy || _cur == null) return;
        _busy = true;
        if (acceptButton != null) acceptButton.interactable = false;
        if (declineButton != null) declineButton.interactable = false;

        Pending p = _cur;
        LobbyRoomPanel room = LobbyRoomPanel.Instance;
        LobbyRoomSession session = LobbyRoomSession.Instance;
        if (room == null || session == null)
        {
            LobbyToast.Show("房间服务没起来，加入失败");
            Hide();
            return;
        }

        room.OpenAsGuest();                                   // 只开面板：客人视角不建自己的房
        Debug.Log("[Invite] 同意 " + p.name + " 的邀请 → 进房间 lobby=" + p.lobby.m_SteamID);
        Hide();                                               // 先收窗，再等 JoinLobby 的回调

        session.JoinInviteAsGuest(p.lobby, room, (ok, msg) =>
        {
            if (!ok) LobbyToast.Show(string.IsNullOrEmpty(msg) ? "加入失败，请重试" : msg);
        });
    }

    /// <summary>拒绝 → 关窗 + 给对面回一句「暂无法响应」。</summary>
    public void Decline()
    {
        if (_busy || _cur == null) return;
        _busy = true;

        ulong inviter = _cur.inviter;
        LobbyInviteService svc = LobbyInviteService.Instance;
        if (svc != null) svc.PublishDecline(inviter);
        Debug.Log("[Invite] 拒绝 " + inviter + " 的邀请");

        Hide();
    }
}
