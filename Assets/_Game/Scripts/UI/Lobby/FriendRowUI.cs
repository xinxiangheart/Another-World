using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友列表里的一行：Steam 头像（圆裁进金环）+ 昵称 + 状态小字。
/// 场景里只有一份**模板**（inactive），运行时由 <see cref="LobbyFriendListUI"/> 克隆 + <see cref="Bind"/>。
/// 头像：先把井里铺一块灰盘占位（别留黑洞），Steam 那头像到货后自己换上去。
/// </summary>
public class FriendRowUI : MonoBehaviour
{
    [Header("头像（金环井里那块，运行时圆裁）")] public RawImage avatarImage;
    [Header("文字")] public TMP_Text nameText;
    public TMP_Text statusText;

    const float AvatarRetryStep = 0.5f;      // 到货前 0.5 秒问一次
    const float AvatarGiveUpSeconds = 20f;   // 20 秒还没到就认了（保持灰盘）

    ulong _steamId;
    float _nextTryAt;
    float _giveUpAt;

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
        _nextTryAt = 0f;
        _giveUpAt = Time.unscaledTime + AvatarGiveUpSeconds;
        ApplyAvatar(true);
    }

    void Update()
    {
        if (_steamId == 0UL) return;                                    // 没有 Steam 身份：灰盘就够了
        if (Time.unscaledTime < _nextTryAt) return;
        if (Time.unscaledTime > _giveUpAt) return;
        _nextTryAt = Time.unscaledTime + AvatarRetryStep;
        ApplyAvatar(false);
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
