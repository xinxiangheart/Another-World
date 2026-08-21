using System.Collections.Generic;
using UnityEngine;
using Steamworks;

/// <summary>
/// Steam 头像统一获取 / 缓存管理器（Steamworks.NET）。
/// 通过 SteamID 获取玩家头像：大尺寸优先 → 中尺寸降级 → 未就绪返回 null（自动请求加载，下次重试）。
/// 缓存按 SteamID 用字典区分，命中直接返回，避免重复请求 Steam API / 重复分配纹理。
/// 己方头像用 LobbyConfig.LocalSteamID，对方头像用 LobbyConfig.RemoteSteamID（AI 对战为 0 → null）。
/// </summary>
public static class SteamAvatarManager
{
    static readonly Dictionary<ulong, Texture2D> _avatarCache = new Dictionary<ulong, Texture2D>();

    /// <summary>
    /// 获取玩家头像（缓存优先；大尺寸 → 中尺寸降级）。
    /// 未就绪（GetLarge/MediumFriendAvatar 返回 0）时触发 RequestUserInformation 并返回 null，
    /// 调用方可在下一帧/下一阶段重试。
    /// </summary>
    public static Texture2D GetAvatarTexture(ulong steamID)
    {
        if (steamID == 0) return null;
        if (_avatarCache.TryGetValue(steamID, out Texture2D cached) && cached != null)
            return cached;

        var cid = new CSteamID(steamID);
        Texture2D tex = TryLoadAvatar(cid, large: true) ?? TryLoadAvatar(cid, large: false);
        if (tex != null)
        {
            _avatarCache[steamID] = tex;
            return tex;
        }

        // 头像尚未就绪——请求 Steam 加载，下次调用重试
        SteamFriends.RequestUserInformation(cid, false);
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

    static Texture2D TryLoadAvatar(CSteamID steamID, bool large)
    {
        int handle = large
            ? SteamFriends.GetLargeFriendAvatar(steamID)
            : SteamFriends.GetMediumFriendAvatar(steamID);
        if (handle <= 0) return null;
        if (!SteamUtils.GetImageSize(handle, out uint w, out uint h) || w == 0 || h == 0) return null;

        byte[] px = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(handle, px, (int)(w * h * 4))) return null;

        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(px);
        // Steam 头像上下颠倒，翻转 Y（与 SteamDataManager 一致）
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
