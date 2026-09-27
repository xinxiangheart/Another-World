using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>右侧大格子里那两格（申请列表 / 黑名单）共用一种行 —— 区别只在右端那几枚动作。</summary>
public enum FriendPanelMode
{
    Request = 0,   // 申请列表：右端「同意（勾）+ 拒绝（叉）」
    Block   = 1,   // 黑名单：右端只有「取消拉黑」
}

/// <summary>
/// 「申请列表 / 黑名单」里的一行：长矩形子背景 + 头像 + 名称 + 异界号 + 状态 + 右端动作。
/// </summary>
/// <remarks>2026-09-27 用户：「申请列表和黑名单一起做，展示申请加好友的列表（均在右下角类似限制50）
/// **只在右边显示不同**，申请列表有一个勾和叉的ui图案用于同意和申请，黑名单则只有一个取消拉黑的」。
///
/// 「只在右边显示不同」是照着做的：左半（环 64 / 井 50 / 名称 340@96 / 异界号 480@456 / 状态 240 宽右对齐）
/// 与「添加好友」那行逐个数一样（**唯一一处让步**：申请列表右边是两枚动作，状态盒的右沿必须从 1272 收到 1228
/// 才不压「同意」—— 见 `LobbyUIBuilder.BuildFriendPanelRow` 的 `statusR`；旧记的「状态 240@1032」只对黑名单成立）
/// 与「添加好友」那行**逐个数都一样**，底板也是同一张 `LobbyFriendRow.png`；差的就是右端：
///   申请列表 → 勾 x1240（次）+ 叉 x1296（主）；黑名单 → 取消拉黑 x1296。
/// 勾 / 叉 / 取消拉黑三枚徽章由 `Tools/cardframe/FriendRequestV1.ps1` 出（与拉黑 / 删除同一支笔）。
///
/// 动作**不挂 Button**：与好友行那三格同一套（同物体上再挂 Button 会有两个 IPointerClickHandler，
/// 各触发一次）—— 见 <see cref="FriendRowAction"/>。
/// </remarks>
public class FriendPanelRowUI : MonoBehaviour
{
    [Header("这一行是哪个面板的（决定动作怎么落地）")] public FriendPanelMode mode;

    [Header("左：金环 + 井里头像")] public RawImage avatarImage;

    [Header("文字：名称 / 异界号 / 状态")] public TMP_Text nameText;
    public TMP_Text idText;
    public TMP_Text statusText;

    [Header("右端动作（按 mode 只开对应的）")] public GameObject actAccept;   // 申请列表：同意
    public GameObject actRefuse;                                            // 申请列表：拒绝
    public GameObject actUnblock;                                           // 黑名单：取消拉黑

    const float AvatarRetryStep = 0.5f;      // 到货前 0.5 秒问一次
    const float AvatarGiveUpSeconds = 20f;   // 20 秒还没到就认了（保持灰盘）
    const string NoIdLabel = "未绑定异界号";
    const string NoNameLabel = "未知玩家";

    /// <summary>「已拉黑」用的生命红 #B64848（与好友行 <c>FriendDetailRowUI.BlockedColor</c> 同一档）。</summary>
    static readonly Color32 BlockedColor = new Color32(182, 72, 72, 235);

    FriendEntry _entry;
    ulong _steamId;
    float _nextTryAt;
    float _giveUpAt;

    /// <summary>这一行现在挂的是谁（点动作时用）。</summary>
    public FriendEntry Entry { get { return _entry; } }

    /// <summary>填一行。e == null → 直接藏起来。</summary>
    public void Bind(FriendEntry e)
    {
        if (e == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        _entry = e;
        _steamId = e.SteamId;

        if (nameText != null) nameText.text = string.IsNullOrEmpty(e.DisplayName) ? NoNameLabel : e.DisplayName;
        if (idText != null) idText.text = string.IsNullOrEmpty(e.playerId) ? NoIdLabel : e.playerId;
        if (statusText != null)
        {
            // 黑名单那格：状态位显示「已拉黑」（生命红 #B64848）—— 拉黑的人**不在好友列表里**了
            // （FriendListService.Refresh ④），那个红字信号归这一格（用户 2026-09-27 追加口径）。
            if (mode == FriendPanelMode.Block)
            {
                statusText.text = "已拉黑";
                statusText.color = BlockedColor;
            }
            else
            {
                statusText.text = e.StatusLabel;
                statusText.color = e.StatusColor;
            }
        }

        bool isRequest = mode == FriendPanelMode.Request;
        if (actAccept != null) actAccept.SetActive(isRequest);
        if (actRefuse != null) actRefuse.SetActive(isRequest);
        if (actUnblock != null) actUnblock.SetActive(!isRequest);

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

    /// <summary>右端某枚动作转过来的点击。</summary>
    public void Action(FriendRowActionKind kind)
    {
        if (_entry == null) return;

        switch (kind)
        {
            case FriendRowActionKind.Accept: Accept(); break;
            case FriendRowActionKind.Refuse: Refuse(); break;
            case FriendRowActionKind.Unblock: Unblock(); break;
        }
    }

    /// <summary>
    /// 同意：这一行**原样进好友表**（<see cref="FriendStore.AddManual"/>），再把申请删掉。
    /// 没有异界号的那种申请没法进好友表（好友表只认异界号）—— 那就**留着这一行**并说明原因，
    /// 不要把人的申请悄悄吃掉。
    /// </summary>
    void Accept()
    {
        if (mode != FriendPanelMode.Request) return;

        if (string.IsNullOrEmpty(_entry.playerId))
        {
            LobbyToast.Show("「" + _entry.DisplayName + "」还没有异界号，加不了");
            return;
        }

        List<FriendEntry> store = FriendStore.Load();
        bool added = FriendStore.AddManual(store, _entry.playerId, _entry.DisplayName, _steamId);
        if (added) FriendStore.Save(store);
        FriendRequestStore.RemoveEntry(_entry);
        LobbyToast.Show(added ? "已添加「" + _entry.DisplayName + "」" : "「" + _entry.DisplayName + "」已经是好友了");

        if (FriendListService.Instance != null) FriendListService.Instance.Refresh();
        Rebuild();
    }

    /// <summary>拒绝：只把这条申请删掉（不留墓碑 —— 对方以后还可以再申请）。</summary>
    void Refuse()
    {
        if (mode != FriendPanelMode.Request) return;

        FriendRequestStore.RemoveEntry(_entry);
        LobbyToast.Show("已拒绝「" + _entry.DisplayName + "」");
        Rebuild();
    }

    /// <summary>取消拉黑：把旗子放下来（好友关系本来就没动过 —— 见 FriendBlock.SetBlocked）。</summary>
    void Unblock()
    {
        if (mode != FriendPanelMode.Block) return;

        FriendBlock.SetBlocked(_entry.playerId, _steamId, _entry.DisplayName, false);
        LobbyToast.Show("已取消拉黑「" + _entry.DisplayName + "」");
        if (FriendListService.Instance != null) FriendListService.Instance.Refresh();
        Rebuild();
    }

    void Rebuild()
    {
        FriendPanelListUI list = GetComponentInParent<FriendPanelListUI>(true);
        if (list != null) list.Rebuild();
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
