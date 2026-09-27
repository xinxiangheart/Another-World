using System;
using System.IO;
using UnityEngine;
using Steamworks;
using Mirror;

/// <summary>
/// Steam 头像+昵称 + 本地持久化数据存储（更新不丢失）。
/// 挂在 NetworkManager 或任意启动 GameObject 上。
/// </summary>
public class SteamDataManager : MonoBehaviour
{
    public static SteamDataManager Instance { get; private set; }

    // ===== Steam 信息（可运行时动态获取） =====
    public string localPlayerName { get; private set; } = "未知玩家";
    public Texture2D localAvatar { get; private set; }
    public CSteamID localSteamID { get; private set; }
    public string opponentPlayerName { get; private set; } = "对手";

    // ===== 玩家数据 =====
    public PlayerSaveData playerData = new PlayerSaveData();

    [System.Serializable]
    public class PlayerSaveData
    {
        public int totalWins;
        public int totalLosses;
        public int totalMatches;
        public int winStreak;
        public int lossStreak;
        public int bestWinStreak;
        public int bestLossStreak;
        public string lastPlayedVersion = "";
        public string playerName = "";
        public string playerId = "";        // 玩家唯一 ID（P00-20260927-482-6）—— 生成后永不变
        public string playerIdSteam = "";   // 该 ID 绑定的 SteamID64 文本（未连接 Steam 时为空）
    }

    public double WinRate => playerData.totalMatches > 0
        ? (double)playerData.totalWins / playerData.totalMatches * 100.0
        : 0.0;

    // ========== 玩家唯一 ID（「异界号」· A 方案：Steam 账号编进 ID） ==========

    /// <summary>本地玩家的唯一 ID（形如 P00-20260927-4-K3M79QX-8）。还没生成时返回空串。</summary>
    public string PlayerIdText => playerData != null ? (playerData.playerId ?? "") : "";

    /// <summary>ID 里编进去的 SteamID64 文本（未连接 Steam 时为空）。</summary>
    public string PlayerIdSteamText => playerData != null ? (playerData.playerIdSteam ?? "") : "";

    /// <summary>当前 ID 解出来的 SteamID64（加好友 / 直连用）；没有或格式不对返回 0。</summary>
    public ulong PlayerIdBoundSteamId => PlayerId.ResolveSteamId(PlayerIdText);

    /// <summary>「我的 ID：P00-20260927-4-K3M79QX-8　·　创建时赛季 00 · 2026-09-27」—— UI 直接铺这一行。</summary>
    public string PlayerIdLabel
    {
        get
        {
            string id = PlayerIdText;
            if (string.IsNullOrEmpty(id)) return "我的 ID：从 Steam 启动游戏后自动生成";
            string desc = PlayerId.Describe(id);
            return string.IsNullOrEmpty(desc)
                ? ("我的 ID：" + PlayerId.Pretty(id))
                : ("我的 ID：" + PlayerId.Pretty(id) + "　·　" + desc);
        }
    }

    /// <summary>确保存档里的 ID 是「绑着这个 Steam 账号」的 A 格式号。true = 这次改过，调用方要落盘。
    /// 只有三种情况会重写：① 还没有号；② 老格式（没编 Steam）→ 升级，**创建日沿用老号里那一天**；
    /// ③ 号里编的是**别的** Steam 账号（同一台机器换了 Steam 号）—— 不重写的话按 ID 找到的是错人。</summary>
    bool EnsurePlayerId(ulong steamId)
    {
        if (playerData == null) playerData = new PlayerSaveData();
        // 没 Steam 就不生成：这种号谁也没法按 ID 找到，生成了反而误导
        if (steamId == 0 || !PlayerId.IsEncodableSteamId(steamId)) return false;

        int season; DateTime created; ulong bound;
        bool ok = PlayerId.TryParse(playerData.playerId, out season, out created, out bound);
        if (ok && bound == steamId) return false;        // 已经是这个账号的号 —— ID 不可变，绝不动它

        DateTime createdAt = DateTime.Now;               // 默认今天
        if (!ok && PlayerId.TryParseLegacy(playerData.playerId, out season, out created))
            createdAt = created;                         // 老格式：把原来的创建日带过来

        playerData.playerId = PlayerId.Create(createdAt, steamId);
        playerData.playerIdSteam = steamId.ToString();
        Debug.Log($"[SteamData] 生成玩家 ID: {playerData.playerId}（绑 Steam {steamId}）");
        return true;
    }

    /// <summary>把 ID 绑到当前 SteamID64。steamId = 0（未连接 Steam / 离线模式）就什么都不做。</summary>
    void BindPlayerId(ulong steamId)
    {
        if (EnsurePlayerId(steamId)) SaveData();
    }

    private string _savePath;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _savePath = Path.Combine(Application.persistentDataPath, "player_data.json");
        LoadData();

        // 玩家唯一 ID 不在这儿生成 —— A 方案要把 Steam 账号编进 ID，得等 Start 里 Steam 就绪（见 BindPlayerId）
    }

    void Start()
    {
        LoadSteamProfile();
    }

    // ========== Steam 读取 ==========

    void LoadSteamProfile()
    {
        if (!SteamManager.Initialized)
        {
            // 非 Steam 启动：用本地存档的名字
            localPlayerName = string.IsNullOrEmpty(playerData.playerName) ? "冒险者" : playerData.playerName;
            Debug.Log($"[SteamData] Steam 未初始化，使用本地名: {localPlayerName}");
            BindPlayerId(0);                        // 未连接 Steam → 不生成 ID（这种号没人按 ID 找得到）
            return;
        }

        localSteamID = SteamUser.GetSteamID();
        localPlayerName = SteamFriends.GetPersonaName();
        // 常态存储本地 SteamID（PhaseWheel 己方头像 / 对手 SteamID 上报用）
        LobbyConfig.LocalSteamID = localSteamID.m_SteamID;
        BindPlayerId(localSteamID.m_SteamID);   // ID ← 把 SteamID64 编进去（没有 / 换了账号都会在这里补）

        // 同步到本地存档
        if (!string.IsNullOrEmpty(localPlayerName))
            playerData.playerName = localPlayerName;

        // 读取头像
        int avatarHandle = SteamFriends.GetLargeFriendAvatar(localSteamID);
        if (avatarHandle > 0)
        {
            uint width, height;
            if (SteamUtils.GetImageSize(avatarHandle, out width, out height))
            {
                byte[] pixels = new byte[width * height * 4];
                if (SteamUtils.GetImageRGBA(avatarHandle, pixels, (int)(width * height * 4)))
                {
                    localAvatar = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                    localAvatar.LoadRawTextureData(pixels);

                    // Steam 头像上下颠倒，翻转 Y
                    Color[] cols = localAvatar.GetPixels();
                    for (int y = 0; y < height / 2; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int top = y * (int)width + x;
                            int bottom = ((int)height - 1 - y) * (int)width + x;
                            Color tmp = cols[top];
                            cols[top] = cols[bottom];
                            cols[bottom] = tmp;
                        }
                    }
                    localAvatar.SetPixels(cols);
                    localAvatar.Apply();
                }
            }
        }

        // 预缓存本地头像到统一管理器（PhaseWheel 己方头像直接命中缓存）
        SteamAvatarManager.CacheAvatar(localSteamID.m_SteamID, localAvatar);

        Debug.Log($"[SteamData] 加载完成: {localPlayerName}, SteamID={localSteamID}");
    }

    /// <summary>记录对手名字（联机时由 NetworkPlayer 调用）</summary>
    public void SetOpponentName(string name)
    {
        opponentPlayerName = string.IsNullOrEmpty(name) ? "对手" : name;
    }

    // ========== 计分 ==========

    public void RecordWin()
    {
        playerData.winStreak++;
        playerData.lossStreak = 0;
        if (playerData.winStreak > playerData.bestWinStreak)
            playerData.bestWinStreak = playerData.winStreak;
        playerData.totalWins++;
        playerData.totalMatches++;
        SaveData();
    }

    public void RecordLoss()
    {
        playerData.lossStreak++;
        playerData.winStreak = 0;
        if (playerData.lossStreak > playerData.bestLossStreak)
            playerData.bestLossStreak = playerData.lossStreak;
        playerData.totalLosses++;
        playerData.totalMatches++;
        SaveData();
    }

    public void SetLastVersion(string ver)
    {
        playerData.lastPlayedVersion = ver;
        SaveData();
    }

    // ========== 持久化（Application.persistentDataPath，更新不丢失） ==========

    void SaveData()
    {
        try
        {
            string json = JsonUtility.ToJson(playerData, true);
            File.WriteAllText(_savePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamData] 保存失败: {e.Message}");
        }
    }

    void LoadData()
    {
        if (!File.Exists(_savePath)) return;
        try
        {
            string json = File.ReadAllText(_savePath);
            playerData = JsonUtility.FromJson<PlayerSaveData>(json) ?? new PlayerSaveData();
            Debug.Log($"[SteamData] 读取存档: {playerData.totalWins}胜/{playerData.totalLosses}负");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SteamData] 存档损坏: {e.Message}");
            playerData = new PlayerSaveData();
        }
    }
}
