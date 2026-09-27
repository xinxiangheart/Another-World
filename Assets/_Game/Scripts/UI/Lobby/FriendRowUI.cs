using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友列表里的一行：Steam 头像（圆裁进金环）+ 昵称 + 状态小字。
/// 场景里只有一份**模板**（inactive），运行时由 <see cref="LobbyFriendListUI"/> 克隆 + <see cref="Bind"/>。
/// 头像：先把井里铺一块灰盘占位（别留黑洞），Steam 那头像到货后自己换上去。
///
/// 2026-09-27 加右端那个**邀请加号**（用户：「好友栏中处于在线（非战斗状态和匹配状态）时其名字右边会出现一个
/// 加号，点击后发送邀请并且加号变成 10 秒倒计时」）：
///   • 显隐只看 <see cref="FriendPresence.Online"/> —— 离线 / 匹配中 / 对局中都不出现（所以名字那行的宽度
///     在场景里已经按「让出加号那一格」收窄过）。
///   • 点下去 → <see cref="LobbyInviteService.Invite"/>（它负责开房 + Steam 邀请）；冷却期间这一格换成
///     **同一块空板 + 金数字**（<c>Icon_InvitePlusCool</c>），数字由 <see cref="LobbyInviteService.CooldownLeft"/>
///     驱动 —— 冷却按 SteamID 存在服务里，所以列表每 20 秒重排一次也不会把倒计时洗掉。
///   • 冷却没走完时点它没有任何反应（<see cref="InviteClicked"/> 直接返回）。
/// </summary>
public class FriendRowUI : MonoBehaviour
{
    [Header("头像（金环井里那块，运行时圆裁）")] public RawImage avatarImage;
    [Header("文字")] public TMP_Text nameText;
    public TMP_Text statusText;

    [Header("邀请加号（只有「在线」那一档才出现；整个场景里只有这一格）")]
    [Tooltip("整块（底 + 加号 / 倒计时数字）；留空 = 这一行没有邀请入口")] public GameObject inviteGroup;
    [Tooltip("贴图换在这一张上：加号 / 悬停 / 倒计时那块空板")] public RawImage inviteIcon;
    [Tooltip("倒计时数字（金，冷却期间才出现）")] public TMP_Text inviteTimer;

    [Header("三张贴图（Tools/cardframe/LobbyInvitePlusV1.ps1 出）")]
    public Texture inviteNormal;
    public Texture inviteHover;
    public Texture inviteCool;

    const float AvatarRetryStep = 0.5f;      // 到货前 0.5 秒问一次
    const float AvatarGiveUpSeconds = 20f;   // 20 秒还没到就认了（保持灰盘）

    ulong _steamId;
    string _displayName;
    bool _canInvite;                          // 这一档给不给加号（= Presence 是不是 Online）
    FriendInviteButton _inviteButton;
    float _nextTryAt;
    float _giveUpAt;

    /// <summary>加号这一格现在是不是在倒计时。</summary>
    public bool InviteCooling { get { return _steamId != 0UL && LobbyInviteService.CooldownLeft(_steamId) > 0f; } }

    /// <summary>填一行。e == null → 直接藏起来。</summary>
    public void Bind(FriendEntry e)
    {
        if (e == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        if (nameText != null) nameText.text = e.DisplayName;
        if (statusText != null)
        {
            statusText.text = e.StatusLabel;
            statusText.color = e.StatusColor;
        }

        _steamId = e.SteamId;
        _displayName = e.DisplayName;
        // 用户口径：只有「在线」（既不在匹配、也不在对局）才给加号；另外没有 Steam 身份就没法邀
        _canInvite = e.Presence == FriendPresence.Online && e.SteamId != 0UL;
        _nextTryAt = 0f;
        _giveUpAt = Time.unscaledTime + AvatarGiveUpSeconds;
        ApplyAvatar(true);
        TickInvite();
    }

    void Update()
    {
        TickAvatar();
        TickInvite();
    }

    void TickAvatar()
    {
        if (_steamId == 0UL) return;                                    // 没有 Steam 身份：灰盘就够了
        if (Time.unscaledTime < _nextTryAt) return;
        if (Time.unscaledTime > _giveUpAt) return;
        _nextTryAt = Time.unscaledTime + AvatarRetryStep;
        ApplyAvatar(false);
    }

    // ===================== 右端那一格：加号 / 倒计时 =====================

    /// <summary>点加号（<see cref="FriendInviteButton"/> 转过来）。冷却没走完就什么都不做。</summary>
    public void InviteClicked()
    {
        if (InviteCooling) return;
        LobbyInviteService svc = LobbyInviteService.Instance;
        if (svc == null) { LobbyToast.Show("邀请服务没起来"); return; }

        svc.Invite(_steamId, _displayName);
        TickInvite();                       // 立刻换成倒计时（不等下一帧）
    }

    /// <summary>按「能不能邀 / 在不在冷却 / 悬没悬停」刷新那一格。悬停与冷却的状态都问别人，这里不自己记。</summary>
    public void ApplyInviteVisual() { TickInvite(); }

    void TickInvite()
    {
        if (inviteGroup == null) return;

        if (!_canInvite)
        {
            if (inviteGroup.activeSelf) inviteGroup.SetActive(false);
            return;
        }
        if (!inviteGroup.activeSelf) inviteGroup.SetActive(true);

        float left = _steamId != 0UL ? LobbyInviteService.CooldownLeft(_steamId) : 0f;
        bool cooling = left > 0f;

        if (_inviteButton == null) _inviteButton = inviteGroup.GetComponent<FriendInviteButton>();
        bool hover = !cooling && _inviteButton != null && _inviteButton.Hovering;
        Texture want = cooling ? inviteCool : (hover ? inviteHover : inviteNormal);
        if (inviteIcon != null && want != null && inviteIcon.texture != want) inviteIcon.texture = want;

        if (inviteTimer != null)
        {
            if (inviteTimer.gameObject.activeSelf != cooling) inviteTimer.gameObject.SetActive(cooling);
            if (cooling)
            {
                string s = Mathf.Max(1, Mathf.CeilToInt(left)).ToString();
                if (inviteTimer.text != s) inviteTimer.text = s;
            }
        }
    }

    /// <summary>request = true 时真的去问 Steam（顺带触发下载）；之后只查缓存，省得刷日志。</summary>
    void ApplyAvatar(bool request)
    {
        if (avatarImage == null) return;

        Texture2D tex = null;
        if (_steamId != 0UL)
        {
            tex = request ? SteamAvatarManager.GetAvatarTexture(_steamId) : SteamAvatarManager.PeekAvatar(_steamId);
            if (tex != null) tex = PlayerProfilePanel.CircleCrop(tex);   // Steam 头像是方的，直接铺会把金环吃掉
        }
        if (tex == null) tex = PlayerProfilePanel.Placeholder();
        if (tex != null && avatarImage.texture != tex) avatarImage.texture = tex;
    }
}
