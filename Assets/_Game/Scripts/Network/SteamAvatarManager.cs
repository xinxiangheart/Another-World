using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

/// <summary>
/// Steam 头像统一获取 / 缓存管理器（Steamworks.NET）。
/// 通过 SteamID 获取玩家头像：大尺寸优先 → 中尺寸降级 → 未就绪返回 null（自动请求加载）。
/// 缓存按 SteamID 用字典区分，命中直接返回，避免重复请求 Steam API / 重复分配纹理。
/// 己方头像用 LobbyConfig.LocalSteamID，对方头像用 LobbyConfig.RemoteSteamID（AI 对战为 0 → null）。
///
/// 加载策略（多路兜底，不依赖单一机制）：
///   1. 缓存命中直接返回；
///   2. 轮询 GetLargeFriendAvatar（大→中降级）；
///   3. RequestUserInformation 触发异步加载；
///   4. AvatarImageLoaded_t + PersonaStateChange_t 双回调，加载完成即写缓存；
///   5. 主动轮询协程（10 秒内每 0.5s 重试，不依赖阶段预载频率）；
///   6. 都失败 → 返回灰色占位头像（避免空白环）。
/// </summary>
public static class SteamAvatarManager
{
    static readonly Dictionary<ulong, Texture2D> _avatarCache = new Dictionary<ulong, Texture2D>();
    static readonly HashSet<ulong> _polling = new HashSet<ulong>();
    static Callback<AvatarImageLoaded_t> _avatarLoadedCB;
    static Callback<PersonaStateChange_t> _personaCB;
    static bool _callbackReady;
    static Texture2D _defaultAvatar;

    /// <summary>
    /// 获取玩家头像（缓存优先；大尺寸 → 中尺寸降级）。
    /// 未就绪时触发 RequestUserInformation + 主动轮询，并返回占位头像（不缓存），
    /// 真头像加载完成后由回调/轮询写入缓存，后续调用返回真头像。
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

        // 触发异步加载 + 主动轮询（每 SteamID 只启动一次）
        SteamFriends.RequestUserInformation(cid, false);
        StartPolling(steamID);
        // 兜底：返回占位头像（不缓存），避免轮盘空白环
        return DefaultAvatar();
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

    /// <summary>注册头像加载回调（SteamManager.Update 里的 SteamAPI.RunCallbacks 派发）。</summary>
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
        _personaCB = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
        Debug.Log("[SteamAvatar] AvatarImageLoaded_t + PersonaStateChange_t 回调已注册");
    }

    /// <summary>Steam 头像加载完成（AvatarImageLoaded_t）：把纹理写入缓存。</summary>
    static void OnAvatarLoaded(AvatarImageLoaded_t cb)
    {
        ulong sid = cb.m_steamID.m_SteamID;
        if (sid == 0 || cb.m_iImage <= 0) return;
        Texture2D tex = LoadImageFromHandle(cb.m_iImage);
        if (tex != null)
        {
            _avatarCache[sid] = tex;
            Debug.Log($"[SteamAvatar] AvatarImageLoaded 已缓存头像: {sid} ({tex.width}x{tex.height})");
        }
    }

    /// <summary>用户数据变化（PersonaStateChange_t，含头像加载）：头像变更时尝试加载。</summary>
    static void OnPersonaStateChange(PersonaStateChange_t cb)
    {
        if (((int)cb.m_nChangeFlags & (int)EPersonaChange.k_EPersonaChangeAvatar) == 0) return;
        ulong sid = cb.m_ulSteamID;
        if (sid == 0) return;
        Texture2D tex = TryLoadAvatar(new CSteamID(sid), large: true) ?? TryLoadAvatar(new CSteamID(sid), large: false);
        if (tex != null)
        {
            _avatarCache[sid] = tex;
            Debug.Log($"[SteamAvatar] PersonaStateChange(头像) 已缓存: {sid} ({tex.width}x{tex.height})");
        }
    }

    /// <summary>主动轮询：10 秒内每 0.5s 重试加载头像（不依赖阶段预载频率）。</summary>
    static void StartPolling(ulong steamID)
    {
        if (steamID == 0 || !_polling.Add(steamID)) return;
        SteamAvatarPoller.Instance.StartCoroutine(PollAvatarRoutine(steamID));
    }

    static IEnumerator PollAvatarRoutine(ulong steamID)
    {
        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForSeconds(0.5f);
            if (_avatarCache.ContainsKey(steamID)) yield break;
            var cid = new CSteamID(steamID);
            Texture2D tex = TryLoadAvatar(cid, large: true) ?? TryLoadAvatar(cid, large: false);
            if (tex != null)
            {
                _avatarCache[steamID] = tex;
                Debug.Log($"[SteamAvatar] 轮询加载成功: {steamID} ({tex.width}x{tex.height})");
                yield break;
            }
            SteamFriends.RequestUserInformation(cid, false);
        }
    }

    static Texture2D TryLoadAvatar(CSteamID steamID, bool large)
    {
        int handle = large
            ? SteamFriends.GetLargeFriendAvatar(steamID)
            : SteamFriends.GetMediumFriendAvatar(steamID);
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

    /// <summary>生成一个灰色占位头像（头像因隐私/未加载而不可用时的兜底，避免空白环）。</summary>
    static Texture2D DefaultAvatar()
    {
        if (_defaultAvatar != null) return _defaultAvatar;
        _defaultAvatar = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var cols = new Color[64 * 64];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color(0.45f, 0.45f, 0.48f, 1f);
        _defaultAvatar.SetPixels(cols);
        _defaultAvatar.Apply();
        return _defaultAvatar;
    }
}

/// <summary>头像轮询协程宿主（SteamAvatarManager 是静态类无法 StartCoroutine，借用此 MonoBehaviour）。</summary>
public class SteamAvatarPoller : MonoBehaviour
{
    static SteamAvatarPoller _instance;
    public static SteamAvatarPoller Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SteamAvatarPoller");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<SteamAvatarPoller>();
            }
            return _instance;
        }
    }
}
