using System.IO;
using UnityEngine;

/// <summary>
/// Writes steam_appid.txt next to the .exe before SteamManager initializes.
/// In the Editor, Steamworks.NET auto-creates this file in the project root.
/// In a standalone build, the file does not exist and SteamAPI.Init() fails silently.
///
/// Must run BEFORE SteamManager.Awake() — uses BeforeSceneLoad.
/// </summary>
public static class SteamAppIDBootstrapper
{
    const string APP_ID = "480"; // Spacewar dev AppID

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureSteamAppId()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        string exeDir = Path.GetDirectoryName(Application.dataPath);
        string appIdPath = Path.Combine(exeDir, "steam_appid.txt");

        if (!File.Exists(appIdPath))
        {
            Debug.Log($"[SteamAppIDBootstrapper] Writing {appIdPath}");
            File.WriteAllText(appIdPath, APP_ID);
        }
#else
        // Editor: the file in the project root is auto-detected by Steamworks.NET
#endif
    }
}
