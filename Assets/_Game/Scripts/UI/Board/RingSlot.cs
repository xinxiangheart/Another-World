using UnityEngine;
using UnityEngine.UI;
using Steamworks;

/// <summary>
/// 阶段轮盘上的单个环：管理空白/头像/攻击回合三种显示状态。
/// 结构：RingBackground（环主体）→ AvatarMask（圆形遮罩）→ AvatarImage（头像）。
/// 攻击回合（BattlePhase）时整个环主体替换为攻击图片，不显示头像。
/// </summary>
public class RingSlot : MonoBehaviour
{
    [Header("环元素")]
    public Image ringBackground;   // 环主体（空白环美术 / 攻击回合图片）
    public Image avatarImage;      // 头像显示（在 AvatarMask 圆形遮罩下）
    public Image iconImage;        // 预留的图标显示（当前不用）

    Sprite _defaultRing;           // 初始空白环 sprite，Awake 缓存

    void Awake()
    {
        _defaultRing = ringBackground != null ? ringBackground.sprite : null;
    }

    /// <summary>空白状态：环主体恢复空白环，隐藏头像/图标。</summary>
    public void SetEmpty()
    {
        if (ringBackground != null && _defaultRing != null) ringBackground.sprite = _defaultRing;
        if (avatarImage != null) avatarImage.enabled = false;
        if (iconImage != null) iconImage.enabled = false;
    }

    /// <summary>攻击回合：整个环主体替换为攻击图片（两剑交叉），隐藏头像。</summary>
    public void SetBattle(Sprite battle)
    {
        if (ringBackground != null && battle != null)
            ringBackground.sprite = battle;
        if (avatarImage != null) avatarImage.enabled = false;
        if (iconImage != null) iconImage.enabled = false;
    }

    /// <summary>显示头像（己方直接用 Texture2D，避免重复 Sprite.Create）。环主体恢复空白环。</summary>
    public void SetAvatar(Texture2D tex)
    {
        Debug.Log($"[RingSlot] SetAvatar: {name}, tex={(tex != null ? $"{tex.width}x{tex.height}" : "null")}, " +
                  $"avatarImage={(avatarImage != null ? $"(enabled={avatarImage.enabled}, sprite={(avatarImage.sprite != null ? avatarImage.sprite.texture?.width.ToString() : "null")})" : "null")}");
        if (ringBackground != null && _defaultRing != null) ringBackground.sprite = _defaultRing;
        if (avatarImage != null && tex != null)
        {
            // 仅在纹理变化时重建 Sprite，避免每帧分配
            if (avatarImage.sprite == null || avatarImage.sprite.texture != tex)
                avatarImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            avatarImage.enabled = true;
            Debug.Log($"[RingSlot] SetAvatar 后: avatarImage.enabled={avatarImage.enabled}, " +
                      $"sprite={(avatarImage.sprite != null ? avatarImage.sprite.texture?.width.ToString() : "null")}, " +
                      $"rect={avatarImage.rectTransform?.rect}");
        }
        if (iconImage != null) iconImage.enabled = false;
    }

    /// <summary>按 SteamID 加载头像（同步尝试；未就绪则留空）。</summary>
    public void SetAvatar(CSteamID steamID)
    {
        if (steamID.m_SteamID == 0) { SetEmpty(); return; }
        Texture2D tex = LoadAvatarFromSteamID(steamID);
        if (tex != null) SetAvatar(tex);
        else SetEmpty();
    }

    /// <summary>从 SteamID 获取头像纹理——统一走 SteamAvatarManager（大→中降级 + 缓存 + Y 翻转）。</summary>
    public static Texture2D LoadAvatarFromSteamID(CSteamID steamID)
        => SteamAvatarManager.GetAvatarTexture(steamID.m_SteamID);
}
