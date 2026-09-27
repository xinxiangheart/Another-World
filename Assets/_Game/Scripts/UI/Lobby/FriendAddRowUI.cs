using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「添加好友」结果列表里的一行：长矩形子背景 + 头像 + 名称 + 异界号 + 状态 + 右端一颗「+」。
/// </summary>
/// <remarks>2026-09-27 用户：「然后会在下方列出所有相关玩家（一般是输入昵称时可以有多个玩家）（也是能滑动的）」。
///
/// 底板复用好友列表那张 LobbyFriendRow.png（1360x96）—— 搜索结果也是「一行玩家」，与好友表同一套肢体语言；
/// 差别只有两处：① 名称后面跟的是**异界号**（不是状态直排）；② 右端只有**一颗**「+」，没有拉黑 / 删除 / 邀请三格。
///
/// 「+」只在 FriendAddState.Addable 时出现（已是好友 / 只是 Steam 好友都不出）——
/// 与好友行那颗「邀请 +」同一条口径：**点不动的动作就别画出来**。
///
/// 头像：先铺灰盘占位，Steam 那头像到货后自己换上去（与好友列表那行同一套）。
/// </remarks>
public class FriendAddRowUI : MonoBehaviour
{
    [Header("左：金环 + 井里头像")] public RawImage avatarImage;

    [Header("文字：名称 / 异界号 / 状态")] public TMP_Text nameText;
    public TMP_Text idText;
    public TMP_Text statusText;

    [Header("右端那颗「+」（只有「可添加」才出现）")] public GameObject addGroup;

    /// <summary>只知道 Steam 身份、还没注册过本游戏时，异界号那一列就写这句（列不留空）。</summary>
    const string NoIdLabel = "未绑定异界号";
    /// <summary>名字问不到时（搜一个只在异界号里见过的陌生人）就用这句。</summary>
    const string NoNameLabel = "未知玩家";

    const float AvatarRetryStep = 0.5f;      // 到货前 0.5 秒问一次
    const float AvatarGiveUpSeconds = 20f;   // 20 秒还没到就认了（保持灰盘）

    FriendSearchHit _hit;
    ulong _steamId;
    float _nextTryAt;
    float _giveUpAt;

    /// <summary>这一行现在挂的是谁（点「+」的时候用）。</summary>
    public FriendSearchHit Hit { get { return _hit; } }

    /// <summary>填一行。h == null → 直接藏起来。</summary>
    public void Bind(FriendSearchHit h)
    {
        if (h == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        _hit = h;
        _steamId = h.steamId;

        // 三格都画满（用户 2026-09-27：搜异界号 / 搜昵称，「要展示全（头像，名称和id）」）—— 缺的那格用占位词，不留空。
        if (nameText != null) nameText.text = string.IsNullOrEmpty(h.displayName) ? NoNameLabel : h.displayName;
        if (idText != null) idText.text = string.IsNullOrEmpty(h.playerId) ? NoIdLabel : h.playerId;
        if (statusText != null)
        {
            statusText.text = StateLabel(h.state);
            statusText.color = StateColor(h.state);
        }
        if (addGroup != null) addGroup.SetActive(h.state == FriendAddState.Addable);

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

    /// <summary>右端那颗「+」转过来的点击。</summary>
    public void Action()
    {
        if (_hit == null || _hit.state != FriendAddState.Addable) return;   // 保险：不可加的一律不发

        string msg;
        bool ok = FriendAddSearch.TryAdd(_hit, out msg);
        LobbyToast.Show(msg);
        if (!ok) return;

        FriendAddSearchUI ui = GetComponentInParent<FriendAddSearchUI>(true);
        if (ui != null) ui.RunSearch();      // 重搜一遍：刚加上的这一行立刻变成「已是好友」
    }

    static string StateLabel(FriendAddState s)
    {
        switch (s)
        {
            case FriendAddState.Existing:    return "已是好友";
            case FriendAddState.SteamFriend: return "Steam 好友";
            default:                         return "可添加";
        }
    }

    static Color StateColor(FriendAddState s)
    {
        if (s == FriendAddState.Addable) return new Color32(228, 203, 132, 255);   // 亮金 #E4CB84
        return new Color32(142, 162, 180, 205);                                     // 钢 #8EA2B4
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
