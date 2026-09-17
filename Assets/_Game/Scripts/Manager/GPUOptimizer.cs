using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局 GPU 优化——启动时自动执行，无需挂载到场景。
///
/// 2026-09-17 改：画质档 / 帧率 / 垂直同步 / 窗口模式 / 音量 / 界面缩放全部交给 <see cref="GameSettings"/>，
/// 玩家可以在设置面板里改。此前硬编码的 SetQualityLevel(5) / vSyncCount / targetFrameRate
/// 会在每次加载场景时把玩家设置冲掉，已移除。
///
/// 本类只保留「不进设置面板的渲染开关」：相机 HDR 与场景级重应用。
/// MSAA / 各向异性过滤放在 GameSettings.ApplyQuality() 里 —— 那样无论谁触发画质变更都会一并生效，
/// 不依赖本类与 SettingsRuntime 的执行先后。
/// </summary>
public static class GPUOptimizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplySettings();
        DisableCameraHdr();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 新场景有自己的 AudioListener / CanvasScaler / 相机，需要重新应用一次
        ApplySettings();
        DisableCameraHdr();
    }

    static void ApplySettings()
    {
        GameSettings.Load();
        GameSettings.ApplyAll();
        Debug.Log($"[GPUOptimizer] quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}({QualitySettings.GetQualityLevel()}) "
                + $"tier={GameSettings.QualityTier} msaa={QualitySettings.antiAliasing} aniso={QualitySettings.anisotropicFiltering} "
                + $"vSync={QualitySettings.vSyncCount} targetFps={Application.targetFrameRate} screen={Screen.width}x{Screen.height}");
    }

    static void DisableCameraHdr()
    {
        // 只关相机的 HDR（省显存；项目无后处理，不影响清晰度）。
        // 注意：不要设 cam.allowMSAA=false——它会取消 GameSettings 里设的 MSAA=2，Play 模式仍会锯齿。
        var allCams = Object.FindObjectsOfType<Camera>();
        for (int i = 0; i < allCams.Length; i++)
        {
            var cam = allCams[i];
            if (cam == null) continue;
            cam.allowHDR = false;
        }
    }
}