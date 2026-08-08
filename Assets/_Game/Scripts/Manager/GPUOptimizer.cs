using UnityEngine;

/// <summary>
/// 全局 GPU 优化——启动时自动执行，无需挂载到场景。
/// </summary>
public static class GPUOptimizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        // 1. 限 60fps（卡牌游戏不需要更高）
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // 2. 降 MSAA（UI 游戏不需要多重采样）
        QualitySettings.antiAliasing = 0;

        // 3. 降画质到 Medium（关阴影、关软粒子、关反射探针）
        QualitySettings.SetQualityLevel(2, true);

        // 4. 关掉所有相机的 HDR + MSAA（省显存）
        var allCams = Object.FindObjectsOfType<Camera>();
        foreach (var cam in allCams)
        {
            cam.allowHDR = false;
            cam.allowMSAA = false;
        }
    }
}
