using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友详情「好友列表」里的一行：长矩形子背景 + 头像 + 名称 + id + 状态 + 右端三格动作。
/// </summary>
/// <remarks>2026-09-27 用户：「好友列表（后续每个独立的玩家好友基本上都是按照这样）（上限50个好友），允许滑动，
/// 每个好友有个独立的长矩形子背景，从左到右分别是头像，名称，id（这个字体小一点），然后中间可以留空，
/// 右边分别是当前状态（在线/离线什么的），拉黑，删除，（仅在在线状态下）邀请，拉黑删除和邀请都是小ui图案代替文字，
/// 每个好友之间是有一点间隔，和底框也有间隔，右下角是以类似 23/50 小字这种形式展示好友数量」。
///
/// 场景里只有一份**模板**（inactive），运行时由 <see cref="LobbyFriendDetailListUI"/> 克隆 + <see cref="Bind"/>。
/// 三格动作的位置是**固定的**（拉黑 / 删除 / 邀请 三格都在），邀请那格只是「非空闲在线」时**藏起来** ——
/// 这样拉黑 / 删除不会因为状态变化而跳位。
///
/// 邀请的口径（用户 2026-09-27 两次强调）：**只有对方「空闲在线」才出邀请图标** ——
/// 对局中 / 匹配中 / 离线都不出（<see cref="FriendPresence.Online"/> 就是「没在匹配、也没在对局」那一档）。
///
/// 头像：先铺一块灰盘占位（别留黑洞），Steam 那头像到货后自己换上去（与好友侧边栏那行同一套）。
/// </remarks>
public class FriendDetailRowUI : MonoBehaviour
{
    [Header("左：金环 + 井里头像")] public RawImage avatarImage;

    [Header("文字：名称 / 异界号 / 状态")]
    public TMP_Text nameText;
    public TMP_Text idText;
    public TMP_Text statusText;

    [Header("右：三格动作（邀请那格只在「空闲在线」出现）")]
    public GameObject inviteGroup;

    const float AvatarRetryStep = 0.5f;      // 到货前 0.5 秒问一次
    const float AvatarGiveUpSeconds = 20f;   // 20 秒还没到就认了（保持灰盘）

    FriendEntry _entry;
    ulong _steamId;
    bool _canInvite;
    float _nextTryAt;
    float _giveUpAt;

    /// <summary>填一行。e == null → 直接藏起来。</summary>
    public void Bind(FriendEntry e)
    {
        if (e == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        _entry = e;
        _steamId = e.SteamId;

        if (nameText != null) nameText.text = e.DisplayName;
        if (idText != null) idText.text = string.IsNullOrEmpty(e.playerId) ? "" : e.playerId;
        if (statusText != null)
        {
            statusText.text = e.StatusLabel;
            statusText.color = e.StatusColor;
        }

        // 用户口径：只有「空闲在线」（既不在匹配、也不在对局）才给邀请；另外没有 Steam 身份就没法邀
        _canInvite = e.Presence == FriendPresence.Online && e.SteamId != 0UL;
        if (inviteGroup != null) inviteGroup.SetActive(_canInvite);

        _nextTryAt = 0f;
        _giveUpAt = Time.unscaledTime + AvatarGiveUpSeconds;
        ApplyAvatar(true);
    }

    void Update() { TickAvatar(); }

    void TickAvatar()
    {
        if (_steamId == 0UL) return;                                    // 没有 Steam 身份：灰盘就够了
        if (Time.unscaledTime < _nextTryAt) return;
        if (Time.unscaledTime > _giveUpAt) return;
        _nextTryAt = Time.unscaledTime + AvatarRetryStep;
        ApplyAvatar(false);
    }

    /// <summary>三格动作转过来的点击。</summary>
    public void Action(FriendRowActionKind kind)
    {
        if (_entry == null) return;

        switch (kind)
        {
            case FriendRowActionKind.Invite:
                if (!_canInvite) return;                                // 保险：非空闲在线一律不发
                LobbyInviteService invites = LobbyInviteService.Instance;
                if (invites == null) { LobbyToast.Show("邀请服务没起来"); return; }
                invites.Invite(_steamId, _entry.DisplayName);
                break;

            case FriendRowActionKind.Delete:
                DeleteMe();
                break;

            case FriendRowActionKind.Block:
                // 拉黑的口径用户还没定（是只屏蔽名单，还是连匹配也不碰他）—— 先明确报个待接入
                LobbyToast.Show("拉黑还没接（口径待定）");
                break;
        }
    }

    /// <summary>
    /// 删除：在本地名单里留一条**墓碑**（<c>FriendEntry.removed</c>），由
    /// <see cref="FriendListService.Refresh"/> 过滤掉。
    ///
    /// 为什么不是直接 <c>FriendStore.Remove</c>：好友表是从「Steam 好友 + 玩过本游戏」**推**出来的，
    /// 每 20 秒重扫一次。本地记录一删，下一轮他就会从 Steam 那条路重新被收回来 —— 看着像删了没反应。
    /// 留墓碑才真的删得掉；日后要恢复，把墓碑清掉即可（异界号手动加好友时 <c>FriendStore.AddManual</c> 会清）。
    /// </summary>
    void DeleteMe()
    {
        string key = !string.IsNullOrEmpty(_entry.playerId) ? _entry.playerId : _entry.steamIdText;
        List<FriendEntry> store = FriendStore.Load();

        FriendEntry rec = FriendStore.FindBySteam(store, _steamId);
        if (rec == null && !string.IsNullOrEmpty(_entry.playerId)) rec = FriendStore.FindByPlayerId(store, _entry.playerId);
        if (rec == null)
        {
            rec = new FriendEntry();
            rec.steamIdText = _entry.steamIdText;
            rec.playerId = _entry.playerId;
            rec.name = _entry.name;
            store.Add(rec);
        }
        rec.removed = true;
        FriendStore.Save(store);

        if (FriendListService.Instance != null) FriendListService.Instance.Refresh();
        LobbyToast.Show("已删除「" + _entry.DisplayName + "」");
        Debug.Log("[FriendDetail] 删除「" + _entry.DisplayName + "」（key=" + key + "）→ 留墓碑，重扫不会再收回来");
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