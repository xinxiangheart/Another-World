using System.Collections.Generic;
using UnityEngine;
using Steamworks;

/// <summary>
/// Steam 头像统一获取 / 缓存管理器（Steamworks.NET）。
/// 通过 SteamID 获取玩家头像：大尺寸优先 → 中尺寸降级 → 未就绪返回 null（自动请求加载）。
/// 缓存按 SteamID 用字典区分，命中直接返回，避免重复请求 Steam API / 重复分配纹理。
/// 己方头像用 LobbyConfig.LocalSteamID，对方头像用 LobbyConfig.RemoteSteamID（AI 对战为 0 → null）。
///
/// 关键：注册 AvatarImageLoaded_t 回调——Steam 头像异步加载完成后立即把纹理写入缓存。
/// 否则只靠轮询 GetLargeFriendAvatar（返回 0 直到加载完成），对一方可能永远拿不到对方头像。
/// </summary>
public static class SteamAvatarManager
{
    static readonly Dictionary<ulong, Texture2D> _avatarCache = new Dictionary<ulong, Texture2D>();
    static Callback<AvatarImageLoaded_t> _avatarLoadedCB;
    static bool _callbackReady;

    /// <summary>
    /// 获取玩家头像（缓存优先；大尺寸 → 中尺寸降级）。
    /// 未就绪时触发 RequestUserInformation（异步加载，完成由 AvatarImageLoaded_t 回调写入缓存）并返回 null，
    /// 调用方可在下一帧/下一阶段重试（届时缓存已命中）。
    /// </summary>
    public static Texture2D GetAvatarTexture(ulong steamID)
    {
        if (steamID == 0) return null;
        if (_avatarCache.TryGetValue(steamID, out Texture2D cached) && cached != null)
            return cached;

        EnsureCallback();
        var cid = new CSteamID(steamID);
        Texture2D tex = TryLoadAvatar(cid, large: true) ?? TryLoadAvatar(cid, large: false);
        if (tex != null)
        {
            _avatarCache[steamID] = tex;
            return tex;
        }

        // 头像尚未就绪——请求 Steam 加载；完成后 AvatarImageLoaded_t 回调会写入缓存
        SteamFriends.RequestUserInformation(cid, false);
        Debug.Log($"[SteamAvatar] RequestUserInformation 已请求: {steamID}");
        return null;
    }

    /// <summary>把已加载的头像预存进缓存（供大厅阶段提前缓存，避免对局中首帧缺失）。</summary>
    public static void CacheAvatar(ulong steamID, Texture2D tex)
    {
        if (steamID == 0 || tex == null) return;
        _avatarCache[steamID] = tex;
    }

    /// <summary>按 SteamID 从缓存取头像（不触发网络/请求；未缓存返回 null）。</summary>
    public static Texture2D PeekAvatar(ulong steamID)
    {
        if (steamID == 0) return null;
        return _avatarCache.TryGetValue(steamID, out Texture2D tex) ? tex : null;
    }

    /// <summary>注册头像加载完成回调（SteamManager.Update 里的 SteamAPI.RunCallbacks 派发）。</summary>
    static void EnsureCallback()
    {
        if (_callbackReady) return;
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("[SteamAvatar] EnsureCallback: Steam 未初始化，回调未注册");
            return;
        }
        _callbackReady = true;
        _avatarLoadedCB = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);
        Debug.Log("[SteamAvatar] AvatarImageLoaded_t 回调已注册");
    }

    /// <summary>Steam 头像加载完成：把纹理写入缓存，后续 GetAvatarTexture 直接命中。</summary>
    static void OnAvatarLoaded(AvatarImageLoaded_t cb)
    {
        ulong sid = cb.m_steamID.m_SteamID;
        Debug.Log($"[SteamAvatar] AvatarImageLoaded 回调触发: steamID={sid}, imageHandle={cb.m_iImage}");
        if (sid == 0 || cb.m_iImage <= 0) return;
        Texture2D tex = LoadImageFromHandle(cb.m_iImage);
        if (tex != null)
        {
            _avatarCache[sid] = tex;
            Debug.Log($"[SteamAvatar] 回调已缓存头像: {sid} ({tex.width}x{tex.height})");
        }
        else
        {
            Debug.LogWarning($"[SteamAvatar] 回调触发但图像加载失败: steamID={sid}, handle={cb.m_iImage}");
        }
    }

    static Texture2D TryLoadAvatar(CSteamID steamID, bool large)
    {
        int handle = large
            ? SteamFriends.GetLargeFriendAvatar(steamID)
            : SteamFriends.GetMediumFriendAvatar(steamID);
        if (handle <= 0)
            Debug.Log($"[SteamAvatar] {(large ? "GetLargeFriendAvatar" : "GetMediumFriendAvatar")} 返回 0（steamID={steamID.m_SteamID}，头像未就绪）");
        return LoadImageFromHandle(handle);
    }

    /// <summary>从 Steam 图像句柄生成 Texture2D（翻转 Y，与 SteamDataManager 一致）。</summary>
    static Texture2D LoadImageFromHandle(int handle)
    {
        if (handle <= 0) return null;
        if (!SteamUtils.GetImageSize(handle, out uint w, out uint h) || w == 0 || h == 0) return null;

        byte[] px = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(handle, px, (int)(w * h * 4))) return null;

        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(px);
        // Steam 头像上下颠倒，翻转 Y
        Color[] cols = tex.GetPixels();
        for (int y = 0; y < h / 2; y++)
        {
            for (int x = 0; x < (int)w; x++)
            {
                int top = y * (int)w + x, bot = ((int)h - 1 - y) * (int)w + x;
                (cols[top], cols[bot]) = (cols[bot], cols[top]);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }
}
