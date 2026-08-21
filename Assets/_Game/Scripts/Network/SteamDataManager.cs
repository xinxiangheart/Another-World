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
    }

    public double WinRate => playerData.totalMatches > 0
        ? (double)playerData.totalWins / playerData.totalMatches * 100.0
        : 0.0;

    private string _savePath;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _savePath = Path.Combine(Application.persistentDataPath, "player_data.json");
        LoadData();
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
            return;
        }

        localSteamID = SteamUser.GetSteamID();
        localPlayerName = SteamFriends.GetPersonaName();
        // 常态存储本地 SteamID（PhaseWheel 己方头像 / 对手 SteamID 上报用）
        LobbyConfig.LocalSteamID = localSteamID.m_SteamID;

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
